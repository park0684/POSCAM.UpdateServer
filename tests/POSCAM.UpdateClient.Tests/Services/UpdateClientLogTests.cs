using System;
using System.IO;
using System.Net;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class UpdateClientLogTests : IDisposable
    {
        private readonly string _installDirectory;

        public UpdateClientLogTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.LogTests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public void Error_WithServerException_WritesActionableDetails()
        {
            var exception = new UpdateServerClientException(
                "Update Check failed",
                HttpStatusCode.NotFound,
                40401,
                new IOException("connection closed"));

            UpdateClientLog.Error(
                _installDirectory,
                "StartupCheck.RequestFailed",
                "Update Check 요청에 실패했습니다. ExitCode=30",
                exception);

            var logsDirectory = Path.Combine(
                _installDirectory,
                "logs");
            var files = Directory.GetFiles(
                logsDirectory,
                "update_*.log");
            var logPath = Assert.Single(files);
            var content = File.ReadAllText(logPath);

            Assert.Contains(
                "ExceptionType=UpdateServerClientException",
                content);
            Assert.Contains(
                "ExceptionMessage=Update Check failed",
                content);
            Assert.Contains("StatusCode=404", content);
            Assert.Contains("ServerErrorCode=40401", content);
            Assert.Contains("InnerExceptionType=IOException", content);
            Assert.Contains(
                "InnerExceptionMessage=connection closed",
                content);
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
