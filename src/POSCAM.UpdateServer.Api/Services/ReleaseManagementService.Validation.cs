using System.Data;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Models.Queries;

namespace POSCAM.UpdateServer.Api.Services;

public sealed partial class ReleaseManagementService
{
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

        var rawOffset = ((long)request.Page - 1L) * request.PageSize;
        if (rawOffset > int.MaxValue)
        {
            return ValidationFailure<PagedResponse<ReleaseListItemResponse>>(
                "요청한 페이지 범위가 너무 큽니다.");
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
            Offset = (int)rawOffset,
            PageSize = request.PageSize
        };

        return null;
    }

    private static AdminServiceResult<ReleaseDetailResponse>? ValidateMutation(
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
            return AdminServiceResult<ReleaseDetailResponse>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidProduct,
                "제품 코드가 올바르지 않습니다.");
        }

        if (!UpdateVersion.TryParse(versionText, out var version, out _))
        {
            return AdminServiceResult<ReleaseDetailResponse>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidVersion,
                "릴리스 버전 형식이 올바르지 않습니다.");
        }

        if (!ReleaseChannels.IsSupported(channel))
        {
            return AdminServiceResult<ReleaseDetailResponse>.Fail(
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
                return AdminServiceResult<ReleaseDetailResponse>.Fail(
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

            return ValidationFailure<ReleaseDetailResponse>(message);
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

    private sealed record ValidatedReleaseMutation(
        string ProductCode,
        UpdateVersion Version,
        string Channel,
        bool IsMandatory,
        UpdateVersion? ForceUpdateBelowVersion);
}
