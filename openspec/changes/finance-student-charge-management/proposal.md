# Proposal: Student Charge Management (Finance → Fees)

## Summary
Implement/finish scalable **Student Charge Management** in Finance: create student charges from a UI form and support **approve/reject** with maker-checker. Charge categories come from **Finance Settings** and can require approval.

## Why
We need a consistent and secure backend workflow for charges that:
- Matches the UI (category, amount, due date, priority, purpose/description, student, notes)
- Enforces lifecycle state transitions (Pending/Approved/Rejected)
- Prevents tampering (derive academic scope from StudentId)
- Keeps fee calculations correct (only Approved charges affect dues/overdue)
- Scales (fast list APIs, bounded pagination)

## What Changes
- Standardize charge creation around **StudentId-derived** Board/Medium/Standard/Division (anti-tamper)
- Enforce maker-checker: requestor cannot approve/reject own charge
- Expand permissions to include Update/Approve/Reject (not just View/Add/Delete)
- Introduce FluentValidation validators for request DTOs
- Optimize list queries (projection, allow-listed sorting, accurate per-status counts)
- Update Fee Collection to include **only Approved charges** in totals/overdue

## Capabilities

### New Capabilities
- `finance-fees-charges`: Create, list, view, update (pending-only), approve, reject, delete (pending-only) student charges

### Modified Capabilities
- `finance-fees-collections`: Dues/overdue calculations exclude non-approved charges

## Impact
- Domain: `src/ShikshaMitra.Domain/Finance/Fees/Charges/*`, `src/ShikshaMitra.Domain.Shared/Finance/Charges/*`
- EF Core: `src/ShikshaMitra.EntityFrameworkCore/Finance/Fees/Charges/*`
- Application: `src/ShikshaMitra.Application/Finance/Fees/Charges/*`, `.../Collections/*`
- Contracts: `src/ShikshaMitra.Application.Contracts/Finance/Fees/Charges/*`
- HTTP APIs:
  - `POST /api/charges`
  - `GET /api/charges`
  - `GET /api/charges/{chargeId}`
  - `PUT /api/charges/{chargeId}`
  - `POST /api/charges/{chargeId}/approve`
  - `POST /api/charges/{chargeId}/reject`
  - `DELETE /api/charges/{chargeId}`

## Guardrails / Non-goals
- No bulk creation/import in this change
- No recurring charge templates
- No notification automation (SMS/Email/WhatsApp)
