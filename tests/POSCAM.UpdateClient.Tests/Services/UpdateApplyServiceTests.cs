using System;
using System.IO;
using System.IO.Compression;
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
            var workerLauncher = new FakeUpdateWorkerLauncherService();
            var service = CreateService(
                processWait,
                restart,
                workerLauncher);

            var exitCode = await service.ApplyAsync(
                CreateOptions(planPath),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.Success, exitCode);
            Assert.Equal(1234, processWait.LastProcessId);
            Assert.Equal(TimeSpan.FromSeconds(30), processWait.LastTimeout);
            Assert.Equal(1, restart.CallCount);
            Assert.Equal(0, workerLauncher.CallCount);
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
            var workerLauncher = new FakeUpdateWorkerLauncherService();

            var exitCode = await CreateService(
                    processWait,
                    restart,
                    workerLauncher)
                .ApplyAsync(
                    CreateOptions(planPath),
                    CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(original, File.ReadAllBytes(destination));
            Assert.Equal(0, restart.CallCount);
            Assert.Equal(0, workerLauncher.CallCount);
            Assert.True(File.Exists(planPath));
        }

        [Fact]
        public async Task ApplyAsync_RestartNameMismatch_RestartsSafePlanTargetAndReturnsApplyFailed()
        {
            var planPath = SaveFileRepairPlan(
                "provider.dll",
                Encoding.UTF8.GetBytes("updated"));
            var options = CreateOptions(planPath);
            options.RestartFileName = "Other.exe";
            var restart = new FakeApplicationRestartService();
            var workerLauncher = new FakeUpdateWorkerLauncherService();

            var exitCode = await CreateService(
                new FakeProcessWaitService(),
                restart,
                workerLauncher).ApplyAsync(
                options,
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(1, restart.CallCount);
            Assert.Equal("PcCam.exe", restart.LastApplicationFileName);
            Assert.Equal(0, workerLauncher.CallCount);
            Assert.True(File.Exists(planPath));
        }

        [Theory]
        [InlineData("CAMVIEWER", "x86")]
        [InlineData("PCCAM", "x64")]
        public async Task ApplyAsync_ProductIdentityMismatch_RestartsSafePlanTargetAndReturnsApplyFailed(
            string productCode,
            string architecture)
        {
            var planPath = SaveFileRepairPlan(
                "provider.dll",
                Encoding.UTF8.GetBytes("updated"));
            var options = CreateOptions(planPath);
            options.ProductCode = productCode;
            options.Architecture = architecture;
            var restart = new FakeApplicationRestartService();
            var workerLauncher = new FakeUpdateWorkerLauncherService();

            var exitCode = await CreateService(
                new FakeProcessWaitService(),
                restart,
                workerLauncher).ApplyAsync(
                options,
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(1, restart.CallCount);
            Assert.Equal("PcCam.exe", restart.LastApplicationFileName);
            Assert.Equal(0, workerLauncher.CallCount);
            Assert.True(File.Exists(planPath));
        }

        [Fact]
        public async Task ApplyAsync_FullPackage_StagesSavesAndLaunchesWorker()
        {
            var planPath = SaveFullPackagePlan();
            var restart = new FakeApplicationRestartService();
            var workerLauncher = new FakeUpdateWorkerLauncherService();

            var exitCode = await CreateService(
                new FakeProcessWaitService(),
                restart,
                workerLauncher).ApplyAsync(
                CreateOptions(planPath),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.Success, exitCode);
            Assert.Equal(0, restart.CallCount);
            Assert.Equal(1, workerLauncher.CallCount);
            Assert.Equal(_installDirectory, workerLauncher.InstallDirectory);
            Assert.Equal(JobId, workerLauncher.JobId);
            Assert.Equal(planPath, workerLauncher.PlanPath);
            Assert.Equal("PCCAM", workerLauncher.ProductCode);
            Assert.Equal("x86", workerLauncher.Architecture);
            Assert.Equal("PcCam.exe", workerLauncher.RestartFileName);
            Assert.Equal(30, workerLauncher.WaitTimeoutSeconds);
            Assert.Equal(5678, workerLauncher.ParentProcessId);
            Assert.True(File.Exists(planPath));

            var persistedPlan = _planStore.Load(planPath);
            Assert.Equal("PCCAM", persistedPlan.ProductCode);
            Assert.Equal("x86", persistedPlan.Architecture);
            Assert.Equal(UpdateApplyModes.FullPackage, persistedPlan.Mode);
            Assert.Equal(2, persistedPlan.Targets.Count);

            foreach (var target in persistedPlan.Targets)
            {
                Assert.True(File.Exists(target.DownloadedPath));
                Assert.Equal(UpdateApplyReasons.FullPackage, target.Reason);
            }
        }

        [Fact]
        public async Task ApplyAsync_FullPackageWorkerLaunchFailure_RestartsPreviousAppAndKeepsPlan()
        {
            var planPath = SaveFullPackagePlan();
            var restart = new FakeApplicationRestartService();
            var workerLauncher = new FakeUpdateWorkerLauncherService
            {
                ExceptionToThrow = new IOException("launch failed")
            };

            var exitCode = await CreateService(
                new FakeProcessWaitService(),
                restart,
                workerLauncher).ApplyAsync(
                CreateOptions(planPath),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyFailed, exitCode);
            Assert.Equal(1, restart.CallCount);
            Assert.Equal(0, workerLauncher.CallCount);
            Assert.True(File.Exists(planPath));
            Assert.Equal(2, _planStore.Load(planPath).Targets.Count);
        }

        private UpdateApplyService CreateService(
            IProcessWaitService processWaitService,
            IApplicationRestartService restartService,
            IUpdateWorkerLauncherService workerLauncherService)
        {
            var hashCalculator = new FileHashCalculator();

            return new UpdateApplyService(
                _planStore,
                _pathService,
                processWaitService,
                new FileRepairApplyService(
                    _pathService,
                    hashCalculator),
                new FullPackageStagingService(
                    _pathService,
                    hashCalculator),
                workerLauncherService,
                restartService,
                () => 5678);
        }

        private ApplyOptions CreateOptions(string planPath)
        {
            return new ApplyOptions
            {
                PlanPath = planPath,
                WaitProcessId = 1234,
                RestartFileName = "PcCam.exe",
                ProductCode = "PCCAM",
                Architecture = "x86",
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
                ProductCode = "PCCAM",
                Architecture = "x86",
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

        private string SaveFullPackagePlan()
        {
            var jobDirectory = _pathService.GetJobDirectory(
                _installDirectory,
                JobId);
            var packagePath = _pathService.ResolveJobFilePath(
                jobDirectory,
                "package/pccam.zip");
            var directory = Path.GetDirectoryName(packagePath);

            Assert.False(string.IsNullOrWhiteSpace(directory));
            Directory.CreateDirectory(directory!);

            using (var stream = new FileStream(
                packagePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None))
            using (var archive = new ZipArchive(
                stream,
                ZipArchiveMode.Create,
                false))
            {
                AddZipEntry(archive, "PcCam.exe", "new app");
                AddZipEntry(
                    archive,
                    "providers/provider.dll",
                    "new provider");
            }

            var plan = new UpdateApplyPlan
            {
                JobId = JobId,
                ProductCode = "PCCAM",
                Architecture = "x86",
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                Mode = UpdateApplyModes.FullPackage,
                CreatedAtUtc = DateTime.UtcNow,
                PackageType = "full",
                PackageFileName = "pccam.zip",
                PackagePath = packagePath,
                PackageSize = new FileInfo(packagePath).Length,
                PackageSha256 = CalculateSha256(packagePath)
            };

            return _planStore.Save(
                _pathService.GetActivePlanPath(_installDirectory),
                plan);
        }

        private static void AddZipEntry(
            ZipArchive archive,
            string path,
            string content)
        {
            var entry = archive.CreateEntry(path);

            using (var writer = new StreamWriter(
                entry.Open(),
                new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private static string CalculateSha256(byte[] content)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(content);
                return ToHex(hash);
            }
        }

        private static string CalculateSha256(string path)
        {
            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (var sha256 = SHA256.Create())
            {
                return ToHex(sha256.ComputeHash(stream));
            }
        }

        private static string ToHex(byte[] hash)
        {
            var builder = new StringBuilder(hash.Length * 2);

            foreach (var value in hash)
            {
                builder.Append(value.ToString("X2"));
            }

            return builder.ToString();
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
