using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using POSCAM.UpdateServer.Api.Options;

namespace POSCAM.UpdateServer.Api.Infrastructure.Operations;

/// <summary>
/// 운영 설정을 검증하고 ASP.NET Core 옵션으로 변환한다.
/// Secret 값 자체는 오류 메시지나 로그에 포함하지 않는다.
/// </summary>
public static class OperationalConfiguration
{
    public static bool IsValidCorsOptions(AdminWebCorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.AdminWebOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .All(IsValidOrigin);
    }

    public static string[] GetNormalizedOrigins(AdminWebCorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.AdminWebOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsValidTrustedProxyOptions(TrustedProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.ForwardLimit is < 1 or > 3)
        {
            return false;
        }

        return options.KnownProxies
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .All(value => TryParseSafeProxy(value, out _));
    }

    public static void ApplyForwardedHeaders(
        ForwardedHeadersOptions target,
        TrustedProxyOptions source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        target.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedProto;
        target.ForwardLimit = source.ForwardLimit;
        target.RequireHeaderSymmetry = true;

        foreach (var proxy in source.KnownProxies
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Select(value => value.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!TryParseSafeProxy(proxy, out var address))
            {
                throw new InvalidOperationException(
                    "ForwardedHeaders:KnownProxies 설정에 유효하지 않은 IP가 있습니다.");
            }

            target.KnownProxies.Add(address);
        }
    }

    public static bool IsValidAuthServerOptions(
        AuthServerOptions options,
        bool requireServiceKey)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp
                && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || options.TimeoutSeconds is < 1 or > 30)
        {
            return false;
        }

        return !requireServiceKey
               || (!string.IsNullOrWhiteSpace(options.InternalServiceKey)
                   && options.InternalServiceKey.Length >= 32);
    }

    private static bool IsValidOrigin(string value)
    {
        var normalized = value.Trim().TrimEnd('/');

        return !normalized.Contains('*')
               && Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp
                   || uri.Scheme == Uri.UriSchemeHttps)
               && string.IsNullOrEmpty(uri.Query)
               && string.IsNullOrEmpty(uri.Fragment)
               && (string.IsNullOrEmpty(uri.AbsolutePath)
                   || uri.AbsolutePath == "/");
    }

    private static bool TryParseSafeProxy(
        string value,
        out IPAddress address)
    {
        address = IPAddress.None;

        if (!IPAddress.TryParse(value.Trim(), out var parsed)
            || parsed.Equals(IPAddress.Any)
            || parsed.Equals(IPAddress.IPv6Any)
            || parsed.Equals(IPAddress.Broadcast))
        {
            return false;
        }

        address = parsed;
        return true;
    }
}
