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
    /// 기존 파일은 작업별 백업 경로에 보관하고 실패 시 역순으로 복구한다.
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
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (restartAction == null)
            {
                throw new ArgumentNullException(nameof(restartAction));
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
                restartAction();
            }
            catch (Exception applyException)
            {
                RecoverPreviousApplication(
                    plan.InstallDirectory,
                    applied,
                    restartAction,
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
                    || string.IsNullOrWhiteSpace(target.RelativePath)
                    || string.IsNullOrWhiteSpace(target.DownloadedPath)
                    || target.ExpectedSize < 0
                    || !IsValidSha256(target.ExpectedSha256)
                    || !string.Equals(
                        target.Reason,
                        UpdateApplyReasons.FullPackage,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Full Package 파일 대상 정보가 올바르지 않습니다.");
                }

                var relativePath = target.RelativePath;
                var destinationPath = _pathService.ResolveInstallFilePath(
                    plan.InstallDirectory,
                    relativePath);
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

                if (string.Equals(
                    destinationPath,
                    currentExecutable,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "실행 중인 worker 파일은 직접 교체할 수 없습니다.");
                }

                if (!destinations.Add(destinationPath))
                {
                    throw new InvalidDataException(
                        "Full Package에 중복된 교체 대상이 있습니다.");
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

                operations.Add(new ApplyOperation
                {
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath,
                    BackupPath = _pathService.ResolveBackupFilePath(
                        backupDirectory,
                        relativePath),
                    ExpectedSize = target.ExpectedSize,
                    ExpectedSha256 = target.ExpectedSha256.Trim(),
                    IsApplication = isApplication
                });
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
                    "교체 대상 디렉터리를 확인할 수 없습니다.");
            }

            Directory.CreateDirectory(destinationDirectory);
            operation.HadOriginal = File.Exists(operation.DestinationPath);

            if (operation.HadOriginal)
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

            applied.Add(operation);

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

        private void RecoverPreviousApplication(
            string installDirectory,
            IList<ApplyOperation> applied,
            Action restartAction,
            Exception applyException)
        {
            var hadAppliedChanges = applied.Count > 0;

            try
            {
                RollbackAndVerify(applied);

                UpdateClientLog.Info(
                    installDirectory,
                    hadAppliedChanges
                        ? "apply.rollback.verified"
                        : "apply.prechange.verified",
                    hadAppliedChanges
                        ? "Full Package 적용 실패 후 기존 파일 복원과 무결성 검증을 완료했습니다."
                        : "파일 변경 전에 Full Package 적용이 실패하여 기존 설치 상태를 확인했습니다.");
            }
            catch (Exception rollbackException)
            {
                UpdateClientLog.Error(
                    installDirectory,
                    "apply.rollback.failed",
                    "Full Package 적용 실패 후 기존 파일 복원 또는 무결성 검증에 실패했습니다.",
                    rollbackException);

                throw new IOException(
                    "Full Package 적용 실패 후 기존 파일 복원 또는 무결성 검증도 완료하지 못했습니다.",
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
