using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Domain;

/// <summary>
/// 릴리스 상태 전이 검증 결과.
/// </summary>
public readonly record struct ReleaseTransitionResult(
    bool IsAllowed,
    ReleaseTransitionError Error);

/// <summary>
/// 릴리스는 Draft → Published → Disabled 방향으로만 이동할 수 있다.
/// </summary>
public static class ReleaseStateMachine
{
    public static ReleaseTransitionResult Evaluate(
        ReleaseStatus currentStatus,
        ReleaseStatus nextStatus)
    {
        if (currentStatus == nextStatus)
        {
            return new ReleaseTransitionResult(
                IsAllowed: false,
                Error: ReleaseTransitionError.SameStatus);
        }

        if (currentStatus == ReleaseStatus.Draft
            && nextStatus == ReleaseStatus.Published)
        {
            return new ReleaseTransitionResult(
                IsAllowed: true,
                Error: ReleaseTransitionError.None);
        }

        if (currentStatus == ReleaseStatus.Published
            && nextStatus == ReleaseStatus.Disabled)
        {
            return new ReleaseTransitionResult(
                IsAllowed: true,
                Error: ReleaseTransitionError.None);
        }

        if (currentStatus == ReleaseStatus.Published
            && nextStatus == ReleaseStatus.Draft)
        {
            return new ReleaseTransitionResult(
                IsAllowed: false,
                Error: ReleaseTransitionError.PublishedCannotReturnToDraft);
        }

        if (currentStatus == ReleaseStatus.Disabled)
        {
            return new ReleaseTransitionResult(
                IsAllowed: false,
                Error: ReleaseTransitionError.DisabledReleaseCannotTransition);
        }

        return new ReleaseTransitionResult(
            IsAllowed: false,
            Error: ReleaseTransitionError.InvalidTransition);
    }

    public static bool CanTransition(
        ReleaseStatus currentStatus,
        ReleaseStatus nextStatus)
    {
        return Evaluate(currentStatus, nextStatus).IsAllowed;
    }
}
