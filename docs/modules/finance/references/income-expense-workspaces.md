---
module: finance
domain: accounts-workspaces
content_type: Reference
status: canonical
updated_on: 2026-10-24
owner: backend-team
audience: humans-and-agents
---

# Income and Expenses register contract

TL;DR: Accounts exposes separate income/expense read scopes, academic-year month filtering, selected-record details and routed manual creation while preserving canonical collection/refund money.
This contract was written before implementation on 14 October 2026.
Frontend ownership is documented in [the frontend workspace contract](../../../../../Shikshamitra-FE/docs/finance/references/income-expense-workspaces.md).

## Flow and ownership

| Concern | Contract |
|---|---|
| Outcome / owner | Accountants inspect money by month and create non-student manual entries; Accounts owns transactions; Fees owns collection/refund lifecycle |
| Scope | Current tenant, selected academic year, server academic access and scoped student query |
| Actors | Exact existing Accounts IncomeTracking.View/Add and ExpenseTracking.View/Add permissions; no new role grants or seeding |
| Read pipeline | Academic access -> direction/source/month/category/search -> stable date/ID ordering -> count -> pagination |
| API | Extend GET `/api/accounts/register` with direction, calendar month/year and category; add GET `/api/accounts/register/{source}/{id}` with academicYearId |
| Legacy compatibility | Existing direction-less register queries retain existing permission semantics; new direction scopes require their exact Income/Expense View grant |
| Detail | Enforce the same direction/source permission, tenant, academic year, confirmed/awaiting state and student scope as the list; never return another category's transactions |
| Manual create | Existing POST `/api/incomes` and `/api/expenses`; bank/reference fields follow the payment method. Cash requires a UUID idempotencyKey; cash Expense requires a pending cashCollectionId and current cashConcurrencyStamp. Validate active/effective category, method, money and academic date |
| Preconditions | Accessible year, active category of the matching type, non-cash eligible school bank, positive decimal amount with at most two decimals |
| Side effects | One manual transaction; automatic monetary entries remain owned by collection/refund services; existing audit/UOW and cache consumers remain |
| Failures | Invalid filters/foreign IDs fail safely; scope failure returns no list rows or denies detail; failed validation creates no transaction |
| Corrections | Read-only workspace; automatic rows remain immutable; existing refund reversal/return semantics remain |

## Time and field meanings

Month/year must be supplied together and intersect the server-owned academic-year dates.
Use the application's India calendar for month boundaries and half-open UTC instant ranges; clip boundary months to the academic period.
No month means the selected academic year's complete date interval.
Collections use recorded payment date, manual entries use payment date, and paid refunds use payout/posting date.
Covered fee periods are detail facts and do not change payment-month filtering.
Bank labels are masked snapshots; category labels retain historical snapshots when available.
Amounts remain decimal with their canonical precision; formatting never changes payload values.
Unavailable legacy facts remain unavailable.

## Monetary and lifecycle invariants

Verified non-cash collections appear in Income; provisional/rejected collections do not.
Cash collections and manual cash Income stay hidden while pending.
Pending cash may fund actual paid expenses; final approval deposits and recognizes only the remaining net amount.
For collection 10000 and paid expense 2000, Income is8000 and Expense remains2000; the official receipt remains 10000.
Fully spent cash finalizes with count 0 and no visible zero Income row or deposit.
Previously confirmed Income and legacy deposit/credit history retain their amounts.
See [Cash Management](./cash-management.md) for original-gross audit evidence, exact retries and final-deposit authority.
Approved/Processing refunds without paid accounting evidence are excluded from Expenses and remain in Refunds.
Refund approval does not create an expense or reduce bank balance.
Recording payment creates the expense exactly once using the selected category, bank and method. It appears as Paid immediately; linked returns appear as Returned.
Positive expense values represent outward money; signed returned-payout corrections remain separate linked evidence.
Automatic category/instrument allocations must retain their exact totals and must not be counted again through receipt/refund header fallbacks.
Manual bank/reference facts use existing transaction fields so no schema migration is required unless source inspection proves otherwise.
Do not change unrelated legacy Update/Delete contracts or maker-checker rules.

## Verification and rollout

Cover exact action denials, tenant/student/year isolation, month clipping/timezone boundaries, source/category/search paging, direct detail IDOR and duplicate suppression.
Cover manual cash/non-cash category/bank validation and decimal payload persistence.
Reuse refund routing/confirmation/retry/returned-payment regression tests and verify unpaid requests are absent from register counts and rows.
Run focused domain/API tests, backend build, docs checks and git diff --check.
Ship backend contracts before frontend clients; old register routes remain compatible during rollout.

Manual creation binds the client UUID idempotency key to the transaction primary key.
The existing database primary-key uniqueness prevents concurrent duplicate inserts without a schema migration.
Matching retries return the existing scoped entry; changed payloads or an automatic entry with that key fail with Finance:ManualEntryKeyConflict.
A losing concurrent insert may fail and is safely retried using the same key after the winning transaction commits.
Historical refund fallback rows are filtered by tenant, year and student in SQL, then by date/search/category on the server before global counting and pagination for SQLite/PostgreSQL parity.

Manual creation accepts paymentDate as DateTimeOffset and persists its UTC instant in the existing transaction date column.
Offsets never change the selected India calendar payment day.

Register sorting accepts reference, categoryName, bankLabel, amount, occurredAt and sourceStatus with sortDescending.
Apply the requested ordering before taking each source window and before global paging; ID is the stable tie-breaker.

The register also returns `grNumber`, selected-academic-year `classDivision`, and a separate manual `counterparty`. Student placement resolves the selected year's latest active enrollment, with the existing same-year Student fallback. Detail fields remain compatible through the shared row contract.

`filters` accepts the shared Finance table JSON operators for reference, studentIdentity (name plus GR), classDivision, counterparty, source, categoryName, bankLabel, amount, occurredAt and sourceStatus. Source/status choices are validated for the requested direction. Alternatives within one column are ORed; different columns are ANDed before counts and pagination. Counterparty evidence is read from scoped manual JSON only when that field is filtered or searched. Student identity and Class/Division sorting are supported; Counterparty and Source sorting are rejected. No monetary or lifecycle rules change.

Amount ordering uses a provider-compatible numeric cast; stored and returned amounts remain decimal.

## Local verification — 14 October 2026

The backend Application/HttpApi/Host projects built successfully through the focused API test build.
`AccountsRegisterPeriod_Tests` plus `IncomeExpenseRouting_Tests`: 16 passed.
`AccountsRegisterApi_Tests`: 7 passed, including exact direction denials, owner Add lookups, scoped manual details, month/search/sorting across pages, matching UUID replay and approval/paid/returned projections.
These API fixtures use isolated SQLite state; payout commands and PostgreSQL transaction concurrency were not executed because the required disposable refund test database was not configured.
`git diff --check` passed. The Finance docs validator reports an existing encoding issue in `references/charge-management-e2e-test-plan.md`; it reports no workspace-contract issue.
