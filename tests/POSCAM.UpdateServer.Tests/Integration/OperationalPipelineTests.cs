using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Updates;
using POSCAM.UpdateServer.Api.Services;

namespace POSCAM.UpdateServer.Tests.Integration;

public class OperationalPipelineTests
{
    [Fact]
    public async Task 허용된Origin의_관리자Preflight는_인증호출전에_성공한다()
    {
        using var factory = new OperationalWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = CreatePreflight("https://admin.example.com");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "https://admin.example.com",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains(
            "authorization",
            string.Join(
                    ',',
                    response.Headers.GetValues("Access-Control-Allow-Headers"))
                .ToLowerInvariant());
    }

    [Fact]
    public async Task 허용되지않은Origin에는_CORS허용Header를_반환하지_않는다()
    {
        using var factory = new OperationalWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = CreatePreflight("https://evil.example.com");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task UpdateCheck는_IP별고정창한도를_넘으면_429_9004를_반환한다()
    {
        using var factory = new OperationalWebApplicationFactory();
        using var client = factory.CreateClient();

        using var first = await PostUpdateCheckAsync(client);
        using var second = await PostUpdateCheckAsync(client);
        using var rejected = await PostUpdateCheckAsync(client);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("no-store", rejected.Headers.CacheControl?.ToString());

        var body = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":false", body, StringComparison.Ordinal);
        Assert.Contains("\"errorCode\":9004", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 안전한RequestId는_응답까지_연결된다()
    {
        using var factory = new OperationalWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = CreateUpdateCheckRequest();
        request.Headers.Add("X-Request-ID", "update-check_2026.06.28:01");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "update-check_2026.06.28:01",
            Assert.Single(response.Headers.GetValues("X-Request-ID")));
    }

    [Fact]
    public async Task 위험한RequestId는_새식별자로_교체된다()
    {
        using var factory = new OperationalWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = CreateUpdateCheckRequest();
        request.Headers.TryAddWithoutValidation("X-Request-ID", "bad request id");

        using var response = await client.SendAsync(request);

        var responseRequestId = Assert.Single(
            response.Headers.GetValues("X-Request-ID"));

        Assert.NotEqual("bad request id", responseRequestId);
        Assert.Equal(32, responseRequestId.Length);
    }

    [Fact]
    public async Task LiveHealth는_DB나_AuthServer없이_프로세스상태만_반환한다()
    {
        using var factory = new OperationalWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"self\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("database", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storage", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("auth", body, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpRequestMessage CreatePreflight(string origin)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/v1/admin/releases");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add(
            "Access-Control-Request-Headers",
            "authorization,x-request-id");
        return request;
    }

    private static async Task<HttpResponseMessage> PostUpdateCheckAsync(
        HttpClient client)
    {
        using var request = CreateUpdateCheckRequest();
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CreateUpdateCheckRequest()
    {
        return new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/updates/check")
        {
            Content = JsonContent.Create(
                new UpdateCheckRequest
                {
                    ProductCode = ProductCodes.Pccam,
                    CurrentVersion = "1.0.0",
                    Os = UpdateOperatingSystems.Windows,
                    Architecture = ArtifactArchitectures.X86,
                    Channel = ReleaseChannels.Stable
                })
        };
    }

    private sealed class OperationalWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        private readonly string _storageRoot = Path.Combine(
            Path.GetTempPath(),
            "poscam-update-operational-tests",
            Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            "Server=localhost;Database=poscam_update;Uid=test;Pwd=test;",
                        ["UpdateStorage:RootPath"] = _storageRoot,
                        ["UpdateStorage:PublicBaseUrl"] =
                            "https://update.example.com",
                        ["AuthServer:BaseUrl"] = "http://auth.test",
                        ["AuthServer:TimeoutSeconds"] = "1",
                        ["Cors:AdminWebOrigins:0"] =
                            "https://admin.example.com",
                        ["ForwardedHeaders:ForwardLimit"] = "1",
                        ["RateLimiting:UpdateCheckPermitLimit"] = "2",
                        ["RateLimiting:UpdateCheckWindowSeconds"] = "60"
                    });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUpdateCheckService>();
                services.AddSingleton<IUpdateCheckService>(
                    new SuccessfulUpdateCheckService());
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            try
            {
                if (Directory.Exists(_storageRoot))
                {
                    Directory.Delete(_storageRoot, recursive: true);
                }
            }
            catch
            {
                // 테스트 결과를 보존한다.
            }
        }
    }

    private sealed class SuccessfulUpdateCheckService : IUpdateCheckService
    {
        public Task<UpdateCheckServiceResult> CheckAsync(
            UpdateCheckRequest? request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                UpdateCheckServiceResult.Ok(
                    new UpdateCheckResponse
                    {
                        UpdateAvailable = false,
                        Mandatory = false,
                        ReasonCode = "ALREADY_LATEST",
                        ProductCode = request?.ProductCode ?? ProductCodes.Pccam,
                        CurrentVersion = request?.CurrentVersion ?? "1.0.0",
                        Channel = request?.Channel ?? ReleaseChannels.Stable,
                        Os = request?.Os ?? UpdateOperatingSystems.Windows,
                        Architecture = request?.Architecture
                                       ?? ArtifactArchitectures.X86
                    }));
        }
    }
}
