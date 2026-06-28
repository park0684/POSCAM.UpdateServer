using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Domain;

/// <summary>
/// 하나의 릴리스가 클라이언트에 적용할 강제 업데이트 정책.
/// 전체 강제와 기준 버전 미만 강제는 동시에 사용할 수 없다.
/// </summary>
public sealed record ReleaseUpdatePolicy
{
    public UpdateVersion ReleaseVersion { get; }

    public bool IsMandatory { get; }

    public UpdateVersion? ForceUpdateBelowVersion { get; }

    private ReleaseUpdatePolicy(
        UpdateVersion releaseVersion,
        bool isMandatory,
        UpdateVersion? forceUpdateBelowVersion)
    {
        ReleaseVersion = releaseVersion;
        IsMandatory = isMandatory;
        ForceUpdateBelowVersion = forceUpdateBelowVersion;
    }

    public static bool TryCreate(
        UpdateVersion releaseVersion,
        bool isMandatory,
        UpdateVersion? forceUpdateBelowVersion,
        out ReleaseUpdatePolicy? policy,
        out ReleasePolicyValidationError error)
    {
        if (isMandatory && forceUpdateBelowVersion.HasValue)
        {
            policy = null;
            error = ReleasePolicyValidationError.MandatoryAndThresholdCannotCoexist;
            return false;
        }

        if (forceUpdateBelowVersion.HasValue
            && forceUpdateBelowVersion.Value > releaseVersion)
        {
            policy = null;
            error = ReleasePolicyValidationError.ForceThresholdExceedsReleaseVersion;
            return false;
        }

        policy = new ReleaseUpdatePolicy(
            releaseVersion,
            isMandatory,
            forceUpdateBelowVersion);

        error = ReleasePolicyValidationError.None;
        return true;
    }
}
