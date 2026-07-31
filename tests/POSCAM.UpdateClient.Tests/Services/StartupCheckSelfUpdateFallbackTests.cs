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
    public sealed class StartupCheckSelfUpdateFallbackTests : IDisposable
    {
        private readonly string _installDirectory;

        public StartupCheckSelfUpdateFallbackTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.SelfUpdateFallbackTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public async Task CheckAsync_AlreadyLatestAndUpdateClientMissing_RequiresFullPackage()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = false,
                    LatestVersion = "3.2.1",
                    Files =
                    {
                        CreateManifestFile(
                            "POSCAM.UpdateClient.exe",
                            Encoding.UTF8.GetBytes("server updater"))
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(
                UpdateClientExitCodes.ApplyRequired,
                result.ExitCode);
            Assert.True(result.FullPackageUpdateRequired);
            Assert.False(result.IncrementalUpdateRequired);

            var target = Assert.Single(result.RepairPlan.Targets);
            Assert.Equal(
                "POSCAM.UpdateClient.exe",
                target.RelativePath);
            Assert.Equal(RepairReasons.Missing, target.Reason);
        }

        [Fact]
        public async Task CheckAsync_UpdateAvailableAndUpdateClientChanged_RequiresFullPackage()
        {
            File.WriteAllBytes(
                Path.Combine(
                    _installDirectory,
                    "POSCAM.UpdateClient.exe"),
                Encoding.UTF8.GetBytes("installed updater"));

            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = true,
                    LatestVersion = "3.2.2",
                    Files =
                    {
                        CreateManifestFile(
                            "POSCAM.UpdateClient.exe",
                            Encoding.UTF8.GetBytes("server updater"))
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(
                UpdateClientExitCodes.ApplyRequired,
                result.ExitCode);
            Assert.True(result.FullPackageUpdateRequired);
            Assert.False(result.IncrementalUpdateRequired);

            var target = Assert.Single(result.RepairPlan.Targets);
            Assert.Equal(
                "POSCAM.UpdateClient.exe",
                target.RelativePath);
            Assert.Equal(RepairReasons.SizeMismatch, target.Reason);
        }

        [Fact]
        public async Task CheckAsync_AlreadyLatestAndNormalDllMissing_KeepsFileRepair()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = false,
                    LatestVersion = "3.2.1",
                    Files =
                    {
                        CreateManifestFile(
                            "PccAuthClient.dll",
                            Encoding.UTF8.GetBytes("auth client"))
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(
                UpdateClientExitCodes.ApplyRequired,
                result.ExitCode);
            Assert.False(result.FullPackageUpdateRequired);
            Assert.False(result.IncrementalUpdateRequired);

            var target = Assert.Single(result.RepairPlan.Targets);
            Assert.Equal("PccAuthClient.dll", target.RelativePath);
        }

        private StartupCheckService CreateService(
            IUpdateServerClient updateServerClient)
        {
            return new StartupCheckService(
                updateServerClient,
                new ApplicationVersionResolver(),
                new ManifestRepairPlanner());
        }

        private StartupCheckOptions CreateOptions()
        {
            return new StartupCheckOptions
            {
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                CurrentVersionOverride = "3.2.1.0",
                ProductCode = "PCCAM_X64",
                OperatingSystem = "windows",
                Architecture = "x64",
                Channel = "stable"
            };
        }

        private static UpdateManifestFile CreateManifestFile(
            string path,
            byte[] content)
        {
            return new UpdateManifestFile
            {
                Path = path,
                Size = content.LongLength,
                Sha256 = CalculateSha256(content),
                Required = true,
                DownloadUrl =
                    "https://update.poscam.co.kr/packages/test-file"
            };
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
