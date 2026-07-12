---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Student activation, academic placement, and access

TL;DR: Convert an approved applicant into one enrolled Student with governed identifiers, valid academic placement, reusable family access, and recoverable activation steps.

## Relationship to the enrollment suite

The offer and seat PRD owns the admission decision and seat commitment.
The household PRD owns person, guardian, identity, and account-resolution readiness.
This PRD owns final conversion, numbering, academic placement, access provisioning, repair, and first-day readiness.

## Goals

- Create exactly one Student for one approved applicant.
- Preserve complete lineage from inquiry, registration, documents, decisions, and household evidence.
- Allocate valid permanent identifiers according to school policy.
- Confirm a capacity-aware academic placement before first-day readiness.
- Provision or link required access without silently ignoring failures.
- Make activation retryable without duplicating students, identifiers, seats, or accounts.

## Non-goals

- Do not operate attendance, class teaching, timetable, or promotion.
- Do not fulfil optional transport, hostel, library, health, meal, or uniform services.
- Do not post Finance transactions.
- Do not own later class or division changes for active students.

## Activation readiness

Activation should evaluate admission approval, seat commitment, applicable offer acceptance, payment gate, identity and household resolution, required documents, policy exceptions, academic placement, and numbering readiness.
Each readiness item should show Passed, Pending, Failed, Waived, Not Applicable, or Overridden.
A blocking failure should prevent normal activation.
An authorized override should require reason, approver, expiry when temporary, and explicit downstream consequence.
The final activation preview should show the Student values, guardians, identifiers, placement, access actions, unresolved undertakings, and service-interest handoffs that will result.

## Applicant-to-Student conversion

Activation should create or link one durable Student identity without losing the separate applicant history.
The Student should retain references to admission cycle, route, source, campaign, playbook, policy pack, offer, seat, payment condition, decision, and activation event.
Field provenance should identify which values came from the applicant, staff, verified document, household, policy, Finance, or academic setup.
Verified data should take precedence according to an explicit conflict policy and should never be silently replaced.
Repeated activation requests should return the existing result or resume the incomplete activation rather than create a duplicate.

## Permanent identifier configuration

Schools should configure number series for GR number, admission number, student code, and other permanent or temporary enrollment identifiers.
Series may be scoped by school, campus, academic year, board, medium, class, or another approved dimension.
The allocation milestone may be seat acceptance, final approval, activation, or another governed playbook milestone.
Series should define prefix, suffix, sequence, reset policy, padding, allowed characters, effective period, and uniqueness scope.
The product should validate overlapping active series before publication.

## Identifier governance

An identifier may be Reserved, Allocated, Committed, Cancelled, Released, or Void.
Reserved identifiers should expire or commit according to policy.
Committed permanent identifiers should not be silently reused.
Cancellation, release, voiding, manual assignment, or gap acceptance should require permission and reason.
Returning students should resume the original admission identifier when the applicable policy requires it.
The product should report gaps, duplicates, manual overrides, and exhausted series.

## Academic placement

Placement should include academic year, campus, education board, medium, medium stage, standard or class, division, stream, elective group, and roll-number strategy where applicable.
The placement should be validated against active Academic Setup configuration and user scope.
Board, medium, standard, and division relationships should remain consistent.
Class and division capacity should account for activated students and configured reserved capacity.
Schools may place at class level first and leave division pending only when the playbook permits it.
First-day readiness should require every placement field designated as blocking by school policy.

## Division and roll allocation

Division assignment may be manual, balanced by capacity, gender-aware only when lawful and school-approved, language-aware, subject-aware, sibling-aware, or based on another explainable school rule.
Automated assignment should show the rule and capacity factors used.
Roll numbers may be allocated at activation or by a later Student Management process according to school configuration.
Roll allocation should not reorder existing students silently.
Manual placement or roll overrides should preserve prior values and reason.

## Account provisioning

Activation should create or link the Student account and required guardian accounts according to identity readiness.
Existing guardian accounts should be reused after verification.
Each account action should record Created, Linked, Already Linked, Pending Verification, Failed, or Not Required.
Credential delivery should use approved secure communication and should not expose passwords in ordinary audit or timeline views.
Account creation failure should not be swallowed as a warning when access is required for first-day readiness.

## Activation execution and repair

Activation should present one user-visible operation with a stable activation reference.
Committed admission identity, seat, and number allocations should not be duplicated when a downstream step fails.
Failed steps should enter Activation Repair with the exact step, cause category, retry eligibility, owner, and deadline.
Authorized staff should be able to retry safe steps, replace invalid input, or escalate to the owning module.
Repair should be idempotent and should preserve every attempt.
The Student should become First-Day Ready only when all configured blocking steps complete.

## Optional service handoffs

Registration may capture interest in transport, hostel, library, health support, meal, uniform, device, or another school service.
Activation may create a structured request or notification for the owning module.
The handoff should contain only the minimum approved data and consent required by that module.
Optional service fulfillment should not block first-day readiness unless a future cross-module policy explicitly owns that requirement.
Handoff failure should create an exception without pretending the service was provisioned.

## Welcome and activation confirmation

Parents should receive a clear activation confirmation with student name, academic placement, permanent identifier when parent-visible, account next steps, school contacts, and pending undertakings.
The product should distinguish successful admission from optional service status.
Welcome communication should respect language, channel consent, and secure-link policy.
Staff should be able to reissue approved confirmation without repeating activation.

## Controlled activation cancellation

A school may configure a same-cycle cancellation window before ordinary attendance or academic activity begins.
Cancellation should verify seat, Finance, attendance, academic, account, identifier, and downstream membership consequences.
Committed identifiers should be voided or retained according to numbering policy and should not be erased.
When the safe window has closed, the case should transfer to Student Records rather than reverse activation.

## Operations workspace

Queues should include ready to activate, placement missing, capacity conflict, numbering conflict, identity conflict, account conflict, activation approval, activation failed, repair overdue, service-handoff failed, and cancellation review.
Bulk activation should preview each applicant and should isolate failures without duplicating successful applicants.
Users should be able to reconcile activated students against accepted seats and committed identifiers.

## Permissions and audit

Permissions should distinguish activation preview, activation approval, activation execution, numbering configuration, manual number assignment, placement override, account repair, service handoff, and cancellation.
Every readiness evaluation, conversion, identifier, placement, account action, repair attempt, handoff, and cancellation should be auditable.

## Analytics

Analytics should include time from admission ready to activation, first-pass activation success, repair rate, repair aging, numbering gaps, placement conflicts, account-link success, division capacity, cancellation, and time to first-day readiness.
Reports should reconcile applicants, approved admissions, seats, Students, identifiers, placements, and accounts.

## Acceptance scenarios

- An approved applicant with satisfied gates becomes one Student with complete lineage.
- A repeated activation request does not create another Student, number, seat, or account.
- A school can allocate GR number at final approval while another allocates it at activation.
- A returning student resumes the original number when the applicable policy requires it.
- An invalid board, medium, class, or division combination blocks activation with a specific resolution.
- An account-provisioning failure enters Activation Repair and prevents first-day readiness when access is required.
- A successful retry completes only the failed step and preserves prior attempts.
- Optional transport interest produces a handoff but does not claim that transport is assigned.
- Users without permission cannot activate, alter numbering, override placement, repair protected identity, or cancel activation.

## Public concepts

Define Activation Readiness, Activation Preview, Student Conversion, Number Series, Identifier Reservation, Permanent Identifier, Academic Placement, Division Assignment Rule, Roll Strategy, Account Provisioning, Activation Reference, Activation Repair, Service Handoff, First-Day Ready, and Activation Cancellation.

## Delivery phases

Phase one should include readiness, idempotent Student conversion, number series, academic placement, account linking, activation repair, confirmation, and reconciliation analytics.
Phase two should add advanced placement rules, richer service handoffs, parent onboarding, government readiness, and bulk activation assistance.
