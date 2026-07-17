using Microsoft.Extensions.Diagnostics.HealthChecks;
using POSCAM.UpdateServer.Api.Infrastructure.Database;

namespace POSCAM.UpdateServer.Api.Infrastructure.Health;

/// <summary>
/// UpdateServer 전용 DB 연결과 최소 쿼리 실행 가능 여부만 확인한다.
/// AuthServer 상태는 확인하지 않는다.
/// </summary>
public sealed class DatabaseReadyHealthCheck : IHealthCheck
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<DatabaseReadyHealthCheck> _logger;

    public DatabaseReadyHealthCheck(
        IDbContext dbContext,
        ILogger<DatabaseReadyHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _dbContext.OpenConnectionAsync(
                cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            command.CommandTimeout = 3;

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(
                       result,
                       System.Globalization.CultureInfo.InvariantCulture) == 1
                ? HealthCheckResult.Healthy("Update database is ready.")
                : HealthCheckResult.Unhealthy("Update database readiness query failed.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Update database readiness check failed. ExceptionType: {ExceptionType}",
                exception.GetType().Name);

            return HealthCheckResult.Unhealthy("Update database is unavailable.");
        }
    }
}
