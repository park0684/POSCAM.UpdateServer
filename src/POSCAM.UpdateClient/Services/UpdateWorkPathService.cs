using System;
using System.IO;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 설치 루트 아래의 업데이트 작업 경로를 생성하고 안전하게 해석한다.
    /// </summary>
    internal sealed class UpdateWorkPathService
    {
        public UpdateWorkPaths Create(string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new ArgumentException(
                    "설치 경로가 비어 있습니다.",
                    nameof(installDirectory));
            }

            var installRoot = Path.GetFullPath(installDirectory.Trim());
            var updateRoot = Path.Combine(installRoot, "_update");
            var downloadsRoot = Path.Combine(updateRoot, "downloads");
            var stateDirectory = Path.Combine(updateRoot, "state");
            var jobId = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")
                + "-"
                + Guid.NewGuid().ToString("N");
            var jobDirectory = Path.Combine(downloadsRoot, jobId);

            return new UpdateWorkPaths
            {
                JobId = jobId,
                InstallDirectory = installRoot,
                UpdateRootDirectory = updateRoot,
                DownloadsRootDirectory = downloadsRoot,
                JobDirectory = jobDirectory,
                StateDirectory = stateDirectory,
                ActivePlanPath = Path.Combine(
                    stateDirectory,
                    "repair-plan.json")
            };
        }

        public string ResolveJobFilePath(
            string jobDirectory,
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(jobDirectory))
            {
                throw new ArgumentException(
                    "작업 경로가 비어 있습니다.",
                    nameof(jobDirectory));
            }

            var normalizedRelativePath = NormalizeRelativePath(relativePath);
            var jobRoot = Path.GetFullPath(jobDirectory.Trim());
            var candidatePath = Path.GetFullPath(
                Path.Combine(jobRoot, normalizedRelativePath));
            var rootPrefix = jobRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!candidatePath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "다운로드 경로가 작업 루트를 벗어납니다.");
            }

            return candidatePath;
        }

        public string ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidDataException(
                    "패키지 파일명이 비어 있습니다.");
            }

            var normalized = fileName.Trim();

            if (!string.Equals(
                    normalized,
                    fileName,
                    StringComparison.Ordinal)
                || Path.IsPathRooted(normalized)
                || normalized.IndexOf('/') >= 0
                || normalized.IndexOf('\\') >= 0
                || normalized.IndexOf(':') >= 0
                || normalized.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0
                || normalized.EndsWith(".", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "패키지 파일명이 올바르지 않습니다.");
            }

            return normalized;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidDataException(
                    "상대 경로가 비어 있습니다.");
            }

            if (relativePath.IndexOf('\0') >= 0
                || Path.IsPathRooted(relativePath)
                || relativePath.IndexOf(':') >= 0)
            {
                throw new InvalidDataException(
                    "상대 경로가 올바르지 않습니다.");
            }

            var segments = relativePath.Split(
                new[] { '/', '\\' },
                StringSplitOptions.None);

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment)
                    || segment == "."
                    || segment == ".."
                    || !string.Equals(
                        segment,
                        segment.Trim(),
                        StringComparison.Ordinal)
                    || segment.EndsWith(".", StringComparison.Ordinal)
                    || segment.IndexOfAny(
                        Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new InvalidDataException(
                        "상대 경로에 사용할 수 없는 구간이 있습니다.");
                }
            }

            return string.Join(
                Path.DirectorySeparatorChar.ToString(),
                segments);
        }
    }
}
