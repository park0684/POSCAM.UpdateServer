using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Audits;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Models.Queries;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Api.Services;

public sealed class AuditQueryService : IAuditQueryService
{
    private const int MaxPageSize = 100;
    private const int MaxTargetCodeLength = 100;
    private const int MaxRequestIdLength = 100;

    private static readonly HashSet<string> SupportedActions = new(
        StringComparer.Ordinal)
    {
        AuditActions.Create,
        AuditActions.Update,
        AuditActions.Upload,
        AuditActions.ReplaceDraftArtifact,
        AuditActions.Publish,
        AuditActions.Disable,
        AuditActions.DeleteDraft
    };

    private static readonly HashSet<string> SupportedTargetTypes = new(
        StringComparer.Ordinal)
    {
        AuditTargetTypes.Product,
        AuditTargetTypes.Release,
        AuditTargetTypes.Artifact
    };

    private readonly IAuditManagementQueryRepository _auditRepository;
    private readonly IUpdateReleaseRepository _releaseRepository;

    public AuditQueryService(
        IAuditManagementQueryRepository auditRepository,
        IUpdateReleaseRepository releaseRepository)
    {
        _auditRepository = auditRepository;
        _releaseRepository = releaseRepository;
    }

    public async Task<AdminServiceResult<PagedResponse<AuditLogResponse>>> GetAuditLogsAsync(
        AuditListRequest? request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request, out var criteria);
        if (validation is not null)
        {
            return validation;
        }

        var logs = await _auditRepository.GetPagedAsync(
            criteria!,
            cancellationToken);
        var totalCount = await _auditRepository.CountAsync(
            criteria!,
            cancellationToken);

        return AdminServiceResult<PagedResponse<AuditLogResponse>>.Ok(
            CreatePage(
                logs,
                request!.Page,
                request.PageSize,
                totalCount),
            "감사 로그 목록을 조회했습니다.");
    }

    public async Task<AdminServiceResult<PagedResponse<AuditLogResponse>>> GetReleaseHistoryAsync(
        long releaseCode,
        ReleaseAuditListRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (releaseCode <= 0)
        {
            return ReleaseNotFound();
        }

        var release = await _releaseRepository.GetByCodeAsync(
            releaseCode,
            cancellationToken: cancellationToken);

        if (release is null)
        {
            return ReleaseNotFound();
        }

        if (request is null)
        {
            return ValidationFailure("조회 조건이 필요합니다.");
        }

        var pagingFailure = ValidatePaging(
            request.Page,
            request.PageSize);
        if (pagingFailure is not null)
        {
            return pagingFailure;
        }

        var action = NormalizeUpper(request.Action);
        if (action is not null && !SupportedActions.Contains(action))
        {
            return ValidationFailure("감사 작업 코드가 올바르지 않습니다.");
        }

        var offset = CalculateOffset(request.Page, request.PageSize);
        if (offset is null)
        {
            return ValidationFailure("요청한 페이지 범위가 너무 큽니다.");
        }

        var logs = await _auditRepository.GetReleaseHistoryAsync(
            releaseCode,
            action,
            offset.Value,
            request.PageSize,
            cancellationToken);
        var totalCount = await _auditRepository.CountReleaseHistoryAsync(
            releaseCode,
            action,
            cancellationToken);

        return AdminServiceResult<PagedResponse<AuditLogResponse>>.Ok(
            CreatePage(
                logs,
                request.Page,
                request.PageSize,
                totalCount),
            "릴리스 감사 이력을 조회했습니다.");
    }

    private static AdminServiceResult<PagedResponse<AuditLogResponse>>? ValidateRequest(
        AuditListRequest? request,
        out AuditSearchCriteria? criteria)
    {
        criteria = null;

        if (request is null)
        {
            return ValidationFailure("조회 조건이 필요합니다.");
        }

        var pagingFailure = ValidatePaging(request.Page, request.PageSize);
        if (pagingFailure is not null)
        {
            return pagingFailure;
        }

        var action = NormalizeUpper(request.Action);
        if (action is not null && !SupportedActions.Contains(action))
        {
            return ValidationFailure("감사 작업 코드가 올바르지 않습니다.");
        }

        var targetType = NormalizeUpper(request.TargetType);
        if (targetType is not null && !SupportedTargetTypes.Contains(targetType))
        {
            return ValidationFailure("감사 대상 유형이 올바르지 않습니다.");
        }

        var targetCode = Normalize(request.TargetCode);
        if (targetCode?.Length > MaxTargetCodeLength)
        {
            return ValidationFailure(
                $"감사 대상 코드는 {MaxTargetCodeLength}자를 초과할 수 없습니다.");
        }

        if (request.ActorUserCode.HasValue && request.ActorUserCode.Value <= 0)
        {
            return ValidationFailure("작업자 코드는 1 이상이어야 합니다.");
        }

        var requestId = Normalize(request.RequestId);
        if (requestId?.Length > MaxRequestIdLength)
        {
            return ValidationFailure(
                $"Request ID는 {MaxRequestIdLength}자를 초과할 수 없습니다.");
        }

        var fromUtc = NormalizeUtc(request.FromUtc);
        var toUtc = NormalizeUtc(request.ToUtc);
        if (fromUtc.HasValue
            && toUtc.HasValue
            && fromUtc.Value > toUtc.Value)
        {
            return ValidationFailure("조회 시작 시각은 종료 시각보다 늦을 수 없습니다.");
        }

        var offset = CalculateOffset(request.Page, request.PageSize);
        if (offset is null)
        {
            return ValidationFailure("요청한 페이지 범위가 너무 큽니다.");
        }

        criteria = new AuditSearchCriteria
        {
            Action = action,
            TargetType = targetType,
            TargetCode = targetCode,
            ActorUserCode = request.ActorUserCode,
            RequestId = requestId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Offset = offset.Value,
            PageSize = request.PageSize
        };

        return null;
    }

    private static AdminServiceResult<PagedResponse<AuditLogResponse>>? ValidatePaging(
        int page,
        int pageSize)
    {
        if (page < 1)
        {
            return ValidationFailure("페이지는 1 이상이어야 합니다.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            return ValidationFailure(
                $"페이지 크기는 1~{MaxPageSize} 범위여야 합니다.");
        }

        return null;
    }

    private static int? CalculateOffset(int page, int pageSize)
    {
        var offset = ((long)page - 1L) * pageSize;
        return offset <= int.MaxValue ? (int)offset : null;
    }

    private static PagedResponse<AuditLogResponse> CreatePage(
        IReadOnlyList<UpdateAuditLog> logs,
        int page,
        int pageSize,
        long totalCount)
    {
        return new PagedResponse<AuditLogResponse>
        {
            Items = logs.Select(Map).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    private static AuditLogResponse Map(UpdateAuditLog log)
    {
        return new AuditLogResponse
        {
            AuditLogCode = log.AuditLogCode,
            Action = log.Action,
            TargetType = log.TargetType,
            TargetCode = log.TargetCode,
            ActorUserCode = log.ActorUserCode,
            ActorUserName = log.ActorUserName,
            BeforeData = log.BeforeData,
            AfterData = log.AfterData,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            RequestId = log.RequestId,
            CreatedAt = log.CreatedAt.Kind == DateTimeKind.Utc
                ? log.CreatedAt
                : DateTime.SpecifyKind(log.CreatedAt, DateTimeKind.Utc)
        };
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeUpper(string? value)
    {
        var normalized = Normalize(value);
        return normalized?.ToUpperInvariant();
    }

    private static AdminServiceResult<PagedResponse<AuditLogResponse>> ValidationFailure(
        string message)
    {
        return AdminServiceResult<PagedResponse<AuditLogResponse>>.Fail(
            StatusCodes.Status400BadRequest,
            UpdateErrorCode.ValidationError,
            message);
    }

    private static AdminServiceResult<PagedResponse<AuditLogResponse>> ReleaseNotFound()
    {
        return AdminServiceResult<PagedResponse<AuditLogResponse>>.Fail(
            StatusCodes.Status404NotFound,
            UpdateErrorCode.ReleaseNotFound,
            "릴리스를 찾을 수 없습니다.");
    }
}
