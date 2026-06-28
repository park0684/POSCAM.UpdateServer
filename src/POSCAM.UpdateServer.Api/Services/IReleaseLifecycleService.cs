using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Lifecycle;

namespace POSCAM.UpdateServer.Api.Services;

public interface IReleaseLifecycleService
{
    Task<AdminServiceResult<ReleaseLifecycleResponse>> PublishAsync(
        long releaseCode,
        CancellationToken cancellationToken = default);

    Task<AdminServiceResult<ReleaseLifecycleResponse>> DisableAsync(
        long releaseCode,
        CancellationToken cancellationToken = default);

    Task<AdminServiceResult<QuarantineArtifactResponse>> QuarantineArtifactAsync(
        long artifactCode,
        CancellationToken cancellationToken = default);
}
