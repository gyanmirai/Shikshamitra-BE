---
module: hr
domain: calendar
status: canonical
source: ../raw/calendar-api.raw.2026-03-27.md
updated_on: 2026-03-27
owner: backend-team
---

# Calendar API

## Purpose
Calendar event and holiday APIs for HR academic operations.

## Endpoint(s)
See [calendar raw contract](../raw/calendar-api.raw.2026-03-27.md).

## Auth & permissions
Follow [calendar PRD](../../prd/calendar/prd.md).

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Path/query/body contract
Canonical schema details are sourced from [calendar raw contract](../raw/calendar-api.raw.2026-03-27.md).

## Response contract (success + errors)
Use PRD and raw contract response/error definitions.

## Business rules / invariants
Source of truth: [calendar PRD](../../prd/calendar/prd.md).

## Idempotency / concurrency notes (if relevant)
No additional module-level note.

## Examples (minimal, copy-ready)
```http
GET /api/calendar
Authorization: Bearer <token>
```

## Change log
- 2026-03-27: Created canonical curated wrapper from raw contract export.
