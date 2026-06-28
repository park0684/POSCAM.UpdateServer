using System.Net.Http.Headers;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public int CallCount { get; private set; }

    public HttpMethod? LastMethod { get; private set; }

    public Uri? LastRequestUri { get; private set; }

    public string? LastAuthorization { get; private set; }

    public string? LastServiceKey { get; private set; }

    public string? LastRequestId { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastMethod = request.Method;
        LastRequestUri = request.RequestUri;
        LastAuthorization = GetHeader(request.Headers, "Authorization");
        LastServiceKey = GetHeader(request.Headers, "X-POSCAM-Service-Key");
        LastRequestId = GetHeader(request.Headers, "X-Request-ID");

        return await _handler(request, cancellationToken);
    }

    private static string? GetHeader(
        HttpRequestHeaders headers,
        string name)
    {
        return headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
