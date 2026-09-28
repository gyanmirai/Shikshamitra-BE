---
module: finance
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-09-24
owner: backend-team
audience: humans-and-agents
---

# Trace Finance backend workflows

TL;DR: use this page to understand how Finance changes affect due rows, approvals, receipts, and reports.

## Core workflows

| Workflow | Main steps | Validation focus |
|---|---|---|
| Charge create, approve, collect, and correct | Create per-student lines, approve/reject pending lines, collect approved balances, issue a charge receipt, and preserve linked corrections | Academic-year/user scope, exact permissions, atomic outstanding checks, maker-checker, idempotency/concurrency, category account mapping, and immutable correction history; see [Charge management end to end](./references/charge-management-end-to-end.md) |
| Concession request | Select an active policy/version on the request, preview, save a draft, attach evidence, submit, independently approve/reject, withdraw, or reverse | Request-pinned policy snapshot, frozen submission evidence, allocation totals, exact assignment dues, concurrency/idempotency, maker-checker, and balance updates |
| Fee adjustment | Create credit pool from paid due rows, approve and apply to pending due rows, reject, or cancel pending request | Paid source rows, pending target rows, idempotency key, and maker-checker rule |
| Fee collection | Load student fee details, collect payment, allocate to due rows, and create receipt records | Payment amount, due allocation, receipt numbering, and audit trail |
| Fee Collection waiver target | Show a waiver toggle on each eligible late-fee and approved charge row, require only a reason, and waive the full live remaining balance immediately | Dedicated action permission, student scope, source status, server-derived amount, idempotency, concurrency, and separate non-cash ledger evidence; see [Fee Collection waiver end to end](./references/fee-collection-waiver-end-to-end.md) |
| Fee refund | Check eligibility, create refund request, update status, and preserve receipt/payment history | Refund eligibility and non-overlap with adjustment flow |
| Reconciliation | Create or review reconciliation records, approve, or reject | Bank/account consistency and approval audit |
| Finance reports | Select report filters, preview, export, and audit output | Preview/export parity and tenant isolation |

## Concession Type lifecycle

1. Finance creates a type for one academic year with a normalized unique name, optional description, and zero or more evidence definitions.
2. Each definition records a mandatory/optional flag, maximum size in bytes, and accepted formats. Defaults are 5 MB and PDF/JPG/JPEG/PNG.
3. New concession requests can select active types only. Existing requests retain and display their type even if it is later deactivated.
4. Deactivation requires a reason; reactivation clears that reason. Deactivation does not interrupt an already pending request.
5. Active types cannot be deleted. An inactive type may be deleted only while unused. Once any request references a type, its evidence definitions are locked and it remains undeletable; name and description may still be corrected.
6. Approval checks only mandatory definitions. Optional documents may be absent, but any uploaded file is always checked against its configured definition.
7. Create, update, and status changes are captured through entity history and exposed as paged version history for the settings action drawer.

## Concession request lifecycle

1. Context resolution selects the student's exact academic-year fee assignment and its pinned concession policy version. When multiple current assignments match, the response supplies scoped choices and requires the caller to select one assignment ID. Missing or legacy assignments return explicit blockers; the request service does not choose an arbitrary assignment or silently repair evidence.
2. Preview and draft evaluation use eligible principal rows belonging to that assignment. Current-installment selection means the earliest unpaid cohort, future means later cohorts, and all-fees also permits paid rows. The selected due IDs are frozen on submission.
3. Draft owners may save an incomplete request, edit values and evidence with Edit/UploadDocument, and use a matching concurrency stamp. A staged file is bound to the uploader and requirement, expires after 24 hours if it remains unattached, and becomes request evidence only when saved. Attached evidence is retained and read through the request-scoped `ViewDocument` endpoint. Submit requires selected eligible fees, mandatory evidence, a fresh fingerprint, Submit permission, one pending-request uniqueness, and an idempotency key. Pending input and evidence are immutable.
4. Approval Preview and Approval use the request's pinned policy and frozen due IDs, reread live balances, and validate request and due-row stamps. The checker must differ from the creator. A positive reduction is allowed with a reason; the approved total can never exceed the submitted amount, current policy maximum, or live selected-row capacity. The all-fees policy uses the remaining original obligation as capacity, including amounts already paid. The final Approval repeats these rules rather than trusting the preview or client allocation amounts.
5. Reject requires an independent checker and a 10-2000 character reason. The owner may withdraw Draft or Pending with a reason. Only Draft can be deleted.
6. Approved allocations reduce `DueFee` in the same transaction and retain requested, approved, and allocation evidence. Any newly overpaid amount creates a submitted, receipt-linked refund request in that transaction for the exact excess. A separate refund checker approves it, then staff records the payout method and settlement. Linked refunds must be cancelled, rejected, or reversed before a full concession reversal; partial reversal is blocked when linked refunds exist. Reversal Preview reports the before/after outstanding impact.
7. Every command writes a Finance audit entry. Retryable commands store an actor-scoped idempotency receipt; replay with a different payload or resource is rejected.

## Integration rules

| Integration | Rule |
|---|---|
| Student to Finance | Student identity and academic scope are source data for fee and charge workflows |
| Library to Finance | Library charge settlement is not assumed unless a Finance integration explicitly maps it |
| Fee Structure to Charges | Scheduled demand remains a Fee Structure/assignment concern; ad hoc charges join it only in collection, receipts, accounts, audit, and reports |
| Policies to Charges | Concession, proration, refund, and late-fee policy applicability is never inferred; use only an explicit versioned product contract |
| Source modules to Charges | Use stable source identity, correlation, and idempotency; retries must not duplicate student obligations |
| Reports to Fees | Reports must use the same due, paid, concession, adjustment, refund, and receipt data as operational screens |
## Payment Term to collection flow

Payment Terms are versioned schedule templates. They own installment coverage dates, due dates, allocation percentages, and the rule used to calculate when a bill is generated. Partial-payment, reminder, concession, refund, and late-fee behavior remains owned by the corresponding Finance policies.

1. Create and edit accept only the user-editable states `Draft` and `Active`; archive is a separate guarded workflow, matching HR policy status management.
2. `Draft` saves an editable version. `Active` atomically validates and publishes that exact immutable version. The server derives the next valid activation date inside the selected board period; clients do not provide it.
3. A Fee Structure may explicitly attach a published or superseded version of an active Payment Term; draft and archived versions are rejected. Existing pins remain stable. See [Fee Structure publication](./references/fee-structure-workspace.md).
4. Student assignment snapshots all installment rows into DueFee records so later term edits cannot rewrite issued student schedules.
5. Every assigned snapshot is visible in Fee Collection and may receive a direct advance-payment allocation. `DemandDate <= today` determines whether the installment's bill has been generated; it does not hide or block a future scheduled installment. Due date and collection policy continue to govern late-fee evaluation.
6. Each Education Board can have only one selected default Payment Term for an academic year, and that term must be Active. Draft and Archived terms cannot be defaults. Selecting a different Active default clears the previous flag only for the same board, with a filtered unique index as the concurrency guard.
7. List and detail responses expose one top-level lifecycle `status`: `Draft`, `Active`, or `Archived`. Version-specific states remain inside version history. Archiving preserves the underlying version state, so restore returns the term to `Draft` or `Active` without rewriting history.

Demand dates are server-derived:

| Rule | Stored demand date |
|---|---|
| On installment start date | Coverage start date |
| X days before due date | Due date minus configured positive offset |
| On due date | Due date |
| On a specific date | Required date supplied for that installment |

Every installment requires a unique name and sequence, a non-overlapping period inside the selected Education Board's `StartDate`/`EndDate`, a due date inside its period, and a positive allocation. The board window, not the wider academic-year window, is authoritative. Allocations across the term must total 100%.

During authoring, staff may request a non-persisting installment-template preview. Annual, half-yearly, quarterly, monthly, and bounded custom-count templates are expanded on the server against the selected board window. The final percentage remainder is assigned deterministically to the last installment. Applying a preview only replaces the form's local rows; create/update still revalidates and versions the complete resulting schedule.

## Student fee assignment

The current preview/commit, year-scoped history, permission and migration contracts are documented in [Student fee assignment](./references/student-fee-assignment.md). That contract supersedes older assignment descriptions that require concession configuration.
