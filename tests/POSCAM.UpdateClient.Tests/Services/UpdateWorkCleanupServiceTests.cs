using System;
using System.IO;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class UpdateWorkCleanupServiceTests : IDisposable
    {
        private const string JobId = "cleanup-job-001";
        private readonly string _installDirectory;
        private readonly UpdateWorkPathService _pathService;
        private readonly UpdateApplyPlanStore _planStore;
        private readonly UpdateWorkCleanupService _service;

        public UpdateWorkCleanupServiceTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.CleanupTests",
                Guid.NewGuid().ToString("N"));
            _pathService = new UpdateWorkPathService();
            _planStore = new UpdateApplyPlanStore();
            _service = new UpdateWorkCleanupService(
                _pathService,
                _planStore);
            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public void CleanupCompletedJob_RemovesMatchingPlanAndJobFolders()
        {
            var plan = SavePlan(JobId);
            var planPath = _pathService.GetActivePlanPath(
                _installDirectory);
            var downloadDirectory = CreateJobDirectory(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    JobId));
            var backupDirectory = CreateJobDirectory(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    JobId));

            _service.CleanupCompletedJob(
                plan,
                planPath,
                false);

            Assert.False(File.Exists(planPath));
            Assert.False(Directory.Exists(downloadDirectory));
            Assert.False(Directory.Exists(backupDirectory));
            Assert.False(Directory.Exists(
                _pathService.GetUpdateRootDirectory(
                    _installDirectory)));
        }

        [Fact]
        public void CleanupCompletedJob_DoesNotDeleteAnotherJobsPlan()
        {
            var completedPlan = new UpdateApplyPlan
            {
                JobId = JobId,
                InstallDirectory = _installDirectory
            };
            var activePlan = SavePlan("active-job-002");
            var planPath = _pathService.GetActivePlanPath(
                _installDirectory);
            var completedDirectory = CreateJobDirectory(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    JobId));
            var activeDirectory = CreateJobDirectory(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    activePlan.JobId));

            _service.CleanupCompletedJob(
                completedPlan,
                planPath,
                false);

            Assert.True(File.Exists(planPath));
            Assert.Equal(
                activePlan.JobId,
                _planStore.Load(planPath).JobId);
            Assert.False(Directory.Exists(completedDirectory));
            Assert.True(Directory.Exists(activeDirectory));
        }

        [Fact]
        public void CleanupCompletedJob_PreservesWorkerAndFallbackMarker()
        {
            var plan = SavePlan(JobId);
            var planPath = _pathService.GetActivePlanPath(
                _installDirectory);
            var workerDirectory = CreateJobDirectory(
                _pathService.GetWorkerDirectory(
                    _installDirectory,
                    JobId));
            var stateDirectory = _pathService.GetStateDirectory(
                _installDirectory);
            var fallbackPath = Path.Combine(
                stateDirectory,
                "full-package-fallback.flag");
            File.WriteAllText(fallbackPath, "fallback");

            _service.CleanupCompletedJob(
                plan,
                planPath,
                true);

            Assert.False(File.Exists(planPath));
            Assert.True(Directory.Exists(workerDirectory));
            Assert.True(File.Exists(fallbackPath));
            Assert.True(Directory.Exists(
                _pathService.GetUpdateRootDirectory(
                    _installDirectory)));
        }

        [Fact]
        public void CleanupOrphanedWork_PreservesActiveJobAndRemovesOthers()
        {
            var activePlan = SavePlan("active-job-003");
            var activeDownload = CreateJobDirectory(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    activePlan.JobId));
            var orphanDownload = CreateJobDirectory(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    "orphan-job-001"));
            var orphanBackup = CreateJobDirectory(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    "orphan-job-002"));
            var orphanWorker = CreateJobDirectory(
                _pathService.GetWorkerDirectory(
                    _installDirectory,
                    "orphan-job-003"));

            _service.CleanupOrphanedWork(
                _installDirectory);

            Assert.True(File.Exists(
                _pathService.GetActivePlanPath(
                    _installDirectory)));
            Assert.True(Directory.Exists(activeDownload));
            Assert.False(Directory.Exists(orphanDownload));
            Assert.False(Directory.Exists(orphanBackup));
            Assert.False(Directory.Exists(orphanWorker));
        }

        [Fact]
        public void CleanupOrphanedWork_InvalidPlanPreservesJobEvidence()
        {
            var planPath = _pathService.GetActivePlanPath(
                _installDirectory);
            Directory.CreateDirectory(
                Path.GetDirectoryName(planPath)!);
            File.WriteAllText(planPath, "invalid");
            var orphanDirectory = CreateJobDirectory(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    "evidence-job-001"));

            _service.CleanupOrphanedWork(
                _installDirectory);

            Assert.True(File.Exists(planPath));
            Assert.True(Directory.Exists(orphanDirectory));
        }

        [Fact]
        public void CleanupNoWork_RemovesInvalidPlanAndAllJobFolders()
        {
            var planPath = _pathService.GetActivePlanPath(
                _installDirectory);
            Directory.CreateDirectory(
                Path.GetDirectoryName(planPath)!);
            File.WriteAllText(planPath, "invalid");
            var downloadDirectory = CreateJobDirectory(
                _pathService.GetJobDirectory(
                    _installDirectory,
                    "stale-job-001"));
            var backupDirectory = CreateJobDirectory(
                _pathService.GetBackupDirectory(
                    _installDirectory,
                    "stale-job-002"));
            var workerDirectory = CreateJobDirectory(
                _pathService.GetWorkerDirectory(
                    _installDirectory,
                    "stale-job-003"));

            _service.CleanupNoWork(
                _installDirectory);

            Assert.False(File.Exists(planPath));
            Assert.False(Directory.Exists(downloadDirectory));
            Assert.False(Directory.Exists(backupDirectory));
            Assert.False(Directory.Exists(workerDirectory));
            Assert.False(Directory.Exists(
                _pathService.GetUpdateRootDirectory(
                    _installDirectory)));
        }

        [Fact]
        public void CleanupFailedPreparation_PreservesAnotherJobsPlan()
        {
            var activePlan = SavePlan("active-job-004");
            var failedPaths = _pathService.Create(
                _installDirectory);
            Directory.CreateDirectory(
                failedPaths.JobDirectory);
            File.WriteAllText(
                Path.Combine(failedPaths.JobDirectory, "partial.bin"),
                "partial");

            _service.CleanupFailedPreparation(
                failedPaths);

            Assert.True(File.Exists(
                failedPaths.ActivePlanPath));
            Assert.Equal(
                activePlan.JobId,
                _planStore.Load(failedPaths.ActivePlanPath).JobId);
            Assert.False(Directory.Exists(
                failedPaths.JobDirectory));
        }

        private UpdateApplyPlan SavePlan(string jobId)
        {
            var plan = new UpdateApplyPlan
            {
                JobId = jobId,
                ProductCode = "PCCAM",
                Architecture = "x86",
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                Mode = UpdateApplyModes.FileRepair,
                CreatedAtUtc = DateTime.UtcNow
            };

            _planStore.Save(
                _pathService.GetActivePlanPath(
                    _installDirectory),
                plan);
            return plan;
        }

        private static string CreateJobDirectory(string path)
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(
                Path.Combine(path, "payload.bin"),
                "payload");
            return path;
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
                // 테스트 정리 실패가 본 테스트 결과를 덮어쓰지 않도록 한다.
            }
        }
    }
}
