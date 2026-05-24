# Seeded Accounts And Access

All seeded login accounts use this password unless noted otherwise:

```text
Temp@123
```

Seeded emails are normalized to `@ddvs.com`. Legacy emails are kept only as seeder aliases so rerunning the migrator can update old local databases instead of creating duplicate users.

## Tenant Admin Accounts

These are created by `TenantSeederContributor` when tenants are created.

| Tenant | Email / Login | Role | Access |
|---|---|---|---|
| DDVS | `admin-ddvs@ddvs.com` | admin | Seeded all application/platform permissions; tenant-bound to DDVS. |
| SSVM | `admin-ssvm@ddvs.com` | admin | Seeded all application/platform permissions; tenant-bound to SSVM. |
| Forgot | `admin-forgot@ddvs.com` | admin | Seeded all application/platform permissions; tenant-bound to Forgot. |

## Baseline Staff Accounts

These are seeded for every tenant by `UserSeederContributor`.

| Email / Login | Role | Access |
|---|---|---|
| `teacher-demo-1@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `teacher-demo-2@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `principal-demo-gseb@ddvs.com` | Principal | Principal module permissions; GSEB board data scope only. |
| `hr-manager@ddvs.com` | HR Manager | Dashboard, HR, Calendar, Institute Setup; full organization data scope. |

## DDVS Test Login Accounts

These are seeded only for the DDVS tenant when `SeedTestLogins=true`.

| Email / Login | Role | Access |
|---|---|---|
| `admin-ddvs@ddvs.com` | admin | Seeded all application/platform permissions; tenant-bound to DDVS. |
| `admin-ops@ddvs.com` | admin | Seeded all application/platform permissions; tenant-bound to DDVS. |
| `principal-gseb@ddvs.com` | Principal | Principal module permissions; GSEB board data scope only. |
| `principal-cbse@ddvs.com` | Principal | Principal module permissions; CBSE board data scope only. |
| `teacher-gseb@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `clerk-office@ddvs.com` | Clerk | Dashboard, Students, HR, Communication, Calendar, Institute Setup; full organization data scope. |
| `finance-officer@ddvs.com` | Finance Officer | Dashboard, Students, Finance; full organization data scope. |
| `student-demo@ddvs.com` | Student | Dashboard, Students, Calendar; no staff-wide organization scope. |

## HR Seeder Employee Accounts

These are created by `HRSeederContributor` for each tenant as employee login users.

| Email / Login | Role | Access |
|---|---|---|
| `jayesh.bhagat@ddvs.com` | Director | Seeded all application/platform permissions; full organization data scope. |
| `dasharthbhai.patel@ddvs.com` | Director | Seeded all application/platform permissions; full organization data scope. |
| `tushar.patel@ddvs.com` | Director | Seeded all application/platform permissions; full organization data scope. |
| `hiren.vadher@ddvs.com` | Principal | Principal module permissions; GSEB board data scope by default. |
| `jignesh.jinjala@ddvs.com` | Vice Principal | All seeded application permissions; full organization data scope. |
| `akshay.patel@ddvs.com` | HOD | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `parth.ghusar@ddvs.com` | HOD | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `sweta.badgujar@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `jeet.ram@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `dharmik.jinjala@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `jay.dalia@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `hiren.akbari@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `prerna.atre@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; full organization data scope. |
| `vishal.makwana@ddvs.com` | Administrative Manager | All seeded application permissions; full organization data scope. |
| `mayur.solanki@ddvs.com` | HR Manager | Dashboard, HR, Calendar, Institute Setup; full organization data scope. |
| `punam.badgujar@ddvs.com` | Finance Officer | Dashboard, Students, Finance; full organization data scope. |
| `milind.kumar@ddvs.com` | IT Administrator | All seeded application permissions; full organization data scope. |
| `nikhil.baku@ddvs.com` | Office Assistant | Dashboard, Students, HR, Communication, Calendar, Institute Setup; full organization data scope. |
| `raj.baku@ddvs.com` | Clerk | Dashboard, Students, HR, Communication, Calendar, Institute Setup; full organization data scope. |

## HR Manual Test Accounts

These are seeded only for the DDVS tenant when `SeedHrManualTestData=true`.

| Email / Login | Role | Access |
|---|---|---|
| `yagnik.vadher1@ddvs.com` | Principal | Principal module permissions; GSEB board data scope by default. |
| `yagnik.vadher2@ddvs.com` | Vice Principal | All seeded application permissions; full organization data scope. |
| `prerna.atre@ddvs.com` | HR Manager | Dashboard, HR, Calendar, Institute Setup; scoped to seeded HR medium-stage data by the HR manual test seeder. |
| `parth.ghusar@ddvs.com` | Finance Officer | Dashboard, Students, Finance; scoped to seeded HR medium-stage data by the HR manual test seeder. |
| `raj.baku@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; scoped to seeded HR medium-stage data by the HR manual test seeder. |
| `jignesh.jinjala@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; scoped to seeded HR medium-stage data by the HR manual test seeder. |
| `nikhil.baku@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; scoped to seeded HR medium-stage data by the HR manual test seeder. |
| `keyur.sidhupura@ddvs.com` | Teacher | Dashboard, Students, Academics, Class Management, Calendar; scoped to seeded HR medium-stage data by the HR manual test seeder. |
| `mayur.solanki@ddvs.com` | Administrative Manager | All seeded application permissions; scoped to seeded HR medium-stage data by the HR manual test seeder. |
| `jagruti.vadher@ddvs.com` | Office Assistant | Dashboard, Students, HR, Communication, Calendar, Institute Setup; scoped to seeded HR medium-stage data by the HR manual test seeder. |
| `vishal.makwana@ddvs.com` | IT Administrator | All seeded application permissions; scoped to seeded HR medium-stage data by the HR manual test seeder. |

## Access Notes

- `admin` users receive seeded permissions for setup, troubleshooting, and permission-management screens; they do not bypass tenant isolation.
- Principal users can use permission-management APIs only when the role/user has the relevant ABP permission-management grants.
- `principal-gseb@ddvs.com` and `principal-demo-gseb@ddvs.com` are intended for GSEB-only testing.
- `principal-cbse@ddvs.com` is intended for CBSE-only testing.
- HR manual test users are designed for HR payroll, attendance, leave, and reporting workflows.
- Director seeded employee accounts are full-access tenant staff accounts and are linked to employee records, so `/api/employees/me` resolves for them.
- `principal-gseb@ddvs.com` is linked to the seeded principal employee record and can see the seeded reporting chain in Employee List.
- Student accounts are linked to seeded student records and are not staff users.
- Student seed data cycles through the fun-name list as middle names so student rows expose parent-style names in testing.

## Legacy Aliases Repaired By Seeders

The seeders recognize these older emails/usernames during rerun and update the matched users to the new `@ddvs.com` login names:

```text
admin1@gmail.com
admin2@ddvs.com
principal1@ddvs.com
principle1@ddvs.com
teacher1@ddvs.com
clerk1@ddvs.com
finance1@ddvs.com
student1@ddvs.com
teacher1@school.com
teacher2@school.com
principal@school.com
hr@school.com
*@school.edu seeded employee emails
*@student.test.com seeded student emails
```
