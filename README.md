# Pure.RelationalSchema.Storage.PostgreSQL.Existence

PostgreSQL schema existence check for the **Pure** relational schema ecosystem — tells whether the physical storage created by `Pure.RelationalSchema.Storage.PostgreSQL.Creation` exists.

[![.NET build & test](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Existence/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Existence/actions/workflows/build-and-test.yml)
[![publish NuGet](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Existence/actions/workflows/publish-nuget.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Existence/actions/workflows/publish-nuget.yml)
[![NuGet](https://img.shields.io/nuget/v/Pure.RelationalSchema.Storage.PostgreSQL.Existence)](https://www.nuget.org/packages/Pure.RelationalSchema.Storage.PostgreSQL.Existence)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Overview

`Pure.RelationalSchema.Storage.PostgreSQL.Existence` tells whether the physical PostgreSQL storage of an `ISchema` exists in a live database. `PostgreSqlStoredSchemaExistence` is an `IBool`: every read of `BoolValue` runs one parameterized query against the PostgreSQL catalog:

```sql
SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_namespace WHERE nspname = @name);
```

The sibling storage packages create, fill, read, migrate and drop that storage, but none of them can tell whether it exists. Callers need the answer before acting on it: a migration should run only when the stored version is the migration's source, and an application may migrate only the schemas whose storage already exists. The check works as follows:

- Nothing is cached. Each read queries the catalog again, because storage can be created or dropped between reads, so one instance can be kept and read repeatedly.
- Nothing is executed until `BoolValue` is read.
- The query runs in whatever transaction the connection has. Storage created in an uncommitted transaction on the same connection is seen; on other connections it is not.
- The schema name is passed as a query parameter and never interpolated into the SQL.
- Only the storage of the given schema counts. Storage of other schemas, including schemas with the same tables but a different name, does not.

The schema is the only unit this package checks. Physical names are content hashes of the whole model (see below), so any change to a table or column produces a different schema name. Checking individual tables of an existing storage is therefore out of scope.

The caller owns the `IDbConnection`: it must be opened before use, and this package never opens, closes or disposes it. The command created for the query is disposed.

## Public API

| Type | Kind | Description |
|------|------|-------------|
| `PostgreSqlStoredSchemaExistence` | record | `IBool` that is `true` while the schema's PostgreSQL storage exists, checked on every read |

`GetHashCode` and `ToString` throw `NotSupportedException`, as in the other `IBool` implementations of the Pure ecosystem. The query builder and the hashing helper are internal.

## Naming convention

Schema identifiers in PostgreSQL are not the model names. The looked-up schema is named with the first 63 hexadecimal characters (PostgreSQL's identifier limit) of `SchemaHash` from `Pure.RelationalSchema.HashCodes`, exactly as `Pure.RelationalSchema.Storage.PostgreSQL.Creation` names it. This convention is the only contract between the packages, and the tests prove it against storage created by the published Creation package and dropped by the published Destruction package.

The hash covers the schema name, its tables, their columns and indexes, and its foreign keys. To check a storage, pass the same model that was used to create it. A model that differs in any detail resolves to another schema name, and its storage does not exist.

## Dependencies

- [`Pure.RelationalSchema.HashCodes`](https://github.com/kudima03/Pure.RelationalSchema.HashCodes/tree/3.3.0) — `SchemaHash`, used to derive the PostgreSQL schema name; brings in the `ISchema` and primitive abstractions
- [`Pure.Primitives.String.Operations`](https://github.com/kudima03/Pure.Primitives.String.Operations/tree/1.7.0) — string composition utilities used to build the SQL query; brings in `Pure.Primitives`

## Target Frameworks

- .NET 8
- .NET 9
- .NET 10

## Installation

```
dotnet add package Pure.RelationalSchema.Storage.PostgreSQL.Existence
```

The example below also uses `Pure.RelationalSchema` for the concrete model types, `Pure.RelationalSchema.Storage.PostgreSQL.Creation` to create the storage and `Pure.RelationalSchema.Storage.PostgreSQL.Destruction` to drop it; none of them is a dependency of this package.

## Usage

```csharp
using System.Data;
using Npgsql;
using Pure.Primitives.Abstractions.Bool;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.PostgreSQL.Creation;
using Pure.RelationalSchema.Storage.PostgreSQL.Destruction;
using Pure.RelationalSchema.Storage.PostgreSQL.Existence;
using Column = Pure.RelationalSchema.Column.Column;
using Schema = Pure.RelationalSchema.Schema.Schema;
using String = Pure.Primitives.String.String;
using Table = Pure.RelationalSchema.Table.Table;

IDbConnection connection = new NpgsqlConnection(connectionString);
connection.Open();

IColumn idColumn = new Column(new String("Id"), new IntColumnType());
IColumn nameColumn = new Column(new String("Name"), new StringColumnType());
ITable usersTable = new Table(new String("Users"), [idColumn, nameColumn], []);
ISchema schema = new Schema(new String("Shop"), [usersTable], []);

IBool stored = new PostgreSqlStoredSchemaExistence(schema, connection);

Console.WriteLine(stored.BoolValue); // False: nothing was created yet

_ = new PostgreSqlCreatedSchema(schema, connection).Name;
Console.WriteLine(stored.BoolValue); // True: every read queries the catalog again

_ = new PostgreSqlDroppedSchema(schema, connection).Name;
Console.WriteLine(stored.BoolValue); // False
```

## Pairing with the other storage packages

- **Creation.** `PostgreSqlCreatedSchema` uses `IF NOT EXISTS` throughout, so creating existing storage is safe. The check tells beforehand whether storage, and possibly data, is already there.
- **Migration.** `PostgreSqlMigratedSchema` carries data over only when the source's storage exists, creates the target empty otherwise, and fails when the target's storage already exists next to the source's. Check that the source's storage exists and the target's does not before running a migration, as the Migration README advises.
- **Destruction.** `PostgreSqlDroppedSchema` is already a no-op for missing storage, so no check is needed before dropping. Reading the check afterwards confirms the drop.
- **Insertion, Querying.** Both expect the tables to exist. Check first to create the storage on the first insert, or to treat a missing storage as empty instead of failing.

The check and the action are separate statements. When other sessions can create or drop the same storage, run both in one transaction and serialize them, for example with an advisory lock.

## Known limitations

- **Only the PostgreSQL schema is checked.** A schema whose tables were dropped or altered by hand still reads `true`. The check does not compare the stored tables with the model.
- **The model must match exactly.** Storage is found only through the hash of the model it was created from. A storage whose model has been lost or changed cannot be addressed by this package.
- **The answer can be stale.** It is true at the moment of the read. Another session can create or drop the storage right after it, unless the caller serializes the check and the action.
- **Uncommitted storage of other sessions is not seen.** The catalog shows only what is visible to the connection's transaction.

## Related packages

- `Pure.RelationalSchema.Storage.PostgreSQL.Creation` — creates the storage this package looks up
- `Pure.RelationalSchema.Storage.PostgreSQL.Migration` — migrates that storage from one schema version to another
- `Pure.RelationalSchema.Storage.PostgreSQL.Destruction` — drops that storage
- `Pure.RelationalSchema.Storage.PostgreSQL.Insertion` — inserts rows into that storage
- `Pure.RelationalSchema.Storage.PostgreSQL.Querying` — reads rows from that storage
