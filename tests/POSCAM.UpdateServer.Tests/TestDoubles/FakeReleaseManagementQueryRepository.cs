using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Queries;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeReleaseManagementQueryRepository
    : IReleaseManagementQueryRepository
{
    public IReadOnlyList<UpdateRelease> PagedReleases { get; set; } = Array.Empty<UpdateRelease>();
    public long TotalCount { get; set; }
    public UpdateRelease? LockedRelease { get; set; }
    public ReleaseSearchCriteria? LastCriteria { get; private set; }

    public Task<IReadOnlyList<UpdateRelease>> GetPagedAsync(
        ReleaseSearchCriteria criteria,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        LastCriteria = criteria;
        return Task.FromResult(PagedReleases);
    }

    public Task<long> CountAsync(
        ReleaseSearchCriteria criteria,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        LastCriteria = criteria;
        return Task.FromResult(TotalCount);
    }

    public Task<UpdateRelease?> GetByCodeForUpdateAsync(
        long releaseCode,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LockedRelease);
    }
}
