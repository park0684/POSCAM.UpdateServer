using Microsoft.AspNetCore.Mvc;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Lifecycle;
using POSCAM.UpdateServer.Api.Services;

namespace POSCAM.UpdateServer.Api.Controllers;

[ApiController]
[Route("api/v1/admin/artifacts")]
public sealed class AdminArtifactLifecycleController : ControllerBase
{
    private readonly IReleaseLifecycleService _lifecycleService;

    public AdminArtifactLifecycleController(IReleaseLifecycleService lifecycleService)
    {
        _lifecycleService = lifecycleService;
    }

    [HttpPost("{artifactCode:long}/quarantine")]
    [ProducesResponseType(typeof(ApiResponse<QuarantineArtifactResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<QuarantineArtifactResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<QuarantineArtifactResponse>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<QuarantineArtifactResponse>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<QuarantineArtifactResponse>>> QuarantineAsync(
        long artifactCode,
        CancellationToken cancellationToken)
    {
        var result = await _lifecycleService.QuarantineArtifactAsync(
            artifactCode,
            cancellationToken);

        var response = result.Success && result.Data is not null
            ? ApiResponse<QuarantineArtifactResponse>.Ok(result.Data, result.Message)
            : ApiResponse<QuarantineArtifactResponse>.Fail(result.ErrorCode, result.Message);

        return StatusCode(result.HttpStatusCode, response);
    }
}
