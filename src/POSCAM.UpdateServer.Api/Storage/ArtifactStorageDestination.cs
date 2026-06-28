namespace POSCAM.UpdateServer.Api.Storage;

public sealed class ArtifactStorageDestination
{
    public string PublicId { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string StorageKey { get; init; } = string.Empty;
}
