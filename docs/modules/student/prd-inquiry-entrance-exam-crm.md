---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Inquiry to Entrance Exam Admissions CRM

TL;DR: Build the future Student Enrollment inquiry-to-entrance-exam experience as an admissions CRM for Indian schools, not as a simple inquiry register.
This document is functional product direction only.
It intentionally avoids API contracts, database schema, migrations, and code implementation details.

## Product Intent

Student Enrollment needs a world-class admissions CRM for school inquiry management.
The product should help schools capture interest from many channels, qualify the lead, follow up on time, schedule entrance exams, record outcomes, and hand eligible applicants to the admission workflow.
The product should support Indian school admission operations where inquiries can come from phone calls, walk-ins, school websites, WhatsApp, QR codes, campaigns, and staff-shared links.
The product should keep quick inquiry capture fast while still allowing each school to configure richer public and internal forms.
The product should make follow-up work visible, measurable, and auditable.
The product should support direct movement from inquiry to entrance exam once the minimum exam readiness information is available.
The product should keep registration form issue as an optional school-specific step, not as a mandatory gate before entrance exam.

## Current Development Baseline

The current development state is a good start but not the target product.
The inquiry screen currently behaves mainly as a form plus table.
Inquiry source configuration exists and should remain the product term.
Some existing development code and compatibility routes may still use `InquiryPreference` naming internally.
Entrance exam slots and entrance exam status concepts already exist in the Student Enrollment area.
The current inquiry capture flow is stricter than the desired CRM experience because it asks for many details before saving a lead.
The future product should allow a lead to be created with contact details and basic admission intent first, then progressively collect more information before exam scheduling or admission handoff.

## Scope

This PRD covers Student Enrollment inquiry through entrance exam result and admission eligibility handoff.
This PRD includes inquiry capture, inquiry configuration, pipeline management, follow-up, communication templates, public inquiry forms, entrance exam scheduling, entrance exam attendance, entrance exam results, and funnel analytics.
This PRD does not cover full admission completion, fee payment, student records, long-term student lifecycle, classroom allocation after admission, or online test-taking.
This PRD treats offline entrance exam operations as the primary exam model.
This PRD treats generic webhook or manual queue dispatch for WhatsApp, SMS, and email as the release baseline for outbound communication.
This PRD treats provider-specific SDK adapters as future extension work after vendors are selected.

## Enrollment suite relationships

The [Student Enrollment Master Blueprint](./prd-student-enrollment-master-blueprint.md) defines the canonical journey, terminology, shared states, and ownership boundaries for the complete enrollment suite.
The [Admissions Playbook, Registration, Documents, and Exam Configuration PRD](./prd-admissions-playbook-registration-documents-exam-configuration.md) continues from this eligibility handoff into configurable registration readiness.
This PRD remains the source of truth for inquiry CRM, lead operations, exam readiness, and eligibility decisions.
The companion PRD owns admissions playbooks, advanced exam configuration, registration templates and links, document verification, readiness checks, and the downstream handoff package.
The [Final Admission, Offers, and Seat Allocation PRD](./prd-final-admission-offers-seat-allocation.md) begins after admission readiness and owns seat commitment and final decisions.
Policy, household, financial-readiness, activation, special-route, and operations behavior is owned by the companion PRDs linked from the master blueprint.

## Goals

- Make inquiry capture fast enough for reception, phone calls, and walk-ins.
- Make public inquiry forms configurable enough for different admission campaigns and school sections.
- Make every inquiry traceable by source, channel, campaign, owner, stage, score, consent, and next action.
- Make follow-up work operationally reliable through reminders, tasks, SLA rules, and stale lead handling.
- Make duplicate inquiry handling explicit and auditable.
- Make entrance exam scheduling and result processing operationally complete for offline exams.
- Make analytics useful for school leadership and admissions staff.
- Preserve tenant isolation, permissions, auditability, and data integrity.

## Non-Goals

- Do not build a full online examination engine in this PRD.
- Do not build proctoring, question banks, or auto-graded online tests in this PRD.
- Do not replace the full admission workflow in this PRD.
- Do not define backend API routes, DTOs, database tables, migrations, or frontend component architecture in this PRD.
- Do not require every school to follow one rigid inquiry-to-exam sequence.

## Primary Users

- Receptionist creates quick inquiries from walk-ins and phone calls.
- Admission counselor owns leads, follows up, schedules exams, and updates outcomes.
- Admission coordinator configures cycles, forms, stages, routing, reminders, templates, and stale rules.
- Principal or school leadership reviews funnel, SLA, source performance, and exam conversion analytics.
- Parent or applicant submits public inquiry forms and receives acknowledgement, schedule, status, and result messages.
- System administrator manages permissions, tenant setup, communication providers, and security controls.

## Product Principles

- Capture first, enrich later.
- Contactability is mandatory for every inquiry.
- Basic admission intent is mandatory for every inquiry.
- Sensitive student and parent data must be protected.
- School-specific configuration should not break reporting consistency.
- Automation should assist staff, not hide accountability.
- Every override must have permission and reason.
- Public forms must capture consent before communication.
- Analytics must be reproducible from auditable operational data.

## End-to-End Journey

1. A school configures an admission cycle for an academic year.
2. The school enables inquiry channels and creates form templates.
3. A parent or staff member creates an inquiry from a configured channel.
4. The system detects possible duplicates before the inquiry is committed or immediately after submission.
5. The system assigns the inquiry to an owner through routing rules or manual assignment.
6. The inquiry enters a configured pipeline stage around fixed system milestones.
7. The owner follows up through calls, notes, tasks, and message templates.
8. The system monitors SLA, next action, lead score, and inactivity.
9. The inquiry moves to exam readiness when minimum exam details are available.
10. The counselor schedules the applicant into an entrance exam slot.
11. The school records exam attendance and marks.
12. The system calculates pass, fail, merit, waitlist, rejection, or eligibility status according to configured rules.
13. Eligible applicants are handed to the admission workflow with required context.
14. Stale or archived leads can be reopened with reason when the family re-engages.

## Admission Cycle Configuration

An admission cycle represents a school admission season for an academic year or intake.
The system should support reusable school-level defaults.
The system should copy defaults into each cycle so schools can safely override cycle-specific behavior.
Cycle configuration should include pipeline stages, inquiry sources, channels, form templates, routing rules, lead scoring rules, follow-up SLAs, message templates, stale rules, archive rules, exam settings, and analytics labels.
Cycle-level changes should not silently rewrite historical inquiries from older cycles.
Cycle configuration should show whether an item is inherited from school defaults or overridden for the current cycle.

## Pipeline Configuration

The pipeline should be configurable around fixed system milestones.
Schools should be able to rename, reorder, activate, and deactivate configurable stages within allowed milestone boundaries.
Fixed milestones should protect reporting and workflow integrity.
Recommended milestones are New Inquiry, Follow-up, Exam Ready, Exam Scheduled, Exam Completed, Eligible, Waitlisted, Rejected, Stale, and Archived.
Stages should support color, display order, SLA target, allowed next stages, and owner responsibility.
A stage should be inactive rather than deleted when historical inquiries still use it.
The system should keep stage history for audit and analytics.

## Inquiry Channels

The product should support many inquiry channels.
Supported channels should include school website form, WhatsApp inquiry, staff-shared link, QR code scan, quick call entry, walk-in reception, campaign link, and manual back-office entry.
Each channel should be enabled or disabled per admission cycle.
Each channel should have a default source, default owner rule, default form template, and default acknowledgement template.
Channel reporting should show inquiry volume, conversion, stale rate, exam scheduled rate, exam attendance rate, pass rate, and admission eligibility handoff rate.
Channels should be different from Inquiry Source.
Channel describes how the inquiry arrived.
Inquiry Source describes the business origin or admission source label used for reporting.

## Inquiry Source

Inquiry Source is the product label for source tracking.
Examples include Website, Walk-in, Phone Call, WhatsApp, Staff Referral, Sibling Reference, Alumni Reference, Campaign, Social Media, and Education Fair.
Schools should be able to configure source name, description, active status, display order, default channel mapping, and reporting group.
Inactive sources should remain visible on existing inquiries that already use them.
New inquiries should only use active sources unless an authorized user overrides with reason.
In-use sources should not be hard-deleted.
The product should note that existing development internals may still use `InquiryPreference` naming for compatibility.

## Dynamic Form Templates

Schools should create dynamic inquiry form templates.
Template examples include Quick Call Entry, Walk-in Reception, Early Intake Form, Primary School Inquiry, High School Inquiry, Campaign Form, Staff Referral Form, and Public Website Form.
Each template should define sections, fields, required rules, helper text, visibility rules, consent requirements, default values, and publishing channels.
Templates should support system fields and custom fields.
System fields should map to known Student Enrollment concepts where possible.
Custom fields should remain attached to the inquiry response and should be available in lead detail views and exports.
Templates should support preview before publishing.
Templates should support draft, active, inactive, and archived states.
Templates should support versioning so historical submissions remain tied to the version used at submission time.
Public templates should require consent configuration before publishing.

## Minimum Inquiry Capture

A new inquiry should require contactability and basic admission intent.
Minimum contactability should include at least one valid primary contact method.
Primary contact method can be mobile number, WhatsApp number, email, or another enabled school contact method.
Basic admission intent should include desired academic year or admission cycle, desired class or class group, and inquiry channel or source.
Student name can be partial when the school does not know the full name at first contact.
DOB, full address, board, medium, previous school, and detailed parent data should not be required for quick inquiry creation.
More fields can become required when the inquiry moves to exam readiness or admission handoff.
The product should clearly show missing required information for the next stage without blocking initial lead capture.

## Public Inquiry Forms

Public inquiry forms should be shareable through links, school websites, QR codes, campaigns, and staff-shared links.
Each public link should identify the admission cycle, channel, form template, source, campaign, and optionally the staff member who shared it.
The public form should show school branding and the configured form fields.
The public form should support mobile-first entry.
The public form should show acknowledgement after submission.
The public form should support duplicate detection without exposing private existing data to the applicant.
The public form should capture consent text, communication preferences, privacy notice link, and submitted consent version.
The public form should not require applicant login.

## Parent And Applicant Status Links

The product should support secure status or checklist links for applicants.
The link should allow a parent to see acknowledgement, missing information checklist, exam schedule, exam instructions, and result or next-step status.
The link should not expose internal notes, staff ownership, lead score, duplicate matches, or other applicants.
The link should expire or require verification according to school security settings.
Phase one can keep the status link basic.
Phase two can expand status links with richer messaging, document checklist, and communication history.

## Duplicate Detection And Merge

The system should detect likely duplicate inquiries.
Duplicate signals should include mobile number, WhatsApp number, email, student name, parent name, DOB, desired class, academic year, and existing student records.
Duplicate detection should run for internal quick entry and public form submissions.
The system should show duplicate confidence and matched fields to authorized staff.
Staff should be able to merge duplicate inquiries when permitted.
Staff should be able to continue with a separate inquiry when permitted and with a required reason.
Merge should preserve timeline, source attribution, consent records, messages, notes, tasks, exam data, and audit history.
Public applicants should not see private duplicate match details.

## Lead Ownership And Assignment

Every active inquiry should have an owner or owner queue.
Staff should be able to assign and reassign inquiries manually.
Routing rules should support class, class group, source, channel, campaign, branch, language, workload, staff availability, and round-robin.
Reassignment should require a reason when the inquiry already has active follow-up work.
Ownership history should be visible in the inquiry timeline.
Unassigned inquiries should appear in an operational exception queue.

## Lead Score And Priority

The product should include rule-based lead scoring.
Score factors can include inquiry source, channel, target class, sibling connection, alumni connection, distance or locality, response speed, follow-up engagement, exam attendance, exam marks, and staff priority.
The score should be transparent enough that staff can understand why a lead is high or low priority.
Staff should be able to set manual priority such as Hot, Warm, Cold, or Low Priority.
Manual priority should not erase the calculated score.
Leadership analytics should show conversion by score band and priority.

## Staff CRM Workspace

The staff experience should be a CRM workspace.
The workspace should include a pipeline board, table view, lead detail panel, timeline, tasks, notes, reminders, duplicate warnings, lead score, owner, next action, and quick actions.
Staff should be able to filter by cycle, class, owner, source, channel, campaign, stage, priority, score band, stale status, and exam status.
Staff should be able to bulk assign, bulk update stage, bulk schedule follow-up, bulk archive, and bulk message where permissions allow.
The inquiry detail timeline should show creation, source, form submission, owner changes, stage changes, notes, tasks, reminders, messages, stale transitions, archive transitions, exam scheduling, attendance, marks, results, and eligibility decisions.
Quick actions should include call note, next follow-up, send template, schedule exam, reschedule exam, mark stale, archive, reopen, merge duplicate, and move stage.

## Follow-Up Tasks And SLA

The product should support follow-up tasks for each inquiry.
Tasks should have owner, due date, type, priority, status, and outcome.
Task types can include call, WhatsApp, SMS, email, document collection, campus visit, exam scheduling, exam reminder, result follow-up, and admission handoff.
SLA rules should define when first response and next follow-up are due.
The system should flag overdue tasks and missed SLA.
SLA rules should be configurable by cycle, source, channel, class, priority, and stage.
Managers should see SLA exceptions and staff workload.

## Automation Rules

Automation should support internal workflow in phase one.
Rules should trigger tasks, reminders, stage changes, stale warnings, and template suggestions.
Rules should use conditions such as source, channel, stage, score, owner, days since last contact, days since creation, exam date, attendance status, result status, and missing information.
Rules should be explainable to staff.
Rules should show last run, affected leads, and failure state.
Automation should never silently delete or permanently close a lead.
Automation changes should appear in the inquiry timeline.

## Message Templates

The product should support WhatsApp, SMS, and email templates.
Templates should include name, channel, language, category, body, variables, preview, active status, and approval status.
Template variables should include student name, parent name, school name, class, inquiry number, exam date, exam time, room, status link, owner name, and next-step instructions.
Templates should support manual use in phase one.
Templates should support triggered journeys in phase two.
Phase one should not require real outbound provider sending.
Phase one should allow internal preview, copy, and task generation based on templates.
Phase two should add generic provider dispatch, delivery logs, failure handling, opt-out handling, and compliance controls.
Phase two should not require first-party vendor SDK adapters before the school selects WhatsApp, SMS, and email vendors.

## Triggered Journeys

Triggered journeys should define when a template or task is produced.
Journey examples include inquiry acknowledgement, first follow-up reminder, no-response reminder, exam scheduled message, exam reminder, absent follow-up, pass next step, fail counselling, waitlist update, stale warning, and archive notice.
Phase one should implement journey definitions and internal task reminders.
Phase two should send outbound messages through configured WhatsApp, SMS, and email provider settings using generic webhook or manual queue dispatch.
Provider-specific SDK dispatch should be added only after vendor payload contracts are known.
Journeys should respect consent and communication preferences.
Journeys should pause when an inquiry is archived, merged, rejected, or handed to admission.

## Stale And Archive Rules

The product should support a stale state before archive.
A lead should become stale after a configured inactivity period.
The owner and manager should be notified when a lead becomes stale.
A stale lead should remain recoverable and visible in stale filters.
A lead should move to archived after a second configured threshold if there is still no activity.
Archive should require a reason when done manually.
Auto-archive should record the rule that caused the transition.
Archived leads should not be included in active workload by default.
Archived leads should remain available for historical analytics.
Archived or stale leads should be reopenable with reason and permission.

## Entrance Exam Readiness

An inquiry should become exam ready when the configured minimum exam fields are complete.
Exam readiness fields should include applicant name, contact, target class, admission cycle, and any school-required exam details.
Exam readiness should not require full admission data.
The CRM should show missing exam readiness fields clearly.
Staff should be able to collect missing fields from the parent before scheduling.
Exam readiness should support direct movement from inquiry to exam scheduling.
Registration form issue may remain an optional school-specific requirement when configured for the cycle.

## Entrance Exam Scheduling

The product should support offline entrance exam scheduling.
Exam slots should include date, time, room or hall, capacity, class or class group, admission cycle, owner, and status.
Staff should schedule eligible inquiries into slots.
The system should prevent overbooking by default.
Authorized staff can override capacity with reason when school policy allows it.
Rescheduling should preserve the previous slot, reason, and audit history.
Parents should receive schedule details through the configured message or status link flow.
Exam scheduling should update the inquiry timeline and analytics.

## Entrance Exam Attendance

The product should support attendance recording for scheduled entrance exams.
Attendance statuses should include Present, Absent, Late, Cancelled, and Rescheduled.
Absent applicants should remain follow-up candidates unless the school marks them rejected or archived.
Attendance should be available in exam analytics.
Attendance changes after result entry should require permission and reason.

## Entrance Exam Results

The product should support marks entry and result processing for offline entrance exams.
Result entry should include total marks, passing marks, obtained marks, percentage, result status, result date, and optional remarks.
The system should support pass, fail, absent, waitlist, rejected, and eligible outcomes.
The system should support merit rank when marks are available.
The system should support class-wise or exam-wise merit lists.
The system should support manual override of result-derived eligibility with permission and reason.
Every result change should be auditable.
Eligible applicants should be ready for admission handoff.

## Admission Eligibility Handoff

The flow ends when an inquiry has an entrance exam outcome and admission eligibility decision.
Eligibility handoff should preserve inquiry source, channel, campaign, form submission, consent, owner, timeline, exam schedule, attendance, marks, result, merit, waitlist, and override reasons.
The handoff should not perform full admission completion inside this PRD.
The handoff should make it clear what information is missing before admission can proceed.

## Analytics And Reporting

The product should include funnel and SLA analytics.
Analytics should cover inquiry count, source conversion, channel performance, campaign performance, stage conversion, owner workload, first response SLA, follow-up SLA, stale rate, archive rate, duplicate rate, exam scheduled rate, exam attendance rate, pass rate, waitlist rate, rejection rate, and eligibility handoff rate.
Analytics should support filters by cycle, class, source, channel, campaign, owner, stage, priority, score band, and date range.
Leadership should be able to compare sources and channels by conversion quality, not only volume.
Admissions staff should be able to see personal workload, overdue tasks, and upcoming exam actions.
Analytics should exclude archived leads from active workload views by default while keeping them available in historical reports.

## Permissions And Security

Every action must be permission-aware.
Users should only see inquiries and exam data within their tenant and allowed scope.
Permissions should control configuration, inquiry create, inquiry view, inquiry edit, owner assignment, stage changes, duplicate merge, archive, reopen, exam scheduling, capacity override, attendance changes, result entry, result override, template management, automation rule management, and analytics access.
Public forms should not expose internal data.
Status links should not expose staff notes, lead score, duplicate details, or other applicants.
Consent records and communication preferences should be protected.

## Audit And Data Integrity

The product should maintain an inquiry timeline for important operational actions.
Audited actions should include create, update, source change, channel assignment, owner assignment, stage change, duplicate merge, continue-with-duplicate reason, task completion, stale transition, archive, reopen, exam schedule, reschedule, attendance, marks entry, result change, override, and eligibility decision.
Critical changes should record actor, timestamp, previous value, new value, and reason when required.
The product should preserve historical reporting when configuration values are renamed or deactivated.
The product should avoid silent mutation of student, parent, contact, exam, consent, and eligibility data.

## Phase 1

Phase one should deliver the CRM foundation.
Phase one should include admission cycle configuration, pipeline stages, lightweight inquiry capture, dynamic form templates, inquiry channels, public forms, consent capture, duplicate detection, lead assignment rules, lead score, staff CRM workspace, tasks, reminders, stale rules, archive rules, offline exam scheduling, exam attendance, marks entry, eligibility decision, and funnel/SLA analytics.
Phase one messaging should include templates, previews, copy support, internal reminders, and triggered task generation.
Phase one should not require actual outbound WhatsApp, SMS, or email provider sending.
Phase one should keep registration form issue optional and configurable.

## Phase 2

Phase two should add outbound WhatsApp, SMS, and email sending through generic webhook or manual queue provider adapters.
Phase two should include provider configuration, delivery logs, failure handling, retry behavior, opt-out handling, consent-aware sending, richer triggered journeys, campaign analytics, advanced routing, richer applicant status links, and deeper template automation.
Phase two is release-complete when generic webhook or manual queue adapters support provider dispatch, callback reconciliation, delivery evidence, retry, and opt-out controls.
First-party WhatsApp, SMS, or email vendor SDK adapters are not required for CRM release completion.
First-party vendor SDK adapters should be planned after the target vendors and payload contracts are selected.
Phase two can add external testing-system integration if the school later wants hybrid exam support.
Phase two should still avoid building full online testing unless a separate PRD approves that scope.

## Release Readiness

The CRM is functionally complete when the product supports inquiry capture through entrance exam result, eligibility handoff, public applicant status, automation, analytics, and generic communication dispatch.
The completion definition includes lightweight CRM lead capture, dynamic public forms, consent, duplicate governance, assignment, scoring, applicant checklist progress, tasks, timeline, stale and archive automation, entrance exam scheduling, attendance, result, merit or waitlist decisions, eligibility handoff, and summary analytics.
The completion definition includes WhatsApp, SMS, and email template preview, generic webhook or manual provider dispatch, provider delivery logs, retry, callback reconciliation, and opt-out enforcement.
The completion definition excludes vendor-specific SDK adapters until the school selects the vendors and provides send and callback payload contracts.
Final browser validation should run against a local seeded DDVS environment.
The read-only CRM preflight must pass before the mutating CRM browser smoke runs.
The mutating CRM browser smoke should not run against a remote environment unless a throwaway staging tenant is explicitly approved.
The seeded local validation should use the existing DDVS tenant, seeded principal user, and seeded default password unless explicit E2E variables override them.
Restricted-user denial browser validation should run only when a CRM-denied seeded user credential is provided.
The release remains blocked if the local backend or frontend cannot start, seeded DDVS credentials fail, CRM preflight fails, mutating smoke fails, or required backend and frontend regression checks fail.

## Acceptance Scenarios

- A receptionist can create a quick inquiry with only contact details and admission intent.
- A receptionist can save an inquiry without DOB, full address, previous school, board, or medium when those fields are not known.
- A public website form captures consent, source, campaign, submitted fields, and form template version.
- A public inquiry creates a lead without exposing duplicate match details to the applicant.
- A duplicate warning appears for authorized staff before save or during review.
- An authorized user can merge duplicate inquiries while preserving timeline and attribution.
- An authorized user can continue with a separate duplicate inquiry only after entering a reason.
- A new website inquiry is assigned automatically according to routing rules.
- A manager can see unassigned inquiries as operational exceptions.
- A lead becomes stale after the configured inactivity threshold.
- A stale lead notifies the owner and manager.
- A stale lead archives after the configured second threshold.
- A stale or archived lead can be reopened with reason and permission.
- A counselor can schedule an entrance exam directly from an inquiry after exam readiness is complete.
- Exam capacity prevents overbooking by default.
- An authorized user can override exam capacity with reason when policy allows it.
- Staff can reschedule an applicant and preserve the previous slot history.
- Staff can mark exam attendance and later enter marks.
- Marks produce pass, fail, merit rank, waitlist, rejection, or eligibility status according to rules.
- An authorized user can override eligibility with reason.
- Funnel analytics show inquiry source conversion, SLA misses, exam attendance, pass rates, and eligibility handoff.
- Users without permission cannot merge duplicates, override exam decisions, change automation rules, or view another tenant's leads.

## Public Concepts

| Concept | Definition |
|---|---|
| Admission Cycle | A school admission season for an academic year or intake. |
| Inquiry Channel | The way an inquiry arrived, such as website, WhatsApp, call, walk-in, QR code, or staff link. |
| Inquiry Source | The business source label used for reporting, such as sibling reference, campaign, website, or walk-in. |
| Campaign | A marketing or admission effort that produces inquiries. |
| Form Template | A configurable inquiry form definition used for public or internal capture. |
| Form Field | A system or custom field collected through a form template. |
| Inquiry Lead | A prospective student admission inquiry before final admission. |
| Pipeline Stage | A configured operational stage around fixed system milestones. |
| Lead Owner | The staff member or queue responsible for next action. |
| Lead Score | A rule-based score that helps prioritize follow-up. |
| Follow-up Task | A dated staff action required to progress an inquiry. |
| Message Template | A reusable WhatsApp, SMS, or email message definition. |
| Automation Rule | A configured rule that creates tasks, reminders, stage changes, or journeys. |
| Stale Rule | A rule that marks inactive inquiries as stale after a threshold. |
| Archive Rule | A rule that archives stale inquiries after a second threshold. |
| Duplicate Match | A possible duplicate inquiry or student record found through matching signals. |
| Consent Record | The submitted communication consent text, preferences, and version. |
| Entrance Exam Slot | A scheduled offline exam time, room, capacity, and eligible class group. |
| Entrance Exam Result | The attendance, marks, result status, and result date for an applicant. |
| Merit Decision | A rank or outcome derived from exam performance and school rules. |
| Waitlist Decision | A decision that keeps an applicant eligible but not immediately selected. |
| Eligibility Decision | The final inquiry-to-admission handoff outcome after exam review. |

## Deferred Product Questions

- Should schools be able to configure different exam readiness fields for different classes.
- Should staff workload routing consider working hours and leave calendars in phase one or phase two.
- Which first-party WhatsApp, SMS, and email vendor SDK adapters should be added after provider selection.
