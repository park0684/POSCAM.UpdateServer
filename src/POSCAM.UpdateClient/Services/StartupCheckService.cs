using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// Update Check 호출과 로컬 Manifest 검사를 순서대로 수행한다.
    /// </summary>
    internal sealed class StartupCheckService
    {
        private readonly IUpdateServerClient _updateServerClient;
        private readonly ApplicationVersionResolver _versionResolver;
        private readonly ManifestRepairPlanner _repairPlanner;
        private readonly InstalledManifestStore _installedManifestStore;

        public StartupCheckService(
            IUpdateServerClient updateServerClient,
            ApplicationVersionResolver versionResolver,
            ManifestRepairPlanner repairPlanner)
            : this(
                updateServerClient,
                versionResolver,
                repairPlanner,
                new InstalledManifestStore())
        {
        }

        internal StartupCheckService(
            IUpdateServerClient updateServerClient,
            ApplicationVersionResolver versionResolver,
            ManifestRepairPlanner repairPlanner,
            InstalledManifestStore installedManifestStore)
        {
            _updateServerClient = updateServerClient
                ?? throw new ArgumentNullException(nameof(updateServerClient));
            _versionResolver = versionResolver
                ?? throw new ArgumentNullException(nameof(versionResolver));
            _repairPlanner = repairPlanner
                ?? throw new ArgumentNullException(nameof(repairPlanner));
            _installedManifestStore = installedManifestStore
                ?? throw new ArgumentNullException(
                    nameof(installedManifestStore));
        }

        public async Task<StartupCheckResult> CheckAsync(
            StartupCheckOptions options,
            CancellationToken cancellationToken)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            string currentVersion;

            try
            {
                currentVersion = _versionResolver.Resolve(options);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidDataException
                    || exception is IOException
                    || exception is UnauthorizedAccessException)
            {
                UpdateClientLog.Error(
                    options.InstallDirectory,
                    "StartupCheck.VersionResolveFailed",
                    "로컬 프로그램 버전을 확인하지 못했습니다. ExitCode=20",
                    exception);

                return new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.VerificationFailed
                };
            }

            UpdateClientLog.Info(
                options.InstallDirectory,
                "StartupCheck.Request",
                "Product=" + options.ProductCode
                    + " CurrentVersion=" + currentVersion
                    + " OS=" + options.OperatingSystem
                    + " Architecture=" + options.Architecture
                    + " Channel=" + options.Channel);

            UpdateCheckResponse response;

            try
            {
                response = await _updateServerClient.CheckAsync(
                    new UpdateCheckRequest
                    {
                        ProductCode = options.ProductCode,
                        CurrentVersion = currentVersion,
                        Os = options.OperatingSystem,
                        Architecture = options.Architecture,
                        Channel = options.Channel
                    },
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
                when (exception is UpdateServerClientException
                    || exception is HttpRequestException
                    || exception is TaskCanceledException)
            {
                UpdateClientLog.Error(
                    options.InstallDirectory,
                    "StartupCheck.RequestFailed",
                    "Update Check 요청에 실패했습니다. ExitCode=30",
                    exception);

                return new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.UpdateCheckFailed
                };
            }

            var manifestCount = response.Files == null
                ? 0
                : response.Files.Count;

            UpdateClientLog.Info(
                options.InstallDirectory,
                "StartupCheck.Response",
                "UpdateAvailable=" + response.UpdateAvailable
                    + " LatestVersion=" + (response.LatestVersion ?? "")
                    + " ManifestFiles=" + manifestCount);

            try
            {
                var targetManifest = CreateTargetManifest(
                    options,
                    response);

                if (response.UpdateAvailable)
                {
                    return CreateVersionUpdateDecision(
                        options,
                        currentVersion,
                        response,
                        targetManifest,
                        manifestCount);
                }

                return CreateRepairDecision(
                    options,
                    response,
                    targetManifest,
                    manifestCount);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidDataException
                    || exception is IOException
                    || exception is UnauthorizedAccessException)
            {
                UpdateClientLog.Error(
                    options.InstallDirectory,
                    "StartupCheck.ManifestFailed",
                    "Manifest 로컬 검증에 실패했습니다. ExitCode=20",
                    exception);

                return new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.VerificationFailed,
                    UpdateResponse = response
                };
            }
        }

        private StartupCheckResult CreateVersionUpdateDecision(
            StartupCheckOptions options,
            string currentVersion,
            UpdateCheckResponse response,
            InstalledManifest? targetManifest,
            int manifestCount)
        {
            var installedManifest = _installedManifestStore.Load(
                options.InstallDirectory);
            var fallbackRequested = _installedManifestStore
                .IsFullPackageFallbackRequested(
                    options.InstallDirectory);

            if (fallbackRequested
                || installedManifest == null
                || targetManifest == null
                || !IsInstalledManifestCompatible(
                    installedManifest,
                    options,
                    currentVersion))
            {
                UpdateClientLog.Info(
                    options.InstallDirectory,
                    "StartupCheck.Decision",
                    "Mode=FullPackage ManifestFiles=" + manifestCount
                        + " FallbackRequested=" + fallbackRequested
                        + " ExitCode=10");

                return new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.ApplyRequired,
                    FullPackageUpdateRequired = true,
                    IncrementalUpdateRequired = false,
                    UpdateResponse = response,
                    TargetManifest = targetManifest
                };
            }

            var incrementalPlan = _repairPlanner.CreateIncrementalPlan(
                options.InstallDirectory,
                response.Files,
                installedManifest);

            if (!incrementalPlan.HasRepairTargets)
            {
                UpdateClientLog.Info(
                    options.InstallDirectory,
                    "StartupCheck.Decision",
                    "Mode=FullPackage"
                        + " Reason=NoIncrementalTargets"
                        + " ManifestFiles=" + manifestCount
                        + " ExitCode=10");

                return new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.ApplyRequired,
                    FullPackageUpdateRequired = true,
                    IncrementalUpdateRequired = false,
                    UpdateResponse = response,
                    TargetManifest = targetManifest
                };
            }

            foreach (var target in incrementalPlan.Targets)
            {
                UpdateClientLog.Info(
                    options.InstallDirectory,
                    "StartupCheck.IncrementalTarget",
                    "Operation=" + target.Operation
                        + " Path=" + target.RelativePath
                        + " Reason=" + target.Reason);
            }

            UpdateClientLog.Info(
                options.InstallDirectory,
                "StartupCheck.Decision",
                "Mode=IncrementalUpdate ManifestFiles=" + manifestCount
                    + " Targets=" + incrementalPlan.Targets.Count
                    + " ExitCode=" + UpdateClientExitCodes.ApplyRequired);

            return new StartupCheckResult
            {
                ExitCode = UpdateClientExitCodes.ApplyRequired,
                FullPackageUpdateRequired = false,
                IncrementalUpdateRequired = true,
                UpdateResponse = response,
                RepairPlan = incrementalPlan,
                TargetManifest = targetManifest
            };
        }

        private StartupCheckResult CreateRepairDecision(
            StartupCheckOptions options,
            UpdateCheckResponse response,
            InstalledManifest? targetManifest,
            int manifestCount)
        {
            var repairPlan = _repairPlanner.CreatePlan(
                options.InstallDirectory,
                response.Files);

            foreach (var target in repairPlan.Targets)
            {
                UpdateClientLog.Info(
                    options.InstallDirectory,
                    "StartupCheck.RepairTarget",
                    "Path=" + target.RelativePath
                        + " Reason=" + target.Reason);
            }

            var exitCode = repairPlan.HasRepairTargets
                ? UpdateClientExitCodes.ApplyRequired
                : UpdateClientExitCodes.Success;

            UpdateClientLog.Info(
                options.InstallDirectory,
                "StartupCheck.Decision",
                "Mode=FileRepair ManifestFiles=" + manifestCount
                    + " RepairTargets=" + repairPlan.Targets.Count
                    + " ExitCode=" + exitCode);

            return new StartupCheckResult
            {
                ExitCode = exitCode,
                FullPackageUpdateRequired = false,
                IncrementalUpdateRequired = false,
                UpdateResponse = response,
                RepairPlan = repairPlan,
                TargetManifest = targetManifest
            };
        }

        private static InstalledManifest? CreateTargetManifest(
            StartupCheckOptions options,
            UpdateCheckResponse response)
        {
            if (response.Files == null
                || response.Files.Count == 0
                || string.IsNullOrWhiteSpace(response.LatestVersion))
            {
                return null;
            }

            if (!UpdateProductIdentity.TryNormalize(
                options.ProductCode,
                options.Architecture,
                out var productCode,
                out var architecture))
            {
                throw new InvalidDataException(
                    "업데이트 제품 또는 아키텍처 정보가 올바르지 않습니다.");
            }

            var manifest = new InstalledManifest
            {
                ProductCode = productCode,
                Architecture = architecture,
                Version = response.LatestVersion!.Trim(),
                InstalledAtUtc = DateTime.UtcNow
            };
            var paths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var file in response.Files)
            {
                if (file == null || !file.Required)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(file.Path)
                    || file.Size < 0
                    || !IsValidSha256(file.Sha256))
                {
                    throw new InvalidDataException(
                        "서버 Manifest 파일 정보가 올바르지 않습니다.");
                }

                var path = file.Path.Trim().Replace('\\', '/');
                if (!paths.Add(path))
                {
                    throw new InvalidDataException(
                        "서버 Manifest에 동일한 파일 경로가 중복되어 있습니다: "
                        + path);
                }

                manifest.Files.Add(new InstalledManifestFile
                {
                    Path = path,
                    Size = file.Size,
                    Sha256 = file.Sha256.Trim().ToUpperInvariant()
                });
            }

            return manifest.Files.Count == 0 ? null : manifest;
        }

        private static bool IsInstalledManifestCompatible(
            InstalledManifest installedManifest,
            StartupCheckOptions options,
            string currentVersion)
        {
            if (!UpdateProductIdentity.TryNormalize(
                    installedManifest.ProductCode,
                    installedManifest.Architecture,
                    out var installedProductCode,
                    out var installedArchitecture)
                || !UpdateProductIdentity.TryNormalize(
                    options.ProductCode,
                    options.Architecture,
                    out var requestedProductCode,
                    out var requestedArchitecture))
            {
                return false;
            }

            return string.Equals(
                    installedProductCode,
                    requestedProductCode,
                    StringComparison.Ordinal)
                && string.Equals(
                    installedArchitecture,
                    requestedArchitecture,
                    StringComparison.Ordinal)
                && string.Equals(
                    installedManifest.Version.Trim(),
                    currentVersion.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            if (normalized.Length != 64)
            {
                return false;
            }

            foreach (var character in normalized)
            {
                var isHex = character >= '0' && character <= '9'
                    || character >= 'a' && character <= 'f'
                    || character >= 'A' && character <= 'F';

                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
