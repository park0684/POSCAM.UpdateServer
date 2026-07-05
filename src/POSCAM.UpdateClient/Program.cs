using System.Threading;

namespace POSCAM.UpdateClient
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return UpdateClientApplication
                .RunAsync(args, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
    }
}
