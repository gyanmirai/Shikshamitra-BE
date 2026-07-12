---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Special admission routes

TL;DR: Apply route-specific eligibility, evidence, approvals, seats, fees, and timelines without fragmenting applicants into separate admission systems.

## Relationship to the enrollment suite

The master journey remains common for every applicant.
This PRD defines route-specific variations and delegates inquiry, registration, offers, fees, household resolution, and activation to their canonical PRDs.
Policy packs supply board and jurisdiction defaults used by these routes.

## Goals

- Support major Indian school admission routes within one governed lifecycle.
- Make route-specific eligibility, evidence, priority, fees, seats, and approvals configurable.
- Prevent route labels from automatically granting priority, concession, or legal status.
- Preserve the reason and evidence for route selection and changes.
- Keep route analytics comparable without erasing operational differences.

## Non-goals

- Do not create separate applicant or Student records for each route considered by the school.
- Do not hard-code statutory percentages, annual dates, income thresholds, or document lists.
- Do not let route priority bypass capacity, identity, payment, approval, or audit controls.
- Do not own transfers or placement changes for an already active student.
- Do not determine legal eligibility without the applicable sourced policy and authorized school decision.

## Shared route behavior

Every route should define applicability, entry source, eligibility rules, required evidence, playbook, seat pool, offer behavior, fee policy, approval chain, deadlines, parent wording, and activation conditions.
Every applicant should have one primary route and may have reviewed alternate routes.
Changing route should preview impacts on seat, offer, fee, documents, policy, waitlist, and deadlines.
Route changes should preserve prior evaluations and require a reason after registration submission.
A route should not bypass common security, consent, identity, audit, or tenant controls.

## Fresh admission

Fresh admission should support applicants entering the school for the first time at pre-primary, primary, secondary, or higher-secondary level.
The playbook should determine whether entrance assessment, registration, offer, payment, and document milestones are required.
Age, prior-school, and class eligibility should come from the applicable policy pack and school overrides.

## Sibling-priority admission

Sibling priority should require a verified relationship to an eligible current or former student according to school policy.
The school should configure qualifying sibling status, campus or school applicability, priority window, seat pool, concession interaction, and expiry.
Sibling priority should not guarantee admission unless the policy explicitly defines that outcome.
The product should prevent duplicate priority claims through the same sibling without review.

## Alumni-related admission

Alumni preference should require a verified relationship to a recognized former student or alumnus.
Policy should define eligible relationship, graduating or leaving status, school or campus scope, evidence, priority, and concession.
Unverified historical records should enter a review queue rather than automatically receiving preference.

## Staff-child admission

Staff-child routes should reference an active or otherwise eligible staff relationship from the authoritative HR record when available.
Policy should define eligible employment status, probation or tenure condition, campus scope, seat pool, concession, and effect of employment changes.
Admissions staff should not be able to alter HR status to satisfy eligibility.

## RTE and EWS admission

RTE or EWS should be a dedicated governed route rather than an ordinary applicant label.
The route should record authority, application or allotment number, academic year, round, allotted school or campus, allotted class, parent acceptance deadline, admission deadline, and current authority status.
The product should support import or manual capture of government allotments with duplicate detection and reconciliation.
Eligibility evidence, document requirements, income or category evidence, and age rules should come from the effective sourced policy pack.
RTE seat pools should reconcile authority allotments, accepted admissions, cancellations, vacancies, and reporting status.
Schools should be able to record discrepancy, appeal, district verification, rejected evidence, re-selection, and later-round movement.
The product should not hard-code a seat percentage, income threshold, document list, or annual date without a versioned official source.

## Transfer and lateral admission

Transfer admission should capture previous school, recognition or affiliation, previous board, last class completed, requested class, transfer certificate, marksheet, migration evidence, reason, and effective date.
The route should validate class eligibility and board-specific restrictions through the policy pack.
The product should distinguish document pending, document verified, countersignature pending, equivalence pending, authority approval pending, and eligible.
The school may use a provisional undertaking only when policy permits it and should track its due date and consequence.
Placement into a class higher than supported by verified evidence should require a governed exception or remain blocked.

## Mid-year admission

Mid-year routes should evaluate remaining instructional days, attendance feasibility, board deadlines, curriculum alignment, seat availability, and approval requirements.
The route should capture joining date, reason, prior attendance, curriculum transition, academic support plan, and authority approval when applicable.
Late-admission exceptions should be sourced from the applicable policy pack and should not be inferred from a staff preference.

## Returning-student admission

Returning students should be matched to the former Student, household, guardians, identifiers, and leaving history before new identity creation.
The route should evaluate re-entry eligibility, unresolved dues supplied by Finance, prior disciplinary or safety restrictions under controlled access, transfer documentation, and academic placement.
The original permanent admission number should be resumed when the applicable policy requires it.
Re-entry should create a new enrollment-cycle history without erasing the former period of attendance.

## Interstate admission

Interstate routes should support recognized-board verification, state-specific age differences, transfer-certificate requirements, language or curriculum transition, and migration evidence.
The applicant should be evaluated under both prior-school evidence and the receiving school's applicable state and board policy.
Policy conflicts should create a review item with the controlling source identified.

## Overseas-return admission

Overseas-return routes should capture country, institution, curriculum, completed grade, academic records, translations, equivalence evidence, passport or visa evidence when appropriate, and proposed placement.
Where board eligibility or equivalence approval is required, activation should remain blocked until the required decision is recorded.
The school should be able to record a placement assessment without presenting it as official equivalence.

## Direct and management-approved admission

A direct route may skip selected offer or assessment milestones only when an approved playbook permits it.
School-defined management or discretionary routes should still respect capacity, policy, identity, payment, audit, and maker-checker requirements.
Route authority should not permit hidden seat creation, undocumented fee waivers, or unexplained policy bypasses.

## Route exceptions and appeals

Exceptions should identify the failed rule, requested outcome, evidence, requester, approving authority, effective period, and downstream consequence.
Appeals should preserve the original decision and should not alter waitlist or seat position until the configured review action permits it.
Approved exceptions should remain visible in final activation and audit reports.

## Operations and analytics

Queues should include route eligibility review, sibling or alumni verification, staff status mismatch, government allotment reconciliation, transfer evidence pending, equivalence pending, late-admission approval, re-entry identity match, and route-change conflict.
Analytics should compare demand, eligibility, offer, acceptance, payment, activation, time to decision, exception, and yield by route without exposing protected category or family information.

## Acceptance scenarios

- A verified sibling receives configured priority without bypassing required admission gates.
- A staff-child request remains pending when the authoritative employment condition is not satisfied.
- A Gujarat RTE allotment records its round, authority reference, seat pool, documents, and deadline.
- A route configuration update does not change an applicant already governed by a prior version without controlled reassignment.
- A transfer applicant cannot be placed above the class supported by verified evidence.
- A late CBSE admission creates the required policy exception or remains blocked.
- A returning student is linked to the former identity and resumes the correct permanent number when required.
- An overseas-return applicant remains pending while required equivalence evidence is unresolved.
- Users without permission cannot change routes, approve exceptions, or access protected category evidence.

## Public concepts

Define Admission Route, Route Eligibility, Route Evidence, Route Priority, Route Change, Fresh Admission, Sibling Priority, Alumni Preference, Staff-Child Admission, RTE or EWS Allotment, Transfer Admission, Lateral Admission, Mid-Year Admission, Re-entry, Interstate Admission, Overseas-Return Admission, Equivalence Review, Route Exception, and Appeal.

## Delivery phases

Phase one should include fresh, sibling, staff-child, basic RTE record, transfer, route changes, exceptions, and route analytics.
Phase two should add complete RTE round operations, alumni verification, advanced re-entry, interstate and overseas equivalence, appeals, and government import adapters.
