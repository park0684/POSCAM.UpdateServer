using System.Data;
using Dapper;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Models.Queries;

namespace POSCAM.UpdateServer.Api.Repositories;

public sealed class UpdateReleaseRepository : DapperRepositoryBase, IUpdateReleaseRepository
{
    internal const string GetByCodeSql = @"
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
LIMIT 1;";

    internal const string ExistsVersionSql = @"
SELECT EXISTS
(
    SELECT 1
    FROM update_releases
    WHERE rel_product_code = @ProductCode
      AND rel_channel = @Channel
      AND rel_version_major = @VersionMajor
      AND rel_version_minor = @VersionMinor
      AND rel_version_build = @VersionPatch
      AND rel_version_revision = @VersionRevision
      AND (@ExcludeReleaseCode IS NULL OR rel_code <> @ExcludeReleaseCode)
);";

    internal const string CreateDraftSql = @"
INSERT INTO update_releases
(
    rel_product_code,
    rel_version,
    rel_version_major,
    rel_version_minor,
    rel_version_build,
    rel_version_revision,
    rel_channel,
    rel_force_update_below_version,
    rel_is_mandatory,
    rel_release_notes,
    rel_internal_memo,
    rel_status,
    rel_published_at,
    rel_created_by_user_code,
    rel_created_by_user_name,
    rel_idate,
    rel_udate
)
VALUES
(
    @ProductCode,
    @Version,
    @VersionMajor,
    @VersionMinor,
    @VersionPatch,
    @VersionRevision,
    @Channel,
    @ForceUpdateBelowVersion,
    @IsMandatory,
    @ReleaseNotes,
    @InternalMemo,
    @ReleaseStatus,
    NULL,
    @CreatedByUserCode,
    @CreatedByUserName,
    UTC_TIMESTAMP(),
    NULL
);";

    internal const string LastInsertIdSql = "SELECT LAST_INSERT_ID();";

    internal const string UpdateDraftSql = @"
UPDATE update_releases
SET rel_product_code = @ProductCode,
    rel_version = @Version,
    rel_version_major = @VersionMajor,
    rel_version_minor = @VersionMinor,
    rel_version_build = @VersionPatch,
    rel_version_revision = @VersionRevision,
    rel_channel = @Channel,
    rel_force_update_below_version = @ForceUpdateBelowVersion,
    rel_is_mandatory = @IsMandatory,
    rel_release_notes = @ReleaseNotes,
    rel_internal_memo = @InternalMemo,
    rel_udate = UTC_TIMESTAMP()
WHERE rel_code = @ReleaseCode
  AND rel_status = @DraftStatus;";

    internal const string DeleteDraftSql = @"
DELETE FROM update_releases
WHERE rel_code = @ReleaseCode
  AND rel_status = @DraftStatus;";

    internal const string PublishSql = @"
UPDATE update_releases
SET rel_status = @PublishedStatus,
    rel_published_at = UTC_TIMESTAMP(),
    rel_udate = UTC_TIMESTAMP()
WHERE rel_code = @ReleaseCode
  AND rel_status = @DraftStatus;";

    internal const string DisableSql = @"
UPDATE update_releases
SET rel_status = @DisabledStatus,
    rel_udate = UTC_TIMESTAMP()
WHERE rel_code = @ReleaseCode
  AND rel_status = @PublishedStatus;";

    internal const string FindLatestCompatibleSql = @"
SELECT
    r.rel_code AS ReleaseCode,
    r.rel_product_code AS ProductCode,
    r.rel_version AS Version,
    r.rel_version_major AS VersionMajor,
    r.rel_version_minor AS VersionMinor,
    r.rel_version_build AS VersionPatch,
    r.rel_version_revision AS VersionRevision,
    r.rel_channel AS Channel,
    r.rel_force_update_below_version AS ForceUpdateBelowVersion,
    r.rel_is_mandatory AS IsMandatory,
    r.rel_release_notes AS ReleaseNotes,
    r.rel_published_at AS PublishedAt,
    a.art_code AS ArtifactCode,
    a.art_public_id AS PublicId,
    a.art_os AS OperatingSystem,
    a.art_architecture AS Architecture,
    a.art_package_type AS PackageType,
    a.art_file_name AS FileName,
    a.art_storage_key AS StorageKey,
    a.art_content_type AS ContentType,
    a.art_file_size AS FileSize,
    a.art_sha256 AS Sha256,
    a.art_signature AS Signature
FROM update_products p
INNER JOIN update_releases r
    ON r.rel_product_code = p.prd_code
INNER JOIN update_artifacts a
    ON a.art_release_code = r.rel_code
WHERE p.prd_code = @ProductCode
  AND p.prd_status = @ActiveProductStatus
  AND r.rel_channel = @Channel
  AND r.rel_status = @PublishedStatus
  AND a.art_os = @OperatingSystem
  AND a.art_status = @ActiveArtifactStatus
  AND a.art_package_type = @PackageType
  AND a.art_architecture IN (@RequestedArchitecture, @AnyArchitecture)
ORDER BY
    r.rel_version_major DESC,
    r.rel_version_minor DESC,
    r.rel_version_build DESC,
    r.rel_version_revision DESC,
    CASE
        WHEN a.art_architecture = @RequestedArchitecture THEN 2
        WHEN a.art_architecture = @AnyArchitecture THEN 1
        ELSE 0
    END DESC,
    a.art_code DESC
LIMIT 1;";

    public UpdateReleaseRepository(IDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<UpdateRelease?> GetByCodeAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetByCodeSql,
                    new { ReleaseCode = releaseCode },
                    activeTransaction,
                    cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<UpdateRelease>(command);
            });
    }

    public Task<bool> ExistsVersionAsync(
        string productCode,
        string channel,
        UpdateVersion version,
        long? excludeReleaseCode = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    ExistsVersionSql,
                    new
                    {
                        ProductCode = productCode,
                        Channel = channel,
                        VersionMajor = version.Major,
                        VersionMinor = version.Minor,
                        VersionPatch = version.Patch,
                        VersionRevision = version.Revision,
                        ExcludeReleaseCode = excludeReleaseCode
                    },
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteScalarAsync<bool>(command);
            });
    }

    public Task<long> CreateDraftAsync(
        UpdateRelease release,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);

        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var parameters = CreateWriteParameters(
                    release,
                    ReleaseStatus.Draft);

                var insertCommand = CreateCommand(
                    CreateDraftSql,
                    parameters,
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

    public Task<bool> UpdateDraftAsync(
        UpdateRelease release,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);

        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var parameters = new
                {
                    release.ReleaseCode,
                    release.ProductCode,
                    release.Version,
                    release.VersionMajor,
                    release.VersionMinor,
                    release.VersionPatch,
                    release.VersionRevision,
                    release.Channel,
                    release.ForceUpdateBelowVersion,
                    release.IsMandatory,
                    release.ReleaseNotes,
                    release.InternalMemo,
                    DraftStatus = (int)ReleaseStatus.Draft
                };

                var command = CreateCommand(
                    UpdateDraftSql,
                    parameters,
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteAsync(command) == 1;
            });
    }

    public Task<bool> DeleteDraftAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteStatusCommandAsync(
            DeleteDraftSql,
            new
            {
                ReleaseCode = releaseCode,
                DraftStatus = (int)ReleaseStatus.Draft
            },
            transaction,
            cancellationToken);
    }

    public Task<bool> PublishAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteStatusCommandAsync(
            PublishSql,
            new
            {
                ReleaseCode = releaseCode,
                DraftStatus = (int)ReleaseStatus.Draft,
                PublishedStatus = (int)ReleaseStatus.Published
            },
            transaction,
            cancellationToken);
    }

    public Task<bool> DisableAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteStatusCommandAsync(
            DisableSql,
            new
            {
                ReleaseCode = releaseCode,
                PublishedStatus = (int)ReleaseStatus.Published,
                DisabledStatus = (int)ReleaseStatus.Disabled
            },
            transaction,
            cancellationToken);
    }

    public Task<CompatibleReleaseArtifact?> FindLatestCompatibleAsync(
        string productCode,
        string channel,
        string operatingSystem,
        string requestedArchitecture,
        string packageType,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var parameters = new
                {
                    ProductCode = productCode,
                    Channel = channel,
                    OperatingSystem = operatingSystem,
                    RequestedArchitecture = requestedArchitecture,
                    AnyArchitecture = ArtifactArchitectures.Any,
                    PackageType = packageType,
                    ActiveProductStatus = (int)ProductStatus.Active,
                    PublishedStatus = (int)ReleaseStatus.Published,
                    ActiveArtifactStatus = (int)ArtifactStatus.Active
                };

                var command = CreateCommand(
                    FindLatestCompatibleSql,
                    parameters,
                    activeTransaction,
                    cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<CompatibleReleaseArtifact>(command);
            });
    }

    private Task<bool> ExecuteStatusCommandAsync(
        string sql,
        object parameters,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    sql,
                    parameters,
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteAsync(command) == 1;
            });
    }

    private static object CreateWriteParameters(
        UpdateRelease release,
        ReleaseStatus releaseStatus)
    {
        return new
        {
            release.ProductCode,
            release.Version,
            release.VersionMajor,
            release.VersionMinor,
            release.VersionPatch,
            release.VersionRevision,
            release.Channel,
            release.ForceUpdateBelowVersion,
            release.IsMandatory,
            release.ReleaseNotes,
            release.InternalMemo,
            ReleaseStatus = (int)releaseStatus,
            release.CreatedByUserCode,
            release.CreatedByUserName
        };
    }
}
