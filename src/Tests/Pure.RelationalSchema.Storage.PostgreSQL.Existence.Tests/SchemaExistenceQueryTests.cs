using System.Collections;
using Pure.Primitives.Abstractions.Char;
using Pure.RelationalSchema.Storage.PostgreSQL.Existence.Tests.Fakes;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Existence.Tests;

public sealed record SchemaExistenceQueryTests
{
    private const string Query =
        "SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_namespace "
        + "WHERE nspname = @name);";

    [Fact]
    public void SelectsExistenceOfNamespaceByParameter()
    {
        Assert.Equal(
            Query,
            new SchemaExistenceQuery(new String("name")).TextValue
        );
    }

    [Fact]
    public void UsesGivenParameterName()
    {
        Assert.Equal(
            "SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_namespace "
                + "WHERE nspname = @schema);",
            new SchemaExistenceQuery(new String("schema")).TextValue
        );
    }

    [Fact]
    public void EnumeratesCharactersOfText()
    {
        Assert.Equal(Query, Text(new SchemaExistenceQuery(new String("name"))));
    }

    [Fact]
    public void EnumeratesCharactersOfTextNonGenerically()
    {
        IEnumerable query = new SchemaExistenceQuery(new String("name"));

        Assert.Equal(Query, Text(new NonGenericItems(query).Cast<IChar>()));
    }

    [Fact]
    public void TrimmedHashEnumeratesCharactersOfText()
    {
        IEnumerable trimmed = new TrimmedHash(new String(new string('x', 70)));

        Assert.Equal(
            new string('x', 63),
            Text(new NonGenericItems(trimmed).Cast<IChar>())
        );
    }

    [Fact]
    public void TrimmedHashEnumeratesCharactersOfTextGenerically()
    {
        TrimmedHash trimmed = new TrimmedHash(new String(new string('y', 63)));

        Assert.Equal(new string('y', 63), Text(trimmed));
    }

    private static string Text(IEnumerable<IChar> chars)
    {
        return new string([.. chars.Select(x => x.CharValue)]);
    }
}
