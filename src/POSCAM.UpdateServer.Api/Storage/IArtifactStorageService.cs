namespace POSCAM.UpdateServer.Api.Storage;

public interface IArtifactStorageService
{
    ArtifactStorageDestination CreateDestination(
        string productCode,
        string channel,
        string version,
        string architecture);

    Task<StagedArtifactFile> SaveToStagingAsync(
        Stream source,
        CancellationToken cancellationToken = default);

    Task ValidatePackageAsync(
        StagedArtifactFile stagedFile,
        CancellationToken cancellationToken = default);

    Task MoveToPackagesAsync(
        StagedArtifactFile stagedFile,
        ArtifactStorageDestination destination,
        CancellationToken cancellationToken = default);

    Task DeleteStagingAsync(
        StagedArtifactFile? stagedFile,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveOrQuarantineAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
