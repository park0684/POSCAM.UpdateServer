using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Artifacts;

namespace POSCAM.UpdateServer.Api.Services;

public interface IArtifactUploadService
{
    Task<AdminServiceResult<ArtifactUploadResponse>> UploadAsync(
        long releaseCode,
        ArtifactUploadRequest? request,
        CancellationToken cancellationToken = default);
}
