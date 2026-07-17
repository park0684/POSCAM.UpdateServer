using System.Data;
using Dapper;
using MySqlConnector;
using POSCAM.UpdateServer.Api.Infrastructure.Database;

namespace POSCAM.UpdateServer.Api.Repositories;

/// <summary>
/// Repository가 동일한 연결 소유권과 MySqlException 변환 규칙을 사용하도록 한다.
/// 외부 트랜잭션이 전달되면 해당 연결을 재사용하고, 없으면 새 연결을 열고 해제한다.
/// </summary>
public abstract class DapperRepositoryBase
{
    private readonly IDbContext _dbContext;

    protected DapperRepositoryBase(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected async Task<T> ExecuteAsync<T>(
        IDbTransaction? transaction,
        CancellationToken cancellationToken,
        Func<IDbConnection, IDbTransaction?, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (transaction is not null)
        {
            var transactionConnection = transaction.Connection
                ?? throw new InvalidOperationException("활성 트랜잭션에 연결이 없습니다.");

            return await ExecuteCoreAsync(
                transactionConnection,
                transaction,
                action);
        }

        await using var connection = await _dbContext.OpenConnectionAsync(cancellationToken);

        return await ExecuteCoreAsync(
            connection,
            transaction: null,
            action);
    }

    protected static CommandDefinition CreateCommand(
        string sql,
        object? parameters,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return new CommandDefinition(
            sql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
    }

    private static async Task<T> ExecuteCoreAsync<T>(
        IDbConnection connection,
        IDbTransaction? transaction,
        Func<IDbConnection, IDbTransaction?, Task<T>> action)
    {
        try
        {
            return await action(connection, transaction);
        }
        catch (MySqlException exception)
        {
            throw DatabaseExceptionTranslator.Translate(exception);
        }
    }
}
