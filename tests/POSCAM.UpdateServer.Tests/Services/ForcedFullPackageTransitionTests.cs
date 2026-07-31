using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Updates;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Models.Queries;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Services;
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Services;

public sealed class ForcedFullPackageTransitionTests
{
    [Fact]
    public async Task CheckAsync_ConfiguredRelease_HidesManifestAndKeepsPackage()
    {
        var service = CreateService(
            new[] { "PCCAM_X64:3.2.2" });

        var result = await service.CheckAsync(
            CreateRequest("3.2.1.0"));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.UpdateAvailable);
        Assert.Equal("3.2.2", result.Data.LatestVersion);
        Assert.Equal("PCCAM_3.2.2_x64.zip", result.Data.FileName);
        Assert.NotNull(result.Data.PackageUrl);
        Assert.Empty(result.Data.Files);
    }

    [Fact]
    public async Task CheckAsync_ConfiguredReleaseAtSameVersion_HidesManifestWithoutUpdateLoop()
    {
        var service = CreateService(
            new[] { " pccam_x64:3.2.2 " });

        var result = await service.CheckAsync(
            CreateRequest("3.2.2.0"));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.UpdateAvailable);
        Assert.Equal("ALREADY_LATEST", result.Data.ReasonCode);
        Assert.Equal("3.2.2", result.Data.LatestVersion);
        Assert.Empty(result.Data.Files);
    }

    [Fact]
    public async Task CheckAsync_UnconfiguredRelease_ReturnsManifest()
    {
        var service = CreateService(
            new[] { "PCCAM_X64:3.2.3" });

        var result = await service.CheckAsync(
            CreateRequest("3.2.1.0"));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.UpdateAvailable);

        var file = Assert.Single(result.Data.Files);
        Assert.Equal("POSCAM.UpdateClient.exe", file.Path);
    }

    [Theory]
    [InlineData("PCCAM_X64", "3.2.2", true)]
    [InlineData("pccam_x64", "3.2.2", true)]
    [InlineData("PCCAM_X64", "3.2.3", false)]
    [InlineData("PCCAM_X86", "3.2.2", false)]
    public void ShouldForceFullPackage_MatchesExactProductAndVersion(
        string productCode,
        string version,
        bool expected)
    {
        var options = new UpdateStorageOptions
        {
            ForceFullPackageReleaseKeys =
                new[] { "PCCAM_X64:3.2.2" }
        };

        Assert.Equal(
            expected,
            options.ShouldForceFullPackage(productCode, version));
    }

    private static UpdateCheckService CreateService(
        string[] forcedReleaseKeys)
    {
        const long artifactCode = 322;

        var productRepository = new FakeUpdateProductRepository
        {
            Product = new UpdateProduct
            {
                ProductCode = ProductCodes.PccamX64,
                ProductName = "PC CAM x64",
                ProductStatus = ProductStatus.Active,
                CreatedAt = DateTime.UtcNow
            }
        };

        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = new CompatibleReleaseArtifact
            {
                ReleaseCode = 322,
                ProductCode = ProductCodes.PccamX64,
                Version = "3.2.2",
                VersionMajor = 3,
                VersionMinor = 2,
                VersionPatch = 2,
                VersionRevision = 0,
                Channel = ReleaseChannels.Stable,
                IsMandatory = false,
                ReleaseNotes = "UpdateClient recovery transition",
                PublishedAt = DateTime.UtcNow,
                ArtifactCode = artifactCode,
                PublicId = "pccam-x64-322",
                OperatingSystem = UpdateOperatingSystems.Windows,
                Architecture = ArtifactArchitectures.X64,
                PackageType = PackageTypes.Full,
                FileName = "PCCAM_3.2.2_x64.zip",
                StorageKey =
                    "pccam_x64/stable/3.2.2/publicid/PCCAM_3.2.2_x64.zip",
                ContentType = "application/zip",
                FileSize = 123456,
                Sha256 = new string('a', 64)
            }
        };

        var artifactFileRepository = new FakeArtifactFileRepository
        {
            Files = new[]
            {
                new UpdateArtifactFile
                {
                    FileCode = 1,
                    ArtifactCode = artifactCode,
                    PublicId = "update-client-file",
                    FilePath = "POSCAM.UpdateClient.exe",
                    FileSize = 117248,
                    Sha256 = new string('b', 64),
                    StorageKey =
                        "pccam_x64/stable/3.2.2/publicid/files/update-client-file",
                    DownloadPath = "/packages/update-client-file",
                    IsRequired = true,
                    FileStatus = ArtifactFileStatus.Active,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        var options = Options.Create(
            new UpdateStorageOptions
            {
                RootPath = "/app/update-storage",
                PublicBaseUrl = "https://update.poscam.co.kr",
                MaxUploadBytes = 1_073_741_824,
                MaxArchiveEntries = 10_000,
                MaxExpandedBytes = 4_294_967_296,
                ForceFullPackageReleaseKeys = forcedReleaseKeys
            });

        return new UpdateCheckService(
            productRepository,
            releaseRepository,
            artifactFileRepository,
            options);
    }

    private static UpdateCheckRequest CreateRequest(
        string currentVersion)
    {
        return new UpdateCheckRequest
        {
            ProductCode = ProductCodes.PccamX64,
            CurrentVersion = currentVersion,
            Os = UpdateOperatingSystems.Windows,
            Architecture = ArtifactArchitectures.X64,
            Channel = ReleaseChannels.Stable
        };
    }
}
