---
module: student
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-07-28
owner: backend-team
audience: humans-and-agents
---

# Understand the Student backend module

TL;DR: use this page as the entrypoint for Student enrollment, records, attendance, compliance, certificates, and dashboard behavior.
Student Enrollment uses a production requirement baseline centered on Pipeline, Assessments, Visits & Events, Registration, Finance handoff, Class Placement, Activation, and Communication.

## Read order

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand Student product areas, Enrollment boundaries, and deferred surfaces |
| 2 | [prd-student-enrollment.md](./prd-student-enrollment.md) | Understand the refined Student Enrollment operating PRD |
| 3 | [workflows.md](./workflows.md) | Trace the admissions flow from Pipeline to activation |
| 4 | [api.md](./api.md) | Find endpoint families, permissions, and integration boundaries |
| 5 | [architecture.md](./architecture.md) | Locate Student source paths and ownership rules |
| 6 | [testing.md](./testing.md) | Pick focused Student validation commands |

## Source of truth

| Need | Source |
|---|---|
| Enrollment PRD | [prd-student-enrollment.md](./prd-student-enrollment.md) |
| Contracts | `src/ShikshaMitra.Application.Contracts/Students/**` |
| Application logic | `src/ShikshaMitra.Application/Students/**` |
| Domain rules | `src/ShikshaMitra.Domain/Students/**` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Students/**` |
| HTTP controllers | `src/ShikshaMitra.HttpApi/Controllers/Student/**` |
| Tests | `test/ShikshaMitra.*.Tests/Students/**` |
| Frontend integration docs | [Student frontend docs](../../../../Shikshamitra-FE/docs/student/README.md) |

## Ownership boundary

Backend Student docs own product rules, endpoint contracts, domain rules, persistence, permissions, tenant isolation, academic scope, and data integrity notes.
Frontend Student docs own visible navigation, routes, screen folders, React state, Redux Toolkit Query clients, and browser workflow coverage.
Finance docs and services own monetary posting, receipts, settlement, reconciliation, concessions, and refunds.
Institute Setup and Class Management own academic structure and capacity truth that Enrollment consumes for placement.
Student Records owns the active student lifecycle after activation.

## For agents

Use this table before searching code:

| Question | First file |
|---|---|
| What does Student own? | [product.md](./product.md) |
| What is the refined Student Enrollment requirement? | [prd-student-enrollment.md](./prd-student-enrollment.md) |
| Which Enrollment workflow should I trace? | [workflows.md](./workflows.md) |
| Which endpoint family should I use? | [api.md](./api.md) |
| Where is the code? | [architecture.md](./architecture.md) |
| Which tests should I run? | [testing.md](./testing.md) |
| Which frontend route or client owns a screen? | [Student frontend docs](../../../../Shikshamitra-FE/docs/student/README.md) |

## Validation

Run these commands from the `Shikshamitra-BE` repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```

Run the frontend Student docs check from `../../../../Shikshamitra-FE` when frontend docs or integration docs changed:

```bash
node scripts/docs/validate-docs.mjs --module student
```
