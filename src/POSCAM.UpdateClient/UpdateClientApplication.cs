using System;
using System.Threading;
using System.Threading.Tasks;

namespace POSCAM.UpdateClient
{
    internal static class UpdateClientApplication
    {
        public static Task<int> RunAsync(
            string[] args,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (args.Length == 0 || IsHelp(args[0]))
            {
                WriteUsage();
                return Task.FromResult(0);
            }

            var command = args[0].Trim().ToLowerInvariant();

            switch (command)
            {
                case "plan-repair":
                    return RunPlaceholderAsync(
                        "Manifest repair planning is not implemented yet.",
                        cancellationToken);

                case "apply-repair":
                    return RunPlaceholderAsync(
                        "Manifest repair apply is not implemented yet.",
                        cancellationToken);

                default:
                    return UnknownCommandAsync(command);
            }
        }

        private static Task<int> RunPlaceholderAsync(
            string message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.Error.WriteLine(message);
            return Task.FromResult(2);
        }

        private static Task<int> UnknownCommandAsync(string command)
        {
            Console.Error.WriteLine("Unknown command: " + command);
            WriteUsage();
            return Task.FromResult(1);
        }

        private static bool IsHelp(string value)
        {
            return value == "-h"
                || value == "--help"
                || value == "/?"
                || value == "help";
        }

        private static void WriteUsage()
        {
            Console.WriteLine("POSCAM.UpdateClient");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  POSCAM.UpdateClient plan-repair  <options>");
            Console.WriteLine("  POSCAM.UpdateClient apply-repair <options>");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  plan-repair   Inspect an update manifest and create a repair plan.");
            Console.WriteLine("  apply-repair  Apply a prepared repair plan by backup, replace, verify, and rollback on failure.");
        }
    }
}
