using Microsoft.AspNetCore.Mvc;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Audits;
using POSCAM.UpdateServer.Api.Services;

namespace POSCAM.UpdateServer.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminAuditsController : ControllerBase
{
    private readonly IAuditQueryService _auditQueryService;

    public AdminAuditsController(IAuditQueryService auditQueryService)
    {
        _auditQueryService = auditQueryService;
    }

    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AuditLogResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AuditLogResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResponse<AuditLogResponse>>>> GetAuditLogsAsync(
        [FromQuery] AuditListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auditQueryService.GetAuditLogsAsync(
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("releases/{releaseCode:long}/audit-logs")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AuditLogResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AuditLogResponse>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<AuditLogResponse>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PagedResponse<AuditLogResponse>>>> GetReleaseHistoryAsync(
        long releaseCode,
        [FromQuery] ReleaseAuditListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auditQueryService.GetReleaseHistoryAsync(
            releaseCode,
            request,
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
