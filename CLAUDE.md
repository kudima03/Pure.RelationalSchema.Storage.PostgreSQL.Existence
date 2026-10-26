# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

All `dotnet` commands must be run from the `./src` directory.

```bash
dotnet restore
dotnet build --no-restore -warnaserror
dotnet format --verify-no-changes             # check code style (CI enforces this)
dotnet format                                 # auto-fix code style
dotnet test --no-build --verbosity normal     # run integration tests (requires Docker for Testcontainers)
dotnet tool restore && dotnet stryker --mutation-level Complete --break-at 100
dotnet pack --configuration Release -p:Version=<version> --output .
```

CI additionally passes `-p:AssemblyVersion` (pinned to the major) and `-p:FileVersion`; see `.github/workflows/publish-nuget.yml`. Stryker leaves a mutated assembly in `bin/`, so rebuild before running `dotnet test --no-build` after it.

## Architecture

This is the **existence check** of the PostgreSQL storage implementation for the Pure relational schema ecosystem. It has no public abstractions of its own: it provides one public record that implements `IBool`. Creating, filling, reading, migrating and dropping the storage live in the sibling packages Creation, Insertion, Querying, Migration and Destruction; none of them can tell whether a schema's storage exists, which is what this package adds.

**Dependencies:** the library references only `Pure.Primitives.String.Operations` (which brings in `Pure.Primitives`) and `Pure.RelationalSchema.HashCodes` (which brings in `Pure.RelationalSchema.Abstractions` and `Pure.Primitives.Abstractions`). Do not add direct references to packages that already come transitively. It does not reference the concrete `Pure.RelationalSchema` model or any storage package. Tests additionally reference the published `Pure.RelationalSchema.Storage.PostgreSQL.Creation` and `Pure.RelationalSchema.Storage.PostgreSQL.Destruction` packages (Creation also brings in the `Pure.RelationalSchema` model types), so they prove the naming contract against real created and dropped storage.

**Key relationships:**

- `PostgreSqlStoredSchemaExistence` (public) wraps an `ISchema` and an `IDbConnection`. Every read of `BoolValue` creates a command, sets the `SchemaExistenceQuery` text, adds the trimmed schema name as the `name` parameter and returns the scalar. There is no caching on purpose: storage can be created or dropped between reads, and callers keep one instance and read it again. The public constructor chains to an internal `(IString schemaName, IDbConnection connection)` constructor that takes the untrimmed name, so tests can pass raw names (SQL fragments, 63-character prefixes). `GetHashCode` and `ToString` throw `NotSupportedException`, like the other public `IBool` implementations (`True`, `EqualCondition`, …).
- `SchemaExistenceQuery` (internal) builds `SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_namespace WHERE nspname = @<parameter>);`. It takes the parameter name, so the placeholder and the parameter cannot drift apart. The schema name is never part of the SQL text; keep it a parameter.
- The schema is the only unit worth checking. Physical names are content hashes of the whole model, so a changed table produces a different schema name; do not add table-level checks.
- All SQL is composed as an `IString` value using `Pure.Primitives.String.Operations` combinators. Do not use string interpolation in library code.
- The caller owns the `IDbConnection`. It is shared with every other decorator using it, so no type here may open, close or dispose it (the `IDbCommand` is disposed). The query runs in whatever transaction the connection has.

**Naming convention:** the schema is named with the first 63 characters (PostgreSQL's identifier limit) of `HexString` over `SchemaHash` from `Pure.RelationalSchema.HashCodes`. This must match Creation exactly, since it is the only contract between the packages. `TrimmedHash` is deliberately duplicated in each repo rather than shared (this copy comes from Destruction); it expects input of at least 63 characters (a 64-character SHA-256 hex string in practice).

**Testing:** Integration tests use `Testcontainers.PostgreSql`. `PostgreSqlStoredSchemaExistenceTests` shares one container per test class through `IClassFixture<DatabaseFixture>`, so **every test uses a distinct schema name** (usually the test's own name) to get its own hash-named schema, and raw-name tests use distinct namespace names. Storage is created with `PostgreSqlCreatedSchema` from a model with two tables (a unique and a non-unique index) and one foreign key, and dropped with `PostgreSqlDroppedSchema`. `DatabaseFixture` computes physical names independently (`hex[..63]`) instead of using the internal `TrimmedHash`, so the tests check the contract rather than the implementation.

- `FollowsCreationAndDestructionOfStorage` reads one instance before creation, after it and after the drop, which proves the value is re-evaluated.
- Tests that open a transaction on the shared connection must roll it back before they end, or later tests in the class run inside it.
- `Fakes/RecordingConnection` wraps the fixture connection and hands out `Fakes/RecordingCommand`s, which record disposal and snapshot the text and parameters at `ExecuteScalar` (Npgsql clears the parameters on dispose). They prove one query per read, no query before the read, the disposed command and the parameterized name. Do not dispose a `RecordingConnection` that wraps the fixture connection.
- `SchemaExistenceQueryTests` asserts the exact SQL text (mutation-testing targets).
- Non-generic enumeration is exercised through `Fakes/NonGenericItems`; `Cast<T>()` short-circuits when the source already implements `IEnumerable<T>` and would not call the non-generic `GetEnumerator`.

CI runs mutation testing via `dotnet-stryker` at `Complete` level with a 100% break threshold (measured: 100%, 11 of 11 tested mutants killed) and requires >=99% line coverage (warning at 99%; measured: 100% line and branch).

**CI style check:** the `Code style check by csharpier` step in `.github/workflows/build-and-test.yml` is commented out on purpose. csharpier's default width of 100 conflicts with the 90-column limit, and it wants `) { }` on one line for chained constructors with an empty body, while `dotnet format` (IDE0055 with `csharp_new_line_before_open_brace = all`) wants `{ }` on its own line. `dotnet format` is the authoritative check.

**Multi-targeting:** net8.0, net9.0, net10.0. `IsAotCompatible` is `true` and the build must stay free of IL2026/IL3050 warnings. Do not use `AsQueryable()` in library code (annotated as trim/AOT-unsafe).

**Documentation:** `GenerateDocumentationFile` is on, so the XML docs ship in the package and a public member without a doc comment fails the build (CS1591 under `-warnaserror`).

**Package validation:** `EnablePackageValidation = true`. There is no `PackageValidationBaselineVersion` until the first release; set it to the last published version after each release so breaking API changes fail the build.

**Publishing:** triggered by pushing a semver tag matching `*.*.*`. The tag name becomes the `PackageVersion`. Packages are published to both GitHub Packages and NuGet.org.

## Code Style

Enforced via `.editorconfig` and `dotnet format --verify-no-changes` in CI:

- No `var` — always use explicit types
- No expression-bodied methods or constructors — properties, indexers, accessors, and lambdas may be expression-bodied
- File-scoped namespace declarations
- Private fields prefixed with `_` (underscore camelCase)
- No non-private instance fields
- `using` directives outside the namespace, System directives sorted first
- Max line length: 90 characters
- Braces on new lines (`csharp_new_line_before_open_brace = all`)
- No implicit object creation (`new()` without type) when the type is apparent

## Commit Messages

Do not mention Claude or AI assistance in commit messages.
