using System;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;

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
                    return RunStartupCheckAsync(args, cancellationToken);

                case "apply":
                    return RunPlaceholderAsync(cancellationToken);

                default:
                    return Task.FromResult(
                        UpdateClientExitCodes.UnknownCommand);
            }
        }

        private static async Task<int> RunStartupCheckAsync(
            string[] args,
            CancellationToken cancellationToken)
        {
            StartupCheckOptions? options;

            if (!StartupCheckOptions.TryParse(args, out options)
                || options == null)
            {
                return UpdateClientExitCodes.UpdateCheckFailed;
            }

            try
            {
                using (var updateServerClient = new UpdateServerClient(
                    options.BaseUrl))
                {
                    var service = new StartupCheckService(
                        updateServerClient,
                        new ApplicationVersionResolver(),
                        new ManifestRepairPlanner());

                    var result = await service.CheckAsync(
                        options,
                        cancellationToken)
                        .ConfigureAwait(false);

                    return result.ExitCode;
                }
            }
            catch (ArgumentException)
            {
                return UpdateClientExitCodes.UpdateCheckFailed;
            }
        }

        private static Task<int> RunPlaceholderAsync(
            CancellationToken cancellationToken)
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
