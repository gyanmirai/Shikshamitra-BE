---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Final admission, offers, and seat allocation

TL;DR: Convert an admission-ready applicant into a governed seat commitment through configurable offers, capacity controls, waitlists, approvals, and cancellation rules.

## Relationship to the enrollment suite

The [admissions playbook PRD](./prd-admissions-playbook-registration-documents-exam-configuration.md) owns readiness and the handoff package.
This PRD begins when an applicant is ready for seat review and ends when the admission decision is ready for Student activation or reaches a terminal outcome.
The [activation PRD](./prd-student-activation-academic-placement-access.md) owns final Student creation and academic placement.

## Goals

- Give schools an accurate real-time view of capacity and committed seats.
- Support both direct admission and formal offer-based admission by playbook.
- Prevent double allocation and unexplained overbooking.
- Make waitlist progression timely, fair according to configured rules, and auditable.
- Preserve every offer, decision, expiry, withdrawal, release, and override.

## Non-goals

- Do not create fee-ledger entries or settle payments.
- Do not create or activate the final Student record.
- Do not prescribe statutory seat percentages or category priorities.
- Do not allocate active students after enrollment.

## Seat-plan configuration

A seat plan should be scoped by admission cycle, campus, board, medium, standard, stream when applicable, admission round, and configurable category.
A seat plan should distinguish approved capacity, operational target, reserved seats, offered seats, accepted seats, payment-pending seats, activated seats, waitlisted applicants, released seats, cancelled seats, and remaining seats.
Seat pools may represent ordinary intake, RTE or EWS, sibling priority, staff-child, transfer, scholarship, or another school-defined segment.
Movement between pools should require an effective date, authority, reason, and impact preview.
The product should warn when total pools exceed approved capacity or when a pool has no valid admission route.
Capacity changes should preserve prior totals and identify offers or activations affected by the change.

## Seat availability and reservation

A seat should be reserved only through a controlled action tied to an applicant and seat pool.
Reservations should have a status, owner, creation reason, expiry, and release behavior.
Temporary reservations may be used during committee review, offer preparation, or payment confirmation.
The product should prevent two concurrent actions from committing the same last available seat.
An authorized capacity override should require permission, reason, approving authority, and an explicit resulting over-capacity count.
An override should never silently increase the approved seat plan.

## Offer configuration

Playbooks should configure whether offers are skipped, provisional, final, or provisional followed by final.
Offer configuration should define validity window, acceptance method, required declarations, deposit or payment condition, document condition, withdrawal consequence, and seat-release behavior.
An offer should state campus, board, medium, class, academic cycle, admission route, applicable conditions, response deadline, and approved parent-facing terms.
An offer should preserve the exact template, language, conditions, fee quotation reference, and policy versions used when issued.
Staff should be able to preview the offer as the parent will receive it.

## Offer lifecycle

Offer states should include Draft, Approval Pending, Approved, Issued, Opened, Accepted, Declined, Expired, Revoked, Superseded, and Cancelled.
Only approved offers should be issuable when maker-checker is enabled.
An accepted offer should reserve the applicable seat but should not bypass payment, final approval, identity, or activation gates.
A declined, expired, revoked, or cancelled offer should release the seat according to the configured timing rule.
Extending, revoking, correcting, or reissuing an offer should preserve the prior version and require a reason.
An offer should not be editable after issue and should instead be superseded by a new version.

## Parent response

Parents should accept or decline through the existing OTP-backed applicant surface.
Acceptance should capture actor, timestamp, language, declarations, terms version, and required evidence.
The surface should show the response deadline, remaining conditions, support contact, and consequence of no response.
Authorized staff may record an offline response only with source, staff identity, evidence note, and reason.
An acceptance conflict or expired offer should produce a clear recovery path without consuming another seat.

## Waitlist management

Waitlists should be scoped to the same dimensions as seat pools.
Waitlist ordering may use merit, category, application time, sibling priority, lottery or authority sequence, committee decision, or a configured composite rule.
The product should show the factors that produced an applicant's current position without exposing protected information to other applicants.
Promotion may be manual, scheduled, or automated according to playbook and policy.
Each promotion should create a time-bound offer or task and should release the opportunity when the response window closes.
Schools should be able to pause promotion, protect selected positions during an appeal, and recalculate after a governed rule change.
Historical waitlist position and promotion decisions should remain reproducible.

## Final admission decision

Decision states should include Approval Pending, Approved, Rejected, Waitlisted, Withdrawn, Expired, and Cancelled.
Final approval should evaluate readiness, seat availability, offer response when required, payment gate, policy exceptions, household readiness, and unresolved undertakings.
Playbooks may require admission committee, principal, Finance, or second-approver sign-off.
Rejection, withdrawal, and cancellation should use configured reason codes with optional protected internal notes.
The parent-facing reason should be separately controlled from internal decision notes.
Changing a published decision should require permission, reason, and preserved prior state.

## Withdrawal and cancellation

Applicants may withdraw before activation through parent self-service or an authorized staff action.
Withdrawal should stop reminders, release the seat, close open tasks, and request any Finance follow-up.
The product should support a controlled same-cycle activation cancellation window before ordinary attendance begins.
Activation cancellation should require checks for attendance, fee activity, academic activity, issued credentials, and downstream memberships.
When cancellation is no longer safe, the case should transfer to the Student Records leaving workflow.
Cancellation should not erase the Student, applicant, household, offer, payment, or audit history.

## Operations workspace

Queues should include seat-plan conflicts, unreviewed ready applicants, reservation expiry, offer approval, offers not opened, offers expiring, accepted offers with unmet conditions, declined offers, waitlist promotion, over-capacity cases, and cancellation review.
Users should be able to filter by cycle, campus, class, board, medium, round, route, category, seat pool, owner, deadline, and status.
Bulk offer issue or waitlist promotion should preview capacity impact and exclude conflicted applicants.

## Permissions and audit

Permissions should distinguish seat-plan configuration, reservation, offer preparation, offer approval, offer issue, waitlist management, final decision, capacity override, and cancellation.
Every capacity, reservation, offer, waitlist, decision, and release event should record actor, time, reason, prior state, resulting state, and policy context.
Tenant and academic-scope restrictions should apply to all actions and analytics.

## Analytics

Analytics should include planned capacity, offers per seat, offer acceptance, response time, offer expiry, decline reasons, seat conversion, waitlist movement, over-capacity, withdrawal, cancellation, and final yield.
Reports should reconcile seat-plan totals to current applicant states and identify unexplained differences.
Historical reports should use the seat plan and rule versions effective at the reported time.

## Acceptance scenarios

- A playbook can use direct admission without creating a formal offer.
- Another playbook can require a provisional offer, payment condition, and final offer.
- Two users attempting to allocate the final seat produce one commitment and one actionable conflict.
- An expired offer releases its reservation and promotes the next eligible waitlisted applicant according to policy.
- An authorized over-capacity decision records the reason and does not rewrite approved capacity.
- A parent can accept an offer in the approved language and the accepted terms remain reproducible.
- A withdrawn applicant releases the seat and retains complete history.
- An activated student can be cancelled only inside the configured safe window and required approvals.
- Users without permission cannot alter seat plans, issue offers, reorder waitlists, override capacity, or cancel admission.

## Public concepts

Define Seat Plan, Seat Pool, Seat Reservation, Provisional Offer, Final Offer, Offer Condition, Offer Acceptance, Offer Expiry, Waitlist Position, Waitlist Promotion, Capacity Override, Admission Decision, Withdrawal, and Activation Cancellation.

## Delivery phases

Phase one should include seat plans, reservations, configurable offers, acceptance, waitlists, final decisions, capacity overrides, withdrawal, core queues, and analytics.
Phase two should add richer applicant self-service, advanced waitlist automation, appeals, bulk decision operations, and predictive yield assistance.
