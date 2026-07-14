using System;
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

        public StartupCheckService(
            IUpdateServerClient updateServerClient,
            ApplicationVersionResolver versionResolver,
            ManifestRepairPlanner repairPlanner)
        {
            _updateServerClient = updateServerClient
                ?? throw new ArgumentNullException(nameof(updateServerClient));
            _versionResolver = versionResolver
                ?? throw new ArgumentNullException(nameof(versionResolver));
            _repairPlanner = repairPlanner
                ?? throw new ArgumentNullException(nameof(repairPlanner));
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

            if (response.UpdateAvailable)
            {
                UpdateClientLog.Info(
                    options.InstallDirectory,
                    "StartupCheck.Decision",
                    "Mode=FullPackage ExitCode=10");

                return new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.ApplyRequired,
                    FullPackageUpdateRequired = true,
                    UpdateResponse = response
                };
            }

            RepairPlan repairPlan;

            try
            {
                repairPlan = _repairPlanner.CreatePlan(
                    options.InstallDirectory,
                    response.Files);
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
                UpdateResponse = response,
                RepairPlan = repairPlan
            };
        }
    }
}
