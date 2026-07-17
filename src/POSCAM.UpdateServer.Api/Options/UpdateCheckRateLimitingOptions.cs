using System.ComponentModel.DataAnnotations;

namespace POSCAM.UpdateServer.Api.Options;

/// <summary>
/// 공개 Update Check API의 기본 호출 제한.
/// 실제 Rate Limiter 등록은 B09에서 수행한다.
/// </summary>
public sealed class UpdateCheckRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, int.MaxValue)]
    public int UpdateCheckPermitLimit { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int UpdateCheckWindowSeconds { get; set; } = 60;
}
