using System.Collections;
using Pure.Primitives.Abstractions.Char;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Char = Pure.Primitives.Char.Char;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Existence;

internal sealed record SchemaExistenceQuery : IString
{
    private readonly IString _parameterName;

    public SchemaExistenceQuery(IString parameterName)
    {
        _parameterName = parameterName;
    }

    public string TextValue =>
        new ConcatenatedString(
            new WhitespaceJoinedString(
                new String("SELECT EXISTS"),
                new WrappedString(
                    new LeftRoundBracketString(),
                    new WhitespaceJoinedString(
                        new String("SELECT 1 FROM pg_catalog.pg_namespace"),
                        new String("WHERE nspname"),
                        new EqualsSignString(),
                        new ConcatenatedString(new AtString(), _parameterName)
                    ),
                    new RightRoundBracketString()
                )
            ),
            new SemicolonString()
        ).TextValue;

    public IEnumerator<IChar> GetEnumerator()
    {
        return TextValue.Select(x => new Char(x)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
