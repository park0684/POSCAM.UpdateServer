using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeArtifactStorageService : IArtifactStorageService
{
    public ArtifactStorageDestination Destination { get; set; } = new()
    {
        PublicId = "new-public-id",
        FileName = "PCCAM_1.0.0_x86.zip",
        StorageKey = "pccam/stable/1.0.0/new-public-id/PCCAM_1.0.0_x86.zip"
    };

    public StagedArtifactFile StagedFile { get; set; } = new()
    {
        PhysicalPath = "fake-staging",
        FileSize = 1234,
        Sha256 = new string('a', 64)
    };

    public ArtifactStorageException? SaveException { get; set; }
    public ArtifactStorageException? ValidateException { get; set; }
    public ArtifactStorageException? MoveException { get; set; }
    public List<string> RemovedStorageKeys { get; } = new();
    public int SaveCallCount { get; private set; }
    public int ValidateCallCount { get; private set; }
    public int MoveCallCount { get; private set; }
    public int DeleteStagingCallCount { get; private set; }

    public ArtifactStorageDestination CreateDestination(
        string productCode,
        string channel,
        string version,
        string architecture)
    {
        return Destination;
    }

    public Task<StagedArtifactFile> SaveToStagingAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        SaveCallCount++;

        if (SaveException is not null)
        {
            throw SaveException;
        }

        return Task.FromResult(StagedFile);
    }

    public Task ValidatePackageAsync(
        StagedArtifactFile stagedFile,
        CancellationToken cancellationToken = default)
    {
        ValidateCallCount++;

        if (ValidateException is not null)
        {
            throw ValidateException;
        }

        return Task.CompletedTask;
    }

    public Task MoveToPackagesAsync(
        StagedArtifactFile stagedFile,
        ArtifactStorageDestination destination,
        CancellationToken cancellationToken = default)
    {
        MoveCallCount++;

        if (MoveException is not null)
        {
            throw MoveException;
        }

        return Task.CompletedTask;
    }

    public Task DeleteStagingAsync(
        StagedArtifactFile? stagedFile,
        CancellationToken cancellationToken = default)
    {
        DeleteStagingCallCount++;
        return Task.CompletedTask;
    }

    public Task<bool> RemoveOrQuarantineAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        RemovedStorageKeys.Add(storageKey);
        return Task.FromResult(true);
    }
}
