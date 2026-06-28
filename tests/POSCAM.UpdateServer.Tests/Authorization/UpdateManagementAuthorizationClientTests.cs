using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Authorization;

public class UpdateManagementAuthorizationClientTests
{
    [Fact]
    public async Task AuthorizeAsync_성공시_헤더를_전달하고_Actor를_반환한다()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            JsonResponse(
                HttpStatusCode.OK,
                new
                {
                    success = true,
                    message = "권한 확인 완료",
                    errorCode = 0,
                    data = new
                    {
                        userCode = 10,
                        userName = "관리자",
                        userRole = 1
                    }
                })));

        var client = CreateClient(handler);

        var result = await client.AuthorizeAsync(
            "Bearer account-token",
            "request-123");

        Assert.True(result.Authorized);
        Assert.Equal(StatusCodes.Status200OK, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.None, result.ErrorCode);
        Assert.NotNull(result.Actor);
        Assert.Equal(10, result.Actor.UserCode);
        Assert.Equal("관리자", result.Actor.UserName);
        Assert.Equal(1, result.Actor.UserRole);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(
            "http://poscam-auth-api:8080/api/internal/update-management/authorize",
            handler.LastRequestUri?.ToString());
        Assert.Equal("Bearer account-token", handler.LastAuthorization);
        Assert.Equal("service-key", handler.LastServiceKey);
        Assert.Equal("request-123", handler.LastRequestId);
    }

    [Fact]
    public async Task AuthorizeAsync_Authorization이_없어도_AuthServer를_호출한다()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            JsonResponse(
                HttpStatusCode.Unauthorized,
                FailurePayload(5001))));

        var client = CreateClient(handler);

        var result = await client.AuthorizeAsync(
            authorizationHeader: null,
            requestId: "request-no-token");

        Assert.False(result.Authorized);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.TokenInvalid, result.ErrorCode);
        Assert.Equal(1, handler.CallCount);
        Assert.Null(handler.LastAuthorization);
        Assert.Equal("service-key", handler.LastServiceKey);
    }

    [Fact]
    public async Task AuthorizeAsync_만료토큰은_TokenExpired로_매핑한다()
    {
        var handler = CreateFailureHandler(
            HttpStatusCode.Unauthorized,
            errorCode: 5003);

        var result = await CreateClient(handler).AuthorizeAsync(
            "Bearer expired-token",
            "request-expired");

        Assert.False(result.Authorized);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.TokenExpired, result.ErrorCode);
    }

    [Theory]
    [InlineData(5001)]
    [InlineData(5004)]
    [InlineData(9999)]
    public async Task AuthorizeAsync_기타401은_TokenInvalid로_매핑한다(
        int authServerErrorCode)
    {
        var handler = CreateFailureHandler(
            HttpStatusCode.Unauthorized,
            authServerErrorCode);

        var result = await CreateClient(handler).AuthorizeAsync(
            "Bearer invalid-token",
            "request-invalid");

        Assert.False(result.Authorized);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.TokenInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task AuthorizeAsync_403은_PermissionDenied로_매핑한다()
    {
        var handler = CreateFailureHandler(
            HttpStatusCode.Forbidden,
            errorCode: 7001);

        var result = await CreateClient(handler).AuthorizeAsync(
            "Bearer no-permission",
            "request-forbidden");

        Assert.False(result.Authorized);
        Assert.Equal(StatusCodes.Status403Forbidden, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.PermissionDenied, result.ErrorCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AuthorizeAsync_AuthServer_오류는_503으로_매핑한다(
        HttpStatusCode statusCode)
    {
        var handler = CreateFailureHandler(
            statusCode,
            errorCode: 9002);

        var result = await CreateClient(handler).AuthorizeAsync(
            "Bearer token",
            "request-server-error");

        Assert.False(result.Authorized);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.ExternalServiceUnavailable, result.ErrorCode);
    }

    [Fact]
    public async Task AuthorizeAsync_비정상JSON은_503으로_매핑한다()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "not-json",
                    Encoding.UTF8,
                    "application/json")
            }));

        var result = await CreateClient(handler).AuthorizeAsync(
            "Bearer token",
            "request-bad-json");

        Assert.False(result.Authorized);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.ExternalServiceUnavailable, result.ErrorCode);
    }

    [Theory]
    [InlineData(0, "관리자", 1)]
    [InlineData(10, "", 1)]
    [InlineData(10, "관리자", 2)]
    public async Task AuthorizeAsync_잘못된_Actor응답은_503으로_매핑한다(
        int userCode,
        string userName,
        int userRole)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            JsonResponse(
                HttpStatusCode.OK,
                new
                {
                    success = true,
                    message = "권한 확인 완료",
                    errorCode = 0,
                    data = new
                    {
                        userCode,
                        userName,
                        userRole
                    }
                })));

        var result = await CreateClient(handler).AuthorizeAsync(
            "Bearer token",
            "request-invalid-actor");

        Assert.False(result.Authorized);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.ExternalServiceUnavailable, result.ErrorCode);
    }

    [Fact]
    public async Task AuthorizeAsync_서비스키가_없으면_호출하지_않고_503이다()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new InvalidOperationException());
        var client = CreateClient(
            handler,
            serviceKey: string.Empty);

        var result = await client.AuthorizeAsync(
            "Bearer token",
            "request-no-key");

        Assert.False(result.Authorized);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.ExternalServiceUnavailable, result.ErrorCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task AuthorizeAsync_Timeout은_503으로_매핑한다()
    {
        var handler = new StubHttpMessageHandler(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException();
            });

        var client = CreateClient(
            handler,
            timeoutSeconds: 1);

        var result = await client.AuthorizeAsync(
            "Bearer token",
            "request-timeout");

        Assert.False(result.Authorized);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.ExternalServiceUnavailable, result.ErrorCode);
    }

    [Fact]
    public async Task AuthorizeAsync_호출자취소는_상위로_전파한다()
    {
        var handler = new StubHttpMessageHandler(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException();
            });

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient(handler).AuthorizeAsync(
                "Bearer token",
                "request-cancelled",
                cancellation.Token));
    }

    private static StubHttpMessageHandler CreateFailureHandler(
        HttpStatusCode statusCode,
        int errorCode)
    {
        return new StubHttpMessageHandler((_, _) => Task.FromResult(
            JsonResponse(
                statusCode,
                FailurePayload(errorCode))));
    }

    private static object FailurePayload(int errorCode)
    {
        return new
        {
            success = false,
            message = "AuthServer 상세 메시지",
            errorCode,
            data = (object?)null
        };
    }

    private static UpdateManagementAuthorizationClient CreateClient(
        StubHttpMessageHandler handler,
        string serviceKey = "service-key",
        int timeoutSeconds = 5)
    {
        var httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        var options = Options.Create(
            new AuthServerOptions
            {
                BaseUrl = "http://poscam-auth-api:8080",
                InternalServiceKey = serviceKey,
                TimeoutSeconds = timeoutSeconds
            });

        return new UpdateManagementAuthorizationClient(
            httpClient,
            options,
            NullLogger<UpdateManagementAuthorizationClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        object payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
    }
}
