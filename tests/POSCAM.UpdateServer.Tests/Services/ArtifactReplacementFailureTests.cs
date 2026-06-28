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
using POSCAM.UpdateServer.Tests.TestDoubles;

namespace POSCAM.UpdateServer.Tests.Services;

public class ArtifactReplacementFailureTests
{
    [Fact]
    public async Task UploadAsync_교체DB실패시_새파일만_정리하고_기존파일은_유지한다()
    {
        var dbContext = new FakeDbContext();
        var release = CreateRelease();
        var releaseRepository = new FakeReleaseRepositoryForManagement
        {
            ReleaseByCode = release
        };
        var releaseQuery = new FakeReleaseManagementQueryRepository
        {
            LockedRelease = release
        };
        var artifactRepository = new FakeArtifactRepository
        {
            ReplaceException = new UpdateDatabaseException(
                DatabaseFailureType.Duplicate,
                1062,
                "duplicate",
                new Exception("provider"))
        };
        var artifactQuery = new FakeArtifactManagementQueryRepository
        {
            LockedArtifact = CreateExistingArtifact()
        };
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
            releaseQuery,
            artifactRepository,
            artifactQuery,
            auditRepository,
            actorAccessor,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "replace-failure"
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
        Assert.Equal(UpdateErrorCode.DuplicateArtifact, result.ErrorCode);
        Assert.True(dbContext.Connection.LastTransaction!.RolledBack);
        Assert.Empty(auditRepository.CreatedLogs);
        Assert.Single(storage.RemovedStorageKeys);
        Assert.Equal(storage.Destination.StorageKey, storage.RemovedStorageKeys[0]);
        Assert.DoesNotContain(
            "pccam/stable/1.0.0/old-public-id/old.zip",
            storage.RemovedStorageKeys);
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

    private static UpdateRelease CreateRelease()
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
            ReleaseStatus = ReleaseStatus.Draft,
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
}
