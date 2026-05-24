---
module: hr
domain: payroll-operations
status: canonical
source: ../raw/payroll-operations-api.raw.2026-04-14.md
updated_on: 2026-04-14
owner: backend-team
---

# Payroll Operations API

## Purpose
Payroll run operations, employee salary structure management, salary slip endpoints, and payroll-facing attendance import log APIs.

## Implemented Endpoints Summary

| Area | Route | Methods | Status |
|---|---|---|---|
| Run Payroll | `/api/payroll/run` | GET (preview), POST (lock) | ✅ Implemented |
| Salary Slips | `/api/payroll/salary-slips` | GET | ✅ Implemented |
| Employee Salary Structure | `/api/payroll/employees/{employeeId}/salary-structure` | GET, PUT | ✅ Implemented |
| Attendance Import Logs | `/api/payroll/attendance-imports` | GET list, GET detail, GET rows | ✅ Implemented |
| Payroll Structures (legacy) | `/api/education-medium-stage/{id}/payroll-structure` | — | ⚠️ Stub (all routes commented out) |

## Endpoint(s)
See [payroll operations raw contract](../raw/payroll-operations-api.raw.2026-04-14.md).

For a full implementation-level reference with request/response examples, see [Implementation Reference](../../../hr/payroll/README.md).

## Auth & permissions
Follow [payroll operations PRD](../../prd/payroll/prd.md).

| Area | Permission |
|---|---|
| Run Payroll — Preview | `PayrollManagement_RunPayroll.View` |
| Run Payroll — Lock | `HR.PayrollManagement.RunPayroll.Lock` |
| Run Payroll — Audit | `HR.PayrollManagement.RunPayroll.ViewAudit` |
| Employee Salary Structure — Get | `Employee_EmployeeList.View` |
| Employee Salary Structure — Update | `Employee_EmployeeList.Add` |
| Attendance Import Logs (list/detail/rows) | `PayrollManagement_RunPayroll.View` |
| Salary Slips | `[Authorize]` (class-level only) |

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Path/query/body contract
Canonical schema details are sourced from [payroll operations raw contract](../raw/payroll-operations-api.raw.2026-04-14.md).

## Response contract (success + errors)
Use PRD and raw contract response/error definitions.

## Key formats
- **Month-year parameter**: `MMMM-YYYY` format (e.g., `JANUARY-2025`) — validated via regex `^[A-Z]+-\d{4}$`
- **Run Payroll pagination**: 0-based page index, pageSize 10–100 (default 50)
- **Import logs pagination**: `skipCount` / `maxResultCount` for both logs and row content endpoints
- **Import log statuses**: `preview`, `confirmed`, `payroll_locked`
- **Salary Slip component types**: returned in UPPERCASE (e.g., `FIXED`, `PERCENTAGE`, `BASIC`, `GROSS`)

## Business rules / invariants
Source of truth: [payroll operations PRD](../../prd/payroll/prd.md).

## Idempotency / concurrency notes (if relevant)
- Locking payroll for an already-locked month returns `PayrollAlreadyLocked` error (idempotent).
- Finalized salary slips cannot be regenerated.
- Attendance import log status `payroll_locked` is a derived state based on finalized salary slips for the same payroll month.

## Examples (minimal, copy-ready)
```http
GET /api/payroll/run?month=JANUARY-2025&page=0&pageSize=50
Authorization: Bearer <token>
__tenant: <tenant-id>
```

```http
POST /api/payroll/run
Authorization: Bearer <token>
__tenant: <tenant-id>
Content-Type: application/json

{ "month": "JANUARY-2025" }
```

```http
GET /api/payroll/salary-slips?employeeId=<guid>&monthYear=JANUARY-2025
Authorization: Bearer <token>
__tenant: <tenant-id>
```

```http
GET /api/payroll/employees/<employeeId>/salary-structure
Authorization: Bearer <token>
__tenant: <tenant-id>
```

```http
GET /api/payroll/attendance-imports?skipCount=0&maxResultCount=20
Authorization: Bearer <token>
__tenant: <tenant-id>
```

```http
GET /api/payroll/attendance-imports/<importId>/rows?skipCount=0&maxResultCount=50
Authorization: Bearer <token>
__tenant: <tenant-id>
```

## Change log
- 2026-04-14: Added attendance import logs endpoints (`/api/payroll/attendance-imports`, detail, rows), status lifecycle (`preview`, `confirmed`, `payroll_locked`), and permission matrix updates.
- 2026-03-27: Created canonical curated wrapper from raw contract export.
- 2025-07-15: Documented Employee Salary Structure endpoints, salary slip details, run payroll pagination, and key formats. Added permission matrix and linked implementation reference.

### Legacy clarification
Legacy salary-structure markdown exports existed in old root docs and may not reflect current request model details. The `PayrollStructureController` (legacy route) has all endpoints commented out. Use raw downloaded contracts in this module plus PRD rules as the source of truth.
