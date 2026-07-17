using System.Data;
using Dapper;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Repositories;

public sealed class UpdateArtifactRepository : DapperRepositoryBase, IUpdateArtifactRepository
{
    internal const string SelectColumns = @"
    art_code AS ArtifactCode,
    art_release_code AS ReleaseCode,
    art_public_id AS PublicId,
    art_os AS OperatingSystem,
    art_architecture AS Architecture,
    art_package_type AS PackageType,
    art_file_name AS FileName,
    art_storage_key AS StorageKey,
    art_content_type AS ContentType,
    art_file_size AS FileSize,
    art_sha256 AS Sha256,
    art_signature AS Signature,
    art_status AS ArtifactStatus,
    art_idate AS CreatedAt,
    art_udate AS UpdatedAt";

    internal static readonly string GetByCodeSql = @"
SELECT" + SelectColumns + @"
FROM update_artifacts
WHERE art_code = @ArtifactCode
LIMIT 1;";

    internal static readonly string GetActiveByReleaseSql = @"
SELECT" + SelectColumns + @"
FROM update_artifacts
WHERE art_release_code = @ReleaseCode
  AND art_status = @ActiveStatus
ORDER BY art_os ASC, art_architecture ASC, art_package_type ASC, art_code ASC;";

    internal static readonly string GetByTargetSql = @"
SELECT" + SelectColumns + @"
FROM update_artifacts
WHERE art_release_code = @ReleaseCode
  AND art_os = @OperatingSystem
  AND art_architecture = @Architecture
  AND art_package_type = @PackageType
LIMIT 1;";

    internal const string CreateSql = @"
INSERT INTO update_artifacts
(
    art_release_code,
    art_public_id,
    art_os,
    art_architecture,
    art_package_type,
    art_file_name,
    art_storage_key,
    art_content_type,
    art_file_size,
    art_sha256,
    art_signature,
    art_status,
    art_idate,
    art_udate
)
VALUES
(
    @ReleaseCode,
    @PublicId,
    @OperatingSystem,
    @Architecture,
    @PackageType,
    @FileName,
    @StorageKey,
    @ContentType,
    @FileSize,
    @Sha256,
    @Signature,
    @ArtifactStatus,
    UTC_TIMESTAMP(),
    NULL
);";

    internal const string LastInsertIdSql = "SELECT LAST_INSERT_ID();";

    internal const string ReplaceSql = @"
UPDATE update_artifacts
SET art_public_id = @PublicId,
    art_os = @OperatingSystem,
    art_architecture = @Architecture,
    art_package_type = @PackageType,
    art_file_name = @FileName,
    art_storage_key = @StorageKey,
    art_content_type = @ContentType,
    art_file_size = @FileSize,
    art_sha256 = @Sha256,
    art_signature = @Signature,
    art_status = @ArtifactStatus,
    art_udate = UTC_TIMESTAMP()
WHERE art_code = @ArtifactCode
  AND art_release_code = @ReleaseCode;";

    internal const string SetStatusSql = @"
UPDATE update_artifacts
SET art_status = @ArtifactStatus,
    art_udate = UTC_TIMESTAMP()
WHERE art_code = @ArtifactCode;";

    public UpdateArtifactRepository(IDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<UpdateArtifact?> GetByCodeAsync(
        long artifactCode,
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
                    new { ArtifactCode = artifactCode },
                    activeTransaction,
                    cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<UpdateArtifact>(command);
            });
    }

    public Task<IReadOnlyList<UpdateArtifact>> GetActiveByReleaseAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<IReadOnlyList<UpdateArtifact>>(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetActiveByReleaseSql,
                    new
                    {
                        ReleaseCode = releaseCode,
                        ActiveStatus = (int)ArtifactStatus.Active
                    },
                    activeTransaction,
                    cancellationToken);

                var artifacts = await connection.QueryAsync<UpdateArtifact>(command);
                return artifacts.AsList();
            });
    }

    public Task<UpdateArtifact?> GetByTargetAsync(
        long releaseCode,
        string operatingSystem,
        string architecture,
        string packageType,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetByTargetSql,
                    new
                    {
                        ReleaseCode = releaseCode,
                        OperatingSystem = operatingSystem,
                        Architecture = architecture,
                        PackageType = packageType
                    },
                    activeTransaction,
                    cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<UpdateArtifact>(command);
            });
    }

    public Task<long> CreateAsync(
        UpdateArtifact artifact,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var parameters = CreateWriteParameters(artifact);

                var insertCommand = CreateCommand(
                    CreateSql,
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

    public Task<bool> ReplaceAsync(
        UpdateArtifact artifact,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var parameters = new
                {
                    artifact.ArtifactCode,
                    artifact.ReleaseCode,
                    artifact.PublicId,
                    artifact.OperatingSystem,
                    artifact.Architecture,
                    artifact.PackageType,
                    artifact.FileName,
                    artifact.StorageKey,
                    artifact.ContentType,
                    artifact.FileSize,
                    artifact.Sha256,
                    artifact.Signature,
                    ArtifactStatus = (int)artifact.ArtifactStatus
                };

                var command = CreateCommand(
                    ReplaceSql,
                    parameters,
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteAsync(command) == 1;
            });
    }

    public Task<bool> SetStatusAsync(
        long artifactCode,
        ArtifactStatus status,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    SetStatusSql,
                    new
                    {
                        ArtifactCode = artifactCode,
                        ArtifactStatus = (int)status
                    },
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteAsync(command) == 1;
            });
    }

    private static object CreateWriteParameters(UpdateArtifact artifact)
    {
        return new
        {
            artifact.ReleaseCode,
            artifact.PublicId,
            artifact.OperatingSystem,
            artifact.Architecture,
            artifact.PackageType,
            artifact.FileName,
            artifact.StorageKey,
            artifact.ContentType,
            artifact.FileSize,
            artifact.Sha256,
            artifact.Signature,
            ArtifactStatus = (int)artifact.ArtifactStatus
        };
    }
}
