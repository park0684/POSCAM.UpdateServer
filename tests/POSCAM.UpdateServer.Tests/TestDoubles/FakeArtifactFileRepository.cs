using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeArtifactFileRepository : IUpdateArtifactFileRepository
{
    public IReadOnlyList<UpdateArtifactFile> Files { get; set; } = Array.Empty<UpdateArtifactFile>();
    public List<UpdateArtifactFile> CreatedFiles { get; } = new();
    public List<long> DeletedArtifactCodes { get; } = new();
    public Exception? CreateException { get; set; }

    public Task<IReadOnlyList<UpdateArtifactFile>> GetActiveByArtifactAsync(
        long artifactCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UpdateArtifactFile> result = Files
            .Where(file =>
                file.ArtifactCode == artifactCode
                && file.FileStatus == ArtifactFileStatus.Active)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<bool> ExistsByArtifactAsync(
        long artifactCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Files.Any(file => file.ArtifactCode == artifactCode));
    }

    public Task CreateManyAsync(
        IReadOnlyList<UpdateArtifactFile> files,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (CreateException is not null)
        {
            throw CreateException;
        }

        CreatedFiles.AddRange(files);
        Files = Files.Concat(files).ToArray();

        return Task.CompletedTask;
    }

    public Task<int> DeleteByArtifactAsync(
        long artifactCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        DeletedArtifactCodes.Add(artifactCode);
        var beforeCount = Files.Count;
        Files = Files
            .Where(file => file.ArtifactCode != artifactCode)
            .ToArray();

        return Task.FromResult(beforeCount - Files.Count);
    }
}
