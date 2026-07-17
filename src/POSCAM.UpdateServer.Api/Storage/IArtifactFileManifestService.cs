namespace POSCAM.UpdateServer.Api.Storage;

public interface IArtifactFileManifestService
{
    Task<IReadOnlyList<ArtifactFileManifestEntry>> CreateManifestFilesAsync(
        string zipFilePath,
        ArtifactStorageDestination artifactDestination,
        CancellationToken cancellationToken = default);

    Task DeleteManifestFilesAsync(
        IReadOnlyList<ArtifactFileManifestEntry>? files,
        CancellationToken cancellationToken = default);
}
