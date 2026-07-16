using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class IncrementalUpdatePlannerTests : IDisposable
    {
        private readonly string _installDirectory;
        private readonly ManifestRepairPlanner _planner;

        public IncrementalUpdatePlannerTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.IncrementalTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_installDirectory);
            _planner = new ManifestRepairPlanner();
        }

        [Fact]
        public void CreateIncrementalPlan_ChangedAddedAndRemovedFiles_CreatesExpectedOperations()
        {
            var previousCore = Encoding.UTF8.GetBytes("OLD001");
            var latestCore = Encoding.UTF8.GetBytes("NEW001");
            var removed = Encoding.UTF8.GetBytes("remove me");
            var added = Encoding.UTF8.GetBytes("new provider");

            CreateLocalFile("Core.dll", previousCore);
            CreateLocalFile("providers/OldProvider.dll", removed);

            var installedManifest = CreateInstalledManifest(
                CreateInstalledFile("Core.dll", previousCore),
                CreateInstalledFile("providers/OldProvider.dll", removed));
            var latestFiles = new[]
            {
                CreateManifestFile("Core.dll", latestCore),
                CreateManifestFile("providers/NewProvider.dll", added)
            };

            var plan = _planner.CreateIncrementalPlan(
                _installDirectory,
                latestFiles,
                installedManifest);

            Assert.Equal(3, plan.Targets.Count);

            var changedTarget = Assert.Single(
                plan.Targets,
                target => target.RelativePath == "Core.dll");
            Assert.Equal(UpdateTargetOperations.Replace, changedTarget.Operation);
            Assert.Equal(RepairReasons.HashMismatch, changedTarget.Reason);

            var addedTarget = Assert.Single(
                plan.Targets,
                target => target.RelativePath
                    == Path.Combine("providers", "NewProvider.dll"));
            Assert.Equal(UpdateTargetOperations.Replace, addedTarget.Operation);
            Assert.Equal(RepairReasons.Missing, addedTarget.Reason);

            var removedTarget = Assert.Single(
                plan.Targets,
                target => target.RelativePath
                    == Path.Combine("providers", "OldProvider.dll"));
            Assert.Equal(UpdateTargetOperations.Delete, removedTarget.Operation);
            Assert.Equal(RepairReasons.Removed, removedTarget.Reason);
            Assert.Equal("", removedTarget.DownloadUrl);
        }

        [Fact]
        public void CreateIncrementalPlan_UnmanagedLocalFile_DoesNotDeleteIt()
        {
            var managed = Encoding.UTF8.GetBytes("managed");
            CreateLocalFile("Core.dll", managed);
            CreateLocalFile("config/settings.json", Encoding.UTF8.GetBytes("user"));

            var installedManifest = CreateInstalledManifest(
                CreateInstalledFile("Core.dll", managed));
            var latestFiles = new[]
            {
                CreateManifestFile("Core.dll", managed)
            };

            var plan = _planner.CreateIncrementalPlan(
                _installDirectory,
                latestFiles,
                installedManifest);

            Assert.Empty(plan.Targets);
            Assert.True(File.Exists(Path.Combine(
                _installDirectory,
                "config",
                "settings.json")));
        }

        [Fact]
        public void CreateIncrementalPlan_RemovedManagedFileAlreadyMissing_DoesNotCreateDeleteTarget()
        {
            var current = Encoding.UTF8.GetBytes("current");
            var removed = Encoding.UTF8.GetBytes("removed");
            CreateLocalFile("Core.dll", current);

            var installedManifest = CreateInstalledManifest(
                CreateInstalledFile("Core.dll", current),
                CreateInstalledFile("Old.dll", removed));
            var latestFiles = new[]
            {
                CreateManifestFile("Core.dll", current)
            };

            var plan = _planner.CreateIncrementalPlan(
                _installDirectory,
                latestFiles,
                installedManifest);

            Assert.Empty(plan.Targets);
        }

        private static InstalledManifest CreateInstalledManifest(
            params InstalledManifestFile[] files)
        {
            return new InstalledManifest
            {
                ProductCode = "PCCAM",
                Architecture = "x86",
                Version = "1.0.0",
                InstalledAtUtc = DateTime.UtcNow,
                Files = new List<InstalledManifestFile>(files)
            };
        }

        private static InstalledManifestFile CreateInstalledFile(
            string relativePath,
            byte[] content)
        {
            return new InstalledManifestFile
            {
                Path = relativePath,
                Size = content.LongLength,
                Sha256 = CalculateSha256(content)
            };
        }

        private static UpdateManifestFile CreateManifestFile(
            string relativePath,
            byte[] content)
        {
            return new UpdateManifestFile
            {
                Path = relativePath,
                Size = content.LongLength,
                Sha256 = CalculateSha256(content),
                Required = true,
                DownloadUrl = "https://update.poscam.co.kr/packages/test"
            };
        }

        private void CreateLocalFile(string relativePath, byte[] content)
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
