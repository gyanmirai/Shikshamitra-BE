---
module: student
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-08-14
owner: backend-team
audience: humans-and-agents
---

# Understand the Student backend module

TL;DR: use this page as the entrypoint for Student Enrollment Admissions behavior, APIs, architecture, runtime workflows, and validation.

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
