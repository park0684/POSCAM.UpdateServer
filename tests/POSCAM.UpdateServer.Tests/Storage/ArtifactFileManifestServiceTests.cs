using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Tests.Storage;

public class ArtifactFileManifestServiceTests
{
    [Fact]
    public async Task CreateManifestFilesAsync_포함대상만_저장하고_SHA를_계산한다()
    {
        var root = CreateTempDirectory();
        var zipPath = CreateZip(
            ("PCCAM.exe", "app"),
            ("PcCam.exe.config", "runtime-config"),
            ("providers/DahuaProvider.dll", "provider"),
            ("config/settings.json", "{}"),
            ("logs/app.log", "log"),
            ("readme.txt", "readme"));
        var service = CreateService(root);
        var destination = CreateDestination();

        try
        {
            var files = await service.CreateManifestFilesAsync(zipPath, destination);

            Assert.Equal(3, files.Count);
            Assert.Contains(files, file => file.FilePath == "PCCAM.exe");
            Assert.Contains(files, file => file.FilePath == "PcCam.exe.config");
            Assert.Contains(files, file => file.FilePath == "providers/DahuaProvider.dll");
            Assert.DoesNotContain(files, file => file.FilePath.StartsWith("config/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(files, file => file.FilePath.StartsWith("logs/", StringComparison.OrdinalIgnoreCase));

            var appFile = files.Single(file => file.FilePath == "PCCAM.exe");
            Assert.Equal(3, appFile.FileSize);
            Assert.Equal(Sha256("app"), appFile.Sha256);
            Assert.True(appFile.IsRequired);
            Assert.Equal(32, appFile.PublicId.Length);
            Assert.Equal(appFile.StorageKey, appFile.DownloadPath);
            Assert.StartsWith("pccam/stable/1.0.0/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/files/", appFile.StorageKey);
            Assert.True(File.Exists(appFile.PhysicalPath));

            var runtimeConfigFile = files.Single(file => file.FilePath == "PcCam.exe.config");
            Assert.Equal(Sha256("runtime-config"), runtimeConfigFile.Sha256);
            Assert.True(runtimeConfigFile.IsRequired);
            Assert.True(File.Exists(runtimeConfigFile.PhysicalPath));

            var providerFile = files.Single(file => file.FilePath == "providers/DahuaProvider.dll");
            Assert.Equal(Sha256("provider"), providerFile.Sha256);
            Assert.True(File.Exists(providerFile.PhysicalPath));
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateManifestFilesAsync_위험한_Entry를_거부하고_생성파일을_정리한다()
    {
        var root = CreateTempDirectory();
        var zipPath = CreateZip(
            ("PCCAM.exe", "app"),
            ("../evil.exe", "evil"));
        var service = CreateService(root);

        try
        {
            var exception = await Assert.ThrowsAsync<ArtifactStorageException>(
                () => service.CreateManifestFilesAsync(zipPath, CreateDestination()));

            Assert.Equal(ArtifactStorageFailureType.InvalidPackage, exception.FailureType);

            var filesDirectory = Path.Combine(
                root,
                "packages",
                "pccam",
                "stable",
                "1.0.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "files");

            if (Directory.Exists(filesDirectory))
            {
                Assert.Empty(Directory.EnumerateFiles(filesDirectory));
            }
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteManifestFilesAsync_생성된_파일을_삭제한다()
    {
        var root = CreateTempDirectory();
        var zipPath = CreateZip(("PCCAM.exe", "app"));
        var service = CreateService(root);

        try
        {
            var files = await service.CreateManifestFilesAsync(zipPath, CreateDestination());
            Assert.Single(files);
            Assert.True(File.Exists(files[0].PhysicalPath));

            await service.DeleteManifestFilesAsync(files);

            Assert.False(File.Exists(files[0].PhysicalPath));
        }
        finally
        {
            File.Delete(zipPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("PCCAM.exe", true)]
    [InlineData("PccAuthClient.dll", true)]
    [InlineData("providers/DahuaProvider.dll", true)]
    [InlineData("ffmpeg.exe", true)]
    [InlineData("mediamtx.exe", true)]
    [InlineData("PcCam.exe.config", true)]
    [InlineData("Other.exe.config", false)]
    [InlineData("config/PcCam.exe.config", false)]
    [InlineData("config/settings.json", false)]
    [InlineData("logs/app.log", false)]
    [InlineData("cache/runtime.bin", false)]
    [InlineData("readme.txt", false)]
    public void IsManifestTarget_포함_제외_대상을_판정한다(
        string path,
        bool expected)
    {
        Assert.Equal(expected, ArtifactFileManifestService.IsManifestTarget(path));
    }

    private static ArtifactFileManifestService CreateService(string root)
    {
        var options = Options.Create(new UpdateStorageOptions
        {
            RootPath = root,
            PublicBaseUrl = "https://update.poscam.co.kr",
            MaxUploadBytes = 1024 * 1024,
            MaxArchiveEntries = 100,
            MaxExpandedBytes = 1024 * 1024
        });

        return new ArtifactFileManifestService(
            options,
            NullLogger<ArtifactFileManifestService>.Instance);
    }

    private static ArtifactStorageDestination CreateDestination()
    {
        return new ArtifactStorageDestination
        {
            PublicId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            FileName = "PCCAM_1.0.0_x86.zip",
            StorageKey = "pccam/stable/1.0.0/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/PCCAM_1.0.0_x86.zip"
        };
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

    private static string CreateZip(params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");

        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);

        foreach (var item in entries)
        {
            var entry = archive.CreateEntry(item.Name, CompressionLevel.Fastest);
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(item.Content);
            stream.Write(bytes, 0, bytes.Length);
        }

        return path;
    }

    private static string Sha256(string content)
    {
        return Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();
    }
}
