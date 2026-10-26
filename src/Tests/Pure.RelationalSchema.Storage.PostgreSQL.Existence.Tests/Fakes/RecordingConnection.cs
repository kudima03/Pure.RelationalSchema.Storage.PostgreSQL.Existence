using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Existence.Tests.Fakes;

internal sealed record RecordingConnection : IDbConnection
{
    private readonly IDbConnection _origin;

    private readonly List<RecordingCommand> _commands;

    public RecordingConnection(IDbConnection origin)
    {
        _origin = origin;
        _commands = [];
    }

    public IReadOnlyList<RecordingCommand> Commands => _commands;

    [AllowNull]
    public string ConnectionString
    {
        get => _origin.ConnectionString;
        set => _origin.ConnectionString = value;
    }

    public int ConnectionTimeout => _origin.ConnectionTimeout;

    public string Database => _origin.Database;

    public ConnectionState State => _origin.State;

    public IDbTransaction BeginTransaction()
    {
        return _origin.BeginTransaction();
    }

    public IDbTransaction BeginTransaction(IsolationLevel il)
    {
        return _origin.BeginTransaction(il);
    }

    public void ChangeDatabase(string databaseName)
    {
        _origin.ChangeDatabase(databaseName);
    }

    public void Close()
    {
        _origin.Close();
    }

    public IDbCommand CreateCommand()
    {
        RecordingCommand command = new RecordingCommand(_origin.CreateCommand());
        _commands.Add(command);
        return command;
    }

    public void Open()
    {
        _origin.Open();
    }

    public void Dispose()
    {
        _origin.Dispose();
    }
}
