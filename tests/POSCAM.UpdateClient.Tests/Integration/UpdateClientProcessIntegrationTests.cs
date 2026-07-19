using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using POSCAM.UpdateClient.ProcessHost;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Integration
{
    [CollectionDefinition(
        "UpdateClient process integration",
        DisableParallelization = true)]
    public sealed class UpdateClientProcessIntegrationCollection
    {
    }

    [Collection("UpdateClient process integration")]
    public sealed class UpdateClientProcessIntegrationTests :
        IDisposable
    {
        private const string TestVersion = "1.0.0";
        private const int ApplyRequiredExitCode = 10;
        private const int ApplyFailedExitCode = 50;

        private readonly string _testRoot;
        private readonly string _installDirectory;
        private readonly string _updateClientPath;
        private readonly string _processHostSourcePath;
        private readonly string _processHostFileName;
        private readonly string _installedProcessHostPath;
        private readonly string _invocationLogPath;

        public UpdateClientProcessIntegrationTests()
        {
            _testRoot = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.ProcessIntegrationTests",
                Guid.NewGuid().ToString("N"));
            _installDirectory = Path.Combine(
                _testRoot,
                "install");
            Directory.CreateDirectory(_installDirectory);

            _updateClientPath =
                typeof(UpdateClientApplication).Assembly.Location;
            _processHostSourcePath =
                typeof(ProcessHostMarker).Assembly.Location;
            _processHostFileName = Path.GetFileName(
                _processHostSourcePath);
            _installedProcessHostPath = Path.Combine(
                _installDirectory,
                _processHostFileName);
            _invocationLogPath = Path.Combine(
                _installDirectory,
                "host-invocations.log");

            Assert.True(File.Exists(_updateClientPath));
            Assert.True(File.Exists(_processHostSourcePath));

            File.Copy(
                _processHostSourcePath,
                _installedProcessHostPath,
                true);
        }

        [Fact]
        [Trait("Category", "ProcessIntegration")]
        public void SameVersionMissingFile_RepairsOnceAndNextCheckIsClean()
        {
            var payload = Encoding.UTF8.GetBytes(
                "POSCAM process integration payload");
            var manifestPath = "managed/integration-probe.bin";
            var installedPayloadPath = Path.Combine(
                _installDirectory,
                "managed",
                "integration-probe.bin");

            using (var server = new MockUpdateServer(
                TestVersion,
                manifestPath,
                payload))
            {
                Assert.Equal(
                    ApplyRequiredExitCode,
                    RunStartupCheck(server));
                Assert.True(File.Exists(GetPlanPath()));

                using (var waitProcess = StartWaitProcess())
                {
                    Assert.Equal(
                        0,
                        RunApply(waitProcess.Id));
                }

                WaitForInvocationCount(1);

                Assert.Equal(
                    new[] { "<none>" },
                    ReadInvocationLines());
                Assert.Equal(
                    payload,
                    File.ReadAllBytes(installedPayloadPath));
                Assert.False(
                    Directory.Exists(GetUpdateRootPath()));

                Assert.Equal(
                    0,
                    RunStartupCheck(server));

                Assert.Equal(2, server.CheckRequestCount);
                Assert.Equal(1, server.DownloadRequestCount);
                Assert.False(File.Exists(GetPlanPath()));
                Assert.False(
                    Directory.Exists(GetUpdateRootPath()));
            }
        }

        [Fact]
        [Trait("Category", "ProcessIntegration")]
        public void InvalidApplicationPayload_RollsBackSkipsOnceAndNormalCheckRunsAgain()
        {
            var originalSha256 = CalculateSha256(
                _installedProcessHostPath);
            var invalidExecutable = Encoding.UTF8.GetBytes(
                "THIS-IS-NOT-A-VALID-WINDOWS-EXECUTABLE");

            using (var server = new MockUpdateServer(
                TestVersion,
                _processHostFileName,
                invalidExecutable))
            {
                Assert.Equal(
                    ApplyRequiredExitCode,
                    RunStartupCheck(server));
                Assert.True(File.Exists(GetPlanPath()));

                using (var waitProcess = StartWaitProcess())
                {
                    Assert.Equal(
                        ApplyFailedExitCode,
                        RunApply(waitProcess.Id));
                }

                WaitForInvocationCount(1);

                Assert.Equal(
                    new[] { "--skip-update-once" },
                    ReadInvocationLines());
                Assert.Equal(
                    originalSha256,
                    CalculateSha256(_installedProcessHostPath));
                Assert.False(
                    Directory.Exists(GetUpdateRootPath()));
                Assert.Equal(1, server.CheckRequestCount);

                Assert.Equal(
                    0,
                    RunInstalledProcessHost());
                WaitForInvocationCount(2);

                Assert.Equal(
                    new[]
                    {
                        "--skip-update-once",
                        "<none>"
                    },
                    ReadInvocationLines());

                Assert.Equal(
                    ApplyRequiredExitCode,
                    RunStartupCheck(server));

                Assert.Equal(2, server.CheckRequestCount);
                Assert.Equal(2, server.DownloadRequestCount);
                Assert.True(File.Exists(GetPlanPath()));
            }
        }

        public void Dispose()
        {
            TryDeleteDirectory(_testRoot);
        }

        private int RunStartupCheck(MockUpdateServer server)
        {
            return RunUpdateClient(
                "startup-check",
                "--install-dir",
                _installDirectory,
                "--app",
                _processHostFileName,
                "--base-url",
                server.BaseUri.AbsoluteUri.TrimEnd('/'),
                "--product-code",
                "PCCAM",
                "--os",
                "windows",
                "--architecture",
                "x86",
                "--channel",
                "stable",
                "--current-version",
                TestVersion);
        }

        private int RunApply(int waitProcessId)
        {
            return RunUpdateClient(
                "apply",
                "--plan",
                GetPlanPath(),
                "--wait-process-id",
                waitProcessId.ToString(),
                "--restart",
                _processHostFileName,
                "--product-code",
                "PCCAM",
                "--architecture",
                "x86",
                "--wait-timeout-seconds",
                "30");
        }

        private int RunUpdateClient(params string[] arguments)
        {
            return RunProcess(
                _updateClientPath,
                BuildArguments(arguments),
                _installDirectory,
                TimeSpan.FromSeconds(45));
        }

        private int RunInstalledProcessHost()
        {
            return RunProcess(
                _installedProcessHostPath,
                "",
                _installDirectory,
                TimeSpan.FromSeconds(15));
        }

        private Process StartWaitProcess()
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = _installedProcessHostPath,
                Arguments = BuildArguments(
                    new[] { "--hold-ms", "1200" }),
                WorkingDirectory = _installDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Assert.NotNull(process);
            Thread.Sleep(100);
            Assert.False(process!.HasExited);
            return process;
        }

        private static int RunProcess(
            string fileName,
            string arguments,
            string workingDirectory,
            TimeSpan timeout)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Assert.True(process.Start());

                if (!process.WaitForExit(
                    checked((int)timeout.TotalMilliseconds)))
                {
                    TryKill(process);

                    throw new TimeoutException(
                        "통합 테스트 프로세스가 제한 시간 안에 종료되지 않았습니다. "
                        + "File="
                        + fileName
                        + " Arguments="
                        + arguments);
                }

                return process.ExitCode;
            }
        }

        private void WaitForInvocationCount(int expectedCount)
        {
            var timeoutAt = DateTime.UtcNow.AddSeconds(15);

            while (DateTime.UtcNow < timeoutAt)
            {
                if (ReadInvocationLines().Length >= expectedCount)
                {
                    return;
                }

                Thread.Sleep(100);
            }

            throw new TimeoutException(
                "재실행 프로세스 인자 로그를 제한 시간 안에 확인하지 못했습니다. "
                + "ExpectedCount="
                + expectedCount);
        }

        private string[] ReadInvocationLines()
        {
            if (!File.Exists(_invocationLogPath))
            {
                return Array.Empty<string>();
            }

            return File.ReadAllLines(
                _invocationLogPath,
                Encoding.UTF8);
        }

        private string GetPlanPath()
        {
            return Path.Combine(
                GetUpdateRootPath(),
                "state",
                "repair-plan.json");
        }

        private string GetUpdateRootPath()
        {
            return Path.Combine(
                _installDirectory,
                "_update");
        }

        private static string BuildArguments(
            string[] arguments)
        {
            var builder = new StringBuilder();

            foreach (var argument in arguments)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(QuoteArgument(argument));
            }

            return builder.ToString();
        }

        private static string QuoteArgument(string value)
        {
            if (value == null)
            {
                value = "";
            }

            var builder = new StringBuilder();
            builder.Append('"');

            var backslashCount = 0;

            foreach (var character in value)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append(
                        '\\',
                        backslashCount * 2 + 1);
                    builder.Append('"');
                    backslashCount = 0;
                    continue;
                }

                if (backslashCount > 0)
                {
                    builder.Append('\\', backslashCount);
                    backslashCount = 0;
                }

                builder.Append(character);
            }

            if (backslashCount > 0)
            {
                builder.Append('\\', backslashCount * 2);
            }

            builder.Append('"');
            return builder.ToString();
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
                var hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);

                foreach (var current in hash)
                {
                    builder.Append(current.ToString("X2"));
                }

                return builder.ToString();
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                // Timeout 원인을 유지하기 위해 종료 정리 실패는 무시한다.
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // 테스트 정리 실패가 테스트 결과를 덮어쓰지 않도록 한다.
            }
        }
    }
}
