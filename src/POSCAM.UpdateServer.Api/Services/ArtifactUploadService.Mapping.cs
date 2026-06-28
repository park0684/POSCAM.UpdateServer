using System.Data;
using System.Text.Json;
using POSCAM.UpdateServer.Api.Models.Authorization;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Artifacts;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Api.Services;

public sealed partial class ArtifactUploadService
{
    private static readonly JsonSerializerOptions ArtifactAuditJsonOptions =
        new(JsonSerializerDefaults.Web);

    private static UpdateArtifact CreateArtifact(
        long releaseCode,
        ValidatedArtifactUpload upload,
        ArtifactStorageDestination destination,
        StagedArtifactFile stagedFile,
        UpdateArtifact? existing)
    {
        return new UpdateArtifact
        {
            ArtifactCode = existing?.ArtifactCode ?? 0,
            ReleaseCode = releaseCode,
            PublicId = destination.PublicId,
            OperatingSystem = upload.OperatingSystem,
            Architecture = upload.Architecture,
            PackageType = upload.PackageType,
            FileName = destination.FileName,
            StorageKey = destination.StorageKey,
            ContentType = "application/zip",
            FileSize = stagedFile.FileSize,
            Sha256 = stagedFile.Sha256,
            Signature = null,
            ArtifactStatus = ArtifactStatus.Active,
            CreatedAt = existing?.CreatedAt ?? default,
            UpdatedAt = existing?.UpdatedAt
        };
    }

    private async Task CreateAuditAsync(
        string action,
        UpdateArtifact? before,
        UpdateArtifact after,
        UpdateManagementActor actor,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var auditLog = new UpdateAuditLog
        {
            Action = action,
            TargetType = AuditTargetTypes.Artifact,
            TargetCode = after.ArtifactCode.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ActorUserCode = actor.UserCode,
            ActorUserName = Truncate(actor.UserName, 100),
            BeforeData = before is null
                ? null
                : JsonSerializer.Serialize(CreateSnapshot(before), ArtifactAuditJsonOptions),
            AfterData = JsonSerializer.Serialize(
                CreateSnapshot(after),
                ArtifactAuditJsonOptions),
            IpAddress = Truncate(
                httpContext?.Connection.RemoteIpAddress?.ToString(),
                45),
            UserAgent = Truncate(
                httpContext?.Request.Headers["User-Agent"].ToString(),
                500),
            RequestId = Truncate(httpContext?.TraceIdentifier, 100)
        };

        await _auditLogRepository.CreateAsync(
            auditLog,
            transaction,
            cancellationToken);
    }

    private static ArtifactUploadResponse MapResponse(
        UpdateArtifact artifact,
        bool replaced)
    {
        return new ArtifactUploadResponse
        {
            ArtifactCode = artifact.ArtifactCode,
            ReleaseCode = artifact.ReleaseCode,
            PublicId = artifact.PublicId,
            Os = artifact.OperatingSystem,
            Architecture = artifact.Architecture,
            PackageType = artifact.PackageType,
            FileName = artifact.FileName,
            FileSize = artifact.FileSize,
            Sha256 = artifact.Sha256,
            Replaced = replaced
        };
    }

    private static object CreateSnapshot(UpdateArtifact artifact)
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
            artifact.Signature,
            Status = (int)artifact.ArtifactStatus,
            artifact.CreatedAt,
            artifact.UpdatedAt
        };
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

    private static async Task SafeRollbackAsync(System.Data.Common.DbTransaction transaction)
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
