using System.Data;
using Dapper;
using POSCAM.UpdateServer.Api.Infrastructure.Database;
using POSCAM.UpdateServer.Api.Models.Entities;
using POSCAM.UpdateServer.Api.Models.Enums;

namespace POSCAM.UpdateServer.Api.Repositories;

public sealed class UpdateProductRepository : DapperRepositoryBase, IUpdateProductRepository
{
    internal const string GetByCodeSql = @"
SELECT
    prd_code AS ProductCode,
    prd_name AS ProductName,
    prd_description AS ProductDescription,
    prd_status AS ProductStatus,
    prd_idate AS CreatedAt,
    prd_udate AS UpdatedAt
FROM update_products
WHERE prd_code = @ProductCode
LIMIT 1;";

    internal const string GetActiveSql = @"
SELECT
    prd_code AS ProductCode,
    prd_name AS ProductName,
    prd_description AS ProductDescription,
    prd_status AS ProductStatus,
    prd_idate AS CreatedAt,
    prd_udate AS UpdatedAt
FROM update_products
WHERE prd_status = @ActiveStatus
ORDER BY prd_code ASC;";

    public UpdateProductRepository(IDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<UpdateProduct?> GetByCodeAsync(
        string productCode,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetByCodeSql,
                    new { ProductCode = productCode },
                    activeTransaction,
                    cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<UpdateProduct>(command);
            });
    }

    public Task<IReadOnlyList<UpdateProduct>> GetActiveAsync(
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<IReadOnlyList<UpdateProduct>>(
            transaction,
            cancellationToken,
            async (connection, activeTransaction) =>
            {
                var command = CreateCommand(
                    GetActiveSql,
                    new { ActiveStatus = (int)ProductStatus.Active },
                    activeTransaction,
                    cancellationToken);

                var products = await connection.QueryAsync<UpdateProduct>(command);
                return products.AsList();
            });
    }
}
