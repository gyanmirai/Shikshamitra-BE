---
module: finance
domain: fee-collection-waiver
content_type: Reference
status: active
updated_on: 2026-09-23
owner: finance-team
audience: humans-and-agents
---

# Apply waivers in Fee Collection end to end

TL;DR: waiver is a direct Fee Collection action, not a Finance submodule or approval workflow.
Each late-fee rule and each Charge Management charge has its own `Allow waiver` toggle.
When the source toggle is ON, an authorized collector enters only the waiver amount and reason in Fee Collection, and the server applies the waiver immediately.

> Product decision: this document defines the approved target flow.
> Implementation status: not yet shipped.
> Current source still keeps broader waiver settings in Collection Policy and supports a late-fee request/approval lifecycle.

## Final product decisions

| Question | Decision |
|---|---|
| Is there a Waiver tab under Finance > Fees? | No |
| Is there a separate Waiver Management screen? | No |
| Is there a global waiver setting? | No |
| Where is late-fee eligibility configured? | `Allow waiver` on each effective late-fee rule |
| Where is charge eligibility configured? | `Allow waiver` on each charge created in Charge Management |
| Where is a waiver applied? | Fee Collection only |
| What does the collector enter? | Amount and mandatory reason only |
| Does it need approval or rejection? | No; it is applied immediately |
| Can the original late fee or charge be overwritten? | No; the waiver is a separate non-cash reduction |

## Ownership boundaries

| Module | Owns | Does not own |
|---|---|---|
| Collection Policy | Late-fee calculation and the `Allow waiver` flag on each effective late-fee rule | Waiver amount, reason, application, approval, or queue |
| Charge Management | Charge creation and the charge-level `Allow waiver` flag | Applying a student's waiver |
| Fee Collection | Showing eligible balances and immediately applying amount plus reason | Configuring late-fee formulas or creating charges |
| Finance navigation | Existing Collection Policy, Charge Management, and Fee Collection screens | A Waiver tab or Waiver Management route |

## End-to-end user flow

```text
Collection Policy
  -> configure a late-fee rule
  -> set Allow waiver = ON or OFF for that rule

Charge Management
  -> create a charge
  -> set Allow waiver = ON or OFF for that charge

Fee Collection
  -> open a student
  -> choose an eligible late-fee or charge row
  -> click Waive
  -> enter amount and reason
  -> confirm
  -> server validates the live balance
  -> waiver is applied immediately
  -> payable/outstanding total refreshes
```

There is no `PendingApproval`, `Approved`, or `Rejected` state.
There is no request list, approver action, or separate waiver workspace.

## Late-fee toggle

Every effective late-fee rule exposes one boolean field:

| Field | Default | Meaning |
|---|---:|---|
| `Allow waiver` | OFF | Whether a positive unpaid late fee produced by this rule may be waived in Fee Collection |

Rules:

- a shared policy-level late-fee rule has one toggle applying to every late-fee line produced by that rule;
- when a fee head has a custom late-fee rule, that effective rule has its own toggle;
- `No late fee` produces no amount and therefore nothing to waive;
- changing the toggle affects newly assessed late-fee eligibility only;
- the eligibility value is captured with the assessed late-fee line so later policy edits do not reinterpret historical financial records;
- no waiver fee-head selector, amount cap, percentage cap, approval threshold, expiry date, or approval role is configured.

The payment term supplies the installment due date.
Collection Policy uses that date, its grace/calculation rules, and its late-fee fee-head scope to calculate the late fee.
The toggle does not create a late fee; it only says whether the resulting balance may be waived.

### Fee-head matching

| Situation | Late-fee result | Waiver result |
|---|---|---|
| Fee head is in both Fee Structure and the policy's late-fee scope | Late fee may be assessed after due/grace rules | `Waive` appears only when that effective rule's toggle was captured as ON |
| Fee head is in Fee Structure but not the policy's late-fee scope | No late fee from that policy | Nothing to waive |
| Fee head is in the policy scope but absent from Fee Structure | No student due exists for that head | Nothing to waive |

Example:

```text
Tuition late-fee rule:  Allow waiver = ON
Transport late-fee rule: Allow waiver = OFF

Student unpaid tuition late fee:   INR 300 -> Waive action available
Student unpaid transport late fee: INR 150 -> Waive action unavailable
```

## Charge toggle

Every charge created in Charge Management exposes:

| Field | Default | Meaning |
|---|---:|---|
| `Allow waiver` | OFF | Whether the approved charge's positive remaining student balance may be waived in Fee Collection |

Rules:

- the toggle is part of charge create and permitted pre-approval edit flows;
- approval copies the setting to every generated student charge line;
- after approval or any collection/waiver activity, the captured setting is immutable;
- a rejected, voided, pending, fully paid, or zero-balance charge cannot be waived;
- the charge does not need to match a Fee Structure or Collection Policy fee head;
- each new charge chooses its own value; Charge Category does not silently enable waiver.

Example:

```text
Transport damage charge: Amount INR 1,000, Allow waiver = ON
ID-card replacement charge: Amount INR 200, Allow waiver = OFF

Fee Collection shows Waive for the transport damage balance only.
```

## Fee Collection experience

Fee Collection is the only screen where waiver money is entered or changed.

Show `Waive` on a late-fee or charge row only when all conditions are true:

- the source row captured `Allow waiver = ON`;
- the source is active and collectible;
- remaining balance is greater than zero;
- the user has the target Fee Collection waiver permission;
- the student is within the user's Finance scope.

Do not show an approval status or route the user to another screen.

### Waiver dialog

Display source information as read-only context and allow only two editable fields.

| Field | Editable | Rule |
|---|---:|---|
| Student | No | Current student |
| Type | No | Late fee or Charge |
| Source | No | Fee head/due date or charge name/category |
| Remaining amount | No | Current server-provided balance |
| Waiver amount | Yes | Required, greater than zero, at most the live remaining balance |
| Reason | Yes | Required and trimmed |

Primary action: `Apply waiver`.

Confirmation text:

```text
Apply a waiver of INR {amount}?
This will immediately reduce the student's outstanding balance and cannot be treated as a payment.
```

After success, close the dialog and refresh the student's late fees, charges, totals, ledger, and available actions.

## Server validation and transaction

The server is authoritative and performs these steps in one transaction:

1. Load the student and source late-fee or charge row inside the tenant boundary.
2. Verify the user's Fee Collection permission and student scope.
3. Verify the source captured `Allow waiver = ON`.
4. Reject inactive, rejected, voided, pending, or otherwise non-collectible sources.
5. Recalculate the live remaining balance.
6. Validate `0 < waiver amount <= live remaining balance`.
7. Require a non-empty reason.
8. Create an immutable waiver application record.
9. Reduce the collectible balance without changing the original assessment or charge.
10. Write the audit/ledger entry and commit.
11. Return refreshed balances.

The command uses an idempotency key and concurrency protection so double-clicks and concurrent collections cannot apply more than the remaining balance.

## Money rules

### Late fee

```text
remainingLateFee =
  max(0, assessedLateFee - paidLateFee - appliedLateFeeWaivers)

newWaiver <= remainingLateFeeAtCommit

newRemainingLateFee = remainingLateFee - newWaiver
```

### Charge

```text
remainingCharge =
  max(0, originalChargeAmount - collectedChargeAmount - appliedChargeWaivers)

newWaiver <= remainingChargeAtCommit

newRemainingCharge = remainingCharge - newWaiver
```

A waiver never becomes a payment, receipt, concession, adjustment, refund, or student credit.

## Example from collection to completion

```text
Approved charge amount             INR 1,000
Collected earlier                    INR 300
Already waived                         INR 0
Current outstanding                  INR 700

Collector clicks Waive
Waiver amount                        INR 500
Reason                    "Management approval"

Server applies immediately           INR 500
New outstanding                      INR 200
Cash received                          INR 0
Approval status                         None
```

## Target contracts

These are approved target changes, not current shipped contracts.

| Area | Target change |
|---|---|
| Effective late-fee rule/snapshot | Add `AllowWaiver` boolean |
| Charge create/update/detail | Add `AllowWaiver` boolean |
| Student charge line | Capture `AllowWaiver` at charge approval |
| Fee Collection row | Return `canWaive`, remaining amount, and source type/id |
| Apply command | Accept source type, source ID, amount, reason, and idempotency key |
| Application record | Store source, student, amount, reason, actor, timestamp, and reversal link if supported |

Proposed Fee Collection endpoint:

```http
POST /api/finance/fee-collections/waivers
```

```json
{
  "sourceType": "LateFee",
  "sourceId": "guid",
  "amount": 200,
  "reason": "Approved by management",
  "idempotencyKey": "client-generated-key"
}
```

The current `late-fee-waivers` list/approve/reject API is retired after migration.
No list, approve, or reject endpoint replaces it.

## Permissions

The target keeps waiver authorization inside Fee Collection, without creating a Waiver permission family or submodule.

| Target permission | Purpose |
|---|---|
| `Finance.Fees.FeeCollections.ApplyWaiver` | Apply an eligible late-fee or charge waiver from Fee Collection |

This is a proposed permission and is not currently shipped.
It should remain separate from ordinary payment collection because a waiver reduces receivables without receiving cash.

## Ledger, receipt, reporting, and audit

| Surface | Required result |
|---|---|
| Fee Collection | Original, paid, waived, and remaining amounts refresh immediately |
| Student ledger | Record a non-cash waiver with source reference, reason, actor, and timestamp |
| Receipt | Do not generate a receipt for the waiver; receipts represent cash/payment only |
| Late-fee report | Show assessed, paid, waived, and remaining late fee separately |
| Charge report | Show original, collected, waived, and outstanding charge separately |
| Audit Trail | Capture source toggle changes and each applied waiver with before/after balances |

## Correction and reversal

A posted waiver is immutable and is not edited or deleted.
If the school must correct it, use an authorized reversal linked to the original application, with a mandatory reason.
The reversal restores only the amount originally reduced and must not create an invalid balance.
No separate Waiver screen is needed; the action can be exposed from the student's Fee Collection history.

## Migration and rollout

1. Remove the proposed Waiver tab, route, queue, settings, and `Finance.Fees.Waivers.*` permissions from the target design.
2. Replace broad Collection Policy waiver configuration with `AllowWaiver` on each effective late-fee rule.
3. Remove fee-head waiver selectors, amount/percentage caps, thresholds, expiry, roles, and approval settings.
4. Add `AllowWaiver` to Charge Management create/edit/detail and capture it on approved student charge lines.
5. Add `ApplyWaiver` to the Fee Collection permission family.
6. Add the common amount-and-reason dialog to eligible late-fee and charge rows.
7. Replace request/approve/reject behavior with one transactional apply command.
8. Preserve existing applied waiver history as immutable financial evidence.
9. Resolve existing pending requests before cutover; do not silently apply them.
10. Update collection, ledger, report, audit, and reversal calculations for both source types.

## Team acceptance scenarios

1. Confirm Finance > Fees has no Waiver tab or Waiver Management route.
2. Configure two late-fee rules with opposite toggle values and confirm only the ON rule produces a waivable balance.
3. Create two charges with opposite toggle values and confirm only the ON charge is waivable after approval.
4. Confirm Fee Collection asks only for amount and reason.
5. Apply a waiver and confirm there is no pending, approve, or reject step.
6. Confirm amount zero, negative, or above the live remaining balance is rejected.
7. Confirm a concurrent payment causes the server to revalidate rather than over-waive.
8. Confirm original late-fee and charge amounts remain unchanged.
9. Confirm receipts do not count waiver as cash.
10. Confirm ledger, reports, and audit show the waiver separately.
11. Confirm rejected, voided, pending, and fully paid charges cannot be waived.
12. Confirm policy/charge toggle changes do not reinterpret historical source rows.
13. Confirm an unauthorized or out-of-scope collector cannot apply a waiver through the API.

## Source impact map

Current source paths expected to change during implementation:

- Collection Policy late-fee rules and snapshots: `src/ShikshaMitra.Domain/Finance/Policies/CollectionPolicy.cs`, `src/ShikshaMitra.Domain/Finance/Policies/CollectionPolicySnapshot.cs`
- Collection Policy contracts: `src/ShikshaMitra.Application.Contracts/Finance/Policies/CollectionPolicyDtos.cs`
- Charge aggregate and contracts: `src/ShikshaMitra.Domain/Finance/Fees/Charges/Aggregates/Charge.cs`, `src/ShikshaMitra.Application.Contracts/Finance/Fees/Charges/Dtos/**`
- Existing waiver application/service: `src/ShikshaMitra.Domain/Finance/Fees/Collections/Aggregates/LateFeeWaiverApplication.cs`, `src/ShikshaMitra.Application/Finance/Fees/Collections/LateFeeWaiverAppService.cs`
- Fee Collection contracts: `src/ShikshaMitra.Application.Contracts/Finance/Fees/Collections/**`
- Finance permissions: `src/ShikshaMitra.Domain.Shared/Constants/Permissions/SubmodulePermissions/Finance/ShikshaMitraSubmodulePermissions.Finance.Fees.cs`
- Frontend Collection Policy: `../Shikshamitra-FE/src/screens/finance/policies/**`
- Frontend Charge Management: `../Shikshamitra-FE/src/screens/finance/charges/**`
- Frontend Fee Collection: `../Shikshamitra-FE/src/screens/finance/FeeCollection/**`

## Verification plan

Backend coverage must prove toggle capture, both source types, permission/scope enforcement, live-balance validation, idempotency, concurrency, immutable original amounts, ledger/report separation, and reversal integrity.

Frontend coverage must prove no Waiver route, correct action visibility, two-field dialog behavior, server error display, refreshed totals, and narrow-layout accessibility.

```powershell
# Backend
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~Waiver"
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Waiver"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~Waiver"
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module finance

# Frontend
npm run ts
node scripts/docs/validate-docs.mjs --module finance
```
