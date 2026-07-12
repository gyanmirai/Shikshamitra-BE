---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Enrollment fees, concessions, and payment handoff

TL;DR: Let Student Enrollment evaluate configurable financial admission conditions while Finance remains authoritative for charges, receipts, reconciliation, settlement, and refunds.

## Relationship to the enrollment suite

The registration PRD owns registration-form fee configuration and reference capture.
This PRD owns the financial conditions between admission readiness, seat acceptance, and Student activation.
Finance owns accounting truth and returns authoritative payment and refund states to Enrollment.

## Goals

- Give parents a clear admission quotation and required payment milestones.
- Let schools configure whether payment blocks offer acceptance, final approval, or activation.
- Support concessions, scholarships, waivers, sponsorship, and installments without duplicating Finance.
- Reconcile seat consequences with authoritative financial status.
- Make every financial exception and approval auditable.

## Non-goals

- Do not create fee ledgers, accounting entries, receipts, settlements, or bank reconciliation.
- Do not define payment-gateway or banking-provider payloads.
- Do not calculate ongoing tuition outside the admission quotation and Finance handoff.
- Do not promise a refund before Finance confirms the outcome.

## Admission fee policy

Fee policy should be configurable by cycle, campus, board, medium, class, admission route, round, category, and effective period.
Policies may include registration fee, admission fee, refundable or non-refundable deposit, caution money, first installment, one-time charges, and school-defined components.
Every component should identify its owning module, tax or accounting treatment reference when supplied by Finance, due milestone, refundability label, and parent-facing description.
The product should preserve the exact quotation version presented to the parent.
Changes to future policy should not alter issued quotations silently.

## Payment gates

Playbooks should support No Financial Gate, Reference Recorded, Payment Initiated, Partial Amount Confirmed, Required Amount Confirmed, and Finance Cleared gates.
A payment gate should specify the milestone it blocks, applicable components, required amount or percentage, due date, grace period, and failure behavior.
Different routes may use different gates, including zero-fee or authority-funded admission.
An override should require permission, reason, approver, expiry when temporary, and visibility to Finance.
An unresolved payment gate should explain the exact condition without exposing protected financial notes.

## Quotation and parent experience

Parents should see a readable admission quotation with components, concessions, paid amount, outstanding condition, deadlines, refund labels, and Finance contact information.
The quotation should distinguish estimates from authoritative posted charges.
Parents should be able to reach the Finance-owned payment experience through a secure handoff when available.
Enrollment should display authoritative receipt references and payment status supplied by Finance without manufacturing receipt numbers.
Expired or revised quotations should remain available in history and should not be payable through an obsolete link.

## Concessions, scholarships, and waivers

The product should support sibling, staff-child, alumni, merit, need-based, RTE or authority, scholarship, discretionary, and school-defined concession types.
A concession may be automatic from an approved rule or requested by staff or parent.
Requests should capture reason, evidence, requested amount or percentage, applicable components, validity, and decision deadline.
Approval chains may require counselor recommendation, Finance review, principal approval, or committee approval.
The product should prevent a user from approving a request beyond their delegated limit.
Approved concessions should be handed to Finance and should become effective only when Finance acknowledges the resulting quotation or charge state.
Rejected, expired, revoked, and superseded decisions should preserve history.

## Installments and sponsored payment

Admission policies may allow one payment, a deposit followed by balance, or a configured installment schedule.
The activation gate should evaluate only the installment conditions required at that milestone.
Sponsored or authority-funded admissions should record payer type, authorization reference, covered components, validity, and unresolved difference.
Failure of a later installment after activation should move to Finance collections and should not silently reverse Student activation.

## Payment and reconciliation states

Enrollment-facing states should include Not Required, Not Started, Initiated, Pending Confirmation, Partially Confirmed, Confirmed, Failed, Reversed, Disputed, Waived, Sponsored, and Refunded.
Only Finance-confirmed states should satisfy a cleared-payment gate.
Provider success alone should not be treated as reconciliation when Finance has not confirmed it.
Duplicate, delayed, reversed, or mismatched payment events should create a reconciliation exception without consuming another seat.
Every status should show its source and last confirmed time.

## Offer expiry and seat consequences

Playbooks should define whether pending payment extends an offer, holds a seat, creates a grace period, or allows expiry.
Enrollment should not release an accepted seat while an authoritative payment confirmation is within a configured processing window.
When payment fails or remains unresolved after the deadline, the system should create a task before releasing a seat if the playbook requires staff review.
A later payment for an expired offer should create an exception and should not automatically displace another applicant.

## Refund and cancellation handoff

Enrollment should calculate the requested cancellation context and send Finance the applicant, offer, payment, fee-policy, reason, and seat consequence references.
Finance should return Requested, Under Review, Approved, Rejected, Processing, Partially Refunded, Refunded, or Closed status.
Seat release rules should be configurable independently from refund completion when school policy permits.
Parent-facing status should clearly distinguish admission cancellation from refund completion.
Enrollment should never edit or delete an authoritative receipt or refund result.

## Operations and exceptions

Queues should include quotation missing, payment gate pending, payment mismatch, concession review, approval overdue, sponsored evidence pending, offer expiry with payment in flight, refund requested, and Finance handoff failure.
Staff should be able to filter by cycle, campus, class, route, fee rule, gate, concession type, Finance state, owner, and deadline.
Bulk reminders should exclude disputed, withdrawn, cancelled, waived, sponsored, or already satisfied cases.

## Permissions and audit

Permissions should distinguish viewing parent financial status, configuring gates, requesting concessions, approving concessions, recording offline references, overriding gates, and initiating cancellation handoff.
Users should see only the financial detail required for their role.
Every quotation, gate evaluation, concession decision, override, payment-state change, and refund handoff should be auditable.

## Analytics

Analytics should include quotation-to-payment conversion, time to satisfy gate, payment failure, reconciliation aging, concession requests and approvals, concession value, scholarship utilization, payment-related offer expiry, refund aging, and admission yield by financial route.
Enrollment and Finance totals should be reconcilable through shared references and identified discrepancies.

## Acceptance scenarios

- One playbook activates without payment while another requires a Finance-cleared deposit.
- A parent sees the exact quotation, concession, paid amount, and activation condition applied to the applicant.
- A counselor can request but cannot self-approve a concession above their authority.
- A confirmed scholarship produces a revised Finance quotation before satisfying the gate.
- A delayed provider event does not release a seat during the configured reconciliation window.
- A payment received after offer expiry enters an exception and does not automatically consume a seat.
- Admission cancellation and refund processing remain separately visible.
- Users without permission cannot change fee policy, approve concessions, override gates, or view restricted financial details.

## Public concepts

Define Admission Fee Policy, Admission Quotation, Fee Component, Payment Gate, Payment Condition, Concession, Scholarship, Waiver, Sponsored Admission, Installment Condition, Reconciliation Exception, Finance Handoff, and Refund Status.

## Delivery phases

Phase one should include quotations, configurable payment gates, Finance status handoff, basic concessions, overrides, cancellation handoff, queues, and analytics.
Phase two should add advanced approval limits, sponsorship, installment orchestration, richer reconciliation, applicant self-service, and concession forecasting.
