---
module: finance
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-10-28
owner: backend-team
audience: humans-and-agents
---

# Locate Finance backend code

TL;DR: Finance is split into Fees, Accounts, Audit, Reports, and shared helpers.
Start in contracts for DTO shape, then trace to application services and domain managers.

Fee Structure configuration, publication and revision contracts: [workspace implementation](./references/fee-structure-workspace.md).

Admissions collections use one Finance account and canonical due schedule through real Student linking and enrollment assignment: [admission fee collection](./references/admission-fee-collection.md).

## Layer map

Proration integration uses `AdmissionProrationPolicyResolver` for the Finance year default, `AdmissionJoiningDateReader` for the Registration fact, assignment preparation for canonical calculation, and `WithdrawalProrationService` for withdrawal projection/application/correction. `WithdrawalProrationAllocation` and named `DueFee` transitions enforce credit invariants. `FinanceAuditWriter` accepts a deterministic owner event ID. Existing DueFee extensions and Finance audit storage are reused; no migration is added.

| Layer | Path |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Finance/**` |
| Application | `src/ShikshaMitra.Application/Finance/**` |
| Domain | `src/ShikshaMitra.Domain/Finance/**` |
| EF Core | `src/ShikshaMitra.EntityFrameworkCore/Finance/**` |
| HTTP API | `src/ShikshaMitra.HttpApi/Controllers/Finance/**` |
| Tests | `test/ShikshaMitra.*.Tests/Finance/**` |

## Domain subsystems

| Subsystem | Source area |
|---|---|
| Fees | `Finance/Fees/**` |
| Fee settings | `Finance/Fees/Settings/**` |
| Collections and due fees | `Finance/Fees/Collections/**` |
| Charges | `Finance/Fees/Charges/**` |
| Concessions | `Finance/Fees/Concessions/**` |
| Adjustments | `Finance/Fees/Adjustments/**` |
| Refunds and receipts | `Finance/Fees/Refunds/**`, `Finance/Fees/Receipts/**` |
| Accounts | `Finance/Accounts/**` |
| Audit and reports | `Finance/Audit/**`, `Finance/Reports/**` |

## Implementation rules

| Rule | Why it matters |
|---|---|
| Keep fee balance mutations transactional | Partial financial updates corrupt dues and receipts |
| Use domain methods for due-fee mutation | Concession, payment, adjustment, and refund effects must stay auditable |
| Derive student scope server-side | Caller-supplied scope can leak or corrupt Finance data |
| Preserve maker-checker restrictions | Finance approvals must be traceable and separated from creation |
| Update report docs with calculation changes | Report accuracy is a production requirement |

## Family charge reads

`Application/Finance/Mobile/MobileFamilyChargeReader.cs` is the canonical tenant/child/year read model for approved and voided charges, owned collection allocations, effective waivers and processed/reversed refunds. It feeds `MobileFinanceReadService.Charges.cs` and the compatible approved-only legacy projection used by overview and statement. `Application.Contracts/Finance/Mobile/MobileFinanceChargeDtos.cs` owns the additive wire DTOs; `MobileFinanceController.Charges.cs` remains thin. New operations use the existing `MobileFinanceAccessService` and exact Charges View permission; receipt/refund references keep independent existing grants. There is no schema migration or read-time write.

## Category and bank routing

See [Income and expense routing](./references/income-expense-routing.md) for the current category selection, school bank, recognition, historical compatibility, and validation contracts.

## Bank account activity projection

`Application.Contracts/Finance/Accounts/BankActivity` defines the bank query, page/detail, status and export contracts.
`BankAccountActivityController` delegates to the focused `BankAccountActivityAppService`; settings CRUD is unchanged.
`IBankActivityQuerySource` keeps provider expressions below Application, and `EfCoreBankActivityQuerySource` composes PostgreSQL SQL over existing authoritative tables without a new ledger, schema change or historical backfill.
The application applies original student/admission-account scope and exact slice grants before database filters, sums, counts and paging.
Repeatable-read snapshots keep each page/detail/export internally coherent.

Canonical noncash collection category allocations group once per instrument and correction, with signed correction directions preserved.
Cash Income and cash expenses are excluded from bank movement; actual per-credit records use saved credit dates.
Historical deposit destinations are resolved only from `DepositBank:<depositId N>` or matching `BulkDeposit:<key>` JSON evidence.
The latest mutable cash-header bank is never used to assign historical deposits.
Legacy instruments/refund accounting entries are fallback evidence only when canonical automatic entries are absent; unassigned or unmapped methods remain excluded.
Legacy cash credit-date evidence is used only when individual credit records are absent.
Cash-method refunds with a selected bank remain Unverified with zero recognized movement.
Operational confirmation never implies external bank settlement.

The adapter returns composable unmapped EF read facts, so historical JSON remains queried in PostgreSQL instead of materializing tenant history.
Malformed destination JSON fails the read rather than guessing an account.
No production migration or normalization job is required by this change.
