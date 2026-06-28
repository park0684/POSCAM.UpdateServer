using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeArtifactManagementQueryRepository
    : IArtifactManagementQueryRepository
{
    public UpdateArtifact? LockedArtifact { get; set; }

    public Task<UpdateArtifact?> GetByTargetForUpdateAsync(
        long releaseCode,
        string operatingSystem,
        string architecture,
        string packageType,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LockedArtifact);
    }
}
