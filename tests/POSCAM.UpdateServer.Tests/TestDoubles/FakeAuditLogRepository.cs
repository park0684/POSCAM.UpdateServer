using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeAuditLogRepository : IUpdateAuditLogRepository
{
    public List<UpdateAuditLog> CreatedLogs { get; } = new();

    public Task<long> CreateAsync(
        UpdateAuditLog auditLog,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        auditLog.AuditLogCode = CreatedLogs.Count + 1;
        CreatedLogs.Add(auditLog);
        return Task.FromResult(auditLog.AuditLogCode);
    }

    public Task<IReadOnlyList<UpdateAuditLog>> GetByTargetAsync(
        string targetType,
        string targetCode,
        int offset,
        int pageSize,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UpdateAuditLog> result = CreatedLogs
            .Where(x => x.TargetType == targetType && x.TargetCode == targetCode)
            .Skip(offset)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<long> CountByTargetAsync(
        string targetType,
        string targetCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            (long)CreatedLogs.Count(x => x.TargetType == targetType && x.TargetCode == targetCode));
    }
}
