---
module: library
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-07-08
owner: backend-team
audience: humans-and-agents
---

# Understand the Library backend module

TL;DR: use this page as the entrypoint for Library behavior, API families, architecture, workflows, and validation.
Library covers catalog, copies, reservations, issue and return, members, charges, reports, notifications, and settings.

## Read order

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand Library product areas and data-integrity rules |
| 2 | [api.md](./api.md) | Find endpoint families, compatibility routes, and permissions |
| 3 | [architecture.md](./architecture.md) | Locate source paths and domain boundaries |
| 4 | [workflows.md](./workflows.md) | Trace catalog, reservation, issue, return, member, charge, and report flows |
| 5 | [testing.md](./testing.md) | Pick focused Library validation commands |

## Source of truth

| Need | Source |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Library/**` |
| Application logic | `src/ShikshaMitra.Application/Library/**` |
| Domain rules | `src/ShikshaMitra.Domain/Library/**` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Library/**` |
| HTTP controllers | `src/ShikshaMitra.HttpApi/Controllers/Library/**` |
| Tests | `test/ShikshaMitra.*.Tests/Library/**` |

## For agents

Use this table before searching code:

| Question | First file |
|---|---|
| What does Library own? | [product.md](./product.md) |
| Which endpoint or permission should I use? | [api.md](./api.md) |
| Where is the code? | [architecture.md](./architecture.md) |
| Which workflow should I trace? | [workflows.md](./workflows.md) |
| Which tests should I run? | [testing.md](./testing.md) |

## Validation

Run these commands from the `Shikshamitra-BE` repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module library
git diff --check
```
