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
                return Task.FromResult(UpdateClientExitCodes.Success);
            }

            var command = args[0].Trim().ToLowerInvariant();

            switch (command)
            {
                case "startup-check":
                    return RunPlaceholderAsync(cancellationToken);

                case "apply":
                    return RunPlaceholderAsync(cancellationToken);

                default:
                    return Task.FromResult(UpdateClientExitCodes.UnknownCommand);
            }
        }

        private static Task<int> RunPlaceholderAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(UpdateClientExitCodes.NotImplemented);
        }

        private static bool IsHelp(string value)
        {
            return value == "-h"
                || value == "--help"
                || value == "/?"
                || value == "help";
        }
    }
}
