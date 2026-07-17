using System;
using System.Threading;
using System.Threading.Tasks;

namespace POSCAM.UpdateClient.Services
{
    internal interface IProcessWaitService
    {
        Task<bool> WaitForExitAsync(
            int processId,
            TimeSpan timeout,
            CancellationToken cancellationToken);
    }
}
