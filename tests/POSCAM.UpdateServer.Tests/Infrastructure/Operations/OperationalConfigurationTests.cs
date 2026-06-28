using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using POSCAM.UpdateServer.Api.Infrastructure.Operations;
using POSCAM.UpdateServer.Api.Options;

namespace POSCAM.UpdateServer.Tests.Infrastructure.Operations;

public class OperationalConfigurationTests
{
    [Fact]
    public void CorsOptions_경로없는_설정Origin만_허용한다()
    {
        var options = new AdminWebCorsOptions
        {
            AdminWebOrigins = new List<string>
            {
                "https://admin.example.com/",
                " https://admin.example.com ",
                "http://localhost:5001"
            }
        };

        Assert.True(OperationalConfiguration.IsValidCorsOptions(options));

        var origins = OperationalConfiguration.GetNormalizedOrigins(options);
        Assert.Equal(2, origins.Length);
        Assert.Contains("https://admin.example.com", origins);
        Assert.Contains("http://localhost:5001", origins);
    }

    [Fact]
    public void AreHttpsOrigins_운영Origin이_모두HTTPS인지_확인한다()
    {
        var httpsOnly = new AdminWebCorsOptions
        {
            AdminWebOrigins = new List<string>
            {
                "https://admin.example.com",
                "https://support.example.com"
            }
        };
        var includesHttp = new AdminWebCorsOptions
        {
            AdminWebOrigins = new List<string>
            {
                "https://admin.example.com",
                "http://localhost:5001"
            }
        };

        Assert.True(OperationalConfiguration.AreHttpsOrigins(httpsOnly));
        Assert.False(OperationalConfiguration.AreHttpsOrigins(includesHttp));
        Assert.False(
            OperationalConfiguration.AreHttpsOrigins(new AdminWebCorsOptions()));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    [InlineData("https://admin.example.com/path")]
    [InlineData("https://admin.example.com?query=1")]
    [InlineData("https://user@admin.example.com")]
    [InlineData("ftp://admin.example.com")]
    public void CorsOptions_와일드카드_경로_사용자정보_비HTTPOrigin을_거부한다(
        string origin)
    {
        var options = new AdminWebCorsOptions
        {
            AdminWebOrigins = new List<string> { origin }
        };

        Assert.False(OperationalConfiguration.IsValidCorsOptions(options));
    }

    [Fact]
    public void TrustedProxyOptions_명시된IP와_1단계만_적용한다()
    {
        var source = new TrustedProxyOptions
        {
            ForwardLimit = 1,
            KnownProxies = new List<string> { "172.18.0.1" }
        };
        var target = new ForwardedHeadersOptions();

        Assert.True(
            OperationalConfiguration.IsValidTrustedProxyOptions(source));

        OperationalConfiguration.ApplyForwardedHeaders(target, source);

        Assert.Equal(1, target.ForwardLimit);
        Assert.True(target.RequireHeaderSymmetry);
        Assert.True(
            target.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(
            target.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
        Assert.Contains(IPAddress.Parse("172.18.0.1"), target.KnownProxies);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("not-an-ip")]
    public void TrustedProxyOptions_전체주소와_잘못된IP를_거부한다(string proxy)
    {
        var options = new TrustedProxyOptions
        {
            KnownProxies = new List<string> { proxy }
        };

        Assert.False(
            OperationalConfiguration.IsValidTrustedProxyOptions(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void TrustedProxyOptions_과도한ForwardLimit을_거부한다(int limit)
    {
        var options = new TrustedProxyOptions
        {
            ForwardLimit = limit
        };

        Assert.False(
            OperationalConfiguration.IsValidTrustedProxyOptions(options));
    }

    [Fact]
    public void AuthServerOptions_운영환경에서는_32자이상키를_요구한다()
    {
        var invalid = new AuthServerOptions
        {
            BaseUrl = "http://poscam-auth-api:8080",
            InternalServiceKey = "short",
            TimeoutSeconds = 5
        };
        var valid = new AuthServerOptions
        {
            BaseUrl = "http://poscam-auth-api:8080",
            InternalServiceKey = new string('k', 32),
            TimeoutSeconds = 5
        };

        Assert.False(
            OperationalConfiguration.IsValidAuthServerOptions(
                invalid,
                requireServiceKey: true));
        Assert.True(
            OperationalConfiguration.IsValidAuthServerOptions(
                valid,
                requireServiceKey: true));
    }
}
