using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;
using POSCAM.UpdateClient.Services;

namespace POSCAM.UpdateClient.Tests.TestDoubles
{
    internal sealed class FakeUpdateFileDownloadService :
        IUpdateFileDownloadService
    {
        public List<UpdateFileDownloadRequest> Requests { get; }
            = new List<UpdateFileDownloadRequest>();

        public byte[] Content { get; set; } = new byte[0];

        public Exception? ExceptionToThrow { get; set; }

        public Task<string> DownloadAndVerifyAsync(
            UpdateFileDownloadRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            Requests.Add(new UpdateFileDownloadRequest
            {
                DownloadUrl = request.DownloadUrl,
                DestinationPath = request.DestinationPath,
                ExpectedSize = request.ExpectedSize,
                ExpectedSha256 = request.ExpectedSha256
            });

            var directory = Path.GetDirectoryName(
                request.DestinationPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(request.DestinationPath, Content);
            return Task.FromResult(request.DestinationPath);
        }
    }
}
