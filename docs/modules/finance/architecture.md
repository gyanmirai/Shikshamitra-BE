---
module: finance
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-11-18
owner: backend-team
audience: humans-and-agents
---

# Locate Finance backend code

TL;DR: Finance is split into Fees, Accounts, Audit, Reports, and shared helpers.
Start in contracts for DTO shape, then trace to application services and domain managers.

Fee Structure configuration, publication and revision contracts: [workspace implementation](./references/fee-structure-workspace.md).

Admissions collections use one Finance account and canonical due schedule through real Student linking and enrollment assignment: [admission fee collection](./references/admission-fee-collection.md).

`AdmissionFeeCollectionAppService` resolves fee calculation, collection forms, payment execution, late-fee balances and collection policies through ABP's request-local `LazyServiceProvider` only in operations that use them. Finalization and activation handoffs keep their existing permissions, scope, transaction and evidence-linking checks without constructing those unrelated graphs. Keep these dependencies lazy; eager construction adds first-use repository/proxy generation to the admission request even when no fee account exists.

The handoff's existing authorization and account lookup run before collection-context and ledger activation. Those repositories and the context reader are also lazy, so an admission without an account returns without generating due, payment, allocation, receipt, method or fee-definition proxies. Prepared-account operations still call the same Finance owners, with unchanged calculations, immutable evidence, linkage guards and transaction boundaries.

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

## Accounts read boundaries

Accounts settings enrich a selected page with batched downstream-reference and account-head reads.
Write operations still revalidate usage, scope, effective dates and concurrency against authoritative records.
Routing lookups project option fields through `IFinanceRoutingLookupReader`; PostgreSQL applies effective windows in SQL, while SQLite retains a scalar compatibility fallback.

Cash balances and dated collection/expense/credit totals use `ICashReadQueries`.
PostgreSQL aggregates exact decimal facts and applies date predicates before paging; SQLite retains narrow fallbacks for unsupported decimal/time operations.
`CashManagementReadProjection` loads only row scalars and the deposit identity/date metadata needed to resolve saved bank labels.
Expense, credit and command replay graphs belong to selected details and writes.

Income/expense registers compose authorized transaction, receipt and refund sources before count, order and page selection.
`IAccountsRegisterEvidenceQuery` supplies scalar counterparty JSON and immutable refund instants without loading full transaction evidence.
The refund bridge normalizes UTC according to the runtime DateTime mapping; SQLite retains exact legacy timestamp handling.
Reconciliation pushes scalar receipt headers into SQL and evaluates rendered legacy instrument fields in bounded candidate batches; only page IDs receive full presentation enrichment.

Bank activity reads method totals and row count together, then reads the selected page and category options in the same repeatable-read snapshot.
Exports read their complete capped result once and calculate matching totals from that same result.
Settings history counts all entity changes and reads descending database pages, loading only the page's approved field changes and actors.
See [frontend clients](../../../../Shikshamitra-FE/docs/finance/clients.md) for cache and history navigation behavior.
