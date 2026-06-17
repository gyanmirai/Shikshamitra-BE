# Leave Policy Rules Shared Understanding

Status: target product decision, implementation pending
Last updated: 2026-06-16

## Why This Exists

ShikshaMitra is a school ERP. For schools, the most important leave distinction is payroll treatment: paid leave or unpaid leave. Labels such as Sick Leave, Casual Leave, Earned Leave, Maternity Leave, or Paternity Leave are not separate entitlement buckets in the target model.

If an employee is sick and the school wants to pay them, the request uses the Paid bucket. If the school treats that absence as loss of pay, the request uses the Unpaid bucket. The reason or description can still say "sick", "family function", "maternity", or any other human explanation, but that text does not create a leave type, balance bucket, or policy rule.

## Domain Language

| Term | Meaning |
|------|---------|
| Leave policy | Assignable bundle for an academic year or effective period. Example: Teaching Staff Leave Policy. |
| Paid rule | The paid leave bucket inside a policy. It grants paid leave entitlement and does not reduce salary when approved. |
| Unpaid rule | The unpaid leave bucket inside a policy. It controls loss-of-pay leave. It may have a limited count or be unlimited. |
| Leave reason | Free-text/request explanation entered by the employee or HR. Example: sick, wedding, family emergency. It is not a configured leave type. |
| Default leave policy | The fallback policy when no employee or group assignment overrides it. |
| Policy assignment | Employee or group rule that chooses the effective leave policy for a date range. |
| Effective leave policy | The leave policy resolved for an employee on a leave request date. |

## Target Model

1. There is no separate HR Leave Type settings/catalog surface.
2. Leave Management settings should not expose Leave Types.
3. Policies & Rules is the only configuration surface for leave entitlement.
4. A leave policy is academic-year/effective-period scoped and assignable through default, employee, or group assignment.
5. Each active leave policy has exactly two rule buckets:
   - `Paid`
   - `Unpaid`
6. A policy cannot have duplicate active rules for the same paid type.
7. The policy header must not have a single `LeaveTypeId`.
8. Policy rules must not reference a leave type catalog.
9. Leave request creation selects payroll treatment (`Paid` or `Unpaid`) and captures a reason/description.
10. Balances, validation, approval checks, reporting, and payroll impact are calculated by employee + policy + paid type.
11. Historical leave requests must keep the policy id, paid type, and rule snapshot that were valid when the request was created, so old records are not reinterpreted after policy edits.

## Rule-Level Configuration

Each of the two rule buckets owns the behavior for that paid type:

| Capability | Requirement |
|------------|-------------|
| Paid type | Fixed to `Paid` or `Unpaid`; users do not create additional leave type labels. |
| Entitlement | Number of days allowed for the bucket. Example: Paid = 12 days, Unpaid = 15 days. |
| Unlimited unpaid leave | The Unpaid bucket can be limited or unlimited. Unlimited should not show as a huge finite balance. |
| Accrual | Yearly upfront or monthly accrual where needed. |
| Carry forward | Whether unused balance carries forward, with an optional cap. Usually relevant to Paid. |
| Encashment | Whether unused balance can be encashed and when it is paid. Usually relevant to Paid. |
| Approval | Whether a leave request needs approval. |
| Request constraints | Min notice days, max days per request, half-day support, attachment requirement, negative-balance behavior. |
| Active state | The bucket can be disabled only through explicit policy rules; historical meaning is preserved. |

## Product Workflow

1. HR opens Policies & Rules > Leave Policies.
2. HR creates a leave policy with policy-level metadata: name, academic year/effective dates, status, default flag, description.
3. The form shows exactly two rule sections: Paid and Unpaid.
4. HR configures entitlement and operational rules for each section.
5. Assignment screens choose the policy bundle, not individual leave categories.
6. Leave request screens resolve the employee's effective policy first.
7. The leave request form offers Paid or Unpaid according to the effective policy, plus reason/description and date fields.
8. Balance views show Paid and Unpaid availability for the resolved policy.
9. Payroll treats approved Paid leave as payable and approved Unpaid leave as loss of pay.
10. Impact previews explain who is affected before a policy is archived, set as default, or substantially changed.

## Superseded Model

The previous direction had a tenant-wide leave type catalog and policy rules linked to leave types such as Sick, Casual, Earned, or Loss of Pay. That model is superseded.

Do not implement or expose:

- Leave Type settings in Leave Management.
- `/api/hr-leave-types` as a user-facing HR configuration surface.
- Policy rules that require `leaveTypeId`.
- Leave balances grouped by Sick/Casual/Earned/Maternity/etc.
- Leave request dropdowns that ask employees to choose configured leave type labels.

## Recommended ERP Direction

Use the existing leave policy assignment architecture as the foundation, but simplify the leave rule dimension to paid type.

1. Keep `LeavePolicy` as the assignable policy bundle.
2. Keep a rule child entity/DTO if useful, but key it by `Paid`/`Unpaid`, not by leave type.
3. Keep effective dating, default policy, employee assignment, group assignment, audit, and impact preview as first-class HR policy concerns.
4. Prefer non-destructive policy changes for assigned active policies. Preserve historical approved/pending requests and payroll snapshots.
5. Keep reason/description free text and searchable for reporting, but do not let it govern entitlement.

## Implementation Plan

### Phase 1: Align Requirements And Contracts

- Update PRD, API docs, FE docs, and technical references to remove Leave Type settings.
- Mark any existing leave-type APIs as legacy/internal until removed.
- Update leave policy DTOs so rules are keyed by `paidType` and no longer require `leaveTypeId`.
- Update leave request DTOs so the selected bucket is `paidType` (`Paid|Unpaid`) plus reason/description.

### Phase 2: Backend Simplification

- Remove HR `LeaveType` aggregate, repository, service, controller, permissions, seed data, and UI-facing contracts if no other current code path needs them.
- Change `LeavePolicyRule` uniqueness from policy + leave type to policy + paid type.
- Enforce at most one active `Paid` rule and one active `Unpaid` rule per policy.
- Support limited/unlimited entitlement for the Unpaid bucket.
- Update balance readers and validation to count usage by employee + policy + paid type.
- Preserve historical leave request payroll treatment when policies are edited.

### Phase 3: Frontend Policy Authoring

- Remove Leave Type settings from Leave Management navigation.
- Replace leave policy rule grids with exactly two rule sections: Paid and Unpaid.
- Keep controls for entitlement, accrual, approval, carry-forward, carry limit, encashment, max per request, half-day, attachment threshold, and negative balance.
- Show policy summaries as Paid entitlement, Unpaid entitlement/unlimited, approval/carry-forward/encashment indicators.

### Phase 4: Leave Request UX

- Remove configured leave type dropdowns.
- Show Paid/Unpaid choice from the resolved effective policy.
- Keep reason/description as the place where employees explain the nature of the leave.
- Show balance and payroll impact clearly: Paid = payable, Unpaid = loss of pay.
- Block submit until the effective leave policy is resolved.

### Phase 5: Data Migration And Cleanup

- Because this feature is not in production and the DB is new, no complex duplicate/backfill migration is required unless existing local seed data must be preserved.
- Replace seeded Sick/Casual/Earned/Loss-of-Pay rules with Paid/Unpaid policy buckets.
- Remove stale report/dashboard labels that imply leave type categories.
- Remove stale tests that assume leave type CRUD.

### Phase 6: QA

- Backend tests for creating/updating a policy with exactly Paid and Unpaid rules.
- Backend tests for duplicate paid-type rule rejection.
- Backend tests for limited and unlimited unpaid leave.
- Backend tests for leave request validation by paid type and policy assignment/default resolution.
- Backend tests for payroll: approved Paid leave remains payable; approved Unpaid leave reduces payable units.
- FE tests for policy form payloads, Paid/Unpaid request choice, stale policy resolution guard, and policy table summaries.
- Browser flow: create policy, set Paid/Unpaid counts, set default, submit Paid leave, submit Unpaid leave, verify payroll/balance behavior.

## Open Decisions

1. Should both Paid and Unpaid buckets always be required, or can a bucket be disabled by setting entitlement to zero?
2. How should unlimited unpaid leave be represented in the API: `isUnlimited`, `leaveCount = null`, or a dedicated entitlement mode?
3. Should carry-forward and encashment be allowed on Unpaid, or hidden/disabled because unpaid leave has no payable balance?
4. Is Monthly/Yearly accrual enough for v1, or do schools need joining-date proration now?
5. Can active assigned policies be edited in place, or should material changes require clone/version plus future reassignment?

## Shared Acceptance Criteria

- HR users configure leave through Leave Policies only.
- Leave Type settings are removed from Leave Management.
- A leave policy exposes exactly Paid and Unpaid rule buckets.
- No policy rule requires or stores a configured leave type label.
- Leave requests select Paid or Unpaid and capture a free-text reason.
- Paid leave is payroll-payable; Unpaid leave is loss of pay.
- Unlimited unpaid leave can be represented without fake huge balances.
- Employee and group assignments choose one leave policy bundle.
- A default leave policy can govern employees without explicit assignment.
- Historical leave records stay stable after policy edits.
