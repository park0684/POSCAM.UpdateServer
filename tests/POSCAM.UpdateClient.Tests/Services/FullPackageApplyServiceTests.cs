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
    public sealed class FullPackageApplyServiceTests : IDisposable
    {
        private const string JobId = "full-apply-job-001";
        private readonly string _installDirectory;
        private readonly UpdateWorkPathService _pathService;

        public FullPackageApplyServiceTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.FullPackageApplyTests",
                Guid.NewGuid().ToString("N"));
            _pathService = new UpdateWorkPathService();
            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public void ApplyAndRestart_ValidTargets_ReplacesBacksUpAndCreates()
        {
            var originalApp = Encoding.UTF8.GetBytes("old app");
            var newApp = Encoding.UTF8.GetBytes("new app");
            var newProvider = Encoding.UTF8.GetBytes("new provider");
            var appPath = WriteInstallFile("PcCam.exe", originalApp);
            var plan = CreatePlan("PcCam.exe", newApp);
            AddTarget(plan, "providers/provider.dll", newProvider);
            var restarted = false;

            CreateService().ApplyAndRestart(
                plan,
                () => restarted = true,
                CancellationToken.None);

            Assert.True(restarted);
            Assert.Equal(newApp, File.ReadAllBytes(appPath));
            Assert.Equal(
                newProvider,
                File.ReadAllBytes(_pathService.ResolveInstallFilePath(
                    _installDirectory,
                    "providers/provider.dll")));
            var backupPath = _pathService.ResolveBackupFilePath(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    JobId),
                "PcCam.exe");

            Assert.Equal(originalApp, File.ReadAllBytes(backupPath));
        }

        [Fact]
        public void ApplyAndRestart_RestartFailure_RollsBackVerifiesAndRestartsPreviousApp()
        {
            var originalApp = Encoding.UTF8.GetBytes("old app");
            var appPath = WriteInstallFile("PcCam.exe", originalApp);
            var plan = CreatePlan(
                "PcCam.exe",
                Encoding.UTF8.GetBytes("new app"));
            AddTarget(
                plan,
                "providers/provider.dll",
                Encoding.UTF8.GetBytes("new provider"));
            var restartCount = 0;

            Assert.Throws<IOException>(() => CreateService().ApplyAndRestart(
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
            Assert.Equal(originalApp, File.ReadAllBytes(appPath));
            Assert.False(File.Exists(_pathService.ResolveInstallFilePath(
                _installDirectory,
                "providers/provider.dll")));
        }

        [Fact]
        public void ApplyAndRestart_RollbackBackupTampered_DoesNotRestartUnverifiedApp()
        {
            var originalApp = Encoding.UTF8.GetBytes("old app");
            var newApp = Encoding.UTF8.GetBytes("new app");
            var appPath = WriteInstallFile("PcCam.exe", originalApp);
            var plan = CreatePlan("PcCam.exe", newApp);
            var backupPath = _pathService.ResolveBackupFilePath(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    JobId),
                "PcCam.exe");
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
            Assert.Equal(newApp, File.ReadAllBytes(appPath));
        }

        [Fact]
        public void ApplyAndRestart_StagingHashMismatch_RestartsPreviousAppWithoutChanges()
        {
            var originalApp = Encoding.UTF8.GetBytes("old app");
            var appPath = WriteInstallFile("PcCam.exe", originalApp);
            var plan = CreatePlan(
                "PcCam.exe",
                Encoding.UTF8.GetBytes("new app"));
            File.WriteAllText(
                plan.Targets[0].DownloadedPath,
                "tampered");
            var restarted = false;

            Assert.Throws<InvalidDataException>(() =>
                CreateService().ApplyAndRestart(
                    plan,
                    () => restarted = true,
                    CancellationToken.None));

            Assert.True(restarted);
            Assert.Equal(originalApp, File.ReadAllBytes(appPath));
        }

        [Fact]
        public void ApplyAndRestart_StagingPathMismatch_IsRejected()
        {
            var plan = CreatePlan(
                "PcCam.exe",
                Encoding.UTF8.GetBytes("new app"));
            plan.Targets[0].DownloadedPath = Path.Combine(
                _installDirectory,
                "outside.exe");

            Assert.Throws<InvalidDataException>(() =>
                CreateService().ApplyAndRestart(
                    plan,
                    () => { },
                    CancellationToken.None));
        }

        [Fact]
        public void ApplyAndRestart_DuplicateDestination_IsRejected()
        {
            var content = Encoding.UTF8.GetBytes("new app");
            var plan = CreatePlan("PcCam.exe", content);
            AddTarget(plan, "PCCAM.EXE", content);

            Assert.Throws<InvalidDataException>(() =>
                CreateService().ApplyAndRestart(
                    plan,
                    () => { },
                    CancellationToken.None));
        }

        [Fact]
        public void ApplyAndRestart_WithoutApplicationTarget_IsRejected()
        {
            var plan = CreatePlan(
                "PcCam.exe",
                Encoding.UTF8.GetBytes("new app"));
            plan.Targets.Clear();
            AddTarget(
                plan,
                "providers/provider.dll",
                Encoding.UTF8.GetBytes("provider"));

            Assert.Throws<InvalidDataException>(() =>
                CreateService().ApplyAndRestart(
                    plan,
                    () => { },
                    CancellationToken.None));
        }

        private FullPackageApplyService CreateService()
        {
            return new FullPackageApplyService(
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
                Mode = UpdateApplyModes.FullPackage,
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
            var sourcePath = _pathService.ResolveJobFilePath(
                jobDirectory,
                "staging/" + relativePath.Replace('\\', '/'));
            var directory = Path.GetDirectoryName(sourcePath);

            Assert.False(string.IsNullOrWhiteSpace(directory));
            Directory.CreateDirectory(directory!);
            File.WriteAllBytes(sourcePath, content);

            plan.Targets.Add(new UpdateApplyTarget
            {
                RelativePath = relativePath,
                DownloadedPath = sourcePath,
                ExpectedSize = content.LongLength,
                ExpectedSha256 = CalculateSha256(content),
                Reason = UpdateApplyReasons.FullPackage
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
