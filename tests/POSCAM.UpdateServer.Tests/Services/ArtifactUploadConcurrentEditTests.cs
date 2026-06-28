using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Artifacts;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Services;
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Services;

public class ArtifactUploadConcurrentEditTests
{
    [Fact]
    public async Task UploadAsync_검증중_릴리스버전이_바뀌면_409와_새파일정리를_수행한다()
    {
        var preliminaryRelease = CreateRelease("1.0.0");
        var lockedRelease = CreateRelease("1.1.0");
        var dbContext = new FakeDbContext();
        var releaseRepository = new FakeReleaseRepositoryForManagement
        {
            ReleaseByCode = preliminaryRelease
        };
        var releaseQueryRepository = new FakeReleaseManagementQueryRepository
        {
            LockedRelease = lockedRelease
        };
        var artifactRepository = new FakeArtifactRepository();
        var auditRepository = new FakeAuditLogRepository();
        var storage = new FakeArtifactStorageService();
        var actorAccessor = new UpdateManagementActorAccessor();
        actorAccessor.SetActor(new UpdateManagementActor
        {
            UserCode = 10,
            UserName = "관리자",
            UserRole = 1
        });

        var service = new ArtifactUploadService(
            dbContext,
            releaseRepository,
            releaseQueryRepository,
            artifactRepository,
            new FakeArtifactManagementQueryRepository(),
            auditRepository,
            actorAccessor,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "concurrent-edit"
                }
            },
            storage,
            Options.Create(new UpdateStorageOptions
            {
                RootPath = "/test-only",
                PublicBaseUrl = "https://update.poscam.co.kr",
                MaxUploadBytes = 1024,
                MaxArchiveEntries = 100,
                MaxExpandedBytes = 1024
            }),
            NullLogger<ArtifactUploadService>.Instance);

        var result = await service.UploadAsync(10, CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.HttpStatusCode);
        Assert.Equal(UpdateErrorCode.InvalidReleaseState, result.ErrorCode);
        Assert.True(dbContext.Connection.LastTransaction!.RolledBack);
        Assert.Null(artifactRepository.LastCreatedArtifact);
        Assert.Empty(auditRepository.CreatedLogs);
        Assert.Single(storage.RemovedStorageKeys);
        Assert.Equal(storage.Destination.StorageKey, storage.RemovedStorageKeys[0]);
    }

    private static ArtifactUploadRequest CreateRequest()
    {
        var stream = new MemoryStream(new byte[10]);

        return new ArtifactUploadRequest
        {
            Os = UpdateOperatingSystems.Windows,
            Architecture = ArtifactArchitectures.X86,
            PackageType = PackageTypes.Full,
            File = new FormFile(stream, 0, stream.Length, "file", "package.zip")
            {
                Headers = new HeaderDictionary()
            }
        };
    }

    private static UpdateRelease CreateRelease(string version)
    {
        Assert.True(UpdateVersion.TryParse(version, out var parsedVersion, out _));

        return new UpdateRelease
        {
            ReleaseCode = 10,
            ProductCode = ProductCodes.Pccam,
            Version = parsedVersion.ToString(),
            VersionMajor = parsedVersion.Major,
            VersionMinor = parsedVersion.Minor,
            VersionPatch = parsedVersion.Patch,
            VersionRevision = parsedVersion.Revision,
            Channel = ReleaseChannels.Stable,
            ReleaseStatus = ReleaseStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }
}
