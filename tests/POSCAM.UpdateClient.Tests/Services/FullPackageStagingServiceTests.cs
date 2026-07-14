using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;
using Xunit;

namespace POSCAM.UpdateClient.Tests.Services
{
    public sealed class FullPackageStagingServiceTests : IDisposable
    {
        private const string JobId = "full-package-job-001";
        private readonly string _installDirectory;
        private readonly UpdateWorkPathService _pathService;

        public FullPackageStagingServiceTests()
        {
            _installDirectory = Path.Combine(
                Path.GetTempPath(),
                "POSCAM.UpdateClient.FullPackageStagingTests",
                Guid.NewGuid().ToString("N"));
            _pathService = new UpdateWorkPathService();
            Directory.CreateDirectory(_installDirectory);
        }

        [Fact]
        public void Stage_ValidPackage_ExtractsFilesAndBuildsTargets()
        {
            var plan = CreatePlan(archive =>
            {
                AddEntry(archive, "PcCam.exe", "new app");
                AddEntry(archive, "providers/provider.dll", "provider");
            });

            var result = CreateService().Stage(
                plan,
                CancellationToken.None);

            Assert.True(Directory.Exists(result.StagingDirectory));
            Assert.Equal(2, result.Targets.Count);

            foreach (var target in result.Targets)
            {
                Assert.True(File.Exists(target.DownloadedPath));
                Assert.Equal(UpdateApplyReasons.FullPackage, target.Reason);
                Assert.Equal(
                    target.ExpectedSize,
                    new FileInfo(target.DownloadedPath).Length);
                Assert.Equal(
                    target.ExpectedSha256,
                    CalculateSha256(target.DownloadedPath));
            }
        }

        [Fact]
        public void Stage_PackageWithoutApplication_IsRejectedAndCleaned()
        {
            var plan = CreatePlan(archive =>
                AddEntry(archive, "providers/provider.dll", "provider"));

            Assert.Throws<InvalidDataException>(() => CreateService().Stage(
                plan,
                CancellationToken.None));

            AssertNoStagingDirectories();
        }

        [Theory]
        [InlineData("../outside.dll")]
        [InlineData("folder/../../outside.dll")]
        [InlineData("C:/outside.dll")]
        [InlineData("/outside.dll")]
        [InlineData("_update/state/repair-plan.json")]
        public void Stage_UnsafeEntryPath_IsRejected(string entryPath)
        {
            var plan = CreatePlan(archive =>
            {
                AddEntry(archive, "PcCam.exe", "new app");
                AddEntry(archive, entryPath, "unsafe");
            });

            Assert.ThrowsAny<Exception>(() => CreateService().Stage(
                plan,
                CancellationToken.None));

            AssertNoStagingDirectories();
        }

        [Fact]
        public void Stage_DuplicateCaseInsensitivePath_IsRejected()
        {
            var plan = CreatePlan(archive =>
            {
                AddEntry(archive, "PcCam.exe", "first");
                AddEntry(archive, "PCCAM.EXE", "second");
            });

            Assert.Throws<InvalidDataException>(() => CreateService().Stage(
                plan,
                CancellationToken.None));

            AssertNoStagingDirectories();
        }

        [Fact]
        public void Stage_SymbolicLinkEntry_IsRejected()
        {
            var plan = CreatePlan(archive =>
            {
                AddEntry(archive, "PcCam.exe", "new app");
                var link = AddEntry(archive, "provider-link.dll", "target");
                link.ExternalAttributes = unchecked((int)0xA0000000);
            });

            Assert.Throws<InvalidDataException>(() => CreateService().Stage(
                plan,
                CancellationToken.None));

            AssertNoStagingDirectories();
        }

        [Fact]
        public void Stage_PackageHashMismatch_IsRejectedBeforeExtraction()
        {
            var plan = CreatePlan(archive =>
                AddEntry(archive, "PcCam.exe", "new app"));
            plan.PackageSha256 = new string('A', 64);

            Assert.Throws<InvalidDataException>(() => CreateService().Stage(
                plan,
                CancellationToken.None));

            AssertNoStagingDirectories();
        }

        [Fact]
        public void Stage_PackagePathOutsideExpectedJobPath_IsRejected()
        {
            var plan = CreatePlan(archive =>
                AddEntry(archive, "PcCam.exe", "new app"));
            plan.PackagePath = Path.Combine(
                _installDirectory,
                "outside.zip");

            Assert.Throws<InvalidDataException>(() => CreateService().Stage(
                plan,
                CancellationToken.None));

            AssertNoStagingDirectories();
        }

        [Fact]
        public void Stage_CorruptZip_IsRejectedAndCleaned()
        {
            var plan = CreatePlan(archive =>
                AddEntry(archive, "PcCam.exe", "new app"));
            File.WriteAllText(plan.PackagePath!, "not a zip");
            plan.PackageSize = new FileInfo(plan.PackagePath!).Length;
            plan.PackageSha256 = CalculateSha256(plan.PackagePath!);

            Assert.Throws<InvalidDataException>(() => CreateService().Stage(
                plan,
                CancellationToken.None));

            AssertNoStagingDirectories();
        }

        [Fact]
        public void Stage_EmptyZip_IsRejectedAndCleaned()
        {
            var plan = CreatePlan(_ => { });

            Assert.Throws<InvalidDataException>(() => CreateService().Stage(
                plan,
                CancellationToken.None));

            AssertNoStagingDirectories();
        }

        [Fact]
        public void Stage_PreCanceled_IsCanceledAndCleaned()
        {
            var plan = CreatePlan(archive =>
                AddEntry(archive, "PcCam.exe", "new app"));
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(
                () => CreateService().Stage(
                    plan,
                    cancellation.Token));

            AssertNoStagingDirectories();
        }

        private FullPackageStagingService CreateService()
        {
            return new FullPackageStagingService(
                _pathService,
                new FileHashCalculator());
        }

        private UpdateApplyPlan CreatePlan(
            Action<ZipArchive> populateArchive)
        {
            var jobDirectory = _pathService.GetJobDirectory(
                _installDirectory,
                JobId);
            var packagePath = _pathService.ResolveJobFilePath(
                jobDirectory,
                "package/pccam.zip");
            var packageDirectory = Path.GetDirectoryName(packagePath);

            Assert.False(string.IsNullOrWhiteSpace(packageDirectory));
            Directory.CreateDirectory(packageDirectory!);

            using (var stream = new FileStream(
                packagePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None))
            using (var archive = new ZipArchive(
                stream,
                ZipArchiveMode.Create,
                false))
            {
                populateArchive(archive);
            }

            return new UpdateApplyPlan
            {
                JobId = JobId,
                InstallDirectory = _installDirectory,
                ApplicationFileName = "PcCam.exe",
                Mode = UpdateApplyModes.FullPackage,
                CreatedAtUtc = DateTime.UtcNow,
                PackageType = "full",
                PackageFileName = "pccam.zip",
                PackagePath = packagePath,
                PackageSize = new FileInfo(packagePath).Length,
                PackageSha256 = CalculateSha256(packagePath)
            };
        }

        private static ZipArchiveEntry AddEntry(
            ZipArchive archive,
            string path,
            string content)
        {
            var entry = archive.CreateEntry(path);

            using (var writer = new StreamWriter(
                entry.Open(),
                new UTF8Encoding(false)))
            {
                writer.Write(content);
            }

            return entry;
        }

        private void AssertNoStagingDirectories()
        {
            var jobDirectory = _pathService.GetJobDirectory(
                _installDirectory,
                JobId);

            Assert.False(Directory.Exists(
                Path.Combine(jobDirectory, "staging.tmp")));
            Assert.False(Directory.Exists(
                Path.Combine(jobDirectory, "staging")));
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
                // 테스트 정리 실패가 테스트 결과를 덮어쓰지 않도록 한다.
            }
        }
    }
}
