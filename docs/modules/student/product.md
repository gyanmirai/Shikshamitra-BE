---
module: student
domain: product
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# Understand Student product behavior

TL;DR: Student owns inquiry, registration, admission, student records, attendance, leave, compliance, certificates, and dashboard previews.
Only part of that surface has deep written behavior docs today, so use source paths before making broad claims.

## Product areas

| Area | Status | Behavior owned by Student |
|---|---|---|
| Enrollment inquiry | Implemented and future-state documented | Inquiry capture, notes, settings, entrance exam, lead-source tracking, and admissions CRM |
| Registration | Implemented and future-state documented | General details, configurable forms, family, communication, bank, medical, previous school, documents, and readiness |
| Admission | Implemented with future-state suite | Offers, seats, financial conditions, household resolution, final approval, Student activation, placement, and access |
| Student records | Implemented | Student list, management, bulk upload, dashboard preview, and settings |
| Attendance and leave | Implemented | Student attendance days, entries, leave requests, and leave type settings |
| Compliance | Implemented | Student identity compliance workflow |
| Certificates | Implemented | Student certificates and certificate templates |
| Inquiry Source | Focused documented surface | Product label for compatibility route `inquiry-preferences` |

## Student Enrollment ownership boundary

The [Student Enrollment Master Blueprint](./prd-student-enrollment-master-blueprint.md) is the canonical functional source for the complete future enrollment journey.
Student Enrollment begins with an inquiry, direct registration, government allotment, transfer, re-entry, or another configured admission route.
Student Enrollment ends when the admitted student is academically first-day ready with required identifiers, academic placement, and access.
Student Enrollment may hand optional service interests to other modules but does not fulfil transport, hostel, library, health, uniform, meal, or similar services.
Finance remains authoritative for monetary posting, receipts, settlement, reconciliation, and refund execution.
Student Records owns active-student changes and the long-term lifecycle after the controlled activation-cancellation window.

## Enrollment PRD coverage

| Product area | Canonical source |
|---|---|
| Complete journey and ownership | [Student Enrollment Master Blueprint](./prd-student-enrollment-master-blueprint.md) |
| Inquiry CRM and entrance-exam eligibility | [Inquiry to Entrance Exam Admissions CRM](./prd-inquiry-entrance-exam-crm.md) |
| Playbooks, registration, documents, and readiness | [Admissions Playbook, Registration, Documents, and Exam Configuration](./prd-admissions-playbook-registration-documents-exam-configuration.md) |
| Offers, seats, waitlists, and final decisions | [Final Admission, Offers, and Seat Allocation](./prd-final-admission-offers-seat-allocation.md) |
| Financial admission conditions | [Enrollment Fees, Concessions, and Payment Handoff](./prd-enrollment-fees-concessions-payment-handoff.md) |
| Households, guardians, and identity | [Household, Guardian, Identity, and Compliance](./prd-household-guardian-identity-compliance.md) |
| Student activation, placement, and access | [Student Activation, Academic Placement, and Access](./prd-student-activation-academic-placement-access.md) |
| Route-specific admission behavior | [Special Admission Routes](./prd-special-admission-routes.md) |
| CBSE, Gujarat, RTE, and state-board policy | [Indian Admission Policy Packs](./prd-indian-admission-policy-packs.md) |
| Operations, analytics, and governance | [Enrollment Operations, Analytics, and Governance](./prd-enrollment-operations-analytics-governance.md) |

## Data-integrity rules

| Rule | Consequence |
|---|---|
| Student identity data is critical | Do not silently mutate student, parent, contact, medical, or compliance data |
| Enrollment settings must preserve history | Inactive Inquiry Sources remain visible on records that already use them |
| Academic scope affects reports and fees | Board, medium, standard, division, and academic year must remain consistent |
| Documents need access audit | Sensitive student documents must not be exposed without traceability |
| Attendance affects reporting | Student attendance mutations must preserve date and student consistency |

## Documentation status

The Student Enrollment future-state product surface is covered by the master PRD suite.
The [Student Enrollment implementation record](./implementation-student-enrollment-master-blueprint.md) maps that suite to the validated release baseline.
The remaining Student product areas still require source-code review where focused behavior documentation is not available.
When adding or changing Student behavior, update [api.md](./api.md), [workflows.md](./workflows.md), and [testing.md](./testing.md).
