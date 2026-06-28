using System.ComponentModel.DataAnnotations;

namespace POSCAM.UpdateServer.Api.Options;

/// <summary>
/// 관리자 요청 권한 확인에 사용할 AuthServer 내부 연결 설정.
/// 실제 서비스 키는 환경변수 AuthServer__InternalServiceKey로 주입한다.
/// </summary>
public sealed class AuthServerOptions
{
    public const string SectionName = "AuthServer";

    [Required]
    public string BaseUrl { get; set; } = "http://poscam-auth-api:8080";

    public string InternalServiceKey { get; set; } = string.Empty;
}
