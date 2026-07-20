using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// UI 없는 UpdateClient의 최소 운영 로그를 설치 루트의 logs 폴더에 기록한다.
    /// 로그 실패가 업데이트 결과나 exit code를 변경하지 않도록 모든 예외를 흡수한다.
    /// </summary>
    internal static class UpdateClientLog
    {
        private const string RedundantLegacyManifestEvent =
            "Preparation.LegacyManifestRemoved";

        private static readonly object SyncRoot = new object();

        public static void Info(
            string? installDirectory,
            string eventName,
            string message)
        {
            if (string.Equals(
                eventName,
                RedundantLegacyManifestEvent,
                StringComparison.Ordinal))
            {
                return;
            }

            Write(
                installDirectory,
                "INFO",
                eventName,
                message);
        }

        public static void Error(
            string? installDirectory,
            string eventName,
            string message,
            Exception? exception = null)
        {
            var detail = exception == null
                ? message
                : message
                    + " ExceptionType="
                    + exception.GetType().Name;

            Write(
                installDirectory,
                "ERROR",
                eventName,
                detail);
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

        private static void Write(
            string? installDirectory,
            string level,
            string eventName,
            string message)
        {
            try
            {
                if (installDirectory == null)
                {
                    return;
                }

                var normalizedInstallDirectory = installDirectory.Trim();

                if (normalizedInstallDirectory.Length == 0)
                {
                    return;
                }

                var localNow = DateTime.Now;
                var logPath = GetLogPath(
                    normalizedInstallDirectory,
                    localNow);
                var logDirectory = Path.GetDirectoryName(logPath);

                if (string.IsNullOrWhiteSpace(logDirectory))
                {
                    return;
                }

                Directory.CreateDirectory(logDirectory);

                var line = localNow.ToString(
                        "yyyy-MM-dd HH:mm:ss.fff",
                        CultureInfo.InvariantCulture)
                    + " | "
                    + Sanitize(level)
                    + " | Event="
                    + Sanitize(eventName)
                    + " | Message="
                    + Sanitize(message)
                    + Environment.NewLine;

                lock (SyncRoot)
                {
                    File.AppendAllText(
                        logPath,
                        line,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                // 로그 실패가 업데이트 흐름과 exit code를 변경하지 않도록 한다.
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
