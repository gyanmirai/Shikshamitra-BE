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
| DDVS | Principal | `principal-gseb@ddvs.com` | `Temp@123` | Enrollment test persona with GSEB academic permissions plus demo-only academic-year-wide scope. |
| DDVS | Principal | `principal-cbse@ddvs.com` | `Temp@123` | Principal persona scoped to CBSE board data. |
| DDVS | Teacher | `teacher-gseb@ddvs.com` | `Temp@123` | Teacher persona for GSEB testing. |
| DDVS | Clerk | `clerk-office@ddvs.com` | `Temp@123` | Office clerk persona. |
| DDVS | Finance Manager | `finance-officer@ddvs.com` | `Temp@123` | Uses existing role `Finance Officer`. |

These baseline logins are seeded for every tenant, including DDVS, SSVM, and Forgot:

| Role | Email / Login | Password |
|---|---|---|
| Teacher | `teacher-demo-1@ddvs.com` | `Temp@123` |
| Teacher | `teacher-demo-2@ddvs.com` | `Temp@123` |
| Principal | `principal-demo-gseb@ddvs.com` | `Temp@123` |
| HR Manager | `hr-manager@ddvs.com` | `Temp@123` |
| Student | `student-demo@ddvs.com` | `Temp@123` |
| Parent | `parent-demo@ddvs.com` | `Temp@123` |

## Expected access

The seeders grant two separate kinds of access:

- Role permissions decide which modules/actions a user can call.
- User scope decides which tenant data rows a staff user can see.
- Seeded Principal users retain board-specific academic permissions by persona.
- When `SeedTestLogins=true`, the exact `principal-gseb@ddvs.com` demo persona also receives academic-year-wide scope for Student Enrollment E2E and global configuration.
- Other seeded staff users receive full organization scope for demo/testing.

| Role | Seeded access |
|---|---|
| Admin | Seeded all application/platform permissions; tenant users remain limited to their current tenant. |
| Director | Seeded all application/platform permissions; full organization scope; HR employee accounts include `jayesh.bhagat@ddvs.com`, `dasharthbhai.patel@ddvs.com`, and `tushar.patel@ddvs.com`. |
| Principal | All seeded application permissions; `principal-demo-gseb@ddvs.com` remains GSEB scoped and `principal-cbse@ddvs.com` remains CBSE scoped; `principal-gseb@ddvs.com` retains GSEB academic permissions and receives the demo-only enrollment academic-year-wide exception described above. |
| Teacher | Dashboard, Students, Academics, Class Management, and Calendar permissions; full organization scope for seeded demo data. |
| Clerk | Dashboard, Students, HR, Communication, Calendar, and Institute Setup permissions; full organization scope for seeded demo data. |
| Finance Manager | Dashboard, Students, and Finance permissions; full organization scope for seeded demo data. |
| Student | Dashboard, Students, and Calendar permissions; no staff-wide organization scope. |
| Parent | Dashboard, Students, and Calendar permissions; no staff-wide organization scope. |

## Usage

Set the flag before running DB migrator:

```powershell
$env:SeedTestLogins='true'
cd src/ShikshaMitra.DbMigrator
dotnet run
```

Or set `SeedTestLogins: true` in the effective configuration for your seed run.
