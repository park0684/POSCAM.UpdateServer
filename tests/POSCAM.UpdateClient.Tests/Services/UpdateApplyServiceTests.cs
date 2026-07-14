using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using POSCAM.UpdateClient.Tests.TestDoubles;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class UpdateApplyServiceTests : IDisposable
    {
        private const string JobId = "apply-job-001";
        private readonly string _installDirectory;
        private readonly UpdateWorkPathService _pathService;
        private readonly UpdateApplyPlanStore _planStore;

        public UpdateApplyServiceTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.ApplyOrchestrationTests",
                Guid.NewGuid().ToString("N"));
            _pathService = new UpdateWorkPathService();
            _planStore = new UpdateApplyPlanStore();
            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public async Task ApplyAsync_ValidFileRepair_AppliesRestartsAndDeletesPlan()
        {
            var updated = Encoding.UTF8.GetBytes("updated content");
            var planPath = SaveFileRepairPlan("provider.dll", updated);
            var processWait = new FakeProcessWaitService();
            var restart = new FakeApplicationRestartService();
            var service = CreateService(processWait, restart);

            var exitCode = await service.ApplyAsync(
                CreateOptions(planPath),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.Success, exitCode);
            Assert.Equal(1234, processWait.LastProcessId);
            Assert.Equal(TimeSpan.FromSeconds(30), processWait.LastTimeout);
            Assert.Equal(1, restart.CallCount);
            Assert.False(File.Exists(planPath));
            Assert.Equal(
                updated,
                File.ReadAllBytes(Path.Combine(
                    _installDirectory,
                    "provider.dll")));
        }

        [Fact]
        public async Task ApplyAsync_ProcessTimeout_ReturnsApplyFailedWithoutChanges()
        {
            var original = Encoding.UTF8.GetBytes("original");
            var destination = Path.Combine(_installDirectory, "provider.dll");
            File.WriteAllBytes(destination, original);
            var planPath = SaveFileRepairPlan(
                "provider.dll",
                Encoding.UTF8.GetBytes("updated"));
            var processWait = new FakeProcessWaitService
            {
                Result = false
            };
            var restart = new FakeApplicationRestartService();

            var exitCode = await CreateService(processWait, restart)
                .ApplyAsync(
                    CreateOptions(planPath),
                    CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(original, File.ReadAllBytes(destination));
            Assert.Equal(0, restart.CallCount);
            Assert.True(File.Exists(planPath));
        }

        [Fact]
        public async Task ApplyAsync_RestartNameMismatch_ReturnsApplyFailed()
        {
            var planPath = SaveFileRepairPlan(
                "provider.dll",
                Encoding.UTF8.GetBytes("updated"));
            var options = CreateOptions(planPath);
            options.RestartFileName = "Other.exe";
            var restart = new FakeApplicationRestartService();

            var exitCode = await CreateService(
                new FakeProcessWaitService(),
                restart).ApplyAsync(
                    options,
                    CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(0, restart.CallCount);
            Assert.True(File.Exists(planPath));
        }

        [Fact]
        public async Task ApplyAsync_FullPackagePlan_IsNotAppliedInThisSlice()
        {
            var plan = new UpdateApplyPlan
            {
                JobId = JobId,
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                Mode = UpdateApplyModes.FullPackage,
                CreatedAtUtc = DateTime.UtcNow
            };
            var planPath = _pathService.GetActivePlanPath(
                _installDirectory);
            _planStore.Save(planPath, plan);
            var restart = new FakeApplicationRestartService();

            var exitCode = await CreateService(
                new FakeProcessWaitService(),
                restart).ApplyAsync(
                    CreateOptions(planPath),
                    CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(0, restart.CallCount);
            Assert.True(File.Exists(planPath));
        }

        private UpdateApplyService CreateService(
            IProcessWaitService processWaitService,
            IApplicationRestartService restartService)
        {
            return new UpdateApplyService(
                _planStore,
                _pathService,
                processWaitService,
                new FileRepairApplyService(
                    _pathService,
                    new FileHashCalculator()),
                restartService);
        }

        private ApplyOptions CreateOptions(string planPath)
        {
            return new ApplyOptions
            {
                PlanPath = planPath,
                WaitProcessId = 1234,
                RestartFileName = "PcCam.exe",
                WaitTimeoutSeconds = 30
            };
        }

        private string SaveFileRepairPlan(
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

            var plan = new UpdateApplyPlan
            {
                JobId = JobId,
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                Mode = UpdateApplyModes.FileRepair,
                CreatedAtUtc = DateTime.UtcNow
            };

            plan.Targets.Add(new UpdateApplyTarget
            {
                RelativePath = relativePath,
                DownloadedPath = downloadedPath,
                ExpectedSize = content.LongLength,
                ExpectedSha256 = CalculateSha256(content),
                Reason = RepairReasons.Missing
            });

            var planPath = _pathService.GetActivePlanPath(
                _installDirectory);
            return _planStore.Save(planPath, plan);
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
