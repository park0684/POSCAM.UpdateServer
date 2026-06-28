using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Admin.Artifacts;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Storage;

namespace POSCAM.UpdateServer.Api.Services;

public sealed partial class ArtifactUploadService
{
    private static AdminServiceResult<ArtifactUploadResponse>? ValidateRequest(
        long releaseCode,
        ArtifactUploadRequest? request,
        long maxUploadBytes,
        out ValidatedArtifactUpload? upload)
    {
        upload = null;

        if (releaseCode <= 0)
        {
            return ReleaseNotFound();
        }

        if (request is null)
        {
            return ValidationFailure("업로드 요청이 필요합니다.");
        }

        var operatingSystem = Normalize(request.Os);
        if (!UpdateOperatingSystems.IsSupported(operatingSystem))
        {
            return AdminServiceResult<ArtifactUploadResponse>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidOperatingSystem,
                "운영체제 코드가 올바르지 않습니다.");
        }

        var architecture = Normalize(request.Architecture);
        if (architecture is not ArtifactArchitectures.X86
            and not ArtifactArchitectures.X64
            and not ArtifactArchitectures.Any)
        {
            return AdminServiceResult<ArtifactUploadResponse>.Fail(
                StatusCodes.Status400BadRequest,
                UpdateErrorCode.InvalidArchitecture,
                "Artifact 아키텍처가 올바르지 않습니다.");
        }

        var packageType = Normalize(request.PackageType);
        if (!string.Equals(
                packageType,
                PackageTypes.Full,
                StringComparison.Ordinal))
        {
            return ValidationFailure("현재는 full 패키지만 등록할 수 있습니다.");
        }

        if (request.File is null)
        {
            return AdminServiceResult<ArtifactUploadResponse>.Fail(
                StatusCodes.Status415UnsupportedMediaType,
                UpdateErrorCode.InvalidPackage,
                "ZIP 패키지 파일이 필요합니다.");
        }

        if (request.File.Length <= 0)
        {
            return AdminServiceResult<ArtifactUploadResponse>.Fail(
                StatusCodes.Status415UnsupportedMediaType,
                UpdateErrorCode.InvalidPackage,
                "빈 파일은 업로드할 수 없습니다.");
        }

        if (request.File.Length > maxUploadBytes)
        {
            return AdminServiceResult<ArtifactUploadResponse>.Fail(
                StatusCodes.Status413PayloadTooLarge,
                UpdateErrorCode.FileTooLarge,
                "업로드 파일 크기 제한을 초과했습니다.");
        }

        var originalFileName = request.File.FileName;
        if (string.IsNullOrWhiteSpace(originalFileName)
            || !originalFileName.EndsWith(
                ".zip",
                StringComparison.OrdinalIgnoreCase))
        {
            return AdminServiceResult<ArtifactUploadResponse>.Fail(
                StatusCodes.Status415UnsupportedMediaType,
                UpdateErrorCode.InvalidPackage,
                "ZIP 파일만 업로드할 수 있습니다.");
        }

        upload = new ValidatedArtifactUpload(
            operatingSystem!,
            architecture!,
            packageType!,
            request.File);

        return null;
    }

    private static AdminServiceResult<ArtifactUploadResponse> MapStorageFailure(
        ArtifactStorageException exception)
    {
        return exception.FailureType switch
        {
            ArtifactStorageFailureType.FileTooLarge =>
                AdminServiceResult<ArtifactUploadResponse>.Fail(
                    StatusCodes.Status413PayloadTooLarge,
                    UpdateErrorCode.FileTooLarge,
                    "업로드 파일 크기 제한을 초과했습니다."),

            ArtifactStorageFailureType.InvalidPackage =>
                AdminServiceResult<ArtifactUploadResponse>.Fail(
                    StatusCodes.Status415UnsupportedMediaType,
                    UpdateErrorCode.InvalidPackage,
                    "유효한 ZIP 패키지가 아닙니다."),

            _ => AdminServiceResult<ArtifactUploadResponse>.Fail(
                StatusCodes.Status500InternalServerError,
                UpdateErrorCode.StorageError,
                "Artifact 파일 저장 중 오류가 발생했습니다.")
        };
    }

    private static AdminServiceResult<ArtifactUploadResponse> ReleaseNotFound()
    {
        return AdminServiceResult<ArtifactUploadResponse>.Fail(
            StatusCodes.Status404NotFound,
            UpdateErrorCode.ReleaseNotFound,
            "릴리스를 찾을 수 없습니다.");
    }

    private static AdminServiceResult<ArtifactUploadResponse> InvalidReleaseState()
    {
        return AdminServiceResult<ArtifactUploadResponse>.Fail(
            StatusCodes.Status409Conflict,
            UpdateErrorCode.InvalidReleaseState,
            "Draft 상태의 릴리스에만 Artifact를 업로드할 수 있습니다.");
    }

    private static AdminServiceResult<ArtifactUploadResponse> ValidationFailure(
        string message)
    {
        return AdminServiceResult<ArtifactUploadResponse>.Fail(
            StatusCodes.Status400BadRequest,
            UpdateErrorCode.ValidationError,
            message);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed record ValidatedArtifactUpload(
        string OperatingSystem,
        string Architecture,
        string PackageType,
        IFormFile File);
}
