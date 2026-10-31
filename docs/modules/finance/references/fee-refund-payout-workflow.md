---
module: finance
domain: accounts
content_type: Reference
status: active
updated_on: 2026-10-30
owner: backend-team
audience: humans-and-agents
---

# Fee refund payout workflow

New fee refund requests use workflow version 1. The stored enum values remain unchanged: Submitted (0) is shown as **Pending**, Approved (2), Processed (7), Rejected (3), and Failed (8). Configured administrators with Add and Approve grants create an Approved request directly under the [administrator exception](./admin-request-auto-approval-contract.md). Historical Draft, UnderReview, Processing, Cancelled, and Reversed records keep their original meanings.

Refund policies require a nonempty `allowedPayoutMethods` selection, persisted as part of every immutable policy version. Policy read/write values are `Cash`, `Cheque`, `DemandDraft`, `BankTransfer`, and `Upi`. Eligibility and refund request responses expose `allowedPayoutMethods` using the payment labels `Cash`, `Cheque`, `Demand Draft (DD)`, `Bank Transfer (NEFT/RTGS/IMPS)`, and `UPI`. Request and review responses use the request's pinned policy snapshot; subsequent policy edits do not change its permitted methods. The server rejects disallowed request and payout methods with `FeeRefundErrorCodes.PayoutMethodNotAllowed`. Concession-generated refunds, which have no refund policy snapshot, retain all five supported payout methods. Historical requests without a policy snapshot expose an empty list.

1. `POST /api/fee-refunds` calculates receipt-linked eligibility and creates a Pending request for ordinary staff or an Approved request for an eligible administrator, with a required `idempotencyKey`. A failed refund can be replaced with `replacesRefundRequestId` after the receipt, student, allocation, and amount are checked again.
2. A different checker approves or rejects an ordinary staff request. Approval reserves the full amount; administrator creation reserves it in the same transaction. Rejection requires a reason; an Approved request can only be rejected before a payout is recorded.
3. `POST /api/fee-refunds/{id}/payout` completes one full payment and creates its source-linked Expense in a serializable transaction. The Process grant authorizes this action. Inputs are `idempotencyKey`, policy-allowed `method`, full approved `amount`, `paymentDate`, active tenant/year `expenseCategoryId` and `bankAccountId`, plus `externalReference` for non-cash methods. Expense routing is selected here, not during approval. Cash receives a unique `CASH-` voucher reference when omitted; a separate signed handover field is not required. The server retains a generated recording reference in the audit/subledger evidence. Completion consumes reservations, updates refunded amounts, and stores Processed (7), displayed as Completed. A concession-generated request selects its method here.
4. Existing pending payments can complete through the same `/payout` command using their original method, amount, date, reference and saved bank; a missing historical bank may be supplied. Worklist responses mask payment references, so clients omit the display reference when resuming a payment. The server reuses the stored reference for an omitted value or its matching masked display value, including on idempotent replay; a different explicit reference is rejected. The settlement is reused, and idempotent retries produce no duplicate expense or subledger entry. The older `.../{attemptId}/confirm` and `.../fail` routes retain their original permissions and checker restrictions for compatibility, but are not offered by the current workspace.
5. `POST /api/fee-refunds/{id}/payout/return` records an evidenced return of a previously confirmed non-cash payout, reverses refunded fee amounts, and moves the request to Failed. A linked replacement needs a fresh approval.

Every payout command checks student scope and exact action permission. The old `/process` route remains for historical records and rejects version 1 requests. The request detail masks beneficiary fields without the sensitive payment detail permission.

Recording payment writes one immutable entry to `Finance_FeeRefundAccountingEntries` and one confirmed automatic Expense keyed by `refund:{settlementId}` in the same transaction as the fee updates. They retain the selected category/bank, source receipt and allocation breakdown, method, reference, amount, and actor. Invalid routing or a failed write rolls the entire command back. A late return writes compensating entries in the fee ledger, refund subledger and Expenses. The net subledger amount equals the request's processed amount less reversals and the net of its lines. This is a refund subledger and operational expense, not a General Accounts journal posting.

Focused coverage in `FeeRefundApi_Tests` checks direct cash/bank completion, one expense after retry, invalid routing rollback, pending-payment compatibility, and returned payments. `RefundPaymentDialog.test.tsx` and approval form coverage belong to the [frontend workflow](../../../../../Shikshamitra-FE/docs/finance/references/fee-refund-payout-workflow.md).

## Edit a Pending request

`PUT /api/fee-refunds/{id}` requires the existing Refund Edit permission and current student/account scope.
Workflow version 1 requests can be edited only while Submitted (Pending), with no reservations or settlements; concession-generated refunds remain protected.
Legacy Draft editing remains supported.
Available actions include `edit` only for an eligible state and an authorized caller.

The detail response supplies `concurrencyStamp`, required on version 1 updates; a missing or stale stamp produces a concurrency conflict.
The serializable update rechecks receipt-linked eligibility and the pinned policy, then replaces lines, beneficiary details, reason and calculation evidence on the same request.
Stored student, account, academic year, original receipt and policy identity cannot be changed through the edit payload.
Unchanged masked payment identifiers are restored from saved values only when the payout method remains the same; new identifiers undergo ordinary validation.

Saving keeps the request Pending and records the editor as the submitting maker, so that editor cannot approve their own changes.
Administrator editing does not invoke the administrator creation exception.
Editing creates no reservations, settlement, expense or accounting entry, and Approved or Completed requests reject updates.
