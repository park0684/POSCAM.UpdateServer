using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Tests.Storage;

public class ArtifactStorageServiceTests
{
    [Fact]
    public async Task SaveValidateMove_크기와_SHA를_계산하고_packages로_이동한다()
    {
        var root = CreateTempDirectory();
        var zipBytes = CreateZipBytes(("app.exe", "content"));
        var service = CreateService(root);

        try
        {
            await using var source = new MemoryStream(zipBytes);
            var staged = await service.SaveToStagingAsync(source);

            Assert.Equal(zipBytes.LongLength, staged.FileSize);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant(),
                staged.Sha256);
            Assert.True(File.Exists(staged.PhysicalPath));

            await service.ValidatePackageAsync(staged);

            var destination = service.CreateDestination(
                "PCCAM",
                "stable",
                "1.0.0",
                "x86");

            await service.MoveToPackagesAsync(staged, destination);

            var finalPath = Path.Combine(
                root,
                "packages",
                destination.StorageKey.Replace('/', Path.DirectorySeparatorChar));

            Assert.False(File.Exists(staged.PhysicalPath));
            Assert.True(File.Exists(finalPath));
            Assert.Equal("PCCAM_1.0.0_x86.zip", destination.FileName);
            Assert.Equal(32, destination.PublicId.Length);
            Assert.Contains(destination.PublicId, destination.StorageKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveToStagingAsync_실제Stream크기가_한도를_넘으면_거부하고_정리한다()
    {
        var root = CreateTempDirectory();
        var service = CreateService(root, maxUploadBytes: 10);

        try
        {
            await using var source = new MemoryStream(new byte[11]);

            var exception = await Assert.ThrowsAsync<ArtifactStorageException>(
                () => service.SaveToStagingAsync(source));

            Assert.Equal(ArtifactStorageFailureType.FileTooLarge, exception.FailureType);

            var staging = Path.Combine(root, ".staging");
            Assert.True(Directory.Exists(staging));
            Assert.Empty(Directory.EnumerateFiles(staging));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidatePackageAsync_손상ZIP을_거부한다()
    {
        var root = CreateTempDirectory();
        var service = CreateService(root);

        try
        {
            await using var source = new MemoryStream("broken"u8.ToArray());
            var staged = await service.SaveToStagingAsync(source);

            var exception = await Assert.ThrowsAsync<ArtifactStorageException>(
                () => service.ValidatePackageAsync(staged));

            Assert.Equal(ArtifactStorageFailureType.InvalidPackage, exception.FailureType);
            await service.DeleteStagingAsync(staged);
            Assert.False(File.Exists(staged.PhysicalPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CreateDestination_서버값으로만_경로를_생성한다()
    {
        var root = CreateTempDirectory();

        try
        {
            var destination = CreateService(root).CreateDestination(
                "CAMVIEWER",
                "beta",
                "2.3.4.5",
                "x64");

            Assert.Equal("CAMVIEWER_2.3.4.5_x64.zip", destination.FileName);
            Assert.StartsWith("camviewer/beta/2.3.4.5/", destination.StorageKey);
            Assert.EndsWith("/CAMVIEWER_2.3.4.5_x64.zip", destination.StorageKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside/file.zip")]
    [InlineData("/absolute/file.zip")]
    [InlineData("safe\\windows-path.zip")]
    public async Task RemoveOrQuarantineAsync_Root탈출StorageKey를_거부한다(
        string storageKey)
    {
        var root = CreateTempDirectory();

        try
        {
            var removed = await CreateService(root).RemoveOrQuarantineAsync(storageKey);
            Assert.False(removed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ArtifactStorageService CreateService(
        string root,
        long maxUploadBytes = 1024 * 1024)
    {
        var options = Options.Create(new UpdateStorageOptions
        {
            RootPath = root,
            PublicBaseUrl = "https://update.poscam.co.kr",
            MaxUploadBytes = maxUploadBytes,
            MaxArchiveEntries = 100,
            MaxExpandedBytes = 1024 * 1024
        });

        return new ArtifactStorageService(
            options,
            new ZipPackageValidator(),
            NullLogger<ArtifactStorageService>.Instance);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "poscam-update-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] CreateZipBytes(
        params (string Name, string Content)[] entries)
    {
        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(
                   memory,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            foreach (var item in entries)
            {
                var entry = archive.CreateEntry(item.Name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(item.Content);
            }
        }

        return memory.ToArray();
    }
}
