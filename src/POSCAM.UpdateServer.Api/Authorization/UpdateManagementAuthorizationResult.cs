using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Authorization;

/// <summary>
/// AuthServer 내부 권한 확인 결과를 UpdateServer 응답 체계로 변환한 값.
/// </summary>
public sealed class UpdateManagementAuthorizationResult
{
    public bool Authorized { get; init; }

    public int HttpStatusCode { get; init; }

    public UpdateErrorCode ErrorCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public UpdateManagementActor? Actor { get; init; }

    public static UpdateManagementAuthorizationResult Allow(
        UpdateManagementActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return new UpdateManagementAuthorizationResult
        {
            Authorized = true,
            HttpStatusCode = StatusCodes.Status200OK,
            ErrorCode = UpdateErrorCode.None,
            Message = "업데이트 관리 권한이 확인되었습니다.",
            Actor = actor
        };
    }

    public static UpdateManagementAuthorizationResult Deny(
        int httpStatusCode,
        UpdateErrorCode errorCode,
        string message)
    {
        return new UpdateManagementAuthorizationResult
        {
            Authorized = false,
            HttpStatusCode = httpStatusCode,
            ErrorCode = errorCode,
            Message = message,
            Actor = null
        };
    }
}
