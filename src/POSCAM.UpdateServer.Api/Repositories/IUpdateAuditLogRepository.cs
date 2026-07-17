using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;

namespace POSCAM.UpdateServer.Api.Repositories;

public interface IUpdateAuditLogRepository
{
    Task<long> CreateAsync(
        UpdateAuditLog auditLog,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UpdateAuditLog>> GetByTargetAsync(
        string targetType,
        string targetCode,
        int offset,
        int pageSize,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<long> CountByTargetAsync(
        string targetType,
        string targetCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
