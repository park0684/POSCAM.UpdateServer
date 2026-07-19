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
    public sealed class UpdateApplyWorkerServiceTests : IDisposable
    {
        private const string JobId = "worker-job-001";
        private readonly string _installDirectory;
        private readonly UpdateWorkPathService _pathService;
        private readonly UpdateApplyPlanStore _planStore;

        public UpdateApplyWorkerServiceTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.WorkerTests",
                Guid.NewGuid().ToString("N"));
            _pathService = new UpdateWorkPathService();
            _planStore = new UpdateApplyPlanStore();
            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public async Task ApplyAsync_ValidPlan_WaitsAppliesRestartsAndDeletesPlan()
        {
            var original = Encoding.UTF8.GetBytes("old app");
            var updated = Encoding.UTF8.GetBytes("new app");
            var appPath = Path.Combine(_installDirectory, "PcCam.exe");
            File.WriteAllBytes(appPath, original);
            var planPath = SavePlan(updated);
            var workerDirectory = CreateWorkerDirectory();
            var wait = new FakeProcessWaitService();
            var restart = new FakeApplicationRestartService();

            var exitCode = await CreateService(wait, restart).ApplyAsync(
                CreateOptions(planPath),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.Success, exitCode);
            Assert.Equal(4321, wait.LastProcessId);
            Assert.Equal(TimeSpan.FromSeconds(45), wait.LastTimeout);
            Assert.Equal(1, restart.CallCount);
            Assert.False(restart.LastSkipUpdateOnce);
            Assert.False(File.Exists(planPath));
            Assert.Equal(updated, File.ReadAllBytes(appPath));
            Assert.False(Directory.Exists(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    JobId)));
            Assert.False(Directory.Exists(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    JobId)));
            Assert.True(Directory.Exists(workerDirectory));
        }

        [Fact]
        public async Task ApplyAsync_ParentTimeout_RestartsPreviousAppWithoutChanges()
        {
            var original = Encoding.UTF8.GetBytes("old app");
            var appPath = Path.Combine(_installDirectory, "PcCam.exe");
            File.WriteAllBytes(appPath, original);
            var planPath = SavePlan(Encoding.UTF8.GetBytes("new app"));
            var wait = new FakeProcessWaitService
            {
                Result = false
            };
            var restart = new FakeApplicationRestartService();

            var exitCode = await CreateService(wait, restart).ApplyAsync(
                CreateOptions(planPath),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(original, File.ReadAllBytes(appPath));
            Assert.Equal(1, restart.CallCount);
            Assert.True(restart.LastSkipUpdateOnce);
            Assert.True(File.Exists(planPath));
        }

        [Fact]
        public async Task ApplyAsync_FileRepairPlan_RestartsSafePlanTargetAndReturnsApplyFailed()
        {
            var planPath = SavePlan(Encoding.UTF8.GetBytes("new app"));
            var plan = _planStore.Load(planPath);
            plan.Mode = UpdateApplyModes.FileRepair;
            _planStore.Save(planPath, plan);
            var restart = new FakeApplicationRestartService();

            var exitCode = await CreateService(
                new FakeProcessWaitService(),
                restart).ApplyAsync(
                CreateOptions(planPath),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(1, restart.CallCount);
            Assert.True(restart.LastSkipUpdateOnce);
            Assert.Equal("PcCam.exe", restart.LastApplicationFileName);
            Assert.True(File.Exists(planPath));
        }

        [Theory]
        [InlineData("CAMVIEWER", "x86")]
        [InlineData("PCCAM", "x64")]
        public async Task ApplyAsync_ProductIdentityMismatch_RestartsSafePlanTargetAndReturnsApplyFailed(
            string productCode,
            string architecture)
        {
            var planPath = SavePlan(Encoding.UTF8.GetBytes("new app"));
            var options = CreateOptions(planPath);
            options.ProductCode = productCode;
            options.Architecture = architecture;
            var restart = new FakeApplicationRestartService();

            var exitCode = await CreateService(
                new FakeProcessWaitService(),
                restart).ApplyAsync(
                options,
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(1, restart.CallCount);
            Assert.True(restart.LastSkipUpdateOnce);
            Assert.Equal("PcCam.exe", restart.LastApplicationFileName);
            Assert.True(File.Exists(planPath));
        }

        [Fact]
        public async Task ApplyAsync_RestartFailure_RollsBackAndRestartsPreviousVersion()
        {
            var original = Encoding.UTF8.GetBytes("old app");
            var appPath = Path.Combine(_installDirectory, "PcCam.exe");
            File.WriteAllBytes(appPath, original);
            var planPath = SavePlan(Encoding.UTF8.GetBytes("new app"));
            var workerDirectory = CreateWorkerDirectory();
            var restart = new FakeApplicationRestartService
            {
                ExceptionFactory = callCount => callCount == 1
                    ? new IOException("restart failed")
                    : null
            };

            var exitCode = await CreateService(
                new FakeProcessWaitService(),
                restart).ApplyAsync(
                CreateOptions(planPath),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(2, restart.CallCount);
            Assert.Equal(
                new[] { false, true },
                restart.SkipUpdateOnceValues);
            Assert.Equal(original, File.ReadAllBytes(appPath));
            Assert.False(File.Exists(planPath));
            Assert.False(Directory.Exists(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    JobId)));
            Assert.False(Directory.Exists(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    JobId)));
            Assert.True(Directory.Exists(workerDirectory));
        }

        [Fact]
        public async Task ApplyAsync_RollbackRecoveryFailure_PreservesPlanAndJob()
        {
            var original = Encoding.UTF8.GetBytes("old app");
            var appPath = Path.Combine(
                _installDirectory,
                "PcCam.exe");
            File.WriteAllBytes(appPath, original);
            var planPath = SavePlan(
                Encoding.UTF8.GetBytes("new app"));
            var workerDirectory = CreateWorkerDirectory();
            var restart = new FakeApplicationRestartService
            {
                ExceptionFactory = callCount =>
                    new IOException("restart failed " + callCount)
            };

            var exitCode = await CreateService(
                    new FakeProcessWaitService(),
                    restart)
                .ApplyAsync(
                    CreateOptions(planPath),
                    CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(2, restart.CallCount);
            Assert.Equal(
                new[] { false, true },
                restart.SkipUpdateOnceValues);
            Assert.Equal(original, File.ReadAllBytes(appPath));
            Assert.True(File.Exists(planPath));
            Assert.True(Directory.Exists(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    JobId)));
            Assert.True(Directory.Exists(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    JobId)));
            Assert.True(Directory.Exists(workerDirectory));
        }

        private UpdateApplyWorkerService CreateService(
            IProcessWaitService waitService,
            IApplicationRestartService restartService)
        {
            return new UpdateApplyWorkerService(
                _planStore,
                _pathService,
                waitService,
                new FullPackageApplyService(
                    _pathService,
                    new FileHashCalculator()),
                restartService);
        }

        private ApplyOptions CreateOptions(string planPath)
        {
            return new ApplyOptions
            {
                PlanPath = planPath,
                WaitProcessId = 4321,
                RestartFileName = "PcCam.exe",
                ProductCode = "PCCAM",
                Architecture = "x86",
                WaitTimeoutSeconds = 45
            };
        }

        private string SavePlan(byte[] updated)
        {
            var jobDirectory = _pathService.GetJobDirectory(
                _installDirectory,
                JobId);
            var stagedPath = _pathService.ResolveJobFilePath(
                jobDirectory,
                "staging/PcCam.exe");
            var directory = Path.GetDirectoryName(stagedPath);

            Assert.False(string.IsNullOrWhiteSpace(directory));
            Directory.CreateDirectory(directory!);
            File.WriteAllBytes(stagedPath, updated);

            var plan = new UpdateApplyPlan
            {
                JobId = JobId,
                ProductCode = "PCCAM",
                Architecture = "x86",
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                Mode = UpdateApplyModes.FullPackage,
                CreatedAtUtc = DateTime.UtcNow
            };

            plan.Targets.Add(new UpdateApplyTarget
            {
                RelativePath = "PcCam.exe",
                DownloadedPath = stagedPath,
                ExpectedSize = updated.LongLength,
                ExpectedSha256 = CalculateSha256(updated),
                Reason = UpdateApplyReasons.FullPackage
            });

            return _planStore.Save(
                _pathService.GetActivePlanPath(_installDirectory),
                plan);
        }

        private string CreateWorkerDirectory()
        {
            var workerDirectory = _pathService.GetWorkerDirectory(
                _installDirectory,
                JobId);
            Directory.CreateDirectory(workerDirectory);
            File.WriteAllText(
                Path.Combine(workerDirectory, "worker.exe"),
                "worker");
            return workerDirectory;
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
