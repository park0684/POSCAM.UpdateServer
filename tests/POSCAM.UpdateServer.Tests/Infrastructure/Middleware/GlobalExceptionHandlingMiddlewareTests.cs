using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Infrastructure.Middleware;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Tests.Infrastructure.Middleware;

public class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_일반예외를_UnknownError로_변환한다()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(
            new InvalidOperationException("외부에 노출되면 안 되는 상세 오류"));

        await middleware.InvokeAsync(context);

        var response = await ReadResponseAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.False(response.Success);
        Assert.Equal((int)UpdateErrorCode.UnknownError, response.ErrorCode);
        Assert.DoesNotContain("외부에 노출되면 안 되는 상세 오류", response.Message);
        Assert.Equal(
            "request-test",
            context.Response.Headers[RequestIdMiddleware.HeaderName].ToString());
        Assert.Equal("no-store", context.Response.Headers["Cache-Control"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_DB예외를_DatabaseError로_변환한다()
    {
        var context = CreateContext();
        var databaseException = new UpdateDatabaseException(
            DatabaseFailureType.ConnectionFailed,
            providerErrorNumber: 1042,
            "내부 DB 오류",
            new Exception("내부 상세"));

        var middleware = CreateMiddleware(databaseException);

        await middleware.InvokeAsync(context);

        var response = await ReadResponseAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.False(response.Success);
        Assert.Equal((int)UpdateErrorCode.DatabaseError, response.ErrorCode);
        Assert.DoesNotContain("내부 DB 오류", response.Message);
        Assert.DoesNotContain("1042", response.Message);
    }

    [Fact]
    public async Task InvokeAsync_요청본문한도초과를_413_FileTooLarge로_유지한다()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(
            new BadHttpRequestException(
                "Request body too large.",
                StatusCodes.Status413PayloadTooLarge));

        await middleware.InvokeAsync(context);

        var response = await ReadResponseAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.False(response.Success);
        Assert.Equal((int)UpdateErrorCode.FileTooLarge, response.ErrorCode);
        Assert.DoesNotContain("Request body too large", response.Message);
    }

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            TraceIdentifier = "request-test",
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static GlobalExceptionHandlingMiddleware CreateMiddleware(Exception exception)
    {
        return new GlobalExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance);
    }

    private static async Task<ApiResponse<object?>> ReadResponseAsync(HttpContext context)
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
