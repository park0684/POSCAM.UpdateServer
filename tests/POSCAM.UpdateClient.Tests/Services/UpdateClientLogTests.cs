using System;
using System.IO;
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
        public void InfoAndError_DoNotCreatePersistentUpdateLog()
        {
            UpdateClientLog.Info(
                _installDirectory,
                "StartupCheck.Request",
                "normal check");
            UpdateClientLog.Error(
                _installDirectory,
                "StartupCheck.RequestFailed",
                "failed check",
                new IOException("connection closed"));

            Assert.False(Directory.Exists(Path.Combine(
                _installDirectory,
                "logs")));
        }

        [Fact]
        public void Completed_WritesSuccessAndDeletesExpiredLogs()
        {
            var logsDirectory = Path.Combine(
                _installDirectory,
                "logs");
            Directory.CreateDirectory(logsDirectory);

            var oldLog = Path.Combine(
                logsDirectory,
                "update_20000101.log");
            var recentLog = Path.Combine(
                logsDirectory,
                "update_20990101.log");

            File.WriteAllText(oldLog, "old");
            File.WriteAllText(recentLog, "recent");
            File.SetLastWriteTime(
                oldLog,
                DateTime.Now.AddDays(-31));
            File.SetLastWriteTime(
                recentLog,
                DateTime.Now.AddDays(-29));

            UpdateClientLog.Completed(
                _installDirectory,
                "job-1",
                "FileRepair",
                "3.2.2",
                2);

            Assert.False(File.Exists(oldLog));
            Assert.True(File.Exists(recentLog));

            var currentLog = UpdateClientLog.GetLogPath(
                _installDirectory,
                DateTime.Now);
            Assert.True(File.Exists(currentLog));

            var content = File.ReadAllText(currentLog);
            Assert.Contains("Result=Success", content);
            Assert.Contains("Mode=FileRepair", content);
            Assert.Contains("Version=3.2.2", content);
            Assert.Contains("UpdatedFiles=2", content);
            Assert.Contains("BackupCleanup=Success", content);
        }

        [Fact]
        public void Completed_WhenBackupJobRemains_DoesNotWriteLog()
        {
            var backupDirectory = Path.Combine(
                _installDirectory,
                "_update",
                "backups",
                "job-1");
            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(
                Path.Combine(backupDirectory, "backup.bin"),
                "backup");

            UpdateClientLog.Completed(
                _installDirectory,
                "job-1",
                "FileRepair",
                "3.2.2",
                1);

            Assert.False(Directory.Exists(Path.Combine(
                _installDirectory,
                "logs")));
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
