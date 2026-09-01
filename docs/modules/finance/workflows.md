---
module: finance
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Trace Finance backend workflows

TL;DR: use this page to understand how Finance changes affect due rows, approvals, receipts, and reports.

## Core workflows

| Workflow | Main steps | Validation focus |
|---|---|---|
| Charge create and approve | Create a pending or auto-approved charge, then approve or reject through maker-checker rules | Student scope derivation, active charge category, pending-only update and delete, approver not creator |
| Concession request | Create one request with fee-head mappings, upload documents, approve allocations to due fee rows, reject, or cancel | Allocation totals, due fee eligibility, pending-only edits, and outstanding balance updates |
| Fee adjustment | Create credit pool from paid due rows, approve and apply to pending due rows, reject, or cancel pending request | Paid source rows, pending target rows, idempotency key, and maker-checker rule |
| Fee collection | Load student fee details, collect payment, allocate to due rows, and create receipt records | Payment amount, due allocation, receipt numbering, and audit trail |
| Fee refund | Check eligibility, create refund request, update status, and preserve receipt/payment history | Refund eligibility and non-overlap with adjustment flow |
| Reconciliation | Create or review reconciliation records, approve, or reject | Bank/account consistency and approval audit |
| Finance reports | Select report filters, preview, export, and audit output | Preview/export parity and tenant isolation |

## Integration rules

| Integration | Rule |
|---|---|
| Student to Finance | Student identity and academic scope are source data for fee and charge workflows |
| Library to Finance | Library charge settlement is not assumed unless a Finance integration explicitly maps it |
| Reports to Fees | Reports must use the same due, paid, concession, adjustment, refund, and receipt data as operational screens |
## Payment Term to collection flow

Payment Terms are versioned schedule templates. They own installment coverage dates, due dates, allocation percentages, and the rule used to calculate when a bill becomes collectable. Partial-payment, reminder, concession, refund, and late-fee behavior remains owned by the corresponding Finance policies.

1. Create and edit accept only the user-editable states `Draft` and `Active`; archive is a separate guarded workflow, matching HR policy status management.
2. `Draft` saves an editable version. `Active` atomically validates and publishes that exact immutable version. The server derives the next valid activation date inside the selected board period; clients do not provide it.
3. A Fee Structure may attach only an active Payment Term's published version.
4. Student assignment snapshots all installment rows into DueFee records so later term edits cannot rewrite issued student schedules.
5. A snapshot is collectable when `DemandDate <= today`. This also provides the late-assignment catch-up rule: already-reached installments become available immediately, while future installments remain hidden from collection.
6. A board can have only one selected default Payment Term per academic year. The default flag can be selected while a term is still a draft, although the term remains unavailable to Fee Structures until it is published. Selecting a different default clears the previous flag before save, with a filtered unique index as the concurrency guard.
7. List and detail responses expose one top-level lifecycle `status`: `Draft`, `Active`, or `Archived`. Version-specific states remain inside version history. Archiving preserves the underlying version state, so restore returns the term to `Draft` or `Active` without rewriting history.

Demand dates are server-derived:

| Rule | Stored demand date |
|---|---|
| On installment start date | Coverage start date |
| X days before due date | Due date minus configured positive offset |
| On due date | Due date |
| On a specific date | Required date supplied for that installment |

Every installment requires a unique name and sequence, a non-overlapping period inside the selected Education Board's `StartDate`/`EndDate`, a due date inside its period, and a positive allocation. The board window, not the wider academic-year window, is authoritative. Allocations across the term must total 100%.
