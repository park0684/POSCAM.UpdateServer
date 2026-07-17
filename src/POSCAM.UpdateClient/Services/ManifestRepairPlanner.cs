using System;
using System.Collections.Generic;
using System.IO;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// UpdateServer Manifest와 로컬 설치 파일을 비교해 복구 또는 증분 업데이트 계획을 생성한다.
    /// </summary>
    internal sealed class ManifestRepairPlanner
    {
        private readonly FileHashCalculator _fileHashCalculator;

        public ManifestRepairPlanner()
            : this(new FileHashCalculator())
        {
        }

        internal ManifestRepairPlanner(FileHashCalculator fileHashCalculator)
        {
            _fileHashCalculator = fileHashCalculator
                ?? throw new ArgumentNullException(nameof(fileHashCalculator));
        }

        public RepairPlan CreatePlan(
            string installDirectory,
            IEnumerable<UpdateManifestFile>? files)
        {
            var installRoot = NormalizeInstallRoot(installDirectory);
            var plan = new RepairPlan();
            AddReplaceTargets(
                installRoot,
                files,
                plan,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return plan;
        }

        public RepairPlan CreateIncrementalPlan(
            string installDirectory,
            IEnumerable<UpdateManifestFile>? latestFiles,
            InstalledManifest installedManifest)
        {
            if (installedManifest == null)
            {
                throw new ArgumentNullException(nameof(installedManifest));
            }

            if (installedManifest.ManifestVersion != 1
                || installedManifest.Files == null)
            {
                throw new InvalidDataException(
                    "기존 설치 Manifest가 올바르지 않습니다.");
            }

            var installRoot = NormalizeInstallRoot(installDirectory);
            var plan = new RepairPlan();
            var latestPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            AddReplaceTargets(
                installRoot,
                latestFiles,
                plan,
                latestPaths);
            AddDeleteTargets(
                installRoot,
                installedManifest.Files,
                latestPaths,
                plan);

            return plan;
        }

        private void AddReplaceTargets(
            string installRoot,
            IEnumerable<UpdateManifestFile>? files,
            RepairPlan plan,
            ISet<string> processedPaths)
        {
            if (files == null)
            {
                return;
            }

            foreach (var manifestFile in files)
            {
                if (manifestFile == null)
                {
                    throw new InvalidDataException(
                        "Manifest 파일 항목이 null입니다.");
                }

                if (!manifestFile.Required)
                {
                    continue;
                }

                ValidateManifestFile(manifestFile);

                string normalizedRelativePath;
                var localPath = GetSafeLocalPath(
                    installRoot,
                    manifestFile.Path,
                    out normalizedRelativePath);

                if (!processedPaths.Add(normalizedRelativePath))
                {
                    throw new InvalidDataException(
                        "Manifest에 동일한 파일 경로가 중복되어 있습니다: "
                        + normalizedRelativePath);
                }

                var expectedSha256 = NormalizeSha256(
                    manifestFile.Sha256);

                if (!File.Exists(localPath))
                {
                    plan.Targets.Add(CreateReplaceTarget(
                        manifestFile,
                        normalizedRelativePath,
                        localPath,
                        expectedSha256,
                        RepairReasons.Missing));
                    continue;
                }

                var fileInfo = new FileInfo(localPath);

                if (fileInfo.Length != manifestFile.Size)
                {
                    plan.Targets.Add(CreateReplaceTarget(
                        manifestFile,
                        normalizedRelativePath,
                        localPath,
                        expectedSha256,
                        RepairReasons.SizeMismatch));
                    continue;
                }

                var actualSha256 = _fileHashCalculator
                    .CalculateSha256(localPath);

                if (!string.Equals(
                    actualSha256,
                    expectedSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    plan.Targets.Add(CreateReplaceTarget(
                        manifestFile,
                        normalizedRelativePath,
                        localPath,
                        expectedSha256,
                        RepairReasons.HashMismatch));
                }
            }
        }

        private static void AddDeleteTargets(
            string installRoot,
            IEnumerable<InstalledManifestFile> installedFiles,
            ISet<string> latestPaths,
            RepairPlan plan)
        {
            var previousPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var installedFile in installedFiles)
            {
                if (installedFile == null)
                {
                    throw new InvalidDataException(
                        "기존 설치 Manifest 파일 항목이 null입니다.");
                }

                if (installedFile.Size < 0)
                {
                    throw new InvalidDataException(
                        "기존 설치 Manifest 파일 크기는 음수일 수 없습니다.");
                }

                NormalizeSha256(installedFile.Sha256);

                string normalizedRelativePath;
                var localPath = GetSafeLocalPath(
                    installRoot,
                    installedFile.Path,
                    out normalizedRelativePath);

                if (!previousPaths.Add(normalizedRelativePath))
                {
                    throw new InvalidDataException(
                        "기존 설치 Manifest에 동일한 파일 경로가 중복되어 있습니다: "
                        + normalizedRelativePath);
                }

                if (latestPaths.Contains(normalizedRelativePath)
                    || !File.Exists(localPath))
                {
                    continue;
                }

                plan.Targets.Add(new RepairTarget
                {
                    Operation = UpdateTargetOperations.Delete,
                    RelativePath = normalizedRelativePath,
                    LocalPath = localPath,
                    ExpectedSize = 0,
                    ExpectedSha256 = "",
                    DownloadUrl = "",
                    Reason = RepairReasons.Removed
                });
            }
        }

        private static string NormalizeInstallRoot(string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new ArgumentException(
                    "설치 경로가 비어 있습니다.",
                    nameof(installDirectory));
            }

            try
            {
                return Path.GetFullPath(installDirectory.Trim());
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException)
            {
                throw new ArgumentException(
                    "설치 경로가 올바르지 않습니다.",
                    nameof(installDirectory),
                    exception);
            }
        }

        private static void ValidateManifestFile(
            UpdateManifestFile manifestFile)
        {
            if (manifestFile.Size < 0)
            {
                throw new InvalidDataException(
                    "Manifest 파일 크기는 음수일 수 없습니다: "
                    + manifestFile.Path);
            }

            if (string.IsNullOrWhiteSpace(manifestFile.DownloadUrl))
            {
                throw new InvalidDataException(
                    "Manifest 파일 다운로드 URL이 비어 있습니다: "
                    + manifestFile.Path);
            }

            NormalizeSha256(manifestFile.Sha256);
        }

        private static string GetSafeLocalPath(
            string installRoot,
            string manifestPath,
            out string normalizedRelativePath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new InvalidDataException(
                    "Manifest 파일 경로가 비어 있습니다.");
            }

            if (manifestPath.IndexOf('\0') >= 0)
            {
                throw new InvalidDataException(
                    "Manifest 파일 경로에 NUL 문자가 포함되어 있습니다.");
            }

            var trimmedPath = manifestPath.Trim();

            if (!string.Equals(
                trimmedPath,
                manifestPath,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Manifest 파일 경로의 앞뒤에 공백이 있습니다: "
                    + manifestPath);
            }

            if (Path.IsPathRooted(trimmedPath))
            {
                throw new InvalidDataException(
                    "절대경로는 사용할 수 없습니다: "
                    + manifestPath);
            }

            if (trimmedPath.IndexOf(':') >= 0)
            {
                throw new InvalidDataException(
                    "콜론이 포함된 경로는 사용할 수 없습니다: "
                    + manifestPath);
            }

            if (trimmedPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                throw new InvalidDataException(
                    "Manifest 파일 경로에 사용할 수 없는 문자가 있습니다: "
                    + manifestPath);
            }

            var segments = trimmedPath.Split(
                new[] { '\\', '/' },
                StringSplitOptions.None);

            if (segments.Length == 0)
            {
                throw new InvalidDataException(
                    "Manifest 파일 경로가 올바르지 않습니다.");
            }

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    throw new InvalidDataException(
                        "Manifest 파일 경로에 빈 구간이 있습니다: "
                        + manifestPath);
                }

                if (segment == "." || segment == "..")
                {
                    throw new InvalidDataException(
                        "상위 또는 현재 경로 구간은 사용할 수 없습니다: "
                        + manifestPath);
                }

                if (!string.Equals(
                    segment,
                    segment.Trim(),
                    StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "파일 경로 구간의 앞뒤에 공백이 있습니다: "
                        + manifestPath);
                }

                if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new InvalidDataException(
                        "파일 경로 구간에 사용할 수 없는 문자가 있습니다: "
                        + manifestPath);
                }
            }

            normalizedRelativePath = string.Join(
                Path.DirectorySeparatorChar.ToString(),
                segments);

            if (string.IsNullOrWhiteSpace(
                Path.GetFileName(normalizedRelativePath)))
            {
                throw new InvalidDataException(
                    "파일명이 없는 경로입니다: "
                    + manifestPath);
            }

            string candidatePath;

            try
            {
                candidatePath = Path.GetFullPath(
                    Path.Combine(
                        installRoot,
                        normalizedRelativePath));
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException)
            {
                throw new InvalidDataException(
                    "Manifest 파일 경로를 해석할 수 없습니다: "
                    + manifestPath,
                    exception);
            }

            var rootPrefix = installRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!candidatePath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Manifest 파일 경로가 설치 루트를 벗어납니다: "
                    + manifestPath);
            }

            return candidatePath;
        }

        private static string NormalizeSha256(string sha256)
        {
            if (string.IsNullOrWhiteSpace(sha256))
            {
                throw new InvalidDataException(
                    "SHA-256 값이 비어 있습니다.");
            }

            var normalized = sha256.Trim();

            if (normalized.Length != 64)
            {
                throw new InvalidDataException(
                    "SHA-256 값은 64자리여야 합니다.");
            }

            foreach (var character in normalized)
            {
                var isHex =
                    character >= '0' && character <= '9'
                    || character >= 'a' && character <= 'f'
                    || character >= 'A' && character <= 'F';

                if (!isHex)
                {
                    throw new InvalidDataException(
                        "SHA-256 값에 16진수가 아닌 문자가 포함되어 있습니다.");
                }
            }

            return normalized.ToUpperInvariant();
        }

        private static RepairTarget CreateReplaceTarget(
            UpdateManifestFile manifestFile,
            string relativePath,
            string localPath,
            string expectedSha256,
            string reason)
        {
            return new RepairTarget
            {
                Operation = UpdateTargetOperations.Replace,
                RelativePath = relativePath,
                LocalPath = localPath,
                ExpectedSize = manifestFile.Size,
                ExpectedSha256 = expectedSha256,
                DownloadUrl = manifestFile.DownloadUrl,
                Reason = reason
            };
        }
    }
}
