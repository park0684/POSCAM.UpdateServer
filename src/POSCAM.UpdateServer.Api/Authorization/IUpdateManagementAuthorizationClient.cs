namespace POSCAM.UpdateServer.Api.Authorization;

/// <summary>
/// 관리자 Bearer 토큰을 직접 해석하지 않고 AuthServer 내부 API에 전달해 권한을 확인한다.
/// </summary>
public interface IUpdateManagementAuthorizationClient
{
    Task<UpdateManagementAuthorizationResult> AuthorizeAsync(
        string? authorizationHeader,
        string? requestId,
        CancellationToken cancellationToken = default);
}
