# Simple Cash Management design

Approved for implementation by the user on 26 October 2026, after simplifying the original PDF proposal.

## Scope

One new submenu: Finance / Accounts / Cash Management. One table with Pending/Approved filters, receipt details, approved bank destination, amount collected, spent, deposited, in transit and remaining cash. Approval and deposit progress are independent. No Handovers, Daily Cash Count, Staff Advances, Cash Account setup or other new submenu.

Cash collection immediately issues the existing official receipt and settles allocated student fees/charges. It atomically creates pending cash source records and unconfirmed category Income entries. Cash Management approval by an independent authorized checker requires an existing active school bank account and the full counted amount. Approval confirms the existing category rows exactly once; it does not reissue the receipt, change student dues or assert bank credit.

Approved collected cash can directly fund actual payments through existing Add Expense. The expense selects a cash source receipt with available balance. This serves as its linked cash funding source without introducing a separate custodian/account setup workflow. Existing Add Expense records an actual paid expense, not an unpaid draft; its action remains the owning Expense Add authority. Cash-linked expenses are immutable paid history and cannot be generically edited/deleted. Category, payee, date, amount and payment reference remain mandatory/validated as appropriate. Amounts are illustrative, never school policy limits.

Deposit to bank is a row action in Cash Management: select amount, actual date and slip/reference against the row's approved bank. Dispatch moves cash held to transit. A separately authorized independent reviewer confirms actual credited amount/date/statement reference from the same screen. Partial credit retains remaining transit. Deposits never generate Income or Expenses. No bank matching submenu is added.

## Integrity

Per cash source: collected = spent + confirmed deposits + deposit transit + held. Held includes unapproved cash; spendable is zero until approved. Never spend/deposit more than available. Approval preserves full Income even when cash subsequently pays expenses. Deposit credits cannot exceed transit. Original receipt stays unchanged. Source keys, command fingerprints and concurrency stamps protect retries and simultaneous expense/deposit allocations; writes are transactional. Amounts are decimal(18,2), INR; actual timestamps use DateTimeOffset. Preserve whole-rupee display rules without changing stored values.

Original collection IDs, payment instrument identity, receipt reference, payer scope, original date, actor and approval evidence are retained. New sources have unique tenant/source keys. Existing historical receipts/Income remain unchanged; do not replay or import historical cash. Existing new manual cash Income must also route through pending Cash Management rather than bypass approval. Non-cash methods and reconciliation stay unchanged. Pending cash must be inaccessible through Income list/detail/fallback/totals/export, while official Receipts remains accessible.

## Authorization

Backend constants are canonical. New family Finance.Accounts.CashManagement with View, Approve, Deposit, Reconcile. No automatic grant to existing users/roles. Approve cannot be performed by source creator; Reconcile cannot be performed by depositor. Same tenant/year/payer scope predicates protect lists/counts/detail/actions; payment-source lookups require existing Expense Add and only return accessible approved positive balances. Approval/deposit bank lookups require the exact owning action. UI menu/route/actions match. Existing compact grouped permission editor is preserved.

## UI

Use the common FE skills, DESIGN.md, CustomBreadcrumbs, CustomizableTable, row-open detail drawer, CustomDialog/DialogScrollContent, ConfirmDialog, NumericFormatCustom, Formik/Yup, shared toast/icons/date/currency, shared RTK Query injection and exact permission guards. Preserve loading/empty/error/forbidden, keyboard/focus, dirty-close, saving and responsive states. Add Expense selects Cash source when Cash is selected; no second school cash account catalogue is introduced. Collection success explains pending Income approval. Cache invalidation refreshes Cash Management and affected registers after each mutation.

## Verification

Cover immediate official receipt and fee settlement with pending Income; independent approval requiring active bank and matching count; exact retry/no duplicate Income; direct expenses and balance conservation; overdraft/concurrent allocation; partial deposit and partial independent credit; wrong tenant/year/payer/permission denial; legacy read preservation and all non-cash regression. Test API persistence, migration, frontend types/forms/permissions and in-app browser. No production financial writes or merge/deployment is authorized.
