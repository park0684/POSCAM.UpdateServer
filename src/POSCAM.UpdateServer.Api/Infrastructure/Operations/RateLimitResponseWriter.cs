using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Infrastructure.Operations;

public static class RateLimitResponseWriter
{
    public static async ValueTask WriteRejectedAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;

        if (response.HasStarted)
        {
            return;
        }

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/json; charset=utf-8";
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["Pragma"] = "no-cache";
        response.Headers["Expires"] = "0";

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var retryAfter))
        {
            response.Headers["Retry-After"] = Math.Max(
                    1,
                    (int)Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await response.WriteAsJsonAsync(
            ApiResponse<object?>.Fail(
                UpdateErrorCode.RateLimitExceeded,
                "업데이트 확인 요청이 너무 많습니다. 잠시 후 다시 시도해 주세요."),
            cancellationToken);
    }
}
