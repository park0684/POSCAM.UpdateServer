using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 검증된 복구 파일을 설치 경로에 적용하고 실패 시 원래 상태로 되돌린다.
    /// </summary>
    internal sealed class FileRepairApplyService
    {
        private readonly UpdateWorkPathService _pathService;
        private readonly FileHashCalculator _hashCalculator;

        public FileRepairApplyService(
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

            var operations = BuildOperations(plan);
            var applied = new List<ApplyOperation>();

            try
            {
                foreach (var operation in operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ApplyOperation(operation, applied);
                }

                cancellationToken.ThrowIfCancellationRequested();
                restartAction();
            }
            catch
            {
                Rollback(applied);
                throw;
            }
        }

        private List<ApplyOperation> BuildOperations(UpdateApplyPlan plan)
        {
            if (!string.Equals(
                plan.Mode,
                UpdateApplyModes.FileRepair,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "파일 복구 적용 계획이 아닙니다.");
            }

            if (string.IsNullOrWhiteSpace(plan.InstallDirectory)
                || string.IsNullOrWhiteSpace(plan.JobId)
                || plan.Targets == null
                || plan.Targets.Count == 0)
            {
                throw new InvalidDataException(
                    "파일 복구 적용 계획이 올바르지 않습니다.");
            }

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

            foreach (var target in plan.Targets)
            {
                if (target == null
                    || string.IsNullOrWhiteSpace(target.RelativePath)
                    || string.IsNullOrWhiteSpace(target.DownloadedPath)
                    || target.ExpectedSize < 0
                    || !IsValidSha256(target.ExpectedSha256))
                {
                    throw new InvalidDataException(
                        "파일 복구 대상 정보가 올바르지 않습니다.");
                }

                var relativePath = target.RelativePath;
                var destinationPath = _pathService.ResolveInstallFilePath(
                    plan.InstallDirectory,
                    relativePath);
                var expectedDownloadedPath = _pathService.ResolveJobFilePath(
                    jobDirectory,
                    "files/" + relativePath.Replace('\\', '/'));
                var downloadedPath = Path.GetFullPath(
                    target.DownloadedPath.Trim());

                if (!string.Equals(
                    downloadedPath,
                    expectedDownloadedPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "다운로드 파일 경로가 적용 계획과 일치하지 않습니다.");
                }

                if (string.Equals(
                    destinationPath,
                    currentExecutable,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "실행 중인 UpdateClient는 직접 교체할 수 없습니다.");
                }

                if (!destinations.Add(destinationPath))
                {
                    throw new InvalidDataException(
                        "중복된 파일 복구 대상이 있습니다.");
                }

                VerifyFile(
                    downloadedPath,
                    target.ExpectedSize,
                    target.ExpectedSha256);

                operations.Add(new ApplyOperation
                {
                    SourcePath = downloadedPath,
                    DestinationPath = destinationPath,
                    BackupPath = _pathService.ResolveBackupFilePath(
                        backupDirectory,
                        relativePath),
                    ExpectedSize = target.ExpectedSize,
                    ExpectedSha256 = target.ExpectedSha256.Trim()
                });
            }

            return operations;
        }

        private void ApplyOperation(
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

        private void Rollback(IList<ApplyOperation> applied)
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

                        File.Copy(
                            operation.BackupPath,
                            operation.DestinationPath,
                            true);
                    }
                    else
                    {
                        DeleteIfExists(operation.DestinationPath);
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
                    "업데이트 적용 실패 후 rollback도 완료하지 못했습니다.",
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
                    "검증할 업데이트 파일을 찾을 수 없습니다.",
                    path);
            }

            var file = new FileInfo(path);

            if (file.Length != expectedSize)
            {
                throw new InvalidDataException(
                    "업데이트 파일 크기가 적용 계획과 다릅니다.");
            }

            var actualSha256 = _hashCalculator.CalculateSha256(path);

            if (!string.Equals(
                actualSha256,
                expectedSha256.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "업데이트 파일 SHA-256이 적용 계획과 다릅니다.");
            }
        }

        private static bool IsValidSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
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

            public bool HadOriginal { get; set; }
        }
    }
}
