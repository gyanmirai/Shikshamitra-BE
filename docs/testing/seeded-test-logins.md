# Seeded Test Logins (DDVS)

This file lists deterministic test credentials seeded by `TestLoginSeederContributor` when:

- `SeedTestLogins=true`
- tenant is `DDVS` (`11111111-1111-1111-1111-111111111111`)

Common password for all accounts in this list:

- `Temp@123`

## Credentials

| Tenant | Role | Email / Login | Password | Notes |
|---|---|---|---|---|
| DDVS | Admin | `admin-ddvs@ddvs.com` | `Temp@123` | Primary DDVS admin account. |
| DDVS | Admin | `admin-ops@ddvs.com` | `Temp@123` | Additional admin/operations account. |
| DDVS | Principal | `principal-gseb@ddvs.com` | `Temp@123` | Principal persona scoped to GSEB board data. |
| DDVS | Principal | `principal-cbse@ddvs.com` | `Temp@123` | Principal persona scoped to CBSE board data. |
| DDVS | Teacher | `teacher-gseb@ddvs.com` | `Temp@123` | Teacher persona for GSEB testing. |
| DDVS | Clerk | `clerk-office@ddvs.com` | `Temp@123` | Office clerk persona. |
| DDVS | Finance Manager | `finance-officer@ddvs.com` | `Temp@123` | Uses existing role `Finance Officer`. |
| DDVS | Student | `student-demo@ddvs.com` | `Temp@123` | Linked to an actual seeded student profile by email and assigned role `Student`. |

These baseline staff logins are seeded for every tenant, including DDVS, SSVM, and Forgot:

| Role | Email / Login | Password |
|---|---|---|
| Teacher | `teacher-demo-1@ddvs.com` | `Temp@123` |
| Teacher | `teacher-demo-2@ddvs.com` | `Temp@123` |
| Principal | `principal-demo-gseb@ddvs.com` | `Temp@123` |
| HR Manager | `hr-manager@ddvs.com` | `Temp@123` |

## Expected access

The seeders grant two separate kinds of access:

- Role permissions decide which modules/actions a user can call.
- User scope decides which tenant data rows a staff user can see. Seeded Principal users are board-scoped by persona; other seeded staff users receive full organization scope for demo/testing.

| Role | Seeded access |
|---|---|
| Admin | Runtime admin bypass; all permissions and all scope. |
| Principal | All seeded application permissions; `principal-gseb@ddvs.com` and `principal-demo-gseb@ddvs.com` are GSEB scoped, `principal-cbse@ddvs.com` is CBSE scoped. |
| Teacher | Dashboard, Students, Academics, Class Management, and Calendar permissions; full organization scope for seeded demo data. |
| Clerk | Dashboard, Students, HR, Communication, Calendar, and Institute Setup permissions; full organization scope for seeded demo data. |
| Finance Manager | Dashboard, Students, and Finance permissions; full organization scope for seeded demo data. |
| Student | Dashboard, Students, and Calendar permissions; no staff-wide organization scope. |

## Usage

Set the flag before running DB migrator:

```powershell
$env:SeedTestLogins='true'
cd src/ShikshaMitra.DbMigrator
dotnet run
```

Or set `SeedTestLogins: true` in the effective configuration for your seed run.
