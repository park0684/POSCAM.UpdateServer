using Microsoft.AspNetCore.Mvc;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Lifecycle;
using POSCAM.UpdateServer.Api.Services;

namespace POSCAM.UpdateServer.Api.Controllers;

[ApiController]
[Route("api/v1/admin/releases")]
public sealed class AdminReleaseLifecycleController : ControllerBase
{
    private readonly IReleaseLifecycleService _lifecycleService;

    public AdminReleaseLifecycleController(IReleaseLifecycleService lifecycleService)
    {
        _lifecycleService = lifecycleService;
    }

    [HttpPost("{releaseCode:long}/publish")]
    [ProducesResponseType(typeof(ApiResponse<ReleaseLifecycleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseLifecycleResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseLifecycleResponse>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ReleaseLifecycleResponse>>> PublishAsync(
        long releaseCode,
        CancellationToken cancellationToken)
    {
        var result = await _lifecycleService.PublishAsync(
            releaseCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("{releaseCode:long}/disable")]
    [ProducesResponseType(typeof(ApiResponse<ReleaseLifecycleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseLifecycleResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ReleaseLifecycleResponse>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ReleaseLifecycleResponse>>> DisableAsync(
        long releaseCode,
        CancellationToken cancellationToken)
    {
        var result = await _lifecycleService.DisableAsync(
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
