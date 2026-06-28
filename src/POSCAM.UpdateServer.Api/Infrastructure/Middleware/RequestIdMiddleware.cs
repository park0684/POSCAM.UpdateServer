namespace POSCAM.UpdateServer.Api.Infrastructure.Middleware;

/// <summary>
/// 요청별 식별자를 생성하거나 전달받아 로그, 감사 이력, 응답을 연결할 수 있게 한다.
/// </summary>
public sealed class RequestIdMiddleware
{
    public const string HeaderName = "X-Request-ID";

    private const int MaxRequestIdLength = 100;

    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incomingRequestId = context.Request.Headers[HeaderName].FirstOrDefault();

        var requestId =
            !string.IsNullOrWhiteSpace(incomingRequestId)
            && incomingRequestId.Length <= MaxRequestIdLength
                ? incomingRequestId
                : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = requestId;
        context.Response.Headers[HeaderName] = requestId;

        await _next(context);
    }
}
