# Pending cash expenses and net deposit approval

User correction on 27 October 2026 supersedes the original approve-before-spend flow. The user explicitly selected Income showing only the deposited remainder, with expenses recorded separately, after the gross-versus-net reporting consequence was explained.

## Required flow

- Student pays 10000 cash: final receipt and settled dues stay 10000. Cash Management is Pending with held cash10000. No Income is visible yet.
- Existing Add Expense may spend from a pending receipt. A paid stationery expense2000 is visible immediately in Expenses and linked to that receipt. Held/pending cash becomes8000. Approved receipts are unavailable for new cash expenses.
- Approval is the final deposit confirmation. Show original collection10000, expenses2000 and remaining/deposit8000. Select an active school bank and enter the remaining counted amount. The submitted count must equal the current remaining balance, not the original collection.
- Approval atomically confirms bank deposit8000, closes pending held cash to0, and confirms only8000 as Income. Original receipt evidence and linked expense2000 remain unchanged. No additional deposit/credit step is required for newly approved receipts.
- Preserve existing independent bank-credit commands for legacy records already in transit; do not reinterpret prior posted history.
- Administrators with the exact Approve permission may approve their own receipts using current tenant database role membership. Other users retain the different-reviewer rule.

## Money and edge cases

- Conservation: original collected = actual linked expenses + confirmed bank deposits + legacy transit + remaining cash.
- Keep original collection and spent totals visible for audit. Pending summary is remaining cash. Income is explicitly the deposited remainder in this product flow; generic Income-minus-Expense reports will reflect this requested deposit-basis classification.
- Multiple original pending Income entries must allocate the net deposit deterministically to two decimals without losing/duplicating paise. Each allocation stays between zero and its original gross amount and all shares sum exactly to the deposited remainder, with stable tie-breaking (including four gross0.01 rows/net0.02). Preserve original gross values as audit metadata before changing unconfirmed entries. Preserve immutable receipts, student/account/category linkage and exact original manual-income creation retries after approval; reject a new-net amount submitted as an original creation request.
- Support fully spent receipts: remaining/count0 may be finalized; produce no positive Income or bank deposit, and avoid a misleading zero-value Income register row. Reject negative, excess, stale, invalid-date, wrong-tenant/year/scope and double-spending commands. Approved cash cannot fund a new expense.
- Approval/expense races use the existing source lock, serializable transaction and stamp. Approval replay must not duplicate Income or deposit evidence. Exercise separate units of work with overlapping expense/approval when supported by the isolated test provider; a losing command must leave no orphan Expense, Income or deposit evidence.
- Legacy approved-but-still-held records and legacy in-transit deposits retain their already confirmed Income and permitted deposit/credit workflow. Never apply new net finalization to their prior confirmed history.

## Interfaces and UI

Keep existing API routes and CashDetail fields. During pending, `amount` is original collected, `spentAmount` is paid expenses, and `remainingAmount` is the amount to approve/deposit. After approval `depositedAmount` includes the confirmed remainder and `remainingAmount` is0. Expense options return only pending sources with a positive remaining balance; legacy deposit options retain their approved-held behavior.

Use existing shared Finance forms, numeric inputs, currency formatting, details sections and dialogs. Approval shows Collected amount, Expenses paid and Remaining to deposit. Count is initially populated with remaining cash, including zero, and bank label describes the receiving bank. Preserve dirty forms, stale refresh, uncertain exact retry, permissions and cache invalidation. Replace approve-before-expense text throughout affected cash surfaces.

## Delivery constraints

Preserve unrelated changes in both sibling repositories. No production writes, new grants, automatic approvals of real receipts, receipt rewrites, migration application, commit or push. Local runtime must use a coherent complete set of assemblies from one successful build; never overlay selected DLLs from different builds. Focused verification may omit historical migration designers to fit memory, but must retain runtime mappings and new cash metadata and must not be used for database migration.
