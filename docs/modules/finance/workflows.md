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
