---
module: student
domain: implementation-plan
content_type: Reference
status: current
updated_on: 2026-07-16
owner: backend-team
audience: humans-and-agents
---

# Implement admissions playbooks, registration, documents, and exam configuration

TL;DR: the companion admissions PRD is delivered as five tenant-safe vertical phases with legacy Student Enrollment compatibility and local seeded DDVS browser evidence.
The functional source of truth is [prd-admissions-playbook-registration-documents-exam-configuration.md](./prd-admissions-playbook-registration-documents-exam-configuration.md).

## Delivery status

Phases one through five are complete for the supported release baseline.
The release baseline includes admissions playbooks, exam rules and operations, configurable registration, secure applicant links, document verification, one-person or two-person readiness approval, operations queues, automation, generic communication dispatch, analytics, and admission handoff.
Applicant self-scheduling is enabled only by an explicit published playbook assignment rule.
Generic webhook and manual queue adapters are the supported communication baseline.
Provider-specific SDK adapters, online testing, optional assisted document extraction, and Finance fee-ledger posting remain outside this release.
Final admission, seat allocation, and student activation are delivered by their companion Enrollment workflows.

## Workflow clarification and safety hardening

The pipeline now reports whether registration has been issued and opens the template-backed Registration workspace instead of the legacy manual form-number dialog.
Pipeline applicant drawers expose only Overview, the current exam work when applicable, and Registration after eligibility or issuance.
Offer, Activation, and Governance remain available from their owning workspaces instead of appearing prematurely in Pipeline.
Staff with document-review permission can upload or replace applicant registration checklist documents through the authenticated registration document endpoint.
Applicant and staff uploads share the same extension, size, versioning, replacement-history, and timeline rules.
Generated documents are grouped by their owning workflow: hall tickets under Exam, acknowledgements and corrections under Registration, offers under Offer, and admission or cancellation letters under Activation.
Backend availability checks prevent each document from being previewed or issued before its owning registration, correction, offer, activation, or cancellation state exists.
Each exam definition now records whether hall tickets are enabled.
Older stored exam definitions remain hall-ticket enabled for compatibility, while new quick setups default hall tickets off.
Hall-ticket preview and issuance require a scheduled exam, respect the resolved playbook setting, and include the scheduled date, time, and venue.
The Exam and Eligibility page provides a quick setup for a written exam, student interaction, or no entrance assessment while retaining the advanced versioned playbook editor.
Finance remains a manual confirmation placeholder until the Finance integration is connected.
An issued unsatisfied payment gate now blocks its configured milestone and every later milestone.
Activation readiness is recalculated at approval and execution so an expired, reversed, disputed, or newly introduced gate cannot be bypassed by an older readiness snapshot.

## Program rules

- Keep pre-admission state in admissions CRM aggregates until the admission-readiness handoff is accepted by a later admission workflow.
- Preserve current inquiry, registration form, registration, document type, entrance exam slot, and entrance exam endpoints during migration.
- Use admission-cycle configuration as the owner of playbook applicability and version selection.
- Store operational events as auditable records rather than inferring history from the latest status.
- Enforce tenant isolation, user scope, and permissions in backend services.
- Version published playbooks, forms, exam rules, and document checklists.
- Stop ambiguous automation and create an exception instead of selecting an arbitrary configuration.
- Keep Finance responsible for payment transactions and store only registration-fee rules and external payment references in this program.
- Keep offline entrance exams as the primary assessment model.
- Keep generic webhook and manual queue dispatch as the communication provider baseline.

## Phase 1: Playbooks and advanced exam configuration

Status: complete.

Deliver versioned admissions playbooks, applicability, rounds, seat plans, exam definitions, assessment components, slot templates, blackout dates, assignment rules, result rules, merit rules, waitlist rules, and publication validation.
Add distinct permissions for playbook publication, seat-plan access, exam configuration, capacity override, result publication, and result override.
Extend the Student Enrollment settings experience with playbook, rounds and seats, exam definitions, slot rules, and result-policy views.
Keep current entrance-exam slots operational while new exam definitions and templates are introduced.

Phase acceptance requires:

- Conflicting playbook applicability cannot be published.
- Published versions remain immutable for assigned applicants.
- Seat plans report planned, offered, accepted, waitlisted, withdrawn, and remaining capacity without creating admissions.
- Slot assignment is explainable and unresolved assignments enter an exception queue.
- Capacity and result overrides require separate permissions and reasons.
- Component and aggregate result rules produce deterministic preview outcomes.
- Backend API tests, frontend contract tests, settings tests, permission tests, and type checks pass.

## Phase 2: Registration builder and secure lifecycle

Status: complete.

Deliver registration templates, sections, system and custom fields, conditional visibility and required rules, prefill mapping, immutable published versions, form-number series, fee rules, issue rules, secure OTP links, expiry, resend, extension, revocation, reissue, partial save, submission, and correction cycles.
Retain existing fixed registration sections as the compatibility path while new issued templates become the configurable path.
Add permissions for template publication, registration issue, form-number override, fee waiver, link extension, and link revocation.

Phase acceptance requires:

- Template applicability conflicts and circular field rules are rejected before publication.
- Existing issued forms retain their template version after later edits.
- Verified information is not silently overwritten by applicant changes.
- Form numbers remain unique and issued numbers are never silently reused.
- Parents can save and resume securely, submit, receive corrections, and resubmit.
- Expired, revoked, extended, and reissued links retain complete audit history.
- Backend lifecycle tests, public-link tests, frontend builder tests, and mobile workflow tests pass.

## Phase 3: Documents and admission readiness

Status: complete.

Deliver versioned document checklist templates, conditional requirements, upload constraints, undertakings, verification, correction requests, replacements, expiry, waivers, original-seen evidence, reviewer queues, readiness calculation, override approval, and the admission handoff package.
Reuse current document types as the document catalog while adding applicant-specific requirements and verification history.
Add permissions for checklist publication, document review, waiver, original-seen confirmation, readiness confirmation, and readiness override.

Phase acceptance requires:

- Applicant checklists resolve deterministically from the applicable published version.
- Replacement uploads preserve prior file and decision history.
- Rejections and waivers require appropriate reasons and permissions.
- Blocking requirements prevent admission readiness unless an authorized override is recorded.
- The handoff distinguishes verified, applicant-supplied, staff-supplied, and derived values.
- Backend document and readiness tests, frontend queue tests, permission tests, and status-link tests pass.

## Phase 4: Operations, automation, communication, and analytics

Status: complete.

Deliver staff exception queues, unified timeline coverage, bulk-action previews, rule execution evidence, deadline reminders, escalation, applicant-safe status details, and playbook, exam, registration, document, readiness, and yield analytics.
Extend existing CRM journeys and generic communication dispatch instead of creating a separate messaging engine.
Add operational metrics only when they can be reproduced from persisted configuration, state, and timeline evidence.

Phase acceptance requires:

- Every failed automated action remains visible and creates a safe follow-up path.
- Reminders stop when the requested action is complete or no longer applicable.
- Bulk actions show the affected applicants before confirmation and audit each result.
- Applicant status never exposes internal notes, scores, duplicate evidence, or other applicants.
- Analytics reconcile with filtered operational records for the same tenant and cycle.
- Backend analytics tests, frontend queue and dashboard tests, communication tests, and permission tests pass.

## Phase 5: Regression, migration, and browser validation

Status: complete for all required gates and the available seeded credentials.

Backfill or link existing CRM registration issues, legacy entrance-exam records, and supported documents without destructive conversion.
Run compatibility coverage for legacy inquiry, registration form, registration, document, and entrance-exam APIs.
Start the local backend and frontend against seeded DDVS data.
Run read-only admissions preflight before any mutating browser scenario.
Run the full applicant and staff E2E workflow only against the seeded local tenant or an explicitly approved throwaway staging tenant.

Phase acceptance requires:

- Backend build and focused application, CRM API, registration, document, and entrance-exam tests pass.
- Frontend TypeScript, CRM contracts, settings, workspace, public applicant, and permission tests pass.
- Documentation validators and `git diff --check` pass in both repositories.
- E2E covers playbook publication, slot assignment, result publication, registration issue, partial save and resume, document correction, readiness, handoff, and unauthorized action denial when denied-user credentials are available.
- A requirement-by-requirement audit identifies authoritative evidence for every PRD acceptance scenario.

## Validation commands

Run backend checks from `Shikshamitra-BE`:

```powershell
dotnet build
dotnet test test\ShikshaMitra.HttpApi.Tests\ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~AdmissionCrm"
dotnet test test\ShikshaMitra.Application.Tests\ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Students"
powershell -ExecutionPolicy Bypass -File scripts\docs\validate-docs.ps1 -Module student
git diff --check
```

Run frontend checks from `Shikshamitra-FE`:

```powershell
npm run ts
npm run test -- src/redux/student/admissions-crm src/screens/student/enrollment/admissions-crm src/screens/student/enrollment/setting
npm run test -- src/constants/student-menu.permissions.test.ts
npm run test:e2e:student-crm:preflight -- --use-default-credentials
npm run test:e2e:student-crm -- --use-default-credentials
node scripts/docs/validate-docs.mjs --module student
git diff --check
```

## Completion evidence

The backend solution build passed with zero errors and four pre-existing nullable warnings in unrelated test files.
The backend Admission CRM HTTP suite passed 71 of 71 tests.
The focused legacy inquiry, entrance exam, and student notes compatibility pack passed 47 of 47 tests.
The Student application test filter passed 7 of 7 tests.
The readiness two-person approval domain test passed and the role-permission seeder pack passed 6 of 6 tests.
The EF model reports no pending changes after the final snapshot update.
The local development migrator applies the complete CRM schema and seeded DDVS permission update successfully.
The frontend TypeScript check passed.
The focused frontend CRM, public applicant, settings, and contract pack passed 68 of 68 tests.
The Student menu permission pack passed 3 of 3 tests.
The read-only local DDVS browser preflight passed 1 of 1 test.
The final mutating local DDVS browser suite passed all 6 executable scenarios in 1.1 minutes.
The browser suite covers anonymous denial, inquiry and duplicate handling, public consent, entrance exam result and eligibility, the legacy registration gate, and the complete secure registration and document-readiness journey.
The complete secure browser journey covers local webhook OTP delivery, partial save, submission, document upload, staff rejection, replacement upload, verification, original-seen evidence, readiness confirmation, and admission handoff.
Restricted-user browser denial remains optional and was skipped because `E2E_STUDENT_CRM_DENIED_EMAIL` was not supplied.
Provider-specific WhatsApp, SMS, and email SDK adapters remain outside completion until vendors are selected.
Finance fee-ledger posting remains outside this implementation plan and is represented by audited manual confirmation until the Finance integration is connected.
