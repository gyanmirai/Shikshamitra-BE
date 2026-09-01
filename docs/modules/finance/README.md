---
module: finance
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-07-08
owner: backend-team
audience: humans-and-agents
---

# Understand the Finance backend module

TL;DR: use this page as the entrypoint for Finance behavior, API families, architecture, workflows, and validation.
Finance covers fees, collections, concessions, adjustments, refunds, receipts, accounts, audit, and reports.

## Read order

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand Finance product areas and data-integrity rules |
| 2 | [api.md](./api.md) | Find endpoint families, permissions, and route ownership |
| 3 | [architecture.md](./architecture.md) | Locate source paths and domain boundaries |
| 4 | [workflows.md](./workflows.md) | Trace charge, concession, adjustment, collection, refund, and reconciliation flows |
| 5 | [testing.md](./testing.md) | Pick focused Finance validation commands |
| 6 | [Concession policy flow](./references/concession-policy-flow.md) | Review the proposed versioned concession policy, fee assignment, request, approval, allocation, and correction design |

## Source of truth

| Need | Source |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Finance/**` |
| Application logic | `src/ShikshaMitra.Application/Finance/**` |
| Domain rules | `src/ShikshaMitra.Domain/Finance/**` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Finance/**` |
| HTTP controllers | `src/ShikshaMitra.HttpApi/Controllers/Finance/**` |
| Tests | `test/ShikshaMitra.*.Tests/Finance/**` |

## For agents

Use this table before searching code:

| Question | First file |
|---|---|
| What does Finance own? | [product.md](./product.md) |
| Which endpoint or permission should I use? | [api.md](./api.md) |
| Where is the code? | [architecture.md](./architecture.md) |
| Which workflow should I trace? | [workflows.md](./workflows.md) |
| Which tests should I run? | [testing.md](./testing.md) |
| How should Concession Policy integrate with fee structures and student requests? | [Concession policy flow](./references/concession-policy-flow.md) |

## Validation

Run these commands from the `Shikshamitra-BE` repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module finance
git diff --check
```
