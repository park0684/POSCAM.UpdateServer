using System.IO.Compression;
using System.Text;
using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Tests.Storage;

public class ZipPackageValidatorTests
{
    [Fact]
    public async Task ValidateAsync_정상ZIP을_허용한다()
    {
        var path = CreateZip(("app.exe", "binary"), ("config/settings.json", "{}"));

        try
        {
            await new ZipPackageValidator().ValidateAsync(path, 10, 1024);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidateAsync_손상ZIP을_거부한다()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "not-a-zip");

        try
        {
            var exception = await Assert.ThrowsAsync<ArtifactStorageException>(
                () => new ZipPackageValidator().ValidateAsync(path, 10, 1024));

            Assert.Equal(ArtifactStorageFailureType.InvalidPackage, exception.FailureType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("folder/../../outside.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/windows.txt")]
    [InlineData("C:\\windows.txt")]
    [InlineData("\\\\server\\share\\file.txt")]
    [InlineData("safe/file.txt:stream")]
    public async Task ValidateAsync_위험한_Entry경로를_거부한다(string entryName)
    {
        var path = CreateZip((entryName, "danger"));

        try
        {
            var exception = await Assert.ThrowsAsync<ArtifactStorageException>(
                () => new ZipPackageValidator().ValidateAsync(path, 10, 1024));

            Assert.Equal(ArtifactStorageFailureType.InvalidPackage, exception.FailureType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidateAsync_Entry수_제한을_검사한다()
    {
        var path = CreateZip(("one.txt", "1"), ("two.txt", "2"));

        try
        {
            await Assert.ThrowsAsync<ArtifactStorageException>(
                () => new ZipPackageValidator().ValidateAsync(path, 1, 1024));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidateAsync_ExpandedBytes_제한을_검사한다()
    {
        var path = CreateZip(("large.txt", new string('x', 100)));

        try
        {
            await Assert.ThrowsAsync<ArtifactStorageException>(
                () => new ZipPackageValidator().ValidateAsync(path, 10, 50));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidateAsync_디렉터리만_있는ZIP을_거부한다()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");

        using (var file = File.Create(path))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            archive.CreateEntry("folder/");
        }

        try
        {
            await Assert.ThrowsAsync<ArtifactStorageException>(
                () => new ZipPackageValidator().ValidateAsync(path, 10, 1024));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("safe/file.txt", false)]
    [InlineData("safe\\file.txt", false)]
    [InlineData("../file.txt", true)]
    [InlineData("/file.txt", true)]
    [InlineData("D:/file.txt", true)]
    [InlineData("safe/file.txt:stream", true)]
    public void IsUnsafeEntryPath_경로정책을_판정한다(
        string path,
        bool expected)
    {
        Assert.Equal(expected, ZipPackageValidator.IsUnsafeEntryPath(path));
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
            using var writer = new StreamWriter(
                stream,
                Encoding.UTF8,
                bufferSize: 1024,
                leaveOpen: false);
            writer.Write(item.Content);
        }

        return path;
    }
}
