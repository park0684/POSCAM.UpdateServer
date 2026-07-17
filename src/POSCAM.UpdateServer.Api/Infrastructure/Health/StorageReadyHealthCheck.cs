using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Options;

namespace POSCAM.UpdateServer.Api.Infrastructure.Health;

/// <summary>
/// Artifact 저장소의 staging 디렉터리에 실제 쓰기·삭제가 가능한지 확인한다.
/// 검사 파일명과 물리 경로는 외부 응답에 노출하지 않는다.
/// </summary>
public sealed class StorageReadyHealthCheck : IHealthCheck
{
    private readonly UpdateStorageOptions _options;
    private readonly ILogger<StorageReadyHealthCheck> _logger;

    public StorageReadyHealthCheck(
        IOptions<UpdateStorageOptions> options,
        ILogger<StorageReadyHealthCheck> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string? probePath = null;

        try
        {
            var rootPath = Path.GetFullPath(_options.RootPath);
            var stagingPath = Path.GetFullPath(
                Path.Combine(rootPath, ".staging"));

            EnsureInsideRoot(rootPath, stagingPath);
            Directory.CreateDirectory(stagingPath);

            probePath = Path.Combine(
                stagingPath,
                $".health-{Guid.NewGuid():N}.tmp");
            EnsureInsideRoot(rootPath, probePath);

            await File.WriteAllTextAsync(
                probePath,
                "ready",
                cancellationToken);
            File.Delete(probePath);
            probePath = null;

            return HealthCheckResult.Healthy("Update storage is ready.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Update storage readiness check failed. ExceptionType: {ExceptionType}",
                exception.GetType().Name);

            return HealthCheckResult.Unhealthy("Update storage is unavailable.");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(probePath))
            {
                try
                {
                    if (File.Exists(probePath))
                    {
                        File.Delete(probePath);
                    }
                }
                catch
                {
                    // Readiness 원인 응답을 보존하고 다음 검사에서 다시 확인한다.
                }
            }
        }
    }

    private static void EnsureInsideRoot(
        string rootPath,
        string candidatePath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!normalizedCandidate.StartsWith(normalizedRoot, comparison))
        {
            throw new InvalidOperationException(
                "Storage readiness path is outside the configured root.");
        }
    }
}
