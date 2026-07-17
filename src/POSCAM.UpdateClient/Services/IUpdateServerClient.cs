using System.Threading;
using System.Threading.Tasks;
using POSCAM.UpdateClient.Models;

namespace POSCAM.UpdateClient.Services
{
    internal interface IUpdateServerClient
    {
        Task<UpdateCheckResponse> CheckAsync(
            UpdateCheckRequest request,
            CancellationToken cancellationToken);
    }
}
