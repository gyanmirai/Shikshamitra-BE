---
module: finance
domain: late-fee-waiver
content_type: Reference
status: active
updated_on: 2026-09-21
owner: backend-team
audience: humans-and-agents
---

# Implement late-fee waivers end to end

TL;DR: a late-fee waiver is an approved decision to forgive some or all of a server-calculated late-payment penalty.
It never reduces fee principal, creates student credit, moves paid money, or replaces a fee adjustment.
The Collection Policy defines eligibility and limits, a staff action creates the waiver request, an independent approver decides requests that exceed pre-approved authority, and only an Applied waiver reduces the remaining collectible late fee.

> This page is the implementation-ready target contract for late-fee waiver operations.
> Current source already supports policy-based calculation, per-due-line requests, Applied/PendingApproval/Rejected states, inline request UI, approve/reject APIs, and collection-time subtraction.
> Sections labelled as gaps or target behavior must not be represented as shipped until their code and tests are complete.

## Business outcome and boundary

| Question | Required answer |
|---|---|
| Business outcome | Let authorized school staff forgive a justified late-payment penalty without changing the original fee obligation |
| Owner | Finance Fees Collection owns the waiver request and effect; Collection Policy owns waiver eligibility, caps, thresholds, and effective rules |
| Actor | Collector requests; waiver approver approves/rejects; senior approver handles above-threshold requests; auditor views history |
| Scope | Tenant, academic year, student, fee-structure assignment, due-fee line, fee head, and captured Collection Policy version |
| Preconditions | Existing DueFee, positive remaining late fee, eligible fee head, active/effective captured rules, valid reason, and accessible student scope |
| Monetary effect | Reduce only the remaining collectible late fee by the Applied effective waiver amount |
| Side effects | Waiver evidence, audit event, approval notification, collection read-model refresh, and receipt/report disclosure |
| Failure behavior | No waiver effect until a transaction commits; retries are idempotent; stale calculations are rejected and refreshed |
| Correction behavior | Cancel before approval; reverse an Applied waiver through a linked correction; never edit or delete financial history |
| Evidence | Domain, application, API, permission, scope, concurrency, collection, receipt, report, and browser tests |

## Terminology

| Term | Meaning |
|---|---|
| Fee principal | Original scheduled or prorated fee obligation before payment and credits |
| Gross late fee | Server-calculated penalty before subtracting prior late-fee payments and Applied waivers |
| Remaining late fee | Amount still collectible after prior late-fee payments and Applied waivers |
| Requested waiver | Amount or percentage requested by staff |
| Effective waiver | Server-capped amount allowed by the captured Collection Policy rules |
| Pre-approved authority | Policy and actor authority that allows a valid request to become Applied without a second approval |
| Above-threshold approval | Additional approval authority required above the policy threshold |
| Waiver reversal | Linked correction that restores only the unconsumed effect of an Applied waiver |

## Confirmed product decisions

| Decision | Target behavior |
|---|---|
| Automatic creation | Never waive a penalty merely because a policy enables waiver |
| Policy role | Policy determines eligibility, fee-head scope, caps, expiry, pre-approval, and approval threshold |
| Staff action | Every waiver starts from an explicit staff request with a reason |
| Fee effect | Waiver changes late fee only and never changes DueFee principal |
| Approval | Requests outside pre-approved authority require an independent approver |
| Above threshold | Requires both ordinary waiver approval and above-threshold authority |
| Historical rules | Resolve the Collection Policy version captured by the student's fee assignment |
| UI ownership | Request inline from Fee Collection; manage pending decisions in a Finance approval queue |
| Navigation | Do not create a separate top-level Finance module only for waivers |
| Evidence | Applied, rejected, cancelled, and reversed records remain queryable and auditable |
| Payment timing | Revalidate waiver and remaining late fee inside the collection transaction |
| Adjustment separation | Never use adjustment to imitate penalty forgiveness |

## Waiver, concession, adjustment, and refund comparison

| Concern | Late-fee waiver | Concession | Fee adjustment | Refund |
|---|---|---|---|---|
| Business reason | Forgive a penalty | Reduce fee principal because of eligibility | Apply a proven non-cash credit or correction | Return previously collected cash |
| Monetary source | Authorized forgiveness under policy | Approved concession policy/request | Identified eligible source balance | Refundable paid balance |
| Reduces fee principal | No | Yes | Reduces target outstanding through adjustment credit | No; returns payment and updates refundable evidence |
| Reduces late fee | Yes | No | No | No |
| Creates or moves credit | No | No | Yes | No |
| Cash movement | No | No | No | Yes |
| Main evidence | Waiver application and approval | Concession request and allocations | Adjustment voucher, source lines, and target allocations | Refund request, settlement, and payment evidence |
| Typical example | Forgive INR 200 of an INR 300 late fee | Apply sibling concession to tuition | Apply INR 1,000 eligible credit to another due line | Return INR 1,000 already paid |

## Monetary invariants

Use decimal arithmetic with the fee currency and centralized rounding.

```text
grossLateFee = calculate(capturedPolicyVersion, dueFee, asOfSchoolDate)

remainingLateFeeBeforeRequest =
  max(0, grossLateFee - postedLateFeePayments - appliedWaivers)

effectiveWaiver = min(
  requestedWaiver,
  remainingLateFeeBeforeRequest,
  policyMaximumAmount when configured,
  remainingLateFeeBeforeRequest * policyMaximumPercentage / 100 when configured
)

remainingCollectibleLateFee =
  max(0, remainingLateFeeBeforeRequest - newlyAppliedWaiver)
```

The following invariants are mandatory:

- Requested and effective waiver amounts must be positive.
- Effective waiver must not exceed the currently remaining late fee.
- Applied waiver totals must not exceed the gross late fee after posted late-fee payments.
- Pending, Rejected, Cancelled, and Reversed waivers must not reduce collectible late fee.
- Waiver must not mutate `DueFee.OriginalAmount`, `PaidAmount`, `ConcessionAmount`, `AdjustmentCreditAmount`, or `OutstandingAmount`.
- A payment retry or waiver retry must not apply the same amount twice.
- Currency, rounding, and school-local date semantics must match late-fee calculation.

## Policy configuration flow

The Collection Policy owns these waiver rules:

| Setting | Validation and runtime meaning |
|---|---|
| Enable waiver | When false, reject all new waiver requests |
| Waiver fee heads | Only captured eligible heads may receive waiver |
| Maximum waiver amount | Optional currency cap on one effective waiver decision |
| Maximum waiver percentage | Optional 0-100 cap calculated from remaining late fee |
| Waiver expiry | Optional date after which new request approval is blocked |
| Requires approval | Determines whether non-pre-approved requests enter PendingApproval |
| Approval threshold | Defines ordinary versus senior approval authority |
| Pre-approved scope | Explicit role/student override allowing immediate application within policy limits |

Policy activation and fee-structure attachment must follow [Collection policy end to end](./collection-policy-end-to-end.md).
The student assignment must retain the exact Collection Policy version used for later calculation and waiver decisions.

## End-to-end waiver workflow

### 1. Load the fee collection ledger

The collect-form query returns each due line with:

- fee principal, paid amount, and outstanding amount;
- gross late fee;
- posted late-fee amount;
- Applied waiver amount;
- remaining collectible late fee;
- captured policy and version identifiers;
- calculation explanation and available waiver action.

Future installments remain visible for advance principal collection but have zero late fee and therefore no waiver action.

### 2. Open waiver request

Show `Waive late fee` only when all conditions are true:

- remaining late fee is greater than zero;
- effective policy enables waiver;
- due line fee head is eligible;
- waiver has not expired;
- actor has request permission and student scope;
- no conflicting in-flight operation prevents a new request.

The dialog displays gross late fee, paid late fee, Applied waivers, remaining late fee, policy cap, and whether approval will be required.

The requester provides:

- amount or percentage;
- mandatory reason;
- optional evidence when school configuration requires it;
- an idempotency key generated once per logical submission.

### 3. Preview on the server

Before submission, the target API returns a server-authoritative preview containing:

- due-fee and student identifiers;
- policy and captured policy-version identifiers;
- gross and remaining late fee;
- requested and effective waiver amounts;
- cap or threshold explanation;
- approval requirement and required permission tier;
- stable preview fingerprint and current concurrency stamps.

The frontend must not calculate the approved amount independently.

### 4. Create the request

Inside one transaction, the server must:

1. Validate tenant, academic year, actor permission, and student scope.
2. Load the exact due line and captured policy version.
3. Recalculate gross and remaining late fee.
4. Validate fee-head eligibility, expiry, reason, and evidence.
5. Recalculate the effective waiver and approval tier.
6. Reject a stale preview or return the refreshed calculation.
7. Create one immutable waiver application for the logical idempotency key.
8. Write the audit event.
9. Commit before publishing notifications.

Within authorized pre-approved limits, the request transitions directly to Applied.
Otherwise it transitions to PendingApproval.

### 5. Review pending requests

The Finance approval queue displays:

- student and academic context;
- due line and fee head;
- due date and days late;
- gross, paid, waived, and remaining late fee;
- requested and effective waiver amounts;
- reason and evidence;
- policy/version and cap explanation;
- requester, submission time, approval tier, and available actions.

Filters include academic year, status, fee head, requester, amount range, student, and submitted date.

### 6. Approve or reject

Approval must repeat current-state validation inside the transaction.

The server must enforce:

- approver differs from requester;
- ordinary or above-threshold permission as resolved by current request evidence;
- student remains within approver scope;
- request is still PendingApproval;
- effective amount does not exceed current remaining late fee;
- policy version, request stamp, and due-line calculation evidence have not become stale;
- idempotency prevents double approval.

Approval changes PendingApproval to Applied and records approver, timestamp, notes, and audit evidence.

Rejection changes PendingApproval to Rejected and requires a rejection reason.

### 7. Refresh collection totals

After application, invalidate the waiver, collect-form, student-fee-detail, summary, and relevant report caches.

The ledger displays waiver separately from principal, concession, adjustment, refund, and paid late fee.

Example:

```text
Fee principal                 INR 1,500
Gross late fee                  INR 300
Applied waiver                  INR 200
Remaining late fee              INR 100
Total currently payable       INR 1,600
```

### 8. Collect payment

Collection must recalculate remaining late fee and Applied waivers inside the same transaction used to validate allocations.

The server rejects a submitted late-fee allocation above the current remaining collectible late fee.

The receipt itemizes:

- principal collected;
- gross late fee at collection;
- waiver amount;
- late fee collected;
- total received;
- waiver reference when an Applied waiver affected the transaction.

Waiver itself does not create a payment receipt because it is not cash.

### 9. Cancel or reverse

A requester may cancel only a PendingApproval request before a decision.

An Applied waiver is never edited or deleted.

Reversal requires a separate permission, mandatory reason, linked original waiver, idempotency key, and server impact preview.

Before payment, reversal may restore the waiver's remaining effect to collectible late fee.

After a receipt has relied on the waiver, preserve the receipt and original waiver.
Use a linked correction or debit workflow approved by Finance rather than rewriting historical collection evidence.

## State machine

```text
Request submitted
  |-- within pre-approved authority -----------------> Applied
  `-- approval required --------------------------> PendingApproval
                                                        |-- approve --> Applied
                                                        |-- reject ---> Rejected
                                                        `-- cancel ---> Cancelled

Applied -- controlled correction --> Reversed
```

Allowed transitions:

| Current | Action | Target | Required evidence |
|---|---|---|---|
| New | Apply within pre-approved authority | Applied | Reason, calculation snapshot, actor, policy version |
| New | Submit for approval | PendingApproval | Reason, calculation snapshot, actor, policy version |
| PendingApproval | Approve | Applied | Independent approver, notes, current validation |
| PendingApproval | Reject | Rejected | Independent approver and rejection reason |
| PendingApproval | Cancel | Cancelled | Requester or authorized manager and reason |
| Applied | Reverse | Reversed | Dedicated permission, reason, impact preview, linked correction |

Current source supports only Applied, PendingApproval, and Rejected.
Cancelled and Reversed are target additions.

## Permission model

Configuration permissions and operational permissions must remain separate.

| Permission | Purpose | Suggested actors |
|---|---|---|
| Collection Policy View/Add/Delete | Configure versioned waiver rules | Finance administrator |
| ViewWaiver | Read a student's waiver history and approval queue | Collector, approver, auditor |
| RequestWaiver | Submit an individual waiver request | Collector, accountant |
| ApproveWaiver | Approve within ordinary threshold | Fee manager, accountant approver |
| ApproveWaiverAboveThreshold | Approve above threshold in addition to ApproveWaiver | Principal, finance administrator |
| RejectWaiver | Reject pending requests | Authorized approver |
| CancelWaiver | Cancel pending request | Requester or waiver manager |
| ReverseWaiver | Correct an Applied waiver | Restricted finance administrator |
| ManageBulkWaiver | Create and approve controlled bulk actions | Restricted finance administrator |

Current source uses `ApproveWaiver` for request, approve, and reject operations.
Implementation must split request and decision authority and enforce maker-checker on the server before the target flow is considered complete.

## API contract

### Current routes

| Method | Route | Current behavior |
|---|---|---|
| GET | `api/finance/late-fee-waivers?dueFeeId={id}` | Returns waiver history for one due line |
| POST | `api/finance/late-fee-waivers` | Creates an Applied or PendingApproval waiver |
| POST | `api/finance/late-fee-waivers/{id}/approve` | Approves a pending waiver |
| POST | `api/finance/late-fee-waivers/{id}/reject` | Rejects a pending waiver |

### Target additions

| Method | Route intent | Requirement |
|---|---|---|
| GET | Waiver request list | Server paging, academic-year/status/student filters, row scope |
| GET | Waiver detail | Complete calculation, policy version, timeline, evidence, and available actions |
| POST | Waiver preview | Authoritative effective amount, threshold, fingerprint, and stamps |
| POST | Cancel | Pending-only transition with reason and idempotency |
| POST | Reverse preview | Show collectible and historical impact before correction |
| POST | Reverse | Linked Applied-to-Reversed correction with dedicated permission |

Retryable commands require an `idempotencyKey`.
Mutations of an existing waiver require a concurrency stamp.
Preview-dependent commands require the current preview fingerprint and due-line evidence stamp.

## Persistence and evidence

Preserve the existing `LateFeeWaiverApplication` aggregate and extend it deliberately.

| Fact | Required persistence behavior |
|---|---|
| Tenant/student/year/due line | Required and indexed for scoped queries |
| Collection Policy | Store both policy ID and exact applied version ID |
| Calculation snapshot | Gross late fee, posted late fee, prior Applied waiver, remaining late fee, requested amount, effective amount, cap explanation |
| Request evidence | Reason, optional documents, requester, requested time |
| Decision evidence | Status, approver, decision time, approval/rejection notes |
| Correction evidence | Original waiver ID, reversal amount, reason, actor, timestamp |
| Safety | Concurrency stamp, idempotency key, and unique command/application constraints |
| Audit | Correlation ID and linked Finance audit events |

Do not mutate `DueFee` to store a computed late-fee balance.
The collect-form query derives the current result from late-fee rules, posted late-fee allocations, and Applied waiver evidence.

## Frontend workflow

### Fee Collection

1. Render the server-provided late-fee breakdown.
2. Show the waiver action only when `availableActions` includes request.
3. Open a dialog with amount/percentage, reason, evidence, and policy limit.
4. Call preview and display effective amount plus approval requirement.
5. Submit once with a stable idempotency key.
6. Show Applied or Pending Approval result.
7. Refresh the ledger and preserve the user's current selection safely.
8. Show waiver timeline and calculation explanation on the due line.

### Finance approval queue

Use a `Waiver Requests` tab in Finance approvals or Fee Collection operations.

Do not add a separate top-level Finance submodule unless operational volume later justifies it.

The queue must support direct links from dashboard pending-approval counts and must use backend-provided available actions.

### Current frontend status

The inline request dialog exists in the frontend repository at `../Shikshamitra-FE/src/screens/finance/FeeCollection/FeeCollectionComponent/LateFeeWaiverButton.tsx`.

The Redux client exposes query, request, approve, and reject operations at `../Shikshamitra-FE/src/redux/feeCollection/lateFeeWaiver.ts`.

Approve and reject hooks are not currently wired to a dedicated approval screen.

## Adjustment flow and separation

Adjustment remains a separate operational flow:

```text
Identify proven source credit
  -> create adjustment request and voucher
  -> reserve source amount
  -> independent review
  -> allocate to one or more target DueFee rows
  -> PartiallyApplied or Applied
  -> audit and report source-to-target movement
```

Adjustment states remain Pending, PartiallyApplied, Applied, Rejected, and Cancelled.

Adjustment must have all of these properties:

- a real and explainable source balance;
- source reservation so concurrent requests cannot reuse it;
- target outstanding validation;
- sum of target allocations not exceeding remaining source credit;
- maker-checker separation;
- voucher, idempotency, concurrency, and audit evidence;
- no cash receipt because no new cash was received;
- no use as a shortcut for concession, waiver, or refund.

### Current adjustment inconsistency to resolve

Canonical product documentation says adjustment creates credit pools from paid due rows.

Current `DueFee.AdjustableBalance` returns `OutstandingAmount`, and `FeeAdjustmentManager` uses that value to validate source lines.

This conflicts with the documented paid-credit source and can make an adjustment resemble unsupported principal forgiveness.

Before extending adjustment or using it as an alternative to waiver, Finance must choose and test one source-of-credit definition.

The recommended definition is an explicit eligible credit balance derived from posted financial evidence, not unpaid outstanding principal.

## Audit, receipt, reporting, and notifications

| Consumer | Required waiver behavior |
|---|---|
| Audit | Record request, approval, rejection, cancellation, reversal, policy version, actor, reason, and safe before/after calculation |
| Collection receipt | Itemize gross late fee, waiver, and late fee collected when waiver affected payment |
| Student ledger | Show waiver as a separate non-cash line linked to due fee and policy version |
| Outstanding report | Subtract only Applied waivers from collectible late fee |
| Waiver report | Report requested/effective amount, status, approver, fee head, policy, and reason |
| Adjustment report | Keep source-to-target credit movement separate from waiver totals |
| Accounts | Do not post waiver as cash; any accounting recognition rule must be explicit and versioned |
| Notifications | Notify approvers after request commit and requester after decision commit through an idempotent outbox consumer |

## Error contract

Use stable business error codes with structured data.

| Condition | Message intent |
|---|---|
| No captured policy | No Collection Policy applies to this student's fee assignment |
| Waiver disabled | This policy does not allow late-fee waiver |
| Fee head outside scope | This fee head is not eligible for waiver |
| No remaining late fee | There is no collectible late fee to waive |
| Expired waiver rule | The policy waiver period has expired |
| Invalid amount/percentage | Enter a positive value within the displayed remaining late fee |
| Cap applied | Show requested amount, effective amount, and cap reason |
| Approval required | Submit for the required approval tier |
| Self-approval | Requester cannot approve or reject their own request |
| Stale calculation | Late fee or waiver state changed; refresh and review again |
| Already processed | Return existing idempotent outcome or current terminal status |
| Student outside scope | Do not expose whether an inaccessible waiver exists |
| Reversal affects receipt | Use the controlled correction workflow instead of rewriting history |

## Test matrix

### Domain tests

- waiver disabled, ineligible fee head, expired rule, and zero remaining late fee;
- amount cap, percentage cap, both caps, exact boundaries, and rounding;
- pre-approved, ordinary approval, and above-threshold approval decisions;
- legal and illegal status transitions;
- Applied-only subtraction and non-negative remaining late fee;
- cancellation and reversal boundaries;
- adjustment source reservation and source-to-target conservation.

### Application and API tests

- tenant, academic-year, student-scope, due-line, and policy-version validation;
- request, approve, reject, cancel, and reverse permissions;
- requester cannot approve or reject their own request;
- above-threshold permission is additional rather than substitutive;
- preview and commit parity;
- idempotent request, approval, collection, and reversal retries;
- optimistic concurrency for two approvers and waiver-versus-payment races;
- collection cannot exceed remaining late fee after waiver;
- transaction rollback on audit, receipt, or persistence failure;
- historical assignment retains its captured policy version after policy edit/archive;
- cross-tenant and out-of-scope identifiers return safe denial.

### Frontend and browser tests

- waiver action visibility from backend capability and permission;
- mandatory reason, amount, percentage, and evidence validation;
- preview explanation and cap rendering;
- Applied versus Pending Approval confirmation;
- approval queue filters, detail, approve, reject, and stale refresh;
- keyboard, focus, loading, duplicate-submit, and error behavior;
- ledger and receipt show waiver separately from concession and adjustment;
- unauthorized direct navigation and mutation are denied.

### Reconciliation tests

- gross late fee equals paid late fee plus Applied waiver plus remaining late fee;
- waiver report equals operational Applied waiver evidence;
- adjustment totals never enter waiver totals;
- receipt totals equal actual received cash and never count waiver as payment;
- reversal restores only the permitted effect and retains linked history.

## Implementation sequence

### Phase 1: secure the current workflow

1. Add distinct ViewWaiver and RequestWaiver permissions.
2. Keep ApproveWaiver and ApproveWaiverAboveThreshold for decisions.
3. Enforce requester-versus-approver separation.
4. Add student-scope checks to every ID-based query and command.
5. Store exact policy-version and calculation snapshot evidence.
6. Add idempotency and concurrency to request and decision commands.

### Phase 2: complete operational UI

1. Add server preview and available actions.
2. Harden the existing inline request dialog.
3. Add the Finance waiver approval queue and detail timeline.
4. Invalidate all collection and report caches after decisions.
5. Itemize waiver on collection preview and receipt.

### Phase 3: correction and reporting

1. Add Cancelled and Reversed states without reinterpreting existing enum values.
2. Add cancellation and reversal commands with preview and linked evidence.
3. Add waiver operational report and dashboard approval count.
4. Publish post-commit notifications through the outbox.

### Phase 4: adjustment integrity

1. Decide the canonical source-of-credit equation.
2. Replace outstanding-based source eligibility with explicit proven credit evidence.
3. Add reservation, concurrency, maker-checker, and source-to-target reconciliation tests.
4. Keep waiver, concession, adjustment, and refund totals separate in every report.

## Acceptance checklist

- [ ] Policy enables and limits waiver but never silently creates one.
- [ ] Every waiver begins with an explicit reasoned staff action.
- [ ] Exact captured Collection Policy version governs calculation.
- [ ] Server preview and commit produce the same effective amount.
- [ ] Only Applied waiver reduces remaining collectible late fee.
- [ ] Fee principal and DueFee outstanding are unchanged by waiver.
- [ ] Request and approval permissions are separate.
- [ ] Requester cannot approve or reject their own request.
- [ ] Above-threshold approval requires the additional permission.
- [ ] Request, approval, collection, and reversal are idempotent and concurrency-safe.
- [ ] Pending approval queue is usable without a separate top-level submodule.
- [ ] Receipt, ledger, audit, notifications, and reports disclose waiver separately.
- [ ] Applied history is corrected through a linked reversal, never edit/delete.
- [ ] Adjustment uses a proven credit source and never substitutes for waiver.
- [ ] Tenant, academic, student, and user scope are enforced on the server.
- [ ] Focused Finance tests and documentation validation pass.

## Current implementation source map

| Concern | Current source |
|---|---|
| Waiver aggregate/status | `src/ShikshaMitra.Domain/Finance/Fees/Collections/Aggregates/LateFeeWaiverApplication.cs` |
| Policy waiver calculator | `src/ShikshaMitra.Domain/Finance/Policies/WaiverCalculationService.cs` |
| Waiver lookup/subtraction | `src/ShikshaMitra.Domain/Finance/Fees/Collections/Managers/LateFeeWaiverLookupService.cs` |
| Application workflow | `src/ShikshaMitra.Application/Finance/Fees/Collections/LateFeeWaiverAppService.cs` |
| Contracts | `src/ShikshaMitra.Application.Contracts/Finance/Fees/Collections/LateFeeWaiverDtos.cs` |
| HTTP controller | `src/ShikshaMitra.HttpApi/Controllers/Finance/Fees/Collections/LateFeeWaiversController.cs` |
| Collection integration | `src/ShikshaMitra.Application/Finance/Fees/Collections/StudentFeeDetailsQueryService.cs`, `CollectFeeWorkflow.Validators.cs` |
| Permission constants | `src/ShikshaMitra.Domain.Shared/Constants/Permissions/SubmodulePermissions/Finance/ShikshaMitraSubmodulePermissions.Finance.Policies.cs` |
| Adjustment aggregate/manager | `src/ShikshaMitra.Domain/Finance/Fees/Adjustments/Aggregates/FeeAdjustmentRequest.cs`, `FeeAdjustmentManager.cs` |
| DueFee monetary methods | `src/ShikshaMitra.Domain/Finance/Fees/Collections/Aggregates/DueFee.cs` |
| Frontend inline action | `../Shikshamitra-FE/src/screens/finance/FeeCollection/FeeCollectionComponent/LateFeeWaiverButton.tsx` |
| Frontend API client | `../Shikshamitra-FE/src/redux/feeCollection/lateFeeWaiver.ts` |

## Current gaps against this target

1. Request, approve, and reject currently share ApproveWaiver authority.
2. Current approval does not enforce requester-versus-approver separation.
3. Waiver DTO does not expose captured Collection Policy version evidence.
4. Request and decision contracts lack idempotency, concurrency, and preview fingerprints.
5. Current lifecycle lacks Cancelled and Reversed correction states.
6. Current API lists history only by dueFeeId and has no paged approval queue contract.
7. Frontend exposes the inline request but does not wire approve/reject into an approval queue.
8. Receipt, report, notification, and correction parity require end-to-end verification.
9. Adjustment documentation and `DueFee.AdjustableBalance` disagree about the source of credit.
