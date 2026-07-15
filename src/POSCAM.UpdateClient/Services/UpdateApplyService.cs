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

            try
            {
                var planPath = Path.GetFullPath(options.PlanPath.Trim());
                var plan = _planStore.Load(planPath);
                ValidatePlan(planPath, plan, options);

                var exited = await _processWaitService.WaitForExitAsync(
                    options.WaitProcessId,
                    TimeSpan.FromSeconds(options.WaitTimeoutSeconds),
                    cancellationToken)
                    .ConfigureAwait(false);

                if (!exited)
                {
                    return UpdateClientExitCodes.ApplyFailed;
                }

                if (string.Equals(
                    plan.Mode,
                    UpdateApplyModes.FileRepair,
                    StringComparison.Ordinal))
                {
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

                return UpdateClientExitCodes.ApplyFailed;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidDataException
                    || exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is NotSupportedException
                    || exception is System.ComponentModel.Win32Exception)
            {
                return UpdateClientExitCodes.ApplyFailed;
            }
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
