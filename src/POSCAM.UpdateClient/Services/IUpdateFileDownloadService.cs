using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    internal interface IUpdateFileDownloadService
    {
        Task<string> DownloadAndVerifyAsync(
            UpdateFileDownloadRequest request,
            CancellationToken cancellationToken);
    }
}
