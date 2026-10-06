---
module: finance
domain: product
content_type: Reference
status: canonical
updated_on: 2026-10-05
owner: backend-team
audience: humans-and-agents
---

# Understand Finance product behavior

TL;DR: Finance protects fee accuracy, payment history, concessions, refunds, adjustments, and account reconciliation.
Treat incorrect Finance reports or due balances as production defects.

## Product areas

| Area | Status | Behavior owned by Finance |
|---|---|---|
| Fee settings | Implemented | Fee categories, fee heads, fee structures, receipt books, charge categories, concession types, and related setup |
| Student fee assignment | Implemented | Assign fee structures to students and expose fee head details |
| Fee collection | Implemented | Collect fees, allocate payments to due rows, expose student fee details, summaries, and exports |
| Receipts | Implemented | Read receipt lists and receipt details after payment activity |
| Charges | Partial | Create ad hoc per-student charges, approve/reject them, collect approved balances, and issue charge receipts. Release-critical scope, concurrency, correction/refund, policy, accounting, and frontend-permission gaps are tracked in [Charge management end to end](./references/charge-management-end-to-end.md) |
| Concession policy and requests | Implemented | Select an active academic-year policy while creating the request and pin that version. `AllFees` permits concessions on paid installments; approval creates a submitted refund request for any resulting overpayment. See [the end-to-end reference](./references/concession-policy-end-to-end.md) |
| Fee adjustments | Implemented | Create credit pools from paid due rows and apply them to pending obligations |
| Refunds | Implemented | Request and process fee refunds separately from adjustments |
| Accounts | Implemented | Income, expense, bank account, transaction categories, transactions, and reconciliation |
| Audit and reports | Implemented | Finance audit trail and Finance report APIs |

## Data-integrity rules

| Rule | Consequence |
|---|---|
| Student academic scope is server-owned | Charge and fee workflows must not trust caller-provided class or division scope |
| Paid and pending due rows are different states | Adjustments must use paid source rows and pending target rows |
| Concession approval allocates to `DueFee` rows | Approved concession amounts must reduce the correct outstanding lines |
| Cash returned to parents uses refunds | Fee adjustments are not cash refunds |
| Fee and charge status derives from the live balance | Fee Ledger lines show Overdue for a generated bill with an unpaid remainder past its due date; otherwise an uncleared allocation shows In Reconciliation, a settled balance shows Paid, and an unsettled balance shows Partial Pending or Pending. Charges retain their independent Pending, Partial Paid, Paid, or Waived status. Concession amount and refund status are separate fields; a paid installment can still have a refund in progress. |
| Empty Collections summaries are not payments | `FeeCollectionStatus.NoFeeAssigned` (`7`, summary only) applies when `IsFeeStructureAssigned` is false and total, paid, pending and overdue fees plus total and pending charges are all zero. Existing fee or charge amounts and assigned structures keep the existing balance-based status. Summary mapping, status filtering before pagination and CSV/Excel exports use the same classifier; exports display `No Fee Assigned`. This read-only rule uses the existing tenant, academic-year, academic-scope and Collections View authorization; it changes no dues, allocations, receipts, postings or history and requires no migration. |
| Maker-checker rules protect approvals | Creators cannot approve or reject their own charge, concession, or adjustment where the domain enforces it |
| Receipt numbers and payment records must remain auditable | Do not silently mutate payment history |
| Charges are not Fee Structure demand | Use charges for exceptional obligations; connect them through collection, receipt, accounts, audit, and reports rather than duplicating recurring fees |
| Concession type names are unique per tenant and academic year after normalization | Duplicate labels cannot create ambiguous request classifications |
| Concession types are lifecycle-controlled master data | Active types cannot be deleted; inactive unused types may be deleted, while used types remain resolvable in requests, reports, and version history |
| Every policy type has an active academic-year default | Where active policies exist, Concession, Collection, Refund, and Proration each have exactly one Default per tenant and academic year. The first Active policy becomes Default; selecting another replaces it. The current Default cannot be cleared, moved to Draft, or archived until another Active policy is designated. A filtered unique index prevents duplicate Active defaults. |
| Concession evidence definitions are server-enforced | Each document declares mandatory/optional, maximum bytes, and accepted formats; only mandatory evidence blocks approval |

## Fee Ledger overdue classification

Collections summaries, student overdue totals, and the collect-form installment status share
`DueFeeCollectionStatusResolver`. Overdue requires a positive outstanding principal balance,
a generated bill, and `ScheduleDueDate` (or legacy `DueDate`) strictly before the server's as-of
date. A bill due today or an installment whose demand date has not arrived is not overdue.
An overdue installment remains Overdue after partial payment; summary overdue amounts sum only
its remaining principal. Reconciliation allocations already reduce outstanding principal, so
an overdue remainder is still collectible and a fully provisionally settled line instead shows
In Reconciliation. This is a read projection and does not change stored balances or payment history.

System late-fee charges retain their independent balance status; they do not inherit Overdue from
the principal installment. The Fee Structure settings policy options expose `IsDefault` only
for a current active version. Explicit policy IDs and versions, saved optional-policy clears,
and historical assignment snapshots remain authoritative.

## Fee-head module field

The fee-head `Module` identifies the product module associated with that fee head. It is optional: `null` means the fee head is not associated with a specific product module. When present, it must be a valid `FeeHeadModule` enum value.

Every Fee Head also owns one required same-academic-year Income Category mapping. Income Categories are independent masters; new financial routing does not require an Account Head. Create, edit, and reactivation reject inactive, cross-year, or wrong-type mappings. Once the Fee Head is used by a locked Fee Structure, its Income Category cannot change; this preserves the accounting classification of downstream dues and collections. An Income Category mapped by an active Fee Head cannot be deactivated or deleted.

Account Heads are retired from active settings. Income Categories, Expense Categories, and Bank Accounts are maintained independently. Historical head references remain preserved, and General Accounts journal posting remains unavailable. The [routing reference](./references/income-expense-routing.md) describes the implemented payment flow and rollout constraints.

## Partial surfaces

No generated Finance contract pack is canonical in this docs tree.
Update [api.md](./api.md) from current source instead of adding raw exports.

### Additional-charge balances in Fee Collection

System late fees and approved manual charges contribute to Collections `TotalCharges` and `PendingCharges` before paging, sorting, status filtering, and export. The same `LateFeeBalanceService` supplies system-charge balances to the collection form, student fee details, collection validation, and waiver commands. `TotalCharges` retains assessed amounts after payment or waiver; `PendingCharges = max(0, assessed - non-reversed payments - applied waivers)`. Principal amounts remain separate and are not increased by these charges.

Before a collection changes principal, it transactionally preserves the accrued late-fee assessment on the existing DueFee aggregate's `ExtraProperties` JSON column: decimal amount, assessment instant, pinned collection-policy version, and explanation. Assessments can increase through accrual but cannot decrease when principal is partially or fully paid, or when a later exception/exemption stops accrual. Paying fee items never allocates money to unselected charges. A remaining late fee is independently collectible or waivable after its principal is paid. Reads do not write assessments or generate receipts.

For payments made before assessment preservation existed, the service recovers late fees from the immutable successful collection response's `CollectedAt` and `AuditSummary.AllocationLines.PendingBefore`, applying the assignment's pinned policy and the exception window covering that payment date. It never accrues a settled principal through today's date. Recovery is also skipped when assignment policy history shows a collection-policy version change after the payment, or when student override audit evidence shows an exemption/grace override created, modified, or deleted after that payment. Recovery is also skipped for a covering exception window created, modified, or deleted after payment; backdated windows cannot rewrite historical debt. Override and exception-window evidence are loaded in scoped batches, including soft-deleted rows while retaining tenant filtering. Applying newer rules would rewrite historical amounts. Records without trustworthy amount/date/version evidence cannot be safely reconstructed automatically; they require a reviewed correction rather than an invented balance. Assignments without a pinned collection-policy version cannot generate new policy-owned late fees (the collect form rejects the incomplete policy assignment); existing assessment/payment/waiver evidence remains included in summary balances. No schema migration is required.

## Category and bank routing

See [Income and expense routing](./references/income-expense-routing.md) for the current category selection, school bank, recognition, historical compatibility, and validation contracts.

## Whole-rupee monetary rules

Money remains decimal-typed for storage and ledger compatibility. New fee-structure pricing and fixed monetary policy settings round upward to whole rupees. Collection, concession, refund and proration calculations use the centralized `WholeRupeeAmounts` rule; rounding is a system rule, rather than a selectable policy setting. Legacy request rounding fields remain accepted for API compatibility; new versions always record RoundUp (RoundUpRupee for proration).

Installments floor each percentage share of the rounded total and put the exact remainder into the final installment in schedule order. For example, Rs. 1,000 at 33.33%, 33.33%, 33.34% produces Rs. 333, Rs. 333, Rs. 334. The annual total, generated dues and preview totals reconcile exactly.

Refunds, concessions and late fees never exceed eligible balances or hard caps: if a legacy cap is fractional, the largest permitted whole rupee is used. Exact full waivers and previously approved fractional concession allocations retain historical minor units so existing balances can be extinguished without rewriting posted evidence. Existing receipts, paid balances, reversals and journal history are not backfilled. Percentages, dates, service-day factors and statutory calculation precision retain their own units.
