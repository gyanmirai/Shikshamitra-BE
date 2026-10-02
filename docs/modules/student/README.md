---
module: student
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-10-01
owner: backend-team
audience: humans-and-agents
---

# Understand the Student backend module

TL;DR: use this page for Admissions, Onboarding, Student Records, and daily Student Attendance behavior, APIs, ownership, and validation.

## Student Attendance

Attendance supports one daily Present/Absent observation, optional Late/Half-day details, dated school policy, individual schedules, leave reconciliation, audited corrections, and a common reporting calculation.
Source implementation is available; the local verification gates in [testing.md](./testing.md) still govern release readiness.
Use [frontend Student documentation](../../../../Shikshamitra-FE/docs/student/README.md) for the four visible Attendance tasks and screen/cache behavior.

| Source | Location |
|---|---|
| Contracts and HTTP | `src/ShikshaMitra.Application.Contracts/Students/Attendance/`, `src/ShikshaMitra.HttpApi/Controllers/Student/Attendance/` |
| Runtime reads and commands | `src/ShikshaMitra.Application/Students/Attendance/` |
| Policy, schedules, calculation and journal | `src/ShikshaMitra.Domain.Shared/Students/Attendance/`, `src/ShikshaMitra.Domain/Students/Attendance/` |
| Dated enrollment membership | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Aggregates/StudentEnrollment.Attendance.cs` |
| Persistence and migration | `src/ShikshaMitra.EntityFrameworkCore/Students/Attendance/`, `src/ShikshaMitra.EntityFrameworkCore/Migrations/20261001043143_AddUniversalStudentAttendance.cs` |

## Student Records

The Records implementation connects completed onboarding to a permanent profile, governed changes, documents and IDs, year-end transitions, reports, and published school policy. Initial enrollment activation remains owned by Onboarding. Use the six canonical pages below for backend behavior; frontend routes and presentation remain owned by [frontend Student documentation](../../../../Shikshamitra-FE/docs/student/README.md).

| Source | Location |
|---|---|
| Runtime contracts and controllers | `Application.Contracts/Students/Records/`, `HttpApi/Controllers/Student/Records/` |
| Governance, scope, transitions, renewal, documents, reports | `Application/Students/Records/` |
| Published school policy | `Domain.Shared/Students/StudentRecords/StudentRecordsPolicy.cs`, `Domain/Students/StudentRecords/`, `Application/Students/StudentRecords/Settings/` |
| Records persistence | `EntityFrameworkCore/Students/Records/` |
| Focused scenarios | `test/ShikshaMitra.Application.Tests/Students/Records/`, Domain policy/transition tests |

These source locations are under `src/` unless explicitly prefixed with `test/`. Source implementation and local tests do not establish that migrations, authenticated school walkthroughs, or deployment gates have passed.

## Read order

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand shipped Admissions behavior and invariants |
| 2 | [api.md](./api.md) | Find endpoint families, permissions, commands, and errors |
| 3 | [architecture.md](./architecture.md) | Locate source paths and ownership boundaries |
| 4 | [workflows.md](./workflows.md) | Trace setup, inquiry, application, clone, and bulk flows |
| 5 | [testing.md](./testing.md) | Run focused Student Admissions validation |

## Source of truth

| Need | Source |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Students/Enrollments/Admissions/**` |
| Application logic | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/**` |
| Domain rules | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/**` |
| Shared enums and errors | `src/ShikshaMitra.Domain.Shared/Students/StudentEnrollments/Admissions/**` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Students/StudentEnrollments/Admissions/**` |
| HTTP controllers | `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/Admissions/**` |
| Focused tests | `test/ShikshaMitra.*.Tests/Students/Enrollment/**` |

## For agents

| Question | First file |
|---|---|
| What makes an offering available? | [product.md](./product.md) |
| Which endpoint or permission applies? | [api.md](./api.md) |
| Where is readiness or hierarchy validated? | [architecture.md](./architecture.md) |
| How are runtime versions frozen? | [workflows.md](./workflows.md) |
| Which tests prove the behavior? | [testing.md](./testing.md) |

## Validation

Run from `Shikshamitra-BE`:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```
