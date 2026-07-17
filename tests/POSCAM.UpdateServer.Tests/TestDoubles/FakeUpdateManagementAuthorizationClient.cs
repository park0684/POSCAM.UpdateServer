using POSCAM.UpdateServer.Api.Authorization;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeUpdateManagementAuthorizationClient
    : IUpdateManagementAuthorizationClient
{
    public UpdateManagementAuthorizationResult Result { get; set; } = null!;

    public int CallCount { get; private set; }

    public string? LastAuthorizationHeader { get; private set; }

    public string? LastRequestId { get; private set; }

    public Task<UpdateManagementAuthorizationResult> AuthorizeAsync(
        string? authorizationHeader,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastAuthorizationHeader = authorizationHeader;
        LastRequestId = requestId;
        return Task.FromResult(Result);
    }
}
