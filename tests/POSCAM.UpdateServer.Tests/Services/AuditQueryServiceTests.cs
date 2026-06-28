using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Audits;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Services;
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Services;

public class AuditQueryServiceTests
{
    [Fact]
    public async Task GetAuditLogsAsync_필터를_정규화하고_페이징한다()
    {
        var repository = new FakeAuditManagementQueryRepository
        {
            Logs = new[] { CreateAuditLog() },
            TotalCount = 51
        };
        var service = CreateService(repository, releaseExists: true);

        var result = await service.GetAuditLogsAsync(
            new AuditListRequest
            {
                Action = " publish ",
                TargetType = " release ",
                TargetCode = " 10 ",
                ActorUserCode = 7,
                RequestId = " request-1 ",
                FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ToUtc = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                Page = 2,
                PageSize = 25
            });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Equal(51, result.Data.TotalCount);

        var criteria = repository.LastCriteria!;
        Assert.Equal(AuditActions.Publish, criteria.Action);
        Assert.Equal(AuditTargetTypes.Release, criteria.TargetType);
        Assert.Equal("10", criteria.TargetCode);
        Assert.Equal(7, criteria.ActorUserCode);
        Assert.Equal("request-1", criteria.RequestId);
        Assert.Equal(25, criteria.Offset);
        Assert.Equal(25, criteria.PageSize);
    }

    [Theory]
    [InlineData("UNKNOWN", null)]
    [InlineData(null, "UNKNOWN")]
    public async Task GetAuditLogsAsync_잘못된_작업과_대상유형을_거부한다(
        string? action,
        string? targetType)
    {
        var service = CreateService(
            new FakeAuditManagementQueryRepository(),
            releaseExists: true);

        var result = await service.GetAuditLogsAsync(
            new AuditListRequest
            {
                Action = action,
                TargetType = targetType
            });

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status400BadRequest, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.ValidationError, result.ErrorCode);
    }

    [Fact]
    public async Task GetAuditLogsAsync_시작시각이_종료시각보다_늦으면_거부한다()
    {
        var service = CreateService(
            new FakeAuditManagementQueryRepository(),
            releaseExists: true);

        var result = await service.GetAuditLogsAsync(
            new AuditListRequest
            {
                FromUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                ToUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.ValidationError, result.ErrorCode);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetAuditLogsAsync_잘못된_페이징을_거부한다(
        int page,
        int pageSize)
    {
        var service = CreateService(
            new FakeAuditManagementQueryRepository(),
            releaseExists: true);

        var result = await service.GetAuditLogsAsync(
            new AuditListRequest
            {
                Page = page,
                PageSize = pageSize
            });

        Assert.False(result.Success);
        Assert.Equal(UpdateErrorCode.ValidationError, result.ErrorCode);
    }

    [Fact]
    public async Task GetReleaseHistoryAsync_릴리스와_Artifact이력을_조회할_조건을_전달한다()
    {
        var repository = new FakeAuditManagementQueryRepository
        {
            Logs = new[] { CreateAuditLog() },
            TotalCount = 12
        };
        var service = CreateService(repository, releaseExists: true);

        var result = await service.GetReleaseHistoryAsync(
            10,
            new ReleaseAuditListRequest
            {
                Action = " disable ",
                Page = 2,
                PageSize = 5
            });

        Assert.True(result.Success);
        Assert.Equal(10, repository.LastReleaseCode);
        Assert.Equal(AuditActions.Disable, repository.LastReleaseAction);
        Assert.Equal(5, repository.LastOffset);
        Assert.Equal(5, repository.LastPageSize);
        Assert.Equal(3, result.Data!.TotalPages);
    }

    [Fact]
    public async Task GetReleaseHistoryAsync_없는릴리스는_404이다()
    {
        var service = CreateService(
            new FakeAuditManagementQueryRepository(),
            releaseExists: false);

        var result = await service.GetReleaseHistoryAsync(
            10,
            new ReleaseAuditListRequest());

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status404NotFound, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.ReleaseNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetAuditLogsAsync_DB_DATETIME을_UTC로_표시한다()
    {
        var repository = new FakeAuditManagementQueryRepository
        {
            Logs = new[]
            {
                new UpdateAuditLog
                {
                    AuditLogCode = 1,
                    Action = AuditActions.Publish,
                    TargetType = AuditTargetTypes.Release,
                    TargetCode = "10",
                    CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified)
                }
            },
            TotalCount = 1
        };
        var service = CreateService(repository, releaseExists: true);

        var result = await service.GetAuditLogsAsync(new AuditListRequest());

        Assert.Equal(DateTimeKind.Utc, result.Data!.Items[0].CreatedAt.Kind);
    }

    private static AuditQueryService CreateService(
        FakeAuditManagementQueryRepository repository,
        bool releaseExists)
    {
        var releaseRepository = new FakeReleaseRepositoryForManagement
        {
            ReleaseByCode = releaseExists
                ? new UpdateRelease
                {
                    ReleaseCode = 10,
                    ProductCode = ProductCodes.Pccam,
                    Version = "1.0.0",
                    Channel = ReleaseChannels.Stable,
                    ReleaseStatus = ReleaseStatus.Disabled,
                    CreatedAt = DateTime.UtcNow
                }
                : null
        };

        return new AuditQueryService(repository, releaseRepository);
    }

    private static UpdateAuditLog CreateAuditLog()
    {
        return new UpdateAuditLog
        {
            AuditLogCode = 1,
            Action = AuditActions.Publish,
            TargetType = AuditTargetTypes.Release,
            TargetCode = "10",
            ActorUserCode = 7,
            ActorUserName = "관리자",
            BeforeData = "{}",
            AfterData = "{}",
            RequestId = "request-1",
            CreatedAt = DateTime.UtcNow
        };
    }
}
