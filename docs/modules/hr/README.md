---
module: hr
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Understand the HR backend module

TL;DR: use this page as the entrypoint for backend HR behavior, API contracts, architecture, workflows, and validation.
The active docs are the six canonical files in this folder.
Use source code when a document and implementation disagree.

## Read order

Follow this order when you need to change or review HR:

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand business behavior, shipped areas, and partial surfaces |
| 2 | [api.md](./api.md) | Find endpoint families, permissions, request formats, and contract caveats |
| 3 | [architecture.md](./architecture.md) | Locate source paths, domain boundaries, and implementation ownership |
| 4 | [workflows.md](./workflows.md) | Trace end-to-end employee, attendance, leave, payroll, policy, and report flows |
| 5 | [testing.md](./testing.md) | Pick validation commands and review known coverage gaps |

## Ownership boundary

Backend HR docs own canonical product behavior, API contracts, permissions, errors, domain rules, and data integrity notes.
Frontend HR docs own routes, screens, React state, RTK Query clients, and browser workflow coverage.
Start frontend work at `../Shikshamitra-FE/docs/hr/README.md`.

## Source of truth

Use source code when a document and implementation disagree:

| Need | Source |
|---|---|
| Backend contracts | `src/ShikshaMitra.Application.Contracts/HR/**` |
| Backend application logic | `src/ShikshaMitra.Application/HR/**` |
| Backend domain rules | `src/ShikshaMitra.Domain/HR/**` |
| Backend persistence | `src/ShikshaMitra.EntityFrameworkCore/HR/**` |
| Backend permissions | `src/ShikshaMitra.Domain.Shared/Constants/Permissions/SubmodulePermissions/HR/**` |
| Backend API tests | `test/ShikshaMitra.HttpApi.Tests/HR/**` |
| Frontend integration docs | `../Shikshamitra-FE/docs/hr/README.md` |

## For agents

Use this table before searching code:

| Question | First file |
|---|---|
| What does HR own? | [product.md](./product.md) |
| Which endpoint or permission should I use? | [api.md](./api.md) |
| Where is the code? | [architecture.md](./architecture.md) |
| How does the workflow run end to end? | [workflows.md](./workflows.md) |
| Which tests should I run? | [testing.md](./testing.md) |
| How do FE permissions map to BE permissions? | [references/permissions.md](./references/permissions.md) |
| How do work calendar, attendance policy, and payroll policy interact? | [references/policy-architecture.md](./references/policy-architecture.md) |

## Supporting material

Use this folder only after the six canonical files:

| Folder | Purpose |
|---|---|
| [references](./references/) | Deep supporting notes that are still active |

## Validation

Run these from the `Shikshamitra-BE` repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module hr
git diff --check
```

Run the frontend HR docs check from `../Shikshamitra-FE`:

```bash
npm run docs:hr:check
```
