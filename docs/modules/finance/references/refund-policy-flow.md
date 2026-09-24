---
module: finance
domain: refund-policy-flow
content_type: Conceptual
status: proposed
updated_on: 2026-09-07
owner: product-and-engineering
audience: humans-and-agents
---

# Design the Refund Policy and settlement flow

TL;DR: configure one versioned Refund Policy per fee-structure context, manage its status like Collection Policy, resolve it automatically from the student's assignment and original receipt, calculate the refundable amount on the server, approve with maker-checker control, and record the actual payout as a separate immutable settlement.
The ordinary school-staff journey is Student and Receipt -> Refundable Items -> Reason and Payout -> Review and Submit -> Approve -> Record Settlement.

## Document status and scope

This page is the backend and product contract for the Refund Policy flow.
Policy setup, Collection-style status selection, version history, technical simulation, and fee-structure/student-assignment snapshots are implemented in the current working tree. The staff policy editor intentionally exposes eligibility only. The receipt-allocation, approval, settlement, and accounting sections remain the target contract for completing the existing refund transaction workflow.
Current source remains authoritative for behavior that is not yet marked as implemented here.

This design deliberately extends the existing `FeeRefundRequest`, `FeeRefundLine`, `DueFee`, and `/api/finance/fee-refunds` flow instead of replacing the Finance module.
The frontend implementation plan is maintained in [Refund Policy frontend flow](../../../../../Shikshamitra-FE/docs/finance/references/refund-policy-frontend-flow.md).

## Outcomes

The completed flow must guarantee these outcomes:

| Outcome | Required behavior |
|---|---|
| Simple setup | Finance setup staff configure a policy once and attach it to a fee-structure version. |
| Simple request | Request staff select a student and original receipt, not a policy or accounting head. |
| Server authority | Eligibility, policy resolution, amount calculation, allocation, and status transitions are recalculated by the backend. |
| No over-refund | Reserved plus processed refund amounts never exceed settled and non-reversed payment allocations. |
| Traceability | Every refund line identifies the original payment, receipt, allocation, due row, fee head, installment, student, and academic year. |
| Correct workflow | Approval reserves money; settlement records that money actually left the school. |
| Clear accounting | A processed refund creates a linked cash/bank transaction and ledger posting. |
| Safe history | Policies, requests, approvals, settlements, reversals, and audit events remain immutable and queryable. |
| Permission parity | Frontend actions and backend endpoints use the same action-specific permission constants. |

## Staff roles and responsibilities

Schools may assign several permission bundles to the same employee, but the actions remain distinct.

| Staff role | Ordinary tasks | Must not do by default |
|---|---|---|
| Fee setup staff | Create, test, save as Active, archive, unarchive to Draft, and attach Refund Policies. | Approve their own refund request. |
| Refund maker | Find a student and receipt, preview a refund, save a draft, and submit it. | Change the server calculation or process money. |
| Refund checker | Review evidence and approve or reject a submitted request. | Approve a request they created. |
| Cashier or accountant | Record the approved cash, cheque, bank, or UPI settlement. | Increase the approved amount. |
| Finance manager | Resolve exceptions and authorize reversals. | Rewrite processed history. |

## Current repository baseline

| Area | Current behavior | Required correction |
|---|---|---|
| Policy UI | The drawer calls the generic `/finance-policies` client. | Add a typed Refund Policy backend and replace the generic client. |
| Policy calculation | The browser simulation returns the entered amount unchanged. | Add a server preview using a versioned policy and current payment allocations. |
| Fee structure | The frontend exposes `refundPolicyId`, but the backend contract does not persist it. | Store policy ID and exact version ID on the fee-structure version. |
| Student assignment | No Refund Policy snapshot is retained. | Snapshot the attached policy version when assigning the fee structure. |
| Request line | A line references only `DueFeeId`. | Reference the original collection allocation, transaction, receipt, due row, and snapshot dimensions. |
| Eligibility | Uses `PaidAmount - RefundedAmount` and ignores the supplied academic year and receipt verification state. | Derive eligibility from successful, verified, non-reversed payment allocations in the requested academic year. |
| Approval | Approval immediately increments `DueFee.RefundedAmount`. | Approval reserves the amount; successful settlement moves the reservation to processed refund. |
| Processing | No active processing workflow or refund transaction exists. | Add settlement, failure, retry, and reversal operations. |
| Accounting | No refund posting is created. | Post a linked cash/bank transaction and reversal of income or configured refund expense. |
| Reports | Student fee details show approved values, but Finance reports do not consistently include refunds. | Read processed and reversed settlements from one canonical reporting projection. |

## Product decisions

These decisions keep the flow safe and understandable.

| Decision | Required behavior |
|---|---|
| Policy scope | Tenant, Academic Year, and attached fee-structure version. |
| Policy status | Match Concession Policy exactly: Draft or Active in create/edit, Archive as a dedicated action, and Unarchive back to Draft. |
| Policy selection | Staff never select a policy during a refund request. |
| Policy resolution | Resolve the exact version snapshotted on the student's fee assignment. |
| Applicability period | Academic Year, Active status, and the assignment's immutable policy-version snapshot define applicability; there are no separate Effective From/To fields. |
| Original-money source | A refund always starts from one or more successful collection allocations and their receipt. |
| Approval | Maker-checker approval is mandatory for every refund. |
| Processing | Approval and actual settlement are different facts and statuses. |
| Refund methods | Cash, Cheque, Demand Draft, Bank Transfer, and UPI remain supported through typed details. |
| Amount arithmetic | Use `decimal` with explicit precision and rounding. |
| Policy changes | Every material save captures a new immutable version; old assignments and requests retain their version. |
| Deletion | Referenced policies and financial records are never hard-deleted. |

## Ownership and configuration chain

Every Refund Policy field must resolve to an existing module fact or an explicitly owned Refund configuration.
The policy does not copy academic, student, payment, receipt, account, or fee-head master data.

| Fact or configuration | Owning module | How Refund uses it |
|---|---|---|
| Tenant and currency precision | Platform and Finance configuration | Scopes all records and supplies the currency and decimal rounding precision. |
| Academic year | Institute Setup | Scopes the policy, fee structure, student assignment, receipt eligibility, request, settlement, and reports. |
| Student and guardian | Student and Admission | Supplies identity, active enrollment context, beneficiary defaults, and transfer or withdrawal facts; Refund stores immutable display snapshots where history requires them. |
| Fee structure version | Finance Fee Setup | Carries one attached Active Refund Policy version and determines the policy snapshotted during student assignment. |
| Fee head | Finance Fee Setup | Determines whether a settled receipt allocation is eligible and supplies the original income-account mapping. |
| Installment or due row | Finance Dues and Collections | Identifies the obligation paid by an allocation; it is displayed to staff but never entered as an unverified client ID. |
| Payment transaction and allocation | Finance Collections | Supplies the settled amount, method, transaction state, and allocation-level refundable source. |
| Receipt | Finance Receipts | Supplies immutable payment evidence, receipt number/date, verification, cancellation, and reversal state. |
| Charge and late fee | Finance Charges and Collections | Supplies separate paid allocation types controlled by Include Paid Charges and Include Paid Late Fee. |
| Concession and adjustment | Finance Concessions and Adjustments | Reduce or reshape obligations but never become refundable cash unless a valid payment allocation actually settled that amount. |
| Refund reason | Finance Refund configuration | Supplies a stable code/name plus whether remarks or evidence are required; requests store the selected reason snapshot. |
| School cash/bank account | Finance Accounts | Selected only during settlement and validated as active and compatible with the payout method. |
| Income, refund expense, and payable heads | Finance Accounts | Determine the approved reservation/payable and processed refund posting. |
| User permissions and student scope | Security and UserScope | Control every setup, request, decision, settlement, reversal, read, and export action. |
| Period closure | Finance Accounts or academic-year close configuration | Blocks new request, approval, settlement, or reversal unless a dedicated override is authorized and audited. |
| Reports and dashboards | Finance reporting projections | Consume canonical reservation, settlement, reversal, receipt, and posting facts; they never recalculate policy rules. |

## Refund Policy fields

### Field contract rules

- Currency is derived from tenant Finance configuration and is not editable on the policy.
- Academic Year is the policy's only calendar boundary; do not add Effective From, Effective To, or an effective-date basis.
- Money uses `decimal` with the configured currency precision.
- Percentages store `10` as ten percent, allow values greater than zero through 100, and use the configured final rounding rule.
- Archived is never accepted in create/update payloads.
- Identifiers are validated for tenant, academic year, active state, type, and UserScope where applicable.
- Active policy changes require an impact preview, a change reason, optimistic concurrency, and a new immutable version.
- Derived values are returned read-only by the server and are not posted back as authoritative facts.

### Basic fields

| Field | Business meaning and owner | Required and validation | Editability and downstream use |
|---|---|---|---|
| Policy ID | Stable system identifier owned by Refund Policy. | System generated and tenant scoped. | Read-only; referenced by versions and attachments. |
| Policy Name | Staff-facing name owned by Refund Policy. | Required; trimmed, bounded, and unique after normalization within tenant + academic year. | Editable in Draft; Active edit creates a version; shown in setup, request review, audit, and reports. |
| Policy Code | Stable internal reference owned by Refund Policy. | Backend-generated by default and unique after normalization within tenant + academic year. | Retained for persistence, versions, attachments, and integration diagnostics; never entered or displayed in the school-staff Refund Policy UI. |
| Academic Year | Policy operating year sourced from Institute Setup. | Required; must exist, belong to the tenant, and be open for policy setup. | Immutable after creation; validates structure, assignment, receipt, request, and report scope. |
| Description | Bounded operational guidance owned by Refund Policy. | Optional; null means no extra guidance. | Editable under version rules; shown only in setup and policy detail. |
| Status | Policy availability using the Concession Policy lifecycle. | Required; create/update accepts only Draft or Active. | Controls attachment and new-request eligibility; Archived is managed only by Archive/Unarchive. |
| Version | Immutable rule-snapshot sequence owned by Finance policy versioning. | System generated, positive, and unique per policy. | Read-only; snapshotted on fee structure, assignment, request, calculation, settlement, audit, and reports. |
| Concurrency Stamp | Optimistic concurrency token owned by persistence. | Required on update and status actions. | Read-only in UI; prevents silent overwrite. |

Do not keep a default-policy toggle.
Explicit attachment to a fee-structure version avoids hidden fallback behavior.

### Eligibility fields

| Field | Values and owner | Required and validation | Downstream meaning |
|---|---|---|---|
| Fee Head Scope | All Refundable Heads / Selected Heads; fee heads come from Finance Fee Setup. | Required. Selected IDs must be active, same-year, refundable, and present in every compatible attached structure. | Filters policy attachment, preview, request lines, posting, and reports. |
| Include Paid Charges | Boolean controlled by Refund Policy and sourced from paid Charge allocations. | Required; default false. | Includes only settled, verified, non-reversed charge money. |
| Include Paid Late Fee | Boolean controlled by Refund Policy and sourced from paid late-fee allocations. | Required; default false. | Includes only settled late-fee money and replaces the ambiguous late-fee-clawback field. |
| Allow Partial Request | Whether staff may request less than the server-calculated remaining amount. | Required boolean. Manual amount mode requires true. | Controls request input and validation, not payment state. |
| Per Request Maximum | Maximum final refund for one request. | Optional positive decimal in tenant currency. | Applied after deduction and before final rounding/cap result. |
| Per Student/Year Maximum | Maximum active reservation plus processed refund, net of reversals, for one student/year. | Optional positive decimal and must be at least the per-request maximum when both exist. | Enforced at preview, approval, settlement retry, and concurrent reservation. |
| Allowed Payout Methods | Cash, Cheque, Demand Draft, Bank Transfer, and UPI. | The standard profile enables all shared Finance methods; the actual refund transaction validates the chosen method and its evidence. | Retained in snapshots for compatibility; not selected by staff in policy setup. |
| Processing Target Days | Optional operational SLA from approval to settlement. | The standard profile stores null; future tenant/workflow configuration may supply a target. | Never changes refund eligibility and is not selected by staff in policy setup. |

Unpaid fees, failed payments, reversed payments, cancelled receipts, pending-verification receipts, concessions, and non-cash adjustments are never refundable money.

### Amount rule

Expose one amount rule per policy.
Do not expose advanced amount rules in the standard school-staff policy editor.
New policies use Manual up to Eligible, exact decimal values, no policy-level cap, and no automatic deduction.
The backend keeps the extended contract for existing policies, version snapshots, calculation compatibility, and future privileged tooling.

| Mode | Server behavior | Staff input |
|---|---|---|
| Remaining Paid Amount | Refund all remaining eligible paid money selected by staff. | Select allocations only. |
| Manual up to Eligible | Permit an amount from zero exclusive through the remaining eligible amount. | Enter an amount per selected item or one total for deterministic allocation. |
| Fixed Amount | Refund the configured fixed amount, capped by remaining eligibility and policy limits. | No amount entry. |
| Percentage | Refund the configured percentage of remaining eligible paid money. | No amount entry. |

An optional deduction rule may be configured only when the school has a documented business reason.
The simple default is no deduction.

| Deduction field | Meaning, validation, and use |
|---|---|
| Type | None, Fixed, or Percentage; exactly one typed rule is stored in the policy version. |
| Value | Required for Fixed/Percentage; positive; percentage is at most 100; money uses tenant currency precision. |
| Maximum Deduction | Optional positive decimal cap; applicable primarily to Percentage. |
| Applies To | Always the selected eligible amount before the final refund rounding stage. |
| Explanation | Required when a deduction exists; displayed in simulation, request preview, approval, voucher, audit, and refund report. |

The Amount Mode, Percentage Value, Fixed Amount, rounding mode, and deduction rule are backend-owned compatibility settings and are not staff-editable in the standard flow.
The eligible base, prior refund, active reservation, cap adjustment, deduction amount, and final refund are server-derived calculation outputs and must remain read-only in the frontend.

### Accounting fields

The default accounting treatment is to reverse the original fee-income mapping for the processed amount.
A policy may instead use a configured Refund Expense head only when Finance explicitly enables that treatment.

| Field | Business meaning and owner | Required and downstream use |
|---|---|---|
| Accounting Treatment | Reverse Original Income or Refund Expense; owned by Refund Policy but resolved against Finance Accounts. | Required before Active; determines processed-refund posting. |
| Refund Expense Head | Active expense posting head sourced from Finance Accounts. | Required only for Refund Expense; forbidden for Reverse Original Income. |
| Refund Payable Head | Active liability/payable head sourced from Finance Accounts. | Required when approval creates a payable reservation; used until settlement or cancellation releases it. |
| Compliance Reference | Optional bounded reference to a statutory order or school approval document. | Guidance/audit only; it never replaces account mapping or legal validation. |
| Cash or Bank Source | Active school account sourced from Finance Accounts. | Not a policy field; selected and permission-validated during settlement. |

Free-text government-compliance notes may remain as policy guidance, but they do not replace accounting configuration or validation.

### Current drawer field disposition

This mapping prevents old compatibility fields from surviving without a business meaning.

| Current frontend field | Target decision |
|---|---|
| `policyName`, `policyCode`, `description`, `academicYearId` | Keep with the Basic field contract above. |
| `status` | Keep, but allow only Draft and Active in the form. |
| `effectiveFrom`, `effectiveTo` | Remove; Academic Year, Active status, and assignment version snapshot define applicability. |
| `isDefault` | Keep as an academic-year fallback/preselection. Only an Active policy can be default; selecting a new default atomically clears the previous Refund Policy default. Direct fee-structure attachment remains higher precedence. |
| `policyType` | Keep only as an internal typed client discriminator; do not persist it as an editable Refund Policy field. |
| `applicableFeeHeads` | Replace with `feeHeadScope` plus validated `feeHeadIds`. |
| `refundOn` | Rename to `eligiblePaymentState`. |
| `enableRefundForCharge` | Rename to `includePaidCharges`. |
| `lateFeeClawback` | Replace with `includePaidLateFee`; clawback is not the correct refund meaning. |
| `refundDueWithin` | Remove from Refund Policy. A processing target may describe operational SLA, but policy setup does not impose a request deadline. |
| `bankDetailsRequired` | Remove; required details are derived from the selected allowed payout method. |
| `refundMethod` | Replace with the multi-select `allowedPayoutMethods`. |
| `allowPartialRefund` | Keep as `allowPartialRequest` and validate it against Amount Mode. |
| `requireApproval` | Remove from the form; maker-checker approval is mandatory for every refund. |
| `approvalRoleIds` | Remove; Security permissions and assignment determine actors, never stored role-name logic. |
| `deductionRules` | Replace the loose structure with one typed, versioned deduction rule. |
| `considerAsExpense` | Replace with the explicit `accountingTreatment` enum. |
| `refundExpenseHeadId` | Keep conditionally for Refund Expense treatment only. |
| `refundPayableHeadId` | Keep when approval creates a payable reservation. |
| `governmentComplianceCode` | Rename to optional `complianceReference`, or remove it when the school has no real reference to record. |
| `processingDays` | Consolidate into `processingTargetDays`; do not store a duplicate SLA field. |

## Runtime field contract

### Refund request header

| Field | Source and meaning | Validation, sensitivity, and downstream use |
|---|---|---|
| Request ID and Refund Number | System-generated identifiers owned by Refund. | Immutable after creation; number uses a concurrency-safe tenant + academic-year sequence and appears in worklists, audit, voucher, and reports. |
| Tenant ID | Current tenant context. | Never accepted as caller authority; scopes all reads, uniqueness, idempotency, and postings. |
| Academic Year ID | Selected Finance context validated against Institute Setup. | Must match assignment, policy snapshot, receipt, payment allocations, and period state. |
| Student ID | Selected Student record. | Must match every assignment, receipt, payment allocation, and DueFee line and pass UserScope checks. |
| Student Fee Assignment ID | Finance assignment resolved for the student/year. | Must own the policy snapshot and affected due rows; prevents selecting a policy manually. |
| Refund Policy ID and Version ID | Copied from the assignment snapshot. | Immutable after the first successful preview; drives calculation and historical explanation. |
| Reason ID, Code, and Name Snapshot | Selected active Refund Reason plus immutable display snapshot. | Reason must be active and same-year/global as configured; drives required remarks/evidence and report grouping. |
| Remarks | Request-specific explanation entered by the maker. | Bounded and sanitized; mandatory when the reason or override requires it; visible in approval and audit. |
| Payout Method | Selected from the snapshotted policy's allowed methods. | Shared enum; determines required typed details and compatible school account during settlement. |
| Beneficiary Snapshot | Confirmed student/guardian/payee identity. | Required; ordinary DTOs mask sensitive values; changes after submission require a new preview and audit. |
| Requested Amount | Maker input only for Manual up to Eligible mode. | Positive decimal and never authoritative; otherwise absent and rejected. |
| Calculated, Approved, Processed, and Reversed Totals | Separate server-owned monetary facts. | Derived from lines and immutable events; each total has one status-specific meaning and report sign. |
| Calculation Snapshot and Hash | Server result containing policy inputs, source balances, deductions, caps, rounding, and allocations. | Immutable per preview version; submit and approval reject stale hashes and recalculate from live facts. |
| Status | Refund request workflow state. | Changed only by named transitions and the exact action permission. |
| Idempotency Key | Client-generated logical-operation key. | Unique per tenant + operation type; repeated submission returns the original result. |
| Concurrency Stamp | Persistence token. | Required on mutable commands; stale commands return a conflict with refreshed safe context. |
| Lifecycle Actors and Timestamps | System clock and authenticated users for create, submit, review, approve/reject, cancel, process, and reverse. | UTC `DateTimeOffset`; append-only audit and worklist ownership, never overloaded into one ProcessedBy field. |

### Refund request line

| Field | Source and meaning | Validation and downstream use |
|---|---|---|
| Collection Allocation ID | Exact settled payment allocation being refunded. | Required and unique within a request; canonical refundable-money source. |
| Payment Transaction ID | Parent successful collection transaction. | Must be settled, verified when applicable, non-cancelled, and non-reversed. |
| Receipt ID and Number Snapshot | Immutable receipt evidence and display number. | Must belong to the same transaction, student, and year; links receipt history and voucher. |
| DueFee ID | Finance obligation funded by the allocation. | Must belong to the assignment/student/year and remain traceable; it is never trusted from the browser alone. |
| Fee Head ID and Snapshot | Finance Fee Head dimension paid by the allocation. | Must be eligible under the policy version and structure; used for accounting and reports. |
| Installment ID and Snapshot | Payment-term installment or due-period dimension. | Must be the actual installment, never an alias for DueFee ID; used for display and analysis. |
| Settled Source Amount | Amount settled by the original allocation. | Immutable decimal source fact. |
| Prior Processed Refund | Net processed refund on the allocation before this calculation. | Server-derived read-only fact included in calculation and audit. |
| Active Reservation | Amount held by other Approved/Processing requests. | Server-derived under transaction/concurrency control. |
| Remaining Eligible | Settled source less processed refunds and active reservations, net of reversals. | Server-derived and never negative. |
| Requested, Approved, Processed, and Reversed Line Amounts | Separate lifecycle amounts for this allocation. | Positive decimals; header totals must equal line totals after deterministic remainder allocation. |

### Settlement and reversal

| Field | Source and meaning | Validation, sensitivity, and downstream use |
|---|---|---|
| Settlement ID and Number | Immutable Refund settlement identifiers. | Generated once per successful logical settlement and shown on voucher, audit, and reports. |
| Request ID | Approved request being paid. | One request may have failed attempts, but only its approved total may be consumed. |
| Settlement Attempt ID | Idempotent processing attempt. | Tracks started, failed, and successful attempts without duplicating money. |
| Method | Request-approved payout method. | Cannot be silently changed during settlement; a change requires an authorized request amendment before approval. |
| School Account ID | Active Finance cash/bank account from which money leaves. | Required and method-compatible; permission and period checked. |
| Beneficiary Details | Typed cash, cheque, demand-draft, bank, or UPI value object. | Required per method, encrypted where appropriate, masked in ordinary DTOs/logs/exports. |
| External Reference | Bank/UPI/cheque/draft transaction reference. | Required where applicable and uniqueness-enforced within its account/method scope. |
| Settled Amount and Settled At | Actual money paid and UTC-compatible occurrence time. | Must equal the approved unconsumed reservation unless an explicitly designed partial-settlement flow exists. |
| Posting ID | Balanced Finance Accounts posting. | Required for Processed; links payable release and income reversal/refund expense plus cash/bank movement. |
| Voucher ID | Refund evidence linked to the original receipt and settlement. | Immutable and printable; never generated by a frontend-only toast. |
| Failure Code and Safe Message | Processing-attempt result. | No sensitive provider detail; used for retry guidance and operations metrics. |
| Reversal ID, Amount, Reason, Actor, and Posting ID | Compensating correction of a Processed settlement. | Separate immutable record; requires permission, positive amount within net processed balance, and linked compensating posting. |

## Policy status and version flow

```text
Draft <-> Active
  |         |
  +----> Archived ----> Draft (Unarchive)
```

This is the same lifecycle and interaction pattern as Concession Policy.

| Status | Selectable in create/edit | Editable | Can attach | Can start new requests | Historical use |
|---|---:|---:|---:|---:|---:|
| Draft | Yes | Yes | No | No | Only an already captured legacy or migrated snapshot. |
| Active | Yes | Yes with impact preview, change reason, concurrency check, and a new version | Yes | Yes through the assignment snapshot | Yes. |
| Archived | No | No | No | No | Yes. |

Lifecycle rules remain aligned with Concession Policy, while the status dropdown and single submit action match Collection Policy:

- Create and update DTOs accept only Draft or Active; sending Archived is a validation error.
- Creating or changing to Active validates academic year, fee heads, the system-managed calculation profile, and concurrency requirements.
- Draft to Active is saved through the ordinary create/update flow; a separate Activate endpoint is not required.
- Active to Draft is allowed only when the policy is not attached or otherwise used for new assignments, and requires impact confirmation.
- Editing an Active policy requires an impact preview and change reason and creates a new immutable version.
- Archive is a dedicated confirmed action, may start from Draft or Active, and is blocked while the policy is attached or otherwise required for new assignments.
- Unarchive is a dedicated action that always restores Archived to Draft; it never automatically reactivates or reattaches the policy.
- Archive and Unarchive actions create version/audit history.
- Existing fee-structure versions, assignments, requests, reservations, settlements, receipts, and reports retain their captured policy version through every status change.

## End-to-end workflow

```text
Create Refund Policy and save it as Active
              |
              v
Attach exact policy version to Fee Structure Version
              |
              v
Assign Fee Structure to Student and snapshot policy version
              |
              v
Successful Payment -> Allocations -> Verified Receipt
              |
              v
Create Refund Request from Student + Receipt
              |
              v
Server preview: eligibility + policy + prior refunds + reservations
              |
              v
Draft -> Submitted -> Under Review -> Approved or Rejected/Cancelled
                                      |
                                      v
                              Approved reservation
                                      |
                                      v
                      Processing -> Processed or Failed
                                      |
                                      v
                  Refund transaction + ledger + statement + reports
                                      |
                                      v
                         Optional controlled reversal
```

### Frontend and backend responsibility map

| Stage | Frontend responsibility | Backend responsibility | Persisted or downstream result |
|---|---|---|---|
| Policy list | Send academic year, status, search, sort, and page; show Current and Archived views using exact permissions. | Scope and page at the database, return impact/use indicators and permitted actions. | No monetary mutation. |
| Policy create/edit | Render name, description, Draft/Active status, eligibility, impact, and change reason. Keep internal and transaction-time settings hidden. | Generate the internal code, apply/validate the simple calculation profile, enforce status rules/concurrency, and write policy plus immutable version. | Draft or Active policy and version audit. |
| Policy archive/unarchive | Confirm consequences; never put Archived in the form. | Block archive when attached/in use; archive without hard delete; unarchive only to Draft. | New version/audit; historical snapshots unchanged. |
| Fee-structure attachment | List only Active same-year policies and show fee-head coverage impact. | Validate tenant/year/status/version/fee-head compatibility and store policy ID plus exact version ID. | Versioned structure attachment. |
| Student assignment | Display the attached policy summary read-only. | Validate the locked structure and snapshot the exact Refund Policy version during assignment. | Immutable assignment policy snapshot. |
| Eligible receipts | Ask for student and academic year; display receipt groups and exclusion reasons. | Enforce UserScope and resolve successful, verified, non-reversed collection allocations under the assignment snapshot. | Read-only eligibility context. |
| Preview | Send selected allocation IDs and permitted request inputs; display returned breakdown without recalculation. | Re-read canonical facts, resolve policy version, calculate caps/deduction/rounding, and return a context hash. | Short-lived calculation snapshot. |
| Draft/submit | Keep one idempotency key, confirm amount/receipt, and unwrap success before navigation. | Revalidate preview, references, evidence, permissions, duplicates, and status; persist atomically. | Draft or Submitted request plus audit/outbox intent. |
| Review/decision | Show immutable source, policy, calculation, maker, and status-specific actions. | Enforce exact permission, maker-checker, concurrency, current eligibility, and named transition. | UnderReview, Approved reservation, Rejected, or Cancelled state. |
| Settlement | Show approved amount read-only; collect typed method evidence and active school account; preserve the retry key. | Process idempotently, create settlement, consume reservation, post accounts, generate voucher, and commit atomically. | Processed or Failed attempt; Processed means money was recorded as paid. |
| Reversal | Show original settlement/posting and require reason plus explicit confirmation. | Enforce reverse permission and period rules and create compensating settlement, allocations, and posting. | Reversed request with original history retained. |
| Statements/reports | Display server projections and consistent status wording. | Project canonical reservations, settlements, reversals, receipts, and postings with identical filters and sign rules. | Reconciled operational and financial views. |

### 1. Setup and attachment

1. Setup staff create a Draft policy for an academic year, or select Active in the same status dropdown used by Collection Policy.
2. Draft policies can be edited but cannot be attached or used for new requests.
3. Setup staff submit Active through the ordinary create/update flow; the backend validates the eligibility configuration and creates the immutable version.
4. Fee-structure setup lists only Active policies from the same academic year.
5. The backend validates fee-head compatibility and attaches the exact policy version.
6. Student fee assignment snapshots both `RefundPolicyId` and `RefundPolicyVersionId`.

An existing student assignment without a snapshot remains usable for collection.
Refund creation is blocked with clear setup guidance unless an authorized migration or explicit legacy policy mapping exists.

### 2. Payment and receipt eligibility

A refundable source must satisfy every condition:

- It belongs to the same tenant, student, and academic year as the request.
- It comes from a successful collection transaction.
- Its receipt is valid and meets the required verification state.
- It has not been cancelled or reversed.
- It has a positive remaining settled allocation after processed refunds and active reservations.
- Its fee head, charge type, late-fee type, payment state, and date are allowed by the snapshotted policy.

The server returns receipt groups first, then their eligible allocation rows.
This keeps the original receipt visible without asking staff to understand internal DueFee identifiers.

### 3. Request preview and submission

1. Staff search for a student in the selected academic year.
2. The server returns eligible receipts with payment date, method, receipt number, paid amount, previously refunded amount, reserved amount, and remaining amount.
3. Staff choose a receipt and select eligible rows.
4. Staff choose Full or Partial only when the policy permits the choice.
5. Staff select a standard reason and add remarks when required.
6. Staff choose an allowed payout method and enter the minimum required beneficiary details.
7. The frontend calls the server preview endpoint.
8. The server resolves the assignment policy snapshot and recalculates the amount.
9. The response explains gross selected amount, deductions, policy cap, prior refunds, reservations, and final refundable amount.
10. Staff save a Draft or submit after one confirmation screen.

The create and submit mutations require an idempotency key.
The backend returns the same request for a safe retry of the same logical operation.

### 4. Review and approval

The checker sees the request and calculation in one read-only review page.
The page displays the student, academic year, receipt, original payment, selected allocations, policy name/version, calculation, prior refunds, remaining balance, reason, payout method, and audit timeline.

On approval, one transaction must:

1. Lock or concurrency-check the request and affected payment allocations or refund-balance rows.
2. Reject approval by the creator without an explicit audited override permission.
3. Revalidate payment, receipt, student, year, policy version, dates, limits, prior refunds, and reservations.
4. Reject stale previews with a stable conflict and return the refreshed calculation.
5. Create immutable reservation allocations.
6. Change status to Approved.
7. Write Finance audit and outbox events.

An approver may reduce an amount only through a new server preview and recorded reason.
An approver may not increase an amount.

### 5. Settlement

Approval does not mean the student has received money.
Cashier or accounting staff process an Approved request.

Required settlement details vary by method:

| Method | Required evidence |
|---|---|
| Cash | Paid date, receiving person, school cash account, and voucher acknowledgement. |
| Cheque | Cheque number, cheque date, issuing bank account, beneficiary, and clearance state. |
| Demand Draft | Draft number, date, issuing bank account, beneficiary, and clearance state. |
| Bank Transfer | Bank account, masked beneficiary account, IFSC, transaction reference, and transaction date. |
| UPI | School account, masked beneficiary UPI ID, transaction reference, and transaction date. |

On successful settlement, one transaction must:

1. Create the immutable `RefundSettlement` and its method-specific detail value object.
2. Create the linked Finance cash/bank transaction and balanced ledger posting.
3. Move each line from reserved amount to processed refund amount.
4. Change the request status to Processed.
5. Create a refund voucher or credit note linked to the original receipt.
6. Publish an outbox event containing request, settlement, posting, receipt, and allocation identifiers.
7. Commit once.

If processing fails, no processed refund or ledger posting is recorded.
The request moves to Failed with a safe reason and may be retried idempotently.

### 6. Cancellation and reversal

| State | Allowed correction |
|---|---|
| Draft | Creator may edit or delete the draft. |
| Submitted or Under Review | Creator may withdraw; checker may reject or cancel with a reason. |
| Approved but not Processed | Authorized staff may cancel and release reservations with a reason. |
| Processing | No cancellation while the settlement transaction is in flight. |
| Failed | Authorized staff may retry or cancel and release reservations. |
| Processed | Never edit or delete; use a separately authorized reversal. |
| Reversed | Terminal historical state. |

A processed reversal creates a compensating settlement and ledger posting.
It never deletes or rewrites the original settlement.
The original collection payment may not be reversed while active reservations or net processed refunds depend on it.

## Status contract

Use one shared numeric or string contract across backend and frontend.
Do not maintain a handwritten frontend enum with a different ordering.

```text
Draft
Submitted
UnderReview
Approved
Processing
Processed
Rejected
Cancelled
Failed
Reversed
```

| From | Allowed next states |
|---|---|
| Draft | Submitted, Cancelled |
| Submitted | UnderReview, Approved, Rejected, Cancelled |
| UnderReview | Approved, Rejected, Cancelled |
| Approved | Processing, Cancelled |
| Processing | Processed, Failed |
| Failed | Processing, Cancelled |
| Processed | Reversed |
| Rejected, Cancelled, Reversed | None |

## Calculation contract

Use decimal arithmetic end to end.
Apply the tenant currency precision and configured rounding once at the documented final calculation stage.

```text
SettledPaid = sum(successful, verified, non-reversed original payment allocations)

Unavailable = sum(processed refund allocations)
            + sum(active approved/processing reservations)
            - sum(processed refund reversals)

RemainingEligible = max(0, SettledPaid - Unavailable)

PolicyBase = sum(selected RemainingEligible values allowed by fee head,
                 payment state, receipt-deadline rule, charge, and late-fee rules)

RawRefund =
  Remaining Paid Amount: PolicyBase
  Manual up to Eligible: RequestedAmount
  Fixed Amount:          Policy.FixedAmount
  Percentage:            PolicyBase * Policy.Percentage / 100

Deduction = configured fixed or percentage deduction, bounded by its maximum

FinalRefund = round(max(0, min(RawRefund - Deduction,
                               PolicyBase,
                               RemainingPerRequestCap,
                               RemainingStudentYearCap)))
```

The preview response must include every component used in this calculation.
Approval repeats the same calculation from current stored facts and the snapshotted policy version.

## Backend data model

Names may follow repository conventions, but the relationships and facts are required.

| Aggregate or table | Required fields and relationships |
|---|---|
| `RefundPolicy` | TenantId, AcademicYearId, Code, Name, Description, Status, eligibility rules, amount rule, limits, payout methods, accounting treatment, current version, concurrency stamp. |
| `FinancePolicyVersion` or typed version | PolicyId, version number, immutable typed rule snapshot, actor, timestamp, and reason. |
| Fee-structure version | Nullable RefundPolicyId and RefundPolicyVersionId with same-year validation. |
| Student fee assignment | Snapshotted RefundPolicyId and RefundPolicyVersionId. |
| `FeeRefundRequest` | StudentFeeAssignmentId, StudentId, AcademicYearId, PolicyId, PolicyVersionId, status, reason category, remarks, calculation snapshot/hash, idempotency key, lifecycle actors/timestamps, and concurrency stamp. |
| `FeeRefundLine` | RequestId, collection allocation ID, payment transaction ID, receipt ID, DueFeeId, FeeHeadId, installment snapshot, selected paid amount, prior refunded amount, prior reserved amount, approved amount, and processed amount. |
| `RefundReservation` or allocation state | Request line, reserved amount, release/consume state, actor, and timestamp. |
| `RefundSettlement` | RequestId, settlement number, method, amount, date, transaction reference, school account, typed payment details, status, idempotency key, and posting ID. |
| `RefundReversal` | SettlementId, amount, reason, actor, timestamp, compensating transaction ID, and posting ID. |

Sensitive payout details must be encrypted where appropriate and masked in ordinary response DTOs.

### Database constraints and indexes

- Unique normalized policy code and name per tenant and academic year.
- Unique version number per policy.
- A fee-structure or student-assignment policy ID and version ID must both be null or both be present.
- Request, assignment, policy, receipt, transaction, allocation, DueFee, and fee head must belong to the same tenant, student, and academic year context.
- Refund line and settlement amounts must be positive decimals with explicit precision.
- Unique request-line allocation reference within one request.
- Unique idempotency key per tenant and operation type.
- Unique settlement number and external transaction reference where the method requires it.
- Index requests by tenant, academic year, student, status, creation time, receipt, and policy version.
- Index reservations and processed allocations by original collection allocation.
- Prevent a settlement total from exceeding its approved request total through transactional domain validation.

## Dedicated API surface

Keep thin controllers and typed contracts.
Do not reuse generic `/finance-policies` payloads.

### Refund Policy

```text
GET    /api/finance/refund-policies
GET    /api/finance/refund-policies/{id}
POST   /api/finance/refund-policies
PUT    /api/finance/refund-policies/{id}
POST   /api/finance/refund-policies/{id}/simulate
POST   /api/finance/refund-policies/{id}/clone
DELETE /api/finance/refund-policies/{id}             # archive, never hard-delete
POST   /api/finance/refund-policies/{id}/unarchive   # restore to Draft
GET    /api/finance/refund-policies/{id}/versions
```

### Policy attachment

```text
GET /api/finance/fee-structures/{id}/available-refund-policies
POST /api/finance/fee-structures/{id}/refund-policy-impact
PUT /api/finance/fee-structures/{id}/refund-policy
```

### Refund request and settlement

```text
GET    /api/finance/fee-refunds
GET    /api/finance/fee-refunds/{id}
GET    /api/finance/fee-refunds/context?studentId=...&academicYearId=...
GET    /api/finance/fee-refunds/eligible-receipts?studentId=...&academicYearId=...
POST   /api/finance/fee-refunds/preview
POST   /api/finance/fee-refunds
PUT    /api/finance/fee-refunds/{id}
DELETE /api/finance/fee-refunds/{id}
POST   /api/finance/fee-refunds/{id}/submit
POST   /api/finance/fee-refunds/{id}/start-review
POST   /api/finance/fee-refunds/{id}/approve
POST   /api/finance/fee-refunds/{id}/reject
POST   /api/finance/fee-refunds/{id}/cancel
POST   /api/finance/fee-refunds/{id}/settlements
POST   /api/finance/fee-refunds/{id}/settlements/retry
POST   /api/finance/fee-refunds/{id}/reverse
GET    /api/finance/fee-refunds/{id}/audit
```

List endpoints support server-side academic year, status, student, receipt, date, search, sorting, and pagination.
Every mutation returns the persisted status, concurrency token, calculation version, and relevant audit identifiers.

## Permission model

```text
Finance.Policies.RefundPolicy.View
Finance.Policies.RefundPolicy.Add
Finance.Policies.RefundPolicy.Delete

Finance.Fees.Refund.View
Finance.Fees.Refund.Create
Finance.Fees.Refund.EditDraft
Finance.Fees.Refund.DeleteDraft
Finance.Fees.Refund.Submit
Finance.Fees.Refund.StartReview
Finance.Fees.Refund.Approve
Finance.Fees.Refund.Reject
Finance.Fees.Refund.Cancel
Finance.Fees.Refund.Process
Finance.Fees.Refund.RetryProcessing
Finance.Fees.Refund.Reverse
Finance.Fees.Refund.ViewSensitivePaymentDetails
Finance.Fees.Refund.OverrideMakerChecker
```

Every controller and application-service mutation enforces its exact permission.
Every entity lookup also enforces tenant and hierarchical student scope.
An override permission never silently permits self-approval; it requires a reason and explicit audit event.

## Stable business errors

| Error | Meaning and staff guidance |
|---|---|
| `RefundPolicyNotConfigured` | The student's assignment has no policy snapshot; contact fee setup staff. |
| `RefundPolicyNotActive` | The policy cannot be attached or used for a new assignment. |
| `RefundPolicyAcademicYearMismatch` | Policy, structure, assignment, payment, or request years differ. |
| `RefundPolicyFeeHeadMismatch` | One or more payment allocations are outside the policy. |
| `RefundOutsideAllowedPeriod` | Receipt or request date is outside the policy period. |
| `RefundPaymentNotEligible` | The payment is unverified, failed, cancelled, or reversed. |
| `RefundReceiptNotEligible` | The receipt is invalid or does not belong to the student/year. |
| `RefundAmountMustBePositive` | The calculated or entered amount is zero or negative. |
| `RefundAmountExceedsAvailable` | The amount exceeds the current settled balance after refunds and reservations. |
| `RefundLimitExceeded` | A request or student/year policy limit would be exceeded. |
| `RefundDuplicateRequest` | The same idempotency key or duplicate active allocation was submitted. |
| `RefundStalePreview` | Payment, reservation, policy, or refund facts changed after preview. |
| `RefundMakerCheckerViolation` | The creator attempted to approve the request. |
| `RefundIllegalStatusTransition` | The requested action is not valid from the current state. |
| `RefundSettlementDetailsInvalid` | Required typed method details are missing or invalid. |
| `RefundSettlementReferenceDuplicate` | The external payment reference is already recorded. |
| `PaymentReversalBlockedByRefund` | The original payment has an active reservation or net processed refund. |

Errors return field or record identifiers safe for the caller and never expose full sensitive account details.

## Audit and reporting contract

Append an immutable audit event for create, edit, submit, review, approve, reject, cancel, processing start, processing failure, processed settlement, retry, and reversal.

Each relevant event records:

- Request, settlement, student, academic year, policy ID/version, receipt, payment, and allocation identifiers.
- Actor, permission-based action, UTC timestamp, tenant, correlation ID, idempotency key, and reason.
- Original settled amount, prior processed refund, prior reservation, approved amount, processed amount, reversed amount, and remaining amount.
- Status before and after.
- Calculation version/hash and posting identifiers.

Reports and student views use processed settlement allocations as the canonical refunded fact.
Approved reservations are shown separately as pending refunds.
Rejected and cancelled requests never reduce processed refund totals.
Reversals appear as compensating values rather than deleted history.

Required views and exports include:

- Refund worklist and refund register.
- Student fee statement.
- Receipt and payment history with linked refund status and remaining refundable amount.
- Collection report with gross collection, processed refund, reversal, and net collection columns.
- Cash/bank ledger and account reconciliation.
- Outstanding and fee-head summaries that distinguish paid, reserved refund, processed refund, and reversed refund facts.

## Cross-module invariants

- A concession never becomes refundable money unless the student actually paid that amount through a valid allocation.
- A fee adjustment never increases refundable money unless it represents a separately settled payment allocation.
- Policy changes never rewrite historical assignment or request snapshots.
- A payment or receipt with an active reservation or net processed refund cannot be deleted, cancelled, or reversed without resolving the refund first.
- Student transfer, withdrawal, or cancellation may supply a refund reason but never bypass payment, policy, approval, or settlement validation.
- Academic-year closure blocks new requests and processing unless an explicit close-period override permission and audit reason are supplied.
- Original receipts and payments remain immutable and display links to refunds and reversals.
- One financial event has one canonical amount and status projection across statements, ledgers, and reports.

## Migration and rollout

1. Add decimal columns, policy/version tables, typed contracts, and action permissions behind a feature flag.
2. Add Refund Policy and version attachment to fee structures and student assignments.
3. Add original payment-allocation references and reservation/settlement tables without rewriting existing refund history.
4. Mark existing requests as `LegacyManual` calculation snapshots with nullable policy and receipt-allocation references.
5. Backfill a policy snapshot only where one deterministic same-year mapping can be proven.
6. Replace count-based voucher generation with a transactional tenant/year sequence.
7. Implement eligibility and preview before enabling new request submission.
8. Implement approval reservation, settlement, accounting, audit, and reporting before enabling processing.
9. Block collection reversal when linked reservations or processed refunds exist.
10. Replace role-name maker-checker exceptions with explicit permissions.
11. Switch the frontend only after contract tests pass against the typed APIs.
12. Reconcile migrated `PaidAmount`, `RefundedAmount`, requests, receipts, and ledger totals before general release.

## Required backend tests

### Policy and calculation

- Policy name/code uniqueness, academic-year immutability, and fee-head validation.
- Draft/Active create and update, Active-to-Draft impact guard, dedicated archive, Unarchive-to-Draft, clone, and immutable version history matching Concession Policy.
- Fee-structure attachment and assignment snapshot behavior.
- Remaining, manual, fixed, percentage, deductions, caps, and rounding.
- Fee-head, charge, late-fee, and refund-amount rules.
- Concession and adjustment amounts do not inflate refundable money.

### Integrity and workflow

- Zero, negative, exact balance, greater-than-balance, decimal, and very large amounts.
- Cross-student, cross-year, cross-tenant, nonexistent, and mismatched allocation references.
- Unverified, failed, cancelled, reversed, and deleted payment or receipt behavior.
- Multiple partial refunds and full refund after partial refund.
- Duplicate create, double-click, API retry, repeated settlement, and repeated external reference.
- Concurrent request, approval, payment reversal, settlement, and reversal conflicts.
- Pending update never adds nonexistent reservation capacity.
- Maker-checker, every action permission, scope enforcement, and explicit override audit.
- Every legal and illegal status transition.
- Settlement success, failure, retry, partial external failure, and compensating reversal.
- Transaction rollback leaves request, reservation, DueFee, settlement, posting, and report facts consistent.

### Reporting and security

- Student statement, receipt history, refund register, collection report, outstanding report, and ledger agree on the same processed and reversed values.
- Approved but unsettled amounts appear only as reservations.
- Sensitive payout details are encrypted or masked and require the dedicated permission.
- Audit history contains every actor, amount snapshot, reason, status change, correlation, and posting reference.

## Definition of done

The flow is ready only when all of these are true:

- Staff can create a refund without choosing a policy, fee structure, internal DueFee ID, or accounting head.
- The server resolves one exact policy version and one or more original settled payment allocations.
- Preview and approval use the same versioned calculation and current financial facts.
- Active reservations plus processed refunds cannot exceed settled payment allocations under concurrency.
- Approval does not claim that money was paid.
- Processing creates one idempotent settlement, voucher, account transaction, ledger posting, and audit event.
- Payment reversal is blocked while a linked refund remains financially active.
- Processed refunds and reversals agree across student, receipt, collection, account, and report views.
- Frontend and backend share statuses, contracts, permissions, and stable errors.
- Legacy refunds remain visible and are never assigned invented historical policy meaning.

## Related documentation

- [Finance product behavior](../product.md)
- [Finance API families](../api.md)
- [Finance architecture](../architecture.md)
- [Finance workflows](../workflows.md)
- [Finance testing](../testing.md)
- [Finance Engineering Constitution](./constitution.md)
- [Refund Policy frontend flow](../../../../../Shikshamitra-FE/docs/finance/references/refund-policy-frontend-flow.md)
