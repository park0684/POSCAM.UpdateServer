using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Infrastructure.Middleware;

/// <summary>
/// 처리되지 않은 예외를 공통 API 응답으로 변환한다.
/// 외부 응답에는 Stack Trace, SQL, 물리 경로, Secret을 노출하지 않는다.
/// </summary>
public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "요청이 클라이언트에 의해 취소되었습니다. RequestId: {RequestId}, Method: {Method}, Path: {Path}",
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "처리되지 않은 서버 오류가 발생했습니다. RequestId: {RequestId}, Method: {Method}, Path: {Path}",
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = ApiResponse<object?>.Fail(
                UpdateErrorCode.UnknownError,
                "요청을 처리하는 중 서버 오류가 발생했습니다.");

            await context.Response.WriteAsJsonAsync(
                response,
                cancellationToken: context.RequestAborted);
        }
    }
}
