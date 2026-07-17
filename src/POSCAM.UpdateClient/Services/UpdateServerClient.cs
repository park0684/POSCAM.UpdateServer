using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 공개 Update Check API를 호출한다.
    /// </summary>
    internal sealed class UpdateServerClient : IUpdateServerClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly Uri _checkEndpoint;

        public UpdateServerClient(string baseUrl)
            : this(
                new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(15)
                },
                baseUrl,
                true)
        {
        }

        internal UpdateServerClient(
            HttpClient httpClient,
            string baseUrl,
            bool ownsHttpClient = false)
        {
            _httpClient = httpClient
                ?? throw new ArgumentNullException(nameof(httpClient));

            _ownsHttpClient = ownsHttpClient;
            _checkEndpoint = BuildCheckEndpoint(baseUrl);
        }

        public async Task<UpdateCheckResponse> CheckAsync(
            UpdateCheckRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var requestJson = JsonConvert.SerializeObject(request);

            using (var content = new StringContent(
                requestJson,
                Encoding.UTF8,
                "application/json"))
            using (var response = await _httpClient
                .PostAsync(
                    _checkEndpoint,
                    content,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                var responseJson = await response.Content
                    .ReadAsStringAsync()
                    .ConfigureAwait(false);

                ApiResponse<UpdateCheckResponse>? envelope;

                try
                {
                    envelope = JsonConvert.DeserializeObject<
                        ApiResponse<UpdateCheckResponse>>(responseJson);
                }
                catch (JsonException exception)
                {
                    throw new UpdateServerClientException(
                        "UpdateServer 응답 JSON을 해석할 수 없습니다.",
                        response.StatusCode,
                        null,
                        exception);
                }

                if (envelope == null)
                {
                    throw new UpdateServerClientException(
                        "UpdateServer 응답이 비어 있습니다.",
                        response.StatusCode);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new UpdateServerClientException(
                        "UpdateServer가 업데이트 확인 요청을 거부했습니다.",
                        response.StatusCode,
                        envelope.ErrorCode);
                }

                if (!envelope.Success || envelope.Data == null)
                {
                    throw new UpdateServerClientException(
                        "UpdateServer가 실패 응답을 반환했습니다.",
                        response.StatusCode,
                        envelope.ErrorCode);
                }

                if (envelope.Data.Files == null)
                {
                    envelope.Data.Files = new System.Collections.Generic.List<
                        UpdateManifestFile>();
                }

                return envelope.Data;
            }
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        private static Uri BuildCheckEndpoint(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException(
                    "UpdateServer 기본 URL이 비어 있습니다.",
                    nameof(baseUrl));
            }

            Uri baseUri;

            try
            {
                baseUri = new Uri(
                    baseUrl.Trim().TrimEnd('/') + "/",
                    UriKind.Absolute);
            }
            catch (UriFormatException exception)
            {
                throw new ArgumentException(
                    "UpdateServer 기본 URL이 올바르지 않습니다.",
                    nameof(baseUrl),
                    exception);
            }

            if (!string.Equals(
                    baseUri.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    baseUri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "UpdateServer 기본 URL은 HTTP 또는 HTTPS만 사용할 수 있습니다.",
                    nameof(baseUrl));
            }

            return new Uri(
                baseUri,
                "api/v1/updates/check");
        }
    }
}
