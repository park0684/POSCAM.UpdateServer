using POSCAM.UpdateServer.Api.Models.Dtos.Updates;
using POSCAM.UpdateServer.Api.Services;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeUpdateCheckService : IUpdateCheckService
{
    public UpdateCheckServiceResult Result { get; set; } = null!;

    public UpdateCheckRequest? LastRequest { get; private set; }

    public Task<UpdateCheckServiceResult> CheckAsync(
        UpdateCheckRequest? request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(Result);
    }
}
