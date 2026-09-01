---
module: finance
domain: concession-policy-flow
content_type: Conceptual
status: proposed
updated_on: 2026-09-01
owner: product-and-engineering
audience: humans-and-agents
---

# Design the concession policy and student fee flow

TL;DR: keep concession type, reusable policy, student request, approval, ledger allocation, and refund as separate concepts.
A fee-structure assignment may snapshot which published concession policies are available to the student, but it must not silently reduce the original obligation.
An approved request creates immutable concession allocations against eligible due rows, and every calculation is performed again by the backend.

## Decision summary

| Decision | Required behavior |
|---|---|
| Concession type | Describes the business reason and evidence category, such as sibling, staff ward, merit, sports, or financial need |
| Concession policy | Versioned rule that defines eligibility scope, calculation, fee-head scope, caps, timing, stacking, and approval controls |
| Fee structure | Defines gross fee heads and schedules; it may allow or recommend multiple concession policy versions but does not contain a student's award |
| Student assignment | Snapshots gross obligations, payment-term versions, and resolved eligible concession policy versions |
| Concession request | Student-specific application with evidence and a server-calculated proposal based on one policy version |
| Approval | Maker-checker decision that atomically records immutable allocations and reduces eligible outstanding due rows |
| Paid fees | A concession never returns cash; any approved retroactive benefit beyond outstanding becomes a separate credit-balance or refund workflow |
| Late fee | Collection Policy owns late-fee waivers; Concession Policy normally applies only to principal fee obligations |
| History | Published policy versions, approved requests, allocations, reversals, receipts, and reports retain the exact version and calculation snapshot |

## Current shipped baseline

The current repository has a complete request-to-due-allocation flow, but it does not yet have a backend Concession Policy aggregate or API.

| Area | Current behavior | Consequence |
|---|---|---|
| Concession Type | Stores name, description, and required document definitions for an academic year | It is an evidence category, not a calculation policy |
| Concession request | Accepts student, type, remarks, and manually entered amount or percentage for selected fee heads | The requester currently chooses the monetary rule |
| Request calculation | Backend recalculates fee-head discount from generated `DueFee.OriginalAmount` rows | Client `netAmount` is not trusted for the stored discount |
| Approval | Approver selects due rows and sends allocations; backend validates row ownership, fee head, positive amount, allocation sum, and outstanding balance | Approved concession reduces pending dues atomically |
| Documents | Approval checks all documents configured on the Concession Type | Evidence requirements are enforced at approval time |
| Maker-checker | Request creator is blocked from approval unless the current legacy privileged-role exception applies | The role-name exception is migration debt; permission-based authority is required |
| Cancellation | Pending requests may be cancelled; approved and rejected requests cannot be cancelled | There is no approved-concession reversal flow yet |
| Due ledger | `DueFee.NetAmount` currently combines concession and adjustment credits and uses `double` money fields | Migrate to distinct decimal monetary facts before extending the policy engine |
| Generic policy versions | `FinancePolicyVersion` exists and Collection Policy uses it | Concession Policy can reuse the version table but still needs a typed aggregate and contracts |
| Concession policy API | No dedicated controller, service, DTO, aggregate, resolver, or persistence mapping exists | The frontend `/finance-policies` integration is not a shipped contract |

Current source paths:

- `src/ShikshaMitra.Domain/Finance/Fees/Concessions/**`
- `src/ShikshaMitra.Application/Finance/Fees/Concessions/**`
- `src/ShikshaMitra.Application.Contracts/Finance/Fees/Concessions/**`
- `src/ShikshaMitra.Domain/Finance/Fees/Settings/ConcessionTypes/**`
- `src/ShikshaMitra.Domain/Finance/Fees/Collections/Aggregates/DueFee.cs`
- `src/ShikshaMitra.Domain/Finance/Policies/FinancePolicyVersion.cs`

## Terminology and ownership

| Concept | Owner | Contains | Must not contain |
|---|---|---|---|
| Concession Type | Finance fee settings | Stable code, name, description, evidence definitions, active/archive status | Percentage, amount, due allocation, or approval outcome |
| Concession Policy | Finance policies | Scope, eligibility, formula, caps, eligible heads, timing, stacking, approval rules | Student-specific evidence or approved money |
| Policy Version | Finance policies | Immutable serialized and queryable snapshot used for decisions | Mutable pointer to today's policy values |
| Policy Attachment | Fee setup | Fee-structure-to-policy-version eligibility relationship and precedence | Student award amount |
| Concession Entitlement | Admission/HR/Student source plus Finance handoff | Verified fact that may initiate a request, such as staff ward or admission promise | Direct mutation of Finance dues |
| Concession Request | Finance concessions | Student, policy version, type, evidence, reason, calculation snapshot, state | Cash return |
| Concession Grant | Finance concessions | Approved header, allocations, approver, applied time, policy snapshot | Editable historical formula |
| Refund or credit balance | Finance refunds/accounts | Settlement of an approved amount that cannot reduce outstanding dues | Reclassification as a concession allocation |

## Policy lifecycle

Use this lifecycle instead of an editable `Active` record:

`Draft` -> `Published` -> `Superseded` -> `Archived`

| State | Editable | Assignable/resolvable | Rules |
|---|---|---|---|
| Draft | Yes, by Edit permission | No | May be simulated but cannot be used by new requests or assignments |
| Published | No | Yes while effective | Publishing creates an immutable version and validates every dependency |
| Superseded | No | Existing snapshots only | A newer published version replaced it; historical requests retain it |
| Archived | No | No new use | Archive requires a reason and does not change historical grants |

Publishing must validate tenant, academic year, academic scope, unique code, effective dates, calculation, eligible heads, caps, stacking rules, approval thresholds, and every referenced active identifier.
Editing a published policy creates a new draft version.
Only one open draft version per policy is recommended.

### Effective dates

- `effectiveFrom` and `effectiveTo` use date-only school semantics.
- `effectiveTo = null` means through the owning academic year's end, not forever across years.
- A request uses the policy effective on its submission date unless a privileged backdated-request flow explicitly supplies an `asOfDate` and reason.
- Two published versions of the same policy must not overlap.
- Fee-structure attachments may point only to a published version effective within the same academic year.

### Default behavior

Do not use one ambiguous `isDefault` flag.

| Field | Meaning |
|---|---|
| `isRequestDefault` | Preselect this policy in a new request after scope and eligibility filtering; never auto-grants money |
| `assignmentMode` | `AvailableOnRequest` by default; `AutoCreateRequest` only for an explicitly supported entitlement flow |
| `isAcademicYearFallback` | Makes the policy available when no fee-structure attachment is present; unique within its type and scope |

There can be at most one request default for the same concession type, academic scope, and effective date.

## Policy resolution and precedence

Resolution determines which policy version may be used; it does not itself grant a concession.

Use this deterministic precedence:

1. A valid student-specific entitlement or approved override naming a policy version.
2. A matching policy version explicitly attached to the student's assigned fee structure.
3. A published scoped policy matching academic year, board, medium, stage/class/division, and concession type.
4. The matching academic-year fallback policy.
5. No policy available.

Within one level, choose the most specific academic scope, then the highest explicit priority, then the newest effective-from date.
If two policies still tie, fail with an ambiguous-policy configuration error instead of selecting by creation order.

The resolver returns all policies available for a request and identifies at most one request default.
It must never return an inactive, out-of-year, out-of-scope, expired, or wrong-tenant version.

## Policy field contract

### Identity and scope

| Field | Required/default | Validation and meaning | Downstream use |
|---|---|---|---|
| `id` | Server generated | Stable policy master identifier | URLs, attachments, audit |
| `policyCode` | Required | Tenant and academic-year unique, 3-30 uppercase letters/numbers/hyphens; generated by server or confirmed by user | Search, exports, integration |
| `policyName` | Required | Trimmed, 3-100 characters, unique within academic year | User selection and receipts/reports |
| `description` | Optional | Sanitized, maximum 500 characters | Explanation and audit; never a calculation source |
| `academicYearId` | Required | Same tenant, active, and accessible | Scope and reporting |
| `concessionTypeId` | Required | Active Concession Type in the same academic year | Evidence requirements and classification |
| `boardId`, `mediumId`, `stageId`, `standardId`, `divisionId` | Optional; null means all at that level | Hierarchy must be internally consistent and user-accessible | Eligibility filtering |
| `effectiveFrom`, `effectiveTo` | Required/from year; optional/to year end | Inside academic year and non-overlapping | Version resolution |
| `priority` | Default 0 | Bounded integer; used only after scope specificity | Deterministic resolution |
| `isRequestDefault` | Default false | Unique within type/scope/effective window | Request preselection only |

### Eligibility

Start with controlled strategies instead of an unrestricted expression builder.

| Field | Values | Rule |
|---|---|---|
| `eligibilityMode` | `ManualReview`, `RuleValidated`, `VerifiedEntitlement` | Controls whether the system can prove eligibility or requires a reviewer |
| `studentCategoryIds` | Zero or more | Empty means no category restriction; IDs are server validated |
| `admissionCategoryIds` | Zero or more | Optional restriction sourced from enrollment/admission |
| `minimumMeritScore` | Optional decimal percentage | Only valid for a merit strategy and must name the score source and assessment period |
| `maximumHouseholdIncome` | Optional decimal money | Requires currency and verified income evidence |
| `minimumSiblingCount` | Optional positive integer | Uses verified active sibling relationships in the same tenant/year |
| `employeeRelationshipRequired` | Optional boolean | Uses an active employee-dependent relationship; never a typed free-text claim |
| `requiredDocumentIds` | Derived from type plus optional policy additions | Missing evidence blocks submission or approval according to configured stage |
| `eligibilityReasonRequired` | Default true for manual review | Sanitized reason, 10-1000 characters, audit visible |

Every rule must name its source fact and behavior when the source is missing.
Missing data means not automatically eligible; it must not be treated as zero or false silently.

### Calculation

| Field | Values/default | Rule |
|---|---|---|
| `calculationMethod` | `Percentage`, `FixedTotal`, `FullWaiver`, `ManualCapped` | `ManualCapped` requires an entered request amount and stricter permission |
| `percentageValue` | Required for percentage | Decimal where `10` means 10%; greater than 0 and at most 100 |
| `fixedAmount` | Required for fixed total | Positive decimal in policy currency |
| `calculationBase` | `OriginalEligibleAmount` or `OutstandingEligibleAmount` | `FeeHeadAmount` is not a separate base; fee-head scope already determines eligible lines |
| `roundingMethod` | `None`, `NearestCurrencyUnit`, `RoundUp`, `RoundDown` | Central backend service; define precision before rounding |
| `maximumPerRequest` | Optional | Hard cap after formula and before allocation |
| `maximumPerStudentAcademicYear` | Optional | Includes approved, non-reversed grants under the configured stacking group |
| `maximumPercentageOfEligibleBase` | Optional | Protects fixed/manual policies from exceeding a share of eligible fees |
| `minimumQualifyingBase` | Optional | Request is ineligible below this base; safer than forcing a minimum award |
| `manualMaximumAmount` | Required for manual capped | Requester cannot enter more; approver may reduce but not increase without override permission |

Remove the current `minimumConcession` floor unless the school has a real minimum-award rule.
A floor can exceed the eligible balance and is usually confused with `minimumQualifyingBase`.

### Fee-head and due scope

| Field | Values/default | Rule |
|---|---|---|
| `feeHeadScope` | `SelectedFeeHeads` recommended; `AllEligibleFeeHeads` allowed | All still excludes non-concessionable and policy-excluded heads |
| `feeHeadIds` | Required for selected scope | Must belong to the academic year and be present in compatible fee structures |
| `excludeOptionalHeads` | Default false | Makes treatment of optional services explicit |
| `excludeExternalModuleHeads` | Default true | Transport/hostel/library heads require an explicit Finance mapping and policy permission |
| `applicationTiming` | `AllOutstanding`, `CurrentInstallment`, `FutureInstallments` | Replace overlapping values such as paid-only, paid-plus-pending, and entire-structure |
| `futureDefinition` | Server derived | A due row whose coverage or demand date is after the request `asOfDate`; define one project-wide rule |
| `allocationMethod` | `OldestDueFirst`, `NewestDueFirst`, `ProRataOutstanding`, `InstallmentPriority` | Required for fixed totals and remainders |

`EntireFeeStructure` belongs to fee-head scope, not application timing.
`PaidFeesOnly` is not a valid ordinary concession target because paid money requires a credit/refund settlement.

### Stacking, budget, and approval

| Field | Required/default | Rule |
|---|---|---|---|
| `stackingMode` | Default `Exclusive` | `Exclusive`, `DifferentGroupsOnly`, or `AllowedWithinCap` |
| `stackingGroupCode` | Required unless globally exclusive | Policies in the same group compete for the same annual cap |
| `maximumActiveGrants` | Default 1 per type/year | Prevents duplicate benefit |
| `budgetId` | Optional | Approved grants reserve/consume budget atomically if budgeting is enabled |
| `approvalMode` | Default `SingleApprover` | `SingleApprover`, `TwoLevel`, or controlled `AutoApprove` |
| `approvalThresholdAmount` | Optional | Above threshold requires a dedicated permission or second level |
| `autoApprovalEligibilityMode` | Optional | Auto approval allowed only when every eligibility fact is system verified |
| `requestWindowFrom`, `requestWindowTo` | Optional | Submission window inside effective period |
| `renewalMode` | Default `NewRequestEachAcademicYear` | Never silently carry an award across academic years |

## Monetary equations

Keep these facts distinct for every due row:

`outstanding = max(0, original principal + posted late fee - paid principal - concession - adjustment credit - other authorized credits)`

Late-fee waiver is stored separately from concession and is owned by Collection Policy.
Refunded cash does not increase or decrease the original concession; it changes refundable/settled balances through the refund ledger.

### Eligible base

For each candidate due row:

1. Confirm tenant, student assignment, academic year, policy version, fee head, application timing, and state.
2. Exclude reversed, cancelled, fully settled, non-concessionable, and already consumed amounts.
3. Use original principal for `OriginalEligibleAmount`.
4. Use current principal outstanding for `OutstandingEligibleAmount`.
5. Exclude late fee unless an explicitly separate waiver policy is being evaluated.

`totalEligibleBase = sum(eligible line bases)`

### Percentage

`rawAward = totalEligibleBase * percentageValue / 100`

Apply annual cap, request cap, maximum percent cap, and remaining budget before allocation.
Calculate with decimal precision, allocate to lines using the configured method, and place the final rounding remainder on the deterministic last eligible line.

### Fixed total

`rawAward = fixedAmount`

`approvedAward <= min(rawAward, totalEligibleBase, remainingRequestCap, remainingAnnualCap, remainingBudget)`

A fixed amount is a total award, not an amount repeated for every selected fee head.

### Full waiver

`rawAward = totalEligibleBase`

Full waiver still obeys fee-head scope, application timing, stacking, budget, and any explicitly configured cap.

### Manual capped

`rawAward = requestedManualAmount`

The server validates the manual maximum and every normal cap.
The approver may reduce a requested amount.
Increasing above the request requires an override permission, reason, and a new server preview.

## Fee structure and student assignment flow

### Configure a fee structure

1. Build and validate gross fee heads and Payment Term version snapshots.
2. Attach zero or more published concession policy versions to the fee structure.
3. Mark at most one attachment per concession type as the request default.
4. Validate every selected policy's academic scope and fee-head compatibility.
5. Lock/publish the fee structure version.

Use a many-to-many attachment.
A single `concessionPolicyId` is insufficient because a structure can allow sibling, merit, staff, sports, and need-based programs simultaneously.

### Assign a fee structure to a student

The assignment command must run in one transaction:

1. Validate the student's academic scope and the locked fee-structure version.
2. Snapshot selected fee heads, amounts, optional selections, Payment Term versions, and schedule rows.
3. Generate idempotent `DueFee` obligations at gross principal amounts.
4. Resolve all published concession policy versions available to this assignment.
5. Snapshot their IDs, version numbers, resolution source, and effective window on the student fee assignment.
6. Evaluate verified entitlements supplied by Admission, HR, or Student modules.
7. For `AvailableOnRequest`, show eligibility only; do not change dues.
8. For a deliberately configured `AutoCreateRequest`, create an idempotent system request/grant record and apply it through the same concession service and audit path.
9. Return a server preview showing gross assigned fees, approved concession already applied, and net outstanding.

The idempotency key should include tenant, student, academic year, fee-structure version, policy version, and entitlement reference where applicable.

### Reassignment or enrollment change

Do not overwrite the old assignment or regenerate all dues blindly.

1. Produce an impact preview that separates paid, concessioned, adjusted, and untouched due rows.
2. Preserve paid and approved history.
3. Cancel or reverse only eligible future/unpaid rows through named transitions.
4. Create a new assignment/version snapshot.
5. Re-evaluate available policies.
6. Carry forward an approved concession only when its policy explicitly permits it; otherwise require review.
7. Link old and new assignments, allocations, and correction reason.

## Concession request flow

### Create and submit

1. Select academic year and student; the student must have a fee assignment and generated dues.
2. Load policies from the canonical resolver, filtered by scope, request window, type, stacking, and eligibility.
3. Preselect only `isRequestDefault`; the user may choose another available policy.
4. Display the Concession Type and required evidence derived from the selected policy version.
5. Load eligible due rows, not only annual fee-head totals.
6. Capture reason and evidence.
7. For manual-capped policies only, capture the requested amount.
8. Call server preview; show gross eligible base, excluded amounts and reasons, formula, caps, line allocation, prior grants, and net outstanding.
9. Submit with policy version ID, selected eligible line IDs where user choice is allowed, evidence references, concurrency token, and idempotency key.
10. Server recalculates and stores the immutable request calculation snapshot.

### Review and approve

The approval screen must show:

- student, academic year, assignment, policy name/code/version, and Concession Type;
- eligibility checks with source and result;
- evidence checklist;
- original, paid, existing concession, adjustment, late fee, and outstanding by due row;
- requested formula/value and server-calculated proposal;
- caps, stacking, budget, and threshold effects;
- final proposed allocations and post-approval outstanding;
- requester identity and maker-checker result.

Approval re-runs the calculation using locked/concurrency-checked due balances.
If balances changed, return a conflict with a refreshed preview rather than silently reducing or reallocating the award.

Approval and due mutations are one local transaction.
Publish the integration event only after commit.

### Request state machine

`Draft` -> `Submitted` -> `UnderReview` -> `ApprovedApplied`

Alternative terminal paths are `Rejected`, `Withdrawn`, `Expired`, and `Reversed`.

| Transition | Actor and precondition | Required evidence |
|---|---|---|
| Create draft | Request creator; student has assignment | Policy version and student snapshot |
| Submit | Request creator; complete fields/evidence | Calculation preview hash and idempotency key |
| Start review | Reviewer with scoped view/review permission | Audit event |
| Approve and apply | Different authorized checker; balances unchanged | Approval reason/message and immutable allocations |
| Reject | Checker | Mandatory 10-1000 character reason |
| Withdraw | Requester before approval | Reason |
| Expire | System after request/policy window | System audit event |
| Reverse approved grant | Dedicated authority; open-period/correction checks | Reason, original grant link, reversing allocations |

An approved request is never edited or cancelled.
Corrections create linked reversing allocations and, when needed, a new request.

## Paid fee and retroactive benefit flow

Remove ordinary `PaidFeesOnly`, `Paid + Pending`, `Refund`, `StudentWallet`, `CreditNote`, and `ManualDecision` choices from the core policy form until the corresponding ledgers exist.

If the product supports a retroactive benefit:

1. Calculate the policy award against the permitted original base.
2. Apply as much as possible to eligible principal outstanding.
3. Record any excess as an approved student credit balance, never a negative due.
4. Let a separate permissioned workflow either retain the credit for future fees or create a refund request.
5. A refund requires refundable payment validation, payout evidence, and Refund Policy.
6. Link the grant, credit, refund, receipt/payment, and audit records.

The concession approval transaction must not pretend that a refund has been paid.

## Data model

| Aggregate/entity | Important fields |
|---|---|
| `ConcessionPolicy` | Tenant, academic year, code, name, type ID, scope, current draft/published pointers, status |
| `ConcessionPolicyVersion` or typed snapshot in `FinancePolicyVersion` | Version, effective dates, formula, caps, timing, eligibility, stacking, approval, snapshot JSON |
| `ConcessionPolicyFeeHead` | Policy version ID, fee head ID, include/exclude, optional per-head cap or priority |
| `FeeStructureConcessionPolicyAttachment` | Fee-structure version ID, policy version ID, source, priority, request-default flag |
| `StudentFeeAssignmentPolicySnapshot` | Student assignment ID, policy version ID, resolution source, eligibility status/time |
| `ConcessionRequest` | Student, assignment, type, policy/version, state, reason, requester, calculation snapshot, concurrency |
| `ConcessionRequestEvidence` | Requirement ID, document reference, verification status, verifier, time |
| `ConcessionGrant` | Request, approved amount, approver, applied time, policy snapshot, reversal state |
| `ConcessionGrantAllocation` | Grant, due row, fee head, eligible base, amount, before/after outstanding |
| `ConcessionGrantReversal` | Original allocation, reversed amount, reason, actor, time |

Database constraints should enforce tenant-aware code uniqueness, non-overlapping effective versions, one request default per scope/type window, unique pending request per student/policy when configured, unique grant per request, and unique allocation per grant/due row.

### Due ledger migration

The target due model keeps decimal fields separate:

- `originalPrincipalAmount`
- `paidPrincipalAmount`
- `concessionAmount`
- `adjustmentCreditAmount`
- `lateFeeAmount`
- `lateFeeWaiverAmount`
- `refundedAmount`
- `outstandingAmount` as a canonical derived/stored-and-validated value

Do not extend the current `double` fields or the overloaded `NetAmount` meaning for new concession behavior.

## API contract

Use dedicated typed endpoints instead of generic `any` policy payloads.

### Policy endpoints

| Method and route | Purpose |
|---|---|
| `GET /api/concession-policies` | Scoped, paged list with status, year, type, and search filters |
| `POST /api/concession-policies` | Create draft master/version |
| `GET /api/concession-policies/{id}` | Full draft/published detail with dependency counts |
| `PUT /api/concession-policies/{id}/draft` | Update draft with concurrency token |
| `POST /api/concession-policies/{id}/simulate` | Server-authoritative scenario preview |
| `POST /api/concession-policies/{id}/publish` | Validate and publish immutable version with reason |
| `POST /api/concession-policies/{id}/archive` | Archive with reason and dependency checks |
| `POST /api/concession-policies/{id}/restore` | Restore master without republishing an old version |
| `GET /api/concession-policies/{id}/versions` | Version history |
| `POST /api/concession-policies/{id}/clone` | Create a draft in the selected academic year |

### Assignment and request endpoints

| Method and route | Purpose |
|---|---|
| `GET /api/fee-structures/{id}/available-concession-policies` | Compatible published versions for attachment |
| `PUT /api/fee-structures/{id}/concession-policy-attachments` | Replace attachments on an editable structure version |
| `GET /api/students/{studentId}/concession-options` | Resolver output with eligibility and exclusion reasons |
| `POST /api/concession-requests/preview` | Canonical calculation and allocation preview |
| `POST /api/concession-requests` | Create idempotent draft/submitted request |
| `POST /api/concession-requests/{id}/submit` | Submit a complete draft |
| `POST /api/concession-requests/{id}/approve` | Recalculate, approve, allocate, and apply atomically |
| `POST /api/concession-requests/{id}/reject` | Reject with reason |
| `POST /api/concession-requests/{id}/withdraw` | Withdraw before approval |
| `POST /api/concession-grants/{id}/reverse` | Reverse an approved grant through linked allocations |

The API never accepts client totals as truth.
Preview and approval responses include a calculation hash/version so changed balances produce a concurrency conflict.

## Permission map

The current Concession Policy permission family only exposes View, Add, and Delete, while request approval and rejection currently reuse `Finance.Fees.Concession.Add`.
Replace that coarse mapping with action-level permissions.

| Area | Permissions |
|---|---|
| Policy | View, CreateDraft, EditDraft, Simulate, Publish, Archive, Restore, Clone, ViewVersions, ViewAudit |
| Attachment | ViewAttachment, ManageAttachment |
| Request | View, CreateDraft, EditDraft, Submit, Withdraw, Review, Approve, Reject |
| Exception | ManualAmount, OverrideEligibility, ApproveAboveThreshold, OverrideCap, ReverseGrant |
| Evidence/report | VerifyEvidence, ViewSensitiveEvidence, Export, ViewAudit, ViewReports |

Backend permissions, seeded grants, frontend constants, menu/layout guards, action visibility, and denied API tests must use the exact same names.
Production authorization must not branch on role-name strings.

## User interface flow

### Policy drawer

Use sections in this order:

1. Identity, academic year, Concession Type, and lifecycle.
2. Academic and student eligibility scope.
3. Calculation and caps.
4. Fee-head and installment scope.
5. Stacking, budget, and request window.
6. Approval and evidence behavior.
7. Server simulation and validation summary.

The footer must distinguish `Save Draft` from `Publish`.
Archive is a row/detail action with a mandatory reason, not a form status value.

### Student request

Use this sequence:

1. Student and assignment context.
2. Eligible policy selection with exclusion explanations.
3. Evidence and reason.
4. Eligible due lines.
5. Server preview.
6. Save Draft or Submit.

Do not let normal requesters switch independently between amount and percentage when a published policy defines the method.

### Fee assignment

Show three totals separately:

- gross fee assignment;
- already approved concession grant applied during this transaction, normally zero;
- net outstanding after approved grants.

Potential or merely eligible concession must be labelled `Available programs`, not deducted from payable fees.

## Audit, events, notifications, and reports

Audit events must include policy/version, request/grant/allocation IDs, tenant/year/student/assignment, actor, old/new state, reason, calculation summary, before/after due balances, correlation ID, and idempotency key.

Recommended events:

- `ConcessionPolicyPublishedEto`
- `StudentConcessionOptionsSnapshottedEto`
- `ConcessionRequestSubmittedEto`
- `ConcessionRequestApprovedEto`
- `ConcessionGrantAppliedEto`
- `ConcessionGrantReversedEto`
- `RetroactiveConcessionCreditCreatedEto`

Notifications consume committed events and do not invent amounts or statuses.

Reports must reconcile these grains:

| Report | Grain and totals |
|---|---|
| Policy utilization | Policy version by academic scope: submitted, approved, rejected, reversed, amount |
| Student concession register | One grant with allocation drill-down and evidence status |
| Fee-head impact | Fee head and due period: original, concession, paid, adjusted, outstanding |
| Budget utilization | Budget/policy: reserved, approved, reversed, remaining |
| Exception report | Manual amount, eligibility override, cap override, retroactive credit, reversal |

## Failure and correction behavior

| Failure | Required response |
|---|---|
| Policy changed after preview | Conflict; reload the effective version and preview |
| Due balance changed after preview | Conflict with changed lines; do not partially apply |
| Duplicate submit/approve retry | Return the original idempotent outcome |
| Missing evidence | Validation error naming each missing requirement |
| Ambiguous resolver result | Configuration error; no fallback by creation order |
| Budget exhausted | Reject atomically or require authorized override |
| Event publication failure after commit | Retry through outbox; do not roll back committed local ledger by external call |
| Approved grant mistake | Linked reversal, never edit/delete the grant or decrement fields without evidence |

## Migration plan

### Phase 0: protect the current surface

- Mark the frontend Concession Policy route as partial until a dedicated backend contract exists.
- Do not treat the current client simulation or generic `/finance-policies` payload as authoritative.
- Keep the existing request and approval APIs working while new policy IDs remain nullable.

### Phase 1: ledger and contract foundation

- Migrate due monetary fields to decimal and distinct concession/adjustment facts.
- Add exact permissions, error codes, audit events, concurrency tokens, and idempotency.
- Add typed policy enums and DTOs.

### Phase 2: policy lifecycle

- Implement typed aggregate, immutable versions, effective dates, resolver, simulation, publish/archive, and tests.
- Backfill no automatic defaults.

### Phase 3: fee structure and assignment

- Add many-to-many policy attachments.
- Snapshot available policy versions during assignment.
- Keep gross dues unchanged unless an explicit auto-entitlement is materialized through a grant.

### Phase 4: request integration

- Add policy selection, server preview, eligibility/evidence checks, calculation snapshot, thresholds, stacking, and budget rules.
- Migrate current requests with `policyVersionId = null` and a `LegacyManual` snapshot so history keeps its original meaning.

### Phase 5: corrections and retroactive settlement

- Add grant reversal.
- Add student credit balance and linked refund flow before enabling paid-fee modes.

### Phase 6: reporting and rollout

- Reconcile operational screens, exports, dashboards, notifications, and audit.
- Enable policies tenant by tenant behind a migration/readiness check.

## Required tests

| Risk | Minimum scenarios |
|---|---|
| Formula | 0/100 percent, decimal rounding, fixed amount below/equal/above base, caps, full waiver, manual maximum |
| Allocation | One/many heads, one/many installments, deterministic remainder, line cap, stable ordering |
| Eligibility | Every scope level, missing source fact, document requirement, request window, entitlement handoff |
| Resolution | Attachment, scoped, fallback, specificity, priority, expiry, ambiguity, cross-tenant denial |
| Stacking | Same type duplicate, exclusive group, annual cap, reversal releasing cap/budget |
| State | Every allowed and denied policy/request/grant transition |
| Maker-checker | Creator denial, normal approval, above-threshold permission, no role-name bypass |
| Concurrency | Preview then payment, duplicate approval retry, two simultaneous approvals, budget race |
| Assignment | Snapshot version, no silent discount, optional heads, bulk per-item outcome, late assignment |
| Correction | Reversal with paid/adjusted lines, linked evidence, report reconciliation |
| End to end | Setup -> publish -> attach -> assign -> request -> approve -> collect -> receipt -> report |

## Acceptance checklist

- [ ] Concession Type and Concession Policy have different documented meanings.
- [ ] Every policy field has a source, unit, validation, state editability, downstream use, and audit rule.
- [ ] Draft/publish/version/effective/archive behavior is implemented.
- [ ] Fee structures can attach multiple compatible published versions.
- [ ] Assignment snapshots policies but does not silently grant money.
- [ ] Request options come from one deterministic resolver.
- [ ] Server preview and approval use one calculation engine.
- [ ] Original, paid, concession, adjustment, late fee, refund, and outstanding remain distinct.
- [ ] Paid-fee settlement uses credit/refund, not a negative due.
- [ ] Approval is transactional, idempotent, concurrency-safe, and maker-checker protected.
- [ ] Approved grants have a reversal path.
- [ ] Exact permissions and frontend parity tests exist.
- [ ] Audit, notifications, receipts, reports, and exports use canonical records.
- [ ] Legacy requests are backfilled without reinterpreting their amounts.
