namespace POSCAM.UpdateServer.Api.Models.Enums;

/// <summary>
/// 공개 Update Check 정상 응답에 사용할 판정 사유.
/// 업데이트가 없는 경우도 오류가 아니므로 ErrorCode가 아닌 ReasonCode로 전달한다.
/// </summary>
public enum UpdateDecisionReason
{
    UpdateAvailable = 1,
    MandatoryRelease = 2,
    ForceUpdateBelowVersion = 3,
    AlreadyLatest = 4,
    ClientVersionAhead = 5,
    NoAvailableRelease = 6,
    NoCompatibleArtifact = 7
}

/// <summary>
/// API 계약에 정의된 고정 ReasonCode 문자열로 변환한다.
/// </summary>
public static class UpdateDecisionReasonExtensions
{
    public static string ToCode(this UpdateDecisionReason reason)
    {
        return reason switch
        {
            UpdateDecisionReason.UpdateAvailable => "UPDATE_AVAILABLE",
            UpdateDecisionReason.MandatoryRelease => "MANDATORY_RELEASE",
            UpdateDecisionReason.ForceUpdateBelowVersion => "FORCE_UPDATE_BELOW_VERSION",
            UpdateDecisionReason.AlreadyLatest => "ALREADY_LATEST",
            UpdateDecisionReason.ClientVersionAhead => "CLIENT_VERSION_AHEAD",
            UpdateDecisionReason.NoAvailableRelease => "NO_AVAILABLE_RELEASE",
            UpdateDecisionReason.NoCompatibleArtifact => "NO_COMPATIBLE_ARTIFACT",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };
    }
}
