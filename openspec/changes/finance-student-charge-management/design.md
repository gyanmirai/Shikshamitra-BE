# Design: Student Charge Management (ABP + DDD)

## Context (what already exists)
The repo already contains a vertical slice:
- Domain: `Finance/Fees/Charges/Charge` (aggregate) + `ChargeStatus`, `ChargePriority`
- EF: `ChargeConfiguration` with basic indexes
- Application: `ChargeAppService`
- HTTP API: `ChargeController`
- Settings: `ChargeCategory` (with `IsApprovalRequired`, `IsActive`) and controller/appservice

This design focuses on elevating code structure/quality to match the Employees module patterns.

---

## Domain model
### Aggregate: Charge
Key fields (persisted snapshot):
- `AcademicYearId`, `ChargeCategoryId`, `StudentId`
- Snapshot of student academic scope: `BoardId`, `EducationMediumId`, `StandardId`, `DivisionId`
- `PurposeDescription`, `Amount`, `DueDate`, `Priority`
- Lifecycle: `Status` (Pending/Approved/Rejected)
- Audit: `RequestorId`, `ApprovedBy`, `ApprovedOn`, `ApprovalNotes`, `AppliedOn`

### State machine
```
Pending  --approve--> Approved
Pending  --reject-->  Rejected

Only Pending: Update/Delete/Approve/Reject
Maker-checker: ApproverId != RequestorId
```

### Domain services / managers
Introduce a `ChargeManager` (Domain) similar in spirit to `EmployeeManager` / `ConcessionRequestManager`:
- `Create(...)` (validates invariants, sets AppliedOn, initial status)
- `UpdatePending(...)`
- `ApprovePending(...)`
- `RejectPending(...)`

Cross-aggregate lookups (Student, Category, AcademicYear) remain in Application layer; manager only receives validated inputs.

### Error codes
Replace string literals like `"ShikshaMitra:InvalidDueDate"` with constants in `Domain.Shared` (e.g., `FinanceErrorCodes.Charge.*`). This matches HR’s `HRErrorCodes` approach.

---

## Application layer
### Authorization & permissions
Extend Finance → Fees → Charges permissions beyond View/Add/Delete:
- `Update`
- `Approve`
- `Reject`

Enforcement points:
- AppService methods: `[Authorize(...)]` at coarse-grain + IDOR/maker-checker checks in method body.
- Approve/Reject: require `Approve/Reject` permissions and ensure `CurrentUserId != charge.RequestorId`.

### Input validation
Add FluentValidation validators in Application.Contracts:
- `CreateChargeRequestValidator`
- `UpdateChargeRequestValidator`
- `ApproveChargeRequestValidator`
- `RejectChargeRequestValidator`

Rules:
- Amount > 0
- DueDate >= today (UTC) and preferably normalize to date
- PurposeDescription min length (>= 10) and max length (<= 1000)
- Rejection reason min length (>= 10)

### Student scope derivation (anti-tamper)
On create/update (if scope is mutable at all), derive:
- BoardId, MediumId, StandardId, DivisionId
from the Student record.

Contract strategy:
- Preferred: remove these fields from Create request.
- Back-compat: keep them optional; if provided and mismatch student’s derived scope → 400.

### Query performance and scalability
Charge listing should avoid heavy joins on every record if not needed.
Patterns to follow (see `ConcessionRequestApprovalAppService`):
- Project IDs for the page first (server-side paging)
- Batch-load related data (students/categories/standards/divisions/users) by IDs
- Build DTOs in memory

Counts:
- Per-status counts should be computed independently of the active status filter.
- Prefer a single grouped query (GROUP BY Status) when possible.

Sorting:
- Implement a strict allow-list of sortable fields (e.g., `AppliedOn`, `DueDate`, `Amount`, `Priority`).
- Default: newest-first by `AppliedOn`.

Page size caps:
- Set maximum page sizes in controller (pattern from Employees).

---

## HTTP API layer
Controllers stay thin.
- Keep routes under `api/charges`.
- Add proper `[ProducesResponseType]` for contract clarity.

---

## Integration: Fee Collection
Business rule: **only Approved charges** contribute to totals/overdue.
Update `FeeCollectionAppService` queries:
- When listing charges for dues, filter `c.Status == ChargeStatus.Approved`.
- Overdue logic uses Approved charges only.

---

## Data & indexing
Existing indexes: `(AcademicYearId, Status)`, `StudentId`, `ChargeCategoryId`, `DueDate`.
Potential additions (measure-driven):
- `(AcademicYearId, StudentId)` for student detail screens
- `(AcademicYearId, BoardId, EducationMediumId, Status)` for admin filters

---

## Testing
Add tests similar to HR patterns:
- Charge creation sets correct initial status based on category approval requirement
- Pending-only update/delete enforced
- Approve/Reject enforce maker-checker + permissions
- FeeCollection includes only Approved charges in totals
