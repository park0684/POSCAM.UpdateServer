using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Domain;

/// <summary>
/// 현재 버전과 선택된 릴리스 정책을 비교한 결과.
/// </summary>
public readonly record struct UpdateDecision(
    bool UpdateAvailable,
    bool Mandatory,
    UpdateDecisionReason Reason)
{
    public string ReasonCode => Reason.ToCode();
}

/// <summary>
/// 현재 클라이언트 버전과 릴리스 정책을 비교해 업데이트 여부와 강제 여부를 판정한다.
/// </summary>
public static class UpdateDecisionEvaluator
{
    public static UpdateDecision Evaluate(
        UpdateVersion currentVersion,
        ReleaseUpdatePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (currentVersion == policy.ReleaseVersion)
        {
            return new UpdateDecision(
                UpdateAvailable: false,
                Mandatory: false,
                Reason: UpdateDecisionReason.AlreadyLatest);
        }

        if (currentVersion > policy.ReleaseVersion)
        {
            return new UpdateDecision(
                UpdateAvailable: false,
                Mandatory: false,
                Reason: UpdateDecisionReason.ClientVersionAhead);
        }

        if (policy.IsMandatory)
        {
            return new UpdateDecision(
                UpdateAvailable: true,
                Mandatory: true,
                Reason: UpdateDecisionReason.MandatoryRelease);
        }

        if (policy.ForceUpdateBelowVersion.HasValue
            && currentVersion < policy.ForceUpdateBelowVersion.Value)
        {
            return new UpdateDecision(
                UpdateAvailable: true,
                Mandatory: true,
                Reason: UpdateDecisionReason.ForceUpdateBelowVersion);
        }

        return new UpdateDecision(
            UpdateAvailable: true,
            Mandatory: false,
            Reason: UpdateDecisionReason.UpdateAvailable);
    }
}
