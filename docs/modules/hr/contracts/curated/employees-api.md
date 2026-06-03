---
module: hr
domain: employees
status: canonical
source: ../raw/employees-api.raw.2026-03-27.md
updated_on: 2026-03-27
owner: backend-team
---

# Employees API

## Purpose
Employee lifecycle and profile APIs for HR operations.

## Endpoint(s)
See source list in [employees raw contract](../raw/employees-api.raw.2026-03-27.md).

## Auth & permissions
Follow HR permissions from PRD: [employees PRD](../../prd/employees/prd.md).

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>` (when tenant-scoped)

## Path/query/body contract
Canonical request/response schema details are sourced from [employees raw contract](../raw/employees-api.raw.2026-03-27.md).

## Response contract (success + errors)
Use standardized business errors and response shapes documented in PRD + raw contract.

## Business rules / invariants
Source of truth: [employees PRD](../../prd/employees/prd.md).

## Idempotency / concurrency notes (if relevant)
No additional module-specific idempotency rules are declared here; apply endpoint-specific behavior from source contract.

## Examples (minimal, copy-ready)
```http
GET /api/employees/{employeeId}
Authorization: Bearer <token>
__tenant: <tenant-id>
```

## Change log
- 2026-03-27: Created canonical curated wrapper from raw contract export.

### Contract clarification
Earlier Widdershins/Slate exports for employee personal and status endpoints were found to diverge from current backend behavior. Treat `contracts/raw` + current PRD as authoritative during contract downloads.
