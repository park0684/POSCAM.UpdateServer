using System.Data.Common;
using POSCAM.UpdateServer.Api.Infrastructure.Database;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeDbContext : IDbContext
{
    public FakeDbConnection Connection { get; } = new();

    public Task<DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        Connection.MarkOpen();
        return Task.FromResult<DbConnection>(Connection);
    }
}
