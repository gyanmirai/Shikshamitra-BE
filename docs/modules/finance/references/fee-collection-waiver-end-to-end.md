---
module: finance
domain: fee-collection-waiver
content_type: Reference
status: active
updated_on: 2026-09-24
owner: finance-team
audience: humans-and-agents
---

# Apply waivers in Fee Collection end to end

## Late fees are collection-only system charges

When the collection policy calculates a positive late fee for a due-fee line, the collect-form
API automatically projects that amount into the Fee Collection `charges` list with:

- `sourceType = LateFee`;
- `sourceId` and `chargeId` equal to the originating `DueFeeId`;
- `isSystemGenerated = true`;
- a name containing the fee head and installment;
- the calculated explanation, paid amount, waived amount, and remaining amount.

This is a system-generated collection obligation, not a Charge Management request. It is never
inserted into or listed from the Charge/ChargeRequest aggregates, so Charge Management continues
to show only user-created charge requests. Collection and waiver commands route the row back to
the existing late-fee allocation and waiver records by `DueFeeId`; they do not create a duplicate
charge ledger entry.

TL;DR: waiver is a direct Fee Collection operation.
Collection Policy and Charge Management contain no waiver configuration.
Every eligible system-generated late-fee charge and approved outstanding manual charge displays a waiver toggle in Fee Collection; enabling it requires only a reason and waives the full live remaining balance.

> Product decision: this document defines the implemented flow.

## Final decisions

| Question | Target decision |
|---|---|
| Does Collection Policy contain waiver fields? | No |
| Does Charge Management contain an `Allow waiver` field? | No |
| Is waiver availability configured globally or per source? | No |
| Where is the waiver control? | On each eligible charge row in Fee Collection, including system-generated late-fee charges |
| What does the collector enter? | Mandatory reason only |
| Is partial waiver supported? | No; the full live remaining balance is waived |
| Is approval or rejection required? | No |
| Is there a Waiver tab or separate route? | No |
| Is the original amount overwritten? | No; waiver is stored as a separate non-cash reduction |

## Module ownership

| Module | Owns | Does not own |
|---|---|---|
| Collection Policy | Due-date/grace rules, late-fee calculation, caps, rounding, and applicable fee heads | Waiver configuration or execution |
| Charge Management | Charge creation, audience, approval, rejection, voiding, and original amount | Waiver configuration or execution |
| Fee Collection | System late-fee charge projection, waiver toggle, reason, confirmation, immediate application, refreshed balance, and history | Late-fee formula or manual charge creation |
| Finance navigation | Collection Policy, Charge Management, and Fee Collection screens | Waiver tab, settings page, or approval queue |

## User flow

```text
Collection Policy
  -> calculates a late fee
  -> no waiver field exists

Charge Management
  -> creates and approves a charge
  -> no waiver field exists

Fee Collection
  -> collector opens a student
  -> calculated late fee appears automatically in Additional Charges as System generated
  -> each eligible system/manual charge row shows Waiver toggle = OFF
  -> collector turns the required row ON
  -> enters mandatory reason
  -> confirms Apply waiver
  -> server rereads and waives the full live remaining balance
  -> collection totals refresh immediately
```

There is no Draft, Pending, Approved, Rejected, or Applied approval state.
There is no request list, checker action, global setting, or Waiver Management workspace.

## Eligible Fee Collection rows

### System-generated late-fee charge

Show the waiver toggle when:

- the server-calculated remaining late fee is greater than zero;
- the due row and fee assignment are active and collectible;
- the user has the target Fee Collection waiver permission;
- the student is inside the user's Finance scope.

Collection Policy determines whether a late fee exists, not whether it may be waived.
If a fee head is outside the policy's late-fee scope, no late fee is calculated and no waiver toggle appears.

| Fee-head situation | Result in Fee Collection |
|---|---|
| Head exists in Fee Structure and the policy's late-fee scope | A positive calculated remaining late fee creates a collection-only system charge with the waiver toggle |
| Head exists in Fee Structure but not the late-fee scope | No late fee exists, so no waiver toggle appears |
| Head exists in the policy scope but not Fee Structure | No student due exists, so no waiver toggle appears |

The payment term supplies the installment due date.
Collection Policy applies its grace and calculation rules to that due date.
Waiver acts only on the resulting remaining late-fee balance.

### Charge

Show the waiver toggle when:

- the student charge was created through Charge Management;
- the charge line is `Approved` and collectible;
- it is not pending, rejected, voided, or cancelled;
- its remaining balance is greater than zero;
- the user has the target Fee Collection waiver permission;
- the student is inside the user's Finance scope.

The charge does not need to match a Collection Policy fee head.
Charge Management creates the obligation; Fee Collection alone decides whether the authorized collector applies a waiver.

## Fee Collection interaction

Each eligible row displays:

| UI element | Behavior |
|---|---|
| Waiver toggle | OFF by default; turning ON selects the row for full waiver |
| Reason | Appears and becomes required only while the toggle is ON |
| Waiver amount | Read-only; equals the server-provided current remaining balance |
| Remaining after waiver | Read-only zero for the selected row |

The collector cannot type or edit the waiver amount.
Turning the toggle OFF before confirmation removes the reason and makes no change.

### Confirmation

```text
Waive the full remaining amount of INR {liveRemainingAmount}?
Reason: {reason}

This is a non-cash reduction and will immediately change the student's outstanding balance.
```

Primary action: `Apply waiver`.

After success, Fee Collection refreshes the student's late fees, charges, payable total, ledger, and row actions.

## Server transaction

The server performs the operation in one transaction:

1. Load the student and source row inside the tenant boundary.
2. Verify the dedicated Fee Collection waiver permission and student scope.
3. Verify that the late fee or charge is currently eligible and collectible.
4. Require a non-empty trimmed reason.
5. Recalculate the source's live remaining balance.
6. Reject the command if the live remaining balance is zero.
7. Use the complete live remaining balance as the applied waiver amount.
8. Create an immutable waiver application record.
9. Reduce the collectible balance without changing the original late fee or charge amount.
10. Write ledger and audit evidence.
11. Commit and return refreshed balances.

The command requires an idempotency key and concurrency protection.
A double-click or retry returns the first successful result and never applies another waiver.

## Money rules

### Late fee

```text
remainingLateFeeBefore =
  max(0, calculatedLateFee - paidLateFee - previousAppliedWaivers)

appliedWaiver = remainingLateFeeBefore
remainingLateFeeAfter = 0
```

### Charge

```text
remainingChargeBefore =
  max(0, originalChargeAmount - collectedChargeAmount - previousAppliedWaivers)

appliedWaiver = remainingChargeBefore
remainingChargeAfter = 0
```

The server calculates the amount at commit time.
The UI never submits a trusted monetary value.

## Example

```text
Approved transport-damage charge     INR 1,000
Already collected                      INR 300
Current outstanding                    INR 700

Collector turns Waiver ON
Reason: "Charge waived by management"

Server-applied waiver                  INR 700
New outstanding                          INR 0
Cash received                            INR 0
Approval step                              None
```

## Target contract

These are the active contracts for this flow.

| Area | Target change |
|---|---|
| Collection Policy contracts | Remove all waiver fields |
| Charge Management contracts | Do not add or retain waiver settings |
| Fee Collection system late-fee charge | Return `sourceType = LateFee`, `sourceId = DueFeeId`, server-derived `canWaive`, and current remaining amount |
| Fee Collection charge row | Return server-derived `canWaive` and current remaining amount |
| Apply command | Accept source type, source ID, reason, and idempotency key; do not accept amount |
| Waiver record | Store source, student, server-calculated applied amount, reason, actor, timestamp, and reversal link if supported |

Proposed Fee Collection endpoint:

```http
POST /api/finance/fee-collections/waivers
```

```json
{
  "sourceType": "Charge",
  "sourceId": "guid",
  "reason": "Charge waived by management",
  "idempotencyKey": "client-generated-key"
}
```

The response returns the applied waiver amount and refreshed source/student totals.
The current late-fee waiver list, request, approve, and reject endpoints are retired after migration.

## Permission

Waiver authorization remains inside Fee Collection without creating a Waiver submodule.

| Permission | Purpose |
|---|---|
| `Finance.Fees.FeeCollections.ApplyWaiver` | Immediately apply a full waiver to an eligible late-fee or charge row |

The dedicated permission controls the server mutation. The collect-form balance controls visibility so stale frontend permission state cannot hide an eligible toggle.

## Ledger, receipt, reporting, and audit

| Surface | Required behavior |
|---|---|
| Fee Collection | Refresh original, paid, waived, and remaining amounts immediately |
| Student ledger | Record a non-cash waiver with source, amount, reason, actor, and timestamp |
| Receipt | Do not generate a payment receipt for the waiver |
| Late-fee report | Show calculated, paid, waived, and remaining amounts separately |
| Charge report | Show original, collected, waived, and outstanding amounts separately |
| Audit Trail | Record before/after balances, reason, actor, timestamp, idempotency key, and correlation ID |

A waiver is not a payment, concession, adjustment, refund, or student credit.

## Correction and reversal

An applied waiver is immutable and cannot be edited or deleted.
If correction is required, use an authorized reversal linked to the original waiver, with a mandatory reason.
The reversal restores only the amount reduced by the original waiver and must respect later financial activity.
The reversal action can appear in Fee Collection history; it does not require a separate Waiver screen.

## Migration

1. Remove all waiver fields from active Collection Policy domain, DTOs, validation, snapshots, and frontend forms.
2. Do not add waiver fields to Charge Management create/edit/detail.
3. Remove the Waiver menu, route, settings, request list, and approval/rejection UI.
4. Retire waiver settings and approval/threshold/role permission behavior.
5. Authorize waiver application through the Fee Collection `ApplyWaiver` permission.
6. Add server-derived `canWaive` to late-fee and approved-charge rows.
7. Add the toggle and reason-only interaction to Fee Collection.
8. Replace amount/request/approve/reject commands with one full-balance apply command.
9. Preserve historical applied waivers as immutable evidence.
10. Resolve existing pending requests explicitly before cutover; never auto-apply them.
11. Update ledger, reports, audit, concurrency, idempotency, and reversal behavior for both source types.

## Acceptance scenarios

1. Confirm Collection Policy create/edit/detail contains no waiver field.
2. Confirm Charge Management create/edit/detail contains no waiver field.
3. Confirm Finance navigation has no Waiver tab or route.
4. Confirm every positive collectible late fee appears as a system-generated charge with the Fee Collection waiver toggle.
5. Confirm system-generated late-fee charges do not appear in Charge Management.
6. Confirm every approved positive outstanding manual charge row shows the toggle.
7. Confirm pending, rejected, voided, cancelled, fully paid, and zero-balance charge rows do not show it.
8. Turn a toggle ON and confirm reason is the only editable waiver field.
9. Confirm the frontend does not send a waiver amount.
10. Apply the waiver and confirm the server uses the full live remaining balance.
11. Confirm the outstanding balance becomes zero immediately without approval.
12. Confirm concurrent collection/waiver activity cannot over-reduce the balance.
13. Confirm the original late-fee or charge amount remains unchanged.
14. Confirm no cash receipt is generated.
15. Confirm ledger, reports, and audit identify the waiver separately.
16. Confirm unauthorized and out-of-scope users cannot apply the operation through the API.

## Source impact map

Current source paths expected to change during implementation:

- Collection Policy domain/snapshots: `src/ShikshaMitra.Domain/Finance/Policies/CollectionPolicy.cs`, `src/ShikshaMitra.Domain/Finance/Policies/CollectionPolicySnapshot.cs`
- Collection Policy contracts: `src/ShikshaMitra.Application.Contracts/Finance/Policies/CollectionPolicyDtos.cs`
- Current waiver aggregate/service: `src/ShikshaMitra.Domain/Finance/Fees/Collections/Aggregates/LateFeeWaiverApplication.cs`, `src/ShikshaMitra.Application/Finance/Fees/Collections/LateFeeWaiverAppService.cs`
- Fee Collection contracts: `src/ShikshaMitra.Application.Contracts/Finance/Fees/Collections/**`
- Charge balance contracts: `src/ShikshaMitra.Application.Contracts/Finance/Fees/Charges/Dtos/**`
- Finance permissions: `src/ShikshaMitra.Domain.Shared/Constants/Permissions/SubmodulePermissions/Finance/ShikshaMitraSubmodulePermissions.Finance.Fees.cs`
- Frontend Collection Policy cleanup: `../Shikshamitra-FE/src/screens/finance/policies/**`
- Frontend Fee Collection toggle: `../Shikshamitra-FE/src/screens/finance/FeeCollection/**`
- Frontend Waiver route/menu removal: `../Shikshamitra-FE/src/app/(auth-route)/finance/waiver-management/**`, `../Shikshamitra-FE/src/constants/finance-menu.ts`

## Verification

Backend tests must prove no policy/charge waiver configuration, both eligible source types, server-derived full amount, reason validation, permission/scope enforcement, idempotency, concurrency, immutable original amounts, ledger/report separation, and reversal integrity.

Frontend tests must prove no Waiver route, correct row visibility, toggle behavior, reason-only input, no client amount, refreshed totals, server error display, and keyboard-accessible narrow layouts.

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
