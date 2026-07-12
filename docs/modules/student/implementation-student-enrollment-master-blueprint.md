---
module: student
domain: implementation
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# Implement the Student Enrollment master blueprint

TL;DR: The Student Enrollment program is implemented from inquiry capture through first-day-ready activation for the current generic-provider and offline-exam release baseline.

## Source of truth

The [Student Enrollment Master Blueprint](./prd-student-enrollment-master-blueprint.md) and its capability ownership matrix remain the functional source of truth.
This document records implementation and validation evidence without redefining product requirements.
Existing inquiry, registration, entrance-exam, admission, division, fee-structure, and account workflows remain available for compatibility.

## Implemented capability map

| Canonical product area | Implementation status | Delivered baseline |
|---|---|---|
| Inquiry CRM and entrance exam | Complete for current scope | Lightweight leads, sources, channels, public forms, consent, duplicate governance, routing, scoring, ownership, tasks, timelines, pipeline controls, working-calendar SLA rules, holds, stale and archive automation, offline exam scheduling, attendance, marks, merit, eligibility, OTP status, analytics, and generic communication dispatch |
| Admissions playbook and registration | Complete for current scope | Versioned playbooks, exam rules, registration templates, numbering, issue and reissue lifecycle, OTP resume, CRM prefill, document checklists, review, correction, waiver, original-seen evidence, readiness approval, queues, and public applicant progress |
| Offers, seats, and final decisions | Complete for current scope | Seat pools, capacity states, draft and approved offers, immutable issued snapshots, reservations, acceptance, decline, expiry, supersession, governed waitlists, promotion preparation, decision history, withdrawal, cancellation, and concurrency checks |
| Financial readiness | Complete for Enrollment ownership | Admission quotations, fee components, concessions, waivers, payment gates, Finance references, reconciliation status, seat consequences, and Finance-owned settlement boundary |
| Household and identity | Complete for current scope | Reusable people, households, memberships, guardians, siblings, grandparents, authority and communication roles, identity provenance, PEN, APAAR, UDISE readiness, consent, duplicate resolution, protected restrictions, and government-readiness export |
| Student activation | Complete for current scope | Approval, GR or admission-number series, uniqueness and gap governance, academic placement, student and guardian access requirements, service handoffs, first-day-ready state, idempotent repair, cancellation preview, maker-checker cancellation, and same-cycle rollback controls |
| Special admission routes | Complete for current scope | Fresh, sibling, staff-child, alumni, RTE record, transfer, lateral, mid-year, returning, interstate, overseas-return, route changes, evidence, eligibility, exceptions, appeals, and route audit |
| Indian policy packs | Complete for configured baseline | Sourced and versioned CBSE, Gujarat Board, and Gujarat RTE defaults, applicability, effective dates, review dates, maker-checker publication, controlled school adoption, override warnings, evaluation evidence, and supersession history |
| Operations and governance | Complete for current scope | Command-center queues, saved views, delegation, server-side bulk actions, audit search, data-quality issues, retention policies, privacy requests, traceable expiring exports, scheduled reports, SLA escalation, protected analytics permissions, and advisory forecasts |
| Parent and applicant experience | Complete for current scope | English, Gujarati, and Hindi inquiry and status surfaces, canonical option values with localized labels, OTP access, registration, documents, offer response, exam information, pending actions, correction requests, expiry states, and applicant-safe activity |
| Communication foundation | Complete for generic provider baseline | Templates, variables, preview, approval, consent and opt-out checks, generic webhook and manual queue adapters, delivery logs, callbacks, retry, failure state, and triggered journeys |

## Release constraints

First-party WhatsApp, SMS, and email vendor SDK adapters remain deferred until providers and payload contracts are selected.
Government interfaces remain versioned exports or pluggable adapters where an approved interface exists.
The product does not claim automatic legal or statutory compliance.
Online testing and proctoring remain outside the offline entrance-exam baseline.
Optional transport, hostel, library, health, uniform, and related service fulfillment remains outside Student Enrollment.
Restricted-user browser denial remains an additional validation when `E2E_STUDENT_CRM_DENIED_EMAIL` is supplied.

## Migration evidence

The CRM schema is represented by incremental migrations from lead foundation through SLA governance.
`20260712080000_SyncAdmissionCrmModelSnapshot` synchronizes the generated EF model snapshot without duplicating schema operations already owned by earlier incremental migrations.
`20260712221312_CompleteAdmissionCrmOwnedCatalogs` adds the CRM-owned Inquiry Source, exam-slot, and document-type catalogs and replaces the lead Inquiry Preference foreign key.
`dotnet ef migrations has-pending-model-changes` reports no model drift.
The local PostgreSQL DDVS development database applied every pending CRM migration through the CRM-owned catalog migration successfully.

## Validation evidence

The following evidence was recorded on 2026-07-11 after the final governance and SLA changes.

| Validation | Result |
|---|---|
| Backend build | Passed with zero errors and existing warning debt |
| CRM domain tests | 29 passed |
| CRM application tests | 6 passed |
| CRM HTTP API tests | 85 passed |
| Frontend TypeScript | Passed |
| Focused CRM, settings, public, and menu tests | 74 passed |
| Backend Student documentation validator | Passed |
| Frontend Student documentation validator | Passed |
| EF pending-model check | No pending model changes |
| Local DDVS read-only Playwright preflight | 1 passed |
| Local DDVS mutating Playwright smoke | 6 passed and 1 optional restricted-user case skipped |

The browser smoke proves anonymous denial, principal inquiry capture, duplicate review, public consent, OTP status creation, exam scheduling, result and eligibility, registration gating, secure partial registration, document correction and replacement, original confirmation, readiness, and handoff.
The local browser host must run with development auto-authentication disabled so anonymous denial is tested under production-equivalent authentication behavior.
Remote mutating CRM smoke remains prohibited unless explicitly approved for a disposable staging tenant.

## Operational safeguards

Tenant and academic-scope checks apply to CRM lists, mutations, reports, exports, and public access resolution.
Consequential overrides require explicit permissions and reasons.
Maker-checker rules apply where configured for policies, retention, offers, activation, and cancellation.
Bulk execution reports per-item success and failure and validates academic scope before mutation.
Localized form options preserve canonical submitted values while rendering translated labels.
SLA evaluation uses the configured time zone, working days, working hours, holidays, approved holds, and escalation owner.
Scheduled report runs expose bounded failure details to authorized operators.
Partial activation failures remain visible and repairable and do not report false first-day readiness.

## Remaining release actions

Publish the frontend and backend changes through the normal code-review process.
Run restricted-user browser denial when a dedicated denied credential is available.
Select communication vendors before planning first-party provider adapters.
Validate future government adapters against the approved interface and jurisdiction before enabling them.
