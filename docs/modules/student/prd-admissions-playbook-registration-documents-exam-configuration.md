---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Admissions playbook, registration, documents, and exam configuration

TL;DR: Build a configurable Student Enrollment admissions operating system that carries an applicant from entrance-exam preparation through registration readiness and an auditable admission handoff.
This document defines future functional product behavior only.
It intentionally avoids API contracts, database schemas, migrations, and frontend implementation details.

## Product intent

Indian schools need to run different admission processes by academic year, campus, class, board, medium, applicant category, and admission round.
The product should let a school define those processes once as an admissions playbook and operate every applicant against the correct version of that playbook.
The product should replace disconnected exam-slot, registration-form, and document screens with one coordinated admissions workflow.
The product should make the parent experience simple on a mobile phone while giving staff strong controls, queues, audit history, and operational analytics.
The product should support schools with simple admission processes without forcing advanced configuration.
The product should also support multi-campus and high-volume schools without relying on spreadsheets or staff memory.

## Enrollment suite relationships

The [Student Enrollment Master Blueprint](./prd-student-enrollment-master-blueprint.md) defines the complete lifecycle, common terminology, and capability ownership.
The [Inquiry to Entrance Exam Admissions CRM PRD](./prd-inquiry-entrance-exam-crm.md) owns inquiry capture, qualification, follow-up, exam readiness, exam outcomes, and eligibility decisions.
This PRD begins with admission playbook configuration and expands the journey from exam preparation through registration readiness and admission handoff.
The two PRDs share admission cycle, applicant, entrance exam, communication, status-link, timeline, permission, and analytics concepts.
Where the two documents overlap, the inquiry CRM owns lead-management behavior and this PRD owns registration, document, and advanced exam-configuration behavior.
The [Indian Admission Policy Packs PRD](./prd-indian-admission-policy-packs.md) supplies sourced board and jurisdiction defaults used by playbooks.
The [Final Admission, Offers, and Seat Allocation PRD](./prd-final-admission-offers-seat-allocation.md) consumes this PRD's readiness handoff and begins the final-admission journey.

## Current development baseline

The current Student Enrollment module supports inquiry, registration-form issue, registration data collection, document-type setup, entrance-exam slots, exam scheduling, marks, and result status.
The current registration experience uses fixed sections for general details, documents, family information, communication, additional details, medical records, previous school, and bank details.
The current registration-form issue behavior records a form number and amount.
The current entrance-exam behavior supports scheduled slot information, marks, passing marks, and status.
The current CRM foundation can issue a registration form and can configure whether registration is required before an entrance exam.
Registration Form Amount and Form Number Series surfaces exist in development but are not yet a complete configurable admissions policy.
Document types are configurable, but class-specific checklists, conditional requirements, verification queues, correction cycles, waivers, and readiness blocking are not yet the target experience.
These existing surfaces remain compatibility baselines while the future product is introduced.

## Scope

This PRD covers admission playbooks, admission rounds, seat planning, entrance-exam configuration, registration templates, registration issue and link lifecycle, document requirements, document verification, applicant status, staff operations, automation, communication, analytics, and admission-readiness handoff.
This PRD includes offline entrance-exam operations and configurable result governance.
This PRD includes registration-fee configuration and payment-reference handoff but does not post financial transactions.
This PRD ends when the applicant has an admission-readiness decision and a complete handoff package for the admission workflow.

## Non-goals

- Do not create the final admitted Student record in this PRD.
- Do not create fee ledgers, receipts, refunds, or accounting entries in this PRD.
- Do not allocate the admitted student to a class or division in this PRD.
- Do not define the long-term student lifecycle in this PRD.
- Do not build a full parent login portal in this PRD.
- Do not build an online examination engine, question bank, auto-grading engine, or proctoring system in this PRD.
- Do not hard-code one admission process for all schools.
- Do not define APIs, DTOs, database tables, migrations, or component architecture in this PRD.

## Goals

- Let a school model its complete pre-admission process without custom development.
- Make entrance-exam scheduling reliable at both low and high applicant volume.
- Make registration forms configurable while preserving data quality and historical meaning.
- Make document collection and verification measurable, auditable, and parent-friendly.
- Reduce manual follow-up through transparent automation and exception queues.
- Prevent applicants from becoming admission ready while blocking requirements remain unresolved.
- Give leadership accurate conversion, capacity, completion, verification, and yield analytics.
- Preserve tenant isolation, permissions, auditability, and compatibility with existing Student Enrollment behavior.

## Primary users

- Admission administrator configures playbooks, rounds, templates, rules, and permissions.
- Admission coordinator monitors capacity, queues, SLAs, exceptions, and applicant progress.
- Counselor guides applicants, schedules exams, issues registration, and resolves pending actions.
- Receptionist performs quick operational actions within limited permissions.
- Examiner or invigilator manages attendance and permitted exam actions.
- Document verifier reviews evidence, requests corrections, and records original-seen status.
- Principal or school leadership monitors policy compliance, funnel performance, and admission yield.
- Finance staff reviews registration-fee references and owns any downstream financial transaction.
- Parent or applicant completes registration, uploads documents, checks status, and responds to corrections.
- System administrator manages tenant-level access, security, and supported communication dispatch.

## Product principles

- Configure once and operate by exception.
- Use progressive disclosure so simple schools see a simple workflow.
- Preserve the exact playbook and template version applied to each applicant.
- Prefill known information and never silently overwrite verified information.
- Make every deadline, requirement, decision, and next action visible.
- Require permission and reason for consequential overrides.
- Keep parent-facing language clear, localizable, and mobile-friendly.
- Keep internal notes, scores, duplicate evidence, and staff decisions out of parent-facing surfaces.
- Treat school policy as configuration while preserving common reporting milestones.
- Make automation explainable and reversible by authorized staff.
- Derive analytics from auditable operational events.

## End-to-end journey

1. A school creates a reusable admissions playbook for an academic intake.
2. The school copies the playbook into an admission cycle and applies cycle-specific overrides.
3. The school defines rounds, seat targets, exam policies, registration templates, document checklists, and communication rules.
4. An inquiry becomes exam ready or follows an approved registration-before-exam path.
5. The system or an authorized staff member assigns an appropriate exam slot.
6. The applicant receives schedule details and completes any required pre-exam actions.
7. Staff records attendance, marks, result, merit, waitlist, or eligibility according to configured rules.
8. A configured milestone issues the correct registration template and secure link.
9. The applicant reviews prefilled information, completes required fields, uploads documents, and submits the form.
10. Staff verifies submitted information and documents, requests corrections, or records justified waivers.
11. The system evaluates exam, registration, document, fee-reference, consent, and policy requirements.
12. An authorized staff member confirms admission readiness or records an override with reason.
13. The system creates an auditable handoff package for the downstream admission workflow.

## Admissions playbook

An Admission Playbook should define the school-approved pre-admission process for a group of applicants.
A school should be able to maintain reusable defaults and copy them into each admission cycle.
Changes to school-level defaults should not silently change an active cycle.
Authorized users should be able to apply selected default changes to a cycle after reviewing their impact.
Each applicant should retain the playbook version that governed the applicant when the journey started.

### Playbook applicability

The playbook should support applicability by academic year, admission cycle, campus, branch, class, board, medium, admission round, applicant category, and admission route.
The product should identify conflicts when more than one active playbook has the same applicability.
The product should display which playbook and version applies before staff performs a consequential action.
The product should provide a controlled fallback playbook when no more specific playbook applies.

### Playbook configuration

The playbook should configure required milestones, optional milestones, stage ordering, entry criteria, exit criteria, responsible role, SLA, applicant-visible label, and allowed transitions.
Common milestones should include inquiry, exam ready, exam scheduled, exam completed, registration invited, registration in progress, documents pending, verification pending, admission ready, waitlisted, rejected, withdrawn, and expired.
Schools should be able to add operational stages around fixed reporting milestones.
Custom stages should map to a fixed reporting milestone so cross-school and historical reporting remains coherent.
The playbook should support registration before exam, registration after exam, exam-waived, and direct-registration paths.
The playbook should define which steps are parent actions, staff actions, automated actions, or approvals.
The playbook should define deadlines relative to a milestone or as fixed dates.
The playbook should define the action taken when a deadline is missed.
Publishing should validate that the playbook has no unreachable stage, missing required milestone, or circular progression.

### Admission rounds and seat plans

Schools should be able to configure multiple admission rounds such as early intake, regular intake, late intake, sibling priority, transfer intake, or rolling admission.
Each round should have an opening date, closing date, applicable applicant groups, exam windows, registration deadline, and decision window.
Seat plans should support targets and approved capacity by cycle, campus, class, board, medium, round, and school-defined category.
The product should distinguish planned seats, offered seats, accepted seats, waitlisted applicants, withdrawn applicants, and remaining capacity.
Seat plans should warn about over-allocation without creating final admissions or class allocations.
Category rules should remain school-configurable and should not encode jurisdiction-specific legal assumptions.
Leadership should be able to compare demand and yield against the seat plan.

## Entrance exam configuration

Entrance-exam configuration should be reusable across dates and admission cycles.
The product should support schools that use one common assessment and schools that use class-specific or subject-specific assessments.

### Exam definitions and components

An exam definition should include name, applicable cycle, class, campus, board, medium, round, assessment mode, instructions, duration, and active dates.
Offline assessment should be the primary supported mode.
An exam may contain one or more subject or assessment components.
Each component should define maximum marks, minimum marks when applicable, weight, whether it is mandatory, and the permitted score precision.
The product should support interview, interaction, observation, readiness, and written-test components without treating all components as academic marks.
The product should support approved accommodations and internal notes without exposing sensitive notes to other applicants.

### Slot templates and calendar

Schools should be able to create reusable slot templates with reporting time, start time, end time, room or hall, campus, capacity, invigilator requirement, and applicable applicant group.
Templates should generate operational slots for a selected date range without changing historical slots.
The calendar should support holidays, blackout dates, unavailable rooms, invigilator constraints, and school-specific closure periods.
The product should warn when rooms, invigilators, or time windows conflict.
Staff should be able to close, reopen, cancel, or replace a slot with permission.
Closing or cancelling a slot with assigned applicants should require a reassignment or communication plan.

### Slot assignment rules

Schools should be able to assign slots manually or through configurable rules.
Supported strategies should include earliest available, round robin, class match, campus match, preferred-date match, language match, sibling grouping, and balanced capacity.
Rules should support triggers such as inquiry becomes interested, inquiry becomes exam ready, registration reaches a configured checkpoint, or staff approves scheduling.
The system should explain which rule selected a slot.
The system should not assign a slot when mandatory readiness information is missing.
The system should place unassignable applicants into an exception queue with the blocking reason.
The system should prevent overbooking by default.
An authorized capacity override should require a reason and should remain visible in capacity reporting.

### Applicant scheduling experience

Phase one should allow staff-managed scheduling and rescheduling.
Phase two should allow configured applicant self-scheduling from eligible slots.
Self-scheduling should show only slots valid for the applicant's playbook and remaining capacity.
A temporary hold should prevent the same final seat from being promised to multiple applicants during confirmation.
The applicant should receive reporting time, venue, map or directions when configured, required documents, preparation instructions, and contact information.
The applicant should be able to request rescheduling without directly bypassing school policy.

### Hall ticket, check-in, and attendance

The product should generate a hall-ticket reference after scheduling when the school enables hall tickets.
The hall ticket should show applicant identity, exam details, instructions, and a QR or check-in reference.
Staff should be able to check in an applicant by search, reference, or QR scan.
Attendance states should include scheduled, checked in, present, absent, late, excused, cancelled, and rescheduled.
No-show and late-arrival behavior should be configurable by playbook.
Attendance changes after result publication should require elevated permission and reason.

### Result rules and governance

Schools should configure component-level minimums, aggregate minimums, weighted scores, grades, pass/fail criteria, merit rules, waitlist rules, and eligibility mapping.
Mandatory-component failure should be able to fail or refer an applicant even when the aggregate threshold is met.
Tie-breakers should be ordered and explicit.
Tie-breakers may use configured exam components, interaction outcomes, submission time, or another school-approved criterion.
Sensitive or legally restricted attributes should not be used as hidden tie-breakers.
The system should preview calculated outcomes before publication.
Draft results should remain internal.
Published results should be immutable except through an authorized correction or override workflow.
Every override should record the previous outcome, new outcome, actor, reason, and timestamp.
The system should support pass, fail, referred, merit-listed, waitlisted, rejected, exam-waived, and eligible outcomes.
Waitlist movement should preserve rank history and decision history.

## Registration form builder

Schools should be able to create registration templates for different applicant journeys.
Templates should support applicability by cycle, campus, class, board, medium, category, round, and admission route.
The product should detect ambiguous template applicability before publication.

### Template sections

Supported sections should include student details, family and guardians, communication and address, medical information, previous school, bank details, additional information, declarations, consent, and documents.
Administrators should be able to reorder sections and hide unused sections.
Administrators should be able to add school-defined sections without changing system-owned reporting fields.
Long forms should show progress, save state, and remaining required actions.

### Field types and behavior

Supported field types should include short text, long text, number, currency, date, time, date-time, email, phone, single select, multi-select, radio, checkbox, yes/no, address, file upload, declaration, consent, and read-only information.
A field should be either a system field or a custom response field.
System fields should map to supported Student Enrollment information where possible.
Custom responses should remain attached to the registration submission and handoff package.
Each field should support label, help text, placeholder, required status, default value, options, validation, visibility, editability, and applicant-facing sensitivity guidance.
Conditional visibility and required rules should use prior responses and applicant context.
Publishing should reject rules that reference unavailable fields or create circular dependencies.
Field labels, help text, declarations, and consent text should support school-configured languages.

### Prefill and verified information

The form should prefill reliable information from inquiry, applicant profile, exam, and previously verified registration data.
The applicant should be able to correct unverified information.
Changing verified or identity-critical information should create a review item instead of silently replacing the verified value.
The form should show the source of locked information in parent-friendly language when appropriate.
Staff should be able to compare submitted changes with existing values during verification.

### Template lifecycle and versioning

Template states should include draft, published, retired, and superseded.
Only published templates should be issued to new applicants.
Publishing should create an immutable version for existing issues and submissions.
Editing a published template should create a new draft version.
Existing applicants should remain on their issued version unless an authorized migration action is performed.
Migration to another version should show newly required fields, removed fields, and validation impact before confirmation.
Retiring a template should not invalidate existing active registration links.
The product should support preview by applicant context before publication.

## Registration issue and secure link lifecycle

Registration should be issuable manually or automatically at a configured playbook milestone.
Automatic triggers may include exam pass, eligibility decision, merit selection, waitlist promotion, staff approval, or another configured milestone.
An issue action should select the applicable template, form-number rule, fee rule, expiry rule, communication rule, and applicant language.
If applicability is ambiguous or configuration is incomplete, the system should stop automation and create an exception instead of guessing.

### Form number series

Schools should be able to configure form-number series by tenant, cycle, campus, class, round, or another approved scope.
A series should support a prefix, numeric sequence, padding, optional date or cycle tokens, starting number, and reset policy.
The system should preview sample numbers before activation.
Issued form numbers should remain unique within the configured school scope.
Changing a series should not renumber issued forms.
Manual form numbers should require permission and should be checked for uniqueness.
Cancelled, expired, or revoked numbers should remain reserved for audit purposes unless school policy explicitly allows reuse.

### Registration fee rule

A registration fee rule should support amount, currency, applicability, waiver permission, validity dates, and payment-required milestone.
The product should record a payment reference and payment status supplied by the Finance or payment workflow.
This PRD should not create accounting entries, receipts, settlements, refunds, or ledger balances.
A waived or adjusted fee should require permission, reason, and audit history.
An applicant should not be shown as payment complete solely because a staff note exists.

### Link security and states

The parent experience should use a secure applicant link with OTP verification for sensitive information.
Links should be revocable, time-limited, rate-limited, and bound to the intended applicant context.
The product should not reveal whether unrelated applicant records exist during OTP or link validation.
Registration states should include issued, delivered, opened, started, partially completed, submitted, returned for correction, resubmitted, verified, expired, revoked, withdrawn, and admission ready.
Every state should have a clear parent-facing meaning and internal operational meaning.
The product should record issue, delivery, open, submission, correction, verification, expiry, extension, revocation, and reissue events.

### Expiry, resend, extension, and reissue

Expiry may be a fixed deadline or a duration from issue.
The product should warn the applicant and owner before expiry according to configured rules.
Resending should preserve the current issue unless policy requires a new secure token.
Extending should preserve the form number, template version, and submission history.
Revoking should immediately block further use while preserving evidence.
Reissuing should create a new controlled issue while linking the prior issue for audit.
An expired applicant with saved data should retain that data for authorized review and possible extension.

### Partial save and submission

The form should save progress without requiring every section to be complete.
The product should show section-level completion and unresolved validation.
Final submission should validate all currently applicable required fields, declarations, consent, and blocking uploads.
Conditional rules should be reevaluated before final submission.
The applicant should receive a submission acknowledgement and reference.
Staff should be able to return selected fields or documents for correction without reopening unrelated verified sections.

## Document setup and verification

Document configuration should convert generic document types into applicant-specific checklists.
The product should support Indian-school admission documents while allowing schools to add their own document types.
Common types should include birth certificate, Aadhaar, applicant photograph, parent or guardian identity, address proof, transfer certificate, previous report card, caste certificate, income certificate, migration certificate, medical certificate, disability or accommodation evidence, and sibling or alumni proof.
Schools should decide which types are appropriate and lawful for each applicant context.

### Checklist templates and applicability

Schools should configure checklist templates by cycle, campus, class, board, medium, category, round, and admission route.
Each requirement should be required, optional, conditional, required later, or informational.
Conditional requirements should use configured applicant attributes or form responses.
The system should identify conflicting checklist applicability before publication.
An applicant should see one resolved checklist with an explanation for each required item.
Changes to a published checklist should be versioned and should not silently change submitted applications.

### Requirement configuration

Each requirement should define accepted file types, maximum size, maximum page count when relevant, upload instructions, sample guidance, validity-date requirement, expiry handling, original-seen requirement, and whether multiple files are allowed.
Requirements should support deadlines relative to registration issue, exam, admission decision, or a fixed date.
The product should distinguish a document required for form submission from a document required before admission readiness.
The product should allow a temporary undertaking when the school permits later submission.
An undertaking should include a due date, responsible party, and consequence of non-compliance.

### Verification workflow

Document states should include missing, uploaded, under review, verified, rejected, expired, waived, original seen, and superseded.
Uploading a replacement should preserve the prior version and verification history.
A verifier should be able to approve, reject, request a clearer copy, request a different document, record validity dates, and mark an original as seen.
Rejection should require a parent-facing reason and may include an internal note.
Waiver should require permission, reason, approving authority, and optional expiry.
The product should support reviewer assignment, queue ownership, due dates, and escalation.
Bulk verification should be limited to low-risk actions and should never hide which staff member made each decision.

### Document safety and privacy

Applicants should be told why sensitive documents are requested.
Staff access should follow least privilege and tenant isolation.
Parent-facing links should prevent access to internal notes and other applicants' files.
Downloads, verification decisions, waivers, and original-seen actions should be auditable.
The product should warn users before sending sensitive documents through communication channels that are not approved for document delivery.
Retention and deletion behavior should follow school policy and applicable law without silently removing audit evidence.

### Assisted document processing

Phase two may provide document quality checks, duplicate-file detection, expiry extraction, and assisted field extraction.
Assisted extraction should present suggestions for human confirmation.
The product should not treat extracted identity information as verified without an authorized review.
Low-confidence or conflicting extraction should create a review item.
The product should display the source document and suggested value together during confirmation.

## Admission readiness and handoff

The product should calculate admission readiness from the applicant's playbook and resolved requirements.
Readiness categories should include exam, registration, documents, consent and declarations, registration-fee reference when applicable, approvals, and unresolved exceptions.
The readiness checklist should show complete, pending, failed, waived, not applicable, and overridden states.
A blocking requirement should prevent normal admission-ready confirmation.
An authorized user may override a blocking requirement only with a reason and, when configured, a second approval.
The handoff package should preserve applicant identity, inquiry attribution, playbook and template versions, exam history, result and override history, registration submission, custom responses, document decisions, consent, communication history, fee reference, readiness evidence, and unresolved undertakings.
The handoff should identify which values are verified, applicant supplied, staff supplied, or derived.
This PRD should not create the final Student admission, finance account, or class allocation.

## Applicant experience

The applicant surface should work on commonly used mobile devices and low-bandwidth connections.
The applicant should see registration progress, exam schedule, hall ticket when enabled, result or next step, document checklist, correction requests, deadlines, fee-reference status, and school contact information.
The surface should show only information approved for parent or applicant visibility.
The applicant should be able to save progress, resume securely, upload replacements, acknowledge declarations, and submit corrections.
Messages and status labels should be localizable.
The applicant should receive clear success, validation, expiry, unavailable, and support states.
The product should avoid requiring a full parent account solely to complete pre-admission tasks.

## Staff admissions workspace

The staff workspace should organize work around queues and next actions instead of requiring staff to search multiple registers.
Queues should include unassigned exams, scheduling exceptions, capacity conflicts, upcoming exams, no-shows, results pending, registration not opened, incomplete registration, expiring links, expired links, payment-reference exceptions, documents missing, documents under review, documents rejected, verification overdue, admission-ready review, waitlist action, and unresolved overrides.
Each queue should support appropriate cycle, campus, class, round, owner, age, status, and deadline filters.
Staff should see applicant identity, current milestone, owner, next action, deadline, blocking reason, and relevant quick actions.
Bulk actions should be permission controlled and should preview affected applicants before confirmation.
The unified timeline should include inquiry CRM events, exam events, registration events, document events, communications, automation, decisions, and overrides.
Timeline entries should distinguish staff actions, applicant actions, automation, and external-provider events.

## Automation and communication

Automation should support registration dispatch, exam assignment, exam reminders, no-show follow-up, incomplete-form reminders, document reminders, correction reminders, expiry warnings, verification escalation, waitlist promotion tasks, and admission-readiness notifications.
Rules should define trigger, conditions, action, timing, quiet hours, retry policy, owner, and failure behavior.
Automation should create an exception or staff task when it cannot safely complete an action.
Staff should be able to see why a rule did or did not run for an applicant.
Communication should reuse approved WhatsApp, SMS, and email templates from the admissions CRM.
Generic webhook and manual queue dispatch should remain the supported provider baseline.
Provider-specific SDK adapters should remain future extension work after providers and payload contracts are selected.
Messages should respect consent, communication preference, opt-out, applicant language, and approved sending windows.
Sensitive information should be shared through the secure status link rather than embedded directly in ordinary messages.

## Notifications and escalation

Applicant reminders should stop when the requested action is completed, withdrawn, revoked, or no longer applicable.
Staff escalation should support owner, team lead, admissions coordinator, and school-defined escalation roles.
Repeated reminders should be capped to prevent harassment and operational noise.
Failed delivery should remain visible and should create a staff action when the message is important to a deadline.
The product should avoid marking an applicant unresponsive solely because one communication provider failed.

## Analytics and reporting

Analytics should be tenant-aware, scope-aware, cycle-filterable, class-filterable, and reproducible from audited events.
Operational dashboards should show current workload and exceptions.
Leadership dashboards should show trends, conversion, capacity, policy performance, and yield.

### Playbook and funnel analytics

- Applicants entering and completing each playbook milestone.
- Conversion and drop-off between milestones.
- Stage aging and SLA compliance.
- Registration-before-exam compared with registration-after-exam outcomes.
- Round, campus, class, board, medium, category, and route performance.
- Withdrawal and expiry reasons.

### Exam analytics

- Slot capacity, booked capacity, available capacity, and authorized overbooking.
- Scheduling-rule success and exception rates.
- Reschedule, cancellation, late-arrival, and no-show rates.
- Attendance, component performance, pass rate, referral rate, merit distribution, and waitlist movement.
- Result-publication turnaround and override rate.

### Registration analytics

- Forms issued, delivered, opened, started, partially completed, submitted, corrected, verified, expired, revoked, and withdrawn.
- Time to first open, time to submit, section-level abandonment, and correction cycles.
- Template version performance and validation failure patterns.
- Registration-fee reference, waiver, and exception counts without duplicating Finance reporting.

### Document analytics

- Checklist completion, missing-document rate, verification backlog, verifier workload, and turnaround time.
- Rejection, replacement, expiry, waiver, undertaking, and original-seen rates.
- Common rejection reasons and requirements causing admission-readiness delays.
- Assisted-extraction acceptance and correction rates when that capability is enabled.

### Yield and capacity analytics

- Eligible, registration invited, registration completed, admission ready, waitlisted, rejected, withdrawn, and handed-off counts.
- Seat-plan demand, offers, acceptance indicators, waitlist movement, and remaining planned capacity.
- Source, campaign, round, class, and campus yield through the handoff milestone.
- Forecasts should be labelled as forecasts and should show the underlying period and assumptions.

## Permissions and security

Every staff action should be permission aware and tenant isolated.
Permissions should separately control playbook configuration, playbook publication, seat-plan access, exam configuration, slot assignment, capacity override, attendance, marks, result publication, result override, registration-template management, registration issue, form-number override, fee waiver, link extension, link revocation, document configuration, document review, document waiver, original-seen confirmation, admission-readiness confirmation, readiness override, bulk actions, applicant communications, and analytics.
User scope should limit applicants, campuses, classes, and queues where applicable.
Backend authorization should enforce all permissions even when a frontend control is hidden.
Public links should reveal no cross-applicant or cross-tenant information.
OTP and public-link errors should avoid account and applicant enumeration.
Sensitive exports should require explicit permission and should be auditable.

## Audit and data integrity

Critical actions should record actor, timestamp, applicant, previous value, new value, source, and reason when required.
Audited actions should include playbook publication, template publication, rule activation, slot generation, slot assignment, capacity override, attendance change, result publication, result override, registration issue, resend, extension, revocation, reissue, submission, correction request, field verification, document decision, waiver, original-seen confirmation, readiness decision, and handoff.
Historical applicants should retain human-readable labels when referenced configuration is renamed or deactivated.
Published policies, templates, and checklists should be versioned rather than silently mutated.
Multi-step actions that affect capacity, applicant state, and timeline should not leave partial results.
The system should make failed automation visible without falsely recording the intended business outcome as complete.

## Accessibility, localization, and usability

Applicant and staff surfaces should support keyboard access, screen-reader labels, clear focus behavior, and understandable validation messages.
Color should not be the only indicator of status or urgency.
Dates, times, numbers, phone formats, currency, and language should respect school configuration and applicant context.
Indian names and addresses should not be forced into assumptions designed for another country.
The product should support applicants without email when a verified phone contact is available.
Long-running uploads and submissions should show progress and safe retry behavior.
Empty, loading, permission-denied, expired, offline, and error states should provide a clear next action.

## Public concepts

| Concept | Definition |
|---|---|
| Admission Playbook | A versioned school policy that defines the pre-admission journey for an applicant group. |
| Admission Round | A dated intake window with its own applicability, deadlines, exams, and decision period. |
| Seat Plan | Planned admission capacity for a configured cycle and applicant grouping. |
| Registration Path | The playbook route that determines when registration and examination occur. |
| Entrance Exam Definition | The configured assessment, components, applicability, and result policy. |
| Entrance Exam Slot Template | A reusable definition for generating operational exam slots. |
| Auto Slot Assignment Rule | A rule that selects an eligible slot for an applicant. |
| Hall Ticket | Applicant-facing exam identity, schedule, venue, instructions, and check-in reference. |
| Exam Attendance | The applicant's operational attendance state for a scheduled assessment. |
| Exam Result Rule | The configured calculation and decision criteria for an assessment. |
| Merit Rule | The ranking and tie-break policy for eligible applicants. |
| Waitlist Rule | The policy for creating, ordering, promoting, and closing a waitlist. |
| Registration Form Template | A versioned definition of registration sections, fields, validation, and consent. |
| Registration Form Link | A secure, time-limited applicant access path to an issued registration form. |
| Form Number Series | A controlled numbering policy for registration issues. |
| Registration Fee Rule | The configured registration amount, applicability, waiver, and payment milestone. |
| Registration Status | The operational state of an issued registration form. |
| Document Checklist Template | A versioned set of document requirements for an applicant group. |
| Document Requirement | A configured evidence item with applicability, deadline, and verification rules. |
| Document Verification | The review decision and history for an uploaded requirement. |
| Document Waiver | An authorized exception that removes or postpones a document requirement. |
| Applicant Status Link | The OTP-protected surface for applicant tasks and status. |
| Admission Readiness Checklist | The resolved set of requirements that must be satisfied before handoff. |
| Admission Handoff Package | The verified and audited pre-admission context passed to the admission workflow. |
| Override Reason | The mandatory explanation for an authorized departure from policy. |
| Admissions Operations Queue | A filtered staff worklist for actions, deadlines, and exceptions. |

## Phase 1

Phase one should deliver playbook and admission-round configuration, exam definitions and slot rules, staff-managed scheduling, attendance, result governance, registration templates, form-number series, issue rules, secure links, partial save, CRM prefill, registration-fee references, document checklists, verification, correction cycles, admission-readiness checks, staff queues, timeline, permissions, audit, and core analytics.
Phase one should preserve compatibility with existing inquiry, registration form, registration, document type, and entrance-exam behavior.
Phase one communication should use existing templates and generic webhook or manual queue dispatch.

## Phase 2

Phase two should add applicant self-scheduling, richer payment handoff, advanced communication journeys, provider delivery evidence, assisted document quality and extraction, configurable second approvals, richer forecasting, and advanced yield analytics.
Phase two should treat extracted information as suggestions requiring human confirmation.
Phase two should not require first-party communication vendor SDKs until vendors are selected.

## Phase 3 boundary

The [Student Enrollment Master Blueprint](./prd-student-enrollment-master-blueprint.md) and its downstream companion PRDs define final offers, seats, financial conditions, household resolution, Student conversion, academic placement, access, and activation repair.
This PRD supplies the admission-readiness decision and handoff package consumed by those workflows.
Finance-account creation, fee-ledger posting, and long-term student lifecycle behavior remain outside this PRD suite.

## Acceptance scenarios

- A school can use one simple playbook for all applicants or different playbooks by cycle, campus, class, board, medium, round, category, or route.
- Publishing detects conflicting applicability and invalid stage progression before applicants are affected.
- A school can configure registration before exam for one applicant group and after exam for another.
- A counselor can assign an eligible applicant to the earliest valid slot through an explainable rule.
- An unassignable applicant appears in an exception queue with a specific blocking reason.
- Capacity prevents overbooking unless an authorized user records an override reason.
- Staff can reschedule an applicant without losing prior slot and communication history.
- Staff can check in an applicant and record attendance without gaining permission to change results.
- Configured component minimums, aggregate thresholds, weights, tie-breakers, merit, and waitlist rules produce a previewable result.
- A published result cannot be changed without elevated permission, reason, and audit history.
- An eligible applicant automatically receives the correct registration template when the configured milestone is reached.
- An applicant receives prefilled CRM and exam information without verified values being silently overwritten.
- A parent can save a partially complete form and resume securely before expiry.
- Staff can resend, extend, revoke, or reissue a registration link without losing history.
- Issued form numbers remain unique and are not changed when the numbering policy changes.
- A registration template change creates a new version and does not alter existing submissions.
- Document requirements resolve correctly for the applicant's class, campus, board, category, round, and route.
- A verifier can reject a document with a parent-facing reason and the parent can upload a replacement.
- A waiver requires permission and reason and remains visible in readiness and audit views.
- A document required later can use an undertaking without incorrectly appearing verified.
- Admission readiness is blocked while mandatory requirements remain unresolved.
- An authorized readiness override records the unresolved requirement, actor, reason, and approval evidence.
- The handoff package distinguishes verified, applicant-supplied, staff-supplied, and derived information.
- Applicant reminders stop when the requested action is completed or no longer applicable.
- Analytics reproduce playbook conversion, exam capacity, registration completion, document verification, waitlist movement, and handoff yield from audited events.
- Users without permission cannot publish configuration, override exam or readiness decisions, waive documents, view restricted applicants, or access another tenant's data.

## Release acceptance

The PRD is complete when product, engineering, design, QA, and school operations can derive the same pre-admission workflow and scope boundaries from this document.
Implementation is not complete merely because individual settings screens exist.
Release readiness requires the configured playbook to work end to end for exam, registration, documents, readiness, and handoff without depending on undocumented manual steps.
Mutating browser validation should use a seeded local tenant or an explicitly approved throwaway staging tenant.
Permission-denial and tenant-isolation checks should be included for all consequential operations.

## Documentation validation

Run these commands from the `Shikshamitra-BE` repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```
