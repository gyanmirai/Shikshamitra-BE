# Finance Module

## Sub-modules

- [Concession requests](./concession-requests.md) — fee concessions (single-form create, due-fee approval)

---

# Finance Module — Charges

## Overview

The **Charges** sub-module lets staff create ad-hoc financial charges against a student (e.g. library fine, sports fee, damage). Charges flow through a maker-checker workflow: the person who creates a charge cannot approve or reject it.

Only **Approved** charges appear in fee-collection dues and overdue calculations.

---

## Domain concepts

| Concept | Description |
|---------|-------------|
| `Charge` | Aggregate root. Tracks lifecycle: `Pending → Approved \| Rejected`. |
| `ChargeCategory` | Settings entity. Controls whether approval is required (`IsApprovalRequired`) and whether the category is active (`IsActive`). |
| `ChargeManager` | Domain service. Orchestrates creation: derives scope from Student, validates category active, sets initial status. |
| `FinanceChargeErrorCodes` | Centralized error code constants (`Finance:Charge:xxxxxx`). |

---

## Status machine

```
[Create] ──► Pending ──► Approved
                    └──► Rejected
```

- If `ChargeCategory.IsApprovalRequired = false`, charge is auto-approved on creation.
- Only `Pending` charges can be updated or deleted.
- Approver / rejector **must differ** from the requestor (maker-checker enforced in domain entity).

---

## Scope derivation

`BoardId`, `EducationMediumId`, `StandardId`, `DivisionId` are **not** accepted from the API caller. They are derived server-side from the `Student` aggregate to prevent tampering. The `CreateChargeRequest` only accepts `StudentId`.

---

## Permissions

| Permission | String | Description |
|------------|--------|-------------|
| View | `Finance.Fees.Charges.View` | List / read charges |
| Add | `Finance.Fees.Charges.Add` | Create new charge |
| Update | `Finance.Fees.Charges.Update` | Edit pending charge |
| Delete | `Finance.Fees.Charges.Delete` | Delete pending charge |
| Approve | `Finance.Fees.Charges.Approve` | Approve pending charge |
| Reject | `Finance.Fees.Charges.Reject` | Reject pending charge |

---

## API endpoints

All routes are under `api/app/charges`.

| Method | Route | Permission | Description |
|--------|-------|-----------|-------------|
| POST | `/api/app/charges?academicYearId=` | Add | Create charge |
| GET | `/api/app/charges?academicYearId=` | View | List charges (max 100/page) |
| GET | `/api/app/charges/{chargeId}` | View | Get charge detail |
| PUT | `/api/app/charges/{chargeId}` | Update | Update pending charge |
| POST | `/api/app/charges/{chargeId}/approve` | Approve | Approve pending charge |
| POST | `/api/app/charges/{chargeId}/reject` | Reject | Reject pending charge |
| DELETE | `/api/app/charges/{chargeId}` | Delete | Delete pending charge |

---

## Error codes

See `FinanceChargeErrorCodes` in `Domain.Shared/Finance/Charges/`.

| Code | Meaning |
|------|---------|
| `Finance:Charge:010001` | Charge is not in Pending status |
| `Finance:Charge:010002` | Invalid charge amount (≤ 0) |
| `Finance:Charge:010003` | Due date is in the past |
| `Finance:Charge:010004` | Charge cannot be deleted (not Pending) |
| `Finance:Charge:010005` | Rejection reason too short |
| `Finance:Charge:010006` | Charge category is inactive |
| `Finance:Charge:020001` | Academic year not found |
| `Finance:Charge:020002` | Student not found |
| `Finance:Charge:020003` | Charge category not found |
| `Finance:Charge:020004` | Student missing academic scope (no Standard/Division) |
| `Finance:Charge:030001` | Approver/rejector cannot be the same as requestor |
