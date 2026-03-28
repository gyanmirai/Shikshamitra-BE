---
module: hr
domain: payroll-settings
status: canonical
source: ../raw/payroll-settings-api.raw.2026-03-27.md
updated_on: 2026-03-27
owner: backend-team
---

# Payroll Settings API

## Purpose
Payroll configuration endpoints for components, tax, and fund settings.

## Endpoint(s)
See [payroll settings raw contract](../raw/payroll-settings-api.raw.2026-03-27.md).

## Auth & permissions
Follow [payroll settings PRD](../../prd/payroll/settings/prd.md).

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Path/query/body contract
Canonical schema details are sourced from [payroll settings raw contract](../raw/payroll-settings-api.raw.2026-03-27.md).

## Response contract (success + errors)
Use PRD and raw contract response/error definitions.

## Business rules / invariants
Source of truth: [payroll settings PRD](../../prd/payroll/settings/prd.md).

## Idempotency / concurrency notes (if relevant)
No additional module-level note.

## Examples (minimal, copy-ready)
```http
GET /api/payroll/settings/pay-elements
Authorization: Bearer <token>
```

## Change log
- 2026-03-27: Created canonical curated wrapper from raw contract export.
