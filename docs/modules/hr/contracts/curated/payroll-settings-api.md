---
module: hr
domain: payroll-settings
status: canonical
source: ../raw/payroll-settings-api.raw.2026-03-28.md
updated_on: 2026-03-28
owner: backend-team
---

# Payroll Settings API

## Purpose
Payroll configuration endpoints for day calculation, earning components, deduction components, and PF rules.

## Endpoint(s)
See [payroll settings raw contract](../raw/payroll-settings-api.raw.2026-03-28.md).

## Auth & permissions
Follow [payroll settings PRD](../../prd/payroll/settings/prd.md).

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Path/query/body contract
Canonical schema details are sourced from [payroll settings raw contract](../raw/payroll-settings-api.raw.2026-03-28.md).

## Response contract (success + errors)
Use PRD and raw contract response/error definitions.

## Business rules / invariants
Source of truth: [payroll settings PRD](../../prd/payroll/settings/prd.md). Key domains are academic-year-scoped day calculation, salary component configuration, and PF rule maintenance.

## Idempotency / concurrency notes (if relevant)
No additional module-level note.

## Examples (minimal, copy-ready)
```http
GET /api/payroll/earning-components?academicYearId=<academic-year-id>
Authorization: Bearer <token>
__tenant: <tenant-id>
```

## Change log
- 2026-03-27: Created canonical curated wrapper from raw contract export.
- 2026-03-28: Updated source contract and examples to reflect day calculation plus current payroll settings endpoints.
