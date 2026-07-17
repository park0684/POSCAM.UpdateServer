using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using POSCAM.UpdateServer.Api.Infrastructure.Middleware;

namespace POSCAM.UpdateServer.Tests.Infrastructure.Middleware;

public class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_요청정보와_RequestId만_기록하고_민감Header는_제외한다()
    {
        var logger = new CaptureLogger<RequestLoggingMiddleware>();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "request-log-01"
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/admin/releases";
        context.Request.Headers.Authorization = "Bearer SECRET_ACCOUNT_TOKEN";
        context.Request.Headers["X-POSCAM-Service-Key"] = "SECRET_SERVICE_KEY";
        context.Request.Headers.Cookie = "session=SECRET_COOKIE";

        var middleware = new RequestLoggingMiddleware(
            next =>
            {
                next.Response.StatusCode = StatusCodes.Status201Created;
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(context);

        var message = Assert.Single(logger.Messages);
        Assert.Contains("POST", message, StringComparison.Ordinal);
        Assert.Contains("/api/v1/admin/releases", message, StringComparison.Ordinal);
        Assert.Contains("201", message, StringComparison.Ordinal);
        Assert.Contains("request-log-01", message, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_ACCOUNT_TOKEN", message, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_SERVICE_KEY", message, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_COOKIE", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cookie", message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
