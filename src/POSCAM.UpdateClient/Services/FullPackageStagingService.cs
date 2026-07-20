using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 검증된 Full ZIP 패키지를 작업 폴더에 안전하게 추출한다.
    /// 설치 경로의 원본 파일은 변경하지 않는다.
    /// </summary>
    internal sealed class FullPackageStagingService
    {
        private const int UnixFileTypeMask = 0xF000;
        private const int UnixSymbolicLink = 0xA000;

        private readonly UpdateWorkPathService _pathService;
        private readonly FileHashCalculator _hashCalculator;

        public FullPackageStagingService(
            UpdateWorkPathService pathService,
            FileHashCalculator hashCalculator)
        {
            _pathService = pathService
                ?? throw new ArgumentNullException(nameof(pathService));
            _hashCalculator = hashCalculator
                ?? throw new ArgumentNullException(nameof(hashCalculator));
        }

        public FullPackageStagingResult Stage(
            UpdateApplyPlan plan,
            CancellationToken cancellationToken)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var packagePath = ValidatePlanAndGetPackagePath(plan);
            VerifyPackageFile(plan, packagePath);

            var jobDirectory = _pathService.GetJobDirectory(
                plan.InstallDirectory,
                plan.JobId);
            var temporaryRoot = Path.Combine(jobDirectory, "staging.tmp");
            var finalRoot = Path.Combine(jobDirectory, "staging");

            DeleteDirectoryIfExists(temporaryRoot);
            DeleteDirectoryIfExists(finalRoot);
            Directory.CreateDirectory(temporaryRoot);

            try
            {
                var targets = ExtractPackage(
                    plan,
                    packagePath,
                    jobDirectory,
                    cancellationToken);

                Directory.Move(temporaryRoot, finalRoot);

                return new FullPackageStagingResult
                {
                    StagingDirectory = finalRoot,
                    Targets = targets
                };
            }
            catch
            {
                DeleteDirectoryIfExists(temporaryRoot);
                DeleteDirectoryIfExists(finalRoot);
                throw;
            }
        }

        private string ValidatePlanAndGetPackagePath(UpdateApplyPlan plan)
        {
            var packageType = plan.PackageType;
            var packageFileNameValue = plan.PackageFileName;
            var packagePathValue = plan.PackagePath;
            var packageSize = plan.PackageSize;
            var packageSha256 = plan.PackageSha256;

            if (plan.PlanVersion != 1
                || !string.Equals(
                    plan.Mode,
                    UpdateApplyModes.FullPackage,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(plan.InstallDirectory)
                || string.IsNullOrWhiteSpace(plan.JobId)
                || string.IsNullOrWhiteSpace(plan.ApplicationFileName)
                || packageType == null
                || packageType.Trim().Length == 0
                || packageFileNameValue == null
                || packageFileNameValue.Trim().Length == 0
                || packagePathValue == null
                || packagePathValue.Trim().Length == 0
                || !packageSize.HasValue
                || packageSize.Value < 0
                || !IsValidSha256(packageSha256))
            {
                throw new InvalidDataException(
                    "Full Package 적용 계획이 올바르지 않습니다.");
            }

            var normalizedPackageType = packageType.Trim();
            var normalizedPackageFileName = packageFileNameValue.Trim();
            var normalizedPackagePath = packagePathValue.Trim();

            if (!string.Equals(
                normalizedPackageType,
                "full",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "지원하지 않는 Full Package 형식입니다.");
            }

            var jobDirectory = _pathService.GetJobDirectory(
                plan.InstallDirectory,
                plan.JobId);
            var packageFileName = _pathService.ValidateFileName(
                normalizedPackageFileName);
            var expectedPackagePath = _pathService.ResolveJobFilePath(
                jobDirectory,
                "package/" + packageFileName);
            var actualPackagePath = Path.GetFullPath(
                normalizedPackagePath);

            if (!string.Equals(
                expectedPackagePath,
                actualPackagePath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Full Package 파일 경로가 적용 계획과 일치하지 않습니다.");
            }

            return actualPackagePath;
        }

        private void VerifyPackageFile(
            UpdateApplyPlan plan,
            string packagePath)
        {
            if (!File.Exists(packagePath))
            {
                UpdateClientLog.Error(
                    plan.InstallDirectory,
                    "apply.prechange.package-missing",
                    "Full Package 파일을 찾을 수 없습니다."
                        + " PackagePath=" + packagePath);

                throw new FileNotFoundException(
                    "Full Package 파일을 찾을 수 없습니다.",
                    packagePath);
            }

            var packageFile = new FileInfo(packagePath);
            var expectedSize = plan.PackageSize!.Value;

            if (packageFile.Length != expectedSize)
            {
                UpdateClientLog.Error(
                    plan.InstallDirectory,
                    "apply.prechange.package-size-mismatch",
                    "Full Package 파일 크기가 적용 계획과 다릅니다."
                        + " PackagePath=" + packagePath
                        + " ExpectedSize=" + expectedSize
                        + " ActualSize=" + packageFile.Length);

                throw new InvalidDataException(
                    "Full Package 파일 크기가 적용 계획과 다릅니다.");
            }

            var expectedSha256 = plan.PackageSha256!.Trim();
            var actualSha256 = _hashCalculator.CalculateSha256(packagePath);

            if (!string.Equals(
                actualSha256,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase))
            {
                UpdateClientLog.Error(
                    plan.InstallDirectory,
                    "apply.prechange.package-sha256-mismatch",
                    "Full Package SHA-256이 적용 계획과 다릅니다."
                        + " PackagePath=" + packagePath
                        + " ExpectedSha256=" + expectedSha256
                        + " ActualSha256=" + actualSha256);

                throw new InvalidDataException(
                    "Full Package SHA-256이 적용 계획과 다릅니다.");
            }
        }

        private List<UpdateApplyTarget> ExtractPackage(
            UpdateApplyPlan plan,
            string packagePath,
            string jobDirectory,
            CancellationToken cancellationToken)
        {
            var targets = new List<UpdateApplyTarget>();
            var relativeFiles = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var containsApplication = false;

            using (var packageStream = new FileStream(
                packagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (var archive = new ZipArchive(
                packageStream,
                ZipArchiveMode.Read,
                false))
            {
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var normalizedEntryPath = NormalizeEntryPath(
                        entry.FullName);

                    if (normalizedEntryPath.Length == 0)
                    {
                        continue;
                    }

                    RejectSymbolicLink(entry);

                    var isDirectory = string.IsNullOrEmpty(entry.Name)
                        && (entry.FullName.EndsWith(
                                "/",
                                StringComparison.Ordinal)
                            || entry.FullName.EndsWith(
                                "\\",
                                StringComparison.Ordinal));

                    if (isDirectory)
                    {
                        var directoryPath = _pathService.ResolveJobFilePath(
                            jobDirectory,
                            "staging.tmp/" + normalizedEntryPath);
                        Directory.CreateDirectory(directoryPath);
                        continue;
                    }

                    _pathService.ResolveInstallFilePath(
                        plan.InstallDirectory,
                        normalizedEntryPath);
                    if (!relativeFiles.Add(normalizedEntryPath))
                    {
                        throw new InvalidDataException(
                            "Full Package에 중복된 파일 경로가 있습니다.");
                    }

                    var temporaryFilePath = _pathService.ResolveJobFilePath(
                        jobDirectory,
                        "staging.tmp/" + normalizedEntryPath);
                    var finalFilePath = _pathService.ResolveJobFilePath(
                        jobDirectory,
                        "staging/" + normalizedEntryPath);
                    var directory = Path.GetDirectoryName(temporaryFilePath);

                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        throw new InvalidDataException(
                            "Full Package 추출 디렉터리를 확인할 수 없습니다.");
                    }

                    Directory.CreateDirectory(directory);

                    using (var source = entry.Open())
                    using (var destination = new FileStream(
                        temporaryFilePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        source.CopyTo(destination);
                    }

                    var extractedFile = new FileInfo(temporaryFilePath);

                    if (extractedFile.Length != entry.Length)
                    {
                        throw new InvalidDataException(
                            "Full Package 추출 파일 크기가 ZIP 정보와 다릅니다.");
                    }

                    var sha256 = _hashCalculator.CalculateSha256(
                        temporaryFilePath);
                    targets.Add(new UpdateApplyTarget
                    {
                        RelativePath = normalizedEntryPath,
                        DownloadedPath = finalFilePath,
                        ExpectedSize = extractedFile.Length,
                        ExpectedSha256 = sha256,
                        Reason = UpdateApplyReasons.FullPackage
                    });

                    if (string.Equals(
                        normalizedEntryPath,
                        plan.ApplicationFileName.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        containsApplication = true;
                    }
                }
            }

            if (targets.Count == 0)
            {
                throw new InvalidDataException(
                    "Full Package에 적용할 파일이 없습니다.");
            }

            if (!containsApplication)
            {
                throw new InvalidDataException(
                    "Full Package에 대상 프로그램 파일이 없습니다.");
            }

            return targets;
        }

        private static string NormalizeEntryPath(string entryPath)
        {
            if (string.IsNullOrWhiteSpace(entryPath))
            {
                return "";
            }

            var normalized = entryPath
                .Replace('\\', '/')
                .TrimEnd('/');

            if (normalized.Length == 0)
            {
                return "";
            }

            return normalized;
        }

        private static void RejectSymbolicLink(ZipArchiveEntry entry)
        {
            var unixMode = (entry.ExternalAttributes >> 16)
                & UnixFileTypeMask;

            if (unixMode == UnixSymbolicLink)
            {
                throw new InvalidDataException(
                    "Full Package의 심볼릭 링크 항목은 허용하지 않습니다.");
            }
        }

        private static bool IsValidSha256(string? value)
        {
            if (value == null)
            {
                return false;
            }

            var normalized = value.Trim();

            if (normalized.Length != 64)
            {
                return false;
            }

            foreach (var character in normalized)
            {
                var isHex = character >= '0' && character <= '9'
                    || character >= 'a' && character <= 'f'
                    || character >= 'A' && character <= 'F';

                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
