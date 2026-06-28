using System.Data;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Queries;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeReleaseRepositoryForManagement : IUpdateReleaseRepository
{
    public UpdateRelease? ReleaseByCode { get; set; }
    public bool DuplicateExists { get; set; }
    public bool UpdateResult { get; set; } = true;
    public bool DeleteResult { get; set; } = true;
    public long CreatedReleaseCode { get; set; } = 100;
    public UpdateRelease? LastCreatedRelease { get; private set; }
    public UpdateRelease? LastUpdatedRelease { get; private set; }
    public long? LastDeletedReleaseCode { get; private set; }

    public Task<UpdateRelease?> GetByCodeAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ReleaseByCode);
    }

    public Task<bool> ExistsVersionAsync(
        string productCode,
        string channel,
        UpdateVersion version,
        long? excludeReleaseCode = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DuplicateExists);
    }

    public Task<bool> HasPublishedReleaseAsync(
        string productCode,
        string channel,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<long> CreateDraftAsync(
        UpdateRelease release,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        LastCreatedRelease = release;
        release.ReleaseCode = CreatedReleaseCode;
        ReleaseByCode = Clone(release);
        return Task.FromResult(CreatedReleaseCode);
    }

    public Task<bool> UpdateDraftAsync(
        UpdateRelease release,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        LastUpdatedRelease = release;
        if (UpdateResult)
        {
            ReleaseByCode = Clone(release);
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<bool> DeleteDraftAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        LastDeletedReleaseCode = releaseCode;
        return Task.FromResult(DeleteResult);
    }

    public Task<bool> PublishAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<bool> DisableAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
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
        return Task.FromResult<CompatibleReleaseArtifact?>(null);
    }

    private static UpdateRelease Clone(UpdateRelease release)
    {
        return new UpdateRelease
        {
            ReleaseCode = release.ReleaseCode,
            ProductCode = release.ProductCode,
            Version = release.Version,
            VersionMajor = release.VersionMajor,
            VersionMinor = release.VersionMinor,
            VersionPatch = release.VersionPatch,
            VersionRevision = release.VersionRevision,
            Channel = release.Channel,
            ForceUpdateBelowVersion = release.ForceUpdateBelowVersion,
            IsMandatory = release.IsMandatory,
            ReleaseNotes = release.ReleaseNotes,
            InternalMemo = release.InternalMemo,
            ReleaseStatus = release.ReleaseStatus,
            PublishedAt = release.PublishedAt,
            CreatedByUserCode = release.CreatedByUserCode,
            CreatedByUserName = release.CreatedByUserName,
            CreatedAt = release.CreatedAt == default ? DateTime.UtcNow : release.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
