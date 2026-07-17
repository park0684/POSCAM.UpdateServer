namespace POSCAM.UpdateServer.Api.Models.Enums;

/// <summary>
/// 클라이언트 또는 관리자가 전달한 버전 문자열을 해석하지 못한 원인.
/// 잘못된 버전을 0.0.0 등으로 자동 보정하지 않고 명시적으로 거부하기 위해 사용한다.
/// </summary>
public enum UpdateVersionParseError
{
    None = 0,
    Required = 1,
    InvalidFormat = 2,
    LeadingZeroNotAllowed = 3,
    ComponentOutOfRange = 4
}
