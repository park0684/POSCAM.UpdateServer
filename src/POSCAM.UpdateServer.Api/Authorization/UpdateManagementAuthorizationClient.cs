using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Infrastructure.Middleware;
using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Options;

namespace POSCAM.UpdateServer.Api.Authorization;

/// <summary>
/// AccountToken을 해석하거나 검증하지 않고 AuthServer 내부 권한 API에 그대로 전달한다.
/// 권한 결과는 매 관리자 요청마다 새로 확인하며 캐시하지 않는다.
/// </summary>
public sealed class UpdateManagementAuthorizationClient
    : IUpdateManagementAuthorizationClient
{
    internal const string AuthorizePath = "api/internal/update-management/authorize";
    internal const string ServiceKeyHeaderName = "X-POSCAM-Service-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AuthServerOptions _options;
    private readonly ILogger<UpdateManagementAuthorizationClient> _logger;

    public UpdateManagementAuthorizationClient(
        HttpClient httpClient,
        IOptions<AuthServerOptions> options,
        ILogger<UpdateManagementAuthorizationClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UpdateManagementAuthorizationResult> AuthorizeAsync(
        string? authorizationHeader,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.InternalServiceKey))
        {
            _logger.LogError(
                "AuthServer 내부 서비스 키가 설정되지 않았습니다. RequestId: {RequestId}",
                requestId);

            return Unavailable();
        }

        if (_options.TimeoutSeconds is < 1 or > 30)
        {
            _logger.LogError(
                "AuthServer 권한 확인 TimeoutSeconds 설정이 올바르지 않습니다. RequestId: {RequestId}",
                requestId);

            return Unavailable();
        }

        if (!TryCreateAuthorizeUri(_options.BaseUrl, out var authorizeUri))
        {
            _logger.LogError(
                "AuthServer BaseUrl 설정이 올바르지 않습니다. RequestId: {RequestId}",
                requestId);

            return Unavailable();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            authorizeUri);

        if (!string.IsNullOrWhiteSpace(authorizationHeader))
        {
            request.Headers.TryAddWithoutValidation(
                "Authorization",
                authorizationHeader);
        }

        request.Headers.TryAddWithoutValidation(
            ServiceKeyHeaderName,
            _options.InternalServiceKey);

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            request.Headers.TryAddWithoutValidation(
                RequestIdMiddleware.HeaderName,
                requestId);
        }

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCancellation.Token);

            AuthServerAuthorizationResponse? authResponse;

            try
            {
                await using var responseStream = await response.Content.ReadAsStreamAsync(
                    timeoutCancellation.Token);

                authResponse = await JsonSerializer.DeserializeAsync<AuthServerAuthorizationResponse>(
                    responseStream,
                    JsonOptions,
                    timeoutCancellation.Token);
            }
            catch (Exception exception)
                when (exception is JsonException
                      or NotSupportedException
                      or IOException)
            {
                _logger.LogWarning(
                    exception,
                    "AuthServer 권한 확인 응답을 해석할 수 없습니다. RequestId: {RequestId}, StatusCode: {StatusCode}",
                    requestId,
                    (int)response.StatusCode);

                return Unavailable();
            }

            if (authResponse is null)
            {
                _logger.LogWarning(
                    "AuthServer 권한 확인 응답이 비어 있습니다. RequestId: {RequestId}, StatusCode: {StatusCode}",
                    requestId,
                    (int)response.StatusCode);

                return Unavailable();
            }

            return MapResponse(
                response.StatusCode,
                authResponse,
                requestId);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "AuthServer 업데이트 관리 권한 확인이 시간 초과되었습니다. RequestId: {RequestId}",
                requestId);

            return Unavailable();
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "AuthServer 업데이트 관리 권한 확인 연결에 실패했습니다. RequestId: {RequestId}",
                requestId);

            return Unavailable();
        }
    }

    private UpdateManagementAuthorizationResult MapResponse(
        HttpStatusCode statusCode,
        AuthServerAuthorizationResponse response,
        string? requestId)
    {
        if (statusCode == HttpStatusCode.OK)
        {
            if (!response.Success
                || response.ErrorCode != 0
                || !IsValidActor(response.Data))
            {
                _logger.LogWarning(
                    "AuthServer 권한 확인 성공 응답 형식이 올바르지 않습니다. RequestId: {RequestId}",
                    requestId);

                return Unavailable();
            }

            return UpdateManagementAuthorizationResult.Allow(
                new UpdateManagementActor
                {
                    UserCode = response.Data!.UserCode,
                    UserName = response.Data.UserName,
                    UserRole = response.Data.UserRole
                });
        }

        if (statusCode == HttpStatusCode.Unauthorized)
        {
            if (response.Success)
            {
                return Unavailable();
            }

            return response.ErrorCode == (int)UpdateErrorCode.TokenExpired
                ? UpdateManagementAuthorizationResult.Deny(
                    StatusCodes.Status401Unauthorized,
                    UpdateErrorCode.TokenExpired,
                    "로그인 토큰이 만료되었습니다.")
                : UpdateManagementAuthorizationResult.Deny(
                    StatusCodes.Status401Unauthorized,
                    UpdateErrorCode.TokenInvalid,
                    "로그인 토큰이 유효하지 않습니다.");
        }

        if (statusCode == HttpStatusCode.Forbidden)
        {
            if (response.Success)
            {
                return Unavailable();
            }

            return UpdateManagementAuthorizationResult.Deny(
                StatusCodes.Status403Forbidden,
                UpdateErrorCode.PermissionDenied,
                "업데이트 관리 권한이 없습니다.");
        }

        _logger.LogWarning(
            "AuthServer 권한 확인에서 처리할 수 없는 응답을 받았습니다. RequestId: {RequestId}, StatusCode: {StatusCode}",
            requestId,
            (int)statusCode);

        return Unavailable();
    }

    private static bool IsValidActor(AuthServerActorData? actor)
    {
        return actor is not null
               && actor.UserCode > 0
               && !string.IsNullOrWhiteSpace(actor.UserName)
               && actor.UserRole is 0 or 1;
    }

    private static bool TryCreateAuthorizeUri(
        string? baseUrl,
        out Uri authorizeUri)
    {
        authorizeUri = null!;

        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(
                baseUrl.TrimEnd('/') + "/",
                UriKind.Absolute,
                out var baseUri)
            || baseUri.Scheme is not (Uri.UriSchemeHttp or Uri.UriSchemeHttps))
        {
            return false;
        }

        authorizeUri = new Uri(baseUri, AuthorizePath);
        return true;
    }

    private static UpdateManagementAuthorizationResult Unavailable()
    {
        return UpdateManagementAuthorizationResult.Deny(
            StatusCodes.Status503ServiceUnavailable,
            UpdateErrorCode.ExternalServiceUnavailable,
            "관리자 권한 확인 서비스를 사용할 수 없습니다.");
    }
}
