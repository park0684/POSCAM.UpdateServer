using System.Data;
using System.Text.Json;
using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Lifecycle;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Api.Services;

public sealed partial class ReleaseLifecycleService
{
    private static readonly JsonSerializerOptions LifecycleAuditJsonOptions =
        new(JsonSerializerDefaults.Web);

    private async Task CreateReleaseAuditAsync(
        string action,
        UpdateRelease before,
        UpdateRelease after,
        UpdateManagementActor actor,
        IDbTransaction transaction,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        await _auditLogRepository.CreateAsync(
            CreateAuditLog(
                action,
                AuditTargetTypes.Release,
                after.ReleaseCode,
                actor,
                JsonSerializer.Serialize(
                    CreateReleaseSnapshot(before),
                    LifecycleAuditJsonOptions),
                JsonSerializer.Serialize(
                    new
                    {
                        release = CreateReleaseSnapshot(after),
                        reason
                    },
                    LifecycleAuditJsonOptions)),
            transaction,
            cancellationToken);
    }

    private async Task CreateArtifactQuarantineAuditAsync(
        UpdateArtifact before,
        UpdateArtifact after,
        QuarantinedArtifactFile quarantinedFile,
        UpdateManagementActor actor,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await _auditLogRepository.CreateAsync(
            CreateAuditLog(
                AuditActions.Disable,
                AuditTargetTypes.Artifact,
                after.ArtifactCode,
                actor,
                JsonSerializer.Serialize(
                    CreateArtifactSnapshot(before),
                    LifecycleAuditJsonOptions),
                JsonSerializer.Serialize(
                    new
                    {
                        artifact = CreateArtifactSnapshot(after),
                        reason = "EMERGENCY_QUARANTINE",
                        fileMoved = quarantinedFile.FileMoved,
                        storageState = quarantinedFile.FileMoved
                            ? "QUARANTINED"
                            : "MISSING"
                    },
                    LifecycleAuditJsonOptions)),
            transaction,
            cancellationToken);
    }

    private UpdateAuditLog CreateAuditLog(
        string action,
        string targetType,
        long targetCode,
        UpdateManagementActor actor,
        string? beforeData,
        string? afterData)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return new UpdateAuditLog
        {
            Action = action,
            TargetType = targetType,
            TargetCode = targetCode.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ActorUserCode = actor.UserCode,
            ActorUserName = Truncate(actor.UserName, 100),
            BeforeData = beforeData,
            AfterData = afterData,
            IpAddress = Truncate(
                httpContext?.Connection.RemoteIpAddress?.ToString(),
                45),
            UserAgent = Truncate(
                httpContext?.Request.Headers["User-Agent"].ToString(),
                500),
            RequestId = Truncate(httpContext?.TraceIdentifier, 100)
        };
    }

    private static object CreateReleaseSnapshot(UpdateRelease release)
    {
        return new
        {
            release.ReleaseCode,
            release.ProductCode,
            release.Version,
            release.Channel,
            release.IsMandatory,
            release.ForceUpdateBelowVersion,
            release.ReleaseNotes,
            release.InternalMemo,
            Status = (int)release.ReleaseStatus,
            release.PublishedAt,
            release.CreatedByUserCode,
            release.CreatedByUserName,
            release.CreatedAt,
            release.UpdatedAt
        };
    }

    private static object CreateArtifactSnapshot(UpdateArtifact artifact)
    {
        return new
        {
            artifact.ArtifactCode,
            artifact.ReleaseCode,
            artifact.PublicId,
            Os = artifact.OperatingSystem,
            artifact.Architecture,
            artifact.PackageType,
            artifact.FileName,
            artifact.StorageKey,
            artifact.ContentType,
            artifact.FileSize,
            artifact.Sha256,
            Status = (int)artifact.ArtifactStatus,
            artifact.CreatedAt,
            artifact.UpdatedAt
        };
    }

    private static ReleaseLifecycleResponse MapRelease(UpdateRelease release)
    {
        return new ReleaseLifecycleResponse
        {
            ReleaseCode = release.ReleaseCode,
            Status = (int)release.ReleaseStatus,
            StatusName = release.ReleaseStatus.ToString(),
            PublishedAt = AsUtc(release.PublishedAt)
        };
    }

    private AdminServiceResult<T>? RequireActor<T>(
        out UpdateManagementActor? actor)
    {
        actor = _actorAccessor.Actor;

        return actor is null
            ? AdminServiceResult<T>.Fail(
                StatusCodes.Status503ServiceUnavailable,
                UpdateErrorCode.ExternalServiceUnavailable,
                "관리자 작업자 정보를 확인할 수 없습니다.")
            : null;
    }

    private static AdminServiceResult<T> ReleaseNotFound<T>()
    {
        return AdminServiceResult<T>.Fail(
            StatusCodes.Status404NotFound,
            UpdateErrorCode.ReleaseNotFound,
            "릴리스를 찾을 수 없습니다.");
    }

    private static AdminServiceResult<QuarantineArtifactResponse> ArtifactNotFound()
    {
        return AdminServiceResult<QuarantineArtifactResponse>.Fail(
            StatusCodes.Status404NotFound,
            UpdateErrorCode.ArtifactNotFound,
            "Artifact를 찾을 수 없습니다.");
    }

    private static AdminServiceResult<T> InvalidReleaseState<T>(string message)
    {
        return AdminServiceResult<T>.Fail(
            StatusCodes.Status409Conflict,
            UpdateErrorCode.InvalidReleaseState,
            message);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static DateTime? AsUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind == DateTimeKind.Utc
            ? value.Value
            : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
    }

    private static async Task SafeRollbackAsync(
        System.Data.Common.DbTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // 원래 예외를 보존한다.
        }
    }
}
