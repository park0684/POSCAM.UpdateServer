using System.Data.Common;
using Dapper;
using MySqlConnector;

namespace POSCAM.UpdateServer.Api.Infrastructure.Database;

/// <summary>
/// Dapper와 MySqlConnector를 사용하는 UpdateServer 전용 DB Context.
/// 실제 연결 문자열은 ConnectionStrings__DefaultConnection 환경변수로 주입한다.
/// </summary>
public sealed class DapperContext : IDbContext
{
    private readonly string _connectionString;

    static DapperContext()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public DapperContext(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection 설정이 필요합니다.");
        }

        var connectionStringBuilder = new MySqlConnectionStringBuilder(_connectionString);

        if (string.Equals(
                connectionStringBuilder.Database,
                "poscam_auth",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "UpdateServer는 poscam_auth 데이터베이스에 연결할 수 없습니다.");
        }
    }

    public async Task<DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (MySqlException exception)
        {
            await connection.DisposeAsync();
            throw DatabaseExceptionTranslator.Translate(exception);
        }
    }
}
