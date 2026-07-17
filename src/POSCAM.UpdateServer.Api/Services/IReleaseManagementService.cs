using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;

namespace POSCAM.UpdateServer.Api.Services;

public interface IReleaseManagementService
{
    Task<AdminServiceResult<IReadOnlyList<ActiveProductResponse>>> GetActiveProductsAsync(
        CancellationToken cancellationToken = default);

    Task<AdminServiceResult<PagedResponse<ReleaseListItemResponse>>> GetReleasesAsync(
        ReleaseListRequest? request,
        CancellationToken cancellationToken = default);

    Task<AdminServiceResult<ReleaseDetailResponse>> GetReleaseAsync(
        long releaseCode,
        CancellationToken cancellationToken = default);

    Task<AdminServiceResult<ReleaseDetailResponse>> CreateDraftAsync(
        CreateReleaseRequest? request,
        CancellationToken cancellationToken = default);

    Task<AdminServiceResult<ReleaseDetailResponse>> UpdateDraftAsync(
        long releaseCode,
        UpdateReleaseRequest? request,
        CancellationToken cancellationToken = default);

    Task<AdminServiceResult<DeleteReleaseResponse>> DeleteDraftAsync(
        long releaseCode,
        CancellationToken cancellationToken = default);
}
