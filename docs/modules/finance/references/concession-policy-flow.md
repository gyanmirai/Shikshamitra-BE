---
module: finance
domain: concession-policy-flow
content_type: Conceptual
status: proposed
updated_on: 2026-09-01
owner: product-and-engineering
audience: humans-and-agents
---

# Design the Concession Policy and student fee flow

TL;DR: a Concession Policy is an academic-year rule set attached to one or more fee structures. A fee structure has one attached active policy version. When that structure is assigned to a student, the exact policy version is snapshotted automatically. Staff select a Concession Type only while creating a student request; the system resolves the policy, calculates the allowed concession on the server, and applies it only after approval. Policy status follows the HR Attendance Policy pattern: Draft, Active, and Archived.

## Final product decisions

These decisions are authoritative for this design:

| Decision | Required behavior |
|---|---|
| Policy scope | Tenant and Academic Year only |
| Policy status | Keep the Status field and mirror HR Attendance Policy: Draft/Active in the form, Archive as a separate action, Restore to Draft |
| Effective dates | Do not configure separate start/end dates; Academic Year and Status define availability, and assignment snapshots the exact attached version |
| Not policy scope | No board, medium, class, standard, division, or student filters on a policy |
| Fee Structure relationship | Each fee-structure version has zero or one Active Concession Policy version; one policy may be reused by many structures in the same academic year |
| Concession Type | Selected only in the Concession Request; it is not stored on or assigned to the policy |
| Type ownership | Defines the reason/category and its mandatory or optional supporting documents |
| Request policy selection | Staff do not select a policy; the backend resolves it from the student's assigned fee structure snapshot |
| Student assignment | Snapshots the attached policy version but does not automatically grant a discount |
| Calculation | The backend is authoritative; the browser only displays server preview results |
| Approval | An approved request creates immutable allocations against eligible outstanding due rows |
| Version history | Every saved policy change increments/captures a version; student assignments and requests retain the exact version used |

## Why this model is simple for school staff

The school configures a policy once per academic year, attaches it while preparing a fee structure, and then does not select it again for every student. During a request, staff only:

1. Search for the student.
2. Select the Concession Type.
3. Upload the documents required by that Type.
4. Enter a concession value only when the policy allows request-level values.
5. Review the server-calculated impact and submit.

The system determines the structure, policy version, eligible fee heads, eligible due rows, limits, and approval route.

## Concept ownership

| Concept | Owns | Must not own |
|---|---|---|
| Concession Type | Academic year, code/name, description, active state, document definitions, required/optional flag, formats and size limits | Percentage, fixed amount, policy reference, due allocation, or approval result |
| Concession Policy | Academic year, status, reusable calculation guardrails, eligible fee heads, due timing, caps, rounding, approval controls, and version | Type/category, student documents, class/medium scope, student-specific award |
| Fee Structure Version | Gross fee heads, payment schedule, and the single attached Active policy version | A student's concession amount |
| Student Fee Assignment | Student-specific gross obligation and immutable snapshots of the fee structure, payment terms, and policy version | Editable reference to the latest policy |
| Concession Request | Student, assignment, selected Type, resolved policy version, evidence, requested input, calculation snapshot, workflow state | Direct uncontrolled mutation of dues |
| Concession Grant/Allocation | Approved amount, exact due-row allocations, approver, timestamp, request and policy references | Editable formula or evidence definition |
| Refund/Credit | Settlement of an overpayment through its own controlled workflow | A hidden paid-fee option inside Concession Policy |

## Current repository baseline

The request and approval flow exists, but the policy chain does not yet exist.

| Area | Current behavior | Required correction |
|---|---|---|
| Policy frontend | Rich drawer backed by generic `/finance-policies` assumptions | Add a dedicated typed Concession Policy contract and remove unsupported generic behavior |
| Policy backend | No Concession Policy aggregate, controller, app service, DTOs, resolver, or persistence mapping | Implement the aggregate, versions, attachment, resolver, preview, status flow, permissions, and audit |
| Concession Type | Academic-year category with document definitions | Keep it separate; the current mandatory/optional document work is correct |
| Request | Staff choose Type, fee heads, and amount/percentage | Resolve policy automatically, restrict inputs through policy, and preview on the server |
| Approval | Backend validates and allocates against `DueFee` rows | Add policy snapshot/cap checks, concurrency, immutable grant history, and reversal |
| Fee Structure | Payment, collection, refund, and proration policy UI exists | Add one Concession Policy attachment per fee-structure version |
| Student assignment | Generates due rows without a policy snapshot | Snapshot the exact attached Concession Policy version |
| Ledger | `DueFee.NetAmount` combines concession and adjustment and uses `double` values | Use decimal and separate original, concession, adjustment, paid, late fee, and outstanding facts |
| Permissions | Backend has only Concession Policy View/Add/Delete; request flow reuses fee-collection authority in places | Introduce edit, clone, archive/restore, request, approval, override, attachment, and reversal permissions |

## Status and version flow

Follow the HR Attendance Policy status model exactly:

`Draft <-> Active -> Archived -> Draft (Restore)`

| Status | Selectable in form | Editable | Can attach to a structure | Can be used by a request |
|---|---:|---:|---:|---:|
| Draft | Yes | Yes | No | No for new assignments/requests |
| Active | Yes | Yes with impact preview/confirmation | Yes, when in the same academic year | Yes through an assignment snapshot |
| Archived | No; separate Archive action | No | No | Historical snapshots only |

Rules matching Attendance Policy:

- The Status field remains in create/edit with only Draft and Active options.
- Archived is rejected by create/update DTO validation; the dedicated Archive action is required.
- Creating as Active validates the complete policy before save.
- Changing Draft to Active validates dependencies and makes the policy available to Fee Structure.
- Active policy edits require impact preview/confirmation and capture a new version.
- Changing Active to Draft requires impact preview and is blocked until it is detached or replaced on fee structures used for new assignments.
- Archived policies cannot be edited.
- Archive is dependency/impact guarded and never deletes history.
- Restore/Unarchive changes Archived to Draft; it does not reactivate automatically.
- Every effective change increments the policy version and writes version/audit history.
- Existing student assignments and requests retain the version they snapshotted even when the current policy is edited, drafted, archived, or restored.
- Unlike Attendance Policy, Concession Policy has no Default toggle because Fee Structure attachment is the explicit selection mechanism.

## Recommended policy form

### 1. General information

| Field | Required | Behavior |
|---|---:|---|
| Policy Name | Yes | Unique after trim and case normalization within tenant + academic year |
| Policy Code | Yes | Generated and uniqueness-enforced by the backend; display as read-only with an optional privileged override |
| Academic Year | Yes | Taken from the active Finance context and shown read-only |
| Description | No | Short operational explanation |
| Status | Yes | Selectable Draft/Active; Archived is displayed read-only and managed through Archive/Restore actions |
| Version | System | Incremented/captured on effective change and shown read-only |

Do not add Effective From or Effective To. The active Academic Year provides the time boundary, Status controls whether a policy can be selected for new setup, and the Student Fee Assignment stores the exact policy version for later requests.

Do not add medium, board, standard, class, division, student, or Concession Type fields.

### 2. Request value rule

Because Concession Type is not connected to a formula, one attached policy must support all request categories. Add an explicit value-source decision:

| Value source | Use when | Request behavior |
|---|---|---|
| Fixed by Policy | Every request under the structure receives the same percentage/fixed/full-waiver formula | Monetary value is read-only in the request |
| Entered in Request within Policy Limits | Sibling, merit, staff ward, need-based, and other Types may receive different values | Staff may enter an allowed percentage or amount; backend enforces caps |

For `Fixed by Policy`, configure Percentage, Fixed Total Amount, or Full Waiver. For `Entered in Request within Policy Limits`, configure allowed input methods, an optional default, maximum percentage, maximum amount per request, and maximum approved amount per student for the academic year.

The ordinary form must not expose `Original Fee`, `Outstanding Amount`, and `Fee Head Amount` as interchangeable calculation bases. The authoritative base is the sum of eligible outstanding principal due rows selected by the policy timing and fee-head rules.

### 3. Applicable fees

| Field | Recommended values | Notes |
|---|---|---|
| Fee Head Scope | All concession-eligible heads / Selected heads | A selected head must belong to the academic year and have `IsConcessionApplied = true` |
| Due Timing | All Outstanding / Current Installment / Future Installments | Default to All Outstanding; do not include paid fees |
| Allocation Order | Oldest due first / Pro-rata | Keep under Advanced; default to oldest due first |
| Rounding | None / Nearest Rupee / Down / Up | Apply once at a documented stage using decimal arithmetic |

`Entire Fee Structure` belongs to fee-head scope, not due timing. `Pending Fees Only` is renamed to `All Outstanding` for clearer school terminology.

### 4. Limits and approvals

| Field | Recommended default | Rule |
|---|---:|---|
| Approval required | Yes | A concession cannot directly mutate dues from the request screen |
| Approval threshold amount | Blank | Above the threshold requires an additional permission or approval level |
| Allow multiple approved concessions | No | When enabled, enforce the annual cap and remaining eligible balance |
| Maximum per student/year | Blank | Applies across all Types under this policy |
| Request remarks required | Yes | Capture why the value was requested; evidence fields still come from Type |

Documents must not be configured in the policy. Each Concession Type document definition remains mandatory or optional; submission/approval checks only mandatory definitions while optional evidence remains attachable.

## Fields to remove from the current form

Remove these because they make the flow ambiguous, duplicate another domain, or cannot be implemented safely as simple policy settings:

- Archived as a selectable Status option. Keep the Status field with Draft and Active only.
- Default Concession Policy toggle.
- Hidden or visible Concession Type on the policy.
- Any eligibility scope for board, medium, class, standard, division, or student.
- Calculation Base selector from the basic form.
- Minimum Concession; use an optional request default and maximum guardrails instead.
- Paid Fees Only and Paid + Pending.
- Refund, Credit Note, Student Wallet, Future Fee Adjustment, and Manual Decision.
- Pending Fee Handling and Recalculate Installments.
- Duplicate Entire Fee Structure timing value.
- Future-installment strategy separate from the deterministic allocation order.
- Any frontend fallback/dummy fee heads.

Paid money is not silently changed by a concession. If a school needs to return or credit money, the approved concession may create a separately authorized refund or credit workflow with its own ledger entries.

## End-to-end workflow

```text
Academic Year
   |
   +--> Configure Concession Types (reason + document requirements)
   |
   +--> Create Concession Policy (Draft or Active)
           |
           +--> Server simulate / validate
           +--> Set Status = Active
                     |
                     v
              Attach one version to Fee Structure Version
                     |
                     v
              Assign Fee Structure to Student
                     |
                     +--> Generate gross DueFee rows
                     +--> Snapshot policy id + version id
                                  |
                                  v
                     Create Concession Request
                     - choose Type only
                     - resolved policy is read-only
                     - upload Type documents
                     - enter value only if permitted
                     - server preview and submit
                                  |
                                  v
                       Approve / Reject (maker-checker)
                                  |
                                  v
                       Immutable grant + DueFee allocations
                                  |
                    +-------------+-------------+
                    v                           v
             Collections/receipts        Reports/audit/reversal
```

### Setup and attachment

1. Finance staff creates a policy for the active academic year and changes its Status to Active when configuration is complete.
2. Fee-structure setup lists only Active policies from the same academic year.
3. The user attaches exactly one version to the fee-structure version.
4. The backend validates that selected policy fee heads exist in the structure or uses the valid intersection for `All eligible heads`.
5. The locked fee-structure version stores both `ConcessionPolicyId` and the exact active `ConcessionPolicyVersionId`.

If concessions are mandatory for the school, structure locking must fail without an attachment. Otherwise, an unattached structure is allowed but concession requests for its assignments are disabled with a clear setup message.

### Student fee-structure assignment

Assignment is the financial snapshot boundary:

1. Validate tenant, academic year, student, fee structure, lock state, and payment schedule.
2. Copy the fee heads and amounts into the student's assignment snapshot.
3. Generate gross due rows; do not reduce them automatically.
4. Copy the attached policy ID and exact active version ID into the assignment.
5. Return a read-only policy summary in the assignment response and audit event.

"Automatically apply" at assignment means automatically resolve and snapshot the attached policy rules. It does not mean automatically reduce the student's fees. The monetary concession is created only after a Concession Request is submitted and approved.

If a policy is later edited, drafted, archived, or restored, existing student assignments retain their old version. A separate impact-previewed and audited bulk action may move eligible assignments to a newer Active version; it must never silently rewrite submitted/approved requests.

### Request creation

1. Search for the student within the active academic year.
2. Backend returns a `request-context` containing the active student fee assignment, snapshotted policy version, eligible fee heads/due rows, remaining limits, and available active Concession Types.
3. If the assignment has no policy snapshot, return a stable `ConcessionPolicyNotConfigured` error and setup guidance.
4. Staff chooses a Concession Type. The UI loads that Type's mandatory and optional documents.
5. Staff enters an amount/percentage only if the policy value source allows it.
6. UI sends the proposed input to the preview endpoint.
7. Backend recalculates eligible balances, caps, rounding, and allocation; returns totals, warnings, and a short-lived context version/hash.
8. Staff saves a Draft or submits the request.

Never accept a browser-calculated `NetAmount` as a financial fact.

### Submission and approval

Recommended request lifecycle:

`Draft -> Submitted -> UnderReview -> Approved | Rejected | Withdrawn`

On submit, validate:

- Type is active and belongs to the request academic year.
- All mandatory Type documents are uploaded and valid; optional documents are allowed.
- Policy and assignment snapshot exist and belong to the same academic year.
- Request input is permitted by the policy.
- Eligible outstanding principal and caps still support the preview.
- Duplicate/pending/approved request rules are satisfied.

On approve, inside one transaction:

1. Lock or concurrency-check the request and affected due rows.
2. Enforce maker-checker using actor ID and permissions, never role-name exceptions.
3. Recalculate from current due balances and the snapshotted policy version.
4. Reject stale or now-invalid previews with a stable conflict response and a refreshed calculation.
5. Permit an approver to reduce the requested amount; increasing it requires an explicit override and a new preview.
6. Create the immutable grant and allocations.
7. Update separate concession totals on due rows.
8. Write Finance audit and outbox events.

### Collections, receipts, reporting, and corrections

Every due/receipt view must keep these monetary facts distinct:

```text
Gross principal
- Approved concession
- Approved adjustment
+ Late fee
- Paid amount
= Outstanding amount
```

Reports must expose Policy Code/Version, Concession Type, Request ID, Grant ID, student, fee structure, fee head, due period, requested/approved/reversed amounts, requester, approver, and timestamps.

An approved concession is never edited or cancelled. Corrections use a reversal record linked to the original grant, with reason, permission, allocations, audit event, and protection against reversing amounts already made impossible by later settlements.

## Calculation contract

Use decimal arithmetic end to end.

```text
EligibleRows = unpaid principal rows
               within policy fee-head scope
               and policy due timing

EligibleBase = sum(max(0, OriginalPrincipal
                          - ApprovedConcession
                          - ApprovedAdjustment
                          - PaidPrincipal))

RawConcession =
  Percentage: EligibleBase * percentage / 100
  Fixed:      requested-or-policy fixed amount
  FullWaiver: EligibleBase

Capped = min(RawConcession,
             EligibleBase,
             remaining per-request cap,
             remaining student/year cap)

ApprovedTotal = apply documented rounding to Capped
```

Allocation must be deterministic and its final row must absorb any rounding remainder without exceeding that row's outstanding principal.

## Backend data model

Names may follow repository conventions, but the relationships are required.

| Aggregate/table | Required additions |
|---|---|
| `ConcessionPolicy` | TenantId, AcademicYearId, Code, Name, Description, Status (Draft/Active/Archived), current version, concurrency stamp |
| `FinancePolicyVersion` or typed version | PolicyId, version number, immutable rule snapshot plus queryable rule columns, status-at-capture, changed metadata |
| Fee-structure version | Nullable ConcessionPolicyId and ConcessionPolicyVersionId; enforce one attachment |
| Student fee assignment | Snapshotted ConcessionPolicyId and ConcessionPolicyVersionId |
| `ConcessionRequest` | StudentFeeAssignmentId, ConcessionTypeId, PolicyId, PolicyVersionId, requested input, calculated totals, calculation snapshot/hash, lifecycle metadata |
| Concession grant/allocation | RequestId, DueFeeId, FeeHeadId, approved amount, policy version, approver, applied time, reversal metadata |
| `DueFee` | Decimal OriginalPrincipal, ConcessionAmount, AdjustmentAmount, PaidPrincipal, LateFeeAmount, OutstandingAmount or an equivalent derived invariant |

Database constraints/indexes:

- Unique normalized policy code per tenant + academic year.
- Unique normalized policy name per tenant + academic year.
- One version number per policy.
- One current version pointer per policy; every prior captured version remains immutable.
- One Concession Policy version per fee-structure version.
- Request references an assignment, Type, policy, and version from the same tenant/year.
- Allocation amount is positive and cannot exceed the due row's available balance.
- Index requests by tenant/year/student/status and policy version.

## Dedicated API surface

Do not reuse a loosely typed generic `/finance-policies` contract.

### Policy setup

```text
GET    /api/finance/concession-policies
GET    /api/finance/concession-policies/{id}
POST   /api/finance/concession-policies
PUT    /api/finance/concession-policies/{id}
POST   /api/finance/concession-policies/{id}/clone
POST   /api/finance/concession-policies/{id}/simulate
GET    /api/finance/concession-policies/{id}/versions
DELETE /api/finance/concession-policies/{id}             # archive, never hard-delete history
POST   /api/finance/concession-policies/{id}/unarchive   # restores as Draft
```

### Fee structure and assignment

```text
GET /api/finance/fee-structures/{id}/available-concession-policies
PUT /api/finance/fee-structures/{id}/concession-policy
GET /api/finance/student-fee-assignments/{id}  # includes policy snapshot summary
```

### Student requests

```text
GET  /api/finance/concession-requests/context?studentId=...&academicYearId=...
POST /api/finance/concession-requests/preview
POST /api/finance/concession-requests
PUT  /api/finance/concession-requests/{id}
POST /api/finance/concession-requests/{id}/submit
POST /api/finance/concession-requests/{id}/approve
POST /api/finance/concession-requests/{id}/reject
POST /api/finance/concession-requests/{id}/withdraw
POST /api/finance/concession-requests/{id}/reverse
```

Every write returns the persisted resource, concurrency token, Status, Version, and calculation/audit identifiers. Errors use stable business codes and field metadata.

## Permission model

The current `Policies_ConcessionPolicy` View/Add/Delete family is insufficient. Use action-specific permissions:

```text
Finance.Policies.ConcessionPolicy.View
Finance.Policies.ConcessionPolicy.Add
Finance.Policies.ConcessionPolicy.Edit
Finance.Policies.ConcessionPolicy.Clone
Finance.Policies.ConcessionPolicy.Simulate
Finance.Policies.ConcessionPolicy.Archive
Finance.Policies.ConcessionPolicy.Unarchive
Finance.Policies.ConcessionPolicy.ViewVersions
Finance.Policies.ConcessionPolicy.ManageAttachment

Finance.Fees.ConcessionRequest.View
Finance.Fees.ConcessionRequest.Create
Finance.Fees.ConcessionRequest.EditDraft
Finance.Fees.ConcessionRequest.Submit
Finance.Fees.ConcessionRequest.Withdraw
Finance.Fees.ConcessionRequest.Approve
Finance.Fees.ConcessionRequest.ApproveAboveThreshold
Finance.Fees.ConcessionRequest.Reject
Finance.Fees.ConcessionRequest.Reverse
```

Frontend route, button, controller, app-service, and domain authorization must use the same permission family. Do not alias Concession Policy to Fee Collections.

Suggested segregation:

- Fee setup staff: View, Add, Edit, Clone, Simulate, ManageAttachment.
- Finance manager/principal: Archive, Unarchive, ApproveAboveThreshold, Reverse.
- Request maker: View, Create, EditDraft, Submit, Withdraw.
- Request checker: View, Approve, Reject.

## Stable business errors

At minimum define:

| Code | Meaning |
|---|---|
| `ConcessionPolicyNotConfigured` | Student assignment has no policy snapshot |
| `ConcessionPolicyNotActive` | Policy selected for a new attachment/assignment is not Active |
| `ConcessionPolicyArchiveRequiresWorkflow` | Archived cannot be sent through create/update Status |
| `ArchivedConcessionPolicyCannotBeEdited` | An Archived policy was sent to update |
| `ConcessionPolicyAcademicYearMismatch` | Policy, structure, assignment, Type, or request years differ |
| `ConcessionPolicyFeeHeadMismatch` | Requested head is not eligible under policy/structure |
| `ConcessionPolicyLimitExceeded` | Request or annual cap would be exceeded |
| `ConcessionPolicyInputNotAllowed` | Request supplied an amount/method not permitted by the policy |
| `ConcessionRequestDocumentsMissing` | One or more mandatory Type documents are absent |
| `ConcessionRequestStalePreview` | Due balance or policy context changed since preview |
| `ConcessionRequestMakerCheckerViolation` | Creator attempted to approve without a permitted exception |
| `ConcessionRequestAlreadyDecided` | A terminal request was mutated |
| `ConcessionAllocationExceedsOutstanding` | An allocation exceeds current eligible principal |

## Migration and rollout

1. Introduce the typed aggregate, version storage, permissions, DTOs, preview engine, and audit events behind a feature flag.
2. Add the singular policy attachment to fee-structure versions.
3. Add the policy snapshot to student fee assignments and request context.
4. Backfill existing assignments only when one deterministic same-year Active attachment exists; otherwise leave the snapshot null and block new requests with setup guidance.
5. Preserve existing requests as `LegacyManual` calculation snapshots with a nullable policy version; do not invent historical policy meaning.
6. Move money fields and calculations to decimal and separate concession/adjustment facts before enabling policy-based approval broadly.
7. Replace role-name approval exceptions with explicit permissions.
8. Enable create/status/attach, then request preview, then approval/reversal after reconciliation testing.

## Required tests

### Domain and application

- Policy name/code uniqueness after normalization.
- No separate policy dates are required; policy, structure, assignment, Type, and request remain in the same academic year.
- No cross-tenant/year references.
- Archived policies cannot be edited; restoring sets Status to Draft.
- Active edits and Active-to-Draft transitions require impact preview/confirmation and capture a new immutable version snapshot.
- Exactly one attachment per fee-structure version.
- Assignment snapshots the exact policy version and does not auto-discount.
- Policy supersession does not change existing assignments or requests.
- Request Type is active and same-year; mandatory documents are enforced and optional documents remain optional.
- Percentage, fixed, full waiver, caps, rounding, and deterministic allocation.
- Paid and late-fee amounts are excluded.
- Multiple concession and annual-cap enforcement.
- Maker-checker and above-threshold permissions.
- Concurrent approval/payment conflict.
- Reversal integrity and audit/outbox events.

### Contract and frontend

- Typed serialization for every policy method and state.
- Preview output matches approval calculation for unchanged inputs.
- Stable error codes map to the correct fields/actions.
- Fee-structure selector lists only same-year Active policy versions.
- Assignment displays its snapshot read-only.
- Request never asks staff to select a policy.
- Request fields change correctly for fixed-policy versus request-entered value source.
- Missing policy, stale preview, cap, document, permission, and conflict journeys.

## Acceptance criteria

The flow is ready only when all of these are true:

- A policy contains no medium/class/division/student scope and no Concession Type.
- A fee-structure version has at most one Active same-year policy attachment.
- Student assignment snapshots that exact policy version and leaves gross fees unchanged.
- Request staff select a Concession Type, never a policy.
- Type document definitions drive mandatory and optional uploads.
- The backend calculates preview and approval from current eligible due rows.
- Paid fees, refunds, wallets, credit notes, and late-fee waivers are outside the ordinary policy form.
- Policy version snapshots and approved allocations are immutable and fully auditable.
- Permissions match across frontend and backend.
- Collection, receipt, audit, reversal, and reporting screens preserve separate monetary facts.

## Related documentation

- [Finance product behavior](../product.md)
- [Finance API families](../api.md)
- [Finance workflows](../workflows.md)
- [Finance testing](../testing.md)
- [Finance Engineering Constitution](./constitution.md)
- [Frontend Concession Policy review](../../../../../Shikshamitra-FE/docs/finance/references/concession-policy-frontend-review.md)
