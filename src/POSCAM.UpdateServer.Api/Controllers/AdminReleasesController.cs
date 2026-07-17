using Microsoft.AspNetCore.Mvc;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;
using POSCAM.UpdateServer.Api.Services;

namespace POSCAM.UpdateServer.Api.Controllers;

[ApiController]
[Route("api/v1/admin/releases")]
public sealed class AdminReleasesController : ControllerBase
{
    private readonly IReleaseManagementService _releaseManagementService;

    public AdminReleasesController(IReleaseManagementService releaseManagementService)
    {
        _releaseManagementService = releaseManagementService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ReleaseListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ReleaseListItemResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResponse<ReleaseListItemResponse>>>> GetListAsync(
        [FromQuery] ReleaseListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _releaseManagementService.GetReleasesAsync(
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("{releaseCode:long}")]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReleaseDetailResponse>>> GetDetailAsync(
        long releaseCode,
        CancellationToken cancellationToken)
    {
        var result = await _releaseManagementService.GetReleaseAsync(
            releaseCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDetailResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDetailResponse>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ReleaseDetailResponse>>> CreateAsync(
        [FromBody] CreateReleaseRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _releaseManagementService.CreateDraftAsync(
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut("{releaseCode:long}")]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDetailResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDetailResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseDetailResponse>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ReleaseDetailResponse>>> UpdateAsync(
        long releaseCode,
        [FromBody] UpdateReleaseRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _releaseManagementService.UpdateDraftAsync(
            releaseCode,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{releaseCode:long}")]
    [ProducesResponseType(typeof(ApiResponse<DeleteReleaseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DeleteReleaseResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<DeleteReleaseResponse>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DeleteReleaseResponse>>> DeleteAsync(
        long releaseCode,
        CancellationToken cancellationToken)
    {
        var result = await _releaseManagementService.DeleteDraftAsync(
            releaseCode,
            cancellationToken);

        return ToActionResult(result);
    }

    private ActionResult<ApiResponse<T>> ToActionResult<T>(AdminServiceResult<T> result)
    {
        var response = result.Success && result.Data is not null
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.ErrorCode, result.Message);

        return StatusCode(result.HttpStatusCode, response);
    }
}
