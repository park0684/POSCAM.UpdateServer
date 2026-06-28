using System.Data;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Queries;

namespace POSCAM.UpdateServer.Api.Repositories;

public interface IUpdateReleaseRepository
{
    Task<UpdateRelease?> GetByCodeAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<UpdateRelease?> GetByCodeForUpdateAsync(
        long releaseCode,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UpdateRelease>> GetPagedAsync(
        ReleaseSearchCriteria criteria,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        ReleaseSearchCriteria criteria,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsVersionAsync(
        string productCode,
        string channel,
        UpdateVersion version,
        long? excludeReleaseCode = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> HasPublishedReleaseAsync(
        string productCode,
        string channel,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<long> CreateDraftAsync(
        UpdateRelease release,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateDraftAsync(
        UpdateRelease release,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDraftAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> PublishAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<CompatibleReleaseArtifact?> FindLatestCompatibleAsync(
        string productCode,
        string channel,
        string operatingSystem,
        string requestedArchitecture,
        string packageType,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
