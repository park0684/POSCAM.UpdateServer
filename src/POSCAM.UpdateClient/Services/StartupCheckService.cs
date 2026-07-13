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
                return new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.VerificationFailed
                };
            }

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
                return new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.UpdateCheckFailed
                };
            }

            if (response.UpdateAvailable)
            {
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
                return new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.VerificationFailed,
                    UpdateResponse = response
                };
            }

            return new StartupCheckResult
            {
                ExitCode = repairPlan.HasRepairTargets
                    ? UpdateClientExitCodes.ApplyRequired
                    : UpdateClientExitCodes.Success,
                FullPackageUpdateRequired = false,
                UpdateResponse = response,
                RepairPlan = repairPlan
            };
        }
    }
}
