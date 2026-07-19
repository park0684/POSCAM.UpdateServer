using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 원본 UpdateClient 프로세스가 종료된 뒤 worker 경로에서 Full Package를 적용한다.
    /// </summary>
    internal sealed class UpdateApplyWorkerService
    {
        private readonly UpdateApplyPlanStore _planStore;
        private readonly UpdateWorkPathService _pathService;
        private readonly IProcessWaitService _processWaitService;
        private readonly FullPackageApplyService _fullPackageApplyService;
        private readonly IApplicationRestartService _restartService;
        private readonly InstalledManifestStore _installedManifestStore;
        private readonly UpdateWorkCleanupService _cleanupService;

        public UpdateApplyWorkerService(
            UpdateApplyPlanStore planStore,
            UpdateWorkPathService pathService,
            IProcessWaitService processWaitService,
            FullPackageApplyService fullPackageApplyService,
            IApplicationRestartService restartService)
            : this(
                planStore,
                pathService,
                processWaitService,
                fullPackageApplyService,
                restartService,
                new InstalledManifestStore())
        {
        }

        internal UpdateApplyWorkerService(
            UpdateApplyPlanStore planStore,
            UpdateWorkPathService pathService,
            IProcessWaitService processWaitService,
            FullPackageApplyService fullPackageApplyService,
            IApplicationRestartService restartService,
            InstalledManifestStore installedManifestStore)
        {
            _planStore = planStore
                ?? throw new ArgumentNullException(nameof(planStore));
            _pathService = pathService
                ?? throw new ArgumentNullException(nameof(pathService));
            _processWaitService = processWaitService
                ?? throw new ArgumentNullException(nameof(processWaitService));
            _fullPackageApplyService = fullPackageApplyService
                ?? throw new ArgumentNullException(nameof(fullPackageApplyService));
            _restartService = restartService
                ?? throw new ArgumentNullException(nameof(restartService));
            _installedManifestStore = installedManifestStore
                ?? throw new ArgumentNullException(
                    nameof(installedManifestStore));
            _cleanupService = new UpdateWorkCleanupService(
                _pathService,
                _planStore);
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
                    recoveryHandled = true;
                    RestartAfterPrechangeFailure(
                        plan,
                        "원본 UpdateClient 종료를 확인하지 못했습니다.");
                    return UpdateClientExitCodes.ApplyFailed;
                }

                recoveryHandled = true;
                var previousFallbackRequested = _installedManifestStore
                    .IsFullPackageFallbackRequested(
                        plan.InstallDirectory);

                _fullPackageApplyService.ApplyAndRestart(
                    plan,
                    () => CommitAppliedState(plan),
                    () => RestoreAppliedState(
                        plan.InstallDirectory,
                        previousFallbackRequested),
                    () => _restartService.Restart(
                        plan.InstallDirectory,
                        plan.ApplicationFileName,
                        false),
                    () => _restartService.Restart(
                        plan.InstallDirectory,
                        plan.ApplicationFileName,
                        true),
                    () => _cleanupService.CleanupCompletedJob(
                        plan,
                        planPath,
                        true),
                    cancellationToken);

                _cleanupService.CleanupCompletedJob(
                    plan,
                    planPath,
                    true);
                return UpdateClientExitCodes.Success;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
                when (IsHandledApplyException(exception))
            {
                if (!recoveryHandled)
                {
                    await RestartAfterSafeValidationFailureAsync(
                        options,
                        plan,
                        planPath,
                        exception)
                        .ConfigureAwait(false);
                }

                UpdateClientLog.Error(
                    plan?.InstallDirectory
                        ?? UpdateClientLog.TryResolveInstallDirectoryFromPlanPath(
                            planPath ?? options.PlanPath),
                    "apply-worker.failed",
                    "Full Package worker 적용을 완료하지 못했습니다. 적용 계획은 유지됩니다.",
                    exception);
                return UpdateClientExitCodes.ApplyFailed;
            }
        }

        private void CommitAppliedState(UpdateApplyPlan plan)
        {
            _installedManifestStore.ClearFullPackageFallback(
                plan.InstallDirectory);
        }

        private void RestoreAppliedState(
            string installDirectory,
            bool previousFallbackRequested)
        {
            if (previousFallbackRequested)
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

        private void RestartAfterPrechangeFailure(
            UpdateApplyPlan plan,
            string failureMessage)
        {
            try
            {
                _restartService.Restart(
                    plan.InstallDirectory,
                    plan.ApplicationFileName,
                    true);

                UpdateClientLog.Info(
                    plan.InstallDirectory,
                    "apply-worker.prechange.restart.success",
                    failureMessage
                        + " 파일을 변경하지 않고 기존 프로그램을 업데이트 확인 없이 한 번 다시 실행했습니다.");
            }
            catch (Exception restartException)
            {
                UpdateClientLog.Error(
                    plan.InstallDirectory,
                    "apply-worker.prechange.restart.failed",
                    failureMessage
                        + " 파일 변경은 없지만 기존 프로그램도 다시 실행하지 못했습니다.",
                    restartException);

                throw new IOException(
                    "Full Package worker 준비 실패 후 기존 프로그램을 다시 실행하지 못했습니다.",
                    restartException);
            }
        }

        private async Task RestartAfterSafeValidationFailureAsync(
            ApplyOptions options,
            UpdateApplyPlan? plan,
            string? planPath,
            Exception validationException)
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
                    "apply-worker.prechange.restart.skipped",
                    "안전한 기존 프로그램 재실행 경로를 확인하지 못해 재실행을 생략했습니다.",
                    validationException);
                return;
            }

            var exited = await _processWaitService.WaitForExitAsync(
                options.WaitProcessId,
                TimeSpan.FromSeconds(options.WaitTimeoutSeconds),
                CancellationToken.None)
                .ConfigureAwait(false);

            if (!exited)
            {
                UpdateClientLog.Info(
                    installDirectory,
                    "apply-worker.prechange.parent-still-running",
                    "worker 검증 실패 후 원본 UpdateClient가 아직 실행 중이므로 기존 프로그램 재실행을 생략했습니다.");
                return;
            }

            try
            {
                _restartService.Restart(
                    installDirectory,
                    applicationFileName,
                    true);

                UpdateClientLog.Info(
                    installDirectory,
                    "apply-worker.prechange.restart.success",
                    "파일 변경 전에 worker 검증이 실패하여 기존 프로그램을 업데이트 확인 없이 한 번 다시 실행했습니다.");
            }
            catch (Exception restartException)
            {
                UpdateClientLog.Error(
                    installDirectory,
                    "apply-worker.prechange.restart.failed",
                    "worker 검증 실패 후 기존 프로그램도 다시 실행하지 못했습니다.",
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
                || !string.Equals(
                    plan.Mode,
                    UpdateApplyModes.FullPackage,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(plan.JobId)
                || string.IsNullOrWhiteSpace(plan.InstallDirectory)
                || string.IsNullOrWhiteSpace(plan.ApplicationFileName)
                || plan.Targets == null
                || plan.Targets.Count == 0
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
                    "Full Package worker 적용 계획이 올바르지 않습니다.");
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
                    "worker 요청의 제품 또는 아키텍처가 계획과 일치하지 않습니다.");
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
