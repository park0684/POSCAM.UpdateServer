using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Models.Entities;

/// <summary>
/// update_artifact_files 테이블과 대응하는 파일별 Manifest Entity.
/// 실제 파일 데이터는 DB가 아니라 Storage Key가 가리키는 파일 저장소에 보관한다.
/// </summary>
public sealed class UpdateArtifactFile
{
    public long FileCode { get; set; }

    public long ArtifactCode { get; set; }

    public string PublicId { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public string DownloadPath { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public ArtifactFileStatus FileStatus { get; set; }

    public DateTime CreatedAt { get; set; }
}
