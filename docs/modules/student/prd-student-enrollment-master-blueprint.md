---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Student Enrollment master blueprint

TL;DR: Define one configurable, auditable Student Enrollment journey for Indian schools from first inquiry through an academically placed and access-provisioned student.

## Product intent

Student Enrollment should operate as one admissions system rather than a set of disconnected inquiry, registration, exam, approval, and student-creation screens.
The product should support the operating depth required by CBSE and Gujarat Board schools while remaining configurable for other Indian state boards.
The primary launch audience is private unaided, aided, and trust-managed schools.
The product should guide simple schools with sensible defaults and support complex multi-campus schools through versioned policy, playbook, approval, and seat rules.

## Current development baseline

The current Student module contains inquiry, registration-form issue, registration, entrance-exam, admission, reports, and enrollment settings surfaces.
The admissions CRM and companion admissions playbook capabilities provide inquiry management, configurable registration, document verification, exam operations, readiness, and a downstream handoff package.
The current Admission workflow can approve or reject students, assign divisions and fee structures, and create student and parent access.
The future product should preserve compatible behavior while replacing disconnected transitions with a governed end-to-end workflow.

## Scope

This blueprint covers inquiry capture, applicant qualification, entrance assessment, registration, document verification, admission readiness, offers, seat allocation, payment conditions, final approval, household resolution, student activation, academic placement, and access provisioning.
The journey ends when the student is academically first-day ready.
Academically first-day ready means the student has an active admission, required identifiers, valid academic placement, and required student and guardian access.
The blueprint includes structured handoffs of optional service interests but does not operate those services.

## Goals

- Give schools one configurable enrollment journey from first contact through first-day readiness.
- Make CBSE and Gujarat admission policy operational without hard-coding changing rules.
- Protect seat, identity, financial, placement, and account integrity during final admission.
- Support ordinary and special admission routes without creating disconnected systems.
- Give parents a clear mobile journey and give staff exception-driven operations.
- Produce reconciled, reproducible, tenant-safe enrollment reporting.
- Preserve compatibility while the future workflows replace existing screens incrementally.

## Non-goals

- Do not own ongoing attendance, examinations, promotion, timetable, homework, or classroom learning.
- Do not own fee-ledger posting, receipt accounting, settlement, or refund disbursement.
- Do not own transport, hostel, library, health, uniform, or meal-service fulfillment.
- Do not own changes to campus, board, medium, class, or division for an already active student.
- Do not own the ordinary leaving, transfer-certificate, alumni, or long-term student lifecycle after the activation rollback window.
- Do not define APIs, DTOs, database tables, migrations, provider payloads, or frontend component architecture.
- Do not represent product configuration as legal advice or automatic statutory compliance.

## Primary users

- Admission administrator configures cycles, playbooks, policy packs, seats, offers, fees, and approvals.
- Admission coordinator operates queues, deadlines, exceptions, assignment, and applicant progression.
- Counselor or receptionist captures inquiries, assists applicants, and performs permitted operational actions.
- Examiner or invigilator manages entrance-assessment operations.
- Document verifier reviews applicant evidence and corrections.
- Admission committee or principal makes controlled decisions and overrides.
- Finance staff supplies fee quotations, payment states, reconciliation results, and refund outcomes.
- Academic administrator controls board, medium, standard, division, stream, and roll-placement capacity.
- Identity administrator resolves household, guardian, account, and duplicate-person exceptions.
- Parent, guardian, or applicant completes actions and accepts decisions through a secure surface.
- Auditor or leadership user reviews policy, capacity, yield, fairness, overrides, and operational performance.

## Product principles

- Configure policy once and operate applicants by exception.
- Preserve the policy, playbook, form, fee, and decision versions applied to every applicant.
- Treat the applicant, household, and final Student as related but distinct lifecycle concepts.
- Never consume a seat, allocate a permanent identifier, or create an active student through an ambiguous partial action.
- Require permission, reason, and evidence for consequential overrides.
- Keep regulatory defaults versioned, sourced, reviewable, and replaceable.
- Keep sensitive identity, custody, financial, and document information least-privileged.
- Make parent-facing actions mobile-friendly, localizable, and clear in English, Gujarati, and Hindi.
- Derive operational and leadership reporting from auditable events and reconciled states.
- Preserve historical truth when configuration changes.

## Canonical journey

1. The school configures an admission cycle, playbook, policy pack, seat plan, fees, and responsible teams.
2. An applicant enters through inquiry, direct registration, a government allotment, transfer, re-entry, or another configured route.
3. The applicant completes the required qualification, entrance assessment, registration, consent, and document journey.
4. The system produces an admission-readiness handoff with unresolved conditions and provenance.
5. The school reserves a seat or issues a provisional or final offer when the playbook requires it.
6. The parent accepts, declines, or allows the offer to expire within the configured window.
7. Finance supplies the payment or waiver state required by the applicable payment gate.
8. Authorized staff confirms the admission decision and any required maker-checker approval.
9. The system resolves the applicant against existing people, households, guardians, siblings, and former students.
10. The system allocates or confirms permanent identifiers according to the configured numbering milestone.
11. The system activates the Student, academic placement, and required identity access without duplicating an existing person.
12. Failed activation steps enter a repair queue and do not produce a false first-day-ready status.
13. The system publishes the final outcome, preserves the complete lineage, and emits downstream handoffs.

## Canonical lifecycle states

The master lifecycle should preserve common reporting milestones while allowing playbook-specific operational stages.
Common pre-admission milestones include Inquiry, Qualified, Exam Ready, Exam Scheduled, Exam Completed, Registration Invited, Registration In Progress, Verification Pending, and Admission Ready.
Common final-admission milestones include Seat Review, Offer Prepared, Offer Issued, Offer Accepted, Payment Condition Pending, Final Approval Pending, Activation Pending, Activation Repair, First-Day Ready, Waitlisted, Rejected, Withdrawn, Expired, and Cancelled.
Every state should define entry criteria, exit criteria, owner, deadline, permitted actions, parent visibility, automation, and terminal behavior.
Custom stages should map to one common milestone for stable cross-cycle reporting.

## Capability ownership matrix

| Capability | Canonical PRD |
|---|---|
| Inquiry capture, lead operations, follow-up, and CRM entrance-exam readiness | [Inquiry to Entrance Exam Admissions CRM](./prd-inquiry-entrance-exam-crm.md) |
| Playbooks, advanced entrance exams, registration, documents, and readiness handoff | [Admissions Playbook, Registration, Documents, and Exam Configuration](./prd-admissions-playbook-registration-documents-exam-configuration.md) |
| Seat offers, reservations, capacity, waitlists, and final admission decisions | [Final Admission, Offers, and Seat Allocation](./prd-final-admission-offers-seat-allocation.md) |
| Fee quotations, concessions, payment gates, and Finance handoff | [Enrollment Fees, Concessions, and Payment Handoff](./prd-enrollment-fees-concessions-payment-handoff.md) |
| Households, guardians, siblings, identity readiness, and privacy | [Household, Guardian, Identity, and Compliance](./prd-household-guardian-identity-compliance.md) |
| Student conversion, permanent numbers, academic placement, access, and repair | [Student Activation, Academic Placement, and Access](./prd-student-activation-academic-placement-access.md) |
| Fresh, priority, RTE, transfer, lateral, mid-year, returning, and overseas routes | [Special Admission Routes](./prd-special-admission-routes.md) |
| CBSE, Gujarat, RTE, and extensible state-board defaults | [Indian Admission Policy Packs](./prd-indian-admission-policy-packs.md) |
| Cross-stage operations, analytics, audit, retention, and data quality | [Enrollment Operations, Analytics, and Governance](./prd-enrollment-operations-analytics-governance.md) |

## Shared functional requirements

- Every applicant should have one canonical enrollment journey per school, cycle, and target admission route unless an authorized exception is recorded.
- Applicants may be considered for more than one campus, class, or route without silently consuming multiple seats.
- Every rule evaluation should identify its policy, playbook, fee rule, and source version.
- Every blocked action should explain the unresolved condition and authorized resolution path.
- Every irreversible or externally visible action should support preview, confirmation, audit, and idempotent retry.
- Bulk actions should show eligible, ineligible, conflicted, and failed applicants before commitment.
- Parent-visible information should exclude internal scores, risk flags, duplicate evidence, committee notes, and protected identity information.
- Existing inquiry, registration, exam, admission, division, fee-structure, and account behavior should remain available during migration.

## Cross-module ownership

Student Enrollment owns applicant progression, offer and seat consequences, admission conditions, and activation orchestration.
Finance owns authoritative charges, ledgers, receipts, settlements, payment reconciliation, and refund execution.
Academic Setup owns valid academic years, campuses, boards, mediums, standards, divisions, streams, electives, and their capacities.
Identity and Access owns authentication, credential policy, role assignment, account recovery, and security controls.
Student Records owns the active student's long-term profile and post-activation changes.
Transport, Hostel, Library, Health, and other service modules own fulfillment after receiving an explicit handoff.

## Permissions and security

- Permissions should distinguish configuration, inquiry operations, document review, seat management, offer issue, fee exceptions, household resolution, final approval, activation, cancellation, and analytics.
- Maker-checker approval should be configurable for final offers, seat over-allocation, policy override, fee waiver, identity merge, final activation, and activation cancellation.
- Tenant isolation and assigned academic scope should apply to every list, action, export, document, and analytic.
- OTP applicant links should be time-bound, revocable, rate-limited, and restricted to approved parent-facing information.
- Sensitive identity values should be masked by default and downloads should be auditable.
- Custody, contact, and pickup restrictions should never be exposed to unauthorized guardians.

## Accessibility and localization

Parent-facing flows should support English, Gujarati, and Hindi at launch.
Templates should separate translatable text from rule logic and preserve the language version accepted by the applicant.
Critical actions should remain usable on common mobile devices and constrained networks.
The product should provide clear loading, empty, validation, expiry, conflict, unavailable, and recovery states.
Generated offers, acknowledgements, and checklists should be readable in both digital and printable formats.

## Delivery phases

### Phase 1

Phase one should deliver the CBSE and Gujarat core for offers, seats, payment gates, household resolution, policy packs, final approval, numbering, academic placement, access provisioning, and activation repair.
Phase one should support direct and offer-based admissions, ordinary fresh admissions, sibling routes, basic transfer routes, and configurable RTE intake records.

### Phase 2

Phase two should add full RTE and EWS operations, advanced concessions, transfer and re-entry routes, government export readiness, multilingual document generation, applicant self-service, and richer cross-module handoffs.

### Phase 3

Phase three may add approved external adapters, assisted document processing, identity anomaly assistance, predictive demand and yield, waitlist forecasting, and provider-specific communication integrations.
Automated recommendations should remain explainable and should not make final admission decisions without authorized human governance.

## End-to-end acceptance scenarios

- A direct-admission applicant can bypass an offer only when the applied playbook permits it.
- A CBSE applicant is evaluated against the correct state age rule and applicable class-specific transfer requirements.
- A Gujarat RTE allotment is recorded against the correct round, authority reference, seat pool, documents, and acceptance deadline.
- Two staff members cannot successfully commit the last available seat to different applicants.
- An accepted offer does not become first-day ready until its configured payment, approval, identity, numbering, and placement gates pass.
- An existing sibling family can be reused without creating duplicate guardians or exposing another household's protected information.
- A returning student resumes the correct historical admission identity when policy permits and does not receive an unrelated duplicate identity.
- A permanent number reservation, cancellation, gap, and reuse decision remains auditable.
- A failed account or placement step creates an activation repair item and does not falsely report completion.
- An unauthorized user cannot override policy, over-allocate seats, waive fees, merge identities, activate students, or view another tenant's applicants.
- Leadership can reconcile inquiries, offers, accepted seats, payments, activations, cancellations, and remaining capacity for one admission cycle.

## Public concepts

| Concept | Definition |
|---|---|
| Applicant | A person seeking admission before final Student activation. |
| Admission Route | The governed path through which an applicant seeks admission. |
| Household | A reusable family or care unit linked to one or more people and students. |
| Guardian | A person with a defined legal, care, communication, pickup, or financial relationship to an applicant or student. |
| Seat Pool | A governed capacity segment for a cycle, campus, class, board, medium, round, or category. |
| Seat Offer | A time-bound provisional or final invitation to accept an available seat. |
| Payment Gate | The configured financial condition required before a milestone may complete. |
| Admission Decision | The authorized approval, rejection, waitlist, withdrawal, or cancellation outcome. |
| Policy Pack | A versioned set of sourced jurisdiction or board defaults and validations. |
| Activation | The controlled conversion of an admission-ready applicant into an enrolled Student. |
| Activation Repair | A recoverable state for unresolved activation steps after an admission commitment. |
| First-Day Ready | The state in which admission, identifiers, academic placement, and required access are complete. |

## Release acceptance

The suite is complete when every Student Enrollment capability has one canonical owner and all companion PRDs use the same lifecycle, terminology, boundaries, and phase definitions.
The suite should let product, design, engineering, QA, school operations, Finance, and leadership derive the same admission behavior without inventing missing product decisions.
