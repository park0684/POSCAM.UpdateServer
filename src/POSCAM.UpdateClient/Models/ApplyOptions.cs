using System;
using System.Collections.Generic;

namespace POSCAM.UpdateClient.Models
{
    /// <summary>
    /// apply 명령의 실행 옵션이다.
    /// </summary>
    internal sealed class ApplyOptions
    {
        public const int DefaultWaitTimeoutSeconds = 60;

        public string PlanPath { get; set; } = "";

        public int WaitProcessId { get; set; }

        public string RestartFileName { get; set; } = "";

        public int WaitTimeoutSeconds { get; set; }
            = DefaultWaitTimeoutSeconds;

        public static bool TryParse(
            string[] args,
            out ApplyOptions? options)
        {
            options = null;

            if (args == null || args.Length < 7)
            {
                return false;
            }

            var values = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            for (var index = 1; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length)
                {
                    return false;
                }

                var key = args[index];
                var value = args[index + 1];

                if (string.IsNullOrWhiteSpace(key)
                    || !key.StartsWith("--", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(value)
                    || values.ContainsKey(key)
                    || !IsSupportedKey(key))
                {
                    return false;
                }

                values.Add(key, value.Trim());
            }

            string planPath;
            string processIdText;
            string restartFileName;

            if (!values.TryGetValue("--plan", out planPath)
                || !values.TryGetValue(
                    "--wait-process-id",
                    out processIdText)
                || !values.TryGetValue("--restart", out restartFileName))
            {
                return false;
            }

            int processId;

            if (!int.TryParse(processIdText, out processId)
                || processId <= 0)
            {
                return false;
            }

            var timeoutSeconds = DefaultWaitTimeoutSeconds;
            string timeoutText;

            if (values.TryGetValue(
                "--wait-timeout-seconds",
                out timeoutText)
                && (!int.TryParse(timeoutText, out timeoutSeconds)
                    || timeoutSeconds <= 0
                    || timeoutSeconds > 600))
            {
                return false;
            }

            options = new ApplyOptions
            {
                PlanPath = planPath,
                WaitProcessId = processId,
                RestartFileName = restartFileName,
                WaitTimeoutSeconds = timeoutSeconds
            };

            return true;
        }

        private static bool IsSupportedKey(string key)
        {
            return string.Equals(
                    key,
                    "--plan",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--wait-process-id",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--restart",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    key,
                    "--wait-timeout-seconds",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
