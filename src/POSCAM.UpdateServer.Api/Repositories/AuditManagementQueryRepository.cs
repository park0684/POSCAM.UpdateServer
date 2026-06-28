using Dapper;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Queries;

namespace POSCAM.UpdateServer.Api.Repositories;

public sealed class AuditManagementQueryRepository
    : DapperRepositoryBase, IAuditManagementQueryRepository
{
    internal const string GetPagedSql = @"
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
WHERE (@Action IS NULL OR ual_action = @Action)
  AND (@TargetType IS NULL OR ual_target_type = @TargetType)
  AND (@TargetCode IS NULL OR ual_target_code = @TargetCode)
  AND (@ActorUserCode IS NULL OR ual_actor_user_code = @ActorUserCode)
  AND (@RequestId IS NULL OR ual_request_id = @RequestId)
  AND (@FromUtc IS NULL OR ual_idate >= @FromUtc)
  AND (@ToUtc IS NULL OR ual_idate <= @ToUtc)
ORDER BY ual_idate DESC, ual_code DESC
LIMIT @Offset, @PageSize;";

    internal const string CountSql = @"
SELECT COUNT(*)
FROM update_audit_logs
WHERE (@Action IS NULL OR ual_action = @Action)
  AND (@TargetType IS NULL OR ual_target_type = @TargetType)
  AND (@TargetCode IS NULL OR ual_target_code = @TargetCode)
  AND (@ActorUserCode IS NULL OR ual_actor_user_code = @ActorUserCode)
  AND (@RequestId IS NULL OR ual_request_id = @RequestId)
  AND (@FromUtc IS NULL OR ual_idate >= @FromUtc)
  AND (@ToUtc IS NULL OR ual_idate <= @ToUtc);";

    internal const string GetReleaseHistorySql = @"
SELECT
    l.ual_code AS AuditLogCode,
    l.ual_action AS Action,
    l.ual_target_type AS TargetType,
    l.ual_target_code AS TargetCode,
    l.ual_actor_user_code AS ActorUserCode,
    l.ual_actor_user_name AS ActorUserName,
    l.ual_before_data AS BeforeData,
    l.ual_after_data AS AfterData,
    l.ual_ip_address AS IpAddress,
    l.ual_user_agent AS UserAgent,
    l.ual_request_id AS RequestId,
    l.ual_idate AS CreatedAt
FROM update_audit_logs l
WHERE (@Action IS NULL OR l.ual_action = @Action)
  AND
  (
      (l.ual_target_type = 'RELEASE' AND l.ual_target_code = CAST(@ReleaseCode AS CHAR))
      OR
      (
          l.ual_target_type = 'ARTIFACT'
          AND EXISTS
          (
              SELECT 1
              FROM update_artifacts a
              WHERE a.art_release_code = @ReleaseCode
                AND l.ual_target_code = CAST(a.art_code AS CHAR)
          )
      )
  )
ORDER BY l.ual_idate DESC, l.ual_code DESC
LIMIT @Offset, @PageSize;";

    internal const string CountReleaseHistorySql = @"
SELECT COUNT(*)
FROM update_audit_logs l
WHERE (@Action IS NULL OR l.ual_action = @Action)
  AND
  (
      (l.ual_target_type = 'RELEASE' AND l.ual_target_code = CAST(@ReleaseCode AS CHAR))
      OR
      (
          l.ual_target_type = 'ARTIFACT'
          AND EXISTS
          (
              SELECT 1
              FROM update_artifacts a
              WHERE a.art_release_code = @ReleaseCode
                AND l.ual_target_code = CAST(a.art_code AS CHAR)
          )
      )
  );";

    public AuditManagementQueryRepository(IDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<IReadOnlyList<UpdateAuditLog>> GetPagedAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return ExecuteAsync<IReadOnlyList<UpdateAuditLog>>(
            null,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetPagedSql,
                    CreateParameters(criteria),
                    activeTransaction,
                    cancellationToken);

                var logs = await connection.QueryAsync<UpdateAuditLog>(command);
                return logs.AsList();
            });
    }

    public Task<long> CountAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return ExecuteAsync(
            null,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    CountSql,
                    CreateParameters(criteria),
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteScalarAsync<long>(command);
            });
    }

    public Task<IReadOnlyList<UpdateAuditLog>> GetReleaseHistoryAsync(
        long releaseCode,
        string? action,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<IReadOnlyList<UpdateAuditLog>>(
            null,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetReleaseHistorySql,
                    new
                    {
                        ReleaseCode = releaseCode,
                        Action = action,
                        Offset = offset,
                        PageSize = pageSize
                    },
                    activeTransaction,
                    cancellationToken);

                var logs = await connection.QueryAsync<UpdateAuditLog>(command);
                return logs.AsList();
            });
    }

    public Task<long> CountReleaseHistoryAsync(
        long releaseCode,
        string? action,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            null,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    CountReleaseHistorySql,
                    new
                    {
                        ReleaseCode = releaseCode,
                        Action = action
                    },
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteScalarAsync<long>(command);
            });
    }

    private static object CreateParameters(AuditSearchCriteria criteria)
    {
        return new
        {
            criteria.Action,
            criteria.TargetType,
            criteria.TargetCode,
            criteria.ActorUserCode,
            criteria.RequestId,
            criteria.FromUtc,
            criteria.ToUtc,
            criteria.Offset,
            criteria.PageSize
        };
    }
}
