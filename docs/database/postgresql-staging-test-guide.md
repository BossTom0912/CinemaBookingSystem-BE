# PostgreSQL staging test guide

Use this guide before merging `Tom/postgresql-mainlocal` into `main`. The
target is **native PostgreSQL used directly by EF Core**; this project does not
use PostgREST as an application data API.

## Safety boundary

- Never point this branch at the production Render database for the first run.
- Do not run `cinema-booking-schema.sql`: it is a destructive SQL Server
  script and is not compatible with PostgreSQL.
- Create a separate PostgreSQL staging database, then clone production into it.
  Do not copy credentials into Git, `.env.example`, screenshots, or chat.

The existing EF migrations are upgrade migrations for the established Cinema
schema. They are not a clean-room seed for an empty database, so a production
clone is the correct staging baseline.

## 1. Create and restore a staging clone

In Render, create a separate PostgreSQL database for staging. Copy its
**internal** connection string only into your local terminal/session. From a
machine with PostgreSQL client tools installed:

```powershell
$env:CINEMA_SOURCE_POSTGRES = '<production PostgreSQL connection string>'
$env:CINEMA_STAGING_POSTGRES = '<staging PostgreSQL connection string>'

pg_dump --format=custom --no-owner --no-acl --file .\cinema-production.backup $env:CINEMA_SOURCE_POSTGRES
pg_restore --clean --if-exists --no-owner --no-acl --dbname $env:CINEMA_STAGING_POSTGRES .\cinema-production.backup
```

`pg_restore --clean` is destructive **only for the database supplied in
`CINEMA_STAGING_POSTGRES`**. Confirm its Render database name before executing
it. Delete the local backup after the test if it contains production data.

## 2. Run migrations against staging

Keep the staging secret out of source control. In PowerShell at the repository
root:

```powershell
$env:ConnectionStrings__DefaultConnection = $env:CINEMA_STAGING_POSTGRES
$env:ASPNETCORE_ENVIRONMENT = 'Staging'
$env:EmailSettings__UseMock = 'true'
$env:VnPaySettings__Enabled = 'false'

dotnet restore CinemaSystem.sln
dotnet build CinemaSystem.sln --configuration Release -m:1
dotnet test CinemaSystem.sln --no-build -m:1
dotnet run --project CinemaSystem
```

The API now fails startup when a migration fails; this is intentional. Do not
continue to API smoke tests if startup logs show a migration error.

## 3. Verify the database before API testing

With the staging connection, run these read-only checks:

```powershell
psql $env:CINEMA_STAGING_POSTGRES -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
psql $env:CINEMA_STAGING_POSTGRES -c 'SELECT COUNT(*) AS bookings FROM "BOOKING";'
psql $env:CINEMA_STAGING_POSTGRES -c 'SELECT table_name, column_name, data_type FROM information_schema.columns WHERE table_name IN (''SHOWTIME_SEAT'', ''REFUND_CLAIM'', ''MANUAL_REFUND_PROCESS'') AND column_name = ''rowVersion'' ORDER BY table_name;'
```

The migration history must include `20260726000000_ConfigurePostgresRowVersionConcurrency`.
The final query must show `bytea` for each existing `rowVersion` column. If it
does not, stop and record the schema output; do not deploy.

## 4. API smoke test on the clone

Use a test account in the cloned data and test only non-financial flows first:

1. `GET /api/movies` and `GET /api/showtimes` return normal data.
2. Login and load a seat map; lock one available seat, then release/expire it.
3. Create a pending booking only if it can be cancelled in staging; do not make
   a real SePay or VNPAY payment.
4. Update one safe record twice in parallel and confirm the second stale write
   receives the expected concurrency conflict rather than overwriting data.
5. Confirm app logs contain PostgreSQL generated SQL (quoted identifiers), and
   no `SqlException` or SQL Server provider reference.

## 5. Render staging deployment

Create a **separate Render web service** connected to this branch and the
staging PostgreSQL database. Set at minimum:

```text
ConnectionStrings__DefaultConnection=<staging PostgreSQL internal connection string>
ASPNETCORE_ENVIRONMENT=Staging
EmailSettings__UseMock=true
VnPaySettings__Enabled=false
```

Use the connection format supplied by Render. Keep pooling enabled and cap the
application pool (for example `Maximum Pool Size=20`) to stay within the
database connection limit; require SSL when the Render connection requires it.

Deploy only after the local migration and smoke tests pass. Check the service
logs for successful migration/startup and repeat the public read-only API
smoke tests. Merge to `main` only after that staging service is healthy.
