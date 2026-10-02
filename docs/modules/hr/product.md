---
module: hr
domain: product
content_type: Reference
status: canonical
updated_on: 2026-10-02
owner: backend-team
audience: humans-and-agents
---

# Understand HR product behavior

TL;DR: HR manages employee records, attendance, leave, payroll, HR policies, professional management, reports, notifications, and calendar-backed HR scenarios.
Use this page for business behavior and shipped-surface status before reading API or code details.

## Product areas

HR behavior is split by operational area:

| Area | Status | Behavior owned by backend HR |
|---|---|---|
| Dashboard | Implemented | English daily staff visibility, attendance classifications, scoped staff filters, attention items, seven-day counts, and saved payroll cycle counts |
| Employees | Implemented | Employee lifecycle, profile data, documents, I-card data, status transitions, payroll details, reporting manager, and academic scope |
| Attendance | Implemented | Manual attendance, attendance tracking, grouped register, imports, automated runs, locks, and payroll tags |
| Leave | Implemented | Paid and unpaid leave policies, leave requests, approvals, balances, leave center, and payroll impact |
| Payroll | Implemented | Component settings, versioned compensation revisions, Gross, Take-home, and Employer Cost quotes, ESI statutory setup and calculation, readiness, preview, reviews, adjustments, lock, salary slips, and PDF output |
| HR policies | Implemented | Work calendar policies, attendance policies, payroll policies, assignments, effective preview, versions, audit, and holiday impact |
| Professional management | Implemented | Templates, cycles, reviews, observations, evidence, feedback, outcomes, reports, audit, and PDFs |
| Reports | Implemented | HR report catalog, options, templates, preview, export, and export history |
| Notifications | Implemented | HR notification settings and notification history surfaces |
| Events and holidays | Partial | Calendar contracts exist, but frontend HR navigation must be verified before treating HR menu access as shipped |
| TDS | Partial | Payroll settings and statutory behavior require separate verification before use. |
| Training | Unconfirmed | Treat as product-unconfirmed until the frontend menu and backend ownership are verified |

## Business rules that protect data

Treat these rules as critical because HR feeds payroll and staff records:

| Rule | Consequence |
|---|---|
| Employee data is tenant-scoped | Queries and reports must not cross school boundaries |
| Employee status changes require valid transitions | Left or retired employees cannot silently become active through unrelated updates |
| Attendance locks protect payroll basis | Locked days or batches must block or control edits |
| Leave policies separate paid and unpaid rules | Payroll impact depends on paid type and policy resolution |
| Effective compensation revisions are the recurring salary source of truth | Superseded and legacy non-effective records cannot silently drive payroll |
| One-time salary components remain separate | Temporary earnings and deductions cannot change the effective recurring compensation revision |
| Saving recurring compensation is direct and immutable | Every save is recalculated on the server and creates a new effective revision without an approval queue |
| Employee activation requires payroll readiness | New employees remain Inactive until an effective salary covers the later of joining month or current payroll month |
| Protected compensation drift is visible but non-mutating | Payroll warns without rewriting saved salary or blocking lock |
| Earning component dependencies are tenant-scoped and acyclic | A percentage component cannot reference itself, another tenant, an inactive target, or a dependency chain that returns to itself |
| Payroll preview and lock use the same policy basis | Salary slips must match the locked payroll snapshot |
| Policy assignments resolve by specificity and date | Employee overrides beat group assignments, and defaults are fallback only |
| Reports must be reproducible and scope-safe | Exports must use the same filters and source data as previews, and recipients must cover the complete tenant UserScope captured at generation |

## Product behavior by area

Use this map to choose the right API and source path:

| Area | Key behavior | Read next |
|---|---|---|
| Employees | Create, search, edit profile tabs, manage documents, update status, assign reporting manager, and manage payroll details | [api.md](./api.md#employee-api-families) |
| Attendance | Capture day attendance, import files or device rows, track registers, adjust rows, and lock batches | [workflows.md](./workflows.md#attendance-workflows) |
| Leave | Configure paid and unpaid policies, submit leave, approve or reject requests, and update balances | [workflows.md](./workflows.md#leave-workflows) |
| Payroll | Configure components, assign salary structures, preview, review, adjust, lock, and generate slips | [workflows.md](./workflows.md#payroll-workflows) |
| Policies | Manage calendars, payroll policies, attendance policies, assignments, impact previews, versions, and audit | [references/policy-architecture.md](./references/policy-architecture.md) |
| Reports | Build report definitions, preview, export, and track history | [api.md](./api.md#reports-api-families) |

## Partial surfaces

Do not treat these as fully shipped without checking frontend navigation and backend contracts:

| Surface | Current rule |
|---|---|
| HR events and holidays | Pages and contracts exist, but active navigation may belong to Calendar instead of HR |
| TDS | Payroll setting pages exist, but active product status depends on menu and backend route verification |
| Professional tax | Domain and data transfer objects exist, but no canonical HTTP controller is documented |
| Training | Pages exist outside the main HR menu model, so confirm ownership before extending |
| Older performance routes | Prefer professional-management routes and verify `/hr/performance` before reuse |

## Canonical contract rule

Use [api.md](./api.md) for current endpoint behavior.
Use current source files for behavior that is not covered here.
Do not restore old generated exports or old planning docs under `docs/`.
