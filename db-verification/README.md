# Database verification (SQL Server → PostgreSQL)

A small console harness that proves the converted data-access layer works against a real
PostgreSQL database. It is intentionally **not** part of `BobsBookstore.sln`; it is a
standalone verification utility.

## What it does

Using the converted `ApplicationDbContext` (now backed by
`Npgsql.EntityFrameworkCore.PostgreSQL`), the harness:

1. Opens an Npgsql-backed context against PostgreSQL.
2. Calls `EnsureCreated()` — EF generates the schema from the model and inserts the
   `HasData` seed rows.
3. Reads seeded data back and checks the counts (`ReferenceData` = 24, `Book` = 8).
4. Performs a runtime INSERT → SELECT → DELETE on `Customer` (CRUD).

It prints `RESULT: VERIFICATION_OK` and exits 0 on success.

## Run it (Docker only — no local .NET or psql required)

```bash
./run-verify.sh
```

The script starts PostgreSQL 16, builds `Bookstore.Web` (Release) and this harness with
the .NET 8 SDK, runs the harness against PostgreSQL, and tears everything down.
Set `KEEP=1 ./run-verify.sh` to leave the database container running.

## Run it against an existing database

Set `PGCONN` to any PostgreSQL connection string and run the harness directly:

```bash
PGCONN="Host=localhost;Port=5432;Database=BobsUsedBookStore;Username=postgres;Password=postgres" \
  dotnet run --project ./bobs-verify.csproj -c Release
```

## Note on schema ownership

This harness uses EF Core `EnsureCreated()`, so tables are created by EF in the `public`
schema with EF naming. The separately-produced DMS schema-conversion DDL creates a
`bobsusedbookstore_dbo` schema with lowercase names. For a real cutover reusing the DMS
schema, either let EF own the schema (EnsureCreated/migrations) or add
`HasDefaultSchema("bobsusedbookstore_dbo")` plus explicit lowercase mappings (PostgreSQL is
case-sensitive on quoted identifiers, unlike SQL Server).
