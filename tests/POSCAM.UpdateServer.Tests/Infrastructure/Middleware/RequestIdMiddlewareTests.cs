using Microsoft.AspNetCore.Http;
using POSCAM.UpdateServer.Api.Infrastructure.Middleware;

namespace POSCAM.UpdateServer.Tests.Infrastructure.Middleware;

public class RequestIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_요청식별자가_없으면_새로_생성한다()
    {
        var context = new DefaultHttpContext();
        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.False(string.IsNullOrWhiteSpace(context.TraceIdentifier));
        Assert.Equal(
            context.TraceIdentifier,
            context.Response.Headers[RequestIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_유효한_요청식별자를_그대로_사용한다()
    {
        const string requestId = "request-123";

        var context = new DefaultHttpContext();
        context.Request.Headers[RequestIdMiddleware.HeaderName] = requestId;

        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(requestId, context.TraceIdentifier);
        Assert.Equal(
            requestId,
            context.Response.Headers[RequestIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_너무_긴_요청식별자는_교체한다()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[RequestIdMiddleware.HeaderName] = new string('A', 101);

        var middleware = new RequestIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.NotEqual(new string('A', 101), context.TraceIdentifier);
        Assert.Equal(32, context.TraceIdentifier.Length);
    }
}
