using System.Data;
using System.Data.Common;

namespace POSCAM.UpdateServer.Tests.TestDoubles;

internal sealed class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public FakeDbTransaction? LastTransaction { get; private set; }

    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "poscam_update_test";
    public override string DataSource => "fake";
    public override string ServerVersion => "test";
    public override ConnectionState State => _state;

    public void MarkOpen()
    {
        _state = ConnectionState.Open;
    }

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Close()
    {
        _state = ConnectionState.Closed;
    }

    public override void Open()
    {
        _state = ConnectionState.Open;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        LastTransaction = new FakeDbTransaction(this, isolationLevel);
        return LastTransaction;
    }

    protected override DbCommand CreateDbCommand()
    {
        throw new NotSupportedException();
    }
}

internal sealed class FakeDbTransaction : DbTransaction
{
    private readonly FakeDbConnection _connection;

    public FakeDbTransaction(
        FakeDbConnection connection,
        IsolationLevel isolationLevel)
    {
        _connection = connection;
        IsolationLevel = isolationLevel;
    }

    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }

    public override IsolationLevel IsolationLevel { get; }
    protected override DbConnection DbConnection => _connection;

    public override void Commit()
    {
        Committed = true;
    }

    public override void Rollback()
    {
        RolledBack = true;
    }

    public override Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public override Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RolledBack = true;
        return Task.CompletedTask;
    }
}
