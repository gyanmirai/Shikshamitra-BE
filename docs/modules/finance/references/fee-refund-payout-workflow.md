---
module: finance
domain: accounts
content_type: Reference
status: active
updated_on: 2026-10-02
owner: backend-team
audience: humans-and-agents
---

# Fee refund payout workflow

New fee refund requests use workflow version 1. The stored enum values remain unchanged: Submitted (0) is shown as **Pending**, Approved (2), Processed (7), Rejected (3), and Failed (8). Historical Draft, UnderReview, Processing, Cancelled, and Reversed records keep their original meanings.

1. `POST /api/fee-refunds` calculates receipt-linked eligibility and creates a Pending request with a required `idempotencyKey`. A failed refund can be replaced with `replacesRefundRequestId` after the receipt, student, allocation, and amount are checked again.
2. A different checker approves or rejects. Approval reserves the full amount. Rejection requires a reason; an Approved request can only be rejected before a payout is recorded.
3. `POST /api/fee-refunds/{id}/payout` records one full payment attempt. Record the method, source school bank account for non-cash methods, reference, date, and evidence. Cash needs only a signed handover evidence reference; if its external reference is omitted, the system generates a unique `CASH-` reference from the refund voucher. The request remains Approved while the result is unknown. A concession-generated request selects its method here.
4. `POST /api/fee-refunds/{id}/payout/{attemptId}/confirm` confirms payment and updates refunded fee amounts. `.../fail` records evidenced failure and releases the reservation. Non-cash verification requires a user different from the payout recorder. Cash confirmation uses the signed handover reference.
5. `POST /api/fee-refunds/{id}/payout/return` records an evidenced return of a previously confirmed non-cash payout, reverses refunded fee amounts, and moves the request to Failed. A linked replacement needs a fresh approval.

Every payout command checks student scope and exact action permission. The old `/process` route remains for historical records and rejects version 1 requests. The request detail masks beneficiary fields without the sensitive payment detail permission.

Confirmation writes one immutable entry to `Finance_FeeRefundAccountingEntries` in the same transaction as the fee updates. It records the source receipt and allocation breakdown by fee head, the selected school account, method, external reference, evidence, amount, and verifier. A late return writes a compensating entry linked to the original entry in the same transaction as the fee reversal. The net subledger amount must equal the request's processed amount less reversals and the net amount of its lines. Unconfirmed and failed payouts create no entry. This is a refund subledger, not a General Accounts journal posting; the Accounts module does not yet provide a posted source-linked journal for this workflow.
