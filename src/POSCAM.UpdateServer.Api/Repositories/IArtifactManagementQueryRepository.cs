using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;

namespace POSCAM.UpdateServer.Api.Repositories;

public interface IArtifactManagementQueryRepository
{
    Task<UpdateArtifact?> GetByTargetForUpdateAsync(
        long releaseCode,
        string operatingSystem,
        string architecture,
        string packageType,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<UpdateArtifact?> GetByCodeForUpdateAsync(
        long artifactCode,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
