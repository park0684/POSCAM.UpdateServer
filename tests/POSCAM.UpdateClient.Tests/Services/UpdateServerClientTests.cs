using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using POSCAM.UpdateClient.Tests.TestDoubles;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class UpdateServerClientTests
    {
        [Fact]
        public async Task CheckAsync_SuccessResponse_ParsesDataAndManifest()
        {
            var handler = new StubHttpMessageHandler(
                _ => CreateJsonResponse(
                    HttpStatusCode.OK,
                    "{\"success\":true,\"message\":\"ok\",\"errorCode\":0,"
                    + "\"data\":{\"updateAvailable\":false,\"mandatory\":false,"
                    + "\"reasonCode\":\"ALREADY_LATEST\",\"productCode\":\"PCCAM\","
                    + "\"currentVersion\":\"1.0.0\",\"channel\":\"stable\","
                    + "\"os\":\"windows\",\"architecture\":\"x86\","
                    + "\"files\":[{\"path\":\"PcCam.exe\",\"size\":12,"
                    + "\"sha256\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\","
                    + "\"required\":true,\"downloadUrl\":\"https://update.poscam.co.kr/packages/file\"}]}}"));

            using (var httpClient = new HttpClient(handler))
            using (var client = new UpdateServerClient(
                httpClient,
                "https://update.poscam.co.kr"))
            {
                var response = await client.CheckAsync(
                    CreateRequest(),
                    CancellationToken.None);

                Assert.False(response.UpdateAvailable);
                Assert.Equal("ALREADY_LATEST", response.ReasonCode);
                var file = Assert.Single(response.Files);
                Assert.Equal("PcCam.exe", file.Path);
                Assert.True(file.Required);

                Assert.Equal(
                    "https://update.poscam.co.kr/api/v1/updates/check",
                    handler.RequestedUri!.AbsoluteUri);
                Assert.Contains(
                    "\"productCode\":\"PCCAM\"",
                    handler.RequestBody);
                Assert.Contains(
                    "\"currentVersion\":\"1.0.0\"",
                    handler.RequestBody);
            }
        }

        [Fact]
        public async Task CheckAsync_NullFiles_NormalizesToEmptyList()
        {
            var handler = new StubHttpMessageHandler(
                _ => CreateJsonResponse(
                    HttpStatusCode.OK,
                    "{\"success\":true,\"message\":\"ok\",\"errorCode\":0,"
                    + "\"data\":{\"updateAvailable\":false,\"files\":null}}"));

            using (var httpClient = new HttpClient(handler))
            using (var client = new UpdateServerClient(
                httpClient,
                "https://update.poscam.co.kr"))
            {
                var response = await client.CheckAsync(
                    CreateRequest(),
                    CancellationToken.None);

                Assert.NotNull(response.Files);
                Assert.Empty(response.Files);
            }
        }

        [Fact]
        public async Task CheckAsync_ApiFailure_ThrowsTypedException()
        {
            var handler = new StubHttpMessageHandler(
                _ => CreateJsonResponse(
                    HttpStatusCode.BadRequest,
                    "{\"success\":false,\"message\":\"invalid\","
                    + "\"errorCode\":1001,\"data\":null}"));

            using (var httpClient = new HttpClient(handler))
            using (var client = new UpdateServerClient(
                httpClient,
                "https://update.poscam.co.kr"))
            {
                var exception = await Assert.ThrowsAsync<
                    UpdateServerClientException>(
                    () => client.CheckAsync(
                        CreateRequest(),
                        CancellationToken.None));

                Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
                Assert.Equal(1001, exception.ErrorCode);
            }
        }

        [Fact]
        public async Task CheckAsync_InvalidJson_ThrowsTypedException()
        {
            var handler = new StubHttpMessageHandler(
                _ => CreateJsonResponse(
                    HttpStatusCode.OK,
                    "not-json"));

            using (var httpClient = new HttpClient(handler))
            using (var client = new UpdateServerClient(
                httpClient,
                "https://update.poscam.co.kr"))
            {
                await Assert.ThrowsAsync<UpdateServerClientException>(
                    () => client.CheckAsync(
                        CreateRequest(),
                        CancellationToken.None));
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("ftp://update.poscam.co.kr")]
        [InlineData("not-a-url")]
        public void Constructor_InvalidBaseUrl_Throws(string baseUrl)
        {
            using (var httpClient = new HttpClient(
                new StubHttpMessageHandler(
                    _ => CreateJsonResponse(HttpStatusCode.OK, "{}"))))
            {
                Assert.Throws<System.ArgumentException>(
                    () => new UpdateServerClient(httpClient, baseUrl));
            }
        }

        private static UpdateCheckRequest CreateRequest()
        {
            return new UpdateCheckRequest
            {
                ProductCode = "PCCAM",
                CurrentVersion = "1.0.0",
                Os = "windows",
                Architecture = "x86",
                Channel = "stable"
            };
        }

        private static HttpResponseMessage CreateJsonResponse(
            HttpStatusCode statusCode,
            string json)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
