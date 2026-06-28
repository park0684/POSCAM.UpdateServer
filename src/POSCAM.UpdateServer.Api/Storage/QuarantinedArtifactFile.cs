namespace POSCAM.UpdateServer.Api.Storage;

/// <summary>
/// packages에서 quarantine으로 이동된 파일의 복구 정보.
/// 물리 경로는 외부 응답에 노출하지 않는다.
/// </summary>
public sealed class QuarantinedArtifactFile
{
    internal string OriginalPhysicalPath { get; init; } = string.Empty;

    internal string QuarantinePhysicalPath { get; init; } = string.Empty;

    public string StorageKey { get; init; } = string.Empty;

    public bool FileMoved { get; init; }
}
