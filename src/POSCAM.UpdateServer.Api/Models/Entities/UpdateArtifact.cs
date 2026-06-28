using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Entities;

/// <summary>
/// update_artifacts 테이블과 대응하는 패키지 Artifact Entity.
/// 실제 ZIP 데이터는 DB가 아니라 Storage Key가 가리키는 파일 저장소에 보관한다.
/// </summary>
public sealed class UpdateArtifact
{
    public long ArtifactCode { get; set; }

    public long ReleaseCode { get; set; }

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

    public ArtifactStatus ArtifactStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
