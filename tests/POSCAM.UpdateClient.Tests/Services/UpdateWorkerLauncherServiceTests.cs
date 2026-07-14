using System;
using System.Diagnostics;
using System.IO;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class UpdateWorkerLauncherServiceTests : IDisposable
    {
        private const string JobId = "worker-launch-job-001";
        private readonly string _rootDirectory;
        private readonly string _installDirectory;
        private readonly string _sourceExecutablePath;
        private readonly string _jsonAssemblyPath;
        private readonly UpdateWorkPathService _pathService;

        public UpdateWorkerLauncherServiceTests()
        {
            _rootDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.WorkerLauncherTests",
                Guid.NewGuid().ToString("N"));
            _installDirectory = Path.Combine(
                _rootDirectory,
                "Install Root");
            var sourceDirectory = Path.Combine(
                _rootDirectory,
                "Source Runtime");
            _sourceExecutablePath = Path.Combine(
                sourceDirectory,
                "POSCAM.UpdateClient.exe");
            _jsonAssemblyPath = Path.Combine(
                sourceDirectory,
                "Newtonsoft.Json.dll");
            _pathService = new UpdateWorkPathService();

            Directory.CreateDirectory(_installDirectory);
            Directory.CreateDirectory(sourceDirectory);
            File.WriteAllText(_sourceExecutablePath, "worker exe");
            File.WriteAllText(_jsonAssemblyPath, "json runtime");
            File.WriteAllText(
                _sourceExecutablePath + ".config",
                "config");
        }

        [Fact]
        public void Launch_ValidInput_CopiesRuntimeAndStartsWorker()
        {
            ProcessStartInfo? captured = null;
            var service = CreateService(info => captured = info);
            var planPath = _pathService.GetActivePlanPath(
                _installDirectory);

            service.Launch(
                _installDirectory,
                JobId,
                planPath,
                "PcCam.exe",
                90,
                2468);

            var startInfo = captured
                ?? throw new InvalidOperationException(
                    "worker 시작 정보가 전달되지 않았습니다.");
            var workerDirectory = _pathService.GetWorkerDirectory(
                _installDirectory,
                JobId);
            var workerExecutablePath = Path.Combine(
                workerDirectory,
                "POSCAM.UpdateClient.exe");

            Assert.Equal(workerExecutablePath, startInfo.FileName);
            Assert.Equal(workerDirectory, startInfo.WorkingDirectory);
            Assert.False(startInfo.UseShellExecute);
            Assert.True(startInfo.CreateNoWindow);
            Assert.Contains("apply-worker", startInfo.Arguments);
            Assert.Contains("--wait-process-id 2468", startInfo.Arguments);
            Assert.Contains("--wait-timeout-seconds 90", startInfo.Arguments);
            Assert.Contains("\"" + planPath + "\"", startInfo.Arguments);
            Assert.Contains("--restart \"PcCam.exe\"", startInfo.Arguments);
            Assert.Equal(
                "worker exe",
                File.ReadAllText(workerExecutablePath));
            Assert.Equal(
                "json runtime",
                File.ReadAllText(Path.Combine(
                    workerDirectory,
                    "Newtonsoft.Json.dll")));
            Assert.Equal(
                "config",
                File.ReadAllText(workerExecutablePath + ".config"));
        }

        [Fact]
        public void Launch_MissingJsonRuntime_ThrowsBeforeStart()
        {
            File.Delete(_jsonAssemblyPath);
            var called = false;
            var service = CreateService(_ => called = true);

            Assert.Throws<FileNotFoundException>(() => service.Launch(
                _installDirectory,
                JobId,
                _pathService.GetActivePlanPath(_installDirectory),
                "PcCam.exe",
                60,
                1234));

            Assert.False(called);
            Assert.False(Directory.Exists(
                _pathService.GetWorkerDirectory(
                    _installDirectory,
                    JobId)));
        }

        [Fact]
        public void Launch_ProcessStartFailure_CleansWorkerDirectory()
        {
            var service = CreateService(_ =>
                throw new IOException("start failed"));

            Assert.Throws<IOException>(() => service.Launch(
                _installDirectory,
                JobId,
                _pathService.GetActivePlanPath(_installDirectory),
                "PcCam.exe",
                60,
                1234));

            Assert.False(Directory.Exists(
                _pathService.GetWorkerDirectory(
                    _installDirectory,
                    JobId)));
        }

        [Theory]
        [InlineData(0, 60)]
        [InlineData(-1, 60)]
        [InlineData(1234, 0)]
        [InlineData(1234, 601)]
        public void Launch_InvalidProcessOrTimeout_IsRejected(
            int parentProcessId,
            int timeoutSeconds)
        {
            var service = CreateService(_ => { });

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
                service.Launch(
                    _installDirectory,
                    JobId,
                    _pathService.GetActivePlanPath(_installDirectory),
                    "PcCam.exe",
                    timeoutSeconds,
                    parentProcessId));
        }

        private UpdateWorkerLauncherService CreateService(
            Action<ProcessStartInfo> starter)
        {
            return new UpdateWorkerLauncherService(
                _pathService,
                _sourceExecutablePath,
                _jsonAssemblyPath,
                starter);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_rootDirectory))
                {
                    Directory.Delete(_rootDirectory, true);
                }
            }
            catch
            {
                // 테스트 정리 실패가 테스트 결과를 덮어쓰지 않도록 한다.
            }
        }
    }
}
