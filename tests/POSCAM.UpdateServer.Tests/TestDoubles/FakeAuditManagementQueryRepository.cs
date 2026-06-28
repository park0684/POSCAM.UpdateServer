using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Queries;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeAuditManagementQueryRepository
    : IAuditManagementQueryRepository
{
    public IReadOnlyList<UpdateAuditLog> Logs { get; set; } = Array.Empty<UpdateAuditLog>();

    public long TotalCount { get; set; }

    public AuditSearchCriteria? LastCriteria { get; private set; }

    public long? LastReleaseCode { get; private set; }

    public string? LastReleaseAction { get; private set; }

    public int LastOffset { get; private set; }

    public int LastPageSize { get; private set; }

    public Task<IReadOnlyList<UpdateAuditLog>> GetPagedAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        LastCriteria = criteria;
        return Task.FromResult(Logs);
    }

    public Task<long> CountAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        LastCriteria = criteria;
        return Task.FromResult(TotalCount);
    }

    public Task<IReadOnlyList<UpdateAuditLog>> GetReleaseHistoryAsync(
        long releaseCode,
        string? action,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        LastReleaseCode = releaseCode;
        LastReleaseAction = action;
        LastOffset = offset;
        LastPageSize = pageSize;
        return Task.FromResult(Logs);
    }

    public Task<long> CountReleaseHistoryAsync(
        long releaseCode,
        string? action,
        CancellationToken cancellationToken = default)
    {
        LastReleaseCode = releaseCode;
        LastReleaseAction = action;
        return Task.FromResult(TotalCount);
    }
}
