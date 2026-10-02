---
module: finance
domain: charge-management
content_type: Reference
status: active
updated_on: 2026-10-02
owner: backend-team
audience: humans-and-agents
---

# Charge management end to end

TL;DR: the implemented flow creates scoped per-student ad hoc charges, supports maker-checker approval with optimistic concurrency, exposes approved balances to transaction-safe Fee Collection, creates charge receipts, and supports audited voiding of uncollected obligations. Charge categories now require a stable income-category/account-head snapshot. Paid charge refunds, automatic ledger posting, source-command idempotency, outbox notifications, and full reporting parity remain release gaps.

The frontend companion is [Charge Management frontend flow](../../../../../Shikshamitra-FE/docs/finance/references/charge-management-frontend-flow.md).
The executable QA matrix is [Charge Management end-to-end test plan](./charge-management-e2e-test-plan.md).

## Product boundary

Charge Management owns exceptional or source-system obligations that are not part of a published Fee Structure, for example damage, event, transport, library, or other one-off charges.

| Concern | Owner | Charge behavior |
|---|---|---|
| Recurring tuition and installment demand | Fee Structure, Payment Term, and Student Fee Assignment | Do not duplicate it as a charge |
| Ad hoc student obligation | Charge Management | Create one immutable student line per resolved student |
| Late fee and collection rules | Collection Policy | Current rules apply to `DueFee`, not charge principal; any future applicability must be explicit and versioned |
| Concession and proration | Their policy modules | Do not reduce charges by default |
| Charge waiver | Fee Collection | Charge Management stores no waiver setting; an authorized collector may waive a qualifying approved outstanding line in full with a mandatory reason |
| Cash return | Refunds | Current refund scope excludes charge receipts; a charge-refund contract is required |
| Settlement | Fee Collection | Collect approved outstanding charge lines and issue a separate charge receipt |
| Revenue classification | Accounts | Every category needs a stable income-category/account mapping before automatic posting is reliable |

Fee Structures and Charge Management meet at student eligibility and in the collection experience, receipts, accounts, audit, and reports. A student must have a fee-structure assignment for the charge's academic year before an ad hoc charge can be created, but the charge itself is not attached to that Fee Structure.

## Current shipped flow

```text
Charge Category
    -> create ChargeRequest + resolved per-student Charge lines
    -> Pending for every category (approval is mandatory)
    -> approve/reject selected Pending lines
    -> approved line appears in Fee Collection
    -> collection validates outstanding amount and stores ChargeCollectionRecord
    -> separate charge receipt is issued
    -> Completed when every approved line has zero outstanding balance
    -> reconciliation reversal excludes reversed allocations and reopens In Process when necessary
```

Transport billing already posts a run through `ChargeRequestManager` and stores Finance charge request/line IDs. Library maintains a separate charge model and is not automatically settled through Finance. Any source-module integration must use the target origin and idempotency contract below rather than relying on notes or duplicate charge creation.

## Four-state batch lifecycle

| Value | Workflow status | Server rule |
|---|---|---|
| 0 | Pending | No lines yet, or at least one student line awaits a decision |
| 1 | In Process | All decisions are made and an approved line has outstanding money |
| 2 | Completed | At least one approved line exists and all approved lines have zero outstanding money |
| 3 | Rejected | All lines are rejected or voided; their original per-line decision remains visible |

Every new batch and newly added audience line starts Pending, including categories whose legacy `isApprovalRequired` flag is false. Editing a pending batch never auto-approves it. The existing Approve and Reject permissions, scope checks, independent checker and concurrency stamp remain authoritative. Approve/reject with no remaining pending lines fails. Mixed partial decisions keep the batch Pending until every decision is made. Rejected/voided lines do not create collection requirements.

`workflowStatus` is a read-only projection shared by create/update, decision responses, detail, student-line detail and inbox. `GET api/charges` accepts `status=0..3`; unknown values are rejected. `displayStatus` mirrors the batch workflow for compatibility. `status` on detail/commands/student lines continues to mean the original approval enum (Pending=0, Approved=1, Rejected=2, Voided=3); consumers must not use it as the batch lifecycle. Inbox totals are `pendingCount`, `inProcessCount`, `completedCount` and `rejectedCount`; `processedCount` remains a compatibility alias for `inProcessCount` at response level and means reviewed student lines at item level.

Outstanding is calculated per approved line as `max(0, principal - non-reversed collection allocations - applied waiver amounts)`, using decimal totals. Waivers remain separate from collected cash in student detail. A partial payment cannot complete a batch while any approved student balance remains; a reversal reopens it through linked correction evidence. Existing collection semantics count accepted provisional allocations until reversal, so Completed describes obligation settlement, not bank reconciliation. Charge-refund reinstatement remains unsupported by the existing charge collection contract.

The lifecycle always considers the entire charge batch even when class/division filters select matching requests. Counts, filtering and pagination are computed from the same server resolver before pagination; no client-computed completion or manual completion endpoint is introduced. The projection applies to existing history without a schema migration or rewriting approval/payment evidence. Source modules using `ChargeRequestManager`, including Transport, now create Pending obligations and require the normal checker flow.

The affected chain is category/account snapshot -> student eligibility/audience -> pending obligation -> checker decision -> collection/waiver -> receipt/reversal evidence -> derived inbox. No monetary amount, payment policy, receipt format, account posting rule or permission definition changes. Writes retain their existing unit-of-work, audit and concurrency boundaries; a failed decision does not produce an approved event. Status is derived on reads so there is no stored completion flag that can become stale.

## Current HTTP and permission contract

The controller base route is `api/charges`.

| Operation | Route | Required backend permission |
|---|---|---|
| Create a batch | `POST api/charges?academicYearId={id}` | `Finance.Fees.Charges.Add` |
| List batches | `GET api/charges?academicYearId={id}` | `Finance.Fees.Charges.View` |
| List student lines | `GET api/charges/lines?academicYearId={id}` | `Finance.Fees.Charges.View` |
| Read batch | `GET api/charges/{chargeId}` | `Finance.Fees.Charges.View` |
| Update pending batch | `PUT api/charges/{chargeId}` | `Finance.Fees.Charges.Update` |
| Approve lines | `POST api/charges/{chargeId}/approve` | `Finance.Fees.Charges.Approve` |
| Reject lines | `POST api/charges/{chargeId}/reject` | `Finance.Fees.Charges.Reject` |
| Void approved uncollected lines | `POST api/charges/{chargeId}/void` | `Finance.Fees.Charges.Void` |
| Delete pending batch | `DELETE api/charges/{chargeId}` | `Finance.Fees.Charges.Delete` |

Do not use module visibility, route access, or a role name as an action grant. The backend returns `availableActions`, and the frontend intersects those with its exact action permissions.

## Implementation status (2026-09-21)

| Phase | Delivered | Remaining |
|---|---|---|
| Phase 0: correctness and access | Required academic year, server-side academic/student scope, wholly-pending update rule, maker-checker enforcement, exact action permissions, concurrency stamps, and serializable/idempotent collection | Broader integration coverage for permission denial, stale writes, and concurrent collection |
| Phase 1: financial lifecycle | Filter/search/sort/paging, server payment-channel policy enforcement, mandatory active same-year income mapping, per-line accounting snapshot, category status governance, collected/outstanding projection, and audited void of uncollected approved lines | Paid charge refund/correction, automatic ledger journals, source origin/idempotency contract, and transactional outbox |
| Phase 2: operability | Typed core charge client, server paging/search, one canonical route, no fake line-delete action, permission-driven controls, void guidance, and end-to-end documentation | Full immutable timeline, notifications, exports/report parity, receipt/account reconciliation tests, and complete browser coverage |

The remaining items are deliberately not simulated in the UI. Collected lines are rejected by the void endpoint and must use the existing reconciliation/reversal path until a charge-refund contract is implemented.

## Integration status

| Integration | Current state | Required target |
|---|---|---|
| Charge Category | Active category and approval flag are resolved | Also enforce same academic year, lifecycle/version, and revenue mapping |
| Student scope | Audience is resolved to active students at create time | Intersect every list/read/mutation with the caller's server-owned scope and retain the audience snapshot |
| Fee Structure | Same-year student assignment is required for charge eligibility | Filter audience candidates and revalidate create/update against the canonical assignment; do not attach the charge line to the Fee Structure |
| Collection Policy | Late-fee rules operate on `DueFee`; `allowOnlineCollection` is surfaced through fee assignment | Decide and document charge applicability; enforce payment-channel eligibility on the server |
| Concession/Proration | Operate on scheduled fee dues | Keep charges excluded unless a new versioned rule explicitly includes them |
| Fee Collection | Approved charge lines can be partially or fully collected | Add atomic outstanding checks, concurrency protection, idempotency, and clear allocation ordering |
| Receipt | A charge receipt is created | Verify final/provisional charge templates, line provenance, void/refund links, and preview/export parity |
| Refund | Fee receipt scope only | Add an explicit charge-refund/correction design or declare the feature unsupported in UI and API |
| Reconciliation | Reversed transaction allocations stop counting as paid | Surface linked reversal state and audit evidence on the charge timeline |
| Accounts | No category-to-income mapping on `ChargeCategory` | Require an active same-year Income Category/default Income Account Head snapshot |
| Audit/Reports | Operational aggregates exist | Add charge lifecycle, origin, category, approval, collection, correction, and export parity |
| Notifications | Approved event handler logs only | Add transactional outbox consumers and user-scoped notifications if product-approved |
| Transport | Stores Finance request/line IDs | Add an explicit integration command, source permission, origin ID, correlation, and replay protection |
| Library/Hostel | No canonical Finance mapping found | Integrate only through the same source contract; never infer settlement from similarly named records |

## Release gap register

| ID | Priority | Gap | Completion condition |
|---|---|---|---|
| CM-01 | P0 | Batch list drops `academicYearId`; line list does not filter it; the active frontend omits it | Both queries require and filter the selected tenant/year; cross-year tests pass |
| CM-02 | P0 | List/read/update/approve/reject/delete do not consistently intersect caller scope | Every operation performs tenant, academic-context, and student-scope authorization before returning or mutating data |
| CM-03 | P0 | Updating a mixed batch changes the header while approved/rejected lines retain old values | Updates are limited to a wholly pending version, or split into an explicit amendment workflow |
| CM-04 | P0 | Frontend controls are status-gated but not action-permission-gated | Each action uses its exact backend permission and server-provided `availableActions` |
| CM-05 | P0 | Different retries or concurrent collections can over-allocate one charge | Collection locks or atomically compares outstanding balances; commands have actor-scoped idempotency and stamps |
| CM-06 | P1 | Processed classification uses any processed line; search/sort are ignored; frontend paging is incomplete | Batch status is derived consistently, all filters are honored, and server paging is visible in UI |
| CM-07 | P1 | Policy applicability and online-collection enforcement are ambiguous | A product decision is encoded in a versioned rule and payment-channel eligibility is rechecked server-side |
| CM-08 | P1 | Charge categories have no revenue classification | Category/version snapshots an Income Category and resolved account head; postings and reports reconcile |
| CM-09 | P1 | Approved or paid charges have no charge-specific cancel, void, waive, or refund lifecycle | Legal correction transitions preserve original lines, reasons, actors, receipts, and accounting reversals |
| CM-10 | P1 | Source modules have no standard origin/idempotency contract | Unique source identity and correlation prevent duplicate charges and support drill-through |
| CM-11 | P1 | Approval events can be repeated and auto-approved create/update does not emit equivalent events | Transactional outbox emits each state change exactly once after commit |
| CM-12 | P1 | Charge Category governance and permission coverage are incomplete | Exact View/Add/Edit/ChangeStatus/Delete rules, dependency checks, history, and concurrency are enforced |
| CM-13 | P2 | Money and date DTOs mix `decimal`/`double` and `DateTime`/`DateTimeOffset`; clients are mostly `any` | One decimal-safe wire convention, ISO offset date contract, and generated/typed clients are used |
| CM-14 | P2 | Audit, reporting, notifications, and receipt rendering lack end-to-end provenance tests | Preview/export/receipt/audit views reconcile to the same charge allocations and source identity |
| CM-15 | P2 | Duplicate, orphaned, and placeholder frontend surfaces obscure the supported workflow | One routed workspace remains; fake actions and obsolete clients are removed |

## Target domain contract

### Batch and line identity

A batch is an authoring and approval envelope. Money is owed at the student-line level. The batch status is derived from its lines; it must not be independently editable.

Add or preserve these fields:

| Area | Required data |
|---|---|
| Tenant and context | tenant, academic year, board/medium/class/division audience snapshot |
| Category snapshot | category ID, name, approval rule, income category, account head, version |
| Origin | `sourceModule`, `sourceEntityType`, `sourceEntityId`, `externalReference`, `correlationId` |
| Command safety | idempotency key, request fingerprint, concurrency stamp |
| Obligation | original amount, effective/due date, priority, purpose, notes |
| Lifecycle | creator, submitter, checker, reasons, timestamps, immutable timeline |
| Settlement | collected, outstanding, adjusted/waived, refunded/reversed amounts derived from linked records |

Enforce a unique origin key per tenant, academic year, source module, and source entity where an external system posts the charge.

### State model

```text
Pending
  -> In Process -> Completed
  -> Rejected

Completed -> In Process only through a linked payment reversal restoring outstanding money
Student lines retain Pending/Approved/Rejected/Voided decision evidence
Approved and unpaid -> Voided with reason and checker (existing correction flow)
```

The current API always begins at Pending. The legacy category approval flag no longer bypasses the checker. Introduce Draft/Submit only if the product needs resumable authoring. Whether Draft exists or not, line state remains authoritative and the server returns `availableActions`.

### Monetary invariants

```text
effective obligation = approved principal - applied waivers/voids
cleared collection   = non-reversed cleared charge allocations
outstanding          = effective obligation - cleared collection + charge-refund reinstatement
```

- Charge principal must be greater than zero, must not exceed `9,999,999.00`, and supports at most two decimal places. The API rejects excess precision rather than rounding it implicitly.
- All downstream values use the platform's decimal currency precision and rounding policy.
- `0 <= collection allocation <= outstanding` is checked in the same transaction that writes it.
- The sum of receipt charge allocations equals the charge portion of the transaction.
- Reversal and refund records reference the original allocation; they do not delete it.
- Batch totals and statuses are projections of line records, never separately trusted inputs.

## Target frontend/backend flow

1. **Open workspace.** Frontend requires `Finance.Fees.Charges.View`, selected academic year, and server-owned academic scope. Backend returns typed filters, category options, permissions, and allowed actions.
2. **Resolve audience.** The author selects an explicit mode: all eligible students in scope or selected students. Eligible students are enrolled in the selected academic scope and have a fee-structure assignment for that academic year. A server preview returns the resolved count, exclusions, category/account/policy snapshot, total amount, and fingerprint. Empty selection never silently changes to all students.
3. **Create safely.** Frontend sends the preview fingerprint and one idempotency key. Backend re-resolves scope, validates category year/status, enforces the maximum batch size, and creates one line per student atomically.
4. **Review.** Detail shows immutable scope, origin, amount, line states, creator, and timeline. Update is allowed only when the server returns Edit and the submitted concurrency stamp matches.
5. **Approve or reject.** The checker selects pending lines and submits a reason where required. Backend enforces the exact action permission, maker-checker without role-name exceptions, scope, line stamps, and one legal transition per line.
6. **Collect or waive.** Student collection reads approved outstanding charge lines with due date, category, origin, and account classification. It atomically allocates payment or, when the user has the dedicated permission, uses a reason-only row toggle to waive the full live remaining balance. Fees and charges remain separate receipt scopes even when paid in one user journey.
7. **Post and receipt.** After commit, an outbox publishes collection/account/audit/notification events. The charge receipt lists the exact allocations and links to the source charge.
8. **Correct.** Incorrect unpaid obligations use void rules; paid obligations use linked reversal/refund commands. An incorrect posted waiver uses a linked reversal rather than editing history. UI previews the monetary impact and requires reason, permission, stamp, and idempotency key.
9. **Report.** Operational view, audit, receipt, accounts, reconciliation, and export derive from the same immutable lines and allocations.

## Required product decisions

These are stop conditions for money-changing implementation, not frontend defaults.

| Decision | Recommended default |
|---|---|
| Do Collection Policy late fees apply to charges? | No; create an explicit Charge Category rule if needed |
| Does `allowOnlineCollection` apply to charges? | Yes at transaction/payment-channel level, enforced by backend |
| Can concessions or proration reduce charges? | No |
| Can a charge be waived? | Any qualifying approved positive outstanding student line may be waived in full from Fee Collection with a mandatory reason; Charge Management has no waiver setting |
| Are partial charge payments allowed? | Yes, with atomic outstanding checks |
| Can an author approve their own request? | No; use a dedicated audited override permission only if legally required |
| What happens after incorrect unpaid approval? | Void/cancel with checker and reason |
| What happens after payment? | Linked reversal/refund; never edit or delete the original line |
| Do source modules post one batch or one line? | One batch per source operation, one immutable line per student |
| Which account receives a charge? | Category's snapshotted Income Category/default Income Account Head |

## Delivery sequence

### Phase 0: correctness and access

- Fix CM-01 through CM-05.
- Add permission-denial, tenant/year/scope, mixed-batch, concurrent-collection, and idempotency tests.
- Remove fake frontend actions and hide all unauthorized actions.

### Phase 1: complete the financial lifecycle

- Fix filtering/paging, policy enforcement, account mapping, correction/refund, source integration, events, and Charge Category governance.
- Backfill mappings only through an approved migration with an exception report; never guess an account.

### Phase 2: operability

- Replace `any` contracts, consolidate routes, add timelines/notifications, and prove report/receipt/export parity.
- Add browser coverage for author, checker, collector, reversal/refund, unauthorized, stale, and retry paths.

## Release gates

Charge Management is releasable only when all P0 items are closed and the following evidence exists:

- exact action-permission and server `availableActions` parity;
- tenant, academic-year, and student-scope isolation tests for every read and mutation;
- mixed-line batch and concurrent approve/reject/collect tests;
- idempotent retry tests with same-key/same-payload replay and mismatch rejection;
- collection, receipt, reversal/refund, account, audit, and report reconciliation;
- transport/source replay test proving one source operation cannot create duplicate charges;
- frontend unit/contract tests plus a browser journey across create, approve, collect, and correct;
- Finance docs validation and clean diff checks.

## Source trace

The current-state findings were traced from these source areas:

- `src/ShikshaMitra.HttpApi/Controllers/Finance/Fees/Charges/ChargeController.cs`
- `src/ShikshaMitra.Application/Finance/Fees/Charges/**`
- `src/ShikshaMitra.Domain/Finance/Fees/Charges/**`
- `src/ShikshaMitra.EntityFrameworkCore/Finance/Fees/Charges/**`
- `src/ShikshaMitra.Application/Finance/Fees/Collections/**`
- `src/ShikshaMitra.Domain/Finance/Fees/Receipts/**`
- `src/ShikshaMitra.Application/Finance/Accounts/Reconciliation/**`
- `src/ShikshaMitra.Application/Transport/**`
- `test/ShikshaMitra.*.Tests/Finance/**`

Source code remains authoritative for shipped behavior. This document is authoritative for the reviewed gap register and target cross-stack contract until superseded by an approved product/API change.
