namespace POSCAM.UpdateServer.Api.Infrastructure.Middleware;

/// <summary>
/// 요청별 식별자를 생성하거나 안전한 전달값을 사용해 로그, 감사 이력, 응답을 연결한다.
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

        var requestId = IsSafeRequestId(incomingRequestId)
            ? incomingRequestId!
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = requestId;
        context.Response.Headers[HeaderName] = requestId;

        await _next(context);
    }

    internal static bool IsSafeRequestId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaxRequestIdLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            var allowed = char.IsAsciiLetterOrDigit(character)
                          || character is '-' or '_' or '.' or ':';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
