using System;
using System.IO;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// UpdateClient 작업 폴더를 JobId 기준으로 안전하게 정리한다.
    ///
    /// 정리 실패는 이미 완료된 업데이트나 기존 프로그램 실행 결과를
    /// 변경하지 않도록 로그만 기록하고 호출자에게 예외를 전파하지 않는다.
    /// </summary>
    internal sealed class UpdateWorkCleanupService
    {
        private readonly UpdateWorkPathService _pathService;
        private readonly UpdateApplyPlanStore _planStore;

        public UpdateWorkCleanupService(
            UpdateWorkPathService pathService,
            UpdateApplyPlanStore planStore)
        {
            _pathService = pathService
                ?? throw new ArgumentNullException(nameof(pathService));
            _planStore = planStore
                ?? throw new ArgumentNullException(nameof(planStore));
        }

        public void CleanupOrphanedWork(string installDirectory)
        {
            try
            {
                var planPath = _pathService.GetActivePlanPath(
                    installDirectory);

                UpdatePlanLock.Execute(
                    planPath,
                    () =>
                    {
                        var activePlanState = TryReadActivePlan(
                            installDirectory,
                            planPath,
                            out var activeJobId);

                        if (activePlanState
                            == ActivePlanState.Invalid)
                        {
                            UpdateClientLog.Info(
                                installDirectory,
                                "Cleanup.OrphanSkipped",
                                "활성 적용 계획을 해석할 수 없어 작업 증거 보존을 위해 고아 Job 정리를 건너뜁니다.");
                            PruneEmptyDirectories(
                                installDirectory);
                            return;
                        }

                        CleanupRootChildren(
                            installDirectory,
                            _pathService.GetDownloadsRootDirectory(
                                installDirectory),
                            activeJobId);
                        CleanupRootChildren(
                            installDirectory,
                            _pathService.GetBackupsRootDirectory(
                                installDirectory),
                            activeJobId);
                        CleanupRootChildren(
                            installDirectory,
                            _pathService.GetWorkersRootDirectory(
                                installDirectory),
                            activeJobId);
                        PruneEmptyDirectories(
                            installDirectory);
                    });
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    installDirectory,
                    "Cleanup.OrphanFailed",
                    "고아 업데이트 작업 정리에 실패했습니다.",
                    exception);
            }
        }

        public void CleanupNoWork(string installDirectory)
        {
            try
            {
                var planPath = _pathService.GetActivePlanPath(
                    installDirectory);

                UpdatePlanLock.Execute(
                    planPath,
                    () =>
                    {
                        TryDeletePlanUnconditionally(
                            installDirectory,
                            planPath);

                        CleanupRootChildren(
                            installDirectory,
                            _pathService.GetDownloadsRootDirectory(
                                installDirectory),
                            null);
                        CleanupRootChildren(
                            installDirectory,
                            _pathService.GetBackupsRootDirectory(
                                installDirectory),
                            null);
                        CleanupRootChildren(
                            installDirectory,
                            _pathService.GetWorkersRootDirectory(
                                installDirectory),
                            null);
                        PruneEmptyDirectories(
                            installDirectory);
                    });
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    installDirectory,
                    "Cleanup.NoWorkFailed",
                    "업데이트가 필요하지 않은 상태의 작업 정리에 실패했습니다.",
                    exception);
            }
        }

        public void CleanupCompletedJob(
            UpdateApplyPlan plan,
            string planPath,
            bool preserveWorkerDirectory)
        {
            if (plan == null)
            {
                return;
            }

            try
            {
                var normalizedJobId = _pathService.ValidateJobId(
                    plan.JobId);
                var normalizedPlanPath = Path.GetFullPath(
                    planPath.Trim());

                TryDeletePlanForJob(
                    plan.InstallDirectory,
                    normalizedPlanPath,
                    normalizedJobId);

                TryDeleteDirectory(
                    plan.InstallDirectory,
                    _pathService.GetJobDirectory(
                        plan.InstallDirectory,
                        normalizedJobId),
                    "Cleanup.DownloadJobFailed");

                TryDeleteDirectory(
                    plan.InstallDirectory,
                    _pathService.GetBackupDirectory(
                        plan.InstallDirectory,
                        normalizedJobId),
                    "Cleanup.BackupJobFailed");

                if (!preserveWorkerDirectory)
                {
                    TryDeleteDirectory(
                        plan.InstallDirectory,
                        _pathService.GetWorkerDirectory(
                            plan.InstallDirectory,
                            normalizedJobId),
                        "Cleanup.WorkerJobFailed");
                }

                PruneEmptyDirectories(plan.InstallDirectory);

                UpdateClientLog.Info(
                    plan.InstallDirectory,
                    "Cleanup.Completed",
                    "JobId=" + normalizedJobId
                        + " PreserveWorker="
                        + preserveWorkerDirectory);
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    plan.InstallDirectory,
                    "Cleanup.CompletedFailed",
                    "완료된 업데이트 Job 정리에 실패했습니다.",
                    exception);
            }
        }

        public void CleanupFailedPreparation(UpdateWorkPaths paths)
        {
            if (paths == null)
            {
                return;
            }

            try
            {
                var normalizedJobId = _pathService.ValidateJobId(
                    paths.JobId);

                TryDeletePlanForJob(
                    paths.InstallDirectory,
                    paths.ActivePlanPath,
                    normalizedJobId);

                TryDeleteDirectory(
                    paths.InstallDirectory,
                    paths.JobDirectory,
                    "Cleanup.FailedDownloadJobFailed");

                TryDeleteDirectory(
                    paths.InstallDirectory,
                    _pathService.GetBackupDirectory(
                        paths.InstallDirectory,
                        normalizedJobId),
                    "Cleanup.FailedBackupJobFailed");

                TryDeleteDirectory(
                    paths.InstallDirectory,
                    _pathService.GetWorkerDirectory(
                        paths.InstallDirectory,
                        normalizedJobId),
                    "Cleanup.FailedWorkerJobFailed");

                PruneEmptyDirectories(paths.InstallDirectory);
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    paths.InstallDirectory,
                    "Cleanup.FailedPreparationFailed",
                    "실패한 업데이트 준비 Job 정리에 실패했습니다.",
                    exception);
            }
        }

        private ActivePlanState TryReadActivePlan(
            string installDirectory,
            string planPath,
            out string? activeJobId)
        {
            activeJobId = null;

            if (!File.Exists(planPath))
            {
                return ActivePlanState.None;
            }

            try
            {
                var plan = _planStore.Load(planPath);
                activeJobId = _pathService.ValidateJobId(
                    plan.JobId);
                return ActivePlanState.Valid;
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    installDirectory,
                    "Cleanup.ActivePlanReadFailed",
                    "활성 적용 계획을 해석하지 못했습니다.",
                    exception);
                return ActivePlanState.Invalid;
            }
        }

        private void CleanupRootChildren(
            string installDirectory,
            string rootDirectory,
            string? protectedJobId)
        {
            if (!Directory.Exists(rootDirectory))
            {
                return;
            }

            string[] directories;

            try
            {
                directories = Directory.GetDirectories(
                    rootDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    installDirectory,
                    "Cleanup.RootEnumerateFailed",
                    "업데이트 작업 하위 폴더를 조회하지 못했습니다. Root="
                        + rootDirectory,
                    exception);
                return;
            }

            foreach (var directory in directories)
            {
                var directoryName = Path.GetFileName(directory);

                if (string.IsNullOrWhiteSpace(directoryName))
                {
                    continue;
                }

                try
                {
                    var normalizedJobId = _pathService.ValidateJobId(
                        directoryName);

                    if (protectedJobId != null
                        && string.Equals(
                            normalizedJobId,
                            protectedJobId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TryDeleteDirectory(
                        installDirectory,
                        directory,
                        "Cleanup.OrphanDirectoryFailed");
                }
                catch (Exception exception)
                {
                    LogCleanupFailure(
                        installDirectory,
                        "Cleanup.InvalidJobDirectory",
                        "업데이트 Job 폴더명이 올바르지 않아 자동 삭제하지 않았습니다. Path="
                            + directory,
                        exception);
                }
            }
        }

        private void TryDeletePlanForJob(
            string installDirectory,
            string planPath,
            string expectedJobId)
        {
            try
            {
                var deleted = _planStore.DeleteIfJobMatches(
                    planPath,
                    expectedJobId);

                if (!deleted && File.Exists(planPath))
                {
                    UpdateClientLog.Info(
                        installDirectory,
                        "Cleanup.PlanPreserved",
                        "다른 Job의 적용 계획은 삭제하지 않았습니다. ExpectedJobId="
                            + expectedJobId);
                }
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    installDirectory,
                    "Cleanup.PlanDeleteFailed",
                    "JobId가 일치하는 적용 계획을 삭제하지 못했습니다.",
                    exception);
            }
        }

        private void TryDeletePlanUnconditionally(
            string installDirectory,
            string planPath)
        {
            try
            {
                _planStore.Delete(planPath);
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    installDirectory,
                    "Cleanup.StalePlanDeleteFailed",
                    "업데이트가 필요하지 않은 상태의 적용 계획을 삭제하지 못했습니다.",
                    exception);
            }
        }

        private void TryDeleteDirectory(
            string installDirectory,
            string directory,
            string eventName)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    installDirectory,
                    eventName,
                    "업데이트 작업 폴더를 삭제하지 못했습니다. Path="
                        + directory,
                    exception);
            }
        }

        private void PruneEmptyDirectories(string installDirectory)
        {
            TryDeleteEmptyDirectory(
                installDirectory,
                _pathService.GetDownloadsRootDirectory(
                    installDirectory));
            TryDeleteEmptyDirectory(
                installDirectory,
                _pathService.GetBackupsRootDirectory(
                    installDirectory));
            TryDeleteEmptyDirectory(
                installDirectory,
                _pathService.GetWorkersRootDirectory(
                    installDirectory));
            TryDeleteEmptyDirectory(
                installDirectory,
                _pathService.GetStateDirectory(
                    installDirectory));
            TryDeleteEmptyDirectory(
                installDirectory,
                _pathService.GetUpdateRootDirectory(
                    installDirectory));
        }

        private void TryDeleteEmptyDirectory(
            string installDirectory,
            string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    return;
                }

                if (Directory.GetFileSystemEntries(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
            catch (Exception exception)
            {
                LogCleanupFailure(
                    installDirectory,
                    "Cleanup.EmptyDirectoryFailed",
                    "비어 있는 업데이트 폴더를 정리하지 못했습니다. Path="
                        + directory,
                    exception);
            }
        }

        private static void LogCleanupFailure(
            string installDirectory,
            string eventName,
            string message,
            Exception exception)
        {
            UpdateClientLog.Error(
                installDirectory,
                eventName,
                message,
                exception);
        }

        private enum ActivePlanState
        {
            None,
            Valid,
            Invalid
        }
    }
}
