using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// UI 없는 UpdateClient의 최소 운영 로그를 설치 루트 아래에 기록한다.
    /// 로그 실패가 업데이트 결과나 exit code를 변경하지 않도록 모든 예외를 흡수한다.
    /// </summary>
    internal static class UpdateClientLog
    {
        private static readonly object SyncRoot = new object();

        public static void Info(
            string? installDirectory,
            string eventName,
            string message)
        {
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
                if (string.IsNullOrWhiteSpace(planPath))
                {
                    return null;
                }

                var fullPlanPath = Path.GetFullPath(planPath.Trim());
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
            DateTime utcNow)
        {
            var installRoot = Path.GetFullPath(
                installDirectory.Trim());
            var fileName = "updateclient-"
                + utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                + ".log";

            return Path.Combine(
                installRoot,
                "_update",
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
                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    return;
                }

                var utcNow = DateTime.UtcNow;
                var logPath = GetLogPath(
                    installDirectory,
                    utcNow);
                var logDirectory = Path.GetDirectoryName(logPath);

                if (string.IsNullOrWhiteSpace(logDirectory))
                {
                    return;
                }

                Directory.CreateDirectory(logDirectory);

                var line = utcNow.ToString(
                        "O",
                        CultureInfo.InvariantCulture)
                    + "\t"
                    + Sanitize(level)
                    + "\t"
                    + Sanitize(eventName)
                    + "\t"
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
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Trim();
        }
    }
}
