---
module: hr
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Locate HR backend code

TL;DR: HR follows the backend layered architecture.
Contracts define data transfer objects, application services orchestrate workflows, domain objects enforce business rules, and EF Core owns persistence.

## Layer map

Use this table to find the right source path:

| Layer | Path | Responsibility |
|---|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/HR/**` | Data transfer objects, app-service interfaces, validators, and contract enums |
| Application | `src/ShikshaMitra.Application/HR/**` | Workflow orchestration, authorization checks, projections, exports, PDFs, and events |
| Domain | `src/ShikshaMitra.Domain/HR/**` | Aggregates, entities, managers, services, repositories, and invariants |
| Shared domain | `src/ShikshaMitra.Domain.Shared/HR/**` | Shared enums, constants, error codes, and event transfer objects |
| EF Core | `src/ShikshaMitra.EntityFrameworkCore/HR/**` | Entity configuration, repositories, query implementations, and migrations |
| HTTP API | `src/ShikshaMitra.HttpApi/Controllers/HR/**` | Thin controllers and HTTP-facing contract mapping |
| Tests | `test/ShikshaMitra.*.Tests/HR/**` | Domain, application, API, and smoke coverage |

## Domain subsystems

HR source paths are organized by subsystem:

| Subsystem | Main source area | Owns |
|---|---|---|
| Dashboard | `HR/Dashboard` | HR summary and dashboard projections |
| Employees | `HR/Employees` | Employee aggregate, profile, documents, status, reporting, payroll details, and search |
| Attendance and leaves | `HR/AttendanceAndLeaves` | Attendance facts, leave requests, leave policies, evaluation runs, and leave balances |
| Payroll | `HR/Payroll` | Payroll settings, salary structures, payroll run, attendance imports, and salary slips |
| Calendar | `HR/Calendar` | HR events, holidays, event types, holiday types, and calendar views |
| Policies | `HR/Policies` | Work calendar, attendance policy, payroll policy, assignments, impact, versions, and audit |
| Professional management | `HR/ProfessionalManagement` | Performance templates, cycles, reviews, outcomes, reports, and audit |
| Reports | `HR/Reports` | Report catalog, options, preview, export, and history |

## Implementation rules

Follow these rules when changing HR:

| Rule | Why it matters |
|---|---|
| Keep controllers thin | Business behavior belongs in application services or domain services |
| Keep domain rules server-side | Frontend validation cannot protect data integrity |
| Enforce permissions on the backend | Hidden UI controls do not secure APIs |
| Preserve tenant isolation | HR data must not leak across schools |
| Use transactions for coupled writes | Employee, attendance, leave, and payroll updates must not partially apply |
| Keep payroll snapshots auditable | Salary slips must explain the policy basis used at lock time |
| Update docs with behavior changes | HR docs are part of the handoff contract |

## Policy architecture

Work calendar policy, attendance policy, payroll policy, and policy assignment share a common resolution model.
Read [references/policy-architecture.md](./references/policy-architecture.md) before changing payroll basis, attendance expectation, holiday impact, or employee policy override behavior.

## Permission mapping

Backend permission constants are canonical.
Read [references/permissions.md](./references/permissions.md) before changing frontend permission names or adding server authorization.

## Non-canonical material

Do not restore generated contract exports or old planning docs under `docs/`.
Use current source paths and the six canonical module pages instead.
