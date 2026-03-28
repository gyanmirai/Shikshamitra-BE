---
module: hr
domain: reports
status: canonical
source: ../raw/reports-api.raw.2026-03-27.md
updated_on: 2026-03-27
owner: backend-team
---

# Reports API

## Purpose
HR reporting and export endpoints.

## Endpoint(s)
See [reports raw contract](../raw/reports-api.raw.2026-03-27.md).

## Auth & permissions
Follow [reports PRD](../../prd/reports/prd.md).

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Path/query/body contract
Canonical schema details are sourced from [reports raw contract](../raw/reports-api.raw.2026-03-27.md).

## Response contract (success + errors)
Use PRD and raw contract response/error definitions.

## Business rules / invariants
Source of truth: [reports PRD](../../prd/reports/prd.md).

## Idempotency / concurrency notes (if relevant)
No additional module-level note.

## Examples (minimal, copy-ready)
```http
GET /api/hr/reports/employees
Authorization: Bearer <token>
```

## Change log
- 2026-03-27: Created canonical curated wrapper from raw contract export.
