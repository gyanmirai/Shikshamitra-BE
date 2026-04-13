---
module: hr
domain: attendance-leaves
status: canonical
source: ../raw/attendance-leaves-api.raw.2026-04-13.md
updated_on: 2026-04-13
owner: backend-team
---

# Attendance Leaves API

## Purpose
Attendance tracking and leave workflow APIs.

## Endpoint(s)
See [attendance-leaves raw contract](../raw/attendance-leaves-api.raw.2026-04-13.md).

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
- 2026-04-13: Breaking migration to `academicYearId`-scoped leave settings; leave type route changed to `/api/hr-leave-types`; added leave policy GET-by-id and attendance configuration upsert endpoint.
- 2026-03-27: Created canonical curated wrapper from raw contract export.
