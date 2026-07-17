using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Tests.Storage;

public class ArtifactStorageLifecycleTests
{
    [Fact]
    public async Task ValidateStoredArtifactAsync_존재_크기_SHA_ZIP이_정상이면_성공한다()
    {
        var fixture = CreateFixture();
        var bytes = CreateZipBytes(("app.exe", "content"));
        var storageKey = "pccam/stable/1.0.0/public-id/package.zip";
        var path = WritePackage(fixture.RootPath, storageKey, bytes);

        try
        {
            await fixture.Service.ValidateStoredArtifactAsync(
                storageKey,
                bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
        finally
        {
            Directory.Delete(fixture.RootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateStoredArtifactAsync_파일이_없으면_무결성오류이다()
    {
        var fixture = CreateFixture();

        try
        {
            var exception = await Assert.ThrowsAsync<ArtifactStorageException>(
                () => fixture.Service.ValidateStoredArtifactAsync(
                    "pccam/stable/1.0.0/public-id/missing.zip",
                    100,
                    new string('a', 64)));

            Assert.Equal(
                ArtifactStorageFailureType.PackageIntegrityError,
                exception.FailureType);
        }
        finally
        {
            Directory.Delete(fixture.RootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateStoredArtifactAsync_크기불일치를_거부한다()
    {
        var fixture = CreateFixture();
        var bytes = CreateZipBytes(("app.exe", "content"));
        var storageKey = "pccam/stable/1.0.0/public-id/package.zip";
        WritePackage(fixture.RootPath, storageKey, bytes);

        try
        {
            var exception = await Assert.ThrowsAsync<ArtifactStorageException>(
                () => fixture.Service.ValidateStoredArtifactAsync(
                    storageKey,
                    bytes.LongLength + 1,
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));

            Assert.Equal(
                ArtifactStorageFailureType.PackageIntegrityError,
                exception.FailureType);
        }
        finally
        {
            Directory.Delete(fixture.RootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateStoredArtifactAsync_SHA불일치를_거부한다()
    {
        var fixture = CreateFixture();
        var bytes = CreateZipBytes(("app.exe", "content"));
        var storageKey = "pccam/stable/1.0.0/public-id/package.zip";
        WritePackage(fixture.RootPath, storageKey, bytes);

        try
        {
            var exception = await Assert.ThrowsAsync<ArtifactStorageException>(
                () => fixture.Service.ValidateStoredArtifactAsync(
                    storageKey,
                    bytes.LongLength,
                    new string('f', 64)));

            Assert.Equal(
                ArtifactStorageFailureType.PackageIntegrityError,
                exception.FailureType);
        }
        finally
        {
            Directory.Delete(fixture.RootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateStoredArtifactAsync_손상ZIP을_무결성오류로_변환한다()
    {
        var fixture = CreateFixture();
        var bytes = "not-a-zip"u8.ToArray();
        var storageKey = "pccam/stable/1.0.0/public-id/package.zip";
        WritePackage(fixture.RootPath, storageKey, bytes);

        try
        {
            var exception = await Assert.ThrowsAsync<ArtifactStorageException>(
                () => fixture.Service.ValidateStoredArtifactAsync(
                    storageKey,
                    bytes.LongLength,
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));

            Assert.Equal(
                ArtifactStorageFailureType.PackageIntegrityError,
                exception.FailureType);
        }
        finally
        {
            Directory.Delete(fixture.RootPath, recursive: true);
        }
    }

    [Fact]
    public async Task QuarantineAsync_파일을_공개경로에서_격리하고_복구할수있다()
    {
        var fixture = CreateFixture();
        var bytes = CreateZipBytes(("app.exe", "content"));
        var storageKey = "pccam/stable/1.0.0/public-id/package.zip";
        var packagePath = WritePackage(fixture.RootPath, storageKey, bytes);

        try
        {
            var quarantined = await fixture.Service.QuarantineAsync(storageKey);

            Assert.True(quarantined.FileMoved);
            Assert.False(File.Exists(packagePath));
            Assert.True(File.Exists(quarantined.QuarantinePhysicalPath));

            var restored = await fixture.Service.RestoreFromQuarantineAsync(quarantined);

            Assert.True(restored);
            Assert.True(File.Exists(packagePath));
            Assert.False(File.Exists(quarantined.QuarantinePhysicalPath));
        }
        finally
        {
            Directory.Delete(fixture.RootPath, recursive: true);
        }
    }

    [Fact]
    public async Task QuarantineAsync_파일이_없으면_이미차단된것으로_표시한다()
    {
        var fixture = CreateFixture();

        try
        {
            var quarantined = await fixture.Service.QuarantineAsync(
                "pccam/stable/1.0.0/public-id/missing.zip");

            Assert.False(quarantined.FileMoved);
            Assert.True(await fixture.Service.RestoreFromQuarantineAsync(quarantined));
        }
        finally
        {
            Directory.Delete(fixture.RootPath, recursive: true);
        }
    }

    private static Fixture CreateFixture()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "poscam-update-lifecycle-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        var options = Options.Create(new UpdateStorageOptions
        {
            RootPath = rootPath,
            PublicBaseUrl = "https://update.poscam.co.kr",
            MaxUploadBytes = 1024 * 1024,
            MaxArchiveEntries = 100,
            MaxExpandedBytes = 1024 * 1024
        });

        return new Fixture(
            rootPath,
            new ArtifactStorageService(
                options,
                new ZipPackageValidator(),
                NullLogger<ArtifactStorageService>.Instance));
    }

    private static string WritePackage(
        string rootPath,
        string storageKey,
        byte[] bytes)
    {
        var path = Path.Combine(
            rootPath,
            "packages",
            storageKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
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

    private sealed record Fixture(
        string RootPath,
        ArtifactStorageService Service);
}
