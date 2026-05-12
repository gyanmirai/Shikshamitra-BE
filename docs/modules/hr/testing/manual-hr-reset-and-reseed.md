# HR Manual Test Reset And Reseed

Use this workflow when you want to wipe development data and repeat HR manual testing with the same seeded records.

## Recommended Local Database

Use a dedicated local database for HR manual testing:

```powershell
$env:ConnectionStrings__Default = 'Host=localhost;Port=5432;Database=shikshamitra_hr_test;User ID=postgres;Password=postgres;Search Path=public'
```

Avoid using a shared database for this workflow. The reset step is destructive.

## Reset Database

From PostgreSQL tools or pgAdmin, drop and recreate the database:

```sql
DROP DATABASE IF EXISTS shikshamitra_hr_test;
CREATE DATABASE shikshamitra_hr_test;
```

If PostgreSQL blocks the drop because connections are active, stop the backend/frontend first and disconnect open DB sessions.

## Run Migrator With HR Manual Data

From `Shikshamitra-BE`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:SeedTestLogins = 'true'
$env:SeedHrManualTestData = 'true'
$env:ConnectionStrings__Default = 'Host=localhost;Port=5432;Database=shikshamitra_hr_test;User ID=postgres;Password=postgres;Search Path=public'
dotnet run --project src/ShikshaMitra.DbMigrator --no-launch-profile
```

This applies migrations and seeds:

- DDVS tenant
- roles and base users
- academic setup required for HR scope
- base HR settings
- deterministic test logins
- tester-owned HR manual test employees

## Start Backend

Use the same connection string:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:SeedTestLogins = 'true'
$env:SeedHrManualTestData = 'false'
$env:ConnectionStrings__Default = 'Host=localhost;Port=5432;Database=shikshamitra_hr_test;User ID=postgres;Password=postgres;Search Path=public'
dotnet run --project src/ShikshaMitra.HttpApi.Host --no-launch-profile
```

Keep `SeedHrManualTestData=false` for normal host runs unless you intentionally want the host to enforce the seed data again.

## Start Frontend

From `Shikshamitra-FE`:

```powershell
npm run dev
```

Log in to DDVS with any seeded HR tester account. For full-access validation, start with:

- `admin1@gmail.com` / `Temp@123`
- `yagnik.vadher1@ddvs.com` / `Temp@123`
- `parth.ghusar@ddvs.com` / `Temp@123`

## Repeat Testing

When testing is complete and you need a clean run:

1. Stop frontend and backend.
2. Drop/recreate `shikshamitra_hr_test`.
3. Run DbMigrator again with `SeedTestLogins=true` and `SeedHrManualTestData=true`.
4. Restart backend and frontend.

