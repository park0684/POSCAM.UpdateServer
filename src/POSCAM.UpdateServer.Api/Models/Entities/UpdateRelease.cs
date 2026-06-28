using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Entities;

/// <summary>
/// update_releases 테이블과 대응하는 릴리스 Entity.
/// 버전 비교는 Version 문자열이 아니라 숫자 구성요소를 사용한다.
/// </summary>
public sealed class UpdateRelease
{
    public long ReleaseCode { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public int VersionMajor { get; set; }

    public int VersionMinor { get; set; }

    public int VersionPatch { get; set; }

    public int VersionRevision { get; set; }

    public string Channel { get; set; } = string.Empty;

    public string? ForceUpdateBelowVersion { get; set; }

    public bool IsMandatory { get; set; }

    public string? ReleaseNotes { get; set; }

    public string? InternalMemo { get; set; }

    public ReleaseStatus ReleaseStatus { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int? CreatedByUserCode { get; set; }

    public string? CreatedByUserName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
