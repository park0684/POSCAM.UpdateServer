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

    public QuarantinedArtifactFile QuarantinedFile { get; set; } = new()
    {
        StorageKey = "pccam/stable/1.0.0/public-id/package.zip",
        FileMoved = true,
        OriginalPhysicalPath = "fake-packages",
        QuarantinePhysicalPath = "fake-quarantine"
    };

    public ArtifactStorageException? SaveException { get; set; }
    public ArtifactStorageException? ValidateException { get; set; }
    public ArtifactStorageException? StoredValidationException { get; set; }
    public ArtifactStorageException? MoveException { get; set; }
    public ArtifactStorageException? QuarantineException { get; set; }
    public bool RestoreResult { get; set; } = true;
    public List<string> RemovedStorageKeys { get; } = new();
    public List<string> ValidatedStorageKeys { get; } = new();
    public List<string> QuarantinedStorageKeys { get; } = new();
    public int SaveCallCount { get; private set; }
    public int ValidateCallCount { get; private set; }
    public int StoredValidationCallCount { get; private set; }
    public int MoveCallCount { get; private set; }
    public int DeleteStagingCallCount { get; private set; }
    public int RestoreCallCount { get; private set; }

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

    public Task ValidateStoredArtifactAsync(
        string storageKey,
        long expectedFileSize,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        StoredValidationCallCount++;
        ValidatedStorageKeys.Add(storageKey);

        if (StoredValidationException is not null)
        {
            throw StoredValidationException;
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

    public Task<QuarantinedArtifactFile> QuarantineAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        QuarantinedStorageKeys.Add(storageKey);

        if (QuarantineException is not null)
        {
            throw QuarantineException;
        }

        QuarantinedFile = new QuarantinedArtifactFile
        {
            StorageKey = storageKey,
            FileMoved = QuarantinedFile.FileMoved,
            OriginalPhysicalPath = QuarantinedFile.OriginalPhysicalPath,
            QuarantinePhysicalPath = QuarantinedFile.QuarantinePhysicalPath
        };

        return Task.FromResult(QuarantinedFile);
    }

    public Task<bool> RestoreFromQuarantineAsync(
        QuarantinedArtifactFile quarantinedFile,
        CancellationToken cancellationToken = default)
    {
        RestoreCallCount++;
        return Task.FromResult(RestoreResult);
    }
}
