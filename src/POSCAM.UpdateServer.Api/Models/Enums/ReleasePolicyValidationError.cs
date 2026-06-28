namespace POSCAM.UpdateServer.Api.Models.Enums;

/// <summary>
/// 릴리스 강제 업데이트 정책이 유효하지 않은 원인.
/// </summary>
public enum ReleasePolicyValidationError
{
    None = 0,
    MandatoryAndThresholdCannotCoexist = 1,
    ForceThresholdExceedsReleaseVersion = 2
}
