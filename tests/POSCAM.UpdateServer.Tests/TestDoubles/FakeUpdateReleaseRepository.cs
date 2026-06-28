using System.Data;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Queries;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeUpdateReleaseRepository : IUpdateReleaseRepository
{
    public CompatibleReleaseArtifact? CompatibleRelease { get; set; }

    public bool HasPublishedRelease { get; set; }

    public string? LastProductCode { get; private set; }

    public string? LastChannel { get; private set; }

    public string? LastOperatingSystem { get; private set; }

    public string? LastArchitecture { get; private set; }

    public string? LastPackageType { get; private set; }

    public Task<UpdateRelease?> GetByCodeAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<UpdateRelease?>(null);
    }

    public Task<bool> ExistsVersionAsync(
        string productCode,
        string channel,
        UpdateVersion version,
        long? excludeReleaseCode = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> HasPublishedReleaseAsync(
        string productCode,
        string channel,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        LastProductCode = productCode;
        LastChannel = channel;
        return Task.FromResult(HasPublishedRelease);
    }

    public Task<long> CreateDraftAsync(
        UpdateRelease release,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<bool> UpdateDraftAsync(
        UpdateRelease release,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<bool> DeleteDraftAsync(
        long releaseCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
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
        LastProductCode = productCode;
        LastChannel = channel;
        LastOperatingSystem = operatingSystem;
        LastArchitecture = requestedArchitecture;
        LastPackageType = packageType;

        return Task.FromResult(CompatibleRelease);
    }
}
