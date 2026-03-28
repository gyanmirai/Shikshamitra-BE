---
module: hr
domain: payroll-operations
status: canonical
source: ../raw/payroll-operations-api.raw.2026-03-27.md
updated_on: 2026-03-27
owner: backend-team
---

# Payroll Operations API

## Purpose
Payroll run operations, structures, and salary slip operational endpoints.

## Endpoint(s)
See [payroll operations raw contract](../raw/payroll-operations-api.raw.2026-03-27.md).

## Auth & permissions
Follow [payroll operations PRD](../../prd/payroll/prd.md).

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Path/query/body contract
Canonical schema details are sourced from [payroll operations raw contract](../raw/payroll-operations-api.raw.2026-03-27.md).

## Response contract (success + errors)
Use PRD and raw contract response/error definitions.

## Business rules / invariants
Source of truth: [payroll operations PRD](../../prd/payroll/prd.md).

## Idempotency / concurrency notes (if relevant)
No additional module-level note.

## Examples (minimal, copy-ready)
```http
POST /api/payroll/run
Authorization: Bearer <token>
```

## Change log
- 2026-03-27: Created canonical curated wrapper from raw contract export.

### Legacy clarification
Legacy salary-structure markdown exports existed in old root docs and may not reflect current request model details. Use raw downloaded contracts in this module plus PRD rules as the source of truth.
