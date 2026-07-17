using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace POSCAM.UpdateClient.Services
{
    /// <summary>
    /// 대상 프로세스가 자발적으로 종료할 때까지 기다린다.
    /// 강제 종료는 수행하지 않는다.
    /// </summary>
    internal sealed class ProcessWaitService : IProcessWaitService
    {
        private static readonly TimeSpan PollInterval =
            TimeSpan.FromMilliseconds(100);

        public async Task<bool> WaitForExitAsync(
            int processId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (processId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(processId));
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            Process process;

            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return true;
            }

            using (process)
            {
                var startedAt = DateTime.UtcNow;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (process.HasExited)
                        {
                            return true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        return true;
                    }

                    if (DateTime.UtcNow - startedAt >= timeout)
                    {
                        return false;
                    }

                    await Task.Delay(PollInterval, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }
}
