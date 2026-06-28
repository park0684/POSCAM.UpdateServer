using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Services;
using POSCAM.UpdateServer.Api.Storage;
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Services;

public class ReleaseLifecycleServiceTests
{
    [Fact]
    public async Task PublishAsync_Draft와_정상Artifact를_게시하고_감사를_기록한다()
    {
        var fixture = CreateFixture(ReleaseStatus.Draft);
        fixture.ArtifactRepository.Artifacts = new[] { CreateArtifact() };

        var result = await fixture.Service.PublishAsync(10);

        Assert.True(result.Success);
        Assert.Equal(ReleaseStatus.Published, (ReleaseStatus)result.Data!.Status);
        Assert.NotNull(result.Data.PublishedAt);
        Assert.Equal(10, fixture.ReleaseRepository.LastPublishedReleaseCode);
        Assert.Single(fixture.Storage.ValidatedStorageKeys);
        Assert.Equal(CreateArtifact().StorageKey, fixture.Storage.ValidatedStorageKeys[0]);

        var audit = Assert.Single(fixture.AuditRepository.CreatedLogs);
        Assert.Equal(AuditActions.Publish, audit.Action);
        Assert.Equal(AuditTargetTypes.Release, audit.TargetType);
        Assert.Equal("10", audit.TargetCode);
        Assert.Contains("Draft", audit.BeforeData ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Published", audit.AfterData ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task PublishAsync_활성Artifact가_없으면_409로_차단한다()
    {
        var fixture = CreateFixture(ReleaseStatus.Draft);

        var result = await fixture.Service.PublishAsync(10);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.NoCompatibleArtifact, result.ErrorCode);
        Assert.Null(fixture.ReleaseRepository.LastPublishedReleaseCode);
        Assert.Empty(fixture.AuditRepository.CreatedLogs);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task PublishAsync_SHA불일치는_409_8033으로_차단한다()
    {
        var fixture = CreateFixture(ReleaseStatus.Draft);
        fixture.ArtifactRepository.Artifacts = new[] { CreateArtifact() };
        fixture.Storage.StoredValidationException = new ArtifactStorageException(
            ArtifactStorageFailureType.PackageIntegrityError,
            "sha mismatch");

        var result = await fixture.Service.PublishAsync(10);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.PackageIntegrityError, result.ErrorCode);
        Assert.Null(fixture.ReleaseRepository.LastPublishedReleaseCode);
        Assert.Empty(fixture.AuditRepository.CreatedLogs);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
    }

    [Theory]
    [InlineData(ReleaseStatus.Published)]
    [InlineData(ReleaseStatus.Disabled)]
    public async Task PublishAsync_Draft가_아니면_재게시를_차단한다(
        ReleaseStatus status)
    {
        var fixture = CreateFixture(status);
        fixture.ArtifactRepository.Artifacts = new[] { CreateArtifact() };

        var result = await fixture.Service.PublishAsync(10);

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.InvalidReleaseState, result.ErrorCode);
        Assert.Equal(0, fixture.Storage.StoredValidationCallCount);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task DisableAsync_Published를_Disabled로_전환하되_파일은_건드리지_않는다()
    {
        var fixture = CreateFixture(ReleaseStatus.Published);

        var result = await fixture.Service.DisableAsync(10);

        Assert.True(result.Success);
        Assert.Equal(ReleaseStatus.Disabled, (ReleaseStatus)result.Data!.Status);
        Assert.Equal(10, fixture.ReleaseRepository.LastDisabledReleaseCode);
        Assert.Empty(fixture.Storage.RemovedStorageKeys);
        Assert.Empty(fixture.Storage.QuarantinedStorageKeys);

        var audit = Assert.Single(fixture.AuditRepository.CreatedLogs);
        Assert.Equal(AuditActions.Disable, audit.Action);
        Assert.Equal(AuditTargetTypes.Release, audit.TargetType);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.Committed);
    }

    [Theory]
    [InlineData(ReleaseStatus.Draft)]
    [InlineData(ReleaseStatus.Disabled)]
    public async Task DisableAsync_Published가_아니면_차단한다(
        ReleaseStatus status)
    {
        var fixture = CreateFixture(status);

        var result = await fixture.Service.DisableAsync(10);

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.InvalidReleaseState, result.ErrorCode);
        Assert.Null(fixture.ReleaseRepository.LastDisabledReleaseCode);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task QuarantineArtifactAsync_파일과_Artifact를_격리하고_PublishedRelease를_중지한다()
    {
        var fixture = CreateFixture(ReleaseStatus.Published);
        var artifact = CreateArtifact();
        fixture.ArtifactRepository.Artifacts = new[] { artifact };
        fixture.ArtifactQuery.LockedArtifact = artifact;

        var result = await fixture.Service.QuarantineArtifactAsync(20);

        Assert.True(result.Success);
        Assert.True(result.Data!.FileMoved);
        Assert.Equal(ArtifactStatus.Disabled, (ArtifactStatus)result.Data.ArtifactStatus);
        Assert.Equal(ReleaseStatus.Disabled, (ReleaseStatus)result.Data.ReleaseStatus);
        Assert.Single(fixture.Storage.QuarantinedStorageKeys);
        Assert.Equal(0, fixture.Storage.RestoreCallCount);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.Committed);

        Assert.Equal(2, fixture.AuditRepository.CreatedLogs.Count);
        Assert.Contains(
            fixture.AuditRepository.CreatedLogs,
            x => x.TargetType == AuditTargetTypes.Artifact
                 && x.Action == AuditActions.Disable
                 && (x.AfterData?.Contains("EMERGENCY_QUARANTINE", StringComparison.Ordinal) ?? false));
        Assert.Contains(
            fixture.AuditRepository.CreatedLogs,
            x => x.TargetType == AuditTargetTypes.Release
                 && x.Action == AuditActions.Disable
                 && (x.AfterData?.Contains("EMERGENCY_ARTIFACT_QUARANTINE", StringComparison.Ordinal) ?? false));
    }

    [Fact]
    public async Task QuarantineArtifactAsync_DB상태전이실패시_Rollback하고_파일을_복구한다()
    {
        var fixture = CreateFixture(ReleaseStatus.Published);
        var artifact = CreateArtifact();
        fixture.ArtifactRepository.Artifacts = new[] { artifact };
        fixture.ArtifactQuery.LockedArtifact = artifact;
        fixture.ReleaseRepository.DisableResult = false;

        var result = await fixture.Service.QuarantineArtifactAsync(20);

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.InvalidReleaseState, result.ErrorCode);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
        Assert.Equal(1, fixture.Storage.RestoreCallCount);
        Assert.Empty(fixture.AuditRepository.CreatedLogs);
    }

    [Fact]
    public async Task QuarantineArtifactAsync_파일이_이미_없어도_Disabled로_전환한다()
    {
        var fixture = CreateFixture(ReleaseStatus.Disabled);
        var artifact = CreateArtifact(ArtifactStatus.Disabled);
        fixture.ArtifactRepository.Artifacts = new[] { artifact };
        fixture.ArtifactQuery.LockedArtifact = artifact;
        fixture.Storage.QuarantinedFile = new QuarantinedArtifactFile
        {
            StorageKey = artifact.StorageKey,
            FileMoved = false
        };

        var result = await fixture.Service.QuarantineArtifactAsync(20);

        Assert.True(result.Success);
        Assert.False(result.Data!.FileMoved);
        Assert.Equal(ReleaseStatus.Disabled, (ReleaseStatus)result.Data.ReleaseStatus);
        Assert.Null(fixture.ReleaseRepository.LastDisabledReleaseCode);
        Assert.Single(fixture.AuditRepository.CreatedLogs);
        Assert.Contains("MISSING", fixture.AuditRepository.CreatedLogs[0].AfterData);
    }

    [Fact]
    public async Task QuarantineArtifactAsync_DraftArtifact는_격리하지_않는다()
    {
        var fixture = CreateFixture(ReleaseStatus.Draft);
        var artifact = CreateArtifact();
        fixture.ArtifactRepository.Artifacts = new[] { artifact };
        fixture.ArtifactQuery.LockedArtifact = artifact;

        var result = await fixture.Service.QuarantineArtifactAsync(20);

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.InvalidReleaseState, result.ErrorCode);
        Assert.Empty(fixture.Storage.QuarantinedStorageKeys);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task QuarantineArtifactAsync_Storage오류는_500_8032로_반환한다()
    {
        var fixture = CreateFixture(ReleaseStatus.Published);
        var artifact = CreateArtifact();
        fixture.ArtifactRepository.Artifacts = new[] { artifact };
        fixture.ArtifactQuery.LockedArtifact = artifact;
        fixture.Storage.QuarantineException = new ArtifactStorageException(
            ArtifactStorageFailureType.StorageError,
            "move failed");

        var result = await fixture.Service.QuarantineArtifactAsync(20);

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.StorageError, result.ErrorCode);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
        Assert.Equal(0, fixture.Storage.RestoreCallCount);
    }

    private static Fixture CreateFixture(ReleaseStatus status)
    {
        var release = CreateRelease(status);
        var dbContext = new FakeDbContext();
        var releaseRepository = new FakeReleaseRepositoryForManagement
        {
            ReleaseByCode = release
        };
        var releaseQuery = new FakeReleaseManagementQueryRepository
        {
            LockedRelease = release
        };
        var artifactRepository = new FakeArtifactRepository();
        var artifactQuery = new FakeArtifactManagementQueryRepository();
        var auditRepository = new FakeAuditLogRepository();
        var storage = new FakeArtifactStorageService();
        var actorAccessor = new UpdateManagementActorAccessor();
        actorAccessor.SetActor(new UpdateManagementActor
        {
            UserCode = 10,
            UserName = "관리자",
            UserRole = 1
        });

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "request-b08"
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        httpContext.Request.Headers["User-Agent"] = "B08-Test";

        var service = new ReleaseLifecycleService(
            dbContext,
            releaseRepository,
            releaseQuery,
            artifactRepository,
            artifactQuery,
            auditRepository,
            actorAccessor,
            new HttpContextAccessor { HttpContext = httpContext },
            storage,
            NullLogger<ReleaseLifecycleService>.Instance);

        return new Fixture(
            service,
            dbContext,
            releaseRepository,
            artifactRepository,
            artifactQuery,
            auditRepository,
            storage);
    }

    private static UpdateRelease CreateRelease(ReleaseStatus status)
    {
        return new UpdateRelease
        {
            ReleaseCode = 10,
            ProductCode = ProductCodes.Pccam,
            Version = "1.0.0",
            VersionMajor = 1,
            VersionMinor = 0,
            VersionPatch = 0,
            VersionRevision = 0,
            Channel = ReleaseChannels.Stable,
            ReleaseStatus = status,
            PublishedAt = status is ReleaseStatus.Published or ReleaseStatus.Disabled
                ? DateTime.UtcNow.AddHours(-1)
                : null,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    private static UpdateArtifact CreateArtifact(
        ArtifactStatus status = ArtifactStatus.Active)
    {
        return new UpdateArtifact
        {
            ArtifactCode = 20,
            ReleaseCode = 10,
            PublicId = "public-id",
            OperatingSystem = UpdateOperatingSystems.Windows,
            Architecture = ArtifactArchitectures.X86,
            PackageType = PackageTypes.Full,
            FileName = "PCCAM_1.0.0_x86.zip",
            StorageKey = "pccam/stable/1.0.0/public-id/PCCAM_1.0.0_x86.zip",
            ContentType = "application/zip",
            FileSize = 1234,
            Sha256 = new string('a', 64),
            ArtifactStatus = status,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
    }

    private sealed record Fixture(
        ReleaseLifecycleService Service,
        FakeDbContext DbContext,
        FakeReleaseRepositoryForManagement ReleaseRepository,
        FakeArtifactRepository ArtifactRepository,
        FakeArtifactManagementQueryRepository ArtifactQuery,
        FakeAuditLogRepository AuditRepository,
        FakeArtifactStorageService Storage);
}
