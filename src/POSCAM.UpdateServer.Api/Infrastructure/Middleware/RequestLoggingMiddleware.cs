using System.Diagnostics;

namespace POSCAM.UpdateServer.Api.Infrastructure.Middleware;

/// <summary>
/// 요청 메서드·경로·상태·처리시간·Request ID만 구조화 로그로 남긴다.
/// Authorization, Cookie, X-POSCAM-Service-Key 등 Header 값은 읽거나 기록하지 않는다.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();

        using var scope = _logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["RequestId"] = context.TraceIdentifier,
                ["RemoteIp"] = context.Connection.RemoteIpAddress?.ToString()
            });

        try
        {
            await _next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var logLevel = GetLogLevel(context.Response.StatusCode);

            _logger.Log(
                logLevel,
                "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms. RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                context.Response.StatusCode,
                Math.Round(elapsed.TotalMilliseconds, 2),
                context.TraceIdentifier);
        }
    }

    private static LogLevel GetLogLevel(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }
}
