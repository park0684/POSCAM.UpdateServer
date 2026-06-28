using System.Data;
using System.Text.Json;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Models.Queries;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Api.Services;

/// <summary>
/// 관리자용 Active Product 조회와 Draft Release CRUD를 담당한다.
/// Published·Disabled Release는 조회만 허용하고 수정·삭제하지 않는다.
/// </summary>
public sealed class ReleaseManagementService : IReleaseManagementService
{
    private const int MaxPageSize = 100;
    private const int MaxKeywordLength = 100;

    private static readonly JsonSerializerOptions AuditJsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IDbContext _dbContext;
    private readonly IUpdateProductRepository _productRepository;
    private readonly IUpdateReleaseRepository _releaseRepository;
    private readonly IReleaseManagementQueryRepository _managementQueryRepository;
    private readonly IUpdateArtifactRepository _artifactRepository;
    private readonly IUpdateAuditLogRepository _auditLogRepository;
    private readonly IUpdateManagementActorAccessor _actorAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReleaseManagementService(
        IDbContext dbContext,
        IUpdateProductRepository productRepository,
        IUpdateReleaseRepository releaseRepository,
        IReleaseManagementQueryRepository managementQueryRepository,
        IUpdateArtifactRepository artifactRepository,
        IUpdateAuditLogRepository auditLogRepository,
        IUpdateManagementActorAccessor actorAccessor,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _productRepository = productRepository;
        _releaseRepository = releaseRepository;
        _managementQueryRepository = managementQueryRepository;
        _artifactRepository = artifactRepository;
        _auditLogRepository = auditLogRepository;
        _actorAccessor = actorAccessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AdminServiceResult<IReadOnlyList<ActiveProductResponse>>> GetActiveProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetActiveAsync(
            cancellationToken: cancellationToken);

        IReadOnlyList<ActiveProductResponse> response = products
            .Select(product => new ActiveProductResponse
            {
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                ProductDescription = product.ProductDescription
            })
            .ToArray();

        return AdminServiceResult<IReadOnlyList<ActiveProductResponse>>.Ok(
            response,
            "활성 제품 목록을 조회했습니다.");
    }

    public async Task<AdminServiceResult<PagedResponse<ReleaseListItemResponse>>> GetReleasesAsync(
        ReleaseListRequest? request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateListRequest(request, out var criteria);
        if (validation is not null)
        {
            return validation;
        }

        var releases = await _managementQueryRepository.GetPagedAsync(
            criteria!,
            cancellationToken: cancellationToken);

        var totalCount = await _managementQueryRepository.CountAsync(
            criteria!,
            cancellationToken: cancellationToken);

        var page = request!.Page;
        var pageSize = request.PageSize;
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new PagedResponse<ReleaseListItemResponse>
        {
            Items = releases.Select(MapListItem).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return AdminServiceResult<PagedResponse<ReleaseListItemResponse>>.Ok(
            response,
            "릴리스 목록을 조회했습니다.");
    }

    public async Task<AdminServiceResult<ReleaseDetailResponse>> GetReleaseAsync(
        long releaseCode,
        CancellationToken cancellationToken = default)
    {
        if (releaseCode <= 0)
        {
            return ReleaseNotFound<ReleaseDetailResponse>();
        }

        var release = await _releaseRepository.GetByCodeAsync(
            releaseCode,
            cancellationToken: cancellationToken);

        if (release is null)
        {
            return ReleaseNotFound<ReleaseDetailResponse>();
        }

        var artifacts = await _artifactRepository.GetActiveByReleaseAsync(
            releaseCode,
            cancellationToken: cancellationToken);

        return AdminServiceResult<ReleaseDetailResponse>.Ok(
            MapDetail(release, artifacts),
            "릴리스 상세 정보를 조회했습니다.");
    }

    public async Task<AdminServiceResult<ReleaseDetailResponse>> CreateDraftAsync(
        CreateReleaseRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorFailure = RequireActor<ReleaseDetailResponse>(out var actor);
        if (actorFailure is not null)
        {
            return actorFailure;
        }

        var validation = ValidateMutation(
            request?.ProductCode,
            request?.Version,
            request?.Channel,
            request?.IsMandatory ?? false,
            request?.ForceUpdateBelowVersion,
            out var mutation);

        if (validation is not null)
        {
            return validation;
        }

        await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var productFailure = await ValidateActiveProductAsync<ReleaseDetailResponse>(
                mutation!.ProductCode,
                transaction,
                cancellationToken);

            if (productFailure is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return productFailure;
            }

            var duplicate = await _releaseRepository.ExistsVersionAsync(
                mutation.ProductCode,
                mutation.Channel,
                mutation.Version,
                transaction: transaction,
                cancellationToken: cancellationToken);

            if (duplicate)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DuplicateRelease<ReleaseDetailResponse>();
            }

            var release = CreateEntity(
                mutation,
                request!.ReleaseNotes,
                request.InternalMemo,
                actor!);

            release.ReleaseCode = await _releaseRepository.CreateDraftAsync(
                release,
                transaction,
                cancellationToken);

            var created = await _releaseRepository.GetByCodeAsync(
                release.ReleaseCode,
                transaction,
                cancellationToken);

            if (created is null)
            {
                throw new InvalidOperationException("생성된 릴리스를 다시 조회할 수 없습니다.");
            }

            await CreateAuditAsync(
                AuditActions.Create,
                created.ReleaseCode,
                before: null,
                after: created,
                actor!,
                transaction,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return AdminServiceResult<ReleaseDetailResponse>.Ok(
                MapDetail(created, Array.Empty<UpdateArtifact>()),
                "Draft 릴리스를 생성했습니다.",
                StatusCodes.Status201Created);
        }
        catch (UpdateDatabaseException exception)
            when (exception.FailureType == DatabaseFailureType.Duplicate)
        {
            await SafeRollbackAsync(transaction);
            return DuplicateRelease<ReleaseDetailResponse>();
        }
        catch
        {
            await SafeRollbackAsync(transaction);
            throw;
        }
    }

    public async Task<AdminServiceResult<ReleaseDetailResponse>> UpdateDraftAsync(
        long releaseCode,
        UpdateReleaseRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (releaseCode <= 0)
        {
            return ReleaseNotFound<ReleaseDetailResponse>();
        }

        var actorFailure = RequireActor<ReleaseDetailResponse>(out var actor);
        if (actorFailure is not null)
        {
            return actorFailure;
        }

        var validation = ValidateMutation(
            request?.ProductCode,
            request?.Version,
            request?.Channel,
            request?.IsMandatory ?? false,
            request?.ForceUpdateBelowVersion,
            out var mutation);

        if (validation is not null)
        {
            return validation;
        }

        await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var current = await _managementQueryRepository.GetByCodeForUpdateAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (current is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ReleaseNotFound<ReleaseDetailResponse>();
            }

            if (current.ReleaseStatus != ReleaseStatus.Draft)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InvalidReleaseState<ReleaseDetailResponse>(
                    "Draft 상태의 릴리스만 수정할 수 있습니다.");
            }

            var productFailure = await ValidateActiveProductAsync<ReleaseDetailResponse>(
                mutation!.ProductCode,
                transaction,
                cancellationToken);

            if (productFailure is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return productFailure;
            }

            var duplicate = await _releaseRepository.ExistsVersionAsync(
                mutation.ProductCode,
                mutation.Channel,
                mutation.Version,
                excludeReleaseCode: releaseCode,
                transaction: transaction,
                cancellationToken: cancellationToken);

            if (duplicate)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DuplicateRelease<ReleaseDetailResponse>();
            }

            var updated = ApplyMutation(
                current,
                mutation,
                request!.ReleaseNotes,
                request.InternalMemo);

            var changed = await _releaseRepository.UpdateDraftAsync(
                updated,
                transaction,
                cancellationToken);

            if (!changed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InvalidReleaseState<ReleaseDetailResponse>(
                    "릴리스 상태가 변경되어 수정할 수 없습니다.");
            }

            var saved = await _releaseRepository.GetByCodeAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (saved is null)
            {
                throw new InvalidOperationException("수정된 릴리스를 다시 조회할 수 없습니다.");
            }

            await CreateAuditAsync(
                AuditActions.Update,
                releaseCode,
                current,
                saved,
                actor!,
                transaction,
                cancellationToken);

            var artifacts = await _artifactRepository.GetActiveByReleaseAsync(
                releaseCode,
                transaction,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return AdminServiceResult<ReleaseDetailResponse>.Ok(
                MapDetail(saved, artifacts),
                "Draft 릴리스를 수정했습니다.");
        }
        catch (UpdateDatabaseException exception)
            when (exception.FailureType == DatabaseFailureType.Duplicate)
        {
            await SafeRollbackAsync(transaction);
            return DuplicateRelease<ReleaseDetailResponse>();
        }
        catch
        {
            await SafeRollbackAsync(transaction);
            throw;
        }
    }

    public async Task<AdminServiceResult<DeleteReleaseResponse>> DeleteDraftAsync(
        long releaseCode,
        CancellationToken cancellationToken = default)
    {
        if (releaseCode <= 0)
        {
            return ReleaseNotFound<DeleteReleaseResponse>();
        }

        var actorFailure = RequireActor<DeleteReleaseResponse>(out var actor);
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
            var current = await _managementQueryRepository.GetByCodeForUpdateAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (current is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ReleaseNotFound<DeleteReleaseResponse>();
            }

            if (current.ReleaseStatus != ReleaseStatus.Draft)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InvalidReleaseState<DeleteReleaseResponse>(
                    "Draft 상태의 릴리스만 삭제할 수 있습니다.");
            }

            await CreateAuditAsync(
                AuditActions.DeleteDraft,
                releaseCode,
                current,
                after: null,
                actor!,
                transaction,
                cancellationToken);

            var deleted = await _releaseRepository.DeleteDraftAsync(
                releaseCode,
                transaction,
                cancellationToken);

            if (!deleted)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InvalidReleaseState<DeleteReleaseResponse>(
                    "릴리스 상태가 변경되어 삭제할 수 없습니다.");
            }

            await transaction.CommitAsync(cancellationToken);

            return AdminServiceResult<DeleteReleaseResponse>.Ok(
                new DeleteReleaseResponse
                {
                    ReleaseCode = releaseCode
                },
                "Draft 릴리스를 삭제했습니다.");
        }
        catch (UpdateDatabaseException exception)
            when (exception.FailureType == DatabaseFailureType.ForeignKeyViolation)
        {
            await SafeRollbackAsync(transaction);
            return InvalidReleaseState<DeleteReleaseResponse>(
                "Artifact가 연결된 Draft 릴리스는 삭제할 수 없습니다.");
        }
        catch
        {
            await SafeRollbackAsync(transaction);
            throw;
        }
    }

    private static AdminServiceResult<PagedResponse<ReleaseListItemResponse>>? ValidateListRequest(
        ReleaseListRequest? request,
        out ReleaseSearchCriteria? criteria)
    {
        criteria = null;

        if (request is null)
        {
            return ValidationFailure<PagedResponse<ReleaseListItemResponse>>(
                "목록 조회 조건이 필요합니다.");
        }

        if (request.Page < 1)
        {
            return ValidationFailure<PagedResponse<ReleaseListItemResponse>>(
                "페이지는 1 이상이어야 합니다.");
        }

        if (request.PageSize is < 1 or > MaxPageSize)
        {
            return ValidationFailure<PagedResponse<ReleaseListItemResponse>>(
                $"페이지 크기는 1~{MaxPageSize} 범위여야 합니다.");
        }

        var productCode = NormalizeFilter(request.ProductCode);
        if (productCode is not null && !ProductCodes.IsSupported(productCode))
        {
            return AdminServiceResult<PagedResponse<ReleaseListItemResponse>>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidProduct,
                "제품 코드가 올바르지 않습니다.");
        }

        var channel = NormalizeFilter(request.Channel);
        if (channel is not null && !ReleaseChannels.IsSupported(channel))
        {
            return AdminServiceResult<PagedResponse<ReleaseListItemResponse>>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidChannel,
                "업데이트 채널이 올바르지 않습니다.");
        }

        ReleaseStatus? status = null;
        if (request.Status.HasValue)
        {
            if (!Enum.IsDefined(typeof(ReleaseStatus), request.Status.Value))
            {
                return ValidationFailure<PagedResponse<ReleaseListItemResponse>>(
                    "릴리스 상태가 올바르지 않습니다.");
            }

            status = (ReleaseStatus)request.Status.Value;
        }

        var keyword = NormalizeFilter(request.Keyword);
        if (keyword?.Length > MaxKeywordLength)
        {
            return ValidationFailure<PagedResponse<ReleaseListItemResponse>>(
                $"검색어는 {MaxKeywordLength}자를 초과할 수 없습니다.");
        }

        criteria = new ReleaseSearchCriteria
        {
            ProductCode = productCode,
            Channel = channel,
            Status = status,
            Keyword = keyword,
            Offset = checked((request.Page - 1) * request.PageSize),
            PageSize = request.PageSize
        };

        return null;
    }

    private static AdminServiceResult<T>? ValidateMutation<T>(
        string? productCode,
        string? versionText,
        string? channel,
        bool isMandatory,
        string? forceUpdateBelowVersionText,
        out ValidatedReleaseMutation? mutation)
    {
        mutation = null;

        if (!ProductCodes.IsSupported(productCode))
        {
            return AdminServiceResult<T>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidProduct,
                "제품 코드가 올바르지 않습니다.");
        }

        if (!UpdateVersion.TryParse(versionText, out var version, out _))
        {
            return AdminServiceResult<T>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidVersion,
                "릴리스 버전 형식이 올바르지 않습니다.");
        }

        if (!ReleaseChannels.IsSupported(channel))
        {
            return AdminServiceResult<T>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidChannel,
                "업데이트 채널이 올바르지 않습니다.");
        }

        UpdateVersion? forceUpdateBelowVersion = null;
        if (!string.IsNullOrWhiteSpace(forceUpdateBelowVersionText))
        {
            if (!UpdateVersion.TryParse(
                    forceUpdateBelowVersionText,
                    out var parsedThreshold,
                    out _))
            {
                return AdminServiceResult<T>.Fail(
                    StatusCodes.Status400BadRequest,
                    UpdateErrorCode.InvalidVersion,
                    "강제 업데이트 기준 버전 형식이 올바르지 않습니다.");
            }

            forceUpdateBelowVersion = parsedThreshold;
        }

        if (!ReleaseUpdatePolicy.TryCreate(
                version,
                isMandatory,
                forceUpdateBelowVersion,
                out _,
                out var policyError))
        {
            var message = policyError switch
            {
                ReleasePolicyValidationError.MandatoryAndThresholdCannotCoexist =>
                    "전체 강제 업데이트와 기준 버전 미만 강제를 동시에 설정할 수 없습니다.",
                ReleasePolicyValidationError.ForceThresholdExceedsReleaseVersion =>
                    "강제 업데이트 기준 버전은 릴리스 버전보다 높을 수 없습니다.",
                _ => "강제 업데이트 정책이 올바르지 않습니다."
            };

            return ValidationFailure<T>(message);
        }

        mutation = new ValidatedReleaseMutation(
            productCode!,
            version,
            channel!,
            isMandatory,
            forceUpdateBelowVersion);

        return null;
    }

    private async Task<AdminServiceResult<T>?> ValidateActiveProductAsync<T>(
        string productCode,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByCodeAsync(
            productCode,
            transaction,
            cancellationToken);

        if (product is null)
        {
            return AdminServiceResult<T>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidProduct,
                "등록되지 않은 제품 코드입니다.");
        }

        if (product.ProductStatus != ProductStatus.Active)
        {
            return AdminServiceResult<T>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.ProductInactive,
                "비활성 제품에는 릴리스를 등록할 수 없습니다.");
        }

        return null;
    }

    private async Task CreateAuditAsync(
        string action,
        long releaseCode,
        UpdateRelease? before,
        UpdateRelease? after,
        Models.Authorization.UpdateManagementActor actor,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var auditLog = new UpdateAuditLog
        {
            Action = action,
            TargetType = AuditTargetTypes.Release,
            TargetCode = releaseCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ActorUserCode = actor.UserCode,
            ActorUserName = Truncate(actor.UserName, 100),
            BeforeData = before is null
                ? null
                : JsonSerializer.Serialize(CreateSnapshot(before), AuditJsonOptions),
            AfterData = after is null
                ? null
                : JsonSerializer.Serialize(CreateSnapshot(after), AuditJsonOptions),
            IpAddress = Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), 45),
            UserAgent = Truncate(httpContext?.Request.Headers["User-Agent"].ToString(), 500),
            RequestId = Truncate(httpContext?.TraceIdentifier, 100)
        };

        await _auditLogRepository.CreateAsync(
            auditLog,
            transaction,
            cancellationToken);
    }

    private static UpdateRelease CreateEntity(
        ValidatedReleaseMutation mutation,
        string? releaseNotes,
        string? internalMemo,
        Models.Authorization.UpdateManagementActor actor)
    {
        return new UpdateRelease
        {
            ProductCode = mutation.ProductCode,
            Version = mutation.Version.ToString(),
            VersionMajor = mutation.Version.Major,
            VersionMinor = mutation.Version.Minor,
            VersionPatch = mutation.Version.Patch,
            VersionRevision = mutation.Version.Revision,
            Channel = mutation.Channel,
            ForceUpdateBelowVersion = mutation.ForceUpdateBelowVersion?.ToString(),
            IsMandatory = mutation.IsMandatory,
            ReleaseNotes = NormalizeOptionalText(releaseNotes),
            InternalMemo = NormalizeOptionalText(internalMemo),
            ReleaseStatus = ReleaseStatus.Draft,
            CreatedByUserCode = actor.UserCode,
            CreatedByUserName = Truncate(actor.UserName, 100)
        };
    }

    private static UpdateRelease ApplyMutation(
        UpdateRelease current,
        ValidatedReleaseMutation mutation,
        string? releaseNotes,
        string? internalMemo)
    {
        return new UpdateRelease
        {
            ReleaseCode = current.ReleaseCode,
            ProductCode = mutation.ProductCode,
            Version = mutation.Version.ToString(),
            VersionMajor = mutation.Version.Major,
            VersionMinor = mutation.Version.Minor,
            VersionPatch = mutation.Version.Patch,
            VersionRevision = mutation.Version.Revision,
            Channel = mutation.Channel,
            ForceUpdateBelowVersion = mutation.ForceUpdateBelowVersion?.ToString(),
            IsMandatory = mutation.IsMandatory,
            ReleaseNotes = NormalizeOptionalText(releaseNotes),
            InternalMemo = NormalizeOptionalText(internalMemo),
            ReleaseStatus = current.ReleaseStatus,
            PublishedAt = current.PublishedAt,
            CreatedByUserCode = current.CreatedByUserCode,
            CreatedByUserName = current.CreatedByUserName,
            CreatedAt = current.CreatedAt,
            UpdatedAt = current.UpdatedAt
        };
    }

    private static ReleaseListItemResponse MapListItem(UpdateRelease release)
    {
        return new ReleaseListItemResponse
        {
            ReleaseCode = release.ReleaseCode,
            ProductCode = release.ProductCode,
            Version = release.Version,
            Channel = release.Channel,
            IsMandatory = release.IsMandatory,
            ForceUpdateBelowVersion = release.ForceUpdateBelowVersion,
            Status = (int)release.ReleaseStatus,
            StatusName = release.ReleaseStatus.ToString(),
            PublishedAt = AsUtc(release.PublishedAt),
            CreatedByUserCode = release.CreatedByUserCode,
            CreatedByUserName = release.CreatedByUserName,
            CreatedAt = AsUtc(release.CreatedAt),
            UpdatedAt = AsUtc(release.UpdatedAt)
        };
    }

    private static ReleaseDetailResponse MapDetail(
        UpdateRelease release,
        IReadOnlyList<UpdateArtifact> artifacts)
    {
        return new ReleaseDetailResponse
        {
            ReleaseCode = release.ReleaseCode,
            ProductCode = release.ProductCode,
            Version = release.Version,
            Channel = release.Channel,
            IsMandatory = release.IsMandatory,
            ForceUpdateBelowVersion = release.ForceUpdateBelowVersion,
            ReleaseNotes = release.ReleaseNotes,
            InternalMemo = release.InternalMemo,
            Status = (int)release.ReleaseStatus,
            StatusName = release.ReleaseStatus.ToString(),
            PublishedAt = AsUtc(release.PublishedAt),
            CreatedByUserCode = release.CreatedByUserCode,
            CreatedByUserName = release.CreatedByUserName,
            CreatedAt = AsUtc(release.CreatedAt),
            UpdatedAt = AsUtc(release.UpdatedAt),
            Artifacts = artifacts.Select(artifact => new ReleaseArtifactSummaryResponse
            {
                ArtifactCode = artifact.ArtifactCode,
                PublicId = artifact.PublicId,
                Os = artifact.OperatingSystem,
                Architecture = artifact.Architecture,
                PackageType = artifact.PackageType,
                FileName = artifact.FileName,
                FileSize = artifact.FileSize,
                Sha256 = artifact.Sha256,
                Status = (int)artifact.ArtifactStatus,
                CreatedAt = AsUtc(artifact.CreatedAt)
            }).ToArray()
        };
    }

    private static object CreateSnapshot(UpdateRelease release)
    {
        return new
        {
            release.ReleaseCode,
            release.ProductCode,
            release.Version,
            release.Channel,
            release.IsMandatory,
            release.ForceUpdateBelowVersion,
            release.ReleaseNotes,
            release.InternalMemo,
            Status = (int)release.ReleaseStatus,
            release.PublishedAt,
            release.CreatedByUserCode,
            release.CreatedByUserName,
            release.CreatedAt,
            release.UpdatedAt
        };
    }

    private AdminServiceResult<T>? RequireActor<T>(
        out Models.Authorization.UpdateManagementActor? actor)
    {
        actor = _actorAccessor.Actor;

        return actor is null
            ? AdminServiceResult<T>.Fail(
                StatusCodes.Status503ServiceUnavailable,
                UpdateErrorCode.ExternalServiceUnavailable,
                "관리자 작업자 정보를 확인할 수 없습니다.")
            : null;
    }

    private static AdminServiceResult<T> ValidationFailure<T>(string message)
    {
        return AdminServiceResult<T>.Fail(
            StatusCodes.Status400BadRequest,
            UpdateErrorCode.ValidationError,
            message);
    }

    private static AdminServiceResult<T> ReleaseNotFound<T>()
    {
        return AdminServiceResult<T>.Fail(
            StatusCodes.Status404NotFound,
            UpdateErrorCode.ReleaseNotFound,
            "릴리스를 찾을 수 없습니다.");
    }

    private static AdminServiceResult<T> DuplicateRelease<T>()
    {
        return AdminServiceResult<T>.Fail(
            StatusCodes.Status409Conflict,
            UpdateErrorCode.DuplicateRelease,
            "동일한 제품·채널·버전의 릴리스가 이미 존재합니다.");
    }

    private static AdminServiceResult<T> InvalidReleaseState<T>(string message)
    {
        return AdminServiceResult<T>.Fail(
            StatusCodes.Status409Conflict,
            UpdateErrorCode.InvalidReleaseState,
            message);
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static DateTime? AsUtc(DateTime? value)
    {
        return value.HasValue ? AsUtc(value.Value) : null;
    }

    private static async Task SafeRollbackAsync(System.Data.Common.DbTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // 원래 예외를 보존하기 위해 Rollback 실패는 여기서 다시 던지지 않는다.
        }
    }

    private sealed record ValidatedReleaseMutation(
        string ProductCode,
        UpdateVersion Version,
        string Channel,
        bool IsMandatory,
        UpdateVersion? ForceUpdateBelowVersion);
}
