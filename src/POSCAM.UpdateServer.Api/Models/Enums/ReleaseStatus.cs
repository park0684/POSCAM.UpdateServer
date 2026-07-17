namespace POSCAM.UpdateServer.Api.Models.Enums;

/// <summary>
/// 릴리스 생명주기 상태.
/// Draft는 최초 게시에 사용하고, Published는 배포 중 상태,
/// Disabled는 수정과 재게시가 가능한 배포 중지 상태로 사용한다.
/// </summary>
public enum ReleaseStatus
{
    Draft = 0,
    Published = 1,
    Disabled = 9
}
