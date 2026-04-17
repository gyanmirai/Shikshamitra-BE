# Seeded Test Logins (DDVS)

This file lists deterministic test credentials seeded by `TestLoginSeederContributor` when:

- `SeedTestLogins=true`
- tenant is `DDVS` (`11111111-1111-1111-1111-111111111111`)

Common password for all accounts in this list:

- `Temp@123`

## Credentials

| Tenant | Role | Email / Login | Password | Notes |
|---|---|---|---|---|
| DDVS | Admin | `admin1@gmail.com` | `Temp@123` | Existing DDVS admin account is normalized/mapped to this login. |
| DDVS | Admin | `admin2@ddvs.com` | `Temp@123` | Additional deterministic admin test account. |
| DDVS | Principal | `principle1@ddvs.com` | `Temp@123` | Mapped to Identity role `Principal` (email alias over existing principal user). |
| DDVS | Teacher | `teacher1@ddvs.com` | `Temp@123` | Mapped to Identity role `Teacher` (email alias over existing teacher user). |
| DDVS | Clerk | `clerk1@ddvs.com` | `Temp@123` | Mapped to Identity role `Clerk` (email alias over existing clerk user). |
| DDVS | Finance Manager | `finance1@ddvs.com` | `Temp@123` | Uses existing role `Finance Officer`. |
| DDVS | Student | `student1@ddvs.com` | `Temp@123` | Linked to an actual seeded student profile by email and assigned role `Student`. |

## Usage

Set the flag before running DB migrator:

```powershell
$env:SeedTestLogins='true'
cd src/ShikshaMitra.DbMigrator
dotnet run
```

Or set `SeedTestLogins: true` in the effective configuration for your seed run.
