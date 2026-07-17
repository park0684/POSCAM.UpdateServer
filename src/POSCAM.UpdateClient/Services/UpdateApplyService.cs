using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 적용 계획을 검증하고 대상 프로세스 종료 후 파일 단위 업데이트 또는
    /// Full Package worker 실행을 수행한다.
    /// </summary>
    internal sealed class UpdateApplyService
    {
        private readonly UpdateApplyPlanStore _planStore;
        private readonly UpdateWorkPathService _pathService;
        private readonly IProcessWaitService _processWaitService;
        private readonly FileRepairApplyService _fileRepairApplyService;
        private readonly FullPackageStagingService _fullPackageStagingService;
        private readonly IUpdateWorkerLauncherService _workerLauncherService;
        private readonly IApplicationRestartService _restartService;
        private readonly Func<int> _currentProcessIdProvider;
        private readonly InstalledManifestStore _installedManifestStore;

        public UpdateApplyService(
            UpdateApplyPlanStore planStore,
            UpdateWorkPathService pathService,
            IProcessWaitService processWaitService,
            FileRepairApplyService fileRepairApplyService,
            FullPackageStagingService fullPackageStagingService,
            IUpdateWorkerLauncherService workerLauncherService,
            IApplicationRestartService restartService,
            Func<int> currentProcessIdProvider)
            : this(
                planStore,
                pathService,
                processWaitService,
                fileRepairApplyService,
                fullPackageStagingService,
                workerLauncherService,
                restartService,
                currentProcessIdProvider,
                new InstalledManifestStore())
        {
        }

        internal UpdateApplyService(
            UpdateApplyPlanStore planStore,
            UpdateWorkPathService pathService,
            IProcessWaitService processWaitService,
            FileRepairApplyService fileRepairApplyService,
            FullPackageStagingService fullPackageStagingService,
            IUpdateWorkerLauncherService workerLauncherService,
            IApplicationRestartService restartService,
            Func<int> currentProcessIdProvider,
            InstalledManifestStore installedManifestStore)
        {
            _planStore = planStore
                ?? throw new ArgumentNullException(nameof(planStore));
            _pathService = pathService
                ?? throw new ArgumentNullException(nameof(pathService));
            _processWaitService = processWaitService
                ?? throw new ArgumentNullException(nameof(processWaitService));
            _fileRepairApplyService = fileRepairApplyService
                ?? throw new ArgumentNullException(nameof(fileRepairApplyService));
            _fullPackageStagingService = fullPackageStagingService
                ?? throw new ArgumentNullException(nameof(fullPackageStagingService));
            _workerLauncherService = workerLauncherService
                ?? throw new ArgumentNullException(nameof(workerLauncherService));
            _restartService = restartService
                ?? throw new ArgumentNullException(nameof(restartService));
            _currentProcessIdProvider = currentProcessIdProvider
                ?? throw new ArgumentNullException(
                    nameof(currentProcessIdProvider));
            _installedManifestStore = installedManifestStore
                ?? throw new ArgumentNullException(
                    nameof(installedManifestStore));
        }

        public async Task<int> ApplyAsync(
            ApplyOptions options,
            CancellationToken cancellationToken)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            UpdateApplyPlan? plan = null;
            string? planPath = null;
            var hostExitConfirmed = false;
            var recoveryHandled = false;

            try
            {
                planPath = Path.GetFullPath(options.PlanPath.Trim());
                plan = _planStore.Load(planPath);
                ValidatePlan(planPath, plan, options);

                var exited = await _processWaitService.WaitForExitAsync(
                    options.WaitProcessId,
                    TimeSpan.FromSeconds(options.WaitTimeoutSeconds),
                    cancellationToken)
                    .ConfigureAwait(false);

                if (!exited)
                {
                    UpdateClientLog.Error(
                        plan.InstallDirectory,
                        "apply.host-exit.timeout",
                        "대상 프로그램 종료를 확인하지 못해 파일 적용을 시작하지 않았습니다.");
                    return UpdateClientExitCodes.ApplyFailed;
                }

                hostExitConfirmed = true;

                if (IsFileMode(plan.Mode))
                {
                    recoveryHandled = true;
                    var previousManifest = _installedManifestStore.Load(
                        plan.InstallDirectory);
                    var previousFallbackRequested = _installedManifestStore
                        .IsFullPackageFallbackRequested(
                            plan.InstallDirectory);
                    var forceFullPackageFallback = string.Equals(
                        plan.Mode,
                        UpdateApplyModes.IncrementalUpdate,
                        StringComparison.Ordinal);

                    _fileRepairApplyService.ApplyAndRestart(
                        plan,
                        () => CommitAppliedState(plan),
                        () => RestoreAppliedState(
                            plan.InstallDirectory,
                            previousManifest,
                            previousFallbackRequested,
                            forceFullPackageFallback),
                        () => _restartService.Restart(
                            plan.InstallDirectory,
                            plan.ApplicationFileName),
                        cancellationToken);

                    _planStore.Delete(planPath);
                    return UpdateClientExitCodes.Success;
                }

                if (string.Equals(
                    plan.Mode,
                    UpdateApplyModes.FullPackage,
                    StringComparison.Ordinal))
                {
                    recoveryHandled = true;
                    return PrepareAndLaunchFullPackageWorker(
                        planPath,
                        plan,
                        options,
                        cancellationToken);
                }

                throw new InvalidDataException(
                    "지원하지 않는 업데이트 적용 모드입니다.");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
                when (IsHandledApplyException(exception))
            {
                if (plan != null
                    && string.Equals(
                        plan.Mode,
                        UpdateApplyModes.IncrementalUpdate,
                        StringComparison.Ordinal))
                {
                    TryRequestFullPackageFallback(
                        plan.InstallDirectory,
                        exception);
                }

                if (!recoveryHandled)
                {
                    await RestartAfterSafePrechangeFailureAsync(
                        options,
                        plan,
                        planPath,
                        hostExitConfirmed,
                        exception)
                        .ConfigureAwait(false);
                }

                UpdateClientLog.Error(
                    plan?.InstallDirectory
                        ?? UpdateClientLog.TryResolveInstallDirectoryFromPlanPath(
                            planPath ?? options.PlanPath),
                    "apply.failed",
                    "업데이트 적용을 완료하지 못했습니다. 적용 계획은 유지됩니다.",
                    exception);
                return UpdateClientExitCodes.ApplyFailed;
            }
        }

        private int PrepareAndLaunchFullPackageWorker(
            string planPath,
            UpdateApplyPlan plan,
            ApplyOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                var previousManifest = _installedManifestStore.Load(
                    plan.InstallDirectory);
                var stagingResult = _fullPackageStagingService.Stage(
                    plan,
                    cancellationToken);

                plan.Targets = stagingResult.Targets;
                AppendRemovedManagedTargets(plan, previousManifest);
                _planStore.Save(planPath, plan);

                _workerLauncherService.Launch(
                    plan.InstallDirectory,
                    plan.JobId,
                    planPath,
                    options.ProductCode,
                    options.Architecture,
                    options.RestartFileName,
                    options.WaitTimeoutSeconds,
                    _currentProcessIdProvider());

                return UpdateClientExitCodes.Success;
            }
            catch (Exception exception)
                when (IsHandledApplyException(exception))
            {
                RestartPreviousApplicationAfterPrechangeFailure(
                    plan,
                    exception);
                return UpdateClientExitCodes.ApplyFailed;
            }
        }

        private static void AppendRemovedManagedTargets(
            UpdateApplyPlan plan,
            InstalledManifest? previousManifest)
        {
            if (previousManifest == null
                || previousManifest.Files == null
                || plan.TargetManifest == null
                || plan.TargetManifest.Files == null)
            {
                return;
            }

            var latestPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var target in plan.Targets)
            {
                if (target != null
                    && !string.IsNullOrWhiteSpace(target.RelativePath))
                {
                    latestPaths.Add(
                        NormalizeManifestPath(target.RelativePath));
                }
            }

            foreach (var file in plan.TargetManifest.Files)
            {
                if (file != null && !string.IsNullOrWhiteSpace(file.Path))
                {
                    latestPaths.Add(NormalizeManifestPath(file.Path));
                }
            }

            foreach (var file in previousManifest.Files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.Path))
                {
                    continue;
                }

                var normalizedPath = NormalizeManifestPath(file.Path);
                if (latestPaths.Contains(normalizedPath))
                {
                    continue;
                }

                plan.Targets.Add(new UpdateApplyTarget
                {
                    Operation = UpdateTargetOperations.Delete,
                    RelativePath = normalizedPath,
                    DownloadedPath = "",
                    ExpectedSize = 0,
                    ExpectedSha256 = "",
                    Reason = RepairReasons.Removed
                });
            }
        }

        private static string NormalizeManifestPath(string path)
        {
            return path.Trim().Replace('\\', '/');
        }

        private void CommitAppliedState(UpdateApplyPlan plan)
        {
            if (plan.TargetManifest != null)
            {
                _installedManifestStore.Save(
                    plan.InstallDirectory,
                    plan.TargetManifest);
            }

            _installedManifestStore.ClearFullPackageFallback(
                plan.InstallDirectory);
        }

        private void RestoreAppliedState(
            string installDirectory,
            InstalledManifest? previousManifest,
            bool previousFallbackRequested,
            bool forceFullPackageFallback)
        {
            if (previousManifest == null)
            {
                _installedManifestStore.Delete(installDirectory);
            }
            else
            {
                _installedManifestStore.Save(
                    installDirectory,
                    previousManifest);
            }

            if (forceFullPackageFallback)
            {
                _installedManifestStore.RequestFullPackageFallback(
                    installDirectory,
                    "IncrementalApplyFailed");
            }
            else if (previousFallbackRequested)
            {
                _installedManifestStore.RequestFullPackageFallback(
                    installDirectory,
                    "RollbackRestore");
            }
            else
            {
                _installedManifestStore.ClearFullPackageFallback(
                    installDirectory);
            }
        }

        private void TryRequestFullPackageFallback(
            string installDirectory,
            Exception applyException)
        {
            try
            {
                _installedManifestStore.RequestFullPackageFallback(
                    installDirectory,
                    "IncrementalApplyFailed");

                UpdateClientLog.Info(
                    installDirectory,
                    "apply.incremental.fallback-requested",
                    "증분 적용 실패로 다음 시작 시 Full Package 업데이트를 요청했습니다.");
            }
            catch (Exception markerException)
            {
                UpdateClientLog.Error(
                    installDirectory,
                    "apply.incremental.fallback-request-failed",
                    "증분 적용 실패 후 Full Package 전환 상태를 저장하지 못했습니다.",
                    new AggregateException(
                        applyException,
                        markerException));
            }
        }

        private void RestartPreviousApplicationAfterPrechangeFailure(
            UpdateApplyPlan plan,
            Exception applyException)
        {
            try
            {
                _restartService.Restart(
                    plan.InstallDirectory,
                    plan.ApplicationFileName);

                UpdateClientLog.Info(
                    plan.InstallDirectory,
                    "apply.prechange.restart.success",
                    "파일 변경 전에 Full Package 준비가 실패하여 기존 프로그램을 다시 실행했습니다.");
            }
            catch (Exception restartException)
            {
                UpdateClientLog.Error(
                    plan.InstallDirectory,
                    "apply.prechange.restart.failed",
                    "파일 변경 전 적용 실패 후 기존 프로그램도 다시 실행하지 못했습니다.",
                    restartException);

                throw new IOException(
                    "Full Package 준비 실패 후 기존 프로그램을 다시 실행하지 못했습니다.",
                    new AggregateException(
                        applyException,
                        restartException));
            }
        }

        private async Task RestartAfterSafePrechangeFailureAsync(
            ApplyOptions options,
            UpdateApplyPlan? plan,
            string? planPath,
            bool hostExitConfirmed,
            Exception applyException)
        {
            if (!TryResolveRecoveryTarget(
                options,
                plan,
                planPath,
                out var installDirectory,
                out var applicationFileName))
            {
                UpdateClientLog.Error(
                    UpdateClientLog.TryResolveInstallDirectoryFromPlanPath(
                        planPath ?? options.PlanPath),
                    "apply.prechange.restart.skipped",
                    "안전한 기존 프로그램 재실행 경로를 확인하지 못해 재실행을 생략했습니다.",
                    applyException);
                return;
            }

            if (!hostExitConfirmed)
            {
                var exited = await _processWaitService.WaitForExitAsync(
                    options.WaitProcessId,
                    TimeSpan.FromSeconds(options.WaitTimeoutSeconds),
                    CancellationToken.None)
                    .ConfigureAwait(false);

                if (!exited)
                {
                    UpdateClientLog.Info(
                        installDirectory,
                        "apply.prechange.host-still-running",
                        "적용 실패 후에도 기존 프로그램 프로세스가 실행 중이므로 중복 재실행하지 않았습니다.");
                    return;
                }
            }

            try
            {
                _restartService.Restart(
                    installDirectory,
                    applicationFileName);

                UpdateClientLog.Info(
                    installDirectory,
                    "apply.prechange.restart.success",
                    "파일 변경 전에 적용이 실패하여 기존 프로그램을 다시 실행했습니다.");
            }
            catch (Exception restartException)
            {
                UpdateClientLog.Error(
                    installDirectory,
                    "apply.prechange.restart.failed",
                    "파일 변경 전 적용 실패 후 기존 프로그램도 다시 실행하지 못했습니다.",
                    restartException);
            }
        }

        private bool TryResolveRecoveryTarget(
            ApplyOptions options,
            UpdateApplyPlan? plan,
            string? planPath,
            out string installDirectory,
            out string applicationFileName)
        {
            installDirectory = "";
            applicationFileName = "";

            var resolvedInstallDirectory =
                UpdateClientLog.TryResolveInstallDirectoryFromPlanPath(
                    planPath ?? options.PlanPath);

            if (resolvedInstallDirectory == null
                || resolvedInstallDirectory.Trim().Length == 0)
            {
                return false;
            }

            try
            {
                var candidateFileName = plan != null
                    && !string.IsNullOrWhiteSpace(plan.ApplicationFileName)
                    ? plan.ApplicationFileName
                    : options.RestartFileName;

                installDirectory = Path.GetFullPath(
                    resolvedInstallDirectory.Trim());
                applicationFileName = _pathService.ValidateFileName(
                    candidateFileName);
                return true;
            }
            catch
            {
                installDirectory = "";
                applicationFileName = "";
                return false;
            }
        }

        private static bool IsFileMode(string mode)
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

        private static bool IsHandledApplyException(Exception exception)
        {
            return exception is ArgumentException
                || exception is InvalidDataException
                || exception is IOException
                || exception is UnauthorizedAccessException
                || exception is NotSupportedException
                || exception is System.ComponentModel.Win32Exception;
        }

        private void ValidatePlan(
            string planPath,
            UpdateApplyPlan plan,
            ApplyOptions options)
        {
            if (plan.PlanVersion != 1
                || string.IsNullOrWhiteSpace(plan.JobId)
                || string.IsNullOrWhiteSpace(plan.InstallDirectory)
                || string.IsNullOrWhiteSpace(plan.ApplicationFileName)
                || !UpdateProductIdentity.TryNormalize(
                    plan.ProductCode,
                    plan.Architecture,
                    out var planProductCode,
                    out var planArchitecture)
                || !UpdateProductIdentity.TryNormalize(
                    options.ProductCode,
                    options.Architecture,
                    out var optionProductCode,
                    out var optionArchitecture))
            {
                throw new InvalidDataException(
                    "적용 계획의 필수 정보가 올바르지 않습니다.");
            }

            if (!string.Equals(
                    planProductCode,
                    optionProductCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    planArchitecture,
                    optionArchitecture,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "적용 요청의 제품 또는 아키텍처가 계획과 일치하지 않습니다.");
            }

            _pathService.ValidateJobId(plan.JobId);
            var applicationFileName = _pathService.ValidateFileName(
                plan.ApplicationFileName);
            var restartFileName = _pathService.ValidateFileName(
                options.RestartFileName);

            if (!string.Equals(
                applicationFileName,
                restartFileName,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "재실행 대상이 적용 계획과 일치하지 않습니다.");
            }

            var expectedPlanPath = Path.GetFullPath(
                _pathService.GetActivePlanPath(plan.InstallDirectory));

            if (!string.Equals(
                planPath,
                expectedPlanPath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "적용 계획 경로가 설치 경로와 일치하지 않습니다.");
            }
        }
    }
}
