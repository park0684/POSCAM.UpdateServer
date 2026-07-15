using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 적용 계획을 검증하고 대상 프로세스 종료 후 파일 복구 또는 Full Package
    /// worker 실행을 수행한다.
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

        public UpdateApplyService(
            UpdateApplyPlanStore planStore,
            UpdateWorkPathService pathService,
            IProcessWaitService processWaitService,
            FileRepairApplyService fileRepairApplyService,
            FullPackageStagingService fullPackageStagingService,
            IUpdateWorkerLauncherService workerLauncherService,
            IApplicationRestartService restartService,
            Func<int> currentProcessIdProvider)
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

                if (string.Equals(
                    plan.Mode,
                    UpdateApplyModes.FileRepair,
                    StringComparison.Ordinal))
                {
                    recoveryHandled = true;
                    _fileRepairApplyService.ApplyAndRestart(
                        plan,
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
                var stagingResult = _fullPackageStagingService.Stage(
                    plan,
                    cancellationToken);

                plan.Targets = stagingResult.Targets;
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

            if (string.IsNullOrWhiteSpace(resolvedInstallDirectory))
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
