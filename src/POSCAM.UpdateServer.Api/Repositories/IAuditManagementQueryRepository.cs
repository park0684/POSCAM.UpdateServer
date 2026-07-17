using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Queries;

namespace POSCAM.UpdateServer.Api.Repositories;

public interface IAuditManagementQueryRepository
{
    Task<IReadOnlyList<UpdateAuditLog>> GetPagedAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UpdateAuditLog>> GetReleaseHistoryAsync(
        long releaseCode,
        string? action,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<long> CountReleaseHistoryAsync(
        long releaseCode,
        string? action,
        CancellationToken cancellationToken = default);
}
