namespace POSCAM.UpdateServer.Api.Models.Dtos.Updates;

/// <summary>
/// 공개 Update Check의 응답 데이터.
/// 업데이트가 없는 정상 응답에서는 package 관련 필드가 null일 수 있다.
/// </summary>
public sealed class UpdateCheckResponse
{
    public bool UpdateAvailable { get; init; }

    public bool Mandatory { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public string CurrentVersion { get; init; } = string.Empty;

    public string? LatestVersion { get; init; }

    public string? ForceUpdateBelowVersion { get; init; }

    public string Channel { get; init; } = string.Empty;

    public string Os { get; init; } = string.Empty;

    public string Architecture { get; init; } = string.Empty;

    public string? PackageType { get; init; }

    public string? PackageUrl { get; init; }

    public string? FileName { get; init; }

    public long? FileSize { get; init; }

    public string? Sha256 { get; init; }

    public string? ReleaseNotes { get; init; }

    public DateTime? PublishedAt { get; init; }
}
