using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using POSCAM.UpdateClient.Tests.TestDoubles;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class StartupUpdatePreparationServiceTests : IDisposable
    {
        private readonly string _installDirectory;

        public StartupUpdatePreparationServiceTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.PreparationTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public async Task PrepareAsync_NoWork_RemovesStalePlan()
        {
            var activePlanPath = GetActivePlanPath();
            var stateDirectory = Path.GetDirectoryName(activePlanPath);

            Assert.False(string.IsNullOrWhiteSpace(stateDirectory));
            Directory.CreateDirectory(stateDirectory!);
            File.WriteAllText(activePlanPath, "stale");

            var service = CreateService(
                new FakeUpdateFileDownloadService());

            var exitCode = await service.PrepareAsync(
                CreateOptions(),
                new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.Success
                },
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.Success, exitCode);
            Assert.False(File.Exists(activePlanPath));
        }

        [Fact]
        public async Task PrepareAsync_FullPackage_DownloadsAndWritesPlan()
        {
            var content = Encoding.UTF8.GetBytes("full package content");
            var downloader = new FakeUpdateFileDownloadService
            {
                Content = content
            };
            var service = CreateService(downloader);

            var exitCode = await service.PrepareAsync(
                CreateOptions(),
                new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.ApplyRequired,
                    FullPackageUpdateRequired = true,
                    UpdateResponse = new UpdateCheckResponse
                    {
                        UpdateAvailable = true,
                        LatestVersion = "2.0.0",
                        PackageType = "zip",
                        PackageUrl =
                            "https://update.poscam.co.kr/packages/pccam.zip",
                        FileName = "pccam.zip",
                        FileSize = content.LongLength,
                        Sha256 = CalculateSha256(content)
                    }
                },
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyRequired, exitCode);
            Assert.Single(downloader.Requests);

            var plan = ReadActivePlan();
            var packagePath = plan.PackagePath;

            Assert.Equal(UpdateApplyModes.FullPackage, plan.Mode);
            Assert.Equal("pccam.zip", plan.PackageFileName);
            Assert.Equal("2.0.0", plan.LatestVersion);
            Assert.False(string.IsNullOrWhiteSpace(packagePath));
            Assert.True(File.Exists(packagePath!));
            Assert.Equal(content, File.ReadAllBytes(packagePath!));
            Assert.Empty(plan.Targets);
        }

        [Fact]
        public async Task PrepareAsync_FileRepair_DownloadsTargetsAndWritesPlan()
        {
            var content = Encoding.UTF8.GetBytes("provider content");
            var downloader = new FakeUpdateFileDownloadService
            {
                Content = content
            };
            var service = CreateService(downloader);
            var target = new RepairTarget
            {
                RelativePath = Path.Combine("providers", "provider.dll"),
                LocalPath = Path.Combine(
                    _installDirectory,
                    "providers",
                    "provider.dll"),
                ExpectedSize = content.LongLength,
                ExpectedSha256 = CalculateSha256(content),
                DownloadUrl =
                    "https://update.poscam.co.kr/packages/provider.dll",
                Reason = RepairReasons.Missing
            };
            var repairPlan = new RepairPlan();
            repairPlan.Targets.Add(target);

            var exitCode = await service.PrepareAsync(
                CreateOptions(),
                new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.ApplyRequired,
                    FullPackageUpdateRequired = false,
                    UpdateResponse = new UpdateCheckResponse
                    {
                        UpdateAvailable = false,
                        LatestVersion = "1.0.0"
                    },
                    RepairPlan = repairPlan
                },
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyRequired, exitCode);
            Assert.Single(downloader.Requests);

            var plan = ReadActivePlan();
            var applyTarget = Assert.Single(plan.Targets);

            Assert.Equal(UpdateApplyModes.FileRepair, plan.Mode);
            Assert.Equal(target.RelativePath, applyTarget.RelativePath);
            Assert.Equal(RepairReasons.Missing, applyTarget.Reason);
            Assert.True(File.Exists(applyTarget.DownloadedPath));
            Assert.Equal(
                content,
                File.ReadAllBytes(applyTarget.DownloadedPath));
            Assert.Null(plan.PackagePath);
        }

        [Fact]
        public async Task PrepareAsync_DownloadFailure_ReturnsDownloadFailedAndNoPlan()
        {
            var content = Encoding.UTF8.GetBytes("package");
            var downloader = new FakeUpdateFileDownloadService
            {
                ExceptionToThrow = new UpdateDownloadException(
                    "download failed")
            };
            var service = CreateService(downloader);

            var exitCode = await service.PrepareAsync(
                CreateOptions(),
                CreateFullPackageResult(content),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.DownloadFailed, exitCode);
            Assert.False(File.Exists(GetActivePlanPath()));

            var downloadsRoot = Path.Combine(
                _installDirectory,
                "_update",
                "downloads");

            if (Directory.Exists(downloadsRoot))
            {
                Assert.Empty(Directory.GetDirectories(downloadsRoot));
            }
        }

        [Fact]
        public async Task PrepareAsync_InvalidFullPackageMetadata_ReturnsVerificationFailed()
        {
            var downloader = new FakeUpdateFileDownloadService();
            var service = CreateService(downloader);

            var exitCode = await service.PrepareAsync(
                CreateOptions(),
                new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.ApplyRequired,
                    FullPackageUpdateRequired = true,
                    UpdateResponse = new UpdateCheckResponse
                    {
                        UpdateAvailable = true,
                        PackageUrl =
                            "https://update.poscam.co.kr/packages/pccam.zip",
                        FileName = "pccam.zip",
                        FileSize = 10,
                        Sha256 = null
                    }
                },
                CancellationToken.None);

            Assert.Equal(
                UpdateClientExitCodes.VerificationFailed,
                exitCode);
            Assert.Empty(downloader.Requests);
            Assert.False(File.Exists(GetActivePlanPath()));
        }

        [Fact]
        public async Task PrepareAsync_ApplyRequiredWithoutTargets_ReturnsVerificationFailed()
        {
            var downloader = new FakeUpdateFileDownloadService();
            var service = CreateService(downloader);

            var exitCode = await service.PrepareAsync(
                CreateOptions(),
                new StartupCheckResult
                {
                    ExitCode = UpdateClientExitCodes.ApplyRequired,
                    FullPackageUpdateRequired = false,
                    RepairPlan = new RepairPlan()
                },
                CancellationToken.None);

            Assert.Equal(
                UpdateClientExitCodes.VerificationFailed,
                exitCode);
            Assert.Empty(downloader.Requests);
            Assert.False(File.Exists(GetActivePlanPath()));
        }

        private StartupUpdatePreparationService CreateService(
            IUpdateFileDownloadService downloader)
        {
            return new StartupUpdatePreparationService(
                downloader,
                new UpdateWorkPathService(),
                new UpdateApplyPlanStore());
        }

        private StartupCheckOptions CreateOptions()
        {
            return new StartupCheckOptions
            {
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                CurrentVersionOverride = "1.0.0"
            };
        }

        private StartupCheckResult CreateFullPackageResult(byte[] content)
        {
            return new StartupCheckResult
            {
                ExitCode = UpdateClientExitCodes.ApplyRequired,
                FullPackageUpdateRequired = true,
                UpdateResponse = new UpdateCheckResponse
                {
                    UpdateAvailable = true,
                    LatestVersion = "2.0.0",
                    PackageType = "zip",
                    PackageUrl =
                        "https://update.poscam.co.kr/packages/pccam.zip",
                    FileName = "pccam.zip",
                    FileSize = content.LongLength,
                    Sha256 = CalculateSha256(content)
                }
            };
        }

        private UpdateApplyPlan ReadActivePlan()
        {
            var json = File.ReadAllText(GetActivePlanPath());
            var plan = JsonConvert.DeserializeObject<UpdateApplyPlan>(json);

            Assert.NotNull(plan);
            return plan!;
        }

        private string GetActivePlanPath()
        {
            return Path.Combine(
                _installDirectory,
                "_update",
                "state",
                "repair-plan.json");
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
                // 테스트 정리 실패로 본 테스트 결과를 덮어쓰지 않는다.
            }
        }
    }
}
