---
module: finance
domain: proration-policy
content_type: Reference
status: implemented
updated_on: 2026-10-27
owner: backend-team
audience: humans-and-agents
---

# Implement Proration Policy end to end

TL;DR: Proration Policy is now an operational Finance workflow. The backend owns a typed, permission-protected policy lifecycle, immutable version snapshots, independent admission/withdrawal rule configuration, day-level assignment charge periods, decimal installment calculation, idempotent demand generation, and calculation evidence on every generated `DueFee`. Concession, collection, payment, refund, adjustment, reporting, and accounting continue to consume the stored prorated obligation; none independently recalculates it.

The frontend-owned screen and client changes are documented in [Proration Policy frontend flow](../../../../../Shikshamitra-FE/docs/finance/references/proration-policy-frontend-flow.md).

> This document began as the implementation review. It now records the implemented contract and the intentionally deferred operational extensions.

## Current Fee Structure boundary

As of 2026-10-05, Proration is removed from Fee Structure setup and its API contracts. Structure lookups, defaults, attachment validation, review coverage and publication warnings contain no proration. The attachment-impact endpoint is removed. New assignments do not inherit legacy template proration. Existing stored structure pins, publication JSON and assigned/charged calculations remain historical evidence; assigned-policy edits preserve them. Policy lifecycle and simulation remain available. The older attachment rollout/review notes below describe the previous integration and do not authorize recreating it. See [Fee Structure contracts](./fee-structure-workspace.md).

## Implementation status

Implemented on 2026-09-08:

- `ProrationPolicy` aggregate, normalized rules, lifecycle, optimistic concurrency, one-active-default enforcement, and immutable `FinancePolicyVersion` snapshots;
- typed list/detail/create/update/activate/clone/archive/unarchive/version/simulate APIs with exact permissions;
- active, same-year Fee Head validation;
- Fee Structure, Student Fee Structure, and DueFee policy/version foreign keys and charge/calculation evidence;
- inclusive Daily and MonthlyCutoff calculation with decimal arithmetic, currency/rupee rounding, no-overlap handling, minimum-day caps, and last-installment base residual reconciliation;
- locked/same-standard assignment checks, same-year uniqueness, idempotent identical retries, and conflicting retry rejection;
- per-installment proration before concession, late-fee, collection, refund, adjustment, receipt, and posting flows;
- database migration with historical `ScheduledAmount` and assignment-period backfill;
- focused domain tests for daily, one-day, leap-year, cutoff, half-charge, minimum-days, and no-overlap behavior.

Intentionally deferred because they require a separate approved financial-adjustment workflow rather than silent repricing:

- changing an already-generated charge period and recalculating existing dues;
- automatic event subscriptions from withdrawal, hostel, and transport modules;
- repricing paid, receipted, posted, or refunded lines. Those must use reversal/adjustment/refund documents.

## Original executive assessment (resolved by this implementation)

Before this implementation, the UI could collect policy-looking data, but no backend endpoint accepted it and no demand-generation path read it. The P0/P1 gaps below are retained as review history; their required dispositions are now implemented.

| Priority | Current gap | Financial effect | Required disposition |
|---|---|---|---|
| P0 | Generic `/finance-policies` client has no matching proration backend | Create, list, edit, clone, and archive are non-operational | Build a typed proration API and client |
| P0 | Fee Structure request/entity does not contain Proration Policy ID/version | A UI selection is dropped before persistence | Add validated ID/version attachment |
| P0 | `DueFeeManager` never invokes proration | Full scheduled amounts are generated | Insert one server calculator before `DueFee` creation |
| P0 | Student assignment has no day-level charge period or policy snapshot | Mid-period eligibility cannot be calculated or reproduced | Capture source, start/end dates, policy ID/version, and per-head overrides |
| P0 | Assignment commands are not protected by the proposed action permission | Direct API use can bypass UI visibility | Authorize every read/write entry point |
| P1 | Policy rules infer recurring/one-time from Fee Head, while `IsOneTime` belongs to Fee Structure detail | Rules can be applied to the wrong charge shape | Resolve eligibility from structure detail during attachment/runtime |
| P1 | Current schedule-generation idempotency is student-wide, not academic-year scoped | A historical due can block a valid later-year assignment | Make assignment and due uniqueness tenant/student/year/schedule scoped |
| P1 | No calculation provenance exists on `DueFee` | Ledger and dispute review cannot explain a reduced charge | Store immutable proration evidence |
| P1 | Money originates as `double` in Fee Structure and generation | Fractional currency can drift or be lost | Use `decimal` end to end and reconcile residuals |
| P2 | UI says admissions, exits, and service changes, but exposes only a rule and no event/source model | Product promise exceeds available configuration | Implement explicit charge-period sources or narrow the UI claim |

## Product boundary

Proration answers one question: **what portion of a scheduled fee obligation applies to this student's eligible service period?**

It owns:

- policy lifecycle, fee-head rules, rounding, versioning, and simulation;
- resolution of an immutable policy version attached to a student fee assignment;
- calculation of scheduled amount versus chargeable amount for each installment;
- calculation evidence and auditable correction behavior.

It does not own:

- the annual fee amount or whether a fee is optional - Fee Structure owns these;
- installment percentages, coverage, demand, and due dates - Payment Terms own these;
- admission, exit, hostel, or transport dates - their source modules own those facts;
- concession approval, late fees, collection, refunds, adjustments, receipts, or accounting - those modules consume the final due and keep their own rules.

## Independent admission and early-withdrawal contract

Create and activate a policy without student dates. Fee Structure no longer accepts a Proration Policy attachment, and new student assignment never inherits one from legacy structure storage. The assignment policy options and update command accept only concession, refund and collection selections.
Supplying a proration selection is rejected as invalid input, including for callers with policy override permission.
Editing other assigned policies preserves the previously pinned proration version.
Actual admission or service-start and exit or service-end dates are supplied when calculating the student assignment.
Posted demands and receipts are never recalculated by editing a policy.

| Field per fee head | Default and meaning |
|---|---|
| ApplyOnAdmission | True; false retains the coverage start boundary |
| RuleType | Admission model: FullCharge (No Proration), Daily, MonthlyCutoff, or FullMonthCharge |
| CutoffDay / AfterCutoffAction | Monthly admission cutoff 1-31; on/before bills full current month, after bills zero (Charge Next Month) or 50% (Half Month Rule) |
| ApplyOnEarlyWithdrawal | True; false retains the coverage end boundary |
| EarlyWithdrawalRuleType | Independently selected FullCharge, Daily, MonthlyCutoff, or FullMonthCharge |
| EarlyWithdrawalCutoffDay / EarlyWithdrawalBeforeCutoffAction | Exit on/before cutoff bills zero or 50% of the exit month; after bills the full exit month; later months are excluded |
| RoundingMode | One shared head-level Currency, NearestRupee, RoundUpRupee or RoundDownRupee mode, applied once after combining both events |

Daily uses inclusive calendar days, including leap days.
FullMonthCharge includes each touched service month in full, separately from FullCharge which leaves that event boundary unchanged.
Policy validation requires at least one recurring fee head with an enabled Daily, MonthlyCutoff, or FullMonthCharge event. Disabled events and FullCharge do not satisfy this requirement.
Monthly rules use equal calendar-month shares of the coverage amount before payment-term allocation.
When mixed with daily, only the daily boundary month is reduced by its eligible-day fraction.
When admission and withdrawal affect the same month, apply the smallest applicable fraction once rather than multiplying reductions.
Daily rules use eligible calendar days without a minimum-day floor.
No date overlap always produces zero, even when both event switches are disabled.
Cutoffs exceeding a month's length clamp to its last day; cutoff equality is inclusive.
One-time Fee Structure details remain unprorated for both events.

Nullable withdrawal fields identify legacy rules and immutable snapshots, which retain their existing calculations.
The additive migration defaults event switches to true and leaves withdrawal settings null; it does not rewrite version JSON or existing financial records.
New form saves explicitly persist the independent withdrawal model.

## Implemented product decisions

These decisions reflect the independent admission and early-withdrawal behavior requested for the current implementation.

| Decision | Target behavior |
|---|---|
| Scope | One tenant and one academic year |
| Lifecycle | `Draft -> Active -> Archived`; restore returns Archived to Draft |
| New/clone | Starts as Draft, is not default, and has no attachments |
| Attachment | Only an Active policy from the same academic year can be attached |
| Default | At most one Active default per academic year; default only preselects, while the fee structure stores an explicit attachment |
| Versioning | Every create, edit, activate, archive, restore, and clone writes an immutable `FinancePolicyVersion` snapshot |
| Historical use | Fee Structure and Student Fee Structure store both policy ID and exact version ID |
| Calculator | Server-only authority; frontend preview always calls the same server calculator |
| Applicability | Calculate per Fee Structure detail and installment, never from Fee Head metadata alone |
| One-time fee | Full charge when selected/applicable; v1 does not prorate one-time rows |
| Recurring fee without a configured rule | Full scheduled charge |
| Date interval | Inclusive dates, clamped to the academic year and the installment coverage interval |
| Currency | `decimal`, two-decimal persisted currency; no binary floating-point in contracts or domain calculations |
| Corrections | Never silently rewrite a paid/posted obligation; use versioned replacement before collection or auditable adjustment/refund afterward |
| Historical data | Never retroactively apply a newly created policy to existing dues |

## Implemented end-to-end flow

```text
Academic year + Fee Heads + Payment Terms
  -> Finance admin creates Proration Policy Draft
  -> Server validates rules and runs simulation
  -> Finance admin activates policy; immutable version is written
  -> Fee Structure selects Active policy
  -> Server previews fee-head/schedule impact
  -> Fee Structure save stores policy ID + exact version ID
  -> Student/Admission or service module supplies charge period and source
  -> Student Fee Structure assignment snapshots policy ID + version + charge period
  -> Server resolves fee details and immutable installment coverage
  -> Proration calculator returns base, factor, rounded amount, and explanation per line
  -> DueFee rows are created once with calculation evidence
  -> Concession applies to prorated outstanding
  -> Collection Policy calculates late fee on the prorated obligation
  -> Payment/receipt/accounting post the amount actually collected
  -> Refund and adjustment use posted allocations; reports use stored evidence
```

### Staff setup order

1. Configure the academic year, fee heads, fee structure details, and published Payment Terms.
2. Create a Proration Policy as Draft.
3. Configure rules only for recurring fee heads that can have partial service.
4. Simulate boundary dates, leap years, and the actual fee structures that will use it.
5. Activate the policy.
6. Attach the Active policy to each Fee Structure after reviewing server impact.
7. Assign a structure to a student with a verified charge-period source.
8. Review the generated demand explanation before collecting money.

## Policy model

### General fields

| Field | Required | Server rule |
|---|---:|---|
| `AcademicYearId` | Yes | Tenant-owned; immutable after create |
| `Name` | Yes | Trimmed, 3-128 characters; unique per tenant/year case-insensitively |
| `Code` | Yes | Upper-case, 1-32 characters; unique per tenant/year; immutable after first use |
| `Description` | No | Maximum 500 characters |
| `Status` | Yes | Draft or Active on create/edit; archive/restore use commands |
| `IsDefault` | Yes | Only one Active default per tenant/year |
| `ConcurrencyStamp` | On edit | Reject stale writes |
| `ChangeReason` | Conditional | Required for Active edits, archive, restore, and attachment replacement |

Do not add editable policy effective dates in v1. Academic-year scope and immutable assignment snapshots determine applicability. If a school needs a mid-year rule change, create a new policy version for future assignments; do not change past assignments.

### Fee-head rule fields

| Field | Rule |
|---|---|
| `FeeHeadId` | Active, tenant-owned Finance Fee Head; unique within policy |
| `RuleType` | `FullCharge` (No Proration), `Daily`, `MonthlyCutoff`, or `FullMonthCharge` |
| `RoundingMode` | `Currency`, `NearestRupee`, `RoundUpRupee`, or `RoundDownRupee` |
| `CutoffDay` | MonthlyCutoff only; integer 1-31, clamped to the last calendar day of a shorter month |
| `AfterCutoffAction` | MonthlyCutoff admission only; `NoCharge` or `HalfCharge` for the admission month's share |
| `ApplyOnAdmission` / `ApplyOnEarlyWithdrawal` | Independent switches per fee head |
| `EarlyWithdrawalRuleType` | Independently selected model with its own minimum-day or monthly-cutoff settings |
| `EarlyWithdrawalCutoffDay` / `EarlyWithdrawalBeforeCutoffAction` | On/before cutoff waive or halve the exit month; after cutoff charge full exit month |

A fee head absent from the rule list receives its full scheduled charge. A configured head has independent event rules and one shared rounding mode. FullMonthCharge is distinct from FullCharge: it bills touched months fully, while FullCharge leaves that event boundary unchanged. Existing snapshots retain their old behavior; the additive migration does not rewrite financial evidence.

`IsOneTime` must not be copied from Fee Head as policy truth. It is a Fee Structure detail property. Attachment preview and runtime reject or ignore a non-full rule when the matching structure detail is one-time.

## Charge-period contract

Proration cannot be derived from a policy alone. Each assignment needs an authoritative eligible interval.

| Field | Purpose |
|---|---|
| `ChargeStartDate` | First inclusive date the student/service is eligible to be charged |
| `ChargeEndDate` | Last inclusive date, or academic-year end when open |
| `ChargePeriodSource` | `Admission`, `StudentAssignment`, `HostelService`, `TransportService`, `StudentExit`, or `ManualOverride` |
| `SourceReferenceId` | Optional stable ID of the admission/service event |
| `OverrideReason` | Mandatory only for authorized manual override |

The assignment-wide interval applies to ordinary academic recurring fees. A selected optional/service fee may carry its own interval; that interval overrides the assignment-wide interval only for that Fee Structure detail. Month-only start/end values in the current assignment UI are insufficient for daily proration and must become real dates.

Bulk assignment is allowed only when all selected students share the same verified charge period. Otherwise the API returns a per-student validation result and creates no partial assignments unless the existing bulk convention explicitly supports atomic batches.

## Calculation contract

### Canonical order

For each selected Fee Structure detail:

1. Resolve its annual/custom amount, coverage dates, and immutable Payment Term schedule.
2. Resolve the student's charge interval for that detail and the captured Proration Policy version.
3. Calculate the detail entitlement across its service coverage, using independent event boundaries and calendar-month shares when applicable.
4. Apply shared rounding once and cap the entitlement at the scheduled detail amount.
5. Allocate scheduled and prorated totals using the payment-term percentages; reconcile the final currency residual.
6. Create idempotent `DueFee` lines with base amounts and proration evidence.

Proration is applied **before** concession, late fee, payment, refund, or adjustment.

### Daily formula

All dates are inclusive and use the school's local calendar date, not UTC timestamps.

```text
coverageDays = (coverageEnd - coverageStart) + 1
eligibleStart = max(chargeStart, coverageStart)
eligibleEnd = min(chargeEnd, coverageEnd)
actualDays = max(0, (eligibleEnd - eligibleStart) + 1)
chargeDays = actualDays == 0
  ? 0
  : actualDays
factor = chargeDays / coverageDays
rawAmount = scheduledInstallmentAmount * factor
proratedAmount = round(rawAmount, configuredRoundingMode)
```

Daily proration charges only eligible calendar days. Retired minimum-day fields in old snapshot JSON are ignored when calculating new results; stored dues and audit snapshots are not rewritten.

### Legacy monthly-cutoff formula

The cutoff applies to the start date in the affected monthly coverage interval:

| Condition | Target result |
|---|---|
| No overlap | Zero |
| Charge begins on or before cutoff | Full scheduled installment |
| Charge begins after cutoff and action is `NoCharge` | Zero for that installment |
| Charge begins after cutoff and action is `HalfCharge` | Half of that installment, then configured rounding |
| Full subsequent coverage | Full scheduled installment |

The cutoff equality is explicitly **on or before**. A day value larger than a short month's final date resolves to that month's final date. The server returns the resolved cutoff date in its explanation.

This table describes immutable legacy snapshots. New event rules use equal calendar-month shares and independent admission and exit cutoffs, as specified under the current field contract above. In the same month, the smaller applicable fraction is used once. Neither rule defers waived money into a later month's charge.

### Rounding

| Mode | Result |
|---|---|
| `Currency` | Round to two decimals using midpoint away from zero |
| `NearestRupee` | Round to whole rupee using midpoint away from zero |
| `RoundUpRupee` | Ceiling to whole rupee |
| `RoundDownRupee` | Floor to whole rupee |

The sum of created installment bases must equal the fee detail amount at currency precision before proration. The sum of prorated lines must never exceed that base. Never cast residual currency to `int`.

### Worked acceptance examples

| Case | Assumptions | Expected result |
|---|---|---:|
| Full coverage | INR 10,000 installment, entire interval eligible | INR 10,000.00 |
| Daily half month | INR 10,000, Apr 1-30, eligible Apr 16-30 | INR 5,000.00 |
| Daily one day | INR 10,000, Apr 1-30, eligible Apr 30, Currency rounding | INR 333.33 |
| Leap February | INR 2,900, Feb 1-29 2028, eligible Feb 15-29 | INR 1,500.00 |
| Cutoff current | INR 3,000, start Jun 10, cutoff 10 | INR 3,000.00 |
| Cutoff no-charge | INR 3,000, start Jun 11, cutoff 10, NoCharge | INR 0.00 |
| Cutoff half | INR 3,000, start Jun 11, cutoff 10, HalfCharge | INR 1,500.00 |
| No overlap | Any base, eligible period outside coverage | INR 0.00 |
| One-time | INR 5,000 one-time detail | INR 5,000.00 |

## Backend architecture

Follow the existing Concession/Collection policy slice rather than adding another generic policy switch.

| Layer | Implemented type/responsibility |
|---|---|
| Domain.Shared | Proration enums, stable error codes, complete permissions |
| Domain | `ProrationPolicy` aggregate, rule entity/value object, snapshot, calculator, attachment validator |
| Application.Contracts | Typed list/detail/create/update/simulate/impact DTOs and app-service interface |
| Application | Mapping, lifecycle, snapshot creation, tenant/year/reference validation, server simulation |
| EntityFrameworkCore | Policy/rule configuration, indexes/FKs, Fee Structure/assignment/DueFee evidence mappings, migration |
| HttpApi | Explicit `/api/finance/proration-policies` controller and authorization |
| Tests | Domain calculations plus application, API permission, persistence, and workflow tests |

Use the existing `FinancePolicyVersion` table with `PolicyType = "proration"`. The snapshot JSON contains the complete normalized policy and rule set; runtime reads that immutable snapshot, not the current mutable policy row.

### Persistence additions

| Aggregate/table | Required additions |
|---|---|
| Proration Policy | Tenant, academic year, identity, lifecycle, default, concurrency, normalized child rules |
| Fee Structure | Nullable `ProrationPolicyId` and `ProrationPolicyVersionId`, paired check/FKs |
| Student Fee Structure | Captured policy ID/version plus assignment charge period/source |
| Student fee-head detail | Optional per-detail charge period/source override |
| DueFee | `ScheduledAmount`, `ProratedAmount`/existing `OriginalAmount`, policy/version, charge and coverage dates, rule, numerator/denominator/factor, rounding, explanation/snapshot hash |

Recommended DueFee meaning: `ScheduledAmount` is the pre-proration installment base; existing `OriginalAmount` becomes the amount legally due after proration and before concession/payment. This minimizes disruption to collection and refund invariants while preserving the comparison needed for audit.

Use foreign keys for policy and version IDs, a paired-null check, a unique policy code/name index per tenant/year, and a filtered unique index for the Active default. Rule uniqueness is policy + fee head. Add a concurrency token to policy writes.

### HTTP API

All routes are implemented under `/api/finance/proration-policies` and require authentication plus the matching permission.

| Method | Route | Purpose |
|---|---|---|
| GET | `/` | List for one required academic year, search, status, paging |
| GET | `/{id}` | Read normalized detail |
| POST | `/` | Create Draft or activate only after full validation |
| PUT | `/{id}` | Edit and create next immutable version |
| POST | `/{id}/activate` | Validate and activate Draft |
| POST | `/{id}/clone` | Clone as Draft, optionally to another academic year |
| DELETE | `/{id}` | Archive when not attached/default |
| POST | `/{id}/unarchive` | Restore to Draft |
| GET | `/{id}/versions` | Read immutable history |
| POST | `/{id}/simulate` | Run the production calculator on explicit sample inputs |

Fee Structure create/update accepts `ProrationPolicyId` plus an optional client version assertion. The server resolves and stores the current Active version. Assignment preview and commit should be two commands using the same domain calculation input; commit requires an idempotency key and rejects a stale preview/version.

### API response evidence

A simulation or assignment preview returns one row per installment containing:

- Fee Structure detail, fee head, schedule/version, installment sequence and coverage;
- scheduled amount, applicable start/end, eligible days, coverage days, factor;
- rule type, rounding mode, raw amount, final prorated amount;
- policy ID/version and a human-readable explanation;
- blocking errors and non-blocking warnings.

The frontend must display this response; it must not reproduce formulas as authoritative JavaScript.

## Integration with other submodules

| Submodule | Provides to proration | Consumes from proration | Integration rule |
|---|---|---|---|
| Academic Year | Start/end and tenant scope | Policy list/filter | Every policy, interval, and schedule must be in the same year |
| Fee Head | Stable identity/name | Policy rule selector | Fee Head does not decide recurring vs one-time |
| Fee Structure | Amount, optional flag, `IsOneTime`, policy attachment | Impact and saved policy version | Save must reject invalid policy/year/version |
| Payment Terms | Allocation, coverage, demand, due date, immutable version | Prorated installment base | Proratable recurring details require complete, non-overlapping coverage |
| Admission/Student | Admission/start and exit dates | Assignment charge period | Source facts stay owned by Student; Finance snapshots dates/source |
| Hostel/Transport/Services | Service activation/termination dates | Per-fee-detail interval | Integrate by command/event contract, not cross-module table mutation |
| Student Fee Assignment | Student + structure + optional heads + intervals | Preview/commit result and snapshot | This is the main policy-resolution point |
| Due/Demand | Schedule line and idempotency key | Final charge plus evidence | Proration executes before row creation |
| Concession | Nothing | Prorated outstanding | Percentage/fixed concession is capped against prorated due |
| Collection Policy | Due date and prorated outstanding | Late-fee base | Late fee must never use the discarded scheduled amount |
| Payment/Receipt | Allocation against DueFee | Final principal/late fee | Receipt remains immutable and references applied due lines |
| Refund Policy | Posted paid allocation | Refundable balance | No refund is calculated from a hypothetical unprorated fee |
| Adjustment | Correction request/reason/approval | Debit/credit delta | Use after posted demand; do not rewrite history |
| Reports/Audit | Stored evidence | Scheduled, prorated, concession, paid, outstanding totals | Reports read snapshots, never current policy math |
| Accounting | Posted charge/collection/refund events | Prorated amount and deltas | Post only committed financial facts with idempotent references |
| Notifications | Committed demand/adjustment event | Amount and explanation link | Publish after transaction commit via outbox |

## Lifecycle, corrections, and idempotency

### Policy changes

- Draft may be edited freely and cannot be attached.
- Activating performs complete reference and rule validation.
- Editing an Active policy creates a new version for future Fee Structure saves and assignments; it never mutates captured versions.
- An attached policy cannot be archived until it is detached. Archiving an unattached default clears its default flag; selecting another Active policy transfers the default atomically.
- Archived restores to Draft for review.

### Assignment and demand corrections

| State | Allowed correction |
|---|---|
| Preview only | Change dates/source and preview again |
| Dues created, all unpaid and unposted | Void/recreate in one transaction or create explicit replacement revision, retaining old evidence |
| Partially/fully paid | Keep due and receipt immutable; create approved debit/credit adjustment and, when money must leave the school, use Refund |
| Accounting posted | Adjustment/refund must create equal ledger delta/reversal links |

Use a unique assignment scope of tenant + student + academic year, not the current student-wide `ExistsForStudentAsync` gate. Due generation remains unique per assignment + Fee Structure detail + schedule version + installment sequence. Retries with the same idempotency key return the original result; a different payload with the same key is rejected.

## Permissions

Proration configuration exposes View, Add and Delete under the [fee configuration permission contract](../api.md#fee-configuration-permissions). View includes history/simulation; Add includes editing, activation, cloning, restoration and attachments; Delete controls archival/deletion. Operational `OverrideChargePeriod` and `Recalculate` grants remain independent and retain their existing names.

Apply the same canonical actions in backend entry points and frontend controls; tenant/scope/version checks still apply.

## Validation and error contract

### Cloning an existing policy

`POST /api/finance/proration-policies/{id}/clone` requires Proration Policy Add permission.
The source is read through the current tenant filter.
Cloning an Active default creates a separate nondefault Draft with new policy/code/rule identities and version 1; it does not modify the source, its default selection, or its version history.
Admission and early-withdrawal rule configuration is preserved.
Explicitly supplying the source academic year in `targetAcademicYearId` is equivalent to omitting it.
The service resolves this optional target once into a nonnullable year ID before using it in database predicates or creating the Draft.
With no explicit name, the server selects the next unused `Copy`, `Copy 2`, and subsequent suffix within the target academic year, including names occupied by archived policies and respecting the name length limit.
An explicit name still follows normalized, case-insensitive uniqueness validation.

Clone validates active Fee Heads in the target academic year, including when a different year is requested.
It does not silently map source-year heads to similarly named target-year heads or discard unavailable rules.
For a different academic year, create a policy using that year's active Fee Heads.
`Finance:ProrationPolicy:010027` identifies inactive, missing, or wrong-year heads and provides current-tenant head names when available; review the source rules and reactivate or select valid heads before retrying.
The API returns localized recovery instructions for name/code conflicts, invalid rules/status, and default restrictions in `application/problem+json` with the stable `errorCode` and safe business data.
A rejected clone creates no Draft or version and does not change the source default.

Regression coverage: `test/ShikshaMitra.Application.Tests/Finance/Policies/ProrationPolicyClone_Tests.cs` and the proration clone cases in `test/ShikshaMitra.HttpApi.Tests/Common/Middleware/ApiProblemDetailsPipeline_Tests.cs`.

### Other validation failures

Return stable error codes with structured fields for:

- wrong tenant or academic year;
- Draft/Archived policy attachment;
- stale version or concurrency stamp;
- duplicate name/code/default/rule;
- invalid or missing Fee Head;
- one-time detail with non-full rule;
- incomplete/overlapping/out-of-year installment coverage;
- charge dates outside year, reversed interval, or unverified source;
- manual override without permission/reason;
- attempt to archive an attached/default policy;
- duplicate assignment/due generation;
- recalculation after payment without adjustment/refund workflow.

## Frontend target at a glance

The detailed frontend plan is in the linked frontend report. The minimum backend-dependent behavior is:

- use a typed proration client, never generic `/finance-policies`;
- filter every list by the active academic year;
- expose Save Draft, Activate, Clone, Archive, Restore, Versions, Simulate, and Impact actions by permission;
- remove fake Fee Head fallback data;
- use Fee Structure detail `IsOneTime` during impact, not an invented Fee Head `feeType`;
- replace local readiness heuristics with server validation results;
- show assignment dates/source and the server preview before commit;
- show scheduled amount, proration reduction, final due, version, and explanation in demand/ledger views.

## Delivery sequence

1. Freeze or feature-flag the current Proration route and attachment selector so staff cannot rely on a non-persisted control.
2. Approve the open business decisions below and add shared enums/error codes/permissions.
3. Implement the policy aggregate, rules, calculator, snapshots, persistence, and focused domain tests.
4. Add typed application contracts/API, authorization, simulation, versions, and attachment impact.
5. Add Fee Structure policy/version persistence and validation.
6. Add assignment charge-period contracts, Student Fee Structure snapshot, and tenant/student/year idempotency.
7. Integrate the calculator into Payment Term and legacy demand generation using decimal amounts and calculation evidence.
8. Add correction/adjustment behavior and downstream read models for concession, collection, refund, reports, audit, accounting, and notifications.
9. Replace the frontend generic client and false-success attachment state; add server preview and explanation UI.
10. Run migration reconciliation and enable the feature per tenant only after a real policy is Active and attached.

Do not backfill a policy as if it had governed historical dues. Existing due rows should be marked as legacy/no-proration; only new or explicitly approved unpaid-demand replacements receive a Proration Policy snapshot.

## Test matrix

### Domain

- every lifecycle transition and forbidden transition;
- daily inclusive-day boundaries, no overlap, full overlap, legacy minimum-field compatibility, leap year;
- cutoff day equality, short months, no-charge and half-charge actions;
- all rounding modes and residual reconciliation;
- one-time exclusion and missing-rule full charge;
- annual/installment amount conservation and no overcharge.

### Application/API

- tenant/year/reference validation and exact permission on every endpoint;
- Draft cannot attach; restore returns Draft; archive blocked when attached/default;
- version snapshot remains stable after policy edit;
- Fee Structure impact and save produce the same validation;
- assignment preview and commit produce identical totals for the same version/input;
- manual override requires permission and reason;
- tenant/student/year idempotency and concurrent duplicate rejection;
- atomic rollback when any due, audit, accounting, or outbox write fails.

### End-to-end Finance

- admission on first, cutoff, day after cutoff, last day, and outside coverage;
- exit truncation under Daily rule;
- service-specific interval overrides only the matching optional fee;
- proration then concession, late fee, partial payment, refund, and adjustment;
- policy edit does not alter existing assignment or due explanation;
- report totals reconcile scheduled - proration - concession +/- adjustment - paid/refunded = outstanding;
- bulk assignment has deterministic all-or-none/per-student results according to the chosen command contract.

### Frontend/browser

- permission-gated menu, route, buttons, attachment, versions, and overrides;
- no fake fee heads or success toast after failed/unsupported calls;
- Save Draft versus Activate validation;
- impact warnings and blocking errors are actionable;
- preview line totals match API exactly;
- stale version, duplicate submit, dirty-close, and retry behavior;
- responsive and keyboard-accessible policy, assignment, and explanation surfaces.

## Implemented boundary decisions

1. Daily and MonthlyCutoff support independent admission and withdrawal settings. Withdrawal uses its own before-cutoff action.
2. Zero overlap remains zero. Daily proration uses eligible calendar days without a minimum-day floor.
3. Admission after cutoff with Charge Next Month waives the admission month's share and starts charging subsequent covered months. The waived share is not carried forward.

Withdrawal recorded after demands have already been created requires an auditable adjustment workflow; this change does not automatically rewrite those demands or receipts.

## Definition of done

- [x] Current Proration UI is no longer connected to a nonexistent generic endpoint.
- [x] Policy lifecycle, versioning, default, and attachment rules are enforced server-side.
- [x] Fee Structure and Student Fee Structure store exact policy-version snapshots.
- [x] Day-level charge periods have an authoritative source and audited override path.
- [x] Payment Term coverage is complete for every proratable recurring detail.
- [x] One server calculator powers simulation, preview, and demand generation.
- [x] Fee amounts use decimal arithmetic and preserve currency totals.
- [x] DueFee stores scheduled amount and enough proration evidence for reconstruction.
- [x] Concession, collection, payment, refund, adjustment, reports, accounting, and notifications consume the stored result consistently.
- [x] Permissions are enforced at API and frontend action level.
- [x] Duplicate retries and concurrent assignments cannot create duplicate dues.
- [x] Historical dues are not silently repriced.
- [ ] Domain, API, workflow, and browser acceptance suites pass.

The domain proration suite and compile checks pass. Full API/workflow/browser acceptance remains an operational follow-up because those suites require the integrated test host and browser environment.

## Implementation source map

| Concern | Current source |
|---|---|
| Frontend proration form/types | In the sibling frontend repository: `Shikshamitra-FE/src/screens/finance/policies/hooks/useProrationPolicyForm.ts`, `Shikshamitra-FE/src/screens/finance/policies/components/proration/**`, `Shikshamitra-FE/src/screens/finance/policies/types/finance-policy.types.ts` |
| Generic frontend client | In the sibling frontend repository: `Shikshamitra-FE/src/redux/finance/financePolicy.ts` |
| Fee Structure frontend | In the sibling frontend repository: `Shikshamitra-FE/src/screens/finance/settings/FeeStructure/Components/FeePoliciesTab.tsx`, `IndependentFeeStructureCard.tsx`, `Shikshamitra-FE/src/redux/finance/financeFeeStructure.tsx` |
| Student assignment frontend | In the sibling frontend repository: `Shikshamitra-FE/src/screens/finance/FeeStructureAssign/**` |
| Existing policy version store | `src/ShikshaMitra.Domain/Finance/Policies/FinancePolicyVersion.cs` |
| Fee Structure domain/contracts | `src/ShikshaMitra.Domain/Finance/Fees/Settings/FeeStructures/FeeStructure.cs`, `src/ShikshaMitra.Application.Contracts/Finance/Fees/Settings/FeeStructures/**` |
| Assignment aggregate/contracts | `src/ShikshaMitra.Domain/Finance/Fees/StudentFeeAssignment/Aggregates/StudentFeeStructure.cs`, `src/ShikshaMitra.Application.Contracts/Finance/Fees/StudentFeeAssignment/**` |
| Current demand generation | `src/ShikshaMitra.Domain/Finance/Fees/Collections/Managers/DueFeeManager.cs` |
| Current obligation model | `src/ShikshaMitra.Domain/Finance/Fees/Collections/Aggregates/DueFee.cs` |
| Existing proration permissions | `src/ShikshaMitra.Domain.Shared/Constants/Permissions/SubmodulePermissions/Finance/ShikshaMitraSubmodulePermissions.Finance.Policies.cs` |
| Reference lifecycle pattern | `docs/modules/finance/references/concession-policy-end-to-end.md` |
