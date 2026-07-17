using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;

namespace POSCAM.UpdateServer.Api.Repositories;

public interface IUpdateProductRepository
{
    Task<UpdateProduct?> GetByCodeAsync(
        string productCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UpdateProduct>> GetActiveAsync(
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
