using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;

namespace POSCAM.UpdateServer.Api.Repositories;

public interface IUpdateArtifactFileRepository
{
    Task<IReadOnlyList<UpdateArtifactFile>> GetActiveByArtifactAsync(
        long artifactCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByArtifactAsync(
        long artifactCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task CreateManyAsync(
        IReadOnlyList<UpdateArtifactFile> files,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<int> DeleteByArtifactAsync(
        long artifactCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
