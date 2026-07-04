using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeArtifactFileManifestService : IArtifactFileManifestService
{
    public IReadOnlyList<ArtifactFileManifestEntry> ManifestFiles { get; set; } =
        new[]
        {
            new ArtifactFileManifestEntry
            {
                PublicId = "manifest-file-public-id",
                FilePath = "PCCAM.exe",
                FileSize = 10,
                Sha256 = new string('c', 64),
                StorageKey = "pccam/stable/1.0.0/new-public-id/files/manifest-file-public-id",
                DownloadPath = "pccam/stable/1.0.0/new-public-id/files/manifest-file-public-id",
                IsRequired = true,
                PhysicalPath = "fake-manifest-file"
            }
        };

    public ArtifactStorageException? CreateException { get; set; }
    public List<IReadOnlyList<ArtifactFileManifestEntry>> DeletedManifests { get; } = new();
    public int CreateCallCount { get; private set; }

    public Task<IReadOnlyList<ArtifactFileManifestEntry>> CreateManifestFilesAsync(
        string zipFilePath,
        ArtifactStorageDestination artifactDestination,
        CancellationToken cancellationToken = default)
    {
        CreateCallCount++;

        if (CreateException is not null)
        {
            throw CreateException;
        }

        return Task.FromResult(ManifestFiles);
    }

    public Task DeleteManifestFilesAsync(
        IReadOnlyList<ArtifactFileManifestEntry>? files,
        CancellationToken cancellationToken = default)
    {
        if (files is not null)
        {
            DeletedManifests.Add(files);
        }

        return Task.CompletedTask;
    }
}
