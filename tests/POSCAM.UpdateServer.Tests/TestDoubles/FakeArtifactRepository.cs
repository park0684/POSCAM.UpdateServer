using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeArtifactRepository : IUpdateArtifactRepository
{
    public IReadOnlyList<UpdateArtifact> Artifacts { get; set; } = Array.Empty<UpdateArtifact>();

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
        return Task.FromResult(Artifacts);
    }

    public Task<UpdateArtifact?> GetByTargetAsync(
        long releaseCode,
        string operatingSystem,
        string architecture,
        string packageType,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<UpdateArtifact?>(null);
    }

    public Task<long> CreateAsync(
        UpdateArtifact artifact,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<bool> ReplaceAsync(
        UpdateArtifact artifact,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<bool> SetStatusAsync(
        long artifactCode,
        ArtifactStatus status,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
