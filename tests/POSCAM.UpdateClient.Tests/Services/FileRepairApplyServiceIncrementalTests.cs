using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class FileRepairApplyServiceIncrementalTests : IDisposable
    {
        private readonly string _installDirectory;
        private readonly FileRepairApplyService _service;

        public FileRepairApplyServiceIncrementalTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.DeleteApplyTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_installDirectory);
            _service = new FileRepairApplyService(
                new UpdateWorkPathService(),
                new FileHashCalculator());
        }

        [Fact]
        public void ApplyAndRestart_DeleteTarget_RemovesManagedFile()
        {
            var path = CreateLocalFile(
                "providers/OldProvider.dll",
                Encoding.UTF8.GetBytes("old provider"));
            var restarted = false;

            _service.ApplyAndRestart(
                CreateDeletePlan("providers/OldProvider.dll"),
                () => restarted = true,
                CancellationToken.None);

            Assert.False(File.Exists(path));
            Assert.True(restarted);
        }

        [Fact]
        public void ApplyAndRestart_RestartFails_RestoresDeletedFile()
        {
            var original = Encoding.UTF8.GetBytes("old provider");
            var path = CreateLocalFile(
                "providers/OldProvider.dll",
                original);
            var restartCalls = 0;

            Assert.Throws<InvalidOperationException>(() =>
                _service.ApplyAndRestart(
                    CreateDeletePlan("providers/OldProvider.dll"),
                    () =>
                    {
                        restartCalls++;
                        if (restartCalls == 1)
                        {
                            throw new InvalidOperationException(
                                "simulated restart failure");
                        }
                    },
                    CancellationToken.None));

            Assert.Equal(2, restartCalls);
            Assert.True(File.Exists(path));
            Assert.Equal(original, File.ReadAllBytes(path));
        }

        [Fact]
        public void ApplyAndRestart_CommitsStateBeforeRestart()
        {
            CreateLocalFile(
                "providers/OldProvider.dll",
                Encoding.UTF8.GetBytes("old provider"));
            var events = new List<string>();
            var state = "old";

            _service.ApplyAndRestart(
                CreateDeletePlan("providers/OldProvider.dll"),
                () =>
                {
                    state = "new";
                    events.Add("commit");
                },
                () =>
                {
                    state = "old";
                    events.Add("rollback");
                },
                () => events.Add("restart:" + state),
                CancellationToken.None);

            Assert.Equal(
                new[] { "commit", "restart:new" },
                events);
        }

        [Fact]
        public void ApplyAndRestart_RestartFails_RollsBackStateBeforeRecoveryRestart()
        {
            var original = Encoding.UTF8.GetBytes("old provider");
            var path = CreateLocalFile(
                "providers/OldProvider.dll",
                original);
            var events = new List<string>();
            var state = "old";
            var restartCalls = 0;

            Assert.Throws<InvalidOperationException>(() =>
                _service.ApplyAndRestart(
                    CreateDeletePlan("providers/OldProvider.dll"),
                    () =>
                    {
                        state = "new";
                        events.Add("commit");
                    },
                    () =>
                    {
                        state = "old";
                        events.Add("rollback");
                    },
                    () =>
                    {
                        restartCalls++;
                        events.Add("restart:" + state);
                        if (restartCalls == 1)
                        {
                            throw new InvalidOperationException(
                                "simulated restart failure");
                        }
                    },
                    CancellationToken.None));

            Assert.Equal(
                new[]
                {
                    "commit",
                    "restart:new",
                    "rollback",
                    "restart:old"
                },
                events);
            Assert.True(File.Exists(path));
            Assert.Equal(original, File.ReadAllBytes(path));
        }

        private UpdateApplyPlan CreateDeletePlan(string relativePath)
        {
            return new UpdateApplyPlan
            {
                JobId = "delete-job-001",
                ProductCode = "PCCAM",
                Architecture = "x86",
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                Mode = UpdateApplyModes.IncrementalUpdate,
                CreatedAtUtc = DateTime.UtcNow,
                Targets =
                {
                    new UpdateApplyTarget
                    {
                        Operation = UpdateTargetOperations.Delete,
                        RelativePath = relativePath,
                        Reason = RepairReasons.Removed
                    }
                }
            };
        }

        private string CreateLocalFile(
            string relativePath,
            byte[] content)
        {
            var fullPath = Path.Combine(
                _installDirectory,
                relativePath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(fullPath, content);
            return fullPath;
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
