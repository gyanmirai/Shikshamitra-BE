---
module: finance
domain: concession-management
content_type: Reference
status: active
updated_on: 2026-09-17
owner: product-and-engineering
audience: humans-and-agents
---

# Concession flow end-to-end review and scenario catalogue

TL;DR: **the concession feature is not ready for an end-to-end production sign-off**. Policy CRUD and the request/approval domain are substantially implemented, but the normal setup path is broken: the fee-structure UI intentionally hides concession attachment, and the student-assignment command writes an empty concession-policy snapshot. A newly assigned student therefore reaches `LegacyRepairRequired` when staff opens a concession request. Fix the two P0 integration gaps and prove the release gates in this document before rollout.

## Review scope and method

This is a source-backed review of the current backend and frontend as of 2026-09-17. It covers:

- concession-policy creation, activation, versioning, simulation, defaulting, attachment, archive and restore;
- fee-structure validation and student-fee assignment snapshotting;
- request context, draft, submit, documents, preview and replacement requests;
- approval, rejection, cancellation, reversal, live balance mutation and concurrency;
- permissions, tenant/scope boundaries, audit evidence, collections/reporting integration and tests;
- negative, boundary, concurrency, recovery, usability and migration scenarios.

The conclusions come from static code tracing and the existing automated-test inventory. No production data was accessed. Focused backend tests could not start in the restricted workspace because the .NET CLI attempted to write its first-use sentinel under the user profile. The focused frontend run was stopped after it stalled and reported `distributeConcession.test.ts` with zero discovered tests. These are recorded as verification limitations, not product failures.

## Verdict

| Area | Current state | Verdict |
|---|---|---|
| Policy backend | CRUD, immutable versions, default replacement, archive/restore, impact and simulation exist | Partial pass |
| Policy frontend | CRUD UI exists, but its simulator is client-authored and diverges from server rules | Partial pass |
| Fee-structure attachment | Backend accepts and validates a concession policy/version and eligible heads | Backend only |
| Fee-structure UI | Concession state is retained for legacy records, but no selector/scope controls are rendered | **P0 fail** |
| Student assignment | Domain can store a snapshot, but the active commit path passes `null` policy/version and empty scope | **P0 fail** |
| Request/draft/submit | Strong server-side preview, frozen evidence, document rules and duplicate-pending protection | Blocked through normal assignment flow |
| Approval/rejection/cancel | Live recalculation, maker-checker, idempotency and optimistic concurrency exist | Partial pass; negative coverage is thin |
| Reversal | Append-only partial reversal exists on backend | Partial; UI exposes full remaining amount only |
| Downstream balances | `DueFee` keeps concession separate and collection views read approved-minus-reversed allocations | Partial pass |
| Events/integrations | Approval event is published; the only in-repo handler logs it | Gap if notifications/accounting/outbox are required |
| Automated coverage | Core calculations and happy-path workflow have tests | Insufficient for release sign-off |

**Release recommendation: NO-GO** until P0-01 and P0-02 are fixed and an actual UI-to-database happy path passes.

## Intended flow versus current flow

```text
Create Active policy
  -> attach policy + immutable version + eligible heads to fee structure
  -> assign fee structure to student and copy that exact snapshot
  -> open request context from the assignment snapshot
  -> save draft or submit with documents and preview fingerprint
  -> different authorized user previews and approves/rejects
  -> approval atomically updates DueFee + allocations + audit
  -> collection views show net concession
  -> optional append-only reversal restores outstanding

Current breaks:
  fee-structure UI --X--> attach concession policy
  assignment commit  --X--> copy concession policy snapshot
  request context     ---> LegacyRepairRequired for a normal new assignment
```

## Critical findings

### P0-01: new assignments discard the concession configuration

`StudentFeeStructureAppService` creates `StudentFeeStructure` with `null, null, EntireFeeStructure, []` for concession policy ID, version ID, scope and heads, while copying collection/refund/proration policy fields. Preparation and its fingerprint also omit concession policy resolution. `ConcessionRequestManager.ResolveAssignmentAsync` requires both concession IDs and returns `LegacyRepairRequired` when either is empty. No active assignment path calls `RefreshConcessionPolicySnapshot`.

Impact: even if an API client attaches a valid concession policy to a fee structure, a normal new student assignment cannot enter the request flow.

Required correction:

1. Resolve the attached Active, same-year concession policy/version during assignment preview.
2. Include policy ID, version ID, fee-head scope and eligible head IDs in preview evidence and the command fingerprint.
3. Copy the exact resolved values into `StudentFeeStructure` during commit.
4. Add a regression test proving a newly committed assignment can immediately load concession request context.
5. Define an explicit repair command/report for existing empty snapshots; never silently replace a non-empty historical snapshot.

### P0-02: staff cannot attach a concession policy in the fee-structure UI

`FeePoliciesTab.POLICY_CONFIG` renders payment, collection, refund and proration only. Its state model still contains concession fields, and a component test explicitly asserts that concession controls are absent. The backend attachment contract therefore has no normal staff-facing entry point.

Required correction: add an Active-policy selector, show version, choose entire-structure versus selected-head scope, list only `IsConcessionApplied` heads, preserve existing attachments, enforce attachment permission, and show validation errors before publish.

### P1-01: policy simulation shown to staff is not server authoritative

The frontend policy form computes simulation in `useMemo` instead of using the existing `/simulate` endpoint. It always uses `Math.round`, ignores the selected rounding rule, and calculates full waiver as pending plus already paid even though concessions are limited to eligible unpaid amounts. Late fee is displayed but not part of the calculation.

Risk: staff can approve a policy based on a preview that the server will not reproduce.

### P1-02: reversal capability and UI do not match

The backend accepts partial reversal amounts per allocation and journals each reversal. The frontend always sends each selected line's full `reversibleConcessionAmount`, so staff cannot perform a partial correction through the UI.

### P1-03: reversal-after-payment behavior needs a signed business rule

The reversal command checks allocation balance and concurrency, then increases outstanding. It does not block or specially handle a later collection/refund/adjustment on the same due row. Confirm whether reversal should reopen a receivable, require settlement checks, or create a separate recovery workflow.

### P1-04: automated coverage does not prove the real integration

Existing tests cover calculators, allocation, basic lifecycle, basic documents, happy-path API approval/reject/cancel and stale approval stamps. They do not prove policy attachment -> assignment snapshot -> request context -> approve -> collection/report -> reversal. There is also no concession Playwright suite in the inspected paths.

### P2 observations

- The policy `Add` permission also controls edit, clone, restore and fee-structure attachment; `Delete` controls archive. Consider separate permissions for risk-sensitive actions.
- Policy archive/restore does not take a concurrency stamp, while policy update does.
- Policy create/clone does not expose a command idempotency key.
- `availableActions` is state/ownership metadata and does not include effective permissions. The current frontend intersects it with permissions, but other clients must not treat it as authorization.
- Approval context requires View rather than Approve/Reverse. Confirm that the financial detail exposed is appropriate for every View holder.
- Approval publishes `ConcessionApprovedEto`, but the in-repo handler only logs. Confirm transactional outbox/delivery and required notification/accounting consumers.
- Money is transported and calculated as JavaScript `number` on the frontend. Stress decimal/minor-unit boundaries even though the server uses `decimal`.
- Current source supports one Active default policy per tenant/year; older proposed documentation that says default should be removed is stale.
- Existing end-to-end documentation says every assignment requires and snapshots a policy, but current publication makes concession optional and current assignment deliberately stores no snapshot. Treat source as authoritative until the documentation is reconciled.

## Shipped server invariants worth preserving

- A request uses the immutable concession-policy version captured by its student assignment, not the current master policy.
- A concession type supplies the business reason and document requirements; the policy supplies calculation and timing rules.
- Drafts may be incomplete. Submission freezes selected dues, due concurrency stamps, preview fingerprint, request values and document-requirement evidence.
- Only one Pending request is allowed per student, type and academic year; rejected/cancelled requests can be replaced under constrained rules.
- Only unpaid eligible dues participate. `AllOutstanding`, `CurrentInstallment` and `FutureInstallments` filter canonical installment cohorts.
- Percentage is calculated per fee head. Fixed totals are allocated proportionally with deterministic minor-unit handling.
- Approval can reduce but not increase the submitted amount and requires a reason for reductions.
- The creator cannot approve or reject the same request.
- Approval re-reads live dues and validates request/due concurrency before atomically updating request, allocations, due balances, audit and command receipt.
- Reversals are append-only and may be partial on the backend; total reversal cannot exceed the original allocation.
- `DueFee` stores concession and adjustment credit separately while deriving net outstanding from both.

## Complete scenario and edge-case catalogue

Legend: **Covered** means directly evidenced by implementation plus a focused automated test; **Implemented** means code exists but test evidence is missing or incomplete; **Gap** means the product path is absent/divergent; **Blocked** means a preceding P0 prevents normal execution; **Decision** means product/accounting confirmation is required.

### A. Policy lifecycle and calculation

| ID | Scenario and expected result | Status |
|---|---|---|
| CP-01 | Create valid Draft policy; code is normalized/generated, version 1 is immutable | Implemented |
| CP-02 | Create valid Active policy; it can be selected for same-year attachment | Implemented |
| CP-03 | Duplicate normalized name or code in tenant/year is rejected, including concurrent insert race | Implemented; race test missing |
| CP-04 | Same name/code in another academic year or tenant is isolated as designed | Implemented; tenant test missing |
| CP-05 | Percentage boundaries: `0.01`, `100`, zero, negative and greater than 100 | Partial test coverage |
| CP-06 | Fixed amount boundaries: smallest currency unit, zero, negative, and greater than eligible base | Partial test coverage |
| CP-07 | Full waiver is policy-fixed and equals exact eligible unpaid base | Covered |
| CP-08 | Request-entered basis must match method; required maximum is enforced | Covered |
| CP-09 | Rounding None/nearest/up/down at `.004`, `.005`, `.006`, whole rupee and cap boundary | Partial; nearest/up covered |
| CP-10 | Default may be Active only; selecting a new default demotes the old default in the same year | Covered at application level |
| CP-11 | Simultaneous default changes preserve a single default through transaction + DB constraint | Implemented; concurrency test missing |
| CP-12 | Editing Draft creates next immutable version; academic year cannot change | Implemented |
| CP-13 | Editing Active requires meaningful reason and creates next version | Implemented; UI validation tested |
| CP-14 | Stale or missing update concurrency stamp is rejected | Implemented; API test missing |
| CP-15 | Active-to-Draft transition while attached is rejected | Implemented |
| CP-16 | Archive unattached policy; attached policy archive is rejected | Implemented; test missing |
| CP-17 | Repeated archive is safe and archive/update race has deterministic result | Partial; concurrency contract missing |
| CP-18 | Unarchive returns to Draft and cannot silently become default | Implemented |
| CP-19 | Clone same-year/cross-year creates Draft with unique name/code and version 1 | Implemented; collision tests missing |
| CP-20 | Search, status, year, paging and sorting reject invalid/unbounded inputs | Partial |
| CP-21 | Version/impact endpoints return only tenant-visible references and accurate attachment count | Implemented; scope tests missing |
| CP-22 | Server simulation handles all methods, limits and rounding using decimal arithmetic | Domain covered; endpoint matrix missing |
| CP-23 | Frontend simulation exactly matches server for the same inputs | **Gap (P1-01)** |
| CP-24 | Corrupt historical snapshot produces controlled error, not generic 500 | Partial |

### B. Fee-structure attachment and publication

| ID | Scenario and expected result | Status |
|---|---|---|
| FS-01 | Attach Active policy and its current version from the same academic year | Backend implemented; UI gap |
| FS-02 | Reject Draft, Archived, other-year, other-policy or missing version | Backend implemented |
| FS-03 | Reject stale non-current version for a newly changed attachment; preserve unchanged historical attachment | Backend implemented |
| FS-04 | Reject policy ID without version, version without policy ID, and mismatched pair | Implemented |
| FS-05 | Entire-structure scope resolves all concession-enabled heads in that structure | Implemented |
| FS-06 | Selected-head scope requires at least one unique, in-structure, concession-enabled head | Implemented; duplicate-specific test missing |
| FS-07 | Structure with policy but zero concession-enabled heads cannot publish | Implemented |
| FS-08 | No concession policy is permitted by current publication rules; request must clearly show unavailable | Implemented; product decision conflicts with old doc |
| FS-09 | Detach/change policy after assignments exist does not mutate historical assignment snapshots | Intended; integration test missing |
| FS-10 | Locked/published revision follows normal revision rules and cannot be silently mutated | Implemented through fee-structure workflow; concession-specific test missing |
| FS-11 | User without attachment permission cannot add/change/remove concession configuration | Backend implemented; UI test missing |
| FS-12 | Staff can perform every attachment action in the normal fee-structure UI | **Gap (P0-02)** |

### C. Student assignment and legacy repair

| ID | Scenario and expected result | Status |
|---|---|---|
| AS-01 | Preview resolves exact policy/version/scope/heads and includes them in fingerprint | **Gap (P0-01)** |
| AS-02 | Commit copies previewed concession snapshot to `StudentFeeStructure` | **Gap (P0-01)** |
| AS-03 | Newly assigned student can immediately load request context | **Blocked** |
| AS-04 | Policy edited between preview and commit makes preview stale | Gap for concession fields |
| AS-05 | Same command retry is idempotent; changed payload with reused key is rejected | Existing assignment mechanism; concession not included |
| AS-06 | Optional/excluded fee heads are represented correctly in assignment snapshot and dues | Gap/integration test missing |
| AS-07 | Multiple valid assignments require explicit selection and cannot mix due rows | Implemented in request context; end-to-end test missing |
| AS-08 | Assignment without policy returns a clear PolicyNotAttached/unavailable state | Current manager reports legacy repair for empty current assignment; UX gap |
| AS-09 | Academic-year-empty legacy assignment is diagnosed without silent mutation | Implemented |
| AS-10 | Explicit authorized repair hydrates only empty fields from a reviewed source | **Gap: no active repair workflow found** |
| AS-11 | Existing non-empty historical snapshot is never overwritten by repair/update | Domain helper intends this; command test missing |
| AS-12 | Bulk migration reports repaired, unresolved and conflicting records | Gap |

### D. Context, draft, preview and submit

| ID | Scenario and expected result | Status |
|---|---|---|
| CR-01 | Student is missing, inaccessible, unplaced, or in mismatched scope/year | Implemented; API matrix missing |
| CR-02 | No assignment, ambiguous assignments, empty snapshot or corrupt/mismatched version | Implemented errors; normal path blocked |
| CR-03 | Concession type is missing, inactive, other-year or tenant-inaccessible | Implemented |
| CR-04 | No eligible unpaid dues/all dues paid produces a clear non-actionable context | Implemented; UX wording needs verification |
| CR-05 | Draft can be incomplete and multiple drafts can coexist | Implemented |
| CR-06 | Only owner can edit/delete/upload to a Draft; Pending/terminal request is immutable | Implemented; API permission tests missing |
| CR-07 | Submit requires at least one permitted head and eligible due | Implemented |
| CR-08 | Duplicate heads, unknown heads and heads outside snapshotted scope are rejected | Implemented; test missing |
| CR-09 | Policy-fixed percentage/fixed/full waiver ignores client attempts to change the rule value | Implemented; API tamper test missing |
| CR-10 | Request-entered percentage/fixed amount accepts exact max and rejects above max | Calculator covered; HTTP test missing |
| CR-11 | Current/future installment selection handles equal dates, missing sequence and boundary cohort | Current/future cohort covered once; broader fixtures missing |
| CR-12 | Preview fingerprint changes after payment, adjustment, concession, due edit or policy evidence change | Implemented for captured evidence; race matrix missing |
| CR-13 | Stale preview cannot submit and UI refreshes without losing valid user input | Backend implemented; UI recovery incomplete |
| CR-14 | Required document evidence is snapshotted at submission | Implemented |
| CR-15 | One Pending request per student/type/year under simultaneous submit | DB constraint present; race test missing |
| CR-16 | Replacement allowed only from owner's Rejected/Cancelled request with same student/year/type | Implemented; tests missing |
| CR-17 | Same submit idempotency key/payload returns same outcome; changed payload is rejected | Implemented; tests missing |
| CR-18 | Browser double-click, retry after timeout and back-navigation do not duplicate request | Partial; E2E missing |
| CR-19 | Frontend distinguishes PolicyNotAttached, LegacyRepairRequired, ambiguous assignment and no dues | Gap: context error is presented generically |
| CR-20 | Money values preserve two-decimal precision through JSON and UI editing | Server decimal; frontend-number boundary tests missing |

### E. Document security and lifecycle

| ID | Scenario and expected result | Status |
|---|---|---|
| DOC-01 | Zero-byte and over-limit upload are rejected | Implemented; frontend max-size test exists |
| DOC-02 | Unsupported extension, MIME mismatch and invalid file signature are rejected server-side | Implemented; backend tests missing |
| DOC-03 | Filename traversal/control characters are sanitized and cannot escape storage root | Implemented; adversarial test missing |
| DOC-04 | Required versus optional documents are enforced at submit/approval from snapshot | Implemented; workflow test missing |
| DOC-05 | Staged file is bound to uploader, type and requirement; another user cannot read/attach it | Domain partial; API auth test missing |
| DOC-06 | Expired staged file cannot be viewed or attached | Domain covered |
| DOC-07 | Attached or released staged upload cannot be reused | Domain covered |
| DOC-08 | Removed draft upload is released for cleanup | Covered |
| DOC-09 | Requirement changed after draft refreshes draft rules without rewriting submitted evidence | Implemented; test missing |
| DOC-10 | Protected document download requires explicit ViewDocument and tenant/scope access | Implemented; API test missing |
| DOC-11 | Malware scanning, quarantine and retention policy are enforced if required by deployment | Not evidenced; security decision |

### F. Approval, rejection and cancellation

| ID | Scenario and expected result | Status |
|---|---|---|
| AP-01 | Creator cannot approve or reject own request | Implemented; direct test missing |
| AP-02 | User with View but without Approve/Reject cannot execute commands | Backend permission attributes exist; API tests missing |
| AP-03 | Only Pending request can approve/reject; Draft/terminal status is rejected | Implemented; partial workflow coverage |
| AP-04 | Approval preview recomputes live maximum from current unpaid dues | Implemented |
| AP-05 | Client cannot approve more than requested or current live maximum | Implemented; tests missing |
| AP-06 | Reduced approval requires a meaningful reason; unchanged approval does not | Implemented; test missing |
| AP-07 | Selected due belongs to same student, assignment and fee head and appears once only | Implemented; tamper tests missing |
| AP-08 | Missing/stale request or due concurrency stamp rejects the whole command | Covered for stale request/due stamps |
| AP-09 | Client-provided per-due amounts cannot influence authoritative proportional allocation | Implemented by server; contract/UI clarity needed |
| AP-10 | Minor-unit remainder is deterministic, capacity-bounded and exact | Covered at domain level |
| AP-11 | Any failed allocation rolls back request, due balances, audit and idempotency receipt | Transaction exists; rollback integration test missing |
| AP-12 | Same approval idempotency key returns prior result; key reuse with changed body is rejected | Implemented; API tests missing |
| AP-13 | Simultaneous payment and approval yields one winner and recoverable stale preview | Concurrency controls exist; race test missing |
| AP-14 | Simultaneous approvals cannot double-apply | Concurrency/idempotency exist; race test missing |
| AP-15 | Reject requires reason, leaves dues unchanged and remains auditable | Happy path/validation partly covered |
| AP-16 | Owner can cancel Draft/Pending with reason; cannot cancel Approved/Rejected/Reversed | Partly covered |
| AP-17 | Applications list/count/detail use identical tenant and organizational scope | Not fully evidenced; authorization test required |
| AP-18 | UI handles `PreviewChanged` as well as generic concurrency conflict and reloads review | Gap: special handling recognizes only `030003` |

### G. Reversal and correction

| ID | Scenario and expected result | Status |
|---|---|---|
| RV-01 | Full reversal creates journal rows, restores outstanding and marks Reversed | Backend implemented; API test missing |
| RV-02 | Partial reversal creates journal row and marks PartiallyReversed | Backend implemented; UI gap/test missing |
| RV-03 | Multiple partial reversals can reach exact full reversal without exceeding allocation | Implemented; test missing |
| RV-04 | Zero, negative, duplicate allocation, wrong request/head/due and over-reversal are rejected | Implemented; test matrix missing |
| RV-05 | Stale request/due stamp rolls back the whole reversal | Implemented; test missing |
| RV-06 | Reversal retry is idempotent and changed-payload key reuse is rejected | Implemented; test missing |
| RV-07 | UI lets authorized staff enter an amount below remaining reversible amount | **Gap (P1-02)** |
| RV-08 | Creator/approver/reverser separation follows an explicit segregation-of-duties rule | Decision; no maker-checker restriction found for reversal |
| RV-09 | Reversal after later payment, refund, adjustment or reconciliation follows approved accounting behavior | **Decision (P1-03)** |
| RV-10 | Reversal publishes required integration event/notification | Gap/not evidenced |
| RV-11 | Timeline and collection views show approved, reversed and net amounts consistently | Implemented; integration test missing |

### H. Balances, reporting, audit and operations

| ID | Scenario and expected result | Status |
|---|---|---|
| DN-01 | `Outstanding = Original - net paid - ConcessionAmount - AdjustmentCreditAmount` remains non-negative | Domain implemented; cross-command property test needed |
| DN-02 | Concession and adjustment remain separately reportable despite combined net reduction | Implemented in domain; report matrix needed |
| DN-03 | Student fee ledger shows approved less reversed concession per due row | Implemented |
| DN-04 | Collection summary totals update after approval and reversal | Implemented paths found; integration test missing |
| DN-05 | Receipt generation/allocation never treats concession as cash payment | Not fully evidenced; integration test required |
| DN-06 | Refund eligibility uses settled payment, not concession credit | Cross-module verification required |
| DN-07 | Audit captures actor, timestamp, reason, before/after amount and immutable policy/type evidence | Mostly implemented; audit-report test missing |
| DN-08 | Approval event is delivered exactly/reliably enough for downstream consumers | Not evidenced; current handler logs only |
| DN-09 | Exported applications and dashboard totals equal detail/list/ledger totals | Not evidenced |
| DN-10 | Archived policy and inactive type remain readable on historical requests | Implemented through snapshots; test missing |
| DN-11 | Corrupt snapshot/allocation is surfaced in an operations report instead of breaking a whole list | Gap/controlled-recovery test missing |
| DN-12 | Support can trace idempotency key, request ID, assignment, policy version and due allocations | Implemented across entities; operational view not evidenced |

### I. Security, tenancy, accessibility and performance

| ID | Scenario and expected result | Status |
|---|---|---|
| QN-01 | Cross-tenant IDs in every policy/request/document/action endpoint return no data and make no mutation | Framework filters likely; explicit tests missing |
| QN-02 | Organizational row scope is identical for context, lists, detail, documents and actions | Partial/not fully evidenced |
| QN-03 | Permission revocation between page load and submit is enforced server-side | Backend enforcement exists; E2E missing |
| QN-04 | Action buttons and routes remain hidden/disabled without permission, including direct URL entry | Frontend guards exist; permission E2E missing |
| QN-05 | Loading, empty, error, stale-preview and retry states are keyboard/screen-reader usable | Not evidenced |
| QN-06 | Multiple assignments, many fee heads and 100+ due rows remain understandable and performant | Not evidenced |
| QN-07 | Large paged application lists avoid N+1 queries and preserve stable sort | Not evidenced |
| QN-08 | Two tabs editing policy/draft or reviewing approval converge through concurrency errors | Partial; E2E missing |
| QN-09 | API validation messages are mapped to actionable staff language without exposing internals | Partial; generic context error remains |
| QN-10 | Logs contain correlation identifiers but no uploaded-document content or sensitive student data | Not fully evidenced |

## Existing automated evidence

### Backend tests found

- Domain: fixed percentage, full waiver, request caps, basis mismatch, nearest/up rounding, installment cohorts, proportional allocation, lifecycle, staged-document ownership/expiry/release and due-fee concession mutation.
- Application: policy list/default replacement and EF concurrency-token configuration.
- HTTP: create/get/delete/list/context happy paths; missing student/reject message validation; approve/reject/cancel happy paths; stale request and due stamps during approval.

Notably absent from the discovered test names: attachment-to-assignment snapshot, normal request-context integration, maker-checker, duplicate pending race, preview fingerprint race, idempotency conflicts, partial/full reversal, document API security, tenant/row scope, downstream collection totals and event delivery.

### Frontend tests found

- policy required-field and Active-edit-reason validation;
- concession-type delete eligibility;
- document accept/max-size/extension helpers;
- a distribution utility;
- a fee-structure test that explicitly confirms concession controls are not rendered.

No discovered frontend test proves request form submission, multiple-assignment selection, approval, reversal, stale-preview recovery, permissions, accessibility, or a full browser flow.

## Prioritized remediation plan

### Gate 0: repair the executable path

1. Add concession attachment controls to the fee-structure UI.
2. Include concession configuration in assignment preparation, fingerprint and commit.
3. Add an explicit legacy diagnostic/repair workflow.
4. Add one backend integration test and one browser test for policy -> structure -> assignment -> request context.

### Gate 1: align calculation and correction UX

1. Replace the concession policy's local simulator with the server simulation endpoint, or label local output non-authoritative and make it rule-identical.
2. Add partial-reversal amount entry or explicitly remove partial reversal from the public backend contract.
3. Decide reversal-after-settlement behavior and segregation of duties.
4. Handle all stale-preview/concurrency codes consistently in the UI.

### Gate 2: prove financial and security safety

1. Add integration tests for approve/payment and reverse/payment races, rollback and idempotency.
2. Add tenant, organizational-scope and permission matrix tests for every read/write/document route.
3. Reconcile collection, receipt, refund, audit and export totals after approval and reversal.
4. Confirm transactional outbox/event delivery requirements and implement consumers beyond logging where required.

### Gate 3: operational readiness

1. Reconcile the older concession design/end-to-end documents with shipped optional-attachment behavior.
2. Add migration diagnostics for empty/corrupt snapshots and duplicate Pending records.
3. Load-test large due sets and application lists.
4. Run accessibility, double-submit, timeout/retry and multi-tab browser scenarios.

## Minimum production sign-off script

Use two users: requester A and approver B.

1. Create and activate percentage, fixed-amount, request-entered and full-waiver policies for a test academic year.
2. Attach each policy to separate fee structures, covering entire and selected fee-head scopes.
3. Assign structures to students and verify non-empty policy/version/scope/head snapshots in the database/API.
4. Create a Draft, upload required evidence, edit it, submit it and verify frozen evidence.
5. Confirm requester A cannot approve/reject; approver B can preview and reduce with a reason.
6. Change a due between preview and approval and verify safe rejection/reload.
7. Approve and reconcile request totals, allocations, `DueFee`, collection detail/summary, audit and event/outbox.
8. Attempt a simultaneous payment and approval; verify one safe outcome with no double reduction.
9. Partially reverse, then fully reverse; verify outstanding, statuses, timeline and downstream totals.
10. Repeat critical reads/actions with a different tenant, restricted organizational scope and missing permissions.
11. Repeat a timed-out submit/approve/reverse with the same idempotency key, then with a changed payload.
12. Verify archived policies/types remain readable on history and cannot be newly selected.

## Release acceptance gates

- [ ] Fee-structure UI can attach an Active same-year concession policy and choose valid eligible heads.
- [ ] New assignment stores non-empty policy ID/version ID and exact scope/heads from a stale-safe preview.
- [ ] Newly assigned student reaches request context without repair.
- [ ] Legacy empty snapshots have an explicit, audited repair path and non-empty snapshots remain immutable.
- [ ] Frontend and server simulations match for every method, cap and rounding mode.
- [ ] Policy-fixed requests cannot be tampered with; request-entered boundaries are enforced.
- [ ] Documents pass type/signature/size/ownership/expiry/download authorization tests.
- [ ] Maker-checker, permission and tenant/scope matrices pass.
- [ ] Approval/payment and reversal/payment races preserve the balance equation.
- [ ] Idempotent retry and changed-payload conflict tests pass for submit, approve and reverse.
- [ ] Partial reversal is either supported in UI or deliberately removed from the contract.
- [ ] Collection, receipt, refund, audit and export/report totals reconcile after approval and reversal.
- [ ] Approval/reversal event delivery and consumer expectations are documented and tested.
- [ ] Focused backend, frontend and browser suites pass in CI.

## Source map

| Concern | Source |
|---|---|
| Policy aggregate/calculator | `src/ShikshaMitra.Domain/Finance/Policies/ConcessionPolicy.cs`, `ConcessionPolicyCalculationService.cs` |
| Policy app/API | `src/ShikshaMitra.Application/Finance/Policies/ConcessionPolicyAppService.cs`, `src/ShikshaMitra.HttpApi/Controllers/Finance/Policies/ConcessionPoliciesController.cs` |
| Fee-structure validation | `src/ShikshaMitra.Application/Finance/Fees/Settings/FeeStructureAppService.Validation.cs` |
| Assignment break | `src/ShikshaMitra.Application/Finance/Fees/StudentFeeAssignment/StudentFeeStructureAppService.cs`, `.Preparation.cs` |
| Request rules | `src/ShikshaMitra.Domain/Finance/Fees/Concessions/Managers/ConcessionRequestManager.cs` |
| Request commands/queries | `src/ShikshaMitra.Application/Finance/Fees/Concessions/ConcessionRequestAppService.*.cs` |
| Request routes | `src/ShikshaMitra.HttpApi/Controllers/Finance/Fees/Concessions/ConcessionRequestController.cs` |
| Due balance | `src/ShikshaMitra.Domain/Finance/Fees/Collections/Aggregates/DueFee.cs` |
| Collection projection | `src/ShikshaMitra.Application/Finance/Fees/Collections/StudentFeeDetailsQueryService.cs` |
| Approval event | `src/ShikshaMitra.Application/Finance/EventHandlers/FinanceEventHandlers.cs` |
| Missing attachment UI | `../Shikshamitra-FE/src/screens/finance/settings/feeStructure/Components/FeePoliciesTab.tsx` |
| Policy simulator divergence | `../Shikshamitra-FE/src/screens/finance/policies/hooks/useConcessionPolicyForm.ts` |
| Request UI | `../Shikshamitra-FE/src/screens/finance/Concession/ConcessionRequestForm.tsx` |
| Approval/reversal UI | `../Shikshamitra-FE/src/screens/finance/ConcessionAdmin/ConcessionFeeHeadApprovalView.tsx` |
| Backend tests | `test/ShikshaMitra.*.Tests/Finance/**/Concession*Tests.cs` |

