using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeArtifactRepository : IUpdateArtifactRepository
{
    public IReadOnlyList<UpdateArtifact> Artifacts { get; set; } = Array.Empty<UpdateArtifact>();
    public UpdateArtifact? LastCreatedArtifact { get; private set; }
    public UpdateArtifact? LastReplacedArtifact { get; private set; }
    public bool ReplaceResult { get; set; } = true;
    public long CreatedArtifactCode { get; set; } = 200;
    public Exception? CreateException { get; set; }
    public Exception? ReplaceException { get; set; }

    public Task<UpdateArtifact?> GetByCodeAsync(
        long artifactCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Artifacts.FirstOrDefault(x => x.ArtifactCode == artifactCode));
    }

    public Task<IReadOnlyList<UpdateArtifact>> GetActiveByReleaseAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UpdateArtifact> result = Artifacts
            .Where(x =>
                x.ReleaseCode == releaseCode
                && x.ArtifactStatus == ArtifactStatus.Active)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<UpdateArtifact?> GetByTargetAsync(
        long releaseCode,
        string operatingSystem,
        string architecture,
        string packageType,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Artifacts.FirstOrDefault(x =>
                x.ReleaseCode == releaseCode
                && x.OperatingSystem == operatingSystem
                && x.Architecture == architecture
                && x.PackageType == packageType));
    }

    public Task<long> CreateAsync(
        UpdateArtifact artifact,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (CreateException is not null)
        {
            throw CreateException;
        }

        artifact.ArtifactCode = CreatedArtifactCode;
        LastCreatedArtifact = artifact;
        Artifacts = Artifacts.Append(artifact).ToArray();
        return Task.FromResult(CreatedArtifactCode);
    }

    public Task<bool> ReplaceAsync(
        UpdateArtifact artifact,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (ReplaceException is not null)
        {
            throw ReplaceException;
        }

        LastReplacedArtifact = artifact;

        if (ReplaceResult)
        {
            Artifacts = Artifacts
                .Where(x => x.ArtifactCode != artifact.ArtifactCode)
                .Append(artifact)
                .ToArray();
        }

        return Task.FromResult(ReplaceResult);
    }

    public Task<bool> SetStatusAsync(
        long artifactCode,
        ArtifactStatus status,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var artifact = Artifacts.FirstOrDefault(x => x.ArtifactCode == artifactCode);
        if (artifact is null)
        {
            return Task.FromResult(false);
        }

        var saved = Clone(artifact);
        saved.ArtifactStatus = status;
        saved.UpdatedAt = DateTime.UtcNow;

        Artifacts = Artifacts
            .Where(x => x.ArtifactCode != artifactCode)
            .Append(saved)
            .ToArray();

        return Task.FromResult(true);
    }

    private static UpdateArtifact Clone(UpdateArtifact artifact)
    {
        return new UpdateArtifact
        {
            ArtifactCode = artifact.ArtifactCode,
            ReleaseCode = artifact.ReleaseCode,
            PublicId = artifact.PublicId,
            OperatingSystem = artifact.OperatingSystem,
            Architecture = artifact.Architecture,
            PackageType = artifact.PackageType,
            FileName = artifact.FileName,
            StorageKey = artifact.StorageKey,
            ContentType = artifact.ContentType,
            FileSize = artifact.FileSize,
            Sha256 = artifact.Sha256,
            Signature = artifact.Signature,
            ArtifactStatus = artifact.ArtifactStatus,
            CreatedAt = artifact.CreatedAt,
            UpdatedAt = artifact.UpdatedAt
        };
    }
}
