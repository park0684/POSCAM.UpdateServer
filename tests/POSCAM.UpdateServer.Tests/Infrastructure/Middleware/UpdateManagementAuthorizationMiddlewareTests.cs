using System.Text.Json;
using Microsoft.AspNetCore.Http;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Infrastructure.Middleware;
using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Infrastructure.Middleware;

public class UpdateManagementAuthorizationMiddlewareTests
{
    [Theory]
    [InlineData("/api/v1/updates/check")]
    [InlineData("/health/live")]
    [InlineData("/api/v1/administrator")]
    public async Task InvokeAsync_관리자경로가_아니면_AuthServer를_호출하지_않는다(
        string path)
    {
        var nextCalled = false;
        var middleware = new UpdateManagementAuthorizationMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext(path);
        var client = new FakeUpdateManagementAuthorizationClient();
        var actorAccessor = new UpdateManagementActorAccessor();

        await middleware.InvokeAsync(
            context,
            client,
            actorAccessor);

        Assert.True(nextCalled);
        Assert.Equal(0, client.CallCount);
        Assert.Null(actorAccessor.Actor);
    }

    [Fact]
    public async Task InvokeAsync_관리자권한성공시_Actor를_저장하고_다음처리를_호출한다()
    {
        var actor = new UpdateManagementActor
        {
            UserCode = 10,
            UserName = "관리자",
            UserRole = 1
        };

        var client = new FakeUpdateManagementAuthorizationClient
        {
            Result = UpdateManagementAuthorizationResult.Allow(actor)
        };

        var actorAccessor = new UpdateManagementActorAccessor();
        var nextCalled = false;
        var middleware = new UpdateManagementAuthorizationMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("/api/v1/admin/releases");
        context.TraceIdentifier = "request-admin";
        context.Request.Headers.Authorization = "Bearer account-token";

        await middleware.InvokeAsync(
            context,
            client,
            actorAccessor);

        Assert.True(nextCalled);
        Assert.Equal(1, client.CallCount);
        Assert.Equal("Bearer account-token", client.LastAuthorizationHeader);
        Assert.Equal("request-admin", client.LastRequestId);
        Assert.Same(actor, actorAccessor.Actor);
    }

    [Fact]
    public async Task InvokeAsync_Authorization이_없어도_관리자경로는_AuthServer를_호출한다()
    {
        var client = new FakeUpdateManagementAuthorizationClient
        {
            Result = UpdateManagementAuthorizationResult.Deny(
                StatusCodes.Status401Unauthorized,
                UpdateErrorCode.TokenInvalid,
                "로그인 토큰이 유효하지 않습니다.")
        };

        var middleware = new UpdateManagementAuthorizationMiddleware(
            _ => throw new InvalidOperationException("호출되면 안 됩니다."));

        var context = CreateContext("/api/v1/admin/products/active");

        await middleware.InvokeAsync(
            context,
            client,
            new UpdateManagementActorAccessor());

        Assert.Equal(1, client.CallCount);
        Assert.Null(client.LastAuthorizationHeader);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(401, UpdateErrorCode.TokenExpired)]
    [InlineData(401, UpdateErrorCode.TokenInvalid)]
    [InlineData(403, UpdateErrorCode.PermissionDenied)]
    [InlineData(503, UpdateErrorCode.ExternalServiceUnavailable)]
    public async Task InvokeAsync_권한실패를_공통응답으로_반환한다(
        int statusCode,
        UpdateErrorCode errorCode)
    {
        var client = new FakeUpdateManagementAuthorizationClient
        {
            Result = UpdateManagementAuthorizationResult.Deny(
                statusCode,
                errorCode,
                "권한 확인 실패")
        };

        var nextCalled = false;
        var middleware = new UpdateManagementAuthorizationMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        var context = CreateContext("/api/v1/admin/releases/1");

        await middleware.InvokeAsync(
            context,
            client,
            new UpdateManagementActorAccessor());

        var response = await ReadResponseAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(statusCode, context.Response.StatusCode);
        Assert.False(response.Success);
        Assert.Equal((int)errorCode, response.ErrorCode);
        Assert.Null(response.Data);
        Assert.Equal("no-store", context.Response.Headers["Cache-Control"].ToString());
        Assert.Equal("no-cache", context.Response.Headers["Pragma"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_모순된_성공결과는_503으로_차단한다()
    {
        var client = new FakeUpdateManagementAuthorizationClient
        {
            Result = new UpdateManagementAuthorizationResult
            {
                Authorized = true,
                HttpStatusCode = StatusCodes.Status200OK,
                ErrorCode = UpdateErrorCode.None,
                Message = "잘못된 성공 결과",
                Actor = null
            }
        };

        var middleware = new UpdateManagementAuthorizationMiddleware(
            _ => throw new InvalidOperationException("호출되면 안 됩니다."));

        var context = CreateContext("/api/v1/admin/releases");

        await middleware.InvokeAsync(
            context,
            client,
            new UpdateManagementActorAccessor());

        var response = await ReadResponseAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal((int)UpdateErrorCode.ExternalServiceUnavailable, response.ErrorCode);
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ApiResponse<object?>> ReadResponseAsync(
        HttpContext context)
    {
        context.Response.Body.Position = 0;

        var response = await JsonSerializer.DeserializeAsync<ApiResponse<object?>>(
            context.Response.Body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        Assert.NotNull(response);
        return response!;
    }
}
