using System;
using System.Net;

namespace POSCAM.UpdateClient.Services
{
    internal sealed class UpdateServerClientException : Exception
    {
        public UpdateServerClientException(
            string message,
            HttpStatusCode? statusCode = null,
            int? errorCode = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        public HttpStatusCode? StatusCode { get; }

        public int? ErrorCode { get; }
    }
}
