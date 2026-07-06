---
module: hr
domain: attendance-integration-db-contract
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Use the attendance import batch database contract

TL;DR: prefer the HTTP API for attendance integrations.
If an integration writes directly to SQL, it must mirror the same aggregate and audit rules as the import API.

## Tables

Use these tables together:

| Table | Entity | Required behavior |
|---|---|---|
| `HR_AttendanceImportBatches` | `AttendanceImportBatch` | Set `RunSource` to `Integration` or `Internal`; keep external correlation and timing fields when available |
| `HR_AttendanceImportRows` | `AttendanceImportRow` | Store one row per source line with `Action` set to `create` or `replace` |
| `AppEmployeeAttendances` | `EmployeeAttendance` | Keep one row per tenant, employee, and calendar date; set `EducationMediumStageId` and `Remarks` when applicable |

## Confirmation rule

Move a batch from `Preview` to `Confirmed` through `MarkConfirmed` after facts are written.
Do not mark a batch confirmed before attendance facts exist.

## Preferred API

Prefer `POST /api/hr/attendance/integration-runs` with `ImportAttendance.Add`.
Raw SQL is allowed only when it mirrors validation, row audit, and confirmation behavior.
