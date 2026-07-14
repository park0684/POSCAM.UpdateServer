using System;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Services;

namespace POSCAM.UpdateClient.Tests.TestDoubles
{
    internal sealed class FakeProcessWaitService : IProcessWaitService
    {
        public bool Result { get; set; } = true;

        public int LastProcessId { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public Task<bool> WaitForExitAsync(
            int processId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastProcessId = processId;
            LastTimeout = timeout;
            return Task.FromResult(Result);
        }
    }
}
