---
module: finance
domain: accounts
content_type: Reference
status: active
updated_on: 2026-10-24
owner: backend-team
audience: humans-and-agents
---

# Route Fees income, refund expenses, and school bank accounts

Account Heads are retired from active account settings. Fee Heads remain, and each Fee Head requires an Income Category. Historical Account Head references and journal evidence remain in storage.

## Configuration and payment flow

| Action | Saved selection | Financial effect |
|---|---|---|
| Create an Income or Expense Category | Academic year and category type | Independent master; no Account Head required |
| Create a Bank Account | School account details | Independent master; no Account Head required |
| Create or edit a pending charge | Charge Category | Backend snapshots its Income Category on the request and student charges; no income yet |
| Approve charge lines | Revalidate the saved active Income Category | Charge becomes collectible; category routing remains fixed |
| Activate a Collection Policy with late fees | Required Late Fee Income Category | Pinned with the policy version |
| Activate a Refund Policy | Required Expense Category; default paying bank if any non-cash method is allowed | Defaults are pinned in the refund policy snapshot |
| Collect fees or charges | Actual school bank on each instrument | Confirmed payment creates Income; deferred payment stores hidden routing snapshots |
| Approve a refund | Optional approval notes | Reserves refundable allocations; creates no Expense |
| Record a refund payout | Expense Category, source school bank, method, date and non-cash reference | Completes the refund and creates Expense exactly once in the same transaction |
| Return a confirmed refund payout | Evidence and reason | Creates an opposite signed Expense correction |

Cash fee and charge collections may omit a bank. Non-cash instruments require an active school bank selected for the payment. Charges carry no receiving-bank restriction.

Refund payment recording validates the selected active, effective, tenant/year expense category and bank for every method. Policy defaults prefill the dialog; approval does not collect routing. The saved request method stays fixed. Existing pending payments retain their saved bank, reference and date while completing through the same payment dialog. See the [refund payout workflow](./fee-refund-payout-workflow.md) for compatibility endpoints and transaction guarantees.

## Contracts and access

`GET /api/finance-routing-options?academicYearId=...&operation=...` supplies active, effective, tenant-scoped categories and masked school-bank labels. Supported operations are `collection`, `refund`, `refund-policy`, `collection-policy`, `fee-head`, and `charge-category`. Each operation checks its owning write/approval permission; a setting module or unrelated action grant does not grant access. The response contains `incomeCategories`, `expenseCategories`, and `bankAccounts`, with `id` and `label` options.

Charge create/update DTOs include `chargeCategoryId`; the backend resolves its Income Category mapping. Collection instruments include `bankAccountId`. Collection policies include `lateFeeIncomeCategoryId`. Refund policies include `expenseCategoryId` and `defaultPayingBankAccountId`. Refund payout recording requires `expenseCategoryId` and `bankAccountId`; the approval dialog collects optional notes.

`/api/account-heads` returns HTTP 410 for authenticated requests. Legacy category head lookups return no options. Historical head fields are compatibility data rather than inputs to new financial routing.

## Accounting integrity

`Finance_Transactions` stores automatic category entries with unique `SourceKey`, `SourceId`, student, category-name snapshot, bank snapshot, and confirmation state. These are operational Income/Expense records. General Accounts journal posting remains unavailable; the register continues to label journal status as `Not posted`.

`FinanceIncomeAllocator` attributes each cent to a category and instrument in stable source order. It rejects nonpositive values, more than two decimal places, and unequal totals. Collection retries retain the existing command fingerprint and serializable unit of work. Automatic entry source keys have a unique database index. Generic manual-entry update and delete operations reject automatic entries, and student scope applies to automatic entry reads.

Reconciliation confirms the existing hidden income rows. Rejected deferred payments never become Income. The existing collection validator rejects deferred transactions with multiple instruments or both fee and charge receipts: receipt-level reconciliation cannot safely decide such payments. Confirmed split payments remain supported.

The separate Income and Expenses registers exclude receipt/refund headers when automatic category entries exist, preventing duplicate amounts. Manual entries and historical source activity remain visible.

## Migration and existing records

Apply `IncomeExpenseBankRouting` before deploying code that reads the new columns. The migration adds nullable routing fields, bounded snapshots, restrictive foreign keys, and the unique source index. Existing manual transactions default to confirmed. No historical tables or columns are dropped, and no bank or category is guessed for existing records.

Review unmapped pending charges and active policies before processing new payments. Editing an existing pending charge can set its missing category and bank. Published policy versions remain immutable; create a valid new version for future assignments instead of rewriting historical policy JSON. Existing completed refunds without category snapshots retain their legacy refund subledger presentation; they are not retrospectively recategorized as new expenses.

## Validation

Run the Finance domain suite, `AccountSettingsPermissionApi_Tests`, `AccountSettingsSimplificationApi_Tests`, and collection input validators. `IncomeExpenseRouting_Tests` covers cent conservation for split payments, invalid amounts, pending recognition, immutable automatic entries, and signed compensations. The API tests cover independent category/bank creation, owning-action lookup permissions, retired endpoints, and non-destructive migration operations.

PostgreSQL migration application, payout replay/concurrency, and authenticated browser payment scenarios must be verified in a disposable test environment before release. Never use a production collection or payout as a smoke test.

Frontend controls and route details: [frontend routing reference](../../../../../Shikshamitra-FE/docs/finance/references/income-expense-routing.md).

Approved refunds remain in Refunds until payment is recorded. Expenses shows paid refunds and linked returned-payment corrections, with no unpaid request projection.
See [the workspace contract](./income-expense-workspaces.md) for direction scopes and selected-record details.
