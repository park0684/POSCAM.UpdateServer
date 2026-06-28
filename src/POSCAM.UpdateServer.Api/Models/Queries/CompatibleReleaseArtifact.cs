namespace POSCAM.UpdateServer.Api.Models.Queries;

public sealed class CompatibleReleaseArtifact
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
    public DateTime? PublishedAt { get; set; }
    public long ArtifactCode { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string PackageType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string? Signature { get; set; }
}
