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
    public sealed class StartupCheckServiceTests : IDisposable
    {
        private readonly string _installDirectory;

        public StartupCheckServiceTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.StartupCheckTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public async Task CheckAsync_UpdateAvailable_ReturnsApplyRequired()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = true,
                    LatestVersion = "2.0.0"
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyRequired, result.ExitCode);
            Assert.True(result.FullPackageUpdateRequired);
            Assert.False(result.RepairPlan.HasRepairTargets);
        }

        [Fact]
        public async Task CheckAsync_UpdateAvailableAndOnlyExecutableChanged_RequiresFullPackage()
        {
            var previousExecutable = Encoding.UTF8.GetBytes("old executable");
            var latestExecutable = Encoding.UTF8.GetBytes("new executable");
            var unchangedLibrary = Encoding.UTF8.GetBytes("unchanged library");

            WriteInstalledFile("PcCam.exe", previousExecutable);
            WriteInstalledFile("Shared.dll", unchangedLibrary);

            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = true,
                    LatestVersion = "1.0.1",
                    Files =
                    {
                        CreateManifestFile(
                            "PcCam.exe",
                            latestExecutable),
                        CreateManifestFile(
                            "Shared.dll",
                            unchangedLibrary)
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyRequired, result.ExitCode);
            Assert.True(result.FullPackageUpdateRequired);
            Assert.False(result.IncrementalUpdateRequired);

            var target = Assert.Single(result.RepairPlan.Targets);
            Assert.Equal("PcCam.exe", target.RelativePath);
            Assert.Equal(UpdateTargetOperations.Replace, target.Operation);
            Assert.Equal(RepairReasons.HashMismatch, target.Reason);
        }

        [Fact]
        public async Task CheckAsync_UpdateAvailableAndUpdateClientChanged_RequiresFullPackage()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = true,
                    LatestVersion = "1.0.1",
                    Files =
                    {
                        CreateManifestFile(
                            "POSCAM.UpdateClient.exe",
                            Encoding.UTF8.GetBytes("new update client"))
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyRequired, result.ExitCode);
            Assert.True(result.FullPackageUpdateRequired);
            Assert.False(result.IncrementalUpdateRequired);
            Assert.Single(result.RepairPlan.Targets);
        }

        [Fact]
        public async Task CheckAsync_UpdateAvailableWithMatchingFiles_ReturnsVerificationFailed()
        {
            var matchingExecutable = Encoding.UTF8.GetBytes(
                "matching executable");

            WriteInstalledFile("PcCam.exe", matchingExecutable);

            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = true,
                    LatestVersion = "1.0.1",
                    Files =
                    {
                        CreateManifestFile(
                            "PcCam.exe",
                            matchingExecutable)
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(
                UpdateClientExitCodes.VerificationFailed,
                result.ExitCode);
            Assert.False(result.FullPackageUpdateRequired);
            Assert.False(result.IncrementalUpdateRequired);
            Assert.False(result.RepairPlan.HasRepairTargets);
        }

        [Fact]
        public async Task CheckAsync_AlreadyLatestAndMatchingFiles_ReturnsSuccess()
        {
            var content = Encoding.UTF8.GetBytes("matching content");
            var path = Path.Combine(_installDirectory, "PcCam.exe");
            File.WriteAllBytes(path, content);

            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = false,
                    Files =
                    {
                        CreateManifestFile("PcCam.exe", content)
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.Success, result.ExitCode);
            Assert.False(result.FullPackageUpdateRequired);
            Assert.False(result.RepairPlan.HasRepairTargets);
        }

        [Fact]
        public async Task CheckAsync_AlreadyLatestAndMissingFile_ReturnsApplyRequired()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = false,
                    Files =
                    {
                        CreateManifestFile(
                            "providers/missing.dll",
                            Encoding.UTF8.GetBytes("missing"))
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyRequired, result.ExitCode);
            Assert.False(result.FullPackageUpdateRequired);
            var target = Assert.Single(result.RepairPlan.Targets);
            Assert.Equal(RepairReasons.Missing, target.Reason);
        }

        [Fact]
        public async Task CheckAsync_AlreadyLatest_ExcludesCoreExecutablesAndRepairsOtherFiles()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = false,
                    LatestVersion = "3.2.2",
                    Files =
                    {
                        CreateManifestFile(
                            "PcCam.exe",
                            Encoding.UTF8.GetBytes("pccam")),
                        CreateManifestFile(
                            "POSCAM.UpdateClient.exe",
                            Encoding.UTF8.GetBytes("update-client")),
                        CreateManifestFile(
                            "PccAuthClient.dll",
                            Encoding.UTF8.GetBytes("auth-client"))
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.ApplyRequired, result.ExitCode);
            Assert.False(result.FullPackageUpdateRequired);
            Assert.False(result.IncrementalUpdateRequired);

            var target = Assert.Single(result.RepairPlan.Targets);
            Assert.Equal("PccAuthClient.dll", target.RelativePath);
            Assert.Equal(RepairReasons.Missing, target.Reason);
        }

        [Fact]
        public async Task CheckAsync_AlreadyLatestAndOnlyCoreExecutablesMissing_ReturnsSuccess()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = false,
                    LatestVersion = "3.2.2",
                    Files =
                    {
                        CreateManifestFile(
                            "PcCam.exe",
                            Encoding.UTF8.GetBytes("pccam")),
                        CreateManifestFile(
                            "POSCAM.UpdateClient.exe",
                            Encoding.UTF8.GetBytes("update-client"))
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(UpdateClientExitCodes.Success, result.ExitCode);
            Assert.False(result.FullPackageUpdateRequired);
            Assert.False(result.RepairPlan.HasRepairTargets);
        }

        [Fact]
        public async Task CheckAsync_InvalidManifest_ReturnsVerificationFailed()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = false,
                    Files =
                    {
                        new UpdateManifestFile
                        {
                            Path = "PcCam.exe",
                            Size = 1,
                            Sha256 = "INVALID",
                            Required = true
                        }
                    }
                }
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(
                UpdateClientExitCodes.VerificationFailed,
                result.ExitCode);
        }

        [Fact]
        public async Task CheckAsync_UpdateServerFailure_ReturnsUpdateCheckFailed()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                ExceptionToThrow = new UpdateServerClientException(
                    "failed")
            };

            var result = await CreateService(fakeClient).CheckAsync(
                CreateOptions(),
                CancellationToken.None);

            Assert.Equal(
                UpdateClientExitCodes.UpdateCheckFailed,
                result.ExitCode);
        }

        [Fact]
        public async Task CheckAsync_CreatesExpectedUpdateRequest()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse
                {
                    UpdateAvailable = false
                }
            };

            var options = CreateOptions();
            options.ProductCode = "CAMVIEWER";
            options.OperatingSystem = "windows";
            options.Architecture = "x64";
            options.Channel = "beta";
            options.CurrentVersionOverride = "1.2.3.4";

            await CreateService(fakeClient).CheckAsync(
                options,
                CancellationToken.None);

            Assert.NotNull(fakeClient.LastRequest);
            Assert.Equal("CAMVIEWER", fakeClient.LastRequest!.ProductCode);
            Assert.Equal("1.2.3.4", fakeClient.LastRequest.CurrentVersion);
            Assert.Equal("windows", fakeClient.LastRequest.Os);
            Assert.Equal("x64", fakeClient.LastRequest.Architecture);
            Assert.Equal("beta", fakeClient.LastRequest.Channel);
        }

        [Fact]
        public async Task CheckAsync_MissingApplicationWithoutOverride_ReturnsVerificationFailed()
        {
            var fakeClient = new FakeUpdateServerClient
            {
                Response = new UpdateCheckResponse()
            };

            var options = CreateOptions();
            options.CurrentVersionOverride = null;

            var result = await CreateService(fakeClient).CheckAsync(
                options,
                CancellationToken.None);

            Assert.Equal(
                UpdateClientExitCodes.VerificationFailed,
                result.ExitCode);
            Assert.Null(fakeClient.LastRequest);
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
                CurrentVersionOverride = "1.0.0",
                ProductCode = "PCCAM",
                OperatingSystem = "windows",
                Architecture = "x86",
                Channel = "stable"
            };
        }

        private void WriteInstalledFile(
            string relativePath,
            byte[] content)
        {
            var path = Path.Combine(
                _installDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, content);
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
                DownloadUrl = "https://update.poscam.co.kr/packages/file"
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
