# Changelog

All notable changes to `Pure.RelationalSchema.Storage.PostgreSQL.Existence` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-preview.0.1.0] - 2026-10-26

### Added

- Schema existence check: `PostgreSqlStoredSchemaExistence` is an `IBool` that tells whether the PostgreSQL storage of an `ISchema`, as created by `PostgreSqlCreatedSchema`, exists. Every read of `BoolValue` queries `pg_catalog.pg_namespace` again with the hash-based schema name as a parameter, in whatever transaction the connection has, so storage created or dropped between reads is seen. Other schemas' storage does not count.
