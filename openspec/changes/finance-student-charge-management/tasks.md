# Tasks: Student Charge Management

- [x] **Task 1 — FluentValidation validators** — Add `CreateChargeRequestValidator`, `UpdateChargeRequestValidator`, `ApproveChargeRequestValidator`, `RejectChargeRequestValidator` in `Application.Contracts/Finance/Fees/Charges/`
- [x] **Task 2 — Permissions** — Extend `ShikshaMitraSubmodulePermissions.Finance.Fees_Charges` with `Update`, `Approve`, `Reject` constants and `DisplayNames`
- [x] **Task 3 — Error codes** — Add `FinanceErrorCodes` class in `Domain.Shared` with `Charge.*` constants and replace string literals in the domain
- [x] **Task 4 — ChargeManager** — Introduce `ChargeManager` (Domain service) encapsulating `Create`, `ApprovePending`, `RejectPending` with invariant checks
- [x] **Task 5 — ChargeAppService refactor** — Derive Board/Medium/Standard/Division from StudentId; validate ChargeCategory is active; add `[Authorize]` attributes for Update/Approve/Reject; enforce maker-checker on Approve/Reject
- [x] **Task 6 — List API optimization** — Implement allow-listed sorting, cap page size (max 100), optimize status counts with a grouped query
- [x] **Task 7 — HttpApi controller alignment** — Add `[ProducesResponseType]` attributes, page size cap constant, thin delegation
- [x] **Task 8 — FeeCollection integration fix** — Filter `c.Status == ChargeStatus.Approved` in dues and overdue calculations in `FeeCollectionAppService`
- [x] **Task 9 — Tests** — Add `ChargeAppService_Tests` covering: auto-approved vs pending, pending-only update/delete, maker-checker, fee collection approved-only
- [x] **Task 10 — Documentation** — Add `docs/modules/finance/README.md` with charge management API notes
