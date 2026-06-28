using Microsoft.Extensions.Options;
using POSCAM.UpdateServer.Api.Models.Domain;
using POSCAM.UpdateServer.Api.Models.Dtos.Updates;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;
using POSCAM.UpdateServer.Api.Models.Queries;
using POSCAM.UpdateServer.Api.Options;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Api.Services;

/// <summary>
/// 익명 클라이언트의 현재 버전과 환경을 검증하고,
/// 호환되는 가장 높은 Published Release와 Artifact를 반환한다.
/// AuthServer 또는 AccountToken에는 의존하지 않는다.
/// </summary>
public sealed class UpdateCheckService : IUpdateCheckService
{
    private readonly IUpdateProductRepository _productRepository;
    private readonly IUpdateReleaseRepository _releaseRepository;
    private readonly UpdateStorageOptions _storageOptions;

    public UpdateCheckService(
        IUpdateProductRepository productRepository,
        IUpdateReleaseRepository releaseRepository,
        IOptions<UpdateStorageOptions> storageOptions)
    {
        _productRepository = productRepository;
        _releaseRepository = releaseRepository;
        _storageOptions = storageOptions.Value;
    }

    public async Task<UpdateCheckServiceResult> CheckAsync(
        UpdateCheckRequest? request,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateRequest(
            request,
            out var currentVersion);

        if (validationFailure is not null)
        {
            return validationFailure;
        }

        var productCode = request!.ProductCode!;
        var channel = request.Channel!;
        var operatingSystem = request.Os!;
        var requestedArchitecture = request.Architecture!;

        var product = await _productRepository.GetByCodeAsync(
            productCode,
            cancellationToken: cancellationToken);

        if (product is null)
        {
            return UpdateCheckServiceResult.Fail(
                UpdateErrorCode.InvalidProduct,
                "등록되지 않은 제품 코드입니다.");
        }

        if (product.ProductStatus != ProductStatus.Active)
        {
            return UpdateCheckServiceResult.Fail(
                UpdateErrorCode.ProductInactive,
                "현재 업데이트를 제공하지 않는 제품입니다.");
        }

        var compatibleRelease = await _releaseRepository.FindLatestCompatibleAsync(
            productCode,
            channel,
            operatingSystem,
            requestedArchitecture,
            PackageTypes.Full,
            cancellationToken: cancellationToken);

        if (compatibleRelease is null)
        {
            return await CreateNoReleaseResultAsync(
                product,
                currentVersion,
                channel,
                operatingSystem,
                requestedArchitecture,
                cancellationToken);
        }

        return CreateCompatibleReleaseResult(
            currentVersion,
            compatibleRelease);
    }

    private async Task<UpdateCheckServiceResult> CreateNoReleaseResultAsync(
        UpdateProduct product,
        UpdateVersion currentVersion,
        string channel,
        string operatingSystem,
        string requestedArchitecture,
        CancellationToken cancellationToken)
    {
        var hasPublishedRelease = await _releaseRepository.HasPublishedReleaseAsync(
            product.ProductCode,
            channel,
            cancellationToken: cancellationToken);

        var reason = hasPublishedRelease
            ? UpdateDecisionReason.NoCompatibleArtifact
            : UpdateDecisionReason.NoAvailableRelease;

        var message = hasPublishedRelease
            ? "호환되는 업데이트 패키지가 없습니다."
            : "배포 가능한 업데이트가 없습니다.";

        var response = new UpdateCheckResponse
        {
            UpdateAvailable = false,
            Mandatory = false,
            ReasonCode = reason.ToCode(),
            ProductCode = product.ProductCode,
            CurrentVersion = currentVersion.ToString(),
            LatestVersion = null,
            ForceUpdateBelowVersion = null,
            Channel = channel,
            Os = operatingSystem,
            Architecture = requestedArchitecture,
            PackageType = null,
            PackageUrl = null,
            FileName = null,
            FileSize = null,
            Sha256 = null,
            ReleaseNotes = null,
            PublishedAt = null
        };

        return UpdateCheckServiceResult.Ok(response, message);
    }

    private UpdateCheckServiceResult CreateCompatibleReleaseResult(
        UpdateVersion currentVersion,
        CompatibleReleaseArtifact release)
    {
        if (!UpdateVersion.TryCreate(
                release.VersionMajor,
                release.VersionMinor,
                release.VersionPatch,
                release.VersionRevision,
                out var releaseVersion,
                out _))
        {
            return InvalidStoredData();
        }

        UpdateVersion? forceUpdateBelowVersion = null;

        if (!string.IsNullOrWhiteSpace(release.ForceUpdateBelowVersion))
        {
            if (!UpdateVersion.TryParse(
                    release.ForceUpdateBelowVersion,
                    out var parsedThreshold,
                    out _))
            {
                return InvalidStoredData();
            }

            forceUpdateBelowVersion = parsedThreshold;
        }

        if (!ReleaseUpdatePolicy.TryCreate(
                releaseVersion,
                release.IsMandatory,
                forceUpdateBelowVersion,
                out var policy,
                out _)
            || policy is null)
        {
            return InvalidStoredData();
        }

        if (!TryBuildPackageUrl(
                release.StorageKey,
                out var packageUrl))
        {
            return InvalidStoredData();
        }

        var decision = UpdateDecisionEvaluator.Evaluate(
            currentVersion,
            policy);

        var response = new UpdateCheckResponse
        {
            UpdateAvailable = decision.UpdateAvailable,
            Mandatory = decision.Mandatory,
            ReasonCode = decision.ReasonCode,
            ProductCode = release.ProductCode,
            CurrentVersion = currentVersion.ToString(),
            LatestVersion = releaseVersion.ToString(),
            ForceUpdateBelowVersion = forceUpdateBelowVersion?.ToString(),
            Channel = release.Channel,
            Os = release.OperatingSystem,
            Architecture = release.Architecture,
            PackageType = release.PackageType,
            PackageUrl = packageUrl,
            FileName = release.FileName,
            FileSize = release.FileSize,
            Sha256 = release.Sha256,
            ReleaseNotes = release.ReleaseNotes,
            PublishedAt = AsUtc(release.PublishedAt)
        };

        var message = decision.UpdateAvailable
            ? "사용 가능한 업데이트가 있습니다."
            : "현재 버전에 적용할 업데이트가 없습니다.";

        return UpdateCheckServiceResult.Ok(response, message);
    }

    private static UpdateCheckServiceResult? ValidateRequest(
        UpdateCheckRequest? request,
        out UpdateVersion currentVersion)
    {
        currentVersion = default;

        if (request is null)
        {
            return UpdateCheckServiceResult.Fail(
                UpdateErrorCode.ValidationError,
                "요청 본문이 필요합니다.");
        }

        if (!ProductCodes.IsSupported(request.ProductCode))
        {
            return UpdateCheckServiceResult.Fail(
                UpdateErrorCode.InvalidProduct,
                "제품 코드가 올바르지 않습니다.");
        }

        if (!UpdateVersion.TryParse(
                request.CurrentVersion,
                out currentVersion,
                out _))
        {
            return UpdateCheckServiceResult.Fail(
                UpdateErrorCode.InvalidVersion,
                "현재 버전 형식이 올바르지 않습니다.");
        }

        if (!UpdateOperatingSystems.IsSupported(request.Os))
        {
            return UpdateCheckServiceResult.Fail(
                UpdateErrorCode.InvalidOperatingSystem,
                "운영체제 코드가 올바르지 않습니다.");
        }

        if (request.Architecture is not ArtifactArchitectures.X86
            and not ArtifactArchitectures.X64)
        {
            return UpdateCheckServiceResult.Fail(
                UpdateErrorCode.InvalidArchitecture,
                "클라이언트 아키텍처가 올바르지 않습니다.");
        }

        if (!ReleaseChannels.IsSupported(request.Channel))
        {
            return UpdateCheckServiceResult.Fail(
                UpdateErrorCode.InvalidChannel,
                "업데이트 채널이 올바르지 않습니다.");
        }

        return null;
    }

    private bool TryBuildPackageUrl(
        string storageKey,
        out string packageUrl)
    {
        packageUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(storageKey)
            || storageKey.StartsWith('/', StringComparison.Ordinal)
            || storageKey.StartsWith('\\'))
        {
            return false;
        }

        var normalizedStorageKey = storageKey.Replace('\\', '/');
        var segments = normalizedStorageKey.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0
            || segments.Any(segment => segment is "." or ".."))
        {
            return false;
        }

        var escapedPath = string.Join(
            '/',
            segments.Select(Uri.EscapeDataString));

        packageUrl = $"{_storageOptions.PublicBaseUrl.TrimEnd('/')}/packages/{escapedPath}";

        return Uri.TryCreate(
            packageUrl,
            UriKind.Absolute,
            out var absoluteUrl)
            && absoluteUrl.Scheme is "http" or "https";
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

    private static UpdateCheckServiceResult InvalidStoredData()
    {
        return UpdateCheckServiceResult.Fail(
            UpdateErrorCode.DatabaseError,
            "업데이트 릴리스 데이터가 올바르지 않습니다.");
    }
}
