using System.Data;
using Npgsql;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.HashCodes;
using Testcontainers.PostgreSql;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Existence.Tests;

public sealed record DatabaseFixture : IDisposable
{
    private const int MaxIdentifierLength = 63;

    private readonly PostgreSqlContainer _postgres;

    public IDbConnection Connection { get; }

    public string ConnectionString => _postgres.GetConnectionString();

    public DatabaseFixture()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _postgres.StartAsync().GetAwaiter().GetResult();

        Connection = new NpgsqlConnection(_postgres.GetConnectionString());

        Connection.Open();
    }

    public static string Name(ISchema schema)
    {
        string name = new HexString(new SchemaHash(schema)).TextValue;
        return name[..Math.Min(MaxIdentifierLength, name.Length)];
    }

    public bool SchemaExists(ISchema schema)
    {
        return NamespaceExists(Name(schema));
    }

    public bool NamespaceExists(string name)
    {
        using IDbCommand command = Connection.CreateCommand();
        command.CommandText =
            "SELECT count(*) FROM pg_catalog.pg_namespace "
            + $"WHERE nspname = '{name}';";
        return Convert.ToInt64(command.ExecuteScalar()) == 1;
    }

    public void CreateNamespace(string name)
    {
        using IDbCommand command = Connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA \"{name}\";";
        _ = command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        Connection.Dispose();
        _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
