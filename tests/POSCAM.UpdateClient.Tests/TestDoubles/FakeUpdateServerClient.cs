using System;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;

namespace POSCAM.UpdateClient.Tests.TestDoubles
{
    internal sealed class FakeUpdateServerClient : IUpdateServerClient
    {
        public UpdateCheckResponse? Response { get; set; }

        public Exception? ExceptionToThrow { get; set; }

        public UpdateCheckRequest? LastRequest { get; private set; }

        public Task<UpdateCheckResponse> CheckAsync(
            UpdateCheckRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(
                Response ?? new UpdateCheckResponse());
        }
    }
}
