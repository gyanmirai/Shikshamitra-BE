---
module: finance
domain: concession-policy
content_type: Reference
status: active
updated_on: 2026-09-01
owner: backend-team
audience: humans-and-agents
---

# Operate concession policies end to end

TL;DR: a concession policy belongs to one academic year, is attached to a fee structure, and is snapshotted when that structure is assigned to a student.
The assignment does not grant a concession.
A staff member selects a concession type on a request, while the server calculates and enforces the policy snapshot during request and approval.

## Confirmed product decisions

| Decision | Shipped behavior |
|---|---|
| Scope | A policy belongs to an academic year, not a board, medium, class, or division |
| Status | `Draft`, `Active`, and `Archived`; create/edit accepts Draft or Active, while archive and unarchive are explicit actions |
| Unarchive | Restores an archived policy to Draft so staff must review it before activation |
| Default | Only an Active policy can be default for its academic year; selecting another default atomically clears the previous Concession Policy default |
| Dates | No effective date or end date is stored; the academic-year scope and assignment snapshot control applicability |
| Concession type | Selected only on the concession request; it supplies the business reason and document rules |
| Fee structure | Every new fee structure assignment requires an attached Active concession policy from the same academic year |
| Student assignment | Stores the exact policy ID and immutable policy-version ID |
| Grant point | No concession is granted during fee-structure assignment; only an approved request changes due amounts |
| Paid fees | Policy application is limited to unpaid due rows; it does not create refunds, wallets, or credits against paid fees |

## End-to-end flow

```text
Academic year
  -> Finance admin creates and activates concession policy
  -> Finance admin attaches policy to fee structure
  -> Fee structure is assigned to student
  -> Assignment snapshots policy ID + immutable version ID
  -> Staff opens concession request
  -> Staff selects concession type and eligible fee heads
  -> Server resolves the assignment snapshot and calculates/validates amount
  -> Different authorized staff member approves or rejects
  -> Approval allocates the approved amount to live unpaid DueFee rows
  -> Due balances and request audit history are updated transactionally
```

## Setup order for school staff

1. Configure fee heads and concession types for the academic year.
2. Create a concession policy as Draft.
3. Choose the value source, calculation method, eligible fee heads, and installment timing.
4. Use simulation to verify typical cases.
5. Change the policy to Active.
6. Attach the Active policy to each relevant fee structure.
7. Assign the fee structure to students.
8. Create concession requests only when a student is eligible.
9. Have another authorized staff member review and approve or reject each request.

This order keeps the routine request form short.
Staff normally selects the student, concession type, fee heads, and evidence; configuration stays in policy setup.

## Policy fields and behavior

| Field | Options | Server rule |
|---|---|---|
| Value source | `PolicyFixed`, `RequestEnteredWithinLimits` | Fixed policies calculate the value automatically; request-entered policies let staff propose a value up to the eligible unpaid amount |
| Calculation method | `Percentage`, `FixedAmount`, `FullWaiver` | Always calculated on the eligible unpaid base |
| Percentage/fixed value | Conditional | Required only for the matching PolicyFixed method |
| Maximum percentage | Optional | Caps request-entered percentages and the resulting concession |
| Fee-head scope | Entire structure or selected fee heads | The request cannot include a fee head outside the snapshot |
| Apply on | All outstanding, current installment, future installments | Filters eligible unpaid due rows before calculation and approval |
| Rounding | None or configured policy rounding | Applied by the shared server calculator |

## Calculation rules

Let `eligibleBase` be the sum of the currently eligible unpaid due amounts after fee-head and installment filters.

| Method | Initial result |
|---|---|
| Percentage | `eligibleBase x percentage / 100` |
| Fixed amount | Configured or requested fixed amount |
| Full waiver | `eligibleBase` |

The server then applies rounding and these boundaries:

- the eligible outstanding amount;
- maximum percentage for a request-entered percentage, when configured.

The frontend preview is advisory.
Create and approve commands calculate against live server data, so changing balances or duplicate allocations cannot bypass limits.

## Policy versioning and changes

Every create or edit writes an immutable `FinancePolicyVersion` snapshot.
A student assignment continues using the version captured at assignment time, even if the policy is edited later.
Saving a fee structure again with the current policy attaches its current version for future student assignments.

An attached policy cannot be archived.
Move fee structures to a replacement Active policy first, then archive the old policy.
Cloning is the recommended way to prepare the next academic year's policy.

## Fee-structure assignment behavior

The backend rejects assignment when any of these conditions is true:

- the fee structure has no concession policy;
- the policy is not Active;
- the policy belongs to a different academic year;
- the referenced policy version is missing or does not belong to that policy.

For assignments created before this feature, the migration leaves empty snapshot identifiers.
After staff attaches an Active policy to the existing fee structure, the first assignment read or concession request safely hydrates the missing academic-year and policy snapshot.
It never replaces a non-empty historical snapshot.

## Concession request and approval

The request stores these two independent concepts:

| Concept | Source | Purpose |
|---|---|---|
| Concession type | Selected on request | Reason/category, eligibility language, and required documents |
| Concession policy version | Student fee assignment | Calculation, maximum percentage, fee-head scope, and installment timing |

At request creation, the server verifies the student, academic year, active concession type, assignment snapshot, eligible due rows, duplicate pending requests, and maximum percentage.
At approval, it re-reads live due rows and rejects any allocation that is duplicated, outside the requested fee heads, above the requested amount, above outstanding, or whose allocation sum is inconsistent.

The request creator cannot approve or reject the same request.
Approval applies allocations and updates due balances within the same transaction.

## HTTP API

All routes are under `/api/finance/concession-policies` and require authentication plus their matching permission.

| Method | Route | Purpose |
|---|---|---|
| GET | `/` | List policies for one required academic year, with search and status filters |
| GET | `/{id}` | Read one policy |
| POST | `/` | Create Draft or Active policy and version 1 |
| PUT | `/{id}` | Edit policy and create the next immutable version |
| POST | `/{id}/clone` | Clone as Draft, optionally into another academic year |
| DELETE | `/{id}` | Archive when not attached |
| POST | `/{id}/unarchive` | Restore to Draft |
| GET | `/{id}/versions` | Read immutable version history |
| POST | `/{id}/simulate` | Run the shared server calculator for an example base/value |

Fee-structure create/update contracts carry `concessionPolicyId`.
Student assignment responses expose the policy name, code, version, calculation configuration, fee-head scope, maximum percentage, and timing needed by the request UI.

## Permissions

Concession policy permissions include View, Add, Edit, Clone, Simulate, Archive, Unarchive, ViewVersions, and ManageAttachment.
Concession request permissions separately include Add, Edit, View, Approve, Reject, Cancel, and document actions.
Finance administrators receive module permissions through the existing role-permission expansion; restricted roles must be granted only the actions they perform.

## Deployment and rollout

1. Apply migration `AddConcessionPolicyEndToEnd`.
2. Deploy backend before exposing the new frontend.
3. Create and activate at least one policy for each open academic year.
4. Edit fee structures and attach the correct policy.
5. Confirm a sample existing assignment hydrates its policy snapshot.
6. Confirm a new assignment captures a non-empty version ID.
7. Run one request through create, review, approve, and balance verification.

Do not bulk-update historical non-empty assignment snapshots.
They are audit evidence of the rules assigned at that time.

## Acceptance checklist

- Draft policies cannot be attached or used for new assignments.
- A policy from another academic year is rejected.
- Selected-fee-head policy blocks unlisted heads in the request.
- Current-installment and future-installment policies exclude other due rows.
- PolicyFixed values are read-only in the request UI and recalculated on the server.
- Request-entered percentages above the configured maximum percentage are rejected.
- Every concession amount is limited to the live eligible unpaid amount.
- Creator cannot approve or reject the same request.
- Approved allocation cannot exceed current outstanding.
- Editing a policy does not mutate existing assignment snapshots.
- Attached policy cannot be archived.
- Unarchive returns the policy to Draft.

## Source map

| Concern | Source |
|---|---|
| Policy aggregate and snapshot | `src/ShikshaMitra.Domain/Finance/Policies/ConcessionPolicy.cs` |
| Calculator | `src/ShikshaMitra.Domain/Finance/Policies/ConcessionPolicyCalculationService.cs` |
| Request enforcement | `src/ShikshaMitra.Domain/Finance/Fees/Concessions/Managers/ConcessionRequestManager.cs` |
| Policy application service | `src/ShikshaMitra.Application/Finance/Policies/ConcessionPolicyAppService.cs` |
| HTTP routes | `src/ShikshaMitra.HttpApi/Controllers/Finance/Policies/ConcessionPoliciesController.cs` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Finance/Policies/ConcessionPolicyConfiguration.cs` |
