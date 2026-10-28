---
module: academic
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-10-28
owner: backend-team
audience: humans-and-agents
---

# Understand the Academic setup guide backend

TL;DR: this doc set owns the Academic setup guide's school-wide first-visit marker, year-specific advisory progress, configuration-state queries and security contract.
Operational configuration stays with its existing application services.
[Examination](../examination/README.md) owns its documented seating workflows; [frontend Academic docs](../../../../Shikshamitra-FE/docs/academic/README.md) own routes, screens, forms and browser evidence.

## Read order

| File | Use it to |
|---|---|
| [product.md](./product.md) | Distinguish first visit, guide progress and actual configuration |
| [api.md](./api.md) | Find endpoints, payloads, permissions, scope and errors |
| [architecture.md](./architecture.md) | Locate implementation and existing persistence |
| [workflows.md](./workflows.md) | Trace first entry, resuming, configuration and conflicts |
| [testing.md](./testing.md) | Run focused checks and understand validation limits |
| [UI read contracts](./references/ui-read-performance.md) | Find operational workspace, paging, report and performance-test contracts |

## Source of truth

| Concern | Source |
|---|---|
| HTTP routes | `src/ShikshaMitra.HttpApi/Controllers/Academic/Setup/AcademicSetupController.cs` |
| Contracts | `src/ShikshaMitra.Application.Contracts/Academic/Setup/**` |
| Authorization and configuration states | `src/ShikshaMitra.Application/Academic/Setup/**` |
| Stored metadata and invariants | `src/ShikshaMitra.Domain/Academic/Setup/Services/**` |
| Exact keys and error codes | `src/ShikshaMitra.Domain.Shared/Academic/Setup/AcademicSetupKeys.cs` |
| Executable evidence | `test/ShikshaMitra.HttpApi.Tests/Academic/Setup/**` |

## For agents

| Question | First page |
|---|---|
| Does missing configuration reopen the guide? | [Product rules](./product.md) |
| Which setting key or permission is accepted? | [API contract](./api.md) |
| Does this require a migration or store form drafts? | [Persistence ownership](./architecture.md) |
| How does a concurrent visitor or stale cursor recover? | [Workflows](./workflows.md) |
| What has been tested on PostgreSQL? | [Testing limits](./testing.md) |

## Validation

Run from `Shikshamitra-BE`:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module academic
git diff --check
```

See [testing.md](./testing.md) for API, build and security commands.
