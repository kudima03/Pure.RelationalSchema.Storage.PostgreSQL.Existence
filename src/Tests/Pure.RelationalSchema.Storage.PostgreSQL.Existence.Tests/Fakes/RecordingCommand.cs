using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Existence.Tests.Fakes;

internal sealed record RecordingCommand : IDbCommand
{
    private readonly IDbCommand _origin;

    public RecordingCommand(IDbCommand origin)
    {
        _origin = origin;
    }

    public bool Disposed { get; private set; }

    public string ExecutedText { get; private set; } = "";

    public IReadOnlyDictionary<string, object> ExecutedParameters
    {
        get;
        private set;
    } = new Dictionary<string, object>();

    [AllowNull]
    public string CommandText
    {
        get => _origin.CommandText;
        set => _origin.CommandText = value;
    }

    public int CommandTimeout
    {
        get => _origin.CommandTimeout;
        set => _origin.CommandTimeout = value;
    }

    public CommandType CommandType
    {
        get => _origin.CommandType;
        set => _origin.CommandType = value;
    }

    public IDbConnection? Connection
    {
        get => _origin.Connection;
        set => _origin.Connection = value;
    }

    public IDataParameterCollection Parameters => _origin.Parameters;

    public IDbTransaction? Transaction
    {
        get => _origin.Transaction;
        set => _origin.Transaction = value;
    }

    public UpdateRowSource UpdatedRowSource
    {
        get => _origin.UpdatedRowSource;
        set => _origin.UpdatedRowSource = value;
    }

    public void Cancel()
    {
        _origin.Cancel();
    }

    public IDbDataParameter CreateParameter()
    {
        return _origin.CreateParameter();
    }

    public int ExecuteNonQuery()
    {
        return _origin.ExecuteNonQuery();
    }

    public IDataReader ExecuteReader()
    {
        return _origin.ExecuteReader();
    }

    public IDataReader ExecuteReader(CommandBehavior behavior)
    {
        return _origin.ExecuteReader(behavior);
    }

    public object? ExecuteScalar()
    {
        ExecutedText = _origin.CommandText;
        ExecutedParameters = _origin
            .Parameters.Cast<IDbDataParameter>()
            .ToDictionary(x => x.ParameterName, x => x.Value!);
        return _origin.ExecuteScalar();
    }

    public void Prepare()
    {
        _origin.Prepare();
    }

    public void Dispose()
    {
        Disposed = true;
        _origin.Dispose();
    }
}
