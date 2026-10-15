---
module: institute-setup
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-10-14
owner: backend-team
audience: humans-and-agents
---

# Understand the Institute Setup backend module

TL;DR: use this page as the entrypoint for Institute Setup behavior, API families, architecture, workflows, and validation.
The curated surface includes durable institute initial setup, Rooms Management and Shift Management tenant isolation under Tenant Settings.
School Identity adds shared/scoped assets, immutable uploads, template integration and historical document references. See [product rules](./product.md), [API contracts](./api.md) and the [frontend guide](../../../../Shikshamitra-FE/docs/institute-setup/README.md).

## Read order

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand initial setup and Rooms Management behavior |
| 2 | [api.md](./api.md) | Find initial setup, building, room type, and room routes |
| 3 | [architecture.md](./architecture.md) | Locate Tenant Settings source paths |
| 4 | [workflows.md](./workflows.md) | Trace setup persistence, completion and room flows |
| 5 | [testing.md](./testing.md) | Pick focused validation commands |

## Source of truth

| Need | Source |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/TenantSettings/**` |
| Application logic | `src/ShikshaMitra.Application/TenantSettings/**` |
| Domain rules | `src/ShikshaMitra.Domain/TenantSettings/**` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/TenantSettings/**` |
| HTTP controllers | `src/ShikshaMitra.HttpApi/Controllers/TenantSettings/**` |
| Tests | `test/ShikshaMitra.*.Tests/TenantSettings/**` |

## For agents

The [frontend initial setup docs](../../../../Shikshamitra-FE/docs/initial-setup/README.md) own route guards, wizard layout and browser workflows.

Use this table before searching code:

| Question | First file |
|---|---|
| What does Institute Setup own? | [product.md](./product.md) |
| Which endpoint should I use? | [api.md](./api.md) |
| Where is the code? | [architecture.md](./architecture.md) |
| Which workflow should I trace? | [workflows.md](./workflows.md) |
| Which tests should I run? | [testing.md](./testing.md) |

## Validation

Run these commands from the `Shikshamitra-BE` repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module institute-setup
git diff --check
```
