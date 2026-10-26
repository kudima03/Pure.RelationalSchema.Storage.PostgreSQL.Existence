using System.Data;
using Pure.Primitives.Abstractions.Bool;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.HashCodes;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Existence;

/// <summary>
/// Tells whether the PostgreSQL storage of a relational schema exists.
/// </summary>
/// <remarks>
/// The storage is the PostgreSQL schema named with the first 63 hexadecimal characters
/// of the schema's <see cref="SchemaHash"/>, as
/// <c>Pure.RelationalSchema.Storage.PostgreSQL.Creation</c> names it. Every read of
/// <see cref="BoolValue"/> queries <c>pg_catalog.pg_namespace</c> again, in whatever
/// transaction the connection has. The connection is neither opened, closed nor
/// disposed.
/// </remarks>
public sealed record PostgreSqlStoredSchemaExistence : IBool
{
    private const string ParameterName = "name";

    private readonly IString _schemaName;

    private readonly IDbConnection _connection;

    /// <summary>
    /// Initializes a check of the PostgreSQL storage of <paramref name="schema"/>.
    /// </summary>
    /// <param name="schema">The schema whose storage is looked up.</param>
    /// <param name="connection">An open connection, owned by the caller.</param>
    public PostgreSqlStoredSchemaExistence(ISchema schema, IDbConnection connection)
        : this(new HexString(new SchemaHash(schema)), connection)
    { }

    internal PostgreSqlStoredSchemaExistence(IString schemaName, IDbConnection connection)
    {
        _schemaName = schemaName;
        _connection = connection;
    }

    /// <summary>
    /// Gets whether the schema's storage exists at the moment of the read.
    /// </summary>
    public bool BoolValue
    {
        get
        {
            using IDbCommand command = _connection.CreateCommand();
            command.CommandText = new SchemaExistenceQuery(
                new String(ParameterName)
            ).TextValue;
            IDbDataParameter name = command.CreateParameter();
            name.ParameterName = ParameterName;
            name.Value = new TrimmedHash(_schemaName).TextValue;
            _ = command.Parameters.Add(name);
            return (bool)command.ExecuteScalar()!;
        }
    }

    /// <summary>
    /// Not supported: the value is read from the database.
    /// </summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int GetHashCode()
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Not supported: the value is read from the database.
    /// </summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override string ToString()
    {
        throw new NotSupportedException();
    }
}
