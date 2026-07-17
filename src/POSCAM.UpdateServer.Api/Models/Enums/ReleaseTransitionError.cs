namespace POSCAM.UpdateServer.Api.Models.Enums;

/// <summary>
/// 릴리스 상태 전이가 거부된 원인.
/// </summary>
public enum ReleaseTransitionError
{
    None = 0,
    SameStatus = 1,
    PublishedCannotReturnToDraft = 2,
    DisabledReleaseCannotTransition = 3,
    InvalidTransition = 4
}
