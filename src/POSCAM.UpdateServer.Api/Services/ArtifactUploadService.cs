using System.Data;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Artifacts;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Repositories;
using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Api.Services;

/// <summary>
/// Draft Release에 ZIP Artifact를 신규 등록하거나 안전하게 교체한다.
/// 대용량 파일 처리 중에는 DB 트랜잭션을 유지하지 않는다.
/// </summary>
public sealed partial class ArtifactUploadService : IArtifactUploadService
{
    private readonly IDbContext _dbContext;
    private readonly IUpdateReleaseRepository _releaseRepository;
    private readonly IReleaseManagementQueryRepository _releaseQueryRepository;
    private readonly IUpdateArtifactRepository _artifactRepository;
    private readonly IArtifactManagementQueryRepository _artifactQueryRepository;
    private readonly IUpdateAuditLogRepository _auditLogRepository;
    private readonly IUpdateManagementActorAccessor _actorAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IArtifactStorageService _storageService;
    private readonly UpdateStorageOptions _storageOptions;
    private readonly ILogger<ArtifactUploadService> _logger;

    public ArtifactUploadService(
        IDbContext dbContext,
        IUpdateReleaseRepository releaseRepository,
        IReleaseManagementQueryRepository releaseQueryRepository,
        IUpdateArtifactRepository artifactRepository,
        IArtifactManagementQueryRepository artifactQueryRepository,
        IUpdateAuditLogRepository auditLogRepository,
        IUpdateManagementActorAccessor actorAccessor,
        IHttpContextAccessor httpContextAccessor,
        IArtifactStorageService storageService,
        IOptions<UpdateStorageOptions> storageOptions,
        ILogger<ArtifactUploadService> logger)
    {
        _dbContext = dbContext;
        _releaseRepository = releaseRepository;
        _releaseQueryRepository = releaseQueryRepository;
        _artifactRepository = artifactRepository;
        _artifactQueryRepository = artifactQueryRepository;
        _auditLogRepository = auditLogRepository;
        _actorAccessor = actorAccessor;
        _httpContextAccessor = httpContextAccessor;
        _storageService = storageService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<AdminServiceResult<ArtifactUploadResponse>> UploadAsync(
        long releaseCode,
        ArtifactUploadRequest? request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(
            releaseCode,
            request,
            _storageOptions.MaxUploadBytes,
            out var upload);

        if (validation is not null)
        {
            return validation;
        }

        var actor = _actorAccessor.Actor;
        if (actor is null)
        {
            return AdminServiceResult<ArtifactUploadResponse>.Fail(
                StatusCodes.Status503ServiceUnavailable,
                UpdateErrorCode.ExternalServiceUnavailable,
                "관리자 작업자 정보를 확인할 수 없습니다.");
        }

        var preliminaryRelease = await _releaseRepository.GetByCodeAsync(
            releaseCode,
            cancellationToken: cancellationToken);

        if (preliminaryRelease is null)
        {
            return ReleaseNotFound();
        }

        if (preliminaryRelease.ReleaseStatus != ReleaseStatus.Draft)
        {
            return InvalidReleaseState();
        }

        ArtifactStorageDestination? destination = null;
        StagedArtifactFile? stagedFile = null;
        var finalFileMoved = false;
        var databaseCommitted = false;

        try
        {
            destination = _storageService.CreateDestination(
                preliminaryRelease.ProductCode,
                preliminaryRelease.Channel,
                preliminaryRelease.Version,
                upload!.Architecture);

            await using var uploadStream = upload.File.OpenReadStream();

            stagedFile = await _storageService.SaveToStagingAsync(
                uploadStream,
                cancellationToken);

            await _storageService.ValidatePackageAsync(
                stagedFile,
                cancellationToken);

            // File.Move는 원자적인 짧은 작업이므로 클라이언트 취소와 분리한다.
            // 이동 후 취소 예외가 발생해 최종 파일을 놓치는 상태를 방지한다.
            await _storageService.MoveToPackagesAsync(
                stagedFile,
                destination,
                CancellationToken.None);
            finalFileMoved = true;

            await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var lockedRelease = await _releaseQueryRepository.GetByCodeForUpdateAsync(
                    releaseCode,
                    transaction,
                    cancellationToken);

                if (lockedRelease is null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return ReleaseNotFound();
                }

                if (lockedRelease.ReleaseStatus != ReleaseStatus.Draft)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return InvalidReleaseState();
                }

                var existing = await _artifactQueryRepository.GetByTargetForUpdateAsync(
                    releaseCode,
                    upload.OperatingSystem,
                    upload.Architecture,
                    upload.PackageType,
                    transaction,
                    cancellationToken);

                var artifact = CreateArtifact(
                    releaseCode,
                    upload,
                    destination,
                    stagedFile,
                    existing);

                var replaced = existing is not null;

                if (existing is null)
                {
                    artifact.ArtifactCode = await _artifactRepository.CreateAsync(
                        artifact,
                        transaction,
                        cancellationToken);
                }
                else
                {
                    var replacedRow = await _artifactRepository.ReplaceAsync(
                        artifact,
                        transaction,
                        cancellationToken);

                    if (!replacedRow)
                    {
                        await transaction.RollbackAsync(CancellationToken.None);

                        return AdminServiceResult<ArtifactUploadResponse>.Fail(
                            StatusCodes.Status409Conflict,
                            UpdateErrorCode.DuplicateArtifact,
                            "Artifact가 변경되어 교체할 수 없습니다.");
                    }
                }

                await CreateAuditAsync(
                    replaced
                        ? AuditActions.ReplaceDraftArtifact
                        : AuditActions.Upload,
                    existing,
                    artifact,
                    actor,
                    transaction,
                    cancellationToken);

                // DB 변경이 준비된 뒤에는 클라이언트 연결 취소와 무관하게 Commit을 확정한다.
                await transaction.CommitAsync(CancellationToken.None);
                databaseCommitted = true;

                if (existing is not null
                    && !string.Equals(
                        existing.StorageKey,
                        artifact.StorageKey,
                        StringComparison.Ordinal))
                {
                    var oldFileRemoved = await _storageService.RemoveOrQuarantineAsync(
                        existing.StorageKey,
                        CancellationToken.None);

                    if (!oldFileRemoved)
                    {
                        _logger.LogError(
                            "교체 완료 후 이전 Artifact 파일을 정리하지 못했습니다. ArtifactCode: {ArtifactCode}, RequestId: {RequestId}",
                            artifact.ArtifactCode,
                            _httpContextAccessor.HttpContext?.TraceIdentifier);
                    }
                }

                return AdminServiceResult<ArtifactUploadResponse>.Ok(
                    MapResponse(artifact, replaced),
                    replaced
                        ? "Draft Artifact를 교체했습니다."
                        : "Draft Artifact를 업로드했습니다.",
                    replaced
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status201Created);
            }
            catch (UpdateDatabaseException exception)
                when (exception.FailureType == DatabaseFailureType.Duplicate)
            {
                await SafeRollbackAsync(transaction);

                return AdminServiceResult<ArtifactUploadResponse>.Fail(
                    StatusCodes.Status409Conflict,
                    UpdateErrorCode.DuplicateArtifact,
                    "동일한 대상의 Artifact가 이미 존재합니다.");
            }
            catch
            {
                await SafeRollbackAsync(transaction);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArtifactStorageException exception)
        {
            return MapStorageFailure(exception);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "Artifact 업로드 Stream 처리 중 오류가 발생했습니다. ReleaseCode: {ReleaseCode}, RequestId: {RequestId}",
                releaseCode,
                _httpContextAccessor.HttpContext?.TraceIdentifier);

            return AdminServiceResult<ArtifactUploadResponse>.Fail(
                StatusCodes.Status500InternalServerError,
                UpdateErrorCode.StorageError,
                "Artifact 파일을 처리하지 못했습니다.");
        }
        finally
        {
            await _storageService.DeleteStagingAsync(
                stagedFile,
                CancellationToken.None);

            if (finalFileMoved
                && !databaseCommitted
                && destination is not null)
            {
                var cleaned = await _storageService.RemoveOrQuarantineAsync(
                    destination.StorageKey,
                    CancellationToken.None);

                if (!cleaned)
                {
                    _logger.LogError(
                        "DB 미반영 Artifact 파일을 정리하지 못했습니다. ReleaseCode: {ReleaseCode}, RequestId: {RequestId}",
                        releaseCode,
                        _httpContextAccessor.HttpContext?.TraceIdentifier);
                }
            }
        }
    }
}
