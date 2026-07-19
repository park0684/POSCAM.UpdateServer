using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// worker 경로에서 실행되어 Full Package staging 파일을 설치 경로에 적용한다.
    /// 기존 파일과 설치 상태는 실패 시 역순으로 복구한다.
    /// </summary>
    internal sealed class FullPackageApplyService
    {
        private readonly UpdateWorkPathService _pathService;
        private readonly FileHashCalculator _hashCalculator;

        public FullPackageApplyService(
            UpdateWorkPathService pathService,
            FileHashCalculator hashCalculator)
        {
            _pathService = pathService
                ?? throw new ArgumentNullException(nameof(pathService));
            _hashCalculator = hashCalculator
                ?? throw new ArgumentNullException(nameof(hashCalculator));
        }

        public void ApplyAndRestart(
            UpdateApplyPlan plan,
            Action restartAction,
            CancellationToken cancellationToken)
        {
            ApplyAndRestart(
                plan,
                () => { },
                () => { },
                restartAction,
                cancellationToken);
        }

        public void ApplyAndRestart(
            UpdateApplyPlan plan,
            Action commitStateAction,
            Action rollbackStateAction,
            Action restartAction,
            CancellationToken cancellationToken)
        {
            ApplyAndRestart(
                plan,
                commitStateAction,
                rollbackStateAction,
                restartAction,
                () => { },
                cancellationToken);
        }

        public void ApplyAndRestart(
            UpdateApplyPlan plan,
            Action commitStateAction,
            Action rollbackStateAction,
            Action restartAction,
            Action recoveryCompletedAction,
            CancellationToken cancellationToken)
        {
            ApplyAndRestart(
                plan,
                commitStateAction,
                rollbackStateAction,
                restartAction,
                restartAction,
                recoveryCompletedAction,
                cancellationToken);
        }

        public void ApplyAndRestart(
            UpdateApplyPlan plan,
            Action commitStateAction,
            Action rollbackStateAction,
            Action successRestartAction,
            Action recoveryRestartAction,
            Action recoveryCompletedAction,
            CancellationToken cancellationToken)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (commitStateAction == null)
            {
                throw new ArgumentNullException(nameof(commitStateAction));
            }

            if (rollbackStateAction == null)
            {
                throw new ArgumentNullException(nameof(rollbackStateAction));
            }

            if (successRestartAction == null)
            {
                throw new ArgumentNullException(
                    nameof(successRestartAction));
            }

            if (recoveryRestartAction == null)
            {
                throw new ArgumentNullException(
                    nameof(recoveryRestartAction));
            }

            if (recoveryCompletedAction == null)
            {
                throw new ArgumentNullException(
                    nameof(recoveryCompletedAction));
            }

            var applied = new List<ApplyOperation>();

            try
            {
                var operations = BuildOperations(plan);

                foreach (var operation in operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ApplySingleOperation(operation, applied);
                }

                cancellationToken.ThrowIfCancellationRequested();
                commitStateAction();
                successRestartAction();
            }
            catch (Exception applyException)
            {
                RecoverPreviousApplication(
                    plan.InstallDirectory,
                    applied,
                    rollbackStateAction,
                    recoveryRestartAction,
                    recoveryCompletedAction,
                    applyException);
                throw;
            }
        }

        private List<ApplyOperation> BuildOperations(UpdateApplyPlan plan)
        {
            if (plan.PlanVersion != 1
                || !string.Equals(
                    plan.Mode,
                    UpdateApplyModes.FullPackage,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(plan.InstallDirectory)
                || string.IsNullOrWhiteSpace(plan.JobId)
                || string.IsNullOrWhiteSpace(plan.ApplicationFileName)
                || plan.Targets == null
                || plan.Targets.Count == 0)
            {
                throw new InvalidDataException(
                    "Full Package 적용 계획이 올바르지 않습니다.");
            }

            var applicationFileName = _pathService.ValidateFileName(
                plan.ApplicationFileName);
            var jobDirectory = _pathService.GetJobDirectory(
                plan.InstallDirectory,
                plan.JobId);
            var backupDirectory = _pathService.GetBackupDirectory(
                plan.InstallDirectory,
                plan.JobId);
            var currentExecutable = Path.GetFullPath(
                Assembly.GetExecutingAssembly().Location);
            var destinations = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var operations = new List<ApplyOperation>(plan.Targets.Count);
            var containsApplication = false;

            foreach (var target in plan.Targets)
            {
                if (target == null
                    || string.IsNullOrWhiteSpace(target.RelativePath))
                {
                    throw new InvalidDataException(
                        "Full Package 파일 대상 정보가 올바르지 않습니다.");
                }

                var operation = NormalizeOperation(target.Operation);
                var relativePath = target.RelativePath;
                var destinationPath = _pathService.ResolveInstallFilePath(
                    plan.InstallDirectory,
                    relativePath);

                if (string.Equals(
                    destinationPath,
                    currentExecutable,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "실행 중인 worker 파일은 직접 교체하거나 삭제할 수 없습니다.");
                }

                if (!destinations.Add(destinationPath))
                {
                    throw new InvalidDataException(
                        "Full Package에 중복된 적용 대상이 있습니다.");
                }

                var applyOperation = new ApplyOperation
                {
                    Operation = operation,
                    DestinationPath = destinationPath,
                    BackupPath = _pathService.ResolveBackupFilePath(
                        backupDirectory,
                        relativePath)
                };

                if (string.Equals(
                    operation,
                    UpdateTargetOperations.Delete,
                    StringComparison.Ordinal))
                {
                    if (!string.Equals(
                        target.Reason,
                        RepairReasons.Removed,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "Full Package 삭제 대상의 사유가 올바르지 않습니다.");
                    }

                    operations.Add(applyOperation);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(target.DownloadedPath)
                    || target.ExpectedSize < 0
                    || !IsValidSha256(target.ExpectedSha256)
                    || !string.Equals(
                        target.Reason,
                        UpdateApplyReasons.FullPackage,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Full Package 교체 대상 정보가 올바르지 않습니다.");
                }

                var expectedSourcePath = _pathService.ResolveJobFilePath(
                    jobDirectory,
                    "staging/" + relativePath.Replace('\\', '/'));
                var sourcePath = Path.GetFullPath(
                    target.DownloadedPath.Trim());

                if (!string.Equals(
                    sourcePath,
                    expectedSourcePath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Full Package staging 파일 경로가 계획과 일치하지 않습니다.");
                }

                VerifyFile(
                    sourcePath,
                    target.ExpectedSize,
                    target.ExpectedSha256);
                var isApplication = string.Equals(
                    relativePath.Replace('\\', '/'),
                    applicationFileName,
                    StringComparison.OrdinalIgnoreCase);

                if (isApplication)
                {
                    containsApplication = true;
                }

                applyOperation.SourcePath = sourcePath;
                applyOperation.ExpectedSize = target.ExpectedSize;
                applyOperation.ExpectedSha256 = target.ExpectedSha256.Trim();
                applyOperation.IsApplication = isApplication;
                operations.Add(applyOperation);
            }

            if (!containsApplication)
            {
                throw new InvalidDataException(
                    "Full Package 적용 대상에 프로그램 파일이 없습니다.");
            }

            operations.Sort((left, right) =>
                left.IsApplication.CompareTo(right.IsApplication));
            return operations;
        }

        private void ApplySingleOperation(
            ApplyOperation operation,
            IList<ApplyOperation> applied)
        {
            var destinationDirectory = Path.GetDirectoryName(
                operation.DestinationPath);

            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new InvalidDataException(
                    "적용 대상 디렉터리를 확인할 수 없습니다.");
            }

            Directory.CreateDirectory(destinationDirectory);
            operation.HadOriginal = File.Exists(operation.DestinationPath);

            if (operation.HadOriginal)
            {
                BackupOriginal(operation);
            }

            applied.Add(operation);

            if (string.Equals(
                operation.Operation,
                UpdateTargetOperations.Delete,
                StringComparison.Ordinal))
            {
                DeleteIfExists(operation.DestinationPath);

                if (File.Exists(operation.DestinationPath))
                {
                    throw new IOException(
                        "Full Package 삭제 대상 파일을 제거하지 못했습니다.");
                }

                return;
            }

            var temporaryPath = operation.DestinationPath
                + ".poscam-update.tmp";
            DeleteIfExists(temporaryPath);

            try
            {
                File.Copy(operation.SourcePath, temporaryPath, true);
                VerifyFile(
                    temporaryPath,
                    operation.ExpectedSize,
                    operation.ExpectedSha256);
                File.Copy(temporaryPath, operation.DestinationPath, true);
                VerifyFile(
                    operation.DestinationPath,
                    operation.ExpectedSize,
                    operation.ExpectedSha256);
            }
            finally
            {
                DeleteIfExists(temporaryPath);
            }
        }

        private void BackupOriginal(ApplyOperation operation)
        {
            var backupDirectory = Path.GetDirectoryName(
                operation.BackupPath);

            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new InvalidDataException(
                    "백업 대상 디렉터리를 확인할 수 없습니다.");
            }

            Directory.CreateDirectory(backupDirectory);
            File.Copy(
                operation.DestinationPath,
                operation.BackupPath,
                true);

            var backupFile = new FileInfo(operation.BackupPath);
            operation.OriginalSize = backupFile.Length;
            operation.OriginalSha256 =
                _hashCalculator.CalculateSha256(operation.BackupPath);
            VerifyFile(
                operation.BackupPath,
                operation.OriginalSize,
                operation.OriginalSha256);
        }

        private void RecoverPreviousApplication(
            string installDirectory,
            IList<ApplyOperation> applied,
            Action rollbackStateAction,
            Action restartAction,
            Action recoveryCompletedAction,
            Exception applyException)
        {
            var hadAppliedChanges = applied.Count > 0;

            try
            {
                RollbackAndVerify(applied);
                rollbackStateAction();

                UpdateClientLog.Info(
                    installDirectory,
                    hadAppliedChanges
                        ? "apply.rollback.verified"
                        : "apply.prechange.verified",
                    hadAppliedChanges
                        ? "Full Package 적용 실패 후 기존 파일과 설치 상태 복원을 완료했습니다."
                        : "파일 변경 전에 Full Package 적용이 실패하여 기존 설치 상태를 확인했습니다.");
            }
            catch (Exception rollbackException)
            {
                UpdateClientLog.Error(
                    installDirectory,
                    "apply.rollback.failed",
                    "Full Package 적용 실패 후 기존 파일 또는 설치 상태 복원에 실패했습니다.",
                    rollbackException);

                throw new IOException(
                    "Full Package 적용 실패 후 기존 파일 또는 설치 상태 복원도 완료하지 못했습니다.",
                    new AggregateException(
                        applyException,
                        rollbackException));
            }

            try
            {
                restartAction();

                UpdateClientLog.Info(
                    installDirectory,
                    hadAppliedChanges
                        ? "apply.rollback.restart.success"
                        : "apply.prechange.restart.success",
                    "현재 설치된 기존 프로그램을 다시 실행했습니다.");

                InvokeRecoveryCompletedAction(
                    installDirectory,
                    recoveryCompletedAction);
            }
            catch (Exception restartException)
            {
                UpdateClientLog.Error(
                    installDirectory,
                    "apply.recovery.restart.failed",
                    "기존 설치 상태는 확인했지만 프로그램을 다시 실행하지 못했습니다.",
                    restartException);

                throw new IOException(
                    "기존 파일 복원 후 프로그램을 다시 실행하지 못했습니다.",
                    new AggregateException(
                        applyException,
                        restartException));
            }
        }

        private static void InvokeRecoveryCompletedAction(
            string installDirectory,
            Action recoveryCompletedAction)
        {
            try
            {
                recoveryCompletedAction();
            }
            catch (Exception exception)
            {
                UpdateClientLog.Error(
                    installDirectory,
                    "apply.recovery.cleanup.failed",
                    "Full Package Rollback과 기존 프로그램 재실행은 완료했지만 작업 폴더 정리에 실패했습니다.",
                    exception);
            }
        }

        private void RollbackAndVerify(IList<ApplyOperation> applied)
        {
            Exception? rollbackFailure = null;

            for (var index = applied.Count - 1; index >= 0; index--)
            {
                var operation = applied[index];

                try
                {
                    if (operation.HadOriginal)
                    {
                        if (!File.Exists(operation.BackupPath))
                        {
                            throw new FileNotFoundException(
                                "Rollback 백업 파일을 찾을 수 없습니다.",
                                operation.BackupPath);
                        }

                        VerifyFile(
                            operation.BackupPath,
                            operation.OriginalSize,
                            operation.OriginalSha256);
                        File.Copy(
                            operation.BackupPath,
                            operation.DestinationPath,
                            true);
                        VerifyFile(
                            operation.DestinationPath,
                            operation.OriginalSize,
                            operation.OriginalSha256);
                    }
                    else
                    {
                        DeleteIfExists(operation.DestinationPath);

                        if (File.Exists(operation.DestinationPath))
                        {
                            throw new IOException(
                                "Rollback 대상 신규 파일을 삭제하지 못했습니다.");
                        }
                    }
                }
                catch (Exception exception)
                {
                    rollbackFailure = rollbackFailure == null
                        ? exception
                        : new AggregateException(
                            rollbackFailure,
                            exception);
                }
            }

            if (rollbackFailure != null)
            {
                throw new IOException(
                    "Full Package 적용 실패 후 rollback 또는 무결성 검증을 완료하지 못했습니다.",
                    rollbackFailure);
            }
        }

        private void VerifyFile(
            string path,
            long expectedSize,
            string expectedSha256)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "검증할 Full Package 파일을 찾을 수 없습니다.",
                    path);
            }

            var file = new FileInfo(path);

            if (file.Length != expectedSize)
            {
                throw new InvalidDataException(
                    "Full Package 파일 크기가 적용 계획과 다릅니다.");
            }

            var actualSha256 = _hashCalculator.CalculateSha256(path);

            if (!string.Equals(
                actualSha256,
                expectedSha256.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Full Package 파일 SHA-256이 적용 계획과 다릅니다.");
            }
        }

        private static string NormalizeOperation(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return UpdateTargetOperations.Replace;
            }

            var normalized = value.Trim();
            if (!string.Equals(
                    normalized,
                    UpdateTargetOperations.Replace,
                    StringComparison.Ordinal)
                && !string.Equals(
                    normalized,
                    UpdateTargetOperations.Delete,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "지원하지 않는 Full Package 작업 유형입니다.");
            }

            return normalized;
        }

        private static bool IsValidSha256(string? value)
        {
            if (value == null)
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

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private sealed class ApplyOperation
        {
            public string Operation { get; set; }
                = UpdateTargetOperations.Replace;

            public string SourcePath { get; set; } = "";

            public string DestinationPath { get; set; } = "";

            public string BackupPath { get; set; } = "";

            public long ExpectedSize { get; set; }

            public string ExpectedSha256 { get; set; } = "";

            public long OriginalSize { get; set; }

            public string OriginalSha256 { get; set; } = "";

            public bool HadOriginal { get; set; }

            public bool IsApplication { get; set; }
        }
    }
}
