---
module: finance
domain: admin-request-auto-approval
content_type: Reference
status: active
updated_on: 2026-10-07
owner: finance-product-and-engineering
audience: humans-and-agents
---

# Admin-created finance requests: immediate approval contract

## Decision and scope

When a tenant administrator creates and submits a **new** refund, charge, or concession request, the successful response must already report `Approved`. The record must not be committed in `Pending`/`Submitted` and then require a second browser action. Non-admin callers keep the existing maker/checker workflow. Draft saves, edits, replacements, cancellation, reversal, rejection, payout, and requests submitted by someone other than their creator retain their existing rules unless explicitly noted below.

This is an intentional exception to the [Finance Engineering Constitution](./constitution.md) maker/checker rule. Product reason: an administrator performing the creation is the final decision-maker for these three request types. Risk: a single actor can create an incorrect financial obligation, concession, or refund reservation. Compensating controls: require the existing create/submit **and approve** action permissions, verify administrator membership from the server's configured role store, retain all scope/policy/amount checks, write distinct creation and automatic-approval audit evidence, use one transactional command, and retain normal correction flows. Owner: Finance product and engineering. Review by **2027-01-07**; remove or renew the exception based on audit findings.

`admin` and `super-admin` are the currently configured administrator roles (`UserScopeOptions.AdminRoles`). Use `IUserScopeService.IsAdminUserAsync(currentUser.GetId())` or the equivalent authoritative server role lookup. A client role claim, an `isAdmin` request field, or possession of the `Approve` permission alone does not qualify. A server-side role check supplements, rather than replaces, exact permission checks. A tenant-specific configured administrator role also qualifies through that service. The current frontend confirmation recognizes `admin` and `super-admin` session roles; deployments that configure additional admin role names must expose matching frontend eligibility before assigning those roles. No role or permission seeder change is implied.

## State and downstream effects

| Command | Admin outcome on success | Non-admin outcome | Existing downstream behavior to preserve |
| --- | --- | --- | --- |
| New charge `CreateAsync` | Every resolved student line and batch is approved in the creation transaction | Pending student lines | Approved charge event, collection availability, account routing, audit and batch counts |
| New concession `CreateAsync` and submission of the creator's draft `SubmitAsync` | Request approved, allocations applied to `DueFee` in the same transaction | Submitted for checker | Pinned policy/version, preview fingerprint, document checks, due-fee allocation, paid-concession evidence, optional linked refund creation, approval event |
| New refund `CreateAsync` and submission of the creator's draft `SubmitAsync` | Request approved and source payment reservations made in the same serializable transaction | Submitted for review | Receipt/assignment/policy validation, allowed payout method, expense category and bank routing, reservation uniqueness and audit |

Approval means authorization of a refund request, **not cash disbursement**. Recording and confirming the payout remains a separate permissioned workflow. Refunds automatically generated from concession approval are system-created linked requests and keep their current submitted/checker behavior. A replacement refund follows the new-refund rule only when an admin explicitly creates it and all replacement validations pass.

## Backend implementation contract

1. Keep existing endpoints and response types. The returned status and available actions reflect the final committed state. Avoid a `create` HTTP call followed by a client-side `approve` call: a failed second call would leave a pending request despite the confirmed outcome.
2. Resolve current administrator membership and check the corresponding `Approve` permission **before** making an admin-path monetary change. Existing `Add` and `Submit` checks still apply where the command currently requires them. If admin lacks `Approve`, reject the command with an authorization error; do not silently create pending after the user confirmed automatic approval.
3. Factor the existing approval work into private transaction-safe routines shared by explicit checker approval and the admin creation path. Do not invoke public app-service methods that start their own unit of work or bypass their `[Authorize]` interceptors internally. Preserve the explicit approval route's independent-approver prohibition for non-admins. For concessions, a configured administrator may also explicitly approve or reject their own existing Draft/Pending request with the corresponding Approve/Reject permission. Check current database administrator membership on every command, including idempotent replay; role claims alone cannot enable this exception. Keep the original creation-only exception for charges and refunds.
4. Charges: after the request and lines are created, run the manager's approve-lines transition for **all** resolved lines, persist the batch, write `AddCharge` and `Approve` audit entries with an `automaticApproval` marker, and publish approved events after successful commit. Approval failures roll back creation and lines.
5. Concessions: reuse the server preview fingerprint and submitted evaluation, calculate the same allocation set used by explicit approval, apply it to dues, record the paid amount, create policy-directed linked refund requests, persist command receipt and both audit actions atomically. Publish `ConcessionApprovedEto` only after commit. A stale preview or changed due balance fails the whole command, returning the established conflict error.
6. Refunds: reuse the existing `FeeRefundManager.ApproveAsync` and `StageNewReservationsAsync` path inside the serializable creation/submit transaction. Resolve policy routing and payout method before approval. If required routing is unavailable, reject the command with a clear validation error and commit nothing. Do not mark paid or create a payout settlement.
7. Keep idempotency keys and replay receipts bound to the final approved result. Retries return the same request without repeating allocations, reservations, events, or audit entries. Concurrency and unique constraints still protect duplicate student/type or refundable source use.
8. Scope checks, policy versions, academic year, student identity, amounts, documents, and existing limits are unchanged. Approval audit records must show the same actor as creator, the automatic-admin reason, request ID, status transition, and relevant amount/count. Do not put payment details or full bank numbers in logs.

## Permission and failure matrix

| Caller | Create/submit grant | Approve grant | Result |
| --- | --- | --- | --- |
| Configured admin | Yes | Yes | Final Approved response after confirmation |
| Configured admin | Yes | No | Denied before commit |
| Other role | Yes | Either | Existing Pending/Submitted workflow; creator cannot self-approve through explicit approval endpoint |
| Any caller | No | Any | Denied by existing command permission |

For a failed validation, permission, preview, transaction, or reservation check, no partial request or monetary effect may remain. The UI must present the returned error and preserve form values for correction. Approved requests use the current cancel/reverse/void or payout-correction path; ordinary edit of approved money remains disallowed.

## Acceptance evidence

- HTTP/application tests for all three admin create paths and the draft-submit paths: final status, actor, audit, allocations/line counts/reservations, and normal downstream events.
- Equivalent non-admin paths remain pending/submitted, including a non-admin granted `Approve` and a same-maker explicit approval denial.
- Configured admin without `Approve`, cross-tenant or out-of-scope student, stale concession fingerprint, invalid refund routing, duplicate command/retry, and concurrent refund/charge requests fail or replay without partial effects.
- Reports, list counts, and collection views use the committed approved state. Payout is still pending after an approved refund request.

See the [frontend confirmation design](../../../../../Shikshamitra-FE/docs/finance/references/admin-request-auto-approval-ui.md) for the screen behavior.
