using System.Data;
using Dapper;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Queries;

namespace POSCAM.UpdateServer.Api.Repositories;

public sealed class ReleaseManagementQueryRepository
    : DapperRepositoryBase, IReleaseManagementQueryRepository
{
    internal const string GetPagedSql = @"
SELECT
    rel_code AS ReleaseCode,
    rel_product_code AS ProductCode,
    rel_version AS Version,
    rel_version_major AS VersionMajor,
    rel_version_minor AS VersionMinor,
    rel_version_build AS VersionPatch,
    rel_version_revision AS VersionRevision,
    rel_channel AS Channel,
    rel_force_update_below_version AS ForceUpdateBelowVersion,
    rel_is_mandatory AS IsMandatory,
    rel_release_notes AS ReleaseNotes,
    rel_internal_memo AS InternalMemo,
    rel_status AS ReleaseStatus,
    rel_published_at AS PublishedAt,
    rel_created_by_user_code AS CreatedByUserCode,
    rel_created_by_user_name AS CreatedByUserName,
    rel_idate AS CreatedAt,
    rel_udate AS UpdatedAt
FROM update_releases
WHERE (@ProductCode IS NULL OR rel_product_code = @ProductCode)
  AND (@Channel IS NULL OR rel_channel = @Channel)
  AND (@ReleaseStatus IS NULL OR rel_status = @ReleaseStatus)
  AND
  (
      @Keyword IS NULL
      OR rel_version LIKE CONCAT('%', @Keyword, '%')
      OR rel_release_notes LIKE CONCAT('%', @Keyword, '%')
      OR rel_internal_memo LIKE CONCAT('%', @Keyword, '%')
      OR rel_created_by_user_name LIKE CONCAT('%', @Keyword, '%')
  )
ORDER BY
    rel_version_major DESC,
    rel_version_minor DESC,
    rel_version_build DESC,
    rel_version_revision DESC,
    rel_code DESC
LIMIT @Offset, @PageSize;";

    internal const string CountSql = @"
SELECT COUNT(*)
FROM update_releases
WHERE (@ProductCode IS NULL OR rel_product_code = @ProductCode)
  AND (@Channel IS NULL OR rel_channel = @Channel)
  AND (@ReleaseStatus IS NULL OR rel_status = @ReleaseStatus)
  AND
  (
      @Keyword IS NULL
      OR rel_version LIKE CONCAT('%', @Keyword, '%')
      OR rel_release_notes LIKE CONCAT('%', @Keyword, '%')
      OR rel_internal_memo LIKE CONCAT('%', @Keyword, '%')
      OR rel_created_by_user_name LIKE CONCAT('%', @Keyword, '%')
  );";

    internal const string GetByCodeForUpdateSql = @"
SELECT
    rel_code AS ReleaseCode,
    rel_product_code AS ProductCode,
    rel_version AS Version,
    rel_version_major AS VersionMajor,
    rel_version_minor AS VersionMinor,
    rel_version_build AS VersionPatch,
    rel_version_revision AS VersionRevision,
    rel_channel AS Channel,
    rel_force_update_below_version AS ForceUpdateBelowVersion,
    rel_is_mandatory AS IsMandatory,
    rel_release_notes AS ReleaseNotes,
    rel_internal_memo AS InternalMemo,
    rel_status AS ReleaseStatus,
    rel_published_at AS PublishedAt,
    rel_created_by_user_code AS CreatedByUserCode,
    rel_created_by_user_name AS CreatedByUserName,
    rel_idate AS CreatedAt,
    rel_udate AS UpdatedAt
FROM update_releases
WHERE rel_code = @ReleaseCode
LIMIT 1
FOR UPDATE;";

    public ReleaseManagementQueryRepository(IDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<IReadOnlyList<UpdateRelease>> GetPagedAsync(
        ReleaseSearchCriteria criteria,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return ExecuteAsync<IReadOnlyList<UpdateRelease>>(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetPagedSql,
                    CreateParameters(criteria),
                    activeTransaction,
                    cancellationToken);

                var releases = await connection.QueryAsync<UpdateRelease>(command);
                return releases.AsList();
            });
    }

    public Task<long> CountAsync(
        ReleaseSearchCriteria criteria,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return ExecuteAsync(
            transaction,
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

    public Task<UpdateRelease?> GetByCodeForUpdateAsync(
        long releaseCode,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetByCodeForUpdateSql,
                    new { ReleaseCode = releaseCode },
                    activeTransaction,
                    cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<UpdateRelease>(command);
            });
    }

    private static object CreateParameters(ReleaseSearchCriteria criteria)
    {
        return new
        {
            criteria.ProductCode,
            criteria.Channel,
            ReleaseStatus = criteria.Status.HasValue
                ? (int?)criteria.Status.Value
                : null,
            criteria.Keyword,
            criteria.Offset,
            criteria.PageSize
        };
    }
}
