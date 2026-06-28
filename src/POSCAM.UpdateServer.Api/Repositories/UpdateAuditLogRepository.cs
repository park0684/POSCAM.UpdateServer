using System.Data;
using Dapper;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Entities;

namespace POSCAM.UpdateServer.Api.Repositories;

public sealed class UpdateAuditLogRepository : DapperRepositoryBase, IUpdateAuditLogRepository
{
    internal const string CreateSql = @"
INSERT INTO update_audit_logs
(
    ual_action,
    ual_target_type,
    ual_target_code,
    ual_actor_user_code,
    ual_actor_user_name,
    ual_before_data,
    ual_after_data,
    ual_ip_address,
    ual_user_agent,
    ual_request_id,
    ual_idate
)
VALUES
(
    @Action,
    @TargetType,
    @TargetCode,
    @ActorUserCode,
    @ActorUserName,
    @BeforeData,
    @AfterData,
    @IpAddress,
    @UserAgent,
    @RequestId,
    UTC_TIMESTAMP()
);";

    internal const string LastInsertIdSql = "SELECT LAST_INSERT_ID();";

    internal const string GetByTargetSql = @"
SELECT
    ual_code AS AuditLogCode,
    ual_action AS Action,
    ual_target_type AS TargetType,
    ual_target_code AS TargetCode,
    ual_actor_user_code AS ActorUserCode,
    ual_actor_user_name AS ActorUserName,
    ual_before_data AS BeforeData,
    ual_after_data AS AfterData,
    ual_ip_address AS IpAddress,
    ual_user_agent AS UserAgent,
    ual_request_id AS RequestId,
    ual_idate AS CreatedAt
FROM update_audit_logs
WHERE ual_target_type = @TargetType
  AND ual_target_code = @TargetCode
ORDER BY ual_idate DESC, ual_code DESC
LIMIT @Offset, @PageSize;";

    internal const string CountByTargetSql = @"
SELECT COUNT(*)
FROM update_audit_logs
WHERE ual_target_type = @TargetType
  AND ual_target_code = @TargetCode;";

    public UpdateAuditLogRepository(IDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<long> CreateAsync(
        UpdateAuditLog auditLog,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditLog);

        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var insertCommand = CreateCommand(
                    CreateSql,
                    auditLog,
                    activeTransaction,
                    cancellationToken);

                await connection.ExecuteAsync(insertCommand);

                var identityCommand = CreateCommand(
                    LastInsertIdSql,
                    parameters: null,
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteScalarAsync<long>(identityCommand);
            });
    }

    public Task<IReadOnlyList<UpdateAuditLog>> GetByTargetAsync(
        string targetType,
        string targetCode,
        int offset,
        int pageSize,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<IReadOnlyList<UpdateAuditLog>>(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetByTargetSql,
                    new
                    {
                        TargetType = targetType,
                        TargetCode = targetCode,
                        Offset = offset,
                        PageSize = pageSize
                    },
                    activeTransaction,
                    cancellationToken);

                var logs = await connection.QueryAsync<UpdateAuditLog>(command);
                return logs.AsList();
            });
    }

    public Task<long> CountByTargetAsync(
        string targetType,
        string targetCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    CountByTargetSql,
                    new
                    {
                        TargetType = targetType,
                        TargetCode = targetCode
                    },
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteScalarAsync<long>(command);
            });
    }
}
