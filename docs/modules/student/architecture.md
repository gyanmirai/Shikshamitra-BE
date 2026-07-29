---
module: student
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-07-28
owner: backend-team
audience: humans-and-agents
---

# Locate Student backend code

TL;DR: Student code is under `Students` in contracts, application, domain, and EF Core, while HTTP controllers use `Controllers/Student`.
Student Enrollment refinement must preserve backend authorization, tenant isolation, and the boundary between applicant workflow and active Student records.

## Layer map

| Layer | Path |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Students/**` |
| Application | `src/ShikshaMitra.Application/Students/**` |
| Domain | `src/ShikshaMitra.Domain/Students/**` |
| EF Core | `src/ShikshaMitra.EntityFrameworkCore/Students/**` |
| HTTP API | `src/ShikshaMitra.HttpApi/Controllers/Student/**` |
| Tests | `test/ShikshaMitra.*.Tests/Students/**` |

## Domain subsystems

| Subsystem | Source area |
|---|---|
| Enrollment admissions CRM | `src/ShikshaMitra.Domain/Students/StudentEnrollments/AdmissionsCrm/**` |
| Enrollment compatibility | `src/ShikshaMitra.Domain/Students/StudentEnrollments/**` |
| Records | `src/ShikshaMitra.Domain/Students/Records/**` and root Student entities |
| Attendance | `src/ShikshaMitra.Domain/Students/Attendance/**` |
| Attendance settings | `src/ShikshaMitra.Domain/Students/Attendance/Settings/**` |
| Certificates | Student certificate source paths under Student records |
| Compliance | Student identity compliance source paths |

## Enrollment boundaries

| Boundary | Rule |
|---|---|
| Pipeline before everything | Entrance exam, registration, Finance handoff, placement review, and activation must reference a Pipeline lead |
| Admission Year Setup extends academic year | Admission setup attaches to the Institute Setup academic year and must not duplicate the academic-year model |
| Stepper is projection-friendly | Applicant progress should be derivable from workflow state, configuration, and blockers rather than stored as disconnected UI-only state |
| Applicant before Student | Incomplete admission leads must not become active Student rows |
| Exam before Student | Pre-admission exam records must not require active Student details |
| Finance owns money | Enrollment can request or display Finance verification but cannot post ledgers or receipts |
| Institute Setup owns academic structure | Enrollment uses board, medium, standard, division, and capacity from the existing academic setup and class-management model |
| Student Records after activation | Ordinary active-student changes move out of Enrollment after activation |
| Communication honesty | Delivery state comes from provider evidence or explicit queued or failed states |

## Implementation rules

| Rule | Why it matters |
|---|---|
| Keep lifecycle stages fixed | Support, reporting, and training depend on stable admissions states |
| Allow configurable gates | Schools differ by standard, route, and admission year |
| Version published Admission Year Setup | Later configuration changes must not silently rewrite existing applicant evidence |
| Snapshot the published setup on capture | Each applicant retains the setup identifier, version, and immutable rule snapshot used when the inquiry was accepted |
| Detect likely duplicate inquiries | One applicant record should remain authoritative unless an authorized reason permits another |
| Preserve route compatibility | Existing API paths such as `setup/cycles` and `inquiry-preferences` may remain even when user-facing copy changes |
| Keep historical lookup values visible | Inactive sources, types, and templates must remain readable on historical applicants |
| Require reasons for consequential overrides | Exam bypass, retest, ineligible reopen, capacity override, and activation override must be auditable |
| Protect document access | Student and applicant documents are sensitive |
| Verify academic scope before reports or fees | Student scope feeds Finance, attendance, reporting, and class capacity |
| Keep provider credentials write-only | Provider header values, parameter values, and callback secrets must never be returned after storage |
| Aggregate reports in the database | Tenant, academic-year, UserScope, date, and standard filters must be applied before lifecycle, source, standard, and outcome grouping |
| Bound background processing | Lifecycle reminders process applicants in batches of 250 with a separate unit of work per batch |
| Page large applicant reads in the database | Timeline, task, Pipeline, and automation candidate queries must filter, sort, skip, and take before materialization |
| Keep timeline sorting provider-portable | Timeline events retain the API `DateTimeOffset` and use an indexed UTC `DateTime` sort column for database pagination |
