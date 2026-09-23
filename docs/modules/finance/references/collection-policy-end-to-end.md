---
module: finance
domain: collection-policy
content_type: Reference
status: active
updated_on: 2026-09-23
owner: backend-team
audience: humans-and-agents
---

# Operate collection policies end to end

TL;DR: a collection policy is an academic-year financial rule set that controls late-fee calculation, per-rule waiver eligibility, online collection, temporary exceptions, and approved student late-fee exceptions.
The target lifecycle follows Concession Policy: `Draft -> Active -> Archived`, an archived policy restores to `Draft`, only an Active policy can be attached, and every student assignment stores an immutable policy version.
School staff configure the policy once, verify it with an impact preview, activate it, and attach it to fee structures; routine collection staff see only the server-calculated result and explanation.

> This page is the implementation-ready target contract.
> It intentionally records behavior that is not fully shipped in the current Collection Policy implementation.
> The final section lists those gaps so current behavior is never mistaken for the target behavior.
> Collection Policy stores only `Allow waiver` on each effective late-fee rule. Waiver amount and reason are applied immediately in Fee Collection; there is no separate Waiver module or approval flow. See [Fee Collection waiver end to end](./fee-collection-waiver-end-to-end.md).

## Goals and boundaries

The policy must make these questions deterministic:

- Which fee heads can attract a late fee?
- When does a late fee start?
- How is it calculated and capped?
- May a balance produced by this late-fee rule be waived in Fee Collection?
- Is online payment allowed?
- Which policy version governed a student and a transaction?

The policy does not define fee amounts, installment schedules, concessions on principal, refunds, payment-mode masters, or accounting ledgers.
Those modules consume the policy result but remain their own source of truth.

## Confirmed target decisions

| Decision | Target behavior |
|---|---|
| Scope | One tenant and one academic year |
| Status | `Draft`, `Active`, `Archived` |
| New policy | Starts as Draft by default |
| Activation | Full server validation is required before Draft can become Active |
| Archive | Explicit action; never accepted as a create/edit status |
| Restore | Archived returns to Draft, never directly to Active |
| Attachment | Only an Active policy from the fee structure's academic year can be attached |
| Default | At most one Active default per academic year; it preselects the policy for staff but the saved fee structure stores an explicit attachment |
| Versioning | Every create, edit, status change, and clone writes an immutable version/audit snapshot |
| Historical use | Fee structure and student assignment store policy ID plus exact policy-version ID |
| Runtime math | Server is authoritative; frontend calculators are previews only |
| Fee-head mismatch | Use the intersection of policy heads and fee-structure heads; zero overlap is allowed and makes that feature inactive for the structure |
| Policy-only heads | Allowed because one policy may serve multiple structures; show a warning for the current structure |
| Structure-only heads | Collect normally but do not receive that policy's late fee |
| No-policy case | New fee structures and new student assignments are blocked; legacy data needs a controlled migration path |
| Policy edit | Existing assignments and posted transactions keep their captured version; only future assignments use the new version |
| Policy deletion | Financial policies are archived, not hard-deleted after creation |

## Keep the workflow simple for school staff

Use progressive disclosure in the UI.

| Staff need | Default experience | Advanced controls |
|---|---|---|
| Create a normal policy | Name, eligible heads, grace days, one calculation method | Caps, accounting, per-head overrides |
| Attach to a structure | Default policy is preselected and an impact summary appears | Change policy or inspect version |
| Collect a payment | Due, calculated late fee, applied waiver, and payable total are already calculated | Open calculation explanation or apply an eligible waiver |
| Handle a closure/disaster | Create one exception window | Student-specific override only for genuine individual exceptions |

Recommended setup order for a school:

1. Configure the academic year, fee heads, payment terms, and accounting income head.
2. Create a Collection Policy as Draft.
3. Configure late-fee rules, each rule's `Allow waiver` toggle, and online-payment rules.
4. Run simulation and fee-structure impact preview.
5. Resolve every blocking validation and review warnings.
6. Activate the policy.
7. Optionally select it as the academic-year default; this atomically replaces the previous Active Collection Policy default.
8. Attach it to relevant fee structures; the default policy may be preselected.
9. Assign fee structures to students; capture the immutable policy version.
10. Collect fees using server-calculated amounts.
11. When an eligible late-fee balance needs reduction, apply amount plus reason directly from Fee Collection.

## Status lifecycle based on Concession Policy

```text
Create
  -> Draft
      -> edit / simulate / clone
      -> activate after validation
  -> Active
      -> attach to fee structures
      -> use for new student assignments
      -> edit creates a new version for future assignments
      -> archive only after default/attachment checks pass
  -> Archived
      -> view history
      -> clone as Draft
      -> restore to Draft for review
```

| Current status | Action | Next status | Rule |
|---|---|---|---|
| New | Save draft | Draft | Partial but syntactically safe configuration may be saved |
| New/Draft | Activate | Active | All references and business combinations must pass server validation |
| Draft | Edit | Draft | Version and audit record are created |
| Draft | Archive | Archived | Allowed when not attached and not default |
| Active | Edit | Active | Creates a new immutable version; historical assignments remain unchanged |
| Active | Move to Draft | Draft | Block when attached or used by downstream assignments |
| Active | Archive | Archived | Block while default or attached; staff must replace/detach first |
| Archived | Restore | Draft | Requires review and reactivation |
| Archived | Edit/attach/use | Blocked | Archived configuration is read-only |
| Any | Clone | Draft | New code, no attachments, not default, version 1 |

The API may use create/update with `status = Draft|Active`, as Concession Policy does.
Archive and unarchive remain dedicated commands.
The UI presents explicit **Save Draft**, **Activate**, **Archive**, and **Restore to Draft** actions rather than an editable status dropdown.

## End-to-end business flow

```text
Academic year + fee heads + payment terms
  -> Collection Policy Draft
  -> server validation + simulation + impact preview
  -> Active Collection Policy + immutable version
  -> Active policy attached to Fee Structure + exact version
  -> Student Fee Structure assignment snapshots policy + version
  -> DueFee schedule is generated with fee-head and due-date context
  -> Collection screen requests effective server-calculated rules
  -> policy version + fee-head scope + overrides + exception window are resolved
  -> grace period + formula + caps + rounding calculate gross late fee
  -> prior paid/waived late fee is deducted to get remaining late fee
  -> collector allocates principal, late fee, and approved adjustments
  -> transaction, allocations, receipt, ledger, accounting, audit, and reports commit together
  -> reversal/refund references original transaction and captured policy version
```

## Policy form and field contract

### General section

| Field | Required | Target rule | Staff-facing behavior |
|---|---:|---|---|
| Academic year | Yes | Immutable after create; valid tenant-owned year | Preselected from current year; changing year on clone reloads references |
| Policy name | Yes | Trimmed, 3-128 characters, unique per tenant/year case-insensitively | Example: `Standard Late Fee 2026-27` |
| Policy code | Yes | Trimmed, upper-case, 1-32 characters, unique per tenant/year; server may generate | Displayed as stable reference, not editable after first use |
| Description | No | Maximum 500 characters | Plain-language purpose |
| Status | Yes | Create/edit accepts Draft or Active only | Use action buttons, not a free dropdown |
| Default | Yes | Boolean; at most one Active default per year | Explain that default preselects, it does not override saved attachments |
Collection Policy does not expose editable effective-from or effective-to fields.
The backend derives and stores the academic-year start and end dates for version snapshots and historical calculations.

### Late-fee section

| Field | Required when | Target rule |
|---|---|---|
| Enable late fee | Always | When false, clear/ignore late-fee formula fields and return zero |
| Grace-period type | Late fee enabled | `Calendar`; expose `WorkingDay` only after a holiday-calendar source is integrated |
| Grace-period days | Late fee enabled | Integer 0-365 |
| Late-fee fee heads | Late fee enabled | At least one valid, active fee head from the same tenant/year |
| Calculation type | Late fee enabled | `Flat`, `Percentage`, `PerDay`, `PerWeek`, `PerMonth`, `Compound`, `Slab` |
| Flat amount | Type = Flat | Greater than zero, currency precision |
| Percentage | Type = Percentage/Compound | Greater than zero and at most 100 |
| Percentage base | Percentage/Compound | `OutstandingAmount`, `OriginalFee`, or approved meaning of `RemainingBalance` |
| Amount per day/week/month | Matching type | Greater than zero, currency precision |
| Compound frequency | Compound | `Daily`, `Weekly`, `Monthly` |
| Slabs | Slab | At least one; start at day 1 after grace; ordered; no gap/overlap; last slab may be open-ended |
| Rounding | Late fee enabled | `None`, `RoundUp`, `RoundDown`, `NearestRupee` |
| Maximum late-fee amount | No | Positive; final fee cannot exceed it |
| Maximum charging days | No | Integer greater than zero; accrual freezes after this many effective late days |
| Allow waiver | Late fee enabled | Boolean, default OFF; captured on each assessed late-fee line and used only to expose the direct Fee Collection action |
| Consider as income | Always | When true, an active tenant-owned income head is required |
| Late-fee income head | Consider as income = true | Must be active and valid for posting |

`RemainingBalance` does not currently have a distinct runtime meaning because the server supplies the current outstanding amount for both fields.
Keep it hidden or label it experimental until Finance approves a precise definition; do not let staff select an ambiguous calculation base.

### Per-fee-head override section

Each policy fee head can use one mode:

| Mode | Meaning | Validation |
|---|---|---|
| Inherit | Use policy-level rule, including its `Allow waiver` value | No nested custom rule |
| No late fee | Explicitly exempt this head | No nested custom rule |
| Custom rule | Use a different formula/cap and its own `Allow waiver` value for this head | Same formula validations as policy-level rule |

Override fee heads must be unique, active, same-year, and included in the policy's late-fee fee-head list.
The Fee Structure screen may display the resolved overrides but must not silently edit the shared policy.
Policy changes happen only in Collection Policy setup with the appropriate permission and audit trail.

### Waiver eligibility boundary

Collection Policy owns only the per-effective-rule `Allow waiver` boolean.
It does not own waiver amount, reason, request state, approval, rejection, thresholds, roles, caps, expiry, or a separate fee-head waiver selector.
The resolved boolean is captured with each assessed late-fee line so later policy edits do not reinterpret historical eligibility.
Fee Collection owns immediate application as defined in [Fee Collection waiver end to end](./fee-collection-waiver-end-to-end.md).

### Online-collection section

| Field | Target rule |
|---|---|
| Allow online collection | Server-enforced at quote creation, payment-order creation, callback posting, and retry; hiding the button alone is insufficient |

An active exception window with `DisableOnlineCollection` overrides policy `true` while the window applies.
Offline collection permissions remain independent.

## Fee-head matching at fee-structure attachment

Let:

- `S` = active fee heads in the fee structure;
- `L` = policy late-fee fee heads when late fee is enabled.

The effective late-fee scope is `S intersect L`.

| Condition | Result |
|---|---|
| Head exists in structure and policy | Feature applies to that head |
| Head exists only in structure | Fee is collectable; this policy does not apply late fee to it |
| Head exists only in policy | Ignore for this structure and show a warning |
| Late fee enabled and `S intersect L` is empty | Allow attachment; warn that no late fee applies to this structure |
| Referenced head is missing, inactive, cross-tenant, or wrong academic year | Block policy activation and attachment |
| Override points to a head outside `L` | Block activation |

Example:

```text
Policy late-fee heads: Tuition, Transport, Exam
Fee structure heads:   Tuition, Library

Covered:               Tuition
Collected without late fee: Library
Policy heads unused here: Transport, Exam
```

Exact list equality is not required because one policy may be reused across multiple fee structures.
The attachment screen must show three groups: **Covered**, **Not covered**, and **Not present in this structure**.

### Impact-preview response

Before saving a fee structure, the frontend calls a server impact endpoint with the policy, policy version, structure ID/draft heads, and schedule.
The response should contain:

- policy ID, version ID, name, code, status, and internal academic-year-derived validity dates;
- covered late-fee heads;
- structure heads without late fee;
- policy late-fee heads absent from the structure;
- resolved per-head overrides;
- schedule dates outside policy validity;
- blocking errors and non-blocking warnings;
- a stable `isAttachable` result.

The save endpoint repeats every check inside its transaction.

## Fee Structure frontend flow

1. Staff creates or edits a fee structure and adds fee heads/payment terms.
2. The Policies tab queries only Active Collection Policies for the structure's academic year.
3. The default policy is preselected only for a new structure; staff can change it.
4. Selecting a policy triggers the server impact preview.
5. The UI displays policy version, late-fee coverage, online status, and warnings.
6. Wrong-year, inactive, archived, missing-version, or invalid-reference results disable Save; zero overlap remains attachable and displays a feature-specific warning.
7. Saving stores both `collectionPolicyId` and `collectionPolicyVersionId`.
8. Changing fee heads, payment terms, or policy invalidates the preview and requires recalculation.
9. Editing the shared policy is not available from Fee Structure; provide a read-only details link.

For a deliberate no-late-fee setup, staff attaches an Active policy with late fee disabled instead of leaving policy behavior implicit.

## Student assignment and immutable snapshots

Student assignment repeats these checks:

- policy and version are attached;
- policy is Active and same academic year;
- version belongs to that policy and academic year;
- effective fee-head scopes are recomputed by intersection;
- the generated schedule belongs to the policy's academic year;
- referenced fee heads and income head remain valid for new use.

The assignment stores:

- Collection Policy ID;
- Finance Policy Version ID;
- effective late-fee fee-head IDs;
- resolved per-fee-head overrides or a complete immutable snapshot;
- assignment timestamp and actor.

Generated `DueFee` rows retain the assignment reference and fee-head/due-date facts needed to reproduce the calculation.
Do not replace a non-empty historical snapshot during repair or migration.

## Runtime resolution order

For each student/due line, resolve in this order:

1. The Collection Policy version captured by the student's fee-structure assignment.
2. Policy status is not reinterpreted for historical assignments; archiving stops new use but does not erase captured rules.
3. Confirm the due line's fee head is in the captured effective policy scope.
4. Apply student override: late-fee exemption or custom grace period.
5. Apply the single deterministic exception window for the date and scope.
6. Apply per-fee-head mode: Inherit, NoLateFee, or CustomRule.
7. Apply grace period.
8. Calculate raw late fee.
9. Apply charging-day and amount caps.
10. Apply rounding.
11. Subtract already posted late-fee payments and applied waivers.
12. Return remaining collectable late fee plus a human-readable explanation.

Do not resolve the mutable policy attached to the current fee structure for an old assignment.
Default-policy fallback is allowed only as a temporary, logged migration bridge for legacy assignments with no snapshot.

## Date and grace-period rules

Let `D` be the due date and `C` the collection/calculation date, both normalized to the school's local date.

```text
daysLate = max(0, C - D in whole calendar days)
effectiveGrace = student custom grace if present, otherwise policy grace
effectiveGrace += exception-window extension
chargeableDays = max(0, daysLate - effectiveGrace)
```

- Payment on `D` has zero late fee.
- With zero grace days, late fee starts on `D + 1`.
- With three grace days, `D + 1`, `D + 2`, and `D + 3` are free; charging starts on `D + 4`.
- Time of day must not change the result.
- Working-day grace must remain unavailable until holidays, weekends, campus calendar, and timezone rules are implemented server-side.

## Late-fee calculation order

| Step | Rule |
|---:|---|
| 1 | Return zero for fee heads outside effective late-fee scope |
| 2 | Return zero for student exemption, disabled late fee, suspension window, or NoLateFee override |
| 3 | Resolve effective grace and chargeable days |
| 4 | Resolve policy or custom per-head rule |
| 5 | Limit chargeable days by maximum charging days |
| 6 | Calculate Flat, Percentage, PerDay, PerWeek, PerMonth, Compound, or Slab amount |
| 7 | Apply slab-level caps where configured |
| 8 | Apply policy/per-head maximum late-fee cap |
| 9 | Apply rounding once at the end |
| 10 | Subtract applied waivers and already paid late fee without going below zero |

| Type | Calculation |
|---|---|
| Flat | One amount per due row after grace, not once per payment attempt |
| Percentage | `rate x selected base` |
| Per day | `amount x chargeableDays` |
| Per week | `amount x ceiling(chargeableDays / 7)` |
| Per month | `amount x ceiling(chargeableDays / 30)` unless Finance approves calendar-month semantics |
| Compound | Compound selected base for completed configured periods; require a cap |
| Slab | Apply ordered contiguous day ranges using the documented cumulative method |

Every response exposes gross late fee, previously paid, waived, remaining, chargeable days, cap/rounding result, policy/version, and explanation.

## Collection scenarios

| Scenario | Required behavior |
|---|---|
| Before/on due date | Principal is collectable; late fee is zero |
| After due date | Server returns late fee per eligible due row |
| Partial principal payment | Post allocation; future percentage-based calculations use the policy's defined base |
| Multiple partial payments | Never recollect already paid principal or late fee |
| Full payment | Principal plus remaining late fee closes the due row |
| Advance payment | Handle through the advance/credit workflow; do not fabricate a due-row late fee |
| Excess payment | Block unless the existing credit/refund workflow explicitly accepts it |
| Zero-value demand | Display but do not create a zero-value collection transaction |
| Zero/negative payment | Reject |
| Retry/double click | Same idempotency key returns the existing result |
| Concurrent payments | Optimistic concurrency/transaction lock prevents overpayment |
| Offline collection | Allowed by collection permission even when online is disabled |
| Online collection | Gateway order and callback both enforce effective policy |

## Fee Collection waiver integration

Collection Policy calculates the late fee and captures whether its effective rule allows waiver.
Fee Collection shows `Waive` only when that captured value is ON and a positive balance remains.
An authorized collector enters amount and reason, and the server applies the non-cash reduction immediately after live-balance validation.
There is no request, approval, rejection, or Waiver Management queue.
The complete flow is defined in [Fee Collection waiver end to end](./fee-collection-waiver-end-to-end.md).

## Exception-window workflow

Exception windows handle school-wide or scoped events without editing the base policy.

| Effect | Result |
|---|---|
| SuspendLateFeeAccrual | No late fee accrues during the applicable period |
| ExtendGracePeriod | Adds validated extension days |
| DisableOnlineCollection | Blocks online order/callback while active |
| Custom | No financial effect until a specific server rule is defined |

Each window needs academic year, label, reason, start/end dates, scope, optional fee-structure ID, effect, extension days where required, status, creator, and audit data.
Overlapping applicable windows with conflicting effects are blocked.
If multiple non-conflicting windows are allowed, the resolver uses an explicit priority and returns all applied window IDs; it must never depend on database row order.

## Student-specific override workflow

Use only for an individual exception that cannot be represented by the policy or an exception window.

| Override | Behavior |
|---|---|
| Late-fee exempt | Returns zero late fee for the student/structure |
| Custom grace days | Replaces policy grace, then exception extension is added |

Creating/editing an override requires a reason, effective period, dedicated permission, actor, and audit snapshot.
An expired override is ignored.
The effective-rules endpoint shows the base rule and every override source.

## Frontend screens and responsibilities

### Collection Policy workspace

Route: `/finance/collection-policy`.

The page contains:

- required academic-year selector;
- Current and Archived views;
- search and status filters;
- columns for name/code, status, default, late-fee type, covered heads, used-by count, version, and last update;
- row actions filtered by exact permission;
- separate access to exception windows;
- empty, loading, retry, and permission-denied states.

### Create/edit drawer

Present sections in this order:

1. General.
2. Late fee.
3. Eligible fee heads.
4. Advanced calculation and caps.
5. Per-fee-head exceptions.
6. Online collection.
7. Validation summary and server simulation.

Draft save is allowed with incomplete optional business configuration, but never malformed values or invalid tenant references.
Activate remains disabled until the server validation result is ready and successful.
Closing a dirty drawer asks for confirmation.

### Details, history, and impact

- Details is read-only and resolves names for IDs.
- Version History shows version, action, actor, timestamp, reason, and a field-level diff.
- The policy-specific Audit Trail is another presentation of the same immutable `FinancePolicyVersion` stream, covering create, edit, clone, archive, and unarchive without duplicating mutable audit state.
- Used By lists fee structures and affected future assignments.
- Impact Preview shows affected structures/students/due rows before an Active policy edit.
- Calculation explanation is available to collectors without exposing edit controls.

### Frontend security and client rules

- Menu and route use `Finance.Policies.CollectionPolicy.View`, not Fee Collections permission.
- Configuration buttons use View, Add and Delete. Direct waiver application uses the proposed `Finance.Fees.FeeCollections.ApplyWaiver`; override, exception, and report grants remain separate.
- Backend repeats every permission and ownership check.
- RTK Query uses typed request/response contracts, precise cache tags by year/policy, and invalidates list/detail/version/impact after mutation.
- Prevent duplicate submit while saving and send an idempotency key for financial commands.
- Map backend error codes to actionable field or banner messages; do not replace them with only `Failed to save`.

## Backend responsibilities by layer

| Layer | Responsibility |
|---|---|
| Domain.Shared | Enums, constants, error codes, permission names |
| Domain | Aggregate lifecycle, rule validation, fee-head scope, calculators, resolver precedence |
| Application.Contracts | Typed DTOs, validators, app-service interfaces |
| Application | Authorization, tenant/year/reference checks, impact/simulation orchestration, transactions, audit/version writes |
| EntityFrameworkCore | Tables, FKs, filtered unique indexes, precision, concurrency token, migrations |
| HttpApi | Thin REST controllers only |
| Tests | Domain matrix, app-service integration, API authorization/idempotency, frontend and browser flows |

All create/update validators call one shared rule validator used again by activation, attachment, assignment, simulation, and runtime calculation.
Invalid enum strings are rejected; they never silently fall back to a financial default.

## Target HTTP API

All routes require authentication and matching permissions.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/finance/collection-policies` | Paged list for required academic year/search/status |
| GET | `/api/fee-heads/active?academicYearId={id}` | Reusable lookup of active fee heads whose parent category is active in the requested academic year |
| GET | `/api/finance/collection-policies/{id}` | Details with resolved reference names and usage |
| POST | `/api/finance/collection-policies` | Create Draft or fully validated Active policy |
| PUT | `/api/finance/collection-policies/{id}` | Edit Draft/Active and create a version |
| POST | `/api/finance/collection-policies/{id}/clone` | Clone to Draft, optionally another year |
| DELETE | `/api/finance/collection-policies/{id}` | Archive after usage/default checks |
| POST | `/api/finance/collection-policies/{id}/unarchive` | Restore to Draft |
| GET | `/api/finance/collection-policies/{id}/versions` | Immutable history |
| POST | `/api/finance/collection-policies/{id}/simulate` | Server-authoritative example calculation |
| POST | `/api/finance/collection-policies/{id}/impact` | Fee-structure/edit impact preview |
| POST | `/api/finance/collection-policies/{id}/validate-attachment` | Validate draft or existing fee structure attachment |
| GET/POST/DELETE | `/api/finance/collection-policies/exception-windows...` | Manage scoped temporary windows |
| GET/PUT | `/api/finance/collection-policies/student-overrides...` | Read/set student effective exceptions |

Collection Policy create/update/detail includes only the `AllowWaiver` value on each effective late-fee rule.
The monetary command is deliberately excluded from the Collection Policy API and is defined under Fee Collection in [Fee Collection waiver end to end](./fee-collection-waiver-end-to-end.md#target-contracts).

Mutation responses return current version/concurrency token.
Conflicts return a stable business error rather than overwriting another administrator's changes.

## Permissions and suggested school roles

Configuration follows the [three-permission contract](../api.md#fee-configuration-permissions): View includes history/simulation; Add includes editing, activation, cloning, restoring and attachments; Delete covers archival. Operational permissions remain independent.

| Permission | Finance admin | Accountant | Collector | Principal/approver | Auditor |
|---|---:|---:|---:|---:|---:|
| View | Yes | Yes | Read effective result | Yes | Yes |
| Add | Yes | Optional | No | No | No |
| EditStudentOverride | Optional | Optional | No | Optional | No |
| ManageExceptionWindow | Yes | No | No | Optional | No |
| ViewReports | Yes | Yes | Limited | Yes | Yes |

Exact grants are tenant-configurable.
API permissions, tenant isolation, and object ownership are authoritative.
Applying a waiver uses the separate proposed Fee Collection permission and is never implied by Collection Policy configuration access.

## Persistence and integrity

Target database guarantees:

- FK from policy to academic year with restricted delete;
- case-normalized unique `(TenantId, AcademicYearId, PolicyCode)`;
- case-normalized unique `(TenantId, AcademicYearId, PolicyName)` if product keeps name uniqueness;
- unique filtered Active default per tenant/year;
- FK from fee structure to Collection Policy and Finance Policy Version with restricted delete;
- Collection Policy version FK/snapshot on student assignment;
- unique version number per policy;
- FKs or normalized child tables for fee heads, overrides, and slabs where referential integrity is required;
- decimal precision for all amounts/percentages;
- concurrency stamp/row version for edits;
- no cascade deletion of policy, version, transaction, waiver, or audit evidence;
- one transaction for policy/version write and one transaction for collection/allocation/receipt/ledger/accounting write.

JSON may remain for immutable snapshot payloads.
Mutable foreign-key lists should not rely only on JSON because inactive, deleted, cross-tenant, and wrong-year references must be enforceable.

## Audit and historical reproducibility

Record:

- creator/modifier/status actor and timestamp;
- change action and mandatory reason for Active edits, archive, restore, default replacement, overrides, and exception windows;
- previous and new field values;
- policy version and snapshot hash;
- fee structures using each version;
- student assignment policy/version;
- transaction calculation inputs and result;
- exception window, override, and applied-waiver IDs plus the Fee Collection actor;
- reversal relationship and reason.

A receipt generated years later must be reproducible without reading the current mutable policy.

## Receipts, accounting, reports, and notifications

Receipts and ledgers itemize principal, late fee charged, late fee paid, late fee waived, advance/credit, and total received.
When `ConsiderAsIncome` is true, posted late fee credits the configured income ledger and the payment-side entry follows the selected mode; reversal creates equal opposite entries.

Reports reconcile from posted allocations, not by recalculating today's policy:

- daily collection;
- student ledger;
- outstanding and overdue;
- late-fee assessment/collection/waiver;
- fee-head collection;
- payment-mode and gateway reconciliation;
- receipt and reversal;
- accounting trial/ledger summaries;
- policy usage and exception audit.

Due/late reminders use the captured policy version and current unpaid balance.
Payment/waiver/reversal notifications are emitted only after transaction commit and use an outbox/idempotent consumer.

## Errors staff should see

| Condition | Message intent |
|---|---|
| Draft/inactive policy selected | Activate the policy before attaching it |
| Wrong academic year | Choose a policy for the fee structure's academic year |
| Fee-head mismatch with no overlap | Policy attached; no late fee applies until the structure and policy share an eligible head |
| Partial mismatch | Policy attached; show exactly which heads are and are not covered |
| Invalid academic-year scope | Policy and fee structure must belong to the same academic year |
| Duplicate default | Name the current default and offer navigation to it |
| Attached/default archive attempt | List affected structures and the required replacement action |
| Stale edit | The policy changed; reload and review the new version |
| Late-fee overpayment | Show computed remaining late fee and requested amount |
| Online disabled | Explain that office payment remains available if permitted |

Business errors use stable error codes with structured data so the frontend can render these messages consistently.

## Concurrency and idempotency

- Policy edit uses a concurrency token.
- Replacing the Active default occurs atomically under a filtered unique index.
- Fee-structure attachment revalidates policy/version and impact inside the save transaction.
- Collection, waiver apply, reversal, online order, and callback require idempotency keys.
- Unique database constraints reject duplicate allocation/application IDs.
- Due-row outstanding and late-fee remaining are checked under the same transaction before posting.
- A retry returns the original successful response; it never posts a second receipt.

## Validation and test matrix

### Domain tests

- every status transition;
- every formula and zero/boundary/large decimal case;
- grace off-by-one dates;
- caps and rounding order;
- contiguous/gapped/overlapping slabs;
- all fee-head match combinations;
- per-head override precedence;
- per-rule waiver-toggle capture and applied-waiver subtraction without changing the original late-fee assessment;
- exception and student-override precedence.

### Application/API tests

- tenant/year/reference validation;
- Draft cannot attach and Archived cannot edit/use;
- restore returns Draft;
- clone returns Draft and not default;
- one Active default under concurrency;
- active in-use downgrade/archive restrictions;
- exact version snapshot at structure and assignment;
- historical calculation after policy edit/archive;
- permissions and direct API bypass attempts;
- online enforcement at order and callback;
- idempotent collection/waiver/reversal;
- atomic rollback on receipt/accounting failure.

### Frontend/browser tests

- permission-gated menu, route, and each row action;
- Draft save versus Activate validation;
- conditional fields clear safely;
- server simulation matches displayed preview;
- mismatch groups, non-blocking zero-overlap warnings, and blocking invalid-reference states;
- dirty-close confirmation and duplicate-submit prevention;
- archive/restore/default replacement guidance;
- responsive keyboard-accessible drawer;
- collection explanation, online-disabled, and retry flows; Fee Collection tests cover immediate amount-plus-reason waiver application.

## Deployment and school rollout

1. Add Draft status without reinterpreting existing numeric enum values incorrectly.
2. Backfill a Finance Policy Version for every existing Collection Policy.
3. Add policy-version columns and integrity constraints to fee structures and student assignments.
4. Backfill only empty references using logged, deterministic rules; never replace historical non-empty snapshots.
5. Validate every policy's fee heads, dates, accounting head, slabs, and default eligibility.
6. Convert invalid existing records to Draft and provide an admin remediation report.
7. Deploy backend validation/API changes before enabling the new frontend actions.
8. Have each school review, simulate, activate, and attach policies for open academic years.
9. Run one late payment, partial payment, waiver, online-disabled case, receipt, report, and reversal in staging.
10. Remove legacy default fallback only after all live assignments have explicit snapshots.

## Acceptance checklist

- [ ] Collection Policy exposes Draft, Active, and Archived only.
- [ ] New and cloned policies default to Draft.
- [ ] Restoring an archived policy returns it to Draft.
- [ ] Only a fully valid Active policy can be attached.
- [ ] Active default is unique per tenant/year at database and application levels.
- [ ] Fee-head intersection behavior matches the documented matrix.
- [ ] Attachment preview and save use the same backend validator.
- [ ] Fee structure and student assignment store exact policy-version IDs.
- [ ] Existing assignments do not change after policy edit/archive.
- [ ] Runtime calculator skips heads outside captured scope.
- [ ] Grace/date/formula/cap/rounding order is deterministic.
- [ ] Prior late-fee payment and waiver reduce remaining collectable late fee.
- [ ] Online restriction is enforced by every backend entry point.
- [ ] Eligible waivers applied in Fee Collection reduce only the remaining late-fee balance and are idempotent.
- [ ] Exception overlaps are rejected or resolved by explicit priority.
- [ ] Receipts, ledgers, accounting, reports, and reversals itemize and reconcile.
- [ ] Audit history explains every applied rule and version.
- [ ] Concurrency and duplicate retries cannot over-collect.
- [ ] Frontend uses exact Collection Policy permissions and actionable errors.

## Current implementation gaps against this target

The current code already provides a Collection Policy aggregate, CRUD/clone/archive/restore, version snapshots, simulation, late-fee calculation, per-head overrides, policy-owned waiver fields, exception windows, student overrides, and collection-screen integration.
These parts should be preserved and hardened rather than rewritten.

The following gaps must be closed before treating this page as shipped behavior:

1. Collection Policy currently has only Active and Archived; it needs Draft and concession-style lifecycle rules.
2. Current restore changes Archived directly to Active; it must restore to Draft.
3. Fee Structure stores only Collection Policy ID; it needs the exact version ID and authoritative attachment validation.
4. Student assignment snapshots Concession Policy but not Collection Policy.
5. The resolver returns a directly attached policy without status, academic-year, date, or version validation and falls back to the mutable default.
6. The runtime calculator does not enforce `LateFeeApplyOnFeeHeads`; obsolete `WaiverApplyOnFeeHeads` and the broader policy-owned waiver settings must be replaced by one captured `AllowWaiver` boolean per effective late-fee rule.
7. Fee-head attachment impact is implemented with intersection semantics and warnings; student-assignment snapshot coverage remains incomplete.
8. Several enum parsers silently choose defaults for invalid strings.
9. Full numeric/date/reference validation and foreign-key integrity are incomplete.
10. The filtered default index is not unique.
11. Policy fee-head lists are stored only as JSON, limiting referential integrity; obsolete waiver role lists must be removed.
12. The frontend creates policies as Active by default and has no Save Draft/Activate review flow.
13. The Fee Structure screen can edit shared Collection Policy overrides, which should be read-only there.
14. Frontend policy routing uses Fee Collections permission instead of Collection Policy permissions.
15. The frontend locally reimplements calculation and can drift from server caps/rules.
16. WorkingDay grace, overlapping exception windows, and `DisableOnlineCollection` are not fully enforced.
17. Remaining previously paid late fee, idempotency, reversal, accounting, receipts, reports, and notification consistency need end-to-end completion.

## Source map

| Concern | Current source |
|---|---|
| Collection aggregate/enums | `src/ShikshaMitra.Domain/Finance/Policies/CollectionPolicy.cs`, `CollectionPolicyEnums.cs` |
| Resolver/calculators | `src/ShikshaMitra.Domain/Finance/Policies/CollectionPolicyResolver.cs`, `LateFeeCalculationService.cs`, `WaiverCalculationService.cs` |
| Application service/mapping | `src/ShikshaMitra.Application/Finance/Policies/CollectionPolicyAppService.cs`, `CollectionPolicyMapping.cs` |
| Contracts | `src/ShikshaMitra.Application.Contracts/Finance/Policies/CollectionPolicyDtos.cs` |
| HTTP routes | `src/ShikshaMitra.HttpApi/Controllers/Finance/Policies/CollectionPoliciesController.cs` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Finance/Policies/CollectionPolicyConfiguration.cs` |
| Fee Structure integration | `src/ShikshaMitra.Application/Finance/Fees/Settings/FeeStructureAppService.cs` |
| Collection runtime | `src/ShikshaMitra.Application/Finance/Fees/Collections/CollectFeeWorkflow.Validators.cs`, `StudentFeeDetailsQueryService.cs` |
| Frontend route/workspace | In the frontend repository: src/app/(auth-route)/finance/(Policies)/collection-policy/** and src/screens/finance/policies/workspaces/CollectionPolicyWorkspace.tsx |
| Frontend form/client | In the frontend repository: src/screens/finance/policies/** and src/redux/finance/collectionPolicy.ts |
| Fee Structure UI | In the frontend repository: src/screens/finance/settings/FeeStructure/Components/FeePoliciesTab.tsx |
| Lifecycle reference | `docs/modules/finance/references/concession-policy-end-to-end.md` |
