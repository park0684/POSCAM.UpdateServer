using System.Net;
using Microsoft.AspNetCore.Http;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Services;
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Services;

public class ReleaseManagementServiceTests
{
    [Fact]
    public async Task GetActiveProductsAsync_활성제품을_DTO로_반환한다()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.GetActiveProductsAsync();

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(ProductCodes.Pccam, result.Data![0].ProductCode);
    }

    [Fact]
    public async Task GetReleasesAsync_필터와_페이징을_적용한다()
    {
        var fixture = CreateFixture();
        fixture.QueryRepository.PagedReleases = new[]
        {
            CreateRelease(11, ReleaseStatus.Published, "2.0.0")
        };
        fixture.QueryRepository.TotalCount = 21;

        var result = await fixture.Service.GetReleasesAsync(
            new ReleaseListRequest
            {
                ProductCode = " PCCAM ",
                Channel = " stable ",
                Status = (int)ReleaseStatus.Published,
                Keyword = " 2.0 ",
                Page = 2,
                PageSize = 10
            });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(2, result.Data.Page);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Equal(21, result.Data.TotalCount);
        Assert.Equal(10, fixture.QueryRepository.LastCriteria!.Offset);
        Assert.Equal(ProductCodes.Pccam, fixture.QueryRepository.LastCriteria.ProductCode);
        Assert.Equal(ReleaseChannels.Stable, fixture.QueryRepository.LastCriteria.Channel);
        Assert.Equal("2.0", fixture.QueryRepository.LastCriteria.Keyword);
    }

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 20, 5)]
    public async Task GetReleasesAsync_잘못된_페이징과_상태를_거부한다(
        int page,
        int pageSize,
        int? status)
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.GetReleasesAsync(
            new ReleaseListRequest
            {
                Page = page,
                PageSize = pageSize,
                Status = status
            });

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status400BadRequest, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.ValidationError, result.ErrorCode);
    }

    [Fact]
    public async Task GetReleaseAsync_릴리스와_Artifact요약을_반환한다()
    {
        var fixture = CreateFixture();
        fixture.ReleaseRepository.ReleaseByCode = CreateRelease(12, ReleaseStatus.Draft, "1.0.0");
        fixture.ArtifactRepository.Artifacts = new[]
        {
            new UpdateArtifact
            {
                ArtifactCode = 30,
                ReleaseCode = 12,
                PublicId = "public-id",
                OperatingSystem = "windows",
                Architecture = "x86",
                PackageType = "full",
                FileName = "package.zip",
                FileSize = 123,
                Sha256 = new string('a', 64),
                ArtifactStatus = ArtifactStatus.Active,
                CreatedAt = DateTime.UtcNow
            }
        };

        var result = await fixture.Service.GetReleaseAsync(12);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Artifacts);
        Assert.Equal("package.zip", result.Data.Artifacts[0].FileName);
    }

    [Fact]
    public async Task CreateDraftAsync_버전을_정규화하고_Actor감사를_기록한다()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.CreateDraftAsync(
            new CreateReleaseRequest
            {
                ProductCode = ProductCodes.Pccam,
                Version = "1.2.0.0",
                Channel = ReleaseChannels.Stable,
                ReleaseNotes = "  공개 메모  ",
                InternalMemo = "  내부 메모  "
            });

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status201Created, result.HttpStatusCode);
        Assert.Equal("1.2.0", result.Data!.Version);
        Assert.Equal("1.2.0", fixture.ReleaseRepository.LastCreatedRelease!.Version);
        Assert.Equal(10, fixture.ReleaseRepository.LastCreatedRelease.CreatedByUserCode);
        Assert.Equal("관리자", fixture.ReleaseRepository.LastCreatedRelease.CreatedByUserName);
        Assert.Equal("공개 메모", fixture.ReleaseRepository.LastCreatedRelease.ReleaseNotes);
        Assert.Equal("내부 메모", fixture.ReleaseRepository.LastCreatedRelease.InternalMemo);

        var audit = Assert.Single(fixture.AuditRepository.CreatedLogs);
        Assert.Equal(AuditActions.Create, audit.Action);
        Assert.Equal(AuditTargetTypes.Release, audit.TargetType);
        Assert.Equal("100", audit.TargetCode);
        Assert.Equal(10, audit.ActorUserCode);
        Assert.Null(audit.BeforeData);
        Assert.Contains("1.2.0", audit.AfterData);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task CreateDraftAsync_중복버전은_409로_Rollback한다()
    {
        var fixture = CreateFixture();
        fixture.ReleaseRepository.DuplicateExists = true;

        var result = await fixture.Service.CreateDraftAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.DuplicateRelease, result.ErrorCode);
        Assert.Null(fixture.ReleaseRepository.LastCreatedRelease);
        Assert.Empty(fixture.AuditRepository.CreatedLogs);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task CreateDraftAsync_전체강제와_기준버전은_동시에_허용하지_않는다()
    {
        var fixture = CreateFixture();
        var request = CreateRequest();
        request.IsMandatory = true;
        request.ForceUpdateBelowVersion = "1.0.0";

        var result = await fixture.Service.CreateDraftAsync(request);

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.ValidationError, result.ErrorCode);
        Assert.Null(fixture.DbContext.Connection.LastTransaction);
    }

    [Fact]
    public async Task UpdateDraftAsync_Published는_409로_차단한다()
    {
        var fixture = CreateFixture();
        fixture.QueryRepository.LockedRelease = CreateRelease(
            20,
            ReleaseStatus.Published,
            "1.0.0");

        var result = await fixture.Service.UpdateDraftAsync(20, CreateUpdateRequest());

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.InvalidReleaseState, result.ErrorCode);
        Assert.Null(fixture.ReleaseRepository.LastUpdatedRelease);
        Assert.Empty(fixture.AuditRepository.CreatedLogs);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task UpdateDraftAsync_Draft를_수정하고_전후감사를_기록한다()
    {
        var fixture = CreateFixture();
        fixture.QueryRepository.LockedRelease = CreateRelease(
            21,
            ReleaseStatus.Draft,
            "1.0.0");
        fixture.ReleaseRepository.ReleaseByCode = fixture.QueryRepository.LockedRelease;

        var request = CreateUpdateRequest();
        request.Version = "1.1.0.0";
        request.ReleaseNotes = "변경";

        var result = await fixture.Service.UpdateDraftAsync(21, request);

        Assert.True(result.Success);
        Assert.Equal("1.1.0", result.Data!.Version);
        Assert.Equal("1.1.0", fixture.ReleaseRepository.LastUpdatedRelease!.Version);

        var audit = Assert.Single(fixture.AuditRepository.CreatedLogs);
        Assert.Equal(AuditActions.Update, audit.Action);
        Assert.Contains("1.0.0", audit.BeforeData);
        Assert.Contains("1.1.0", audit.AfterData);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task DeleteDraftAsync_Draft를_삭제하고_감사를_기록한다()
    {
        var fixture = CreateFixture();
        fixture.QueryRepository.LockedRelease = CreateRelease(
            22,
            ReleaseStatus.Draft,
            "1.0.0");

        var result = await fixture.Service.DeleteDraftAsync(22);

        Assert.True(result.Success);
        Assert.Equal(22, result.Data!.ReleaseCode);
        Assert.Equal(22, fixture.ReleaseRepository.LastDeletedReleaseCode);

        var audit = Assert.Single(fixture.AuditRepository.CreatedLogs);
        Assert.Equal(AuditActions.DeleteDraft, audit.Action);
        Assert.NotNull(audit.BeforeData);
        Assert.Null(audit.AfterData);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task DeleteDraftAsync_Disabled는_409로_차단한다()
    {
        var fixture = CreateFixture();
        fixture.QueryRepository.LockedRelease = CreateRelease(
            23,
            ReleaseStatus.Disabled,
            "1.0.0");

        var result = await fixture.Service.DeleteDraftAsync(23);

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.InvalidReleaseState, result.ErrorCode);
        Assert.Null(fixture.ReleaseRepository.LastDeletedReleaseCode);
        Assert.True(fixture.DbContext.Connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task CreateDraftAsync_Actor가_없으면_503으로_차단한다()
    {
        var fixture = CreateFixture(setActor: false);

        var result = await fixture.Service.CreateDraftAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.ExternalServiceUnavailable, result.ErrorCode);
        Assert.Null(fixture.DbContext.Connection.LastTransaction);
    }

    private static Fixture CreateFixture(bool setActor = true)
    {
        var dbContext = new FakeDbContext();
        var productRepository = new FakeUpdateProductRepository
        {
            Product = new UpdateProduct
            {
                ProductCode = ProductCodes.Pccam,
                ProductName = "PC CAM",
                ProductStatus = ProductStatus.Active,
                CreatedAt = DateTime.UtcNow
            }
        };
        var releaseRepository = new FakeReleaseRepositoryForManagement();
        var queryRepository = new FakeReleaseManagementQueryRepository();
        var artifactRepository = new FakeArtifactRepository();
        var auditRepository = new FakeAuditLogRepository();
        var actorAccessor = new UpdateManagementActorAccessor();

        if (setActor)
        {
            actorAccessor.SetActor(new UpdateManagementActor
            {
                UserCode = 10,
                UserName = "관리자",
                UserRole = 1
            });
        }

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "request-b06"
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        httpContext.Request.Headers["User-Agent"] = "B06-Test";

        var service = new ReleaseManagementService(
            dbContext,
            productRepository,
            releaseRepository,
            queryRepository,
            artifactRepository,
            auditRepository,
            actorAccessor,
            new HttpContextAccessor
            {
                HttpContext = httpContext
            });

        return new Fixture(
            service,
            dbContext,
            releaseRepository,
            queryRepository,
            artifactRepository,
            auditRepository);
    }

    private static CreateReleaseRequest CreateRequest()
    {
        return new CreateReleaseRequest
        {
            ProductCode = ProductCodes.Pccam,
            Version = "1.0.0",
            Channel = ReleaseChannels.Stable
        };
    }

    private static UpdateReleaseRequest CreateUpdateRequest()
    {
        return new UpdateReleaseRequest
        {
            ProductCode = ProductCodes.Pccam,
            Version = "1.1.0",
            Channel = ReleaseChannels.Stable
        };
    }

    private static UpdateRelease CreateRelease(
        long releaseCode,
        ReleaseStatus status,
        string version)
    {
        Assert.True(UpdateVersion.TryParse(version, out var parsed, out _));

        return new UpdateRelease
        {
            ReleaseCode = releaseCode,
            ProductCode = ProductCodes.Pccam,
            Version = parsed.ToString(),
            VersionMajor = parsed.Major,
            VersionMinor = parsed.Minor,
            VersionPatch = parsed.Patch,
            VersionRevision = parsed.Revision,
            Channel = ReleaseChannels.Stable,
            ReleaseStatus = status,
            CreatedByUserCode = 10,
            CreatedByUserName = "관리자",
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed record Fixture(
        ReleaseManagementService Service,
        FakeDbContext DbContext,
        FakeReleaseRepositoryForManagement ReleaseRepository,
        FakeReleaseManagementQueryRepository QueryRepository,
        FakeArtifactRepository ArtifactRepository,
        FakeAuditLogRepository AuditRepository);
}
