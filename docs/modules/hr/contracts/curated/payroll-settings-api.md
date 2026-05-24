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
Payroll settings endpoints for payroll policies, earning components, deduction components, PF rules, and pay elements.

## Implemented Endpoints Summary

| Area | Route | Methods | Status |
|---|---|---|---|
| Earning Components | `/api/payroll/earning-components` | GET (list), GET (by id), POST, PUT, DELETE | ✅ Implemented |
| Deduction Components | `/api/payroll/deduction-components` | GET (list), GET (by id), POST, PUT, DELETE | ✅ Implemented |
| PF Rules | `/api/payroll/pf-rules` | GET (list), GET (by id), POST, PUT, DELETE | ✅ Implemented |
| Pay Elements | `/api/payroll/pay-elements` | GET (list), GET (by id), POST, PUT, DELETE | ✅ Implemented |
| Payroll Policies | `/api/hr/payroll/policies` | GET (list), GET (by id), POST, PUT, DELETE, set-default | ✅ Implemented |
| Professional Tax | — | — | ⚠️ Domain + DTOs exist, no controller |

## Endpoint(s)
Module-driven payroll policy endpoints:

```http
GET /api/hr/payroll/policies?academicYearId={academicYearId}
POST /api/hr/payroll/policies
GET /api/hr/payroll/policies/{id}
PUT /api/hr/payroll/policies/{id}
DELETE /api/hr/payroll/policies/{id}
POST /api/hr/payroll/policies/{id}/set-default
```

For a full implementation-level reference with request/response examples, see [Implementation Reference](../../../hr/payroll/README.md).

## Auth & permissions
Follow [payroll settings PRD](../../prd/payroll/settings/prd.md).

| Area | View | Add/Update | Delete |
|---|---|---|---|
| Payroll Policies | `HR.PayrollManagement.Settings.PayrollPolicies.View` | `HR.PayrollManagement.Settings.PayrollPolicies.Add` | `HR.PayrollManagement.Settings.PayrollPolicies.Delete` |
| Earning Components | `PayrollManagement_SettingsEarningComponents.View` | `PayrollManagement_SettingsEarningComponents.Add` | `PayrollManagement_SettingsEarningComponents.Delete` |
| Deduction Components | `PayrollManagement_SettingsDeductionComponents.View` | `PayrollManagement_SettingsDeductionComponents.Add` | `PayrollManagement_SettingsDeductionComponents.Delete` |
| PF Rules | `PayrollManagement_SettingsPfRule.View` | `PayrollManagement_SettingsPfRule.Add` | `PayrollManagement_SettingsPfRule.Delete` |
| Pay Elements | `[Authorize]` (class-level only) | `[Authorize]` (class-level only) | `[Authorize]` (class-level only) |

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Path/query/body contract
Canonical schema details are sourced from the HR policy contract and implementation architecture docs.

## Response contract (success + errors)
Use PRD and raw contract response/error definitions.

## Business rules / invariants
Source of truth: [payroll settings PRD](../../prd/payroll/settings/prd.md). Key domains are payroll policies, salary component configuration, PF rule maintenance, and pay element management.

## Idempotency / concurrency notes (if relevant)
No additional module-level note.

## Examples (minimal, copy-ready)
```http
GET /api/payroll/earning-components?page=1&pageSize=20&search=Basic
Authorization: Bearer <token>
__tenant: <tenant-id>
```

```http
GET /api/payroll/pay-elements?SkipCount=0&MaxResultCount=10
Authorization: Bearer <token>
__tenant: <tenant-id>
```

## Not yet exposed
- **Professional Tax**: `ProfessionalTaxAppService` and DTOs (`CreateUpdateProfessionalTaxDto`, `ProfessionalTaxDto`) exist in the application layer but no HTTP controller has been created. The domain model supports slab-based tax by gender and salary range.

## Change log
- 2026-03-27: Created canonical curated wrapper from raw contract export.
- 2026-03-28: Updated source contract and examples to reflect day calculation plus current payroll settings endpoints.
- 2025-07-15: Documented Pay Elements endpoints, Professional Tax gap, and added per-area permission matrix. Linked implementation reference.
- 2026-05-19: Removed old day-calculation API from the canonical contract; payroll policies are now the only payroll basis configuration API.
