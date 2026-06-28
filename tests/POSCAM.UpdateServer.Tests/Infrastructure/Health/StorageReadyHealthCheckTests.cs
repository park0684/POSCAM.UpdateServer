using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Infrastructure.Health;
using POSCAM.UpdateServer.Api.Options;

namespace POSCAM.UpdateServer.Tests.Infrastructure.Health;

public class StorageReadyHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_Staging쓰기와_삭제가_가능하면_Healthy이다()
    {
        var root = CreateTempPath();

        try
        {
            var check = CreateCheck(root);

            var result = await check.CheckHealthAsync(
                new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            var stagingPath = Path.Combine(root, ".staging");
            Assert.True(Directory.Exists(stagingPath));
            Assert.Empty(Directory.EnumerateFiles(stagingPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CheckHealthAsync_Root가_파일이면_Unhealthy이다()
    {
        var rootFile = CreateTempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(rootFile)!);
        await File.WriteAllTextAsync(rootFile, "not-a-directory");

        try
        {
            var check = CreateCheck(rootFile);

            var result = await check.CheckHealthAsync(
                new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.DoesNotContain(
                rootFile,
                result.Description ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(rootFile))
            {
                File.Delete(rootFile);
            }
        }
    }

    private static StorageReadyHealthCheck CreateCheck(string rootPath)
    {
        return new StorageReadyHealthCheck(
            Options.Create(new UpdateStorageOptions
            {
                RootPath = rootPath,
                PublicBaseUrl = "https://update.example.com"
            }),
            NullLogger<StorageReadyHealthCheck>.Instance);
    }

    private static string CreateTempPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "poscam-update-ready-tests",
            Guid.NewGuid().ToString("N"));
    }
}
