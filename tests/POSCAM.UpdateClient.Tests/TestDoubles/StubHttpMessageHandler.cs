using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace POSCAM.UpdateClient.Tests.TestDoubles
{
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage>
            _responseFactory;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public Uri? RequestedUri { get; private set; }

        public string RequestBody { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RequestedUri = request.RequestUri;
            RequestBody = request.Content == null
                ? ""
                : request.Content.ReadAsStringAsync()
                    .GetAwaiter()
                    .GetResult();

            return Task.FromResult(_responseFactory(request));
        }
    }
}
