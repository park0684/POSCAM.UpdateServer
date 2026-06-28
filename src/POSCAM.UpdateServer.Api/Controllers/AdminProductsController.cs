using Microsoft.AspNetCore.Mvc;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;
using POSCAM.UpdateServer.Api.Services;

namespace POSCAM.UpdateServer.Api.Controllers;

[ApiController]
[Route("api/v1/admin/products")]
public sealed class AdminProductsController : ControllerBase
{
    private readonly IReleaseManagementService _releaseManagementService;

    public AdminProductsController(IReleaseManagementService releaseManagementService)
    {
        _releaseManagementService = releaseManagementService;
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ActiveProductResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ActiveProductResponse>>>> GetActiveAsync(
        CancellationToken cancellationToken)
    {
        var result = await _releaseManagementService.GetActiveProductsAsync(cancellationToken);

        return StatusCode(
            result.HttpStatusCode,
            result.Success && result.Data is not null
                ? ApiResponse<IReadOnlyList<ActiveProductResponse>>.Ok(result.Data, result.Message)
                : ApiResponse<IReadOnlyList<ActiveProductResponse>>.Fail(result.ErrorCode, result.Message));
    }
}
