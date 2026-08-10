---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-28
owner: backend-team
audience: humans-and-agents
---

# PRD: Student Enrollment operating workflow

TL;DR: Student Enrollment should be a guided admission workflow for Indian schools where every applicant enters Pipeline, follows a clear stepper-style path, skips only the steps allowed by school rules, and becomes an active Student only after Finance, placement, and approval gates are satisfied.

## Product intent

Student Enrollment should help schools run admissions without spreadsheets, repeated phone calls, or disconnected inquiry, exam, registration, fee, and student-creation screens.
The production baseline targets CBSE and Gujarat Board schools, including private unaided, aided, trust-managed, and practical mixed-mode schools.
The product should feel like a school admissions desk, not a policy-engine configuration project.
Schools should be able to start with defaults, then tune standards, routes, exam rules, registration documents, message templates, owners, and activation gates.
The product should give every school an Admission Year Setup attached to the academic year and every applicant an Applicant Stepper that shows current step, completed steps, skipped steps, blocked steps, and next action.

## Operating principles

- Pipeline is the source of truth for every applicant before admission.
- Admission Year Setup defines the school-approved path for an academic year.
- Applicant Stepper applies that setup to one applicant.
- The core lifecycle is fixed enough for support, reporting, and staff training.
- Schools can skip optional steps by rule, but they should not invent unrelated lifecycle stages.
- Academic year is the main school-facing admission context.
- The word cycle may remain internal where the code already uses it, but user-facing docs and screens should use Admission Year Setup.
- Student Enrollment owns applicant progression and activation orchestration.
- Finance owns fee posting, receipts, reconciliation, settlement, and refunds.
- Institute Setup and Class Management own board, medium, standard, division, and capacity structure.
- Student Records owns the active student lifecycle after activation.
- Parents must receive clear, timely messages at important admission moments.
- Sensitive identity, family, document, and financial information must be least-privileged and audit-friendly.

## Primary users

| User | Main need |
|---|---|
| Receptionist or counselor | Capture inquiries quickly, assign owner, follow up, and invite families for the next step |
| Admission coordinator | Monitor Pipeline, schedule exams, move eligible applicants, and keep parents informed |
| Exam coordinator | Maintain exam schedules, enter results, and decide eligibility from configured rules |
| Registration clerk | Collect family, student, previous-school, document, and consent information |
| Finance staff | Receive an admission-fee request and return payment or waiver status through Finance-owned workflow |
| Principal or admission approver | Review exceptions, override where permitted, and approve final activation |
| Academic administrator | Confirm board, medium, standard, division, and capacity before the student becomes active |
| Parent or guardian | Receive admission instructions, provide information, and understand the applicant's status |

## Core lifecycle

The production lifecycle is fixed for product clarity and should appear as a stepper.
Schools configure whether an applicant must pass through each optional gate, but the order remains predictable.

| Stage | Purpose | Required by default |
|---|---|---|
| Inquiry | Capture applicant and guardian intent in Pipeline | Yes |
| Assessment scheduled | Invite or schedule a written test, interaction, or interview | Configurable |
| Exam completed | Record attendance and result | Configurable |
| Eligible or ineligible | Decide whether the applicant can continue | Configurable when exam is enabled |
| Registration | Collect student, family, previous-school, consent, and document details | Yes |
| Finance verification | Ask Finance to confirm admission-fee readiness | Configurable |
| Placement review | Confirm board, medium, standard, and division timing against Institute Setup and Class Management | Yes |
| Activation approval | Approve conversion into an active Student | Yes |
| Activated | Handoff to Student Records | Yes |

Terminal outcomes are Rejected, Withdrawn, Ineligible, Cancelled, and Activated.
Rejected or ineligible applicants may be reopened only by authorized users with a reason.

## Admission Year Setup and Applicant Stepper

Admission Year Setup is the configured path attached to an Institute Setup academic year.
It should not create a parallel academic-year model.
It adds admission-specific rules to the academic year that already owns school structure.

Admission Year settings:

| Area | Admission Year decision |
|---|---|
| Academic context | Academic year from Institute Setup, target board, medium, and standards |
| Admission window | Inquiry open date, inquiry close date, registration close date, and activation target date |
| Path requirement | Whether an assessment and Finance verification are required and whether division is assigned before activation |
| Route behavior | Fresh, transfer, RTE, sibling, staff-child, management referral, and other school-approved routes |
| Step owner | Default owner role or team for each step |
| Message moments | Templates for step start, reminder, completion, rejection, and activation |
| Capacity view | Placement capacity source and override rule |

Each school has one active Admission Year Setup per academic year.
Board, medium, standard, and route differences are rules inside that setup rather than separate admission-year records.

| Setup state | Behavior |
|---|---|
| Draft | Authorized staff can edit rules and complete readiness checks |
| Published | New applicants use the published rules and message moments |
| Closed | New inquiry capture is blocked while historical applicants and reports remain readable |

Publishing must validate required dates, routes, step owners, gate rules, placement timing, and required message templates.
Each applicant must retain the applied setup version so later changes do not silently rewrite completed, skipped, or blocked step evidence.
Changes to a published setup require impact review and a new version.

Applicant Stepper is the applicant-facing and staff-facing progress model.
It should show a compact sequence with status per step.

Stepper statuses:

| Status | Meaning |
|---|---|
| Not started | Step is expected but no work has begun |
| In progress | Step has started and awaits action |
| Complete | Step requirement is satisfied |
| Skipped | Admission Year Setup allows this applicant to skip the step |
| Blocked | Step cannot proceed until a listed issue is resolved |
| Needs review | Authorized staff must decide or override |

Stepper rules:

| Rule | Requirement |
|---|---|
| Fixed order | Inquiry, Exam where required, Registration, Finance where required, Placement, Activation |
| Controlled skip | A skipped step must be allowed by Admission Year Setup or recorded by an authorized bypass reason |
| Clear blocker | A blocked step must show the missing field, document, Finance status, placement issue, or permission needed |
| One next action | Each applicant should show the single most useful next action for staff |
| Audit trail | Completed, skipped, blocked, and reopened steps preserve actor, time, and reason where applicable |
| Parent clarity | Parent-facing progress hides internal notes but explains the next required family action |

## Target navigation and interaction model

The accepted Student Enrollment navigation is intentionally smaller than the current source menu.
Visible labels should use school-facing language even when existing route paths or code identifiers are retained for compatibility.

| Menu group | Menu | Main surface |
|---|---|---|
| Daily work | My Work | Assigned tasks, due work, blockers, and one recommended next action |
| Daily work | Pipeline | School-wide applicant board and list before activation |
| Admission process | Assessments | Written tests, interviews, interactions, schedules, attendance, decisions, absence, and retest work |
| Admission process | Visits & Events | Campus visits, open houses, counselling, orientations, capacity, reservations, and attendance |
| Admission process | Registration | Form progress, missing items, corrections, document review, and verification |
| Admission process | Class Placement | Capacity review and division assignment from Institute Setup and Class Management |
| Admission process | Activation | Gate matrix, approval, repair state, and Student Records handoff |
| Admission process | Follow-ups & Messages | Due reminders, manual follow-ups, message history, and delivery issues |
| Review | Admission Reports | Auditable admission counts, stage funnel, source mix, standard, and outcomes |
| Settings | Admission Year Setup | Academic-year admission dates, routes, required steps, ownership, and publish readiness |
| Settings | Pipeline Settings | Inquiry sources, inquiry types, owner defaults, owner pool, capture fields, and shortcuts |
| Settings | Assessment Settings | Applicability, schedules, decision rules, retest, scheduling, and assessment messages |
| Settings | Registration Settings | Sections, fields, documents, languages, verification, and correction reasons |
| Settings | Finance Handoff Settings | Finance gate applicability, request trigger, and visible Finance references |
| Settings | Class Placement Settings | Division timing, capacity warning, and override permissions |
| Settings | Activation Settings | Required gates, approval role, cancellation window, and activation message |
| Settings | Communication Settings | Channels, templates, consent, delivery visibility, and simple reminders |

Contextual surfaces should not become extra menu items.

| Contextual surface | Entry behavior |
|---|---|
| Add Inquiry | Opens from My Work and Pipeline and creates the Pipeline record before any later work |
| Applicant workspace | Opens from any applicant row or card and carries the full Applicant Stepper, checklist, next action, and activity |
| Finance verification | Appears in the Applicant Stepper and Activation gate matrix while Finance staff complete money work in Finance |
| Parent inquiry form | Uses a public link outside authenticated staff navigation |
| Family enrollment center | Uses a public, applicant-safe view of the same step concept without internal notes or permission details; one verified guardian session may switch among active applications with the same tenant and primary contact, or among applications explicitly granted to the same authority-verified portal guardian |

Family return sign-in is passwordless. It accepts a tenant-scoped phone or email, returns a
non-enumerating challenge response, and grants only a short-lived session after OTP verification.
Guardian delegation requires an active relationship, explicit portal access, and verified authority.
Authorization is rechecked for every family action so revocation takes effect during an existing
session. Expired, revoked, cross-tenant, or unmatched application links are never included.

The target menu should not expose generic Operations, Offers & Seats, Policy & Compliance, Follow-up Automation, or generic Analytics labels.
Follow-ups & Messages replaces generic Operations.
Class Placement replaces Offers & Seats or Class Placement Plan.
Admission Reports replaces generic Analytics.
Simple admission reminders remain part of Communication Settings while advanced campaigns and journey automation remain deferred.

## Workspace interaction rules

| Surface | Interaction rule |
|---|---|
| My Work | Show one configurable table of assigned tasks with applicant, current step, issue, due time, owner, and next action |
| Pipeline | Support board and list views over the same applicant records |
| Pipeline board | Use fixed school-facing columns and do not allow arbitrary lifecycle columns |
| Focused worklists | Assessments, Registration, Class Placement, Activation, and Follow-ups & Messages filter the same Pipeline applicants rather than copying records |
| Shared admissions events | Visits & Events uses a dedicated event aggregate with one capacity and roster; it must not be represented as duplicate applicant tasks or assessment slots |
| Applicant workspace | Make the Applicant Stepper the primary navigation and show the exact blocker for locked work |
| Settings pages | Keep each operational area on its own settings page instead of one large configuration page |
| Admission Year Setup | Use a sequential setup stepper because configuration must be completed and published in order |
| Tables | Use one primary table per worklist page with filters, sorting, pagination, and role-appropriate actions |
| Permissions | Hide unauthorized menu items for usability and enforce every action on the backend |

The Pipeline board columns are fixed for the production baseline.

| Order | Column |
|---|---|
| 1 | New Inquiry |
| 2 | Follow-up |
| 3 | Exam Pending |
| 4 | Exam Done |
| 5 | Eligible |
| 6 | Registration Pending |
| 7 | Registration Done |
| 8 | Finance Verification |
| 9 | Placement Pending |
| 10 | Ready for Activation |
| 11 | Activated |

Rejected, Ineligible, Withdrawn, Cancelled, Duplicate, and Not Interested are closed outcomes available through filters and reports rather than permanent board columns.

## Pipeline requirements

Pipeline is the admissions command center.
Every applicant must enter Pipeline, even direct-registration or management-referred cases.
Pipeline quick capture should be fast enough for a walk-in family at the school counter.

Minimum inquiry fields:

| Field | Rule |
|---|---|
| Applicant name | Required first name, optional middle and last names |
| Guardian name | Required for school follow-up |
| Guardian mobile | Required unless a school-approved exception is recorded |
| Desired standard | Required |
| Academic year | Required |
| Board and medium | Required |
| Inquiry source | Required from configured values |
| Inquiry type | Required from configured values |
| Owner | Optional at capture and assignable later |
| Notes | Optional |

Before creating a new inquiry, the system should check likely duplicates using normalized applicant identity, guardian contact, and academic context.
Staff should be able to open the existing applicant instead.
Continuing with a likely duplicate requires permission and a recorded reason.

Pipeline actions:

| Action | Requirement |
|---|---|
| Assign owner | Assign an HR employee or admissions user who can work the lead |
| Create follow-up | Record due date, owner, and reason |
| Send message | Use approved templates for inquiry acknowledgement, exam invitation, registration invitation, fee reminder, and admission decision |
| Schedule exam | Manually schedule an exam from available slots |
| Invite exam scheduling | Send a parent-facing scheduling link when self-scheduling is enabled |
| Move to registration | Allowed when exam is not required or an authorized user bypasses exam with reason |
| Mark closed | Record rejected, withdrawn, duplicate, or not-interested outcome with reason |

Pipeline settings should not allow arbitrary lifecycle redesign.
Schools may configure labels, colors, owners, inquiry types, sources, required quick-capture fields, and whether exam or registration shortcuts are allowed.

## Assessment requirements

Assessment is optional by academic year, standard, admission pathway, and school policy.
Schools may use it for formal exams, interactions, interviews, readiness checks, or document-screening sessions.

Settings:

| Setting | Requirement |
|---|---|
| Applicability | Academic year, board, medium, standard range, and admission route |
| Exam mode | Written exam, interaction, interview, readiness check, or other school-defined label |
| Schedules and slots | Date, start time, end time, room, capacity, and owner |
| Parent scheduling | Enabled or disabled per admission year or exam definition |
| Pass rule | Pass mark, grade band, or manual review threshold |
| Result outcomes | Eligible, Ineligible, Needs Review, Absent, and Retest Required |
| Retest policy | Disabled, allowed once, or authorized-only |
| Message templates | Invitation, reminder, result eligible, result ineligible, retest, and absent follow-up |

Result entry is manual in the production baseline.
Bulk import and scanner import may exist later, but the requirement baseline should not depend on them.
Eligibility must be auditable because it affects admission opportunity.

## Visits and events requirements

Admissions events are operational encounters that do not decide applicant eligibility. Campus visits,
open houses, counselling sessions, and orientations therefore remain separate from Assessments.

| Capability | Requirement |
|---|---|
| Lifecycle | Draft, Published, Cancelled, and Completed with audited actor and time |
| Schedule | Start, end, location or meeting point, owner, and family-facing instructions |
| Capacity | One event-wide limit with atomic duplicate and full-capacity protection |
| Roster | Applicant, guardian context, reservation state, check-in, no-show, and cancellation evidence |
| Cancellation | A reason is mandatory and active reservations are cancelled with the event |
| Scope | Tenant, academic year, backend permission, and effective UserScope are mandatory |
| Parent scheduling | Disabled by default per event; staff may allow OTP-verified families to reserve and cancel future published events, with the same event-wide capacity and applicant timeline audit |

Event creation, editing, publishing, cancellation, completion, and roster mutation require explicit
permissions. An event may be completed only after its end time. Applicant timeline evidence records
reservation, cancellation, check-in, and no-show actions.

## Optional structured admissions review

Schools may enable a structured review gate for approve, reject, and waitlist decisions. It is
disabled by default so a school can retain the simpler single-decision workflow.

- Configuration is academic-year scoped and snapshots its title, required completed-review count,
  conflict-declaration rule, and rubric criteria into each started review.
- Only active employees with both review visibility and submission permissions may be assigned.
- A reviewer can see and submit only their own confidential recommendation unless they separately
  hold confidential-review access. Conflict declarations can recuse a reviewer without exposing the
  reason to ordinary viewers.
- Final decision-makers must have confidential-review access and cannot record an approve, reject,
  or waitlist decision until the configured number of reviews is complete. Governed eligibility
  override remains available only through the existing override permission and mandatory rationale.
- Review assignment, conflict declaration, submission, readiness, closure, final rationale, and any
  override remain tenant-scoped and auditable. Confidential rubric content is never copied into the
  general applicant timeline.

## Registration requirements

Registration collects the data required before the school can safely admit the student.
Registration can be staff-entered from paper, staff-assisted at the counter, or parent-facing when that surface is enabled.

Registration sections:

| Section | Examples |
|---|---|
| Applicant details | Name, DOB, gender, photo, category where required |
| Academic target | Board, medium, standard, division preference if applicable |
| Guardian and household | Parents, guardians, mobile, email, address, relationship, emergency contact |
| Previous school | Previous school name, last standard, transfer details where applicable |
| Documents | Birth certificate, Aadhaar where collected, transfer certificate, report card, address proof, caste or RTE documents where applicable |
| Consent and declarations | Communication consent, document declaration, school policy acknowledgement |

Registration settings:

| Setting | Requirement |
|---|---|
| Form sections | Enable, disable, order, and label standard sections |
| Required fields | Configure by academic year, standard range, and route |
| Required documents | Configure by academic year, standard range, and route |
| Languages | English is required, Gujarati and Hindi labels are supported where parent-facing forms are enabled |
| Verification | Staff must mark registration verified before activation can proceed |
| Correction | Staff can return incomplete or incorrect information with reason |

## Finance handoff requirements

Finance owns actual money.
Student Enrollment may request admission-fee verification and display Finance-returned status.

Finance handoff states:

| State | Meaning |
|---|---|
| Not required | This admission year or route does not require fee verification before activation |
| Requested | Enrollment has requested Finance review |
| Pending | Finance has not confirmed payment or waiver |
| Verified | Finance confirmed the required payment or approved waiver |
| Blocked | Finance found a payment, waiver, or reconciliation issue |

Enrollment must not create receipts, post ledgers, settle payments, or issue refunds.
Enrollment may store a Finance reference and display it to authorized staff.
Activation should be blocked when fee verification is required and Finance has not returned Verified.

## Placement requirements

Placement is not optional.
Only the timing and depth of placement are configurable.

Placement uses Institute Setup and Class Management as the source of truth for academic structure and capacity.
Enrollment should consume board, medium, standard, division, and capacity availability from those modules rather than maintaining a parallel seat-pool model.

Placement modes:

| Mode | Requirement |
|---|---|
| Standard-only before activation | Confirm board, medium, and standard before activation, then assign division later in Student Records |
| Division before activation | Confirm board, medium, standard, and division before activation |
| Approval-required capacity override | Allow over-capacity placement only with permission and reason |

Capacity signals should show configured capacity, current active students, pending activations, and remaining capacity where available.
If Class Management cannot provide a reliable count, Enrollment must show the capacity status as unknown rather than inventing a number.

## Activation requirements

Activation is the controlled conversion from applicant to active Student.
Activation is allowed only after required gates pass.

Activation gates:

| Gate | Rule |
|---|---|
| Registration verified | Required |
| Exam eligibility | Required only when exam is configured |
| Finance verification | Required only when configured |
| Placement review | Required |
| Approval | Required by configured role or permission |

Activation creates or links the Student record and hands ownership to Student Records.
GR number generation and advanced identifier allocation may remain a later implementation slice, but the requirement should reserve the activation point as the correct place for that future automation.
Partial activation failures must be visible and repairable.

## Communication requirements

Messaging is required for production readiness.
The production baseline may start with one configured provider per channel, but the school must be able to configure message templates and send the right instruction at the right moment.

Message moments:

| Moment | Message |
|---|---|
| Inquiry captured | Acknowledgement and next-step expectation |
| Exam invitation | Schedule, date, documents to bring, and contact |
| Exam reminder | Reminder before scheduled exam |
| Result eligible | Registration invitation |
| Result ineligible | Polite outcome and school contact |
| Registration incomplete | Missing information or document reminder |
| Registration verified | Next step or Finance instruction |
| Finance pending | Payment or verification reminder |
| Activation approved | Admission confirmation and first-day instructions |

Settings:

| Setting | Requirement |
|---|---|
| Channels | WhatsApp, SMS, and email can be enabled independently |
| Templates | Each moment can select an approved template per channel |
| Variables | Templates can use applicant, guardian, standard, exam, registration, fee, and school-contact variables |
| Consent | Parent communication must respect captured consent and opt-out where applicable |
| Delivery log | The system records queued, sent, failed, provider-not-configured, and opt-out-blocked states |
| Provider credentials | Header values, parameter values, and callback secrets are write-only and must be redacted after storage |

The product must not imply successful delivery unless a provider callback or delivery status supports that claim.
Simple reminders may be configured by admission moment, channel, maximum attempts, and school quiet hours.
One-time admission moments send immediately when the corresponding action occurs.
Idle-delay reminders are limited to Registration Issued, Registration Incomplete, Registration Correction Requested, Registration Verified, Fee Follow-up, Exam No-show, and Activation Welcome because those states are reevaluated by the hourly lifecycle worker.
Scheduled reminders must appear in Follow-ups & Messages and remain cancellable by authorized staff.
Reminder rules must respect consent, terminal outcomes, completed steps, and duplicate-message protection.
Advanced audience builders, discount campaigns, and multi-branch communication journeys are outside the production baseline.

## Settings model

Student Enrollment settings should follow an operational module pattern like HR.
Each operational area owns its settings so staff do not need to navigate one giant configuration page.

| Area | Settings |
|---|---|
| Admission Year Setup | Academic year, open and close dates, target standards, routes, default gates, and placement timing |
| Pipeline Settings | Inquiry sources, inquiry types, default owner, owner pool, quick-capture required fields, follow-up reasons, and stage labels |
| Assessment Settings | Written tests, interviews, interactions, applicability, slots, decision rules, retest policy, parent scheduling, and messages |
| Registration Settings | Sections, required fields, required documents, language labels, verification rules, and correction reasons |
| Finance Handoff Settings | Whether fee verification is required, Finance request trigger, and displayed Finance reference fields |
| Class Placement Settings | Placement timing, live capacity status, and override permissions |
| Activation Settings | Approval requirement, required gates, activation messages, and cancellation window |
| Communication Settings | Channels, provider mode, message templates, variables, simple reminders, and delivery-log visibility |

A separate Automation settings page is outside the production baseline.
Simple admission reminders are configured in Communication Settings.
Advanced campaign journeys, SLA escalation, forecasts, retention workflows, privacy requests, waitlist forecasting, and provider-specific adapters are outside the current production requirement.

## CBSE and Gujarat considerations

Age eligibility, standard mapping, transfer documents, RTE records, and board-specific requirements matter for Indian schools.
The production baseline should support warnings and required-document rules by board, standard, and route.
Hard legal validation should be added only when the rule source, jurisdiction, and school policy are clear.
RTE and government allotment flows may enter Pipeline but should allow route-specific document and fee behavior.

## Reports and audit

Enrollment reporting should reconcile from auditable states, not from UI assumptions.
Minimum reports should cover inquiry count, source mix, exam scheduled, exam completed, eligible, registration pending, registration verified, finance pending, activation pending, activated, rejected, and withdrawn counts by academic year and standard.
Admission Reports must reuse the fixed Pipeline lifecycle projection and group results by lifecycle, inquiry source, target standard, and closed outcome.
The backend must apply tenant, academic-year, effective UserScope, applicant-creation-date, and target-standard filters before aggregation.
Interactive report date ranges must not span more than 366 days.
Important actions require actor, timestamp, previous state, new state, and reason where applicable.

## Explicit non-goals for the production baseline

- Do not let schools create arbitrary lifecycle stages.
- Do not own Finance receipts, ledgers, settlement, reconciliation, or refunds.
- Do not duplicate Class Management capacity as a separate Enrollment-owned capacity system.
- Do not make GR number automation mandatory in the first implementation slice.
- Do not expose a separate automation page, retention, privacy-request, forecast, or advanced analytics configuration in the production workflow.
- Do not include advanced audience builders, discount campaigns, or multi-branch communication journeys.
- Do not claim provider delivery success without provider evidence.
- Do not keep editing ordinary active-student profile data in Enrollment after activation.

## Decision register

| Decision | Agreed behavior |
|---|---|
| Parent self-scheduling | Support as configurable, but admin scheduling must work first |
| Direct registration | Allowed only after creating a Pipeline inquiry record |
| Division timing | Configurable between before activation and after activation |
| Fee gate | Finance-owned verification blocks activation when required |
| GR number | Reserve the activation boundary and implement allocation in a later slice if needed |
| RTE route | Keep in Pipeline with route-specific registration, document, and fee behavior |
| Age eligibility | Start with warnings and configurable document requirements unless legal rules are sourced |
