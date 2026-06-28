namespace POSCAM.UpdateServer.Api.Authorization;

/// <summary>
/// AuthServer의 ApiResponse&lt;UpdateManagementActorResponse&gt; JSON 계약.
/// AuthServer 프로젝트를 참조하지 않고 필요한 필드만 독립적으로 정의한다.
/// </summary>
internal sealed class AuthServerAuthorizationResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public int ErrorCode { get; set; }

    public AuthServerActorData? Data { get; set; }
}

internal sealed class AuthServerActorData
{
    public int UserCode { get; set; }

    public string UserName { get; set; } = string.Empty;

    public int UserRole { get; set; }
}
