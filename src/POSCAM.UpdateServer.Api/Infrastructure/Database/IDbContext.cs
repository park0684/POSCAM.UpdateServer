using System.Data.Common;

namespace POSCAM.UpdateServer.Api.Infrastructure.Database;

/// <summary>
/// UpdateServer 전용 MariaDB 연결을 생성한다.
/// 연결은 호출자가 await using으로 해제해야 한다.
/// </summary>
public interface IDbContext
{
    Task<DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default);
}
