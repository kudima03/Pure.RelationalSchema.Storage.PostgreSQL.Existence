using System.Data;
using Npgsql;
using Pure.Primitives.Abstractions.Bool;
using Pure.Primitives.Bool;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.PostgreSQL.Creation;
using Pure.RelationalSchema.Storage.PostgreSQL.Destruction;
using Pure.RelationalSchema.Storage.PostgreSQL.Existence.Tests.Fakes;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Existence.Tests;

using Column = Column.Column;
using ForeignKey = ForeignKey.ForeignKey;
using Index = Index.Index;
using Schema = Schema.Schema;
using Table = Table.Table;

public sealed record PostgreSqlStoredSchemaExistenceTests
    : IClassFixture<DatabaseFixture>
{
    private const string Injection = "x'; DROP SCHEMA public CASCADE; --";

    private readonly DatabaseFixture _fixture;

    public PostgreSqlStoredSchemaExistenceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void FollowsCreationAndDestructionOfStorage()
    {
        ISchema schema = Model("FollowsCreationAndDestructionOfStorage");
        IBool existence = new PostgreSqlStoredSchemaExistence(
            schema,
            _fixture.Connection
        );
        Assert.False(existence.BoolValue);

        _ = new PostgreSqlCreatedSchema(schema, _fixture.Connection).Name;
        Assert.True(existence.BoolValue);

        _ = new PostgreSqlDroppedSchema(schema, _fixture.Connection).Name;
        Assert.False(existence.BoolValue);
    }

    [Fact]
    public void IsFalseWhenStorageWasNeverCreated()
    {
        ISchema schema = Model("IsFalseWhenStorageWasNeverCreated");

        Assert.False(
            new PostgreSqlStoredSchemaExistence(schema, _fixture.Connection).BoolValue
        );
    }

    [Fact]
    public void IsTrueWhenStorageWasCreated()
    {
        ISchema schema = Created("IsTrueWhenStorageWasCreated");

        Assert.True(
            new PostgreSqlStoredSchemaExistence(schema, _fixture.Connection).BoolValue
        );
    }

    [Fact]
    public void AgreesWithCatalog()
    {
        ISchema created = Created("AgreesWithCatalogCreated");
        ISchema missing = Model("AgreesWithCatalogMissing");

        Assert.Equal(
            [_fixture.SchemaExists(created), _fixture.SchemaExists(missing)],
            [
                new PostgreSqlStoredSchemaExistence(created, _fixture.Connection)
                    .BoolValue,
                new PostgreSqlStoredSchemaExistence(missing, _fixture.Connection)
                    .BoolValue,
            ]
        );
    }

    [Fact]
    public void IsFalseForOtherSchemaWhileItsStorageExists()
    {
        _ = Created("IsFalseForOtherSchemaWhileItsStorageExistsCreated");
        ISchema other = Model("IsFalseForOtherSchemaWhileItsStorageExistsOther");

        Assert.False(
            new PostgreSqlStoredSchemaExistence(other, _fixture.Connection).BoolValue
        );
    }

    [Fact]
    public void IsTrueForCreatedSchemaWithoutTables()
    {
        ISchema schema = new Schema(
            new String("IsTrueForCreatedSchemaWithoutTables"),
            [],
            []
        );
        _ = new PostgreSqlCreatedSchema(schema, _fixture.Connection).Name;

        Assert.True(
            new PostgreSqlStoredSchemaExistence(schema, _fixture.Connection).BoolValue
        );
    }

    [Fact]
    public void SeesStorageCreatedInUncommittedTransaction()
    {
        ISchema schema = Model("SeesStorageCreatedInUncommittedTransaction");
        IBool existence = new PostgreSqlStoredSchemaExistence(
            schema,
            _fixture.Connection
        );
        using IDbTransaction transaction = _fixture.Connection.BeginTransaction();
        _ = new PostgreSqlCreatedSchema(schema, _fixture.Connection).Name;

        Assert.True(existence.BoolValue);

        transaction.Rollback();
        Assert.False(existence.BoolValue);
    }

    [Fact]
    public void RunsOnGivenConnection()
    {
        ISchema schema = Model("RunsOnGivenConnection");
        using IDbConnection other = new NpgsqlConnection(_fixture.ConnectionString);
        other.Open();
        using IDbTransaction transaction = _fixture.Connection.BeginTransaction();
        _ = new PostgreSqlCreatedSchema(schema, _fixture.Connection).Name;

        Assert.False(new PostgreSqlStoredSchemaExistence(schema, other).BoolValue);

        transaction.Rollback();
    }

    [Fact]
    public void LeavesOpenConnectionOpen()
    {
        ISchema schema = Created("LeavesOpenConnectionOpen");

        _ = new PostgreSqlStoredSchemaExistence(schema, _fixture.Connection).BoolValue;

        Assert.Equal(ConnectionState.Open, _fixture.Connection.State);
    }

    [Fact]
    public void DoesNotOpenClosedConnection()
    {
        ISchema schema = Model("DoesNotOpenClosedConnection");
        using IDbConnection closed = new NpgsqlConnection(_fixture.ConnectionString);

        _ = Assert.Throws<InvalidOperationException>(() =>
            new PostgreSqlStoredSchemaExistence(schema, closed).BoolValue
        );
        Assert.Equal(ConnectionState.Closed, closed.State);
    }

    [Fact]
    public void QueriesOnEveryRead()
    {
        RecordingConnection connection = new RecordingConnection(_fixture.Connection);
        IBool existence = new PostgreSqlStoredSchemaExistence(
            Model("QueriesOnEveryRead"),
            connection
        );

        _ = existence.BoolValue;
        _ = existence.BoolValue;
        _ = existence.BoolValue;

        Assert.Equal(3, connection.Commands.Count);
    }

    [Fact]
    public void DoesNotQueryBeforeRead()
    {
        RecordingConnection connection = new RecordingConnection(_fixture.Connection);

        _ = new PostgreSqlStoredSchemaExistence(
            Model("DoesNotQueryBeforeRead"),
            connection
        );

        Assert.Empty(connection.Commands);
    }

    [Fact]
    public void DisposesCommand()
    {
        RecordingConnection connection = new RecordingConnection(_fixture.Connection);

        _ = new PostgreSqlStoredSchemaExistence(
            Model("DisposesCommand"),
            connection
        ).BoolValue;

        Assert.True(connection.Commands.Single().Disposed);
    }

    [Fact]
    public void PassesSchemaNameAsParameter()
    {
        ISchema schema = Model("PassesSchemaNameAsParameter");
        RecordingConnection connection = new RecordingConnection(_fixture.Connection);

        _ = new PostgreSqlStoredSchemaExistence(schema, connection).BoolValue;

        RecordingCommand command = connection.Commands.Single();
        KeyValuePair<string, object> parameter = command.ExecutedParameters.Single();
        Assert.Equal(DatabaseFixture.Name(schema), parameter.Value);
        Assert.Contains($"@{parameter.Key}", command.ExecutedText);
        Assert.DoesNotContain(DatabaseFixture.Name(schema), command.ExecutedText);
    }

    [Fact]
    public void FindsStorageOfSchemaWhoseNameBreaksUnparameterizedSql()
    {
        ISchema schema = Created(Injection);

        Assert.True(
            new PostgreSqlStoredSchemaExistence(schema, _fixture.Connection).BoolValue
        );
        Assert.True(_fixture.NamespaceExists("public"));
    }

    [Fact]
    public void ComparesNameLiterallyInsteadOfExecutingIt()
    {
        string name = $"' OR TRUE OR nspname = '{new string('x', 50)}";

        Assert.False(
            new PostgreSqlStoredSchemaExistence(new String(name), _fixture.Connection)
                .BoolValue
        );
    }

    [Fact]
    public void FindsNamespaceWhoseNameContainsSql()
    {
        string name = Injection + new string('y', 63 - Injection.Length);
        _fixture.CreateNamespace(name);

        Assert.True(
            new PostgreSqlStoredSchemaExistence(new String(name), _fixture.Connection)
                .BoolValue
        );
    }

    [Fact]
    public void LooksUpFirst63CharactersOfName()
    {
        _fixture.CreateNamespace(new string('z', 63));

        Assert.True(
            new PostgreSqlStoredSchemaExistence(
                new String(new string('z', 100)),
                _fixture.Connection
            ).BoolValue
        );
    }

    [Fact]
    public void DoesNotMatchLongerNamespaceByPrefix()
    {
        _fixture.CreateNamespace(new string('w', 63));

        Assert.False(
            new PostgreSqlStoredSchemaExistence(
                new String(new string('w', 62) + "v"),
                _fixture.Connection
            ).BoolValue
        );
    }

    [Fact]
    public void ThrowsExceptionOnGetHashCode()
    {
        _ = Assert.Throws<NotSupportedException>(() =>
            new PostgreSqlStoredSchemaExistence(
                Model("ThrowsExceptionOnGetHashCode"),
                _fixture.Connection
            ).GetHashCode()
        );
    }

    [Fact]
    public void ThrowsExceptionOnToString()
    {
        _ = Assert.Throws<NotSupportedException>(() =>
            new PostgreSqlStoredSchemaExistence(
                Model("ThrowsExceptionOnToString"),
                _fixture.Connection
            ).ToString()
        );
    }

    private ISchema Created(string name)
    {
        ISchema schema = Model(name);
        _ = new PostgreSqlCreatedSchema(schema, _fixture.Connection).Name;
        return schema;
    }

    private static ISchema Model(string name)
    {
        IColumn id = new Column(new String("Id"), new IntColumnType());
        IColumn parentId = new Column(new String("ParentId"), new IntColumnType());
        IColumn title = new Column(new String("Title"), new StringColumnType());

        ITable parents = new Table(
            new String("Parents"),
            [id, title],
            [new Index(new True(), [id])]
        );

        ITable children = new Table(
            new String("Children"),
            [parentId, title],
            [new Index(new False(), [parentId])]
        );

        return new Schema(
            new String(name),
            [parents, children],
            [new ForeignKey(children, [parentId], parents, [id])]
        );
    }
}
