using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Infrastructure.Health;
using POSCAM.UpdateServer.Api.Infrastructure.Middleware;
using POSCAM.UpdateServer.Api.Infrastructure.Operations;
using POSCAM.UpdateServer.Api.Models.Common;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Repositories;
using POSCAM.UpdateServer.Api.Services;
using POSCAM.UpdateServer.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

var configuredMaxUploadBytes = builder.Configuration.GetValue<long?>(
    $"{UpdateStorageOptions.SectionName}:MaxUploadBytes") ?? 1_073_741_824L;
var multipartRequestLimit = configuredMaxUploadBytes <= long.MaxValue - 1_048_576L
    ? configuredMaxUploadBytes + 1_048_576L
    : configuredMaxUploadBytes;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = multipartRequestLimit;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = multipartRequestLimit;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    var configuredTrustedProxies = builder.Configuration
        .GetSection(TrustedProxyOptions.SectionName)
        .Get<TrustedProxyOptions>() ?? new TrustedProxyOptions();

    OperationalConfiguration.ApplyForwardedHeaders(
        options,
        configuredTrustedProxies);
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        context.HttpContext.Response.Headers["Cache-Control"] = "no-store";
        context.HttpContext.Response.Headers["Pragma"] = "no-cache";
        context.HttpContext.Response.Headers["Expires"] = "0";

        var response = ApiResponse<object?>.Fail(
            UpdateErrorCode.ValidationError,
            "요청 형식이 올바르지 않습니다.");

        return new BadRequestObjectResult(response);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddOptions<UpdateStorageOptions>()
    .Bind(builder.Configuration.GetSection(UpdateStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options =>
            Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && (!builder.Environment.IsProduction()
                || uri.Scheme == Uri.UriSchemeHttps),
        "UpdateStorage:PublicBaseUrl은 유효한 절대 URL이어야 하며 운영 환경에서는 HTTPS여야 합니다.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AuthServerOptions>()
    .Bind(builder.Configuration.GetSection(AuthServerOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => OperationalConfiguration.IsValidAuthServerOptions(
            options,
            builder.Environment.IsProduction()),
        "AuthServer 설정이 올바르지 않습니다. 운영 환경에는 32자 이상의 내부 서비스 키가 필요합니다.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AdminWebCorsOptions>()
    .Bind(builder.Configuration.GetSection(AdminWebCorsOptions.SectionName))
    .Validate(
        OperationalConfiguration.IsValidCorsOptions,
        "Cors:AdminWebOrigins에는 경로 없는 HTTP 또는 HTTPS Origin만 설정할 수 있습니다.")
    .Validate(
        options =>
            !builder.Environment.IsProduction()
            || OperationalConfiguration.AreHttpsOrigins(options),
        "운영 환경에는 하나 이상의 HTTPS Cors:AdminWebOrigins가 필요합니다.")
    .ValidateOnStart();

builder.Services
    .AddOptions<TrustedProxyOptions>()
    .Bind(builder.Configuration.GetSection(TrustedProxyOptions.SectionName))
    .Validate(
        OperationalConfiguration.IsValidTrustedProxyOptions,
        "ForwardedHeaders 설정이 올바르지 않습니다.")
    .Validate(
        options =>
            !builder.Environment.IsProduction()
            || options.KnownProxies.Any(value => !string.IsNullOrWhiteSpace(value)),
        "운영 환경에는 ForwardedHeaders:KnownProxies가 하나 이상 필요합니다.")
    .ValidateOnStart();

builder.Services
    .AddOptions<UpdateCheckRateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(UpdateCheckRateLimitingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddCors(options =>
{
    var configuredCors = builder.Configuration
        .GetSection(AdminWebCorsOptions.SectionName)
        .Get<AdminWebCorsOptions>() ?? new AdminWebCorsOptions();
    var normalizedAdminOrigins = OperationalConfiguration.GetNormalizedOrigins(
        configuredCors);

    options.AddPolicy(
        OperationalPolicyNames.AdminWebCors,
        policy =>
        {
            if (normalizedAdminOrigins.Length > 0)
            {
                policy.WithOrigins(normalizedAdminOrigins);
            }
            else
            {
                policy.SetIsOriginAllowed(_ => false);
            }

            policy
                .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                .WithHeaders(
                    "Authorization",
                    "Content-Type",
                    RequestIdMiddleware.HeaderName)
                .WithExposedHeaders(RequestIdMiddleware.HeaderName)
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });
});

builder.Services.AddRateLimiter(options =>
{
    var configuredRateLimit = builder.Configuration
        .GetSection(UpdateCheckRateLimitingOptions.SectionName)
        .Get<UpdateCheckRateLimitingOptions>()
        ?? new UpdateCheckRateLimitingOptions();

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = RateLimitResponseWriter.WriteRejectedAsync;

    options.AddPolicy(
        OperationalPolicyNames.UpdateCheckRateLimit,
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = configuredRateLimit.UpdateCheckPermitLimit,
                Window = TimeSpan.FromSeconds(
                    configuredRateLimit.UpdateCheckWindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
});

builder.Services.AddSingleton<IDbContext, DapperContext>();
builder.Services.AddScoped<IUpdateProductRepository, UpdateProductRepository>();
builder.Services.AddScoped<IUpdateReleaseRepository, UpdateReleaseRepository>();
builder.Services.AddScoped<IReleaseManagementQueryRepository, ReleaseManagementQueryRepository>();
builder.Services.AddScoped<IUpdateArtifactRepository, UpdateArtifactRepository>();
builder.Services.AddScoped<IArtifactManagementQueryRepository, ArtifactManagementQueryRepository>();
builder.Services.AddScoped<IUpdateAuditLogRepository, UpdateAuditLogRepository>();
builder.Services.AddScoped<IAuditManagementQueryRepository, AuditManagementQueryRepository>();
builder.Services.AddScoped<IUpdateCheckService, UpdateCheckService>();
builder.Services.AddScoped<IReleaseManagementService, ReleaseManagementService>();
builder.Services.AddScoped<IArtifactUploadService, ArtifactUploadService>();
builder.Services.AddScoped<IReleaseLifecycleService, ReleaseLifecycleService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddSingleton<IZipPackageValidator, ZipPackageValidator>();
builder.Services.AddSingleton<IArtifactStorageService, ArtifactStorageService>();

builder.Services.AddScoped<IUpdateManagementActorAccessor, UpdateManagementActorAccessor>();
builder.Services
    .AddHttpClient<IUpdateManagementAuthorizationClient, UpdateManagementAuthorizationClient>(client =>
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
    });

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: new[] { "live" })
    .AddCheck<DatabaseReadyHealthCheck>(
        "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" })
    .AddCheck<StorageReadyHealthCheck>(
        "storage",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" });

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseCors(OperationalPolicyNames.AdminWebCors);
app.UseRateLimiter();
app.UseMiddleware<UpdateManagementAuthorizationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = HealthResponseWriter.WriteAsync
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthResponseWriter.WriteAsync
    });

app.Run();

/// <summary>
/// 통합 테스트에서 WebApplicationFactory가 진입점을 참조할 수 있도록 공개한다.
/// </summary>
public partial class Program
{
}
