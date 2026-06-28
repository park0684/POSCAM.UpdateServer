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

public class UpdateCheckServiceTests
{
    [Fact]
    public async Task CheckAsync_요청본문이_없으면_ValidationError다()
    {
        var service = CreateService();

        var result = await service.CheckAsync(null);

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.ValidationError, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData("product", "pccam", UpdateErrorCode.InvalidProduct)]
    [InlineData("version", "1.0", UpdateErrorCode.InvalidVersion)]
    [InlineData("os", "linux", UpdateErrorCode.InvalidOperatingSystem)]
    [InlineData("architecture", "any", UpdateErrorCode.InvalidArchitecture)]
    [InlineData("channel", "Stable", UpdateErrorCode.InvalidChannel)]
    public async Task CheckAsync_잘못된_입력을_명시적_오류로_거부한다(
        string field,
        string invalidValue,
        UpdateErrorCode expectedError)
    {
        var request = CreateValidRequest();

        switch (field)
        {
            case "product":
                request.ProductCode = invalidValue;
                break;
            case "version":
                request.CurrentVersion = invalidValue;
                break;
            case "os":
                request.Os = invalidValue;
                break;
            case "architecture":
                request.Architecture = invalidValue;
                break;
            case "channel":
                request.Channel = invalidValue;
                break;
            default:
                throw new InvalidOperationException(field);
        }

        var service = CreateService();

        var result = await service.CheckAsync(request);

        Assert.False(result.Success);
        Assert.Equal(expectedError, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CheckAsync_등록되지_않은_제품은_InvalidProduct다()
    {
        var productRepository = new FakeUpdateProductRepository
        {
            Product = null
        };

        var service = CreateService(productRepository: productRepository);

        var result = await service.CheckAsync(CreateValidRequest());

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.InvalidProduct, result.ErrorCode);
    }

    [Fact]
    public async Task CheckAsync_Disabled_제품은_ProductInactive다()
    {
        var productRepository = new FakeUpdateProductRepository
        {
            Product = CreateProduct(ProductStatus.Disabled)
        };

        var service = CreateService(productRepository: productRepository);

        var result = await service.CheckAsync(CreateValidRequest());

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.ProductInactive, result.ErrorCode);
    }

    [Fact]
    public async Task CheckAsync_Published_릴리스가_없으면_정상_NO_AVAILABLE_RELEASE다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = null,
            HasPublishedRelease = false
        };

        var service = CreateService(releaseRepository: releaseRepository);
        var request = CreateValidRequest(currentVersion: "1.0.0.0");

        var result = await service.CheckAsync(request);

        Assert.True(result.Success);
        Assert.Equal(UpdateErrorCode.None, result.ErrorCode);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.UpdateAvailable);
        Assert.False(result.Data.Mandatory);
        Assert.Equal("NO_AVAILABLE_RELEASE", result.Data.ReasonCode);
        Assert.Equal("1.0.0", result.Data.CurrentVersion);
        Assert.Null(result.Data.LatestVersion);
        Assert.Null(result.Data.PackageUrl);
        Assert.Null(result.Data.FileSize);
    }

    [Fact]
    public async Task CheckAsync_Published는_있지만_호환Artifact가_없으면_NO_COMPATIBLE_ARTIFACT다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = null,
            HasPublishedRelease = true
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(CreateValidRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.UpdateAvailable);
        Assert.Equal("NO_COMPATIBLE_ARTIFACT", result.Data.ReasonCode);
        Assert.Null(result.Data.PackageUrl);
    }

    [Fact]
    public async Task CheckAsync_하위버전이면_UPDATE_AVAILABLE과_패키지정보를_반환한다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = CreateCompatibleRelease()
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(
            CreateValidRequest(currentVersion: "1.9.0"));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.UpdateAvailable);
        Assert.False(result.Data.Mandatory);
        Assert.Equal("UPDATE_AVAILABLE", result.Data.ReasonCode);
        Assert.Equal("2.0.0", result.Data.LatestVersion);
        Assert.Equal("full", result.Data.PackageType);
        Assert.Equal("PCCAM_2.0.0_x86.zip", result.Data.FileName);
        Assert.Equal(123456L, result.Data.FileSize);
        Assert.Equal(new string('a', 64), result.Data.Sha256);
        Assert.Equal(
            "https://update.poscam.co.kr/packages/pccam/stable/2.0.0/publicid/PCCAM_2.0.0_x86.zip",
            result.Data.PackageUrl);
        Assert.Equal(DateTimeKind.Utc, result.Data.PublishedAt?.Kind);
    }

    [Fact]
    public async Task CheckAsync_Mandatory_릴리스는_MANDATORY_RELEASE다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = CreateCompatibleRelease(isMandatory: true)
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(
            CreateValidRequest(currentVersion: "1.9.0"));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.UpdateAvailable);
        Assert.True(result.Data.Mandatory);
        Assert.Equal("MANDATORY_RELEASE", result.Data.ReasonCode);
    }

    [Fact]
    public async Task CheckAsync_강제기준미만이면_FORCE_UPDATE_BELOW_VERSION이다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = CreateCompatibleRelease(
                forceUpdateBelowVersion: "1.5.0")
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(
            CreateValidRequest(currentVersion: "1.4.9"));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Mandatory);
        Assert.Equal("FORCE_UPDATE_BELOW_VERSION", result.Data.ReasonCode);
        Assert.Equal("1.5.0", result.Data.ForceUpdateBelowVersion);
    }

    [Fact]
    public async Task CheckAsync_강제기준과_같으면_일반_UPDATE_AVAILABLE이다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = CreateCompatibleRelease(
                forceUpdateBelowVersion: "1.5.0")
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(
            CreateValidRequest(currentVersion: "1.5.0"));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.UpdateAvailable);
        Assert.False(result.Data.Mandatory);
        Assert.Equal("UPDATE_AVAILABLE", result.Data.ReasonCode);
    }

    [Fact]
    public async Task CheckAsync_현재버전과_최신버전이_같으면_ALREADY_LATEST다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = CreateCompatibleRelease()
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(
            CreateValidRequest(currentVersion: "2.0.0.0"));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.UpdateAvailable);
        Assert.False(result.Data.Mandatory);
        Assert.Equal("ALREADY_LATEST", result.Data.ReasonCode);
    }

    [Fact]
    public async Task CheckAsync_현재버전이_서버보다_높으면_CLIENT_VERSION_AHEAD다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = CreateCompatibleRelease(isMandatory: true)
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(
            CreateValidRequest(currentVersion: "2.0.1"));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.UpdateAvailable);
        Assert.False(result.Data.Mandatory);
        Assert.Equal("CLIENT_VERSION_AHEAD", result.Data.ReasonCode);
    }

    [Fact]
    public async Task CheckAsync_any_Artifact를_선택하면_응답Architecture도_any다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = CreateCompatibleRelease(architecture: "any")
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(CreateValidRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("any", result.Data.Architecture);
        Assert.Equal("x86", releaseRepository.LastArchitecture);
        Assert.Equal("full", releaseRepository.LastPackageType);
    }

    [Fact]
    public async Task CheckAsync_StorageKey의_공백을_URL인코딩한다()
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = CreateCompatibleRelease(
                storageKey: "pccam/stable/2.0.0/publicid/PCCAM 2.0.0.zip")
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(CreateValidRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(
            "https://update.poscam.co.kr/packages/pccam/stable/2.0.0/publicid/PCCAM%202.0.0.zip",
            result.Data.PackageUrl);
    }

    [Theory]
    [InlineData(70000, 0, 0, 0, null, "pccam/stable/2.0.0/publicid/file.zip")]
    [InlineData(2, 0, 0, 0, "invalid", "pccam/stable/2.0.0/publicid/file.zip")]
    [InlineData(2, 0, 0, 0, null, "../outside/file.zip")]
    public async Task CheckAsync_저장된_릴리스데이터가_잘못되면_DatabaseError다(
        int major,
        int minor,
        int patch,
        int revision,
        string? forceUpdateBelowVersion,
        string storageKey)
    {
        var releaseRepository = new FakeUpdateReleaseRepository
        {
            CompatibleRelease = CreateCompatibleRelease(
                major: major,
                minor: minor,
                patch: patch,
                revision: revision,
                forceUpdateBelowVersion: forceUpdateBelowVersion,
                storageKey: storageKey)
        };

        var service = CreateService(releaseRepository: releaseRepository);

        var result = await service.CheckAsync(CreateValidRequest());

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.DatabaseError, result.ErrorCode);
        Assert.Null(result.Data);
    }

    private static UpdateCheckService CreateService(
        FakeUpdateProductRepository? productRepository = null,
        FakeUpdateReleaseRepository? releaseRepository = null)
    {
        productRepository ??= new FakeUpdateProductRepository
        {
            Product = CreateProduct(ProductStatus.Active)
        };

        releaseRepository ??= new FakeUpdateReleaseRepository
        {
            CompatibleRelease = null,
            HasPublishedRelease = false
        };

        var options = Options.Create(
            new UpdateStorageOptions
            {
                RootPath = "/app/update-storage",
                PublicBaseUrl = "https://update.poscam.co.kr",
                MaxUploadBytes = 1_073_741_824,
                MaxArchiveEntries = 10_000,
                MaxExpandedBytes = 4_294_967_296
            });

        return new UpdateCheckService(
            productRepository,
            releaseRepository,
            options);
    }

    private static UpdateCheckRequest CreateValidRequest(
        string currentVersion = "1.0.0")
    {
        return new UpdateCheckRequest
        {
            ProductCode = ProductCodes.Pccam,
            CurrentVersion = currentVersion,
            Os = UpdateOperatingSystems.Windows,
            Architecture = ArtifactArchitectures.X86,
            Channel = ReleaseChannels.Stable
        };
    }

    private static UpdateProduct CreateProduct(ProductStatus status)
    {
        return new UpdateProduct
        {
            ProductCode = ProductCodes.Pccam,
            ProductName = "PC CAM",
            ProductStatus = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static CompatibleReleaseArtifact CreateCompatibleRelease(
        int major = 2,
        int minor = 0,
        int patch = 0,
        int revision = 0,
        bool isMandatory = false,
        string? forceUpdateBelowVersion = null,
        string architecture = "x86",
        string storageKey = "pccam/stable/2.0.0/publicid/PCCAM_2.0.0_x86.zip")
    {
        return new CompatibleReleaseArtifact
        {
            ReleaseCode = 10,
            ProductCode = ProductCodes.Pccam,
            Version = "2.0.0",
            VersionMajor = major,
            VersionMinor = minor,
            VersionPatch = patch,
            VersionRevision = revision,
            Channel = ReleaseChannels.Stable,
            ForceUpdateBelowVersion = forceUpdateBelowVersion,
            IsMandatory = isMandatory,
            ReleaseNotes = "안정성 개선",
            PublishedAt = new DateTime(2026, 6, 28, 10, 0, 0),
            ArtifactCode = 20,
            PublicId = "publicid",
            OperatingSystem = UpdateOperatingSystems.Windows,
            Architecture = architecture,
            PackageType = PackageTypes.Full,
            FileName = "PCCAM_2.0.0_x86.zip",
            StorageKey = storageKey,
            ContentType = "application/zip",
            FileSize = 123456,
            Sha256 = new string('a', 64)
        };
    }
}
