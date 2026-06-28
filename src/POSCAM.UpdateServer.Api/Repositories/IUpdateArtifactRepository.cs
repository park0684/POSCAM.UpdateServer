using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Repositories;

public interface IUpdateArtifactRepository
{
    Task<UpdateArtifact?> GetByCodeAsync(
        long artifactCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UpdateArtifact>> GetActiveByReleaseAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<UpdateArtifact?> GetByTargetAsync(
        long releaseCode,
        string operatingSystem,
        string architecture,
        string packageType,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<long> CreateAsync(
        UpdateArtifact artifact,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(
        UpdateArtifact artifact,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> SetStatusAsync(
        long artifactCode,
        ArtifactStatus status,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
