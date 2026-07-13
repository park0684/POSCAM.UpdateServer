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
    public sealed class ManifestRepairPlannerTests : IDisposable
    {
        private readonly string _installDirectory;
        private readonly ManifestRepairPlanner _planner;

        public ManifestRepairPlannerTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_installDirectory);
            _planner = new ManifestRepairPlanner();
        }

        [Fact]
        public void CreatePlan_NullFiles_ReturnsEmptyPlan()
        {
            var plan = _planner.CreatePlan(_installDirectory, null);

            Assert.False(plan.HasRepairTargets);
            Assert.Empty(plan.Targets);
        }

        [Fact]
        public void CreatePlan_EmptyFiles_ReturnsEmptyPlan()
        {
            var plan = _planner.CreatePlan(
                _installDirectory,
                new List<UpdateManifestFile>());

            Assert.False(plan.HasRepairTargets);
            Assert.Empty(plan.Targets);
        }

        [Fact]
        public void CreatePlan_OptionalMissingFile_DoesNotCreateTarget()
        {
            var manifestFile = CreateManifestFile(
                "optional.dll",
                Encoding.UTF8.GetBytes("optional file content"),
                false);

            var plan = _planner.CreatePlan(
                _installDirectory,
                new[] { manifestFile });

            Assert.False(plan.HasRepairTargets);
            Assert.Empty(plan.Targets);
        }

        [Fact]
        public void CreatePlan_MissingFile_CreatesMissingTarget()
        {
            var manifestFile = CreateManifestFile(
                "providers/missing.dll",
                Encoding.UTF8.GetBytes("expected file content"));

            var plan = _planner.CreatePlan(
                _installDirectory,
                new[] { manifestFile });

            var target = Assert.Single(plan.Targets);

            Assert.True(plan.HasRepairTargets);
            Assert.Equal(RepairReasons.Missing, target.Reason);
            Assert.Equal(
                Path.Combine("providers", "missing.dll"),
                target.RelativePath);
            Assert.Equal(
                Path.Combine(_installDirectory, "providers", "missing.dll"),
                target.LocalPath);
        }

        [Fact]
        public void CreatePlan_SizeMismatch_CreatesSizeMismatchTarget()
        {
            CreateLocalFile(
                "providers/provider.dll",
                Encoding.UTF8.GetBytes("short"));

            var manifestFile = CreateManifestFile(
                "providers/provider.dll",
                Encoding.UTF8.GetBytes("expected long content"));

            var plan = _planner.CreatePlan(
                _installDirectory,
                new[] { manifestFile });

            var target = Assert.Single(plan.Targets);
            Assert.Equal(RepairReasons.SizeMismatch, target.Reason);
        }

        [Fact]
        public void CreatePlan_HashMismatch_CreatesHashMismatchTarget()
        {
            var expectedContent = Encoding.UTF8.GetBytes("ABCDEF");
            var actualContent = Encoding.UTF8.GetBytes("123456");

            Assert.Equal(expectedContent.Length, actualContent.Length);

            CreateLocalFile("PcCam.exe", actualContent);

            var manifestFile = CreateManifestFile(
                "PcCam.exe",
                expectedContent);

            var plan = _planner.CreatePlan(
                _installDirectory,
                new[] { manifestFile });

            var target = Assert.Single(plan.Targets);
            Assert.Equal(RepairReasons.HashMismatch, target.Reason);
        }

        [Fact]
        public void CreatePlan_MatchingFile_ReturnsEmptyPlan()
        {
            var content = Encoding.UTF8.GetBytes("matching file content");

            CreateLocalFile("providers/provider.dll", content);

            var manifestFile = CreateManifestFile(
                "providers/provider.dll",
                content);

            var plan = _planner.CreatePlan(
                _installDirectory,
                new[] { manifestFile });

            Assert.False(plan.HasRepairTargets);
            Assert.Empty(plan.Targets);
        }

        [Fact]
        public void CreatePlan_LowercaseSha256_IsAccepted()
        {
            var content = Encoding.UTF8.GetBytes("same content");

            CreateLocalFile("PcCam.exe", content);

            var manifestFile = CreateManifestFile("PcCam.exe", content);
            manifestFile.Sha256 = manifestFile.Sha256.ToLowerInvariant();

            var plan = _planner.CreatePlan(
                _installDirectory,
                new[] { manifestFile });

            Assert.False(plan.HasRepairTargets);
            Assert.Empty(plan.Targets);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("../outside.dll")]
        [InlineData(@"providers\..\outside.dll")]
        [InlineData(@"providers\.\provider.dll")]
        [InlineData(@"C:\Windows\system32\file.dll")]
        [InlineData(@"\Windows\file.dll")]
        [InlineData(@"\\server\share\file.dll")]
        [InlineData("providers//provider.dll")]
        [InlineData("file.dll:stream")]
        public void CreatePlan_UnsafePath_ThrowsInvalidDataException(
            string relativePath)
        {
            var manifestFile = CreateManifestFile(
                relativePath,
                Encoding.UTF8.GetBytes("content"));

            Assert.Throws<InvalidDataException>(
                () => _planner.CreatePlan(
                    _installDirectory,
                    new[] { manifestFile }));
        }

        [Fact]
        public void CreatePlan_PathContainingNullCharacter_Throws()
        {
            var manifestFile = CreateManifestFile(
                "providers/\0provider.dll",
                Encoding.UTF8.GetBytes("content"));

            Assert.Throws<InvalidDataException>(
                () => _planner.CreatePlan(
                    _installDirectory,
                    new[] { manifestFile }));
        }

        [Fact]
        public void CreatePlan_InvalidSha256Length_Throws()
        {
            var manifestFile = new UpdateManifestFile
            {
                Path = "PcCam.exe",
                Size = 10,
                Sha256 = "ABCDEF",
                Required = true,
                DownloadUrl = "/packages/file"
            };

            Assert.Throws<InvalidDataException>(
                () => _planner.CreatePlan(
                    _installDirectory,
                    new[] { manifestFile }));
        }

        [Fact]
        public void CreatePlan_NonHexSha256_Throws()
        {
            var manifestFile = new UpdateManifestFile
            {
                Path = "PcCam.exe",
                Size = 10,
                Sha256 = new string('Z', 64),
                Required = true,
                DownloadUrl = "/packages/file"
            };

            Assert.Throws<InvalidDataException>(
                () => _planner.CreatePlan(
                    _installDirectory,
                    new[] { manifestFile }));
        }

        [Fact]
        public void CreatePlan_NegativeFileSize_Throws()
        {
            var manifestFile = new UpdateManifestFile
            {
                Path = "PcCam.exe",
                Size = -1,
                Sha256 = new string('A', 64),
                Required = true,
                DownloadUrl = "/packages/file"
            };

            Assert.Throws<InvalidDataException>(
                () => _planner.CreatePlan(
                    _installDirectory,
                    new[] { manifestFile }));
        }

        [Fact]
        public void CreatePlan_DuplicateNormalizedPaths_Throws()
        {
            var content = Encoding.UTF8.GetBytes("same content");
            var first = CreateManifestFile(
                "providers/provider.dll",
                content);
            var second = CreateManifestFile(
                @"providers\provider.dll",
                content);

            Assert.Throws<InvalidDataException>(
                () => _planner.CreatePlan(
                    _installDirectory,
                    new[] { first, second }));
        }

        [Fact]
        public void CreatePlan_ForwardSlashPath_NormalizesToWindowsPath()
        {
            var manifestFile = CreateManifestFile(
                "providers/dahua/provider.dll",
                Encoding.UTF8.GetBytes("provider content"));

            var plan = _planner.CreatePlan(
                _installDirectory,
                new[] { manifestFile });

            var target = Assert.Single(plan.Targets);

            Assert.Equal(
                Path.Combine("providers", "dahua", "provider.dll"),
                target.RelativePath);
            Assert.Equal(RepairReasons.Missing, target.Reason);
        }

        private static UpdateManifestFile CreateManifestFile(
            string relativePath,
            byte[] content,
            bool required = true)
        {
            return new UpdateManifestFile
            {
                Path = relativePath,
                Size = content.LongLength,
                Sha256 = CalculateSha256(content),
                Required = required,
                DownloadUrl = "/packages/test/file"
            };
        }

        private void CreateLocalFile(string relativePath, byte[] content)
        {
            var normalizedPath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            var fullPath = Path.Combine(
                _installDirectory,
                normalizedPath);

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
