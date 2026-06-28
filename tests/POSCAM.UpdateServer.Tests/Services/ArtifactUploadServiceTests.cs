using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Artifacts;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Services;
using POSCAM.UpdateServer.Api.Storage;
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Services;

public class ArtifactUploadServiceTests
{
    [Fact]
    public async Task UploadAsync_신규Artifact를_생성하고_감사와_Commit을_처리한다()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.UploadAsync(10, CreateRequest());

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status201Created, result.HttpStatusCode);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.Replaced);
        Assert.Equal(200, result.Data.ArtifactCode);
        Assert.Equal("new-public-id", result.Data.PublicId);
        Assert.Equal(1234, result.Data.FileSize);
        Assert.Equal(new string('a', 64), result.Data.Sha256);

        var artifact = fixture.ArtifactRepository.LastCreatedArtifact;
        Assert.NotNull(artifact);
        Assert.Equal("application/zip", artifact.ContentType);
        Assert.Equal(ArtifactStatus.Active, artifact.ArtifactStatus);

        var audit = Assert.Single(fixture.AuditRepository.CreatedLogs);
        Assert.Equal(AuditActions.Upload, audit.Action);
        Assert.Equal(AuditTargetTypes.Artifact, audit.TargetType);
        Assert.Equal("200", audit.TargetCode);
        Assert.Equal(10, audit.ActorUserCode);
        Assert.Null(audit.BeforeData);
        Assert.Contains("new-public-id", audit.AfterData);

        Assert.True(fixture.DbContext.Connection.LastTransaction!.Committed);
        Assert.Empty(fixture.Storage.RemovedStorageKeys);
        Assert.Equal(1, fixture.Storage.SaveCallCount);
        Assert.Equal(1, fixture.Storage.ValidateCallCount);
        Assert.Equal(1, fixture.Storage.MoveCallCount);
        Assert.Equal(1, fixture.Storage.DeleteStagingCallCount);
    }

    [Fact]
    public async Task UploadAsync_기존DraftArtifact를_새PublicId로_교체하고_Commit후_이전파일을_정리한다()
    {
        var fixture = CreateFixture();
        fixture.ArtifactQuery.LockedArtifact = CreateExistingArtifact();

        var result = await fixture.Service.UploadAsync(10, CreateRequest());

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.HttpStatusCode);
        Assert.True(result.Data!.Replaced);
        Assert.Equal(300, result.Data.ArtifactCode);
        Assert.Equal("new-public-id", result.Data.PublicId);
        Assert.NotNull(fixture.ArtifactRepository.LastReplacedArtifact);

        var audit = Assert.Single(fixture.AuditRepository.CreatedLogs);
        Assert.Equal(AuditActions.ReplaceDraftArtifact, audit.Action);
        Assert.Contains("old-public-id", audit.BeforeData);
        Assert.Contains("new-public-id", audit.AfterData);

        Assert.True(fixture.DbContext.Connection.LastTransaction!.Committed);
        Assert.Single(fixture.Storage.RemovedStorageKeys);
        Assert.Equal(
            "pccam/stable/1.0.0/old-public-id/old.zip",
            fixture.Storage.RemovedStorageKeys[0]);
    }

    [Theory]
    [InlineData(ReleaseStatus.Published)]
    [InlineData(ReleaseStatus.Disabled)]
    public async Task UploadAsync_Draft가_아니면_파일을_읽기전에_차단한다(
        ReleaseStatus status)
    {
        var fixture = CreateFixture(preliminaryStatus: status);

        var result = await fixture.Service.UploadAsync(10, CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.InvalidReleaseState, result.ErrorCode);
        Assert.Equal(0, fixture.Storage.SaveCallCount);
        Assert.Null(fixture.DbContext.Connection.LastTransaction);
    }

    [Fact]
    public async Task UploadAsync_검증후_상태가_Published로_바뀌면_Rollback하고_새파일을_정리한다()
    {
        var fixture = CreateFixture(lockedStatus: ReleaseStatus.Published);

        var result = await fixture.Service.UploadAsync(10, CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.InvalidReleaseState, result.ErrorCode);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
        Assert.Contains(
            fixture.Storage.Destination.StorageKey,
            fixture.Storage.RemovedStorageKeys);
        Assert.Null(fixture.ArtifactRepository.LastCreatedArtifact);
    }

    [Fact]
    public async Task UploadAsync_손상ZIP은_415로_매핑하고_DB를_열지_않는다()
    {
        var fixture = CreateFixture();
        fixture.Storage.ValidateException = new ArtifactStorageException(
            ArtifactStorageFailureType.InvalidPackage,
            "broken");

        var result = await fixture.Service.UploadAsync(10, CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.InvalidPackage, result.ErrorCode);
        Assert.Null(fixture.DbContext.Connection.LastTransaction);
        Assert.Equal(0, fixture.Storage.MoveCallCount);
        Assert.Equal(1, fixture.Storage.DeleteStagingCallCount);
    }

    [Fact]
    public async Task UploadAsync_DB_Unique충돌은_409로_매핑하고_최종파일을_정리한다()
    {
        var fixture = CreateFixture();
        fixture.ArtifactRepository.CreateException = new UpdateDatabaseException(
            DatabaseFailureType.Duplicate,
            1062,
            "duplicate",
            new Exception("provider"));

        var result = await fixture.Service.UploadAsync(10, CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.DuplicateArtifact, result.ErrorCode);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
        Assert.Contains(
            fixture.Storage.Destination.StorageKey,
            fixture.Storage.RemovedStorageKeys);
        Assert.Empty(fixture.AuditRepository.CreatedLogs);
    }

    [Fact]
    public async Task UploadAsync_표시크기가_한도를_넘으면_413으로_즉시차단한다()
    {
        var fixture = CreateFixture(maxUploadBytes: 5);
        var request = CreateRequest(fileLength: 10);

        var result = await fixture.Service.UploadAsync(10, request);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.FileTooLarge, result.ErrorCode);
        Assert.Equal(0, fixture.Storage.SaveCallCount);
    }

    [Theory]
    [InlineData("linux", "x86", "full", UpdateErrorCode.InvalidOperatingSystem)]
    [InlineData("windows", "arm64", "full", UpdateErrorCode.InvalidArchitecture)]
    [InlineData("windows", "x86", "delta", UpdateErrorCode.ValidationError)]
    public async Task UploadAsync_대상코드를_검증한다(
        string os,
        string architecture,
        string packageType,
        UpdateErrorCode expectedError)
    {
        var fixture = CreateFixture();
        var request = CreateRequest();
        request.Os = os;
        request.Architecture = architecture;
        request.PackageType = packageType;

        var result = await fixture.Service.UploadAsync(10, request);

        Assert.False(result.Success);
        Assert.Equal(expectedError, result.ErrorCode);
        Assert.Equal(0, fixture.Storage.SaveCallCount);
    }

    [Fact]
    public async Task UploadAsync_원본확장자가_ZIP이_아니면_415로_거부한다()
    {
        var fixture = CreateFixture();
        var request = CreateRequest(fileName: "package.exe");

        var result = await fixture.Service.UploadAsync(10, request);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.InvalidPackage, result.ErrorCode);
        Assert.Equal(0, fixture.Storage.SaveCallCount);
    }

    private static Fixture CreateFixture(
        ReleaseStatus preliminaryStatus = ReleaseStatus.Draft,
        ReleaseStatus lockedStatus = ReleaseStatus.Draft,
        long maxUploadBytes = 1024 * 1024)
    {
        var release = CreateRelease(10, preliminaryStatus);
        var lockedRelease = CreateRelease(10, lockedStatus);
        var dbContext = new FakeDbContext();
        var releaseRepository = new FakeReleaseRepositoryForManagement
        {
            ReleaseByCode = release
        };
        var releaseQuery = new FakeReleaseManagementQueryRepository
        {
            LockedRelease = lockedRelease
        };
        var artifactRepository = new FakeArtifactRepository();
        var artifactQuery = new FakeArtifactManagementQueryRepository();
        var auditRepository = new FakeAuditLogRepository();
        var actorAccessor = new UpdateManagementActorAccessor();
        actorAccessor.SetActor(new UpdateManagementActor
        {
            UserCode = 10,
            UserName = "관리자",
            UserRole = 1
        });

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "request-b07"
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        httpContext.Request.Headers["User-Agent"] = "B07-Test";

        var storage = new FakeArtifactStorageService();
        var options = Options.Create(new UpdateStorageOptions
        {
            RootPath = "/test-only",
            PublicBaseUrl = "https://update.poscam.co.kr",
            MaxUploadBytes = maxUploadBytes,
            MaxArchiveEntries = 100,
            MaxExpandedBytes = 1024 * 1024
        });

        var service = new ArtifactUploadService(
            dbContext,
            releaseRepository,
            releaseQuery,
            artifactRepository,
            artifactQuery,
            auditRepository,
            actorAccessor,
            new HttpContextAccessor { HttpContext = httpContext },
            storage,
            options,
            NullLogger<ArtifactUploadService>.Instance);

        return new Fixture(
            service,
            dbContext,
            artifactRepository,
            artifactQuery,
            auditRepository,
            storage);
    }

    private static ArtifactUploadRequest CreateRequest(
        int fileLength = 10,
        string fileName = "package.zip")
    {
        var stream = new MemoryStream(new byte[fileLength]);
        var file = new FormFile(
            stream,
            0,
            fileLength,
            "file",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };

        return new ArtifactUploadRequest
        {
            Os = UpdateOperatingSystems.Windows,
            Architecture = ArtifactArchitectures.X86,
            PackageType = PackageTypes.Full,
            File = file
        };
    }

    private static UpdateRelease CreateRelease(
        long releaseCode,
        ReleaseStatus status)
    {
        return new UpdateRelease
        {
            ReleaseCode = releaseCode,
            ProductCode = ProductCodes.Pccam,
            Version = "1.0.0",
            VersionMajor = 1,
            VersionMinor = 0,
            VersionPatch = 0,
            VersionRevision = 0,
            Channel = ReleaseChannels.Stable,
            ReleaseStatus = status,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static UpdateArtifact CreateExistingArtifact()
    {
        return new UpdateArtifact
        {
            ArtifactCode = 300,
            ReleaseCode = 10,
            PublicId = "old-public-id",
            OperatingSystem = UpdateOperatingSystems.Windows,
            Architecture = ArtifactArchitectures.X86,
            PackageType = PackageTypes.Full,
            FileName = "old.zip",
            StorageKey = "pccam/stable/1.0.0/old-public-id/old.zip",
            ContentType = "application/zip",
            FileSize = 100,
            Sha256 = new string('b', 64),
            ArtifactStatus = ArtifactStatus.Active,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    private sealed record Fixture(
        ArtifactUploadService Service,
        FakeDbContext DbContext,
        FakeArtifactRepository ArtifactRepository,
        FakeArtifactManagementQueryRepository ArtifactQuery,
        FakeAuditLogRepository AuditRepository,
        FakeArtifactStorageService Storage);
}
