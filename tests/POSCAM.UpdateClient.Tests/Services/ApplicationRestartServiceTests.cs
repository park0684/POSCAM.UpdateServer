using System;
using System.Diagnostics;
using System.IO;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class ApplicationRestartServiceTests : IDisposable
    {
        private readonly string _installDirectory;

        public ApplicationRestartServiceTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.RestartTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_installDirectory);
            File.WriteAllText(
                Path.Combine(_installDirectory, "PcCam.exe"),
                "test executable");
        }

        [Theory]
        [InlineData(false, "")]
        [InlineData(true, "--skip-update-once")]
        public void Restart_BuildsExpectedOneTimeSkipArgument(
            bool skipUpdateOnce,
            string expectedArguments)
        {
            ProcessStartInfo? capturedStartInfo = null;
            var service = new ApplicationRestartService(
                startInfo => capturedStartInfo = startInfo);

            service.Restart(
                _installDirectory,
                "PcCam.exe",
                skipUpdateOnce);

            Assert.NotNull(capturedStartInfo);
            Assert.Equal(
                Path.Combine(_installDirectory, "PcCam.exe"),
                capturedStartInfo!.FileName);
            Assert.Equal(
                _installDirectory,
                capturedStartInfo.WorkingDirectory);
            Assert.Equal(
                expectedArguments,
                capturedStartInfo.Arguments);
            Assert.True(capturedStartInfo.UseShellExecute);
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
