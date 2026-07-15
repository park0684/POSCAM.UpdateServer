using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class FileRepairApplyServiceTests : IDisposable
    {
        private const string JobId = "job-001";
        private readonly string _installDirectory;
        private readonly UpdateWorkPathService _pathService;

        public FileRepairApplyServiceTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.ApplyTests",
                Guid.NewGuid().ToString("N"));
            _pathService = new UpdateWorkPathService();
            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public void ApplyAndRestart_ExistingFile_ReplacesAndBacksUp()
        {
            var relativePath = Path.Combine("providers", "provider.dll");
            var original = Encoding.UTF8.GetBytes("original");
            var updated = Encoding.UTF8.GetBytes("updated");
            var destination = WriteInstallFile(relativePath, original);
            var plan = CreatePlan(relativePath, updated);
            var restarted = false;

            CreateService().ApplyAndRestart(
                plan,
                () => restarted = true,
                CancellationToken.None);

            Assert.True(restarted);
            Assert.Equal(updated, File.ReadAllBytes(destination));

            var backupPath = _pathService.ResolveBackupFilePath(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    JobId),
                relativePath);

            Assert.Equal(original, File.ReadAllBytes(backupPath));
        }

        [Fact]
        public void ApplyAndRestart_MissingFile_CreatesFile()
        {
            var relativePath = Path.Combine("resources", "model.dat");
            var updated = Encoding.UTF8.GetBytes("new content");
            var plan = CreatePlan(relativePath, updated);

            CreateService().ApplyAndRestart(
                plan,
                () => { },
                CancellationToken.None);

            var destination = _pathService.ResolveInstallFilePath(
                _installDirectory,
                relativePath);

            Assert.Equal(updated, File.ReadAllBytes(destination));
        }

        [Fact]
        public void ApplyAndRestart_RestartFailure_RollsBackVerifiesAndRestartsPreviousApp()
        {
            var existingPath = Path.Combine("providers", "provider.dll");
            var missingPath = Path.Combine("resources", "new.dat");
            var original = Encoding.UTF8.GetBytes("original");
            var existingDestination = WriteInstallFile(
                existingPath,
                original);
            var plan = CreatePlan(
                existingPath,
                Encoding.UTF8.GetBytes("updated"));
            AddTarget(
                plan,
                missingPath,
                Encoding.UTF8.GetBytes("created"));
            var restartCount = 0;

            Assert.Throws<IOException>(
                () => CreateService().ApplyAndRestart(
                    plan,
                    () =>
                    {
                        restartCount++;

                        if (restartCount == 1)
                        {
                            throw new IOException("restart failed");
                        }
                    },
                    CancellationToken.None));

            Assert.Equal(2, restartCount);
            Assert.Equal(original, File.ReadAllBytes(existingDestination));
            Assert.False(File.Exists(
                _pathService.ResolveInstallFilePath(
                    _installDirectory,
                    missingPath)));
        }

        [Fact]
        public void ApplyAndRestart_RollbackBackupTampered_DoesNotRestartUnverifiedApp()
        {
            var relativePath = Path.Combine("providers", "provider.dll");
            var original = Encoding.UTF8.GetBytes("original");
            var updated = Encoding.UTF8.GetBytes("updated");
            var destination = WriteInstallFile(relativePath, original);
            var plan = CreatePlan(relativePath, updated);
            var backupPath = _pathService.ResolveBackupFilePath(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    JobId),
                relativePath);
            var restartCount = 0;

            Assert.Throws<IOException>(() => CreateService().ApplyAndRestart(
                plan,
                () =>
                {
                    restartCount++;
                    File.WriteAllText(backupPath, "tampered");
                    throw new IOException("restart failed");
                },
                CancellationToken.None));

            Assert.Equal(1, restartCount);
            Assert.Equal(updated, File.ReadAllBytes(destination));
        }

        [Fact]
        public void ApplyAndRestart_DownloadHashMismatch_RestartsPreviousAppWithoutChanges()
        {
            var relativePath = "PcCam.config";
            var original = Encoding.UTF8.GetBytes("original");
            var destination = WriteInstallFile(relativePath, original);
            var updated = Encoding.UTF8.GetBytes("updated");
            var plan = CreatePlan(relativePath, updated);
            plan.Targets[0].ExpectedSha256 = new string('A', 64);
            var restarted = false;

            Assert.Throws<InvalidDataException>(
                () => CreateService().ApplyAndRestart(
                    plan,
                    () => restarted = true,
                    CancellationToken.None));

            Assert.True(restarted);
            Assert.Equal(original, File.ReadAllBytes(destination));
        }

        [Fact]
        public void ApplyAndRestart_UnsafeRelativePath_IsRejected()
        {
            var plan = CreatePlan(
                "safe.dll",
                Encoding.UTF8.GetBytes("content"));
            plan.Targets[0].RelativePath = "../outside.dll";

            Assert.Throws<InvalidDataException>(
                () => CreateService().ApplyAndRestart(
                    plan,
                    () => { },
                    CancellationToken.None));
        }

        [Fact]
        public void ApplyAndRestart_DuplicateDestination_IsRejected()
        {
            var content = Encoding.UTF8.GetBytes("content");
            var plan = CreatePlan("provider.dll", content);
            AddTarget(plan, "PROVIDER.DLL", content);

            Assert.Throws<InvalidDataException>(
                () => CreateService().ApplyAndRestart(
                    plan,
                    () => { },
                    CancellationToken.None));
        }

        private FileRepairApplyService CreateService()
        {
            return new FileRepairApplyService(
                _pathService,
                new FileHashCalculator());
        }

        private UpdateApplyPlan CreatePlan(
            string relativePath,
            byte[] content)
        {
            var plan = new UpdateApplyPlan
            {
                JobId = JobId,
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                Mode = UpdateApplyModes.FileRepair,
                CreatedAtUtc = DateTime.UtcNow
            };

            AddTarget(plan, relativePath, content);
            return plan;
        }

        private void AddTarget(
            UpdateApplyPlan plan,
            string relativePath,
            byte[] content)
        {
            var jobDirectory = _pathService.GetJobDirectory(
                _installDirectory,
                JobId);
            var downloadedPath = _pathService.ResolveJobFilePath(
                jobDirectory,
                "files/" + relativePath.Replace('\\', '/'));
            var directory = Path.GetDirectoryName(downloadedPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(downloadedPath, content);

            plan.Targets.Add(new UpdateApplyTarget
            {
                RelativePath = relativePath,
                DownloadedPath = downloadedPath,
                ExpectedSize = content.LongLength,
                ExpectedSha256 = CalculateSha256(content),
                Reason = RepairReasons.Missing
            });
        }

        private string WriteInstallFile(
            string relativePath,
            byte[] content)
        {
            var path = _pathService.ResolveInstallFilePath(
                _installDirectory,
                relativePath);
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, content);
            return path;
        }

        private static string CalculateSha256(byte[] content)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(content);
                var builder = new StringBuilder(hash.Length * 2);

                foreach (var value in hash)
                {
                    builder.Append(value.ToString("X2"));
                }

                return builder.ToString();
            }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_installDirectory))
                {
                    Directory.Delete(_installDirectory, true);
                }
            }
            catch
            {
                // 테스트 정리 실패가 테스트 결과를 덮어쓰지 않도록 한다.
            }
        }
    }
}
