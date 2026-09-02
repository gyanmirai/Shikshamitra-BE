---
module: examination
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-08-31
owner: backend-team
audience: humans-and-agents
---

# Understand the Examination backend module

TL;DR: this doc set currently covers examination seating-plan behavior, APIs, data integrity, workflows, and focused validation.

## Read order

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand subject ownership and plan-group invariants |
| 2 | [api.md](./api.md) | Find routes, permissions, versions, and errors |
| 3 | [architecture.md](./architecture.md) | Locate contracts, domain, persistence, and migration code |
| 4 | [workflows.md](./workflows.md) | Trace preparation through publication |
| 5 | [testing.md](./testing.md) | Run focused validation |

## Source of truth

| Need | Source |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Academic/Examination/SeatAllocation/**` |
| Application behavior | `src/ShikshaMitra.Application/Academic/Examination/SeatAllocation/**` |
| Domain | `src/ShikshaMitra.Domain/Academic/Examination/SeatAllocation/**` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Academic/Examination/SeatAllocation/**` |
| HTTP routes | `src/ShikshaMitra.HttpApi/Controllers/Academic/Examination/Exams/SeatAllocationController.cs` |
| Tests | `test/ShikshaMitra.Application.Tests/Academic/Examination/SeatAllocation/**` |

## For agents

| Question | First file |
|---|---|
| What is one seating plan allowed to cover? | [product.md](./product.md) |
| Which permission protects an operation? | [api.md](./api.md) |
| Where is subject ownership enforced? | [architecture.md](./architecture.md) |
| Which operational sequence is safe? | [workflows.md](./workflows.md) |
| Which tests prove it? | [testing.md](./testing.md) |

## Validation

Run from `Shikshamitra-BE`:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module examination
git diff --check
```
