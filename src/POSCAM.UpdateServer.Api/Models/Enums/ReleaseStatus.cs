namespace POSCAM.UpdateServer.Api.Models.Enums;

/// <summary>
/// 릴리스 생명주기 상태.
/// 허용되는 방향은 Draft → Published → Disabled뿐이다.
/// </summary>
public enum ReleaseStatus
{
    Draft = 0,
    Published = 1,
    Disabled = 9
}
