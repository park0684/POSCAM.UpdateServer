using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Dtos.Updates;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Services;

namespace POSCAM.UpdateServer.Api.Controllers;

[ApiController]
[Route("api/v1/updates")]
public sealed class UpdatesController : ControllerBase
{
    private readonly IUpdateCheckService _updateCheckService;

    public UpdatesController(IUpdateCheckService updateCheckService)
    {
        _updateCheckService = updateCheckService;
    }

    /// <summary>
    /// 현재 버전과 실행 환경을 기준으로 호환되는 최신 업데이트를 확인한다.
    /// 라이선스 또는 관리자 인증 없이 익명 호출할 수 있다.
    /// </summary>
    [HttpPost("check")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(ApiResponse<UpdateCheckResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UpdateCheckResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UpdateCheckResponse>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<UpdateCheckResponse>>> CheckAsync(
        [FromBody] UpdateCheckRequest? request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        var result = await _updateCheckService.CheckAsync(
            request,
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            return Ok(
                ApiResponse<UpdateCheckResponse>.Ok(
                    result.Data,
                    result.Message));
        }

        var statusCode = GetFailureStatusCode(result.ErrorCode);

        return StatusCode(
            statusCode,
            ApiResponse<UpdateCheckResponse>.Fail(
                result.ErrorCode,
                result.Message));
    }

    private static int GetFailureStatusCode(UpdateErrorCode errorCode)
    {
        return errorCode switch
        {
            UpdateErrorCode.InvalidProduct => StatusCodes.Status400BadRequest,
            UpdateErrorCode.ProductInactive => StatusCodes.Status400BadRequest,
            UpdateErrorCode.InvalidVersion => StatusCodes.Status400BadRequest,
            UpdateErrorCode.InvalidOperatingSystem => StatusCodes.Status400BadRequest,
            UpdateErrorCode.InvalidArchitecture => StatusCodes.Status400BadRequest,
            UpdateErrorCode.InvalidChannel => StatusCodes.Status400BadRequest,
            UpdateErrorCode.ValidationError => StatusCodes.Status400BadRequest,
            UpdateErrorCode.DatabaseError => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}
