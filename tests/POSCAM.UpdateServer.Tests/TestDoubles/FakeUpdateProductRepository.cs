using System.Data;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Repositories;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeUpdateProductRepository : IUpdateProductRepository
{
    public UpdateProduct? Product { get; set; }

    public string? LastProductCode { get; private set; }

    public Task<UpdateProduct?> GetByCodeAsync(
        string productCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        LastProductCode = productCode;
        return Task.FromResult(Product);
    }

    public Task<IReadOnlyList<UpdateProduct>> GetActiveAsync(
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UpdateProduct> result = Product is null
            ? Array.Empty<UpdateProduct>()
            : new[] { Product };

        return Task.FromResult(result);
    }
}
