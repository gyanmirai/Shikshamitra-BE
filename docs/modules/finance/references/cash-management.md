---
module: finance
domain: cash-management
content_type: Reference
status: active
updated_on: 2026-10-27
owner: product-and-engineering
audience: humans-and-agents
---

# Cash collection, approval, expense and bank deposit

Cash Management owns the custody balance for newly accepted cash. It does not change the meaning of an official receipt or the student's paid allocations. Historical receipts and confirmed register entries are not replayed into new cash balances.

## Operational contract

| Action | Receipt/student effect | Register effect | Cash effect |
| --- | --- | --- | --- |
| Accept cash fee/charge payment | Issue the existing official receipt; settle allocations immediately | Create unconfirmed category Income rows | Create one pending cash source per instrument |
| Record manual cash Income | No student receipt/dues mutation | Keep the manual entry unconfirmed | Create a linked pending source |
| Add paid cash expense | No student payment mutation | One paid Expense through the existing owning action | Consume the selected pending source balance |
| Approve cash and confirm deposit | Keep original receipt and allocations | Confirm only the deposited remainder as Income with the selected receiving bank | Close held cash to zero; record an immediately credited deposit when positive |
| Dispatch legacy deposit | No receipt mutation | Preserve prior Income/Expense | Move an existing approved-held legacy amount to transit |
| Confirm legacy bank credit | No receipt mutation | Preserve prior Income/Expense | Move legacy transit to confirmed deposits |

Approval is final confirmation of the deposit into the selected receiving bank. The counted amount must equal the current remaining cash after expenses, including zero for a fully spent source, and the school bank must be active. Ordinary users need a checker different from the collector. Administrators may approve their own collections, as requested on 27 October 2026; current tenant-scoped database membership in the configured administrator roles controls this exception through `IUserScopeService.IsAdminUserAsync`. The exact cash Approve permission remains mandatory, and client role claims do not confer the exception. List/detail `CanApprove` and the approval command apply the same rule. The separate legacy bank-credit reviewer still differs from the depositor. Existing approved-held and in-transit history retains partial deposits and credits; unmatched legacy credit remains in transit. New approvals need no further deposit command.

For each source, `collected = spent + confirmed deposits + in transit + remaining cash`. Only pending cash with a positive remaining balance can fund a new expense. Approved sources cannot fund new expenses. Approval finalizes unconfirmed Income at the deposited net amount while leaving original receipts and previously confirmed Income unchanged. For example, collection 10000 and expense 2000 produce deposit/Income 8000, expense 2000, original receipt 10000 and held cash 0. This explicitly selected deposit-basis Income means generic Income-minus-Expense reports subtract the expense from the net Income. Expense creation records actual payment; an unpaid approval is not represented as a paid register entry. Cash-linked paid records are protected from ordinary edit/delete.

## API and authority

All routes below are relative to `/api/finance/cash-management`.

| Method/path | Exact authority | Input/result |
| --- | --- | --- |
| `GET /` and `GET /{id}` | `Finance.Accounts.CashManagement.View` | Academic year; list also accepts pagination, approval status, search, allowlisted column filters and single-column sorting; scoped balances, source and history |
| `GET /options?operation=approve` | `Finance.Accounts.CashManagement.Approve` | Eligible school bank destinations |
| `GET /options?operation=deposit` | `Finance.Accounts.CashManagement.Deposit` | Legacy approved-held sources and bank destinations |
| `GET /options?operation=expense` | `Finance.Accounts.ExpenseTracking.Add` | Accessible pending source receipts with positive remaining amount and stamp |
| `POST /{id}/approve` | `Finance.Accounts.CashManagement.Approve` | Year, receiving bank, current remaining count (zero permitted), notes, command key and source stamp |
| `POST /{id}/deposits` | `Finance.Accounts.CashManagement.Deposit` | Year, amount, actual date, deposit-slip reference, notes, key and stamp |
| `POST /{id}/deposits/{depositId}/confirm` | `Finance.Accounts.CashManagement.Reconcile` | Year, credited amount, actual date, bank-statement reference, notes, key and parent stamp |

Existing `POST /api/expenses` additionally requires `cashCollectionId` and `cashConcurrencyStamp` for Cash, plus the existing category/payee/payment evidence. Non-cash behavior is retained. The source picker is permitted by Expense Add; it does not require bank-settings administration or Cash Management View.

The list accepts `filters` as the shared Finance table JSON (`column`, `type`, `value`), plus `sortBy` and `sortOrder=asc|desc`. Supported fields are `reference`, `payerName`, `bankLabel`, `amount`, `spentAmount`, `depositedAmount`, `inTransitAmount`, `remainingAmount`, `collectedAt`, and `status`. Filtering and stable sorting run in the scoped database query before counting and paging; remaining cash predicates use the stored balance facts. Unknown fields/operators are rejected. `statusCounts` contains `all`, `pending`, and `approved` record counts after search/column criteria and before the top-level status selection; monetary `summary` remains scoped to the selected status and all criteria.

`GET /api/finance-routing-options?operation=cash-expense` supplies active expense categories under the same Expense Add authority and accessible school year. It returns no bank accounts. This category lookup supports scoped admission fee accounts before student enrollment; the source picker and payment action still enforce the original payer scope.

New grants are not automatically assigned to existing users or roles. Tenant/year and original payer/student/fee-account scope apply to every read, summary, option and mutation. Pending Income is not available through direct register IDs or receipt fallback; official documents remain available through their own receipt permission.

## Persistence and retries

`CashCollection` owns its original payment-instrument link, exact Income source prefix/manual transaction ID, immutable deposit/credit/expense records, actors, dates and replay evidence. New amounts use decimal precision of two fractional digits; display rounding does not change amounts. Pending summary totals use remaining cash; approved totals retain original collection amounts, with spent/deposited/transit shown separately. Multiple pending Income rows receive a proportional net allocation by largest fractional-paise remainder, with source key and transaction ID as deterministic tie-breaks. Each row preserves its original gross amount in `ExtraProperties.OriginalCashGrossAmount`, category/source/payer identity and original payment date. Original manual creation requests continue to replay against that gross evidence after net finalization. A zero share retains audit evidence with amount 0 and `IsConfirmed=false`, intentionally suppressing it from Income register/list/detail/totals; the approved cash source is nevertheless finalized. Zero approval creates no deposit record. Generic non-cash confirmation excludes cash rows.

Commands use source concurrency stamps and transactional source locks on PostgreSQL. Actual paid expense creation shares the same source allocation lock. ABP owns stamp rotation. Commands first authorize the scoped source, then load its tracked graph in the same locked transaction; readonly scope subqueries must not make new owned deposits or credits untracked. Replay evidence is appended atomically without changing the saved response stamp. Exact retries return stored results; changed payloads with an existing key fail. Generic non-cash reconciliation cannot release pending cash Income. Additive migrations preserve existing confirmed rows and create no opening or historical cash by inference.

Finance payer access follows `StudentRecordScope` wildcard boundaries: ancestor board, medium and stage IDs on a class assignment do not authorize other classes. Actual cash timestamps are converted to UTC instants, rather than relabelling server-local values as UTC.

Deposit-slip and bank-statement references are evidence fields in this simplified flow; this delivery does not add file-upload or bank-statement import capabilities. Handovers, daily count, staff advances and a cash-account catalogue are outside this flow.

## Verification

`CashCollection_Tests` covers transitions, amounts, conservation and immutability. `SimpleCashFlowApi_Tests` exercises real HTTP collection, pending paid expense, net final approval and replay, partial/zero manual Income finalization with original-gross retries, exact fractional multi-row allocation, and separately seeded legacy deposit/credit history. `PendingCashRegisterApi_Tests` checks Income receipt-fallback denial while the original receipt remains available. `CashManagementAuthorizationApi_Tests` checks exact action grants and option ownership.

Frontend route and UI behavior: [Cash Management frontend](../../../../../Shikshamitra-FE/docs/finance/references/cash-management.md). Release verification results belong in the owning [testing page](../testing.md).

Production deployment remains separate from this implementation. The design factory uses modern Npgsql timestamps while the existing runtime module enables legacy timestamps. An isolated design-model match does not clear that preexisting deployment-path difference; validate the actual migrator/provider configuration before applying the migration. No production migration or PostgreSQL allocation race has been executed for this delivery.
