---
module: student
domain: product
content_type: Reference
status: canonical
updated_on: 2026-08-10
owner: backend-team
audience: humans-and-agents
---

# Understand Student product behavior

TL;DR: Student owns pre-admission enrollment, active student records, attendance, leave, compliance, certificates, and dashboard previews.
Student Enrollment uses Admission Year Setup and a per-applicant Stepper where Pipeline is the source of truth before activation.

## Product areas

| Area | Status | Behavior owned by Student |
|---|---|---|
| Student Enrollment | Implemented and actively refined | Inquiry capture, owner assignment, assessment flow, visits and events, registration, Finance handoff status, placement review, activation, family self-service, admission intelligence, and governance |
| Student Records | Implemented | Active student list, management, bulk upload, profile, dashboard preview, and records settings |
| Attendance and leave | Implemented | Student attendance days, entries, leave requests, and leave type settings |
| Compliance | Implemented | Student identity compliance workflow |
| Certificates | Implemented | Student certificates and certificate templates |
| Inquiry Source | Implemented compatibility surface | Product label for compatibility route `inquiry-preferences` |

## Student Enrollment ownership boundary

[Student Enrollment workflows](./workflows.md) and this product page are the current functional sources for admissions behavior.
Student Enrollment begins when an applicant is captured in Pipeline for an academic year.
Even direct registration, management referral, transfer, RTE, or government-allotment cases must create a Pipeline inquiry record first.
Student Enrollment ends when the applicant is activated and handed to Student Records as an active student.
Student Enrollment owns applicant progression, admission communication, registration readiness, Finance handoff status, placement review, and activation orchestration.
Finance owns fee posting, receipts, settlement, reconciliation, concessions, and refunds.
Institute Setup and Class Management own academic structure, class or division capacity, and active-student counts used for placement decisions.
Student Records owns the active student's long-term lifecycle after activation.

## Current Enrollment capabilities

| Capability | Baseline rule |
|---|---|
| Pipeline | Required for every applicant and fixed enough for supportable reporting |
| Admission Year Setup | Admission-specific rules attached to an Institute Setup academic year |
| Applicant Stepper | Visible progress through Inquiry, Exam, Registration, Finance, Placement, and Activation |
| Inquiry settings | Inquiry sources, inquiry types, default owner, owner pool, and quick-capture required fields are configurable |
| Assessment | Optional by academic year, standard, admission pathway, and school policy |
| Registration | Required before activation and configurable by section, field, document, language, and route |
| Finance handoff | Enrollment requests or displays Finance verification but does not own payment accounting |
| Placement | Required, with timing configurable between standard-only and division-before-activation |
| Activation | Converts applicant to active Student only after configured gates pass |
| Communication | Required templates and simple reminders for key moments, with write-only provider credentials and delivery status based on provider evidence |
| Family enrollment center | Branded, tenant-qualified inquiry, OTP return, status, registration, document, event, offer, communication-preference, and withdrawal workflows |
| Admission intelligence | Scoped funnel, source, standard, outcome, cross-cycle, capacity, response-risk, abandonment, and SLA indicators |
| Governance | Delegation, audit search, retention, privacy requests, advisory forecasts, waitlist monitoring, and scheduled coordinator report delivery |

## Deliberately deferred surfaces

Simple admission reminders are part of Communication Settings.
Enrollment does not expose a separate generic Automation settings page.
Advanced audience builders, discount campaigns, multi-branch communication journeys, and additional provider-specific adapters remain deferred.
GR number automation is reserved for activation and can be implemented in a later slice.

## Data-integrity rules

| Rule | Consequence |
|---|---|
| Pipeline is canonical before activation | No applicant should bypass inquiry capture, even when staff proceeds directly to registration |
| Admission setup belongs to the academic year | Enrollment must extend the Institute Setup academic year rather than creating a parallel school-year model |
| Published setup is versioned | Existing applicants retain the Admission Year Setup version that determined their completed, skipped, and blocked steps |
| Duplicate inquiry prevention | Likely matches should open the existing applicant or require an authorized continue reason |
| Stepper state must be explainable | Every skipped, blocked, reopened, or completed step must have rule or audit evidence |
| Student identity data is critical | Do not silently mutate student, parent, contact, medical, or compliance data |
| Enrollment settings must preserve history | Inactive Inquiry Sources and historical configuration labels remain visible on records that already use them |
| Academic structure belongs to Institute Setup and Class Management | Enrollment must not duplicate board, medium, standard, division, or capacity truth |
| Finance owns money | Enrollment must not claim paid status unless Finance returned or verified it |
| Parent communication must be honest | Delivery success must not be inferred from template configuration alone |
| Provider credentials are write-only | Stored header values, parameter values, and callback secrets are redacted from every read response |
| Admission reports enforce scope | Report totals and groupings must use tenant, academic-year, UserScope, date, and standard filters on the server |
| Activation is a boundary | Ordinary active-student lifecycle changes belong to Student Records after activation |

## Documentation status

This page and [workflows.md](./workflows.md) define the canonical Enrollment product behavior.
When changing Enrollment behavior, update this page, [api.md](./api.md), [workflows.md](./workflows.md), and [testing.md](./testing.md).
Frontend route and screen documentation remains in [Student frontend docs](../../../../Shikshamitra-FE/docs/student/README.md).
