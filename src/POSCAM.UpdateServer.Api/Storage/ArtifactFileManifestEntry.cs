namespace POSCAM.UpdateServer.Api.Storage;

/// <summary>
/// ZIP 내부 파일을 파일별 복구 대상으로 저장한 결과.
/// DB Entity가 아니라 Storage 단계의 생성 결과를 나타낸다.
/// </summary>
public sealed class ArtifactFileManifestEntry
{
    public string PublicId { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public string Sha256 { get; init; } = string.Empty;

    public string StorageKey { get; init; } = string.Empty;

    public string DownloadPath { get; init; } = string.Empty;

    public bool IsRequired { get; init; } = true;

    public string PhysicalPath { get; init; } = string.Empty;
}
