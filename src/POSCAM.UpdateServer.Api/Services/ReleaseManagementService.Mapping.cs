using System.Data;
using System.Text.Json;
using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Releases;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Services;

public sealed partial class ReleaseManagementService
{
    private static readonly JsonSerializerOptions AuditJsonOptions =
        new(JsonSerializerDefaults.Web);

    private async Task CreateAuditAsync(
        string action,
        long releaseCode,
        UpdateRelease? before,
        UpdateRelease? after,
        UpdateManagementActor actor,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var auditLog = new UpdateAuditLog
        {
            Action = action,
            TargetType = AuditTargetTypes.Release,
            TargetCode = releaseCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ActorUserCode = actor.UserCode,
            ActorUserName = Truncate(actor.UserName, 100),
            BeforeData = before is null
                ? null
                : JsonSerializer.Serialize(CreateSnapshot(before), AuditJsonOptions),
            AfterData = after is null
                ? null
                : JsonSerializer.Serialize(CreateSnapshot(after), AuditJsonOptions),
            IpAddress = Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), 45),
            UserAgent = Truncate(httpContext?.Request.Headers["User-Agent"].ToString(), 500),
            RequestId = Truncate(httpContext?.TraceIdentifier, 100)
        };

        await _auditLogRepository.CreateAsync(
            auditLog,
            transaction,
            cancellationToken);
    }

    private static UpdateRelease CreateEntity(
        ValidatedReleaseMutation mutation,
        string? releaseNotes,
        string? internalMemo,
        UpdateManagementActor actor)
    {
        return new UpdateRelease
        {
            ProductCode = mutation.ProductCode,
            Version = mutation.Version.ToString(),
            VersionMajor = mutation.Version.Major,
            VersionMinor = mutation.Version.Minor,
            VersionPatch = mutation.Version.Patch,
            VersionRevision = mutation.Version.Revision,
            Channel = mutation.Channel,
            ForceUpdateBelowVersion = mutation.ForceUpdateBelowVersion?.ToString(),
            IsMandatory = mutation.IsMandatory,
            ReleaseNotes = NormalizeOptionalText(releaseNotes),
            InternalMemo = NormalizeOptionalText(internalMemo),
            ReleaseStatus = ReleaseStatus.Draft,
            CreatedByUserCode = actor.UserCode,
            CreatedByUserName = Truncate(actor.UserName, 100)
        };
    }

    private static UpdateRelease ApplyMutation(
        UpdateRelease current,
        ValidatedReleaseMutation mutation,
        string? releaseNotes,
        string? internalMemo)
    {
        return new UpdateRelease
        {
            ReleaseCode = current.ReleaseCode,
            ProductCode = mutation.ProductCode,
            Version = mutation.Version.ToString(),
            VersionMajor = mutation.Version.Major,
            VersionMinor = mutation.Version.Minor,
            VersionPatch = mutation.Version.Patch,
            VersionRevision = mutation.Version.Revision,
            Channel = mutation.Channel,
            ForceUpdateBelowVersion = mutation.ForceUpdateBelowVersion?.ToString(),
            IsMandatory = mutation.IsMandatory,
            ReleaseNotes = NormalizeOptionalText(releaseNotes),
            InternalMemo = NormalizeOptionalText(internalMemo),
            ReleaseStatus = current.ReleaseStatus,
            PublishedAt = current.PublishedAt,
            CreatedByUserCode = current.CreatedByUserCode,
            CreatedByUserName = current.CreatedByUserName,
            CreatedAt = current.CreatedAt,
            UpdatedAt = current.UpdatedAt
        };
    }

    private static ReleaseListItemResponse MapListItem(UpdateRelease release)
    {
        return new ReleaseListItemResponse
        {
            ReleaseCode = release.ReleaseCode,
            ProductCode = release.ProductCode,
            Version = release.Version,
            Channel = release.Channel,
            IsMandatory = release.IsMandatory,
            ForceUpdateBelowVersion = release.ForceUpdateBelowVersion,
            Status = (int)release.ReleaseStatus,
            StatusName = release.ReleaseStatus.ToString(),
            PublishedAt = AsUtc(release.PublishedAt),
            CreatedByUserCode = release.CreatedByUserCode,
            CreatedByUserName = release.CreatedByUserName,
            CreatedAt = AsUtc(release.CreatedAt),
            UpdatedAt = AsUtc(release.UpdatedAt)
        };
    }

    private static ReleaseDetailResponse MapDetail(
        UpdateRelease release,
        IReadOnlyList<UpdateArtifact> artifacts)
    {
        return new ReleaseDetailResponse
        {
            ReleaseCode = release.ReleaseCode,
            ProductCode = release.ProductCode,
            Version = release.Version,
            Channel = release.Channel,
            IsMandatory = release.IsMandatory,
            ForceUpdateBelowVersion = release.ForceUpdateBelowVersion,
            ReleaseNotes = release.ReleaseNotes,
            InternalMemo = release.InternalMemo,
            Status = (int)release.ReleaseStatus,
            StatusName = release.ReleaseStatus.ToString(),
            PublishedAt = AsUtc(release.PublishedAt),
            CreatedByUserCode = release.CreatedByUserCode,
            CreatedByUserName = release.CreatedByUserName,
            CreatedAt = AsUtc(release.CreatedAt),
            UpdatedAt = AsUtc(release.UpdatedAt),
            Artifacts = artifacts.Select(artifact => new ReleaseArtifactSummaryResponse
            {
                ArtifactCode = artifact.ArtifactCode,
                PublicId = artifact.PublicId,
                Os = artifact.OperatingSystem,
                Architecture = artifact.Architecture,
                PackageType = artifact.PackageType,
                FileName = artifact.FileName,
                FileSize = artifact.FileSize,
                Sha256 = artifact.Sha256,
                Status = (int)artifact.ArtifactStatus,
                CreatedAt = AsUtc(artifact.CreatedAt)
            }).ToArray()
        };
    }

    private static object CreateSnapshot(UpdateRelease release)
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

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static DateTime? AsUtc(DateTime? value)
    {
        return value.HasValue ? AsUtc(value.Value) : null;
    }

    private static async Task SafeRollbackAsync(System.Data.Common.DbTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // 원래 예외를 보존하기 위해 Rollback 실패는 다시 던지지 않는다.
        }
    }
}
