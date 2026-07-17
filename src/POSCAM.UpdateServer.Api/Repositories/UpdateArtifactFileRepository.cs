using System.Data;
using Dapper;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Repositories;

public sealed class UpdateArtifactFileRepository : DapperRepositoryBase, IUpdateArtifactFileRepository
{
    internal const string SelectColumns = @"
    file_code AS FileCode,
    artifact_code AS ArtifactCode,
    file_public_id AS PublicId,
    file_path AS FilePath,
    file_size AS FileSize,
    file_sha256 AS Sha256,
    file_storage_key AS StorageKey,
    file_download_path AS DownloadPath,
    is_required AS IsRequired,
    file_status AS FileStatus,
    created_at AS CreatedAt";

    internal static readonly string GetActiveByArtifactSql = @"
SELECT" + SelectColumns + @"
FROM update_artifact_files
WHERE artifact_code = @ArtifactCode
  AND file_status = @ActiveStatus
ORDER BY file_path ASC;";

    internal const string ExistsByArtifactSql = @"
SELECT EXISTS
(
    SELECT 1
    FROM update_artifact_files
    WHERE artifact_code = @ArtifactCode
);";

    internal const string CreateSql = @"
INSERT INTO update_artifact_files
(
    artifact_code,
    file_public_id,
    file_path,
    file_size,
    file_sha256,
    file_storage_key,
    file_download_path,
    is_required,
    file_status,
    created_at
)
VALUES
(
    @ArtifactCode,
    @PublicId,
    @FilePath,
    @FileSize,
    @Sha256,
    @StorageKey,
    @DownloadPath,
    @IsRequired,
    @FileStatus,
    UTC_TIMESTAMP()
);";

    internal const string DeleteByArtifactSql = @"
DELETE FROM update_artifact_files
WHERE artifact_code = @ArtifactCode;";

    public UpdateArtifactFileRepository(IDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<IReadOnlyList<UpdateArtifactFile>> GetActiveByArtifactAsync(
        long artifactCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<IReadOnlyList<UpdateArtifactFile>>(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetActiveByArtifactSql,
                    new
                    {
                        ArtifactCode = artifactCode,
                        ActiveStatus = (int)ArtifactFileStatus.Active
                    },
                    activeTransaction,
                    cancellationToken);

                var files = await connection.QueryAsync<UpdateArtifactFile>(command);
                return files.AsList();
            });
    }

    public Task<bool> ExistsByArtifactAsync(
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
                    ExistsByArtifactSql,
                    new { ArtifactCode = artifactCode },
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteScalarAsync<bool>(command);
            });
    }

    public Task CreateManyAsync(
        IReadOnlyList<UpdateArtifactFile> files,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                foreach (var file in files)
                {
                    var command = CreateCommand(
                        CreateSql,
                        CreateWriteParameters(file),
                        activeTransaction,
                        cancellationToken);

                    await connection.ExecuteAsync(command);
                }

                return true;
            });
    }

    public Task<int> DeleteByArtifactAsync(
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
                    DeleteByArtifactSql,
                    new { ArtifactCode = artifactCode },
                    activeTransaction,
                    cancellationToken);

                return await connection.ExecuteAsync(command);
            });
    }

    private static object CreateWriteParameters(UpdateArtifactFile file)
    {
        return new
        {
            file.ArtifactCode,
            file.PublicId,
            file.FilePath,
            file.FileSize,
            file.Sha256,
            file.StorageKey,
            file.DownloadPath,
            file.IsRequired,
            FileStatus = (int)file.FileStatus
        };
    }
}
