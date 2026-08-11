---
module: student
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-08-10
owner: backend-team
audience: humans-and-agents
---

# Trace Student backend workflows

TL;DR: Student Enrollment starts in Pipeline, follows Admission Year Setup through a stepper-style applicant path, then requires Finance handoff, placement review, approval, and activation before Student Records takes over.

## Core Student workflows

| Workflow | Main steps | Validation focus |
|---|---|---|
| Enrollment Pipeline | Capture inquiry, assign owner, follow up, message family, and choose exam or registration path | Academic year, applicant identity, guardian contact, source, type, owner, tenant, and academic scope |
| Admission Year Setup | Configure admission rules attached to one Institute Setup academic year | No parallel academic-year model, valid standards, route applicability, step rules, and message moments |
| Applicant Stepper | Show one applicant's progress and next action | Step state, blockers, skips, owner, parent-safe visibility, and audit evidence |
| Assessment | Configure applicability, schedule a written test, interaction, or interview, invite family, record attendance and results, and decide eligibility | Standard or pathway applicability, pass rule, retest policy, result audit, and override permission |
| Registration | Collect applicant, family, previous-school, consent, and document details, then verify | Required fields, required documents, language labels, correction reason, and document access |
| Finance handoff | Request Finance verification and block activation until Finance returns a verified state when required | Finance authority, payment-reference visibility, and no ledger or receipt mutation from Enrollment |
| Placement review | Confirm board, medium, standard, and division timing from Institute Setup and Class Management | No parallel capacity truth, valid academic hierarchy, current count, pending activation count, and override reason |
| Activation | Approve and convert applicant into active Student after required gates pass | Registration, exam, Finance, placement, approval, idempotency, and repair visibility |
| Student records | Manage active student profile after activation | Scope filters, pagination, dashboard preview freshness, and data integrity |
| Attendance and leave | Capture student attendance and manage leave | Date consistency, student eligibility, leave type validity, and reporting impact |
| Certificates | Create templates and issue certificates | Student identity, template validity, and audit trail |

## Enrollment operating flow

1. Staff selects the academic year from Institute Setup.
2. Enrollment loads Admission Year Setup attached to that academic year.
3. Staff captures every applicant in Pipeline, including walk-in, phone, website, referral, transfer, RTE, and direct-registration cases.
4. Pipeline stores applicant name, guardian contact, board, medium, standard, source, inquiry type, owner, and notes.
5. The Applicant Stepper shows Inquiry as complete and calculates the next required step from Admission Year Setup.
6. Staff sends an acknowledgement or follow-up message from an approved template.
7. Staff schedules or invites the configured assessment when Admission Year Setup requires it.
8. Staff may move directly to Registration when the published configuration requires no exam. Once an exam is scheduled, an authorized eligibility override with a reason may also continue the applicant without a recorded result.
9. Exam staff records attendance and result when an exam is required.
10. The system marks the applicant Eligible, Ineligible, Needs Review, Absent, or Retest Required.
11. Staff issues one shared Registration draft as Online, Staff-assisted, or Both. Email failure does not revoke the issued registration, and staff can retry the invitation against that same draft.
12. Registration staff verifies the template-driven sections and documents; activation projects verified canonical responses into Student Records.
13. Enrollment requests Finance verification when Admission Year Setup requires a fee gate.
14. Academic staff reviews placement using Institute Setup and Class Management structure and capacity signals.
15. An authorized approver confirms activation.
16. Activation creates or links the active Student record and hands ordinary lifecycle ownership to Student Records.

## Governance rules

| Scenario | Expected behavior |
|---|---|
| Applicant skips Pipeline | Reject or redirect the operation because Pipeline is the pre-admission source of truth |
| Academic year has no Admission Year Setup | Show setup-required state and block new admissions unless a safe default is explicitly allowed |
| Stepper skip | Require an Admission Year Setup rule or authorized bypass reason |
| Stepper blocker | Show missing data, document, Finance status, capacity issue, or permission requirement |
| Exam not required | Allow direct movement from Pipeline to Registration with the configured reason trail |
| Exam required but unscheduled | Block Registration; an unscheduled applicant cannot use eligibility override |
| Exam scheduled | Allow Registration after an eligible result or an authorized Eligible override with a recorded reason |
| Ineligible applicant | Keep terminal unless an authorized user reopens, overrides, or schedules retest with reason |
| Registration incomplete | Block activation and show missing information or document requirements |
| Finance verification required | Block activation until Finance returns Verified |
| Finance pending or blocked | Keep applicant visible in Pipeline, Registration, or Activation work queues without claiming payment success |
| Placement capacity unknown | Show unknown capacity rather than inventing available seats |
| Capacity override | Require permission and reason |
| Activation failure | Keep repairable activation state visible and do not report first-day-ready completion |
| Active student edit | Route ordinary post-activation edits to Student Records |

## Settings workflows

| Settings area | Workflow |
|---|---|
| Admission Year Setup | Configure admission dates, routes, required steps, owners, messages, and publish readiness for an Institute Setup academic year |
| Pipeline Settings | Configure inquiry sources, inquiry types, owner defaults, owner pool, quick-capture required fields, and follow-up reasons |
| Assessment Settings | Configure applicability, slots, pass rules, retest policy, family scheduling, and message templates |
| Registration Settings | Configure sections, fields, documents, languages, verification, and correction reasons |
| Finance Handoff Settings | Configure whether Finance verification is required and which Finance reference is visible |
| Class Placement Settings | Configure placement timing and override permission behavior while reading live capacity status from Class Management |
| Activation Settings | Configure approval, required gates, cancellation window, and admission confirmation message |
| Communication Settings | Configure channels, templates, variables, consent behavior, provider mode, and delivery-log visibility |

Simple admission reminders remain part of Communication Settings.
Enrollment does not expose a separate generic Automation page.
Governance settings provide scoped advisory forecasts, retention policies, privacy-request handling, delegation, audit search, waitlist monitoring, and scheduled report delivery without adding extra top-level navigation.
One-time admission moments send immediately when the corresponding action occurs.
Idle-delay reminders are available only for Registration Issued, Registration Incomplete, Registration Correction Requested, Registration Verified, Fee Follow-up, Exam No-show, and Activation Welcome.
The hourly lifecycle worker reevaluates those delayed states in batches of 250.
Terminal outcomes, completed requirements, consent, opt-out, quiet hours, attempt limits, and duplicate-message protection can cancel or suppress a reminder.
Provider header values, parameter values, and callback secrets remain write-only after storage.

## Admission Reports workflow

Admission Reports uses the same fixed lifecycle projection as Pipeline.
Staff may filter by applicant creation date and target standard.
The backend applies tenant, academic-year, effective UserScope, date, and standard filters before grouping.
The screen shows one table at a time for lifecycle, source, standard, or closed outcome, with a second academic-year selector for cross-cycle comparison.
The report returns all fixed lifecycle and closed-outcome rows, including zero-count rows, so exports and comparisons remain stable.
The report date span is limited to 366 days to keep interactive requests bounded.
The intelligence summary adds approved and remaining capacity, projected seat surplus or shortfall, first-response risk, funnel abandonment and completion, overdue work, and SLA misses from server-scoped data.
Scheduled delivery accepts only an active Enrollment employee recipient and sends an authenticated report link rather than attaching applicant data to email.

## Family enrollment workflow

The public portal returns only safe tenant branding, support contacts, office hours, and configured languages.
English copy is always available, while configured locale tags may select tenant-authored form labels and fall back to English for built-in interface copy.
After OTP verification, a guardian can review related authorized applications, update email, SMS, and WhatsApp preferences, and withdraw an eligible application with an explicit reason.
Withdrawal releases Enrollment-owned reservations and waitlist state, closes active offers, records timeline evidence, and never performs Finance accounting work.
