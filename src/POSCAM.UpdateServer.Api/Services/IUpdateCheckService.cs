using POSCAM.UpdateServer.Api.Models.Dtos.Updates;

namespace POSCAM.UpdateServer.Api.Services;

public interface IUpdateCheckService
{
    Task<UpdateCheckServiceResult> CheckAsync(
        UpdateCheckRequest? request,
        CancellationToken cancellationToken = default);
}
