using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Infrastructure.Middleware;

/// <summary>
/// `/api/v1/admin` 경로의 모든 요청을 AuthServer에서 매번 확인한다.
/// 요청 본문을 읽기 전에 실행되며 공개 Update Check 경로는 통과시킨다.
/// </summary>
public sealed class UpdateManagementAuthorizationMiddleware
{
    public static readonly PathString AdminPathPrefix = new("/api/v1/admin");

    private readonly RequestDelegate _next;

    public UpdateManagementAuthorizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUpdateManagementAuthorizationClient authorizationClient,
        IUpdateManagementActorAccessor actorAccessor)
    {
        if (!context.Request.Path.StartsWithSegments(AdminPathPrefix))
        {
            await _next(context);
            return;
        }

        var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();

        var result = await authorizationClient.AuthorizeAsync(
            authorizationHeader,
            context.TraceIdentifier,
            context.RequestAborted);

        if (result.Authorized && result.Actor is not null)
        {
            actorAccessor.SetActor(result.Actor);
            await _next(context);
            return;
        }

        var httpStatusCode = result.HttpStatusCode is
            StatusCodes.Status401Unauthorized or
            StatusCodes.Status403Forbidden or
            StatusCodes.Status503ServiceUnavailable
                ? result.HttpStatusCode
                : StatusCodes.Status503ServiceUnavailable;

        var errorCode = httpStatusCode == result.HttpStatusCode
            ? result.ErrorCode
            : UpdateErrorCode.ExternalServiceUnavailable;

        var message = httpStatusCode == result.HttpStatusCode
            ? result.Message
            : "관리자 권한 확인 서비스를 사용할 수 없습니다.";

        var requestId = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = httpStatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers[RequestIdMiddleware.HeaderName] = requestId;
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";

        var response = ApiResponse<object?>.Fail(
            errorCode,
            message);

        await context.Response.WriteAsJsonAsync(
            response,
            cancellationToken: context.RequestAborted);
    }
}
