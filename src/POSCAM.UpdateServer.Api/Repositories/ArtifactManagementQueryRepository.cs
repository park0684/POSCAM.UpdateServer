using System.Data;
using Dapper;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Entities;

namespace POSCAM.UpdateServer.Api.Repositories;

public sealed class ArtifactManagementQueryRepository
    : DapperRepositoryBase, IArtifactManagementQueryRepository
{
    private const string SelectColumns = @"
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

    internal static readonly string GetByTargetForUpdateSql = $@"
SELECT
{SelectColumns}
FROM update_artifacts
WHERE art_release_code = @ReleaseCode
  AND art_os = @OperatingSystem
  AND art_architecture = @Architecture
  AND art_package_type = @PackageType
LIMIT 1
FOR UPDATE;";

    internal static readonly string GetByCodeForUpdateSql = $@"
SELECT
{SelectColumns}
FROM update_artifacts
WHERE art_code = @ArtifactCode
LIMIT 1
FOR UPDATE;";

    public ArtifactManagementQueryRepository(IDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<UpdateArtifact?> GetByTargetForUpdateAsync(
        long releaseCode,
        string operatingSystem,
        string architecture,
        string packageType,
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
                    GetByTargetForUpdateSql,
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

    public Task<UpdateArtifact?> GetByCodeForUpdateAsync(
        long artifactCode,
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
                    new { ArtifactCode = artifactCode },
                    activeTransaction,
                    cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<UpdateArtifact>(command);
            });
    }
}
