using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Models.Common;

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

        context.Response.Clear();
        context.Response.StatusCode = result.HttpStatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        var response = ApiResponse<object?>.Fail(
            result.ErrorCode,
            result.Message);

        await context.Response.WriteAsJsonAsync(
            response,
            cancellationToken: context.RequestAborted);
    }
}
