# Unpaid Fee Adjustment Request Design

Status: proposed for review; implementation has not started.
Date: 2026-10-27 (Asia/Calcutta).

## Confirmed requirements

- Adjustment is a request with exactly three statuses: Pending, Approved, Rejected.
- Select the student using the existing concession request pattern.
- Show only unpaid fees, grouped by actual payment terms/installments.
- A partially paid fee is eligible only for its outstanding portion.
- A main amount automatically allocates from the earliest unpaid term forward.
- Alternatively, staff can enter individual fee amounts. The main amount and each individual amount are optional independently, but the request needs at least one positive amount.
- Approval applies the saved adjustment to unpaid fees. Rejection does not change fees.
- Deliver a frontend/backend plan first, followed by implementation after review.

The allocation order and alternative amount-entry rules were explicitly confirmed in chat.

## Current implementation findings

- `DueFee.AdjustableBalance` already equals outstanding balance. No prior payment is needed for the new business outcome. The earlier chat description of a strictly paid-fee source was too broad.
- The adjustment form still reuses `FeeHeadRefundTable`, `feeHeadRefunds`, monthly entries, and source-line DTOs. Its descriptions refer to paid fees.
- The present workflow creates a request, then distributes it through a separate application screen and supports partial application.
- Backend legacy status values are Pending=0, Rejected=1, PartiallyApplied=2, Applied=3, Cancelled=4. The frontend enum uses different meanings for 1 and 3; one mapper also treats unknown statuses as approved.
- Eligibility and apply-context queries accept an academic year but do not constrain due rows through the assignment/account year relationship.
- Approval and rejection use Add; the frontend shared permission hook only exposes View/Add/Delete. Some application checks use role names.
- The create form does not await submission before resetting/closing. Updating a request currently deletes it and creates a different record.
- The form is also opened from `src/screens/finance/FeeCollection/index.tsx`; both entry points must change together.

## Recommended architecture

Keep the existing Adjustments navigation destination and shared frontend components. Introduce a dedicated unpaid-fee request aggregate and API inside the existing adjustment feature, using decimal amounts, explicit requested target lines, and a three-state lifecycle. Reuse `DueFee.ApplyAdjustmentCredit(decimal)` as the monetary mutation.

Use new request tables/contracts rather than reinterpreting old source lines, double amounts, or legacy status integers. Existing adjustment history remains readable with its original meanings. This makes the new workflow independent of the old partial-application lifecycle and permits a staged deployment.

No new concession policy, refund, sponsorship, payment receipt, or adjustment-settings system is part of this change. A required reason explains the individual case. Sensitive reasons stay in authorized internal views.

## Frontend behavior

Use the global academic year and the concession request's Board -> Medium -> Class -> Division -> Student fields, scoped `useGetStudentsByScopeQuery`, searchable student selection, defaults, loading/error states, and dependent-field reset behavior. Match the concession layout with `FormSectionCard`; do not copy its concession policy/type requirements or administrator auto-approval.

When more than one fee assignment is available, require the assignment choice used in concessions. Auto-select the sole eligible assignment. Changing student, assignment, year, or academic scope clears amounts, fee selection, and preview; late network responses cannot populate another student's form.

The request form contains:

1. Student information and fee assignment when needed.
2. Adjustment amount: optional main amount, with helper text explaining automatic earliest-term allocation.
3. Unpaid fees: term groups with installment label, relevant dates, fee head, current unpaid amount, amount reserved by other pending adjustment requests, available amount, optional adjustment input, and balance after adjustment. Fully paid rows are absent. Rows with unpaid balance but no available capacity remain visible and disabled with an explanation.
4. Required reason (trimmed 5-1000 characters).
5. Server preview of current unpaid total, requested adjustment, and unpaid balance after approval; Submit and Cancel actions.

Term groups use persisted installment identity, sequence, coverage and schedule dates. Never relabel payment-term installments as calendar months or use display labels as identifiers. Legacy monthly dues use a documented month/year label when reliable academic-year linkage exists.

### Amount entry

- Automatic mode: entering a positive main amount replaces generated line allocations from earliest to latest. Every eligible visible line participates; paid, unavailable, or out-of-scope lines never participate.
- Manual mode: with the main amount blank, enter one or more fee-line amounts. Blank unused rows are valid and do not become zero-amount allocations.
- Editing a generated line switches to manual mode, clears the main input, and retains the other generated amounts. This prevents the main amount and lines from being counted twice.
- Clearing the main amount while still in automatic mode clears generated allocations. Clearing a manual line affects only that line.
- The UI retains raw numeric strings while typing; the backend validates money and owns the accepted preview and total.
- Changing a main amount never silently clips an excess amount to available capacity; show an error and disable submission.
- The user sees the affected fee rows and total before saving. Editing a pending request preserves its ID and reference and requires a fresh preview.

Example: unpaid Term 1=6000, Term 2=7000, Term 3=8000; main amount=10000. Allocate 6000, 4000, 0. Remaining unpaid amounts are 0, 3000, 8000. A manual request with main amount blank and only Term 2=2500 is also valid.

### Validation

- Student, valid academic context, resolved assignment, reason, and a fresh successful preview are required.
- Either main amount > 0 or at least one fee-line amount > 0 is required. Exact message: `Enter a main adjustment amount or an amount against at least one unpaid fee.`
- Optional blanks are valid. Zero alone cannot satisfy the requirement. Reject negative, malformed, non-finite, greater-than-two-decimal, or oversized monetary values.
- Every positive line must belong to the selected student's selected-year assignment and be no greater than its available outstanding balance.
- Main amount cannot exceed total available capacity. Positive manual lines must be unique; the header total is their exact sum.
- A submitted automatic command cannot also contain user-supplied manual line amounts.
- No unpaid fees produces an explicit empty state and disabled Submit.
- Failure keeps entered values and the form open. Submit is awaited and cannot run twice concurrently.

## Server contract and rules

New endpoint family: `/api/fee-adjustment-requests`.

| Endpoint | Meaning | Exact action permission |
| --- | --- | --- |
| GET `/context?studentId&academicYearId&studentFeeStructureId&requestId` | Assignment choices and term-wise eligible unpaid lines; optional requestId excludes the creator's own Pending reservation during revision | Existing adjustment View plus request ownership/state checks when requestId is supplied |
| POST `/preview?requestId` | Canonical allocation and calculation token; optional requestId binds a revision preview to that Pending request | Existing adjustment Add plus request ownership/state checks when requestId is supplied |
| POST `/` | Create Pending request with saved allocation snapshot | Existing adjustment Add |
| PUT `/{id}` | Creator revises Pending request without changing identity | Existing adjustment Add plus creator/state checks |
| GET `/applications` | Scoped, searched, paged register and three status counts | Existing adjustment View |
| GET `/{id}` | Saved request, lines, current balances, history and permitted actions | Existing adjustment View |
| POST `/{id}/approve` | Apply all saved lines and approve atomically | New `Finance.Fees.Adjustment.Approve` |
| POST `/{id}/reject` | Reject with required reason, release pending capacity | New `Finance.Fees.Adjustment.Reject` |

Verify the existing permission prefix from canonical backend constants before implementation. New permissions are defined in the same permission family, including explicit grants/denials tests. Never infer approval authority from Add, a role-name string, or concession administrator behavior. Existing roles receive no automatic grant restoration. Preserve the current grouped permission-editor UI and update its owning adjustment mapping without adding a replacement editor.

### Lifecycle

New shared enum: Pending=0, Approved=1, Rejected=2. It is a distinct type/contract from the legacy five-state enum.

- Create -> Pending. No due balance, cash, or payment receipt changes.
- Pending -> Approved only when every saved allocation can be applied. Persist approval and fee credits in the same transaction.
- Pending -> Rejected with a reason (trimmed 3-2000 characters). No fee credits.
- Creator cannot approve or reject their own request. Approved/Rejected are terminal and read-only.
- The new flow has no Draft, Partially Applied, Apply Later, Cancelled, delete, or automatic administrator approval state/action.
- A correction to an approved monetary record must use a separately authorized linked reversal/correction contract; no direct editing or deletion is introduced here. Audit must preserve all approved evidence.

### Allocation and balance contract

- Determine academic year by the persisted `StudentFeeStructure`/`FinanceFeeAccount` relation, not the due's calendar year. Reject contradictory links. Unlinked legacy rows without trustworthy year provenance are excluded with a diagnostic; do not guess.
- Include generated principal dues with outstanding > 0, including the remaining portion of partially paid dues and future generated installments. Do not generate new installments here. Late-fee/charge waivers remain separate workflows.
- Term order is ascending `CoverageStartDate ?? DemandDate ?? ScheduleDueDate ?? DueDate`, then installment sequence, payment-term version/installment identity, and stable fee-head/due IDs. The same sorted context drives grouping and server allocation.
- Available line amount = max(0, current outstanding - amounts reserved by other Pending requests). Pending capacity is an adjustment-request reservation, not a deduction from the parent's payable balance and not a block on normal collections.
- Exclude the request's own reservation during revision/approval. Reject an overlapping new request above remaining capacity. Serialize reservation decisions per student/year/assignment so two simultaneous requests cannot over-reserve.
- Allocation consumes min(remaining main amount, available line capacity) in the defined order; decimal sum must equal the requested amount exactly. Manual mode preserves exactly the positive lines entered.
- Persist saved allocations and current due concurrency stamps in the preview token. At creation/revision, reject a changed preview; do not silently recalculate and save a different allocation.
- At approval, reread and lock the request and affected dues in a stable order. Require unchanged relevant monetary state and sufficient line capacity. If a payment, concession, refund, another adjustment, or assignment change invalidates the saved review, return a named conflict and keep Pending. Show the affected rows; the creator may revise and resubmit for review. Never move amounts to other fees without a fresh review.
- Approval preserves OriginalAmount and PaidAmount. For each line it increases AdjustmentCreditAmount by the allocation and uses the existing canonical balance recalculation once. Saved requested total = applied credit total after approval; unapplied remainder is always zero for a new Approved request.
- All request amounts, line amounts and monetary DTO values use decimal/numeric(18,2), INR, with at most two input decimals. Reuse central money validation; do not round excess silently. Display convention must not change stored values.
- Record a reference number, requester/reviewer, DateTimeOffset timestamps, reason, per-line before/after monetary evidence, audit, and a recoverable event intent. No cash income or fee-payment receipt is created.
- Create, revise and decisions require an idempotency key. Bind it to tenant, actor, command, request and payload hash. Identical replay returns the saved result; a different payload with the same key is rejected. Concurrent approval/rejection has one terminal winner.

## Historical compatibility and rollout

Add new request/line/command-replay storage without changing legacy monetary columns or status integers. Existing old requests remain available through a clearly labelled read-only historical view/link, not a fourth lifecycle tab on the new register. Do not display partially applied/cancelled old records as newly Approved or Pending.

After all known clients switch, retire legacy create/update/apply/delete commands with a named migration response. Historical GETs remain scoped. A legacy pending request can be recreated using the new form and an explicit previous-reference link; it is not silently converted or applied. Legacy pending reservations must not participate in new request reservations after cutover.

Deploy additive backend/migration first, then frontend, then retire old writes after a caller audit. Reconcile affected outstanding totals before and after migration; the additive migration itself must not change any balance. Coordinate with the existing uncommitted cash-management work and do not regenerate or replace its migration/snapshot changes.

## Acceptance evidence

Cover amount-entry alternatives, exact earliest-first allocation, term identity, optional blanks, partially paid/fully paid fees, cross-year/tenant/scope denial, live payment conflicts, concurrent create/approve/reject, maker-checker and exact permissions, duplicate retry, transaction rollback, historical reads, and the Fee Collection entry point. Run PostgreSQL tests for real locking/rollback; the SQLite API fixture alone cannot prove these. Finish UI validation in the Codex in-app browser with desktop/mobile and keyboard/error recovery checks.
