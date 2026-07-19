using System;
using System.IO;
using System.Text;
using System.Threading;

namespace POSCAM.UpdateClient.ProcessHost
{
    /// <summary>
    /// 프로세스 통합 테스트에서 빌드 결과 경로를 찾기 위한 공개 표식 형식이다.
    /// </summary>
    public sealed class ProcessHostMarker
    {
    }

    internal static class Program
    {
        private const string InvocationLogFileName =
            "host-invocations.log";

        private static int Main(string[] args)
        {
            if (TryRunHoldMode(args, out var holdExitCode))
            {
                return holdExitCode;
            }

            var logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                InvocationLogFileName);
            var line = BuildInvocationLine(args);

            File.AppendAllText(
                logPath,
                line + Environment.NewLine,
                new UTF8Encoding(false));

            return 0;
        }

        private static bool TryRunHoldMode(
            string[] args,
            out int exitCode)
        {
            exitCode = 0;

            if (args == null
                || args.Length != 2
                || !string.Equals(
                    args[0],
                    "--hold-ms",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!int.TryParse(args[1], out var milliseconds)
                || milliseconds <= 0
                || milliseconds > 30000)
            {
                exitCode = 2;
                return true;
            }

            Thread.Sleep(milliseconds);
            return true;
        }

        private static string BuildInvocationLine(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "<none>";
            }

            var builder = new StringBuilder();

            for (var index = 0; index < args.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(args[index] ?? "");
            }

            return builder.ToString();
        }
    }
}
