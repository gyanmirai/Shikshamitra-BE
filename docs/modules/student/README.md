---
module: student
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Understand the Student backend module

TL;DR: use this page as the entrypoint for Student enrollment, records, attendance, compliance, certificates, and dashboard behavior.
Student data is critical school data and must be protected for accuracy, tenant isolation, and auditability.

## Read order

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand Student product areas and partial docs coverage |
| 2 | [api.md](./api.md) | Find endpoint families and compatibility notes |
| 3 | [architecture.md](./architecture.md) | Locate Student source paths |
| 4 | [workflows.md](./workflows.md) | Trace inquiry, registration, admission, records, attendance, and certificate flows |
| 5 | [testing.md](./testing.md) | Pick focused Student validation commands |

## Source of truth

| Need | Source |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Students/**` |
| Application logic | `src/ShikshaMitra.Application/Students/**` |
| Domain rules | `src/ShikshaMitra.Domain/Students/**` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Students/**` |
| HTTP controllers | `src/ShikshaMitra.HttpApi/Controllers/Student/**` |
| Tests | `test/ShikshaMitra.*.Tests/Students/**` |

## For agents

Use this table before searching code:

| Question | First file |
|---|---|
| What does Student own? | [product.md](./product.md) |
| Which endpoint should I use? | [api.md](./api.md) |
| Where is the code? | [architecture.md](./architecture.md) |
| Which workflow should I trace? | [workflows.md](./workflows.md) |
| Which tests should I run? | [testing.md](./testing.md) |

## Supporting material

| Folder | Purpose |
|---|---|
| [references](./references/README.md) | Active focused references |

## Validation

Run these commands from the `Shikshamitra-BE` repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```
