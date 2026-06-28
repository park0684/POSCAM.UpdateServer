using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Infrastructure.Middleware;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddOptions<UpdateStorageOptions>()
    .Bind(builder.Configuration.GetSection(UpdateStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options =>
            Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        "UpdateStorage:PublicBaseUrl은 유효한 HTTP 또는 HTTPS 절대 URL이어야 합니다.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AuthServerOptions>()
    .Bind(builder.Configuration.GetSection(AuthServerOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<AdminWebCorsOptions>()
    .Bind(builder.Configuration.GetSection(AdminWebCorsOptions.SectionName));

builder.Services
    .AddOptions<UpdateCheckRateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(UpdateCheckRateLimitingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IDbContext, DapperContext>();
builder.Services.AddScoped<IUpdateProductRepository, UpdateProductRepository>();
builder.Services.AddScoped<IUpdateReleaseRepository, UpdateReleaseRepository>();
builder.Services.AddScoped<IUpdateArtifactRepository, UpdateArtifactRepository>();
builder.Services.AddScoped<IUpdateAuditLogRepository, UpdateAuditLogRepository>();

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: new[] { "live" });

var app = builder.Build();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

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
        Predicate = registration => registration.Tags.Contains("live")
    });

app.Run();

/// <summary>
/// 통합 테스트에서 WebApplicationFactory가 진입점을 참조할 수 있도록 공개한다.
/// </summary>
public partial class Program
{
}
