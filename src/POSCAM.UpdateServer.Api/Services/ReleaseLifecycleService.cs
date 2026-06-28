using System.Data;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Lifecycle;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Repositories;
using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Api.Services;

/// <summary>
/// Release 게시·일반 배포 중지와 Artifact 긴급 격리를 담당한다.
/// </summary>
public sealed partial class ReleaseLifecycleService : IReleaseLifecycleService
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
    private readonly ILogger<ReleaseLifecycleService> _logger;

    public ReleaseLifecycleService(
        IDbContext dbContext,
        IUpdateReleaseRepository releaseRepository,
        IReleaseManagementQueryRepository releaseQueryRepository,
        IUpdateArtifactRepository artifactRepository,
        IArtifactManagementQueryRepository artifactQueryRepository,
        IUpdateAuditLogRepository auditLogRepository,
        IUpdateManagementActorAccessor actorAccessor,
        IHttpContextAccessor httpContextAccessor,
        IArtifactStorageService storageService,
        ILogger<ReleaseLifecycleService> logger)
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
        _logger = logger;
    }

    public async Task<AdminServiceResult<ReleaseLifecycleResponse>> PublishAsync(
        long releaseCode,
        CancellationToken cancellationToken = default)
    {
        if (releaseCode <= 0)
        {
            return ReleaseNotFound<ReleaseLifecycleResponse>();
        }

        var actorFailure = RequireActor<ReleaseLifecycleResponse>(out var actor);
        if (actorFailure is not null)
        {
            return actorFailure;
        }

        await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var current = await _releaseQueryRepository.GetByCodeForUpdateAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (current is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return ReleaseNotFound<ReleaseLifecycleResponse>();
            }

            if (current.ReleaseStatus != ReleaseStatus.Draft)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return InvalidReleaseState<ReleaseLifecycleResponse>(
                    "Draft 상태의 릴리스만 게시할 수 있습니다.");
            }

            var artifacts = await _artifactRepository.GetActiveByReleaseAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (artifacts.Count == 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return AdminServiceResult<ReleaseLifecycleResponse>.Fail(
                    StatusCodes.Status409Conflict,
                    UpdateErrorCode.NoCompatibleArtifact,
                    "게시할 활성 Artifact가 없습니다.");
            }

            foreach (var artifact in artifacts)
            {
                try
                {
                    await _storageService.ValidateStoredArtifactAsync(
                        artifact.StorageKey,
                        artifact.FileSize,
                        artifact.Sha256,
                        cancellationToken);
                }
                catch (ArtifactStorageException exception)
                {
                    await transaction.RollbackAsync(CancellationToken.None);

                    _logger.LogWarning(
                        "게시 전 Artifact 무결성 검증에 실패했습니다. ReleaseCode: {ReleaseCode}, ArtifactCode: {ArtifactCode}, FailureType: {FailureType}, RequestId: {RequestId}",
                        releaseCode,
                        artifact.ArtifactCode,
                        exception.FailureType,
                        _httpContextAccessor.HttpContext?.TraceIdentifier);

                    return AdminServiceResult<ReleaseLifecycleResponse>.Fail(
                        StatusCodes.Status409Conflict,
                        UpdateErrorCode.PackageIntegrityError,
                        "Artifact 무결성 검증에 실패했습니다.");
                }
            }

            var published = await _releaseRepository.PublishAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (!published)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return InvalidReleaseState<ReleaseLifecycleResponse>(
                    "릴리스 상태가 변경되어 게시할 수 없습니다.");
            }

            var saved = await _releaseRepository.GetByCodeAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (saved is null)
            {
                throw new InvalidOperationException("게시된 릴리스를 다시 조회할 수 없습니다.");
            }

            await CreateReleaseAuditAsync(
                AuditActions.Publish,
                current,
                saved,
                actor!,
                transaction,
                cancellationToken);

            await transaction.CommitAsync(CancellationToken.None);

            return AdminServiceResult<ReleaseLifecycleResponse>.Ok(
                MapRelease(saved),
                "릴리스를 게시했습니다.");
        }
        catch
        {
            await SafeRollbackAsync(transaction);
            throw;
        }
    }

    public async Task<AdminServiceResult<ReleaseLifecycleResponse>> DisableAsync(
        long releaseCode,
        CancellationToken cancellationToken = default)
    {
        if (releaseCode <= 0)
        {
            return ReleaseNotFound<ReleaseLifecycleResponse>();
        }

        var actorFailure = RequireActor<ReleaseLifecycleResponse>(out var actor);
        if (actorFailure is not null)
        {
            return actorFailure;
        }

        await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var current = await _releaseQueryRepository.GetByCodeForUpdateAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (current is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return ReleaseNotFound<ReleaseLifecycleResponse>();
            }

            if (current.ReleaseStatus != ReleaseStatus.Published)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return InvalidReleaseState<ReleaseLifecycleResponse>(
                    "Published 상태의 릴리스만 배포 중지할 수 있습니다.");
            }

            var disabled = await _releaseRepository.DisableAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (!disabled)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return InvalidReleaseState<ReleaseLifecycleResponse>(
                    "릴리스 상태가 변경되어 배포 중지할 수 없습니다.");
            }

            var saved = await _releaseRepository.GetByCodeAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (saved is null)
            {
                throw new InvalidOperationException("중지된 릴리스를 다시 조회할 수 없습니다.");
            }

            await CreateReleaseAuditAsync(
                AuditActions.Disable,
                current,
                saved,
                actor!,
                transaction,
                cancellationToken);

            await transaction.CommitAsync(CancellationToken.None);

            return AdminServiceResult<ReleaseLifecycleResponse>.Ok(
                MapRelease(saved),
                "릴리스 배포를 중지했습니다. 기존 패키지 파일은 유지됩니다.");
        }
        catch
        {
            await SafeRollbackAsync(transaction);
            throw;
        }
    }

    public async Task<AdminServiceResult<QuarantineArtifactResponse>> QuarantineArtifactAsync(
        long artifactCode,
        CancellationToken cancellationToken = default)
    {
        if (artifactCode <= 0)
        {
            return ArtifactNotFound();
        }

        var actorFailure = RequireActor<QuarantineArtifactResponse>(out var actor);
        if (actorFailure is not null)
        {
            return actorFailure;
        }

        var preliminaryArtifact = await _artifactRepository.GetByCodeAsync(
            artifactCode,
            cancellationToken: cancellationToken);

        if (preliminaryArtifact is null)
        {
            return ArtifactNotFound();
        }

        await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        QuarantinedArtifactFile? quarantinedFile = null;
        var committed = false;

        try
        {
            var currentRelease = await _releaseQueryRepository.GetByCodeForUpdateAsync(
                preliminaryArtifact.ReleaseCode,
                transaction,
                cancellationToken);

            if (currentRelease is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return ReleaseNotFound<QuarantineArtifactResponse>();
            }

            if (currentRelease.ReleaseStatus == ReleaseStatus.Draft)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return InvalidReleaseState<QuarantineArtifactResponse>(
                    "Draft Artifact는 교체하거나 Draft 릴리스를 삭제해 처리해야 합니다.");
            }

            var currentArtifact = await _artifactQueryRepository.GetByCodeForUpdateAsync(
                artifactCode,
                transaction,
                cancellationToken);

            if (currentArtifact is null
                || currentArtifact.ReleaseCode != currentRelease.ReleaseCode)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return ArtifactNotFound();
            }

            cancellationToken.ThrowIfCancellationRequested();

            quarantinedFile = await _storageService.QuarantineAsync(
                currentArtifact.StorageKey,
                CancellationToken.None);

            if (currentArtifact.ArtifactStatus != ArtifactStatus.Disabled)
            {
                var artifactDisabled = await _artifactRepository.SetStatusAsync(
                    artifactCode,
                    ArtifactStatus.Disabled,
                    transaction,
                    cancellationToken);

                if (!artifactDisabled)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return AdminServiceResult<QuarantineArtifactResponse>.Fail(
                        StatusCodes.Status409Conflict,
                        UpdateErrorCode.InvalidReleaseState,
                        "Artifact 상태가 변경되어 긴급 격리할 수 없습니다.");
                }
            }

            var releaseChanged = currentRelease.ReleaseStatus == ReleaseStatus.Published;
            if (releaseChanged)
            {
                var releaseDisabled = await _releaseRepository.DisableAsync(
                    currentRelease.ReleaseCode,
                    transaction,
                    cancellationToken);

                if (!releaseDisabled)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return InvalidReleaseState<QuarantineArtifactResponse>(
                        "릴리스 상태가 변경되어 긴급 격리할 수 없습니다.");
                }
            }

            var savedArtifact = await _artifactRepository.GetByCodeAsync(
                artifactCode,
                transaction,
                cancellationToken);
            var savedRelease = await _releaseRepository.GetByCodeAsync(
                currentRelease.ReleaseCode,
                transaction,
                cancellationToken);

            if (savedArtifact is null || savedRelease is null)
            {
                throw new InvalidOperationException("격리 처리 결과를 다시 조회할 수 없습니다.");
            }

            await CreateArtifactQuarantineAuditAsync(
                currentArtifact,
                savedArtifact,
                quarantinedFile,
                actor!,
                transaction,
                cancellationToken);

            if (releaseChanged)
            {
                await CreateReleaseAuditAsync(
                    AuditActions.Disable,
                    currentRelease,
                    savedRelease,
                    actor!,
                    transaction,
                    cancellationToken,
                    reason: "EMERGENCY_ARTIFACT_QUARANTINE");
            }

            await transaction.CommitAsync(CancellationToken.None);
            committed = true;

            return AdminServiceResult<QuarantineArtifactResponse>.Ok(
                new QuarantineArtifactResponse
                {
                    ArtifactCode = savedArtifact.ArtifactCode,
                    ReleaseCode = savedArtifact.ReleaseCode,
                    ArtifactStatus = (int)savedArtifact.ArtifactStatus,
                    ReleaseStatus = (int)savedRelease.ReleaseStatus,
                    FileMoved = quarantinedFile.FileMoved
                },
                quarantinedFile.FileMoved
                    ? "Artifact를 긴급 격리하고 배포 대상에서 제외했습니다."
                    : "Artifact 파일은 이미 없으며 배포 대상에서 제외했습니다.");
        }
        catch (ArtifactStorageException exception)
        {
            await SafeRollbackAsync(transaction);

            _logger.LogError(
                "Artifact 긴급 격리에 실패했습니다. ArtifactCode: {ArtifactCode}, FailureType: {FailureType}, RequestId: {RequestId}",
                artifactCode,
                exception.FailureType,
                _httpContextAccessor.HttpContext?.TraceIdentifier);

            return AdminServiceResult<QuarantineArtifactResponse>.Fail(
                StatusCodes.Status500InternalServerError,
                UpdateErrorCode.StorageError,
                "Artifact 파일을 긴급 격리하지 못했습니다.");
        }
        catch
        {
            await SafeRollbackAsync(transaction);
            throw;
        }
        finally
        {
            if (!committed && quarantinedFile is not null)
            {
                var restored = await _storageService.RestoreFromQuarantineAsync(
                    quarantinedFile,
                    CancellationToken.None);

                if (!restored)
                {
                    _logger.LogCritical(
                        "DB Rollback 후 긴급 격리 파일을 복구하지 못했습니다. ArtifactCode: {ArtifactCode}, RequestId: {RequestId}",
                        artifactCode,
                        _httpContextAccessor.HttpContext?.TraceIdentifier);
                }
            }
        }
    }
}
