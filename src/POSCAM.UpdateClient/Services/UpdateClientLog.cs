using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 실제 업데이트가 성공적으로 완료된 경우에만 설치 루트의 logs 폴더에
    /// 완료 기록을 남긴다. 일반 시작 확인, 무작업, 실패 및 Rollback 과정은
    /// 영구 업데이트 로그를 생성하지 않는다.
    /// </summary>
    internal static class UpdateClientLog
    {
        private const int LogRetentionDays = 30;
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// 기존 호출부 호환용이다. 일반 진행 정보는 영구 파일로 기록하지 않는다.
        /// </summary>
        public static void Info(
            string? installDirectory,
            string eventName,
            string message)
        {
        }

        /// <summary>
        /// 기존 호출부 호환용이다. 실패한 업데이트는 완료 로그로 남기지 않는다.
        /// </summary>
        public static void Error(
            string? installDirectory,
            string eventName,
            string message,
            Exception? exception = null)
        {
        }

        /// <summary>
        /// 업데이트 적용, 프로그램 재실행 및 Job 백업 정리가 모두 끝난 뒤
        /// 성공 완료 결과를 한 줄 기록한다.
        /// </summary>
        public static void Completed(
            string? installDirectory,
            string? jobId,
            string? mode,
            string? version,
            int updatedFileCount)
        {
            try
            {
                if (installDirectory == null
                    || jobId == null)
                {
                    return;
                }

                var normalizedInstallDirectory = installDirectory.Trim();
                var normalizedJobId = jobId.Trim();

                if (normalizedInstallDirectory.Length == 0
                    || normalizedJobId.Length == 0
                    || normalizedJobId.IndexOfAny(
                        new[]
                        {
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar
                        }) >= 0)
                {
                    return;
                }

                var installRoot = Path.GetFullPath(
                    normalizedInstallDirectory);
                var backupJobDirectory = Path.Combine(
                    installRoot,
                    "_update",
                    "backups",
                    normalizedJobId);
                var downloadJobDirectory = Path.Combine(
                    installRoot,
                    "_update",
                    "downloads",
                    normalizedJobId);

                // 완료 정리가 끝나지 않았다면 성공 로그를 남기지 않는다.
                if (Directory.Exists(backupJobDirectory)
                    || Directory.Exists(downloadJobDirectory))
                {
                    return;
                }

                var localNow = DateTime.Now;
                var logPath = GetLogPath(
                    installRoot,
                    localNow);
                var logDirectory = Path.GetDirectoryName(logPath);

                if (string.IsNullOrWhiteSpace(logDirectory))
                {
                    return;
                }

                lock (SyncRoot)
                {
                    Directory.CreateDirectory(logDirectory);
                    DeleteExpiredLogs(
                        logDirectory,
                        logPath,
                        localNow);

                    var line = localNow.ToString(
                            "yyyy-MM-dd HH:mm:ss.fff",
                            CultureInfo.InvariantCulture)
                        + " | Result=Success"
                        + " | Mode=" + Sanitize(mode)
                        + " | Version=" + Sanitize(version)
                        + " | UpdatedFiles="
                        + Math.Max(0, updatedFileCount)
                            .ToString(CultureInfo.InvariantCulture)
                        + " | BackupCleanup=Success"
                        + Environment.NewLine;

                    File.AppendAllText(
                        logPath,
                        line,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                // 완료 로그 실패가 업데이트 성공 결과를 변경하지 않도록 한다.
            }
        }

        public static string? TryResolveInstallDirectoryFromPlanPath(
            string? planPath)
        {
            try
            {
                if (planPath == null)
                {
                    return null;
                }

                var normalizedPlanPath = planPath.Trim();

                if (normalizedPlanPath.Length == 0)
                {
                    return null;
                }

                var fullPlanPath = Path.GetFullPath(normalizedPlanPath);
                var stateDirectory = Directory.GetParent(fullPlanPath);
                var updateDirectory = stateDirectory?.Parent;
                var installDirectory = updateDirectory?.Parent;

                if (stateDirectory == null
                    || updateDirectory == null
                    || installDirectory == null
                    || !string.Equals(
                        stateDirectory.Name,
                        "state",
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(
                        updateDirectory.Name,
                        "_update",
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(
                        Path.GetFileName(fullPlanPath),
                        "repair-plan.json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return Path.GetFullPath(installDirectory.FullName);
            }
            catch
            {
                return null;
            }
        }

        internal static string GetLogPath(
            string installDirectory,
            DateTime localNow)
        {
            var installRoot = Path.GetFullPath(
                installDirectory.Trim());
            var fileName = "update_"
                + localNow.ToString(
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture)
                + ".log";

            return Path.Combine(
                installRoot,
                "logs",
                fileName);
        }

        private static void DeleteExpiredLogs(
            string logDirectory,
            string currentLogPath,
            DateTime localNow)
        {
            var cutoff = localNow.AddDays(-LogRetentionDays);
            var normalizedCurrentLogPath = Path.GetFullPath(
                currentLogPath);

            foreach (var candidate in Directory.GetFiles(
                logDirectory,
                "update_*.log",
                SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var fullCandidate = Path.GetFullPath(candidate);

                    if (string.Equals(
                        fullCandidate,
                        normalizedCurrentLogPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (File.GetLastWriteTime(candidate) < cutoff)
                    {
                        File.Delete(candidate);
                    }
                }
                catch
                {
                    // 개별 오래된 로그 삭제 실패는 완료 기록을 막지 않는다.
                }
            }
        }

        private static string Sanitize(string? value)
        {
            if (value == null)
            {
                return "-";
            }

            var normalized = value.Trim();

            if (normalized.Length == 0)
            {
                return "-";
            }

            return normalized
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Trim();
        }
    }
}
