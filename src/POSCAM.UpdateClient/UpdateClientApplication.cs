using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;

namespace POSCAM.UpdateClient
{
    internal static class UpdateClientApplication
    {
        public static Task<int> RunAsync(
            string[] args,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (args.Length == 0 || IsHelp(args[0]))
            {
                return Task.FromResult(UpdateClientExitCodes.Success);
            }

            var command = args[0].Trim().ToLowerInvariant();

            switch (command)
            {
                case "startup-check":
                    return RunStartupCheckAsync(args, cancellationToken);

                case "apply":
                    return RunApplyAsync(args, cancellationToken);

                case "apply-worker":
                    return RunApplyWorkerAsync(args, cancellationToken);

                default:
                    return Task.FromResult(
                        UpdateClientExitCodes.UnknownCommand);
            }
        }

        private static async Task<int> RunStartupCheckAsync(
            string[] args,
            CancellationToken cancellationToken)
        {
            StartupCheckOptions? options;

            if (!StartupCheckOptions.TryParse(args, out options)
                || options == null)
            {
                return UpdateClientExitCodes.UpdateCheckFailed;
            }

            try
            {
                using (var updateServerClient = new UpdateServerClient(
                    options.BaseUrl))
                using (var downloadService = new UpdateFileDownloadService())
                {
                    var checkService = new StartupCheckService(
                        updateServerClient,
                        new ApplicationVersionResolver(),
                        new ManifestRepairPlanner());

                    var checkResult = await checkService.CheckAsync(
                        options,
                        cancellationToken)
                        .ConfigureAwait(false);

                    var preparationService =
                        new StartupUpdatePreparationService(
                            downloadService,
                            new UpdateWorkPathService(),
                            new UpdateApplyPlanStore());

                    return await preparationService.PrepareAsync(
                        options,
                        checkResult,
                        cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (ArgumentException)
            {
                return UpdateClientExitCodes.UpdateCheckFailed;
            }
        }

        private static async Task<int> RunApplyAsync(
            string[] args,
            CancellationToken cancellationToken)
        {
            ApplyOptions? options;

            if (!ApplyOptions.TryParse(args, out options)
                || options == null)
            {
                return UpdateClientExitCodes.ApplyFailed;
            }

            var pathService = new UpdateWorkPathService();
            var hashCalculator = new FileHashCalculator();
            var planStore = new UpdateApplyPlanStore();
            var completionPlan = TryLoadCompletionPlan(
                planStore,
                options);
            var service = new UpdateApplyService(
                planStore,
                pathService,
                new ProcessWaitService(),
                new FileRepairApplyService(
                    pathService,
                    hashCalculator),
                new FullPackageStagingService(
                    pathService,
                    hashCalculator),
                new UpdateWorkerLauncherService(pathService),
                new ApplicationRestartService(),
                GetCurrentProcessId);

            var exitCode = await service.ApplyAsync(
                options,
                cancellationToken)
                .ConfigureAwait(false);

            if (exitCode == UpdateClientExitCodes.Success
                && completionPlan != null
                && IsCompletedFileMode(completionPlan.Mode))
            {
                WriteCompletionLog(completionPlan);
            }

            return exitCode;
        }

        private static async Task<int> RunApplyWorkerAsync(
            string[] args,
            CancellationToken cancellationToken)
        {
            ApplyOptions? options;

            if (!ApplyOptions.TryParse(args, out options)
                || options == null)
            {
                return UpdateClientExitCodes.ApplyFailed;
            }

            var pathService = new UpdateWorkPathService();
            var planStore = new UpdateApplyPlanStore();
            var completionPlan = TryLoadCompletionPlan(
                planStore,
                options);
            var service = new UpdateApplyWorkerService(
                planStore,
                pathService,
                new ProcessWaitService(),
                new FullPackageApplyService(
                    pathService,
                    new FileHashCalculator()),
                new ApplicationRestartService());

            var exitCode = await service.ApplyAsync(
                options,
                cancellationToken)
                .ConfigureAwait(false);

            if (exitCode == UpdateClientExitCodes.Success
                && completionPlan != null
                && string.Equals(
                    completionPlan.Mode,
                    UpdateApplyModes.FullPackage,
                    StringComparison.Ordinal))
            {
                WriteCompletionLog(completionPlan);
            }

            return exitCode;
        }

        private static UpdateApplyPlan? TryLoadCompletionPlan(
            UpdateApplyPlanStore planStore,
            ApplyOptions options)
        {
            try
            {
                var planPath = Path.GetFullPath(
                    options.PlanPath.Trim());
                return planStore.Load(planPath);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsCompletedFileMode(string mode)
        {
            return string.Equals(
                    mode,
                    UpdateApplyModes.FileRepair,
                    StringComparison.Ordinal)
                || string.Equals(
                    mode,
                    UpdateApplyModes.IncrementalUpdate,
                    StringComparison.Ordinal);
        }

        private static void WriteCompletionLog(
            UpdateApplyPlan plan)
        {
            UpdateClientLog.Completed(
                plan.InstallDirectory,
                plan.JobId,
                plan.Mode,
                plan.LatestVersion,
                plan.Targets == null ? 0 : plan.Targets.Count);
        }

        private static int GetCurrentProcessId()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.Id;
            }
        }

        private static bool IsHelp(string value)
        {
            return value == "-h"
                || value == "--help"
                || value == "/?"
                || value == "help";
        }
    }
}
