---
module: hr
domain: attendance-leaves
status: canonical
source: ../raw/attendance-leaves-api.raw.2026-04-13.md
updated_on: 2026-05-20
owner: backend-team
---

# Attendance Leaves API

## Purpose
Attendance tracking and leave workflow APIs.

## Endpoint(s)
See [attendance-leaves raw contract](../raw/attendance-leaves-api.raw.2026-04-13.md).

New module-driven attendance policy endpoints:

```http
GET /api/hr/attendance/policies?academicYearId={academicYearId}
POST /api/hr/attendance/policies
GET /api/hr/attendance/policies/{id}
PUT /api/hr/attendance/policies/{id}
DELETE /api/hr/attendance/policies/{id}
POST /api/hr/attendance/policies/{id}/set-default
```

Shared policy assignment endpoints:

```http
GET /api/hr/work-calendar-policies?academicYearId={academicYearId}
GET /api/hr/policy-assignments?academicYearId={academicYearId}
GET /api/hr/policy-assignment-preview?academicYearId={academicYearId}&date=YYYY-MM-DD
GET /api/hr/employees/{employeeId}/effective-policies?date=YYYY-MM-DD
GET /api/hr/policy-holiday-impact-preview?academicYearId={academicYearId}&workCalendarPolicyId={workCalendarPolicyId}&month=YYYY-MM
```

Attendance evaluation endpoints:

```http
POST /api/hr/attendance/evaluations/preview
POST /api/hr/attendance/evaluations
GET /api/hr/attendance/evaluations?academicYearId={academicYearId}&month={month}&year={year}
GET /api/hr/attendance/evaluations/{runId}
GET /api/hr/attendance/evaluations/{runId}/results
POST /api/hr/attendance/evaluations/{runId}/review
POST /api/hr/attendance/evaluations/{runId}/approve
POST /api/hr/attendance/evaluations/{runId}/publish
POST /api/hr/attendance/evaluations/{runId}/cancel
```

## Auth & permissions
Follow [attendance-leaves PRD](../../prd/attendance-leaves/prd.md).

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Path/query/body contract
Canonical schema details are sourced from [attendance-leaves raw contract](../raw/attendance-leaves-api.raw.2026-04-13.md).

## Response contract (success + errors)
Use PRD and raw contract response/error definitions.

## Business rules / invariants
Source of truth: [attendance-leaves PRD](../../prd/attendance-leaves/prd.md).

## Idempotency / concurrency notes (if relevant)
No additional module-level note.

## Examples (minimal, copy-ready)
```http
POST /api/hr-leave-types?academicYearId={academicYearId}
Authorization: Bearer <token>
```

## Change log
- 2026-05-20: Added HR policy evaluation batch endpoints and clarified that evaluation creates policy-specific runs.
- 2026-04-13: Breaking migration to `academicYearId`-scoped leave settings; leave type route changed to `/api/hr-leave-types`; added leave policy GET-by-id and attendance configuration upsert endpoint.
- 2026-03-27: Created canonical curated wrapper from raw contract export.
