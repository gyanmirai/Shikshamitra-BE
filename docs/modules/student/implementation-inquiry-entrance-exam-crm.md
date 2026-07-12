---
module: student
domain: implementation-plan
content_type: Reference
status: draft
updated_on: 2026-07-10
owner: backend-team
audience: humans-and-agents
---

# Implement Inquiry to Entrance Exam Admissions CRM

TL;DR: deliver the Student Enrollment admissions CRM in sequenced milestones while preserving existing inquiry, registration, and entrance exam APIs until the new surfaces fully replace them.
The functional source of truth is [prd-inquiry-entrance-exam-crm.md](./prd-inquiry-entrance-exam-crm.md).

## Program Rules

Keep incomplete admission leads in CRM-specific aggregates until they are ready for student admission handoff.
Do not force minimal inquiry leads into the `Student` aggregate too early.
Use `Inquiry Source` in product language and in CRM public contracts.
Keep registration form issue optional and cycle-configurable.
Keep Phase 1 and Phase 2 planned together, but ship them sequentially.
Treat generic webhook or manual queue dispatch as the Phase 2 communication release baseline.
Do not treat vendor-specific WhatsApp, SMS, or email SDK adapters as a release blocker until vendors and payload contracts are selected.
Run final mutating browser validation only against a local seeded DDVS environment unless a throwaway staging tenant is explicitly approved.
Run the read-only CRM browser preflight before the mutating CRM browser smoke.
Remove obsolete Enrollment inquiry, Inquiry Preference, registration workflow, entrance exam, and admission endpoints after their CRM replacements are operational.

## Current Implementation Status

| Area | Status | Notes |
|---|---|---|
| Functional PRD | Done | The target inquiry-to-entrance-exam CRM scope and release-readiness definition are documented in the PRD |
| CRM lead foundation | Done | Minimal lead creation, optional applicant DOB capture, branch and preferred-language capture, duplicate preview, lead listing, campaign filtering, permissions, EF mapping, and migrations are implemented |
| CRM configuration foundation | Done | School-level cycle defaults, per-year cycle override copying, inquiry channel enablement and defaults, exam readiness fields, merit ranking mode, applicant checklist, fixed pipeline milestones with color, SLA, owner responsibility, and allowed-transition metadata, form template CRUD, public consent validation, EF mapping, and migration are implemented |
| CRM replacement boundary | Done for current scope | Legacy inquiry, admission, entrance exam, Inquiry Preference, exam-slot, and Enrollment document-type controllers and dead application services are removed, while registration section services retained for active Student data are disabled for remote exposure |
| Public forms | Done for current scope | Active public templates can be read anonymously, submitted, mapped to CRM leads including optional applicant DOB, audited with raw submitted fields, assigned from configured staff-shared owner links, marked internally for contact, email, parent-profile, or DOB-profile duplicate review without exposing duplicate details to applicants, configured and edited through a dynamic frontend field builder, and rendered through frontend public inquiry routes |
| OTP status links | Done for current scope | Public status tokens, OTP challenge state, applicant resend flow, resend cooldown, failed-attempt lockout, structured public error messages, consent-aware SMS, WhatsApp, or email provider dispatch, masked delivery logs, OTP verification, cycle-configured applicant checklist, lead-specific applicant checklist progress, sanitized recent activity, exam schedule, exam result, and eligibility status content are implemented |
| Duplicate governance | Done for current scope | Duplicate preview now detects exact contact or email matches plus conservative applicant, parent, DOB, and target-class profile matches; confidence score, matched-field evidence, duplicate continue reason, list warning counts, analytics warning counts, and authorized merge-to-surviving-lead are implemented |
| Assignment and scoring | Done for current scope | Manual owner assignment, manual lead priority, channel, source, campaign, class text, exact target-standard, campus branch, and preferred-language rule routing, default-owner routing, round-robin owner pools, least-open-leads owner pools, configurable score rules, baseline score recalculation, and exam-aware score recalculation are implemented |
| Tasks, timeline, and automation | Done for current scope | Follow-up tasks, staff notes, timeline events, stage movement, archive and reopen, stale rules, working-calendar SLA evaluation, SLA holds and resume history, escalation ownership, hourly automation, condition-aware journeys, non-blocking failure capture, run history, and operator-facing analytics are implemented |
| Message templates | Done for current scope | WhatsApp, SMS, and email template configuration, variables including campus branch and preferred language, preview rendering, unresolved-variable warnings, active state, approval status, provider configuration, channel default acknowledgement delivery logs, opt-out records, send-template delivery logs, structured provider message IDs, status OTP delivery logs, webhook dispatch, failure logging, and retry queue states are implemented |
| Entrance exam CRM | Done for current scope | Direct scheduling from CRM lead, cycle-configured exam readiness checks, CRM-owned future slot catalogs, slot capacity checks, authorized capacity override, attendance, marks, pass or fail, class-wise merit ranking, waitlist, rejection, eligibility override governance, and read-only eligibility handoff context with missing-admission-field review are implemented |
| Analytics | Done for current scope | Tenant and academic-year scoped summary metrics are implemented for stage funnel, lead-level conversion rates, first-response SLA, follow-up SLA misses, channel, source, campaign performance, date-range filters, daily trends, staff workload, duplicate warnings, task counts, automation run count, failed run count, recent runs, automation failure count and recent failures, exam attendance, pass rate, average percentage, eligibility decisions, class-wise exam performance, merit-ranked count, capacity overrides, and eligibility overrides |
| Phase 2 messaging | Done for generic provider baseline | Provider configuration, configurable webhook or manual adapter settings, opt-out handling, opt-out register filtering, delivery logs, structured and searchable provider message IDs, provider-not-configured and opt-out blocked states, template rendering, failed dispatch logging, retry queue states, and journey-triggered message logging are implemented |
| Frontend CRM workspace | Done for current scope | The inquiry route provides board, list, exam, and operations views with filters, saved queues, server-side bulk actions, owner, score, priority, tasks, timeline, SLA hold and resume, duplicate review, registration, documents, financial readiness, household identity, policy, special routes, offers, waitlist, activation, communication, analytics, and guarded Playwright coverage |

## Backend Endpoints Implemented

| Route | Purpose |
|---|---|
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/leads` | Create a lightweight CRM inquiry lead |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/leads` | List CRM leads for an academic year |
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/leads/duplicate-preview` | Preview likely duplicate CRM leads |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/duplicates/merge` | Merge one duplicate lead into the surviving lead with reason |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/owner` | Assign or clear a CRM lead owner |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/priority` | Set manual CRM lead priority with optional reason while preserving calculated lead score |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/stage` | Move a lead across operational inquiry stages with reason and stage-reached journey execution |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/registration-form` | Issue a CRM-native registration form on one lead |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/score/recalculate` | Recalculate a baseline CRM lead score |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/archive` | Archive a CRM lead with reason |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/reopen` | Reopen a stale or archived CRM lead with reason |
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/automation/stale-archive/run` | Run configured stale and archive thresholds and create internal stale follow-up tasks |
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/automation/sla-reminders/run` | Create follow-up tasks for leads that missed their current stage SLA threshold, falling back to the cycle SLA threshold |
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/automation/journeys/run` | Run configured journey rules for one trigger and create deduplicated follow-up tasks, template delivery logs, or failure evidence |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/tasks` | Create a follow-up task for a CRM lead |
| `GET /api/student-enrollment/admissions-crm/leads/{leadId}/tasks` | List follow-up tasks for a CRM lead |
| `GET /api/student-enrollment/admissions-crm/leads/{leadId}/timeline` | List CRM timeline events for one lead |
| `GET /api/student-enrollment/admissions-crm/leads/{leadId}/applicant-checklist` | List cycle-configured applicant checklist items merged with lead-specific progress |
| `PUT /api/student-enrollment/admissions-crm/leads/{leadId}/applicant-checklist/{key}` | Update one lead-specific applicant checklist item with progress and internal notes |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/notes` | Add a staff lead note or call note to the CRM timeline |
| `POST /api/student-enrollment/admissions-crm/tasks/{taskId}/complete` | Complete one follow-up task |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/exams` | List CRM entrance exam records for an academic year |
| `GET /api/student-enrollment/admissions-crm/leads/{leadId}/exam` | Get the CRM entrance exam record for one lead |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/exam/schedule` | Schedule or reschedule a CRM lead into an entrance exam slot |
| `POST /api/student-enrollment/admissions-crm/exams/{examRecordId}/attendance` | Mark CRM entrance exam attendance |
| `POST /api/student-enrollment/admissions-crm/exams/{examRecordId}/result` | Record CRM entrance exam marks, pass or fail, and manual or class-wise merit rank |
| `POST /api/student-enrollment/admissions-crm/exams/{examRecordId}/eligibility` | Set eligibility, waitlist, or rejection decision with override governance |
| `GET /api/student-enrollment/admissions-crm/exams/{examRecordId}/eligibility-handoff` | Return read-only lead and exam context for admission handoff |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/analytics/summary` | Return CRM funnel, source, campaign, task, duplicate, automation run, automation failure, daily trend, and exam analytics with optional filters |
| `GET /api/student-enrollment/admissions-crm/configuration/cycle-default` | Get or initialize tenant-level school defaults for new admission cycles |
| `PUT /api/student-enrollment/admissions-crm/configuration/cycle-default` | Update tenant-level school defaults copied into new admission cycles |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle` | Get or initialize CRM cycle configuration |
| `PUT /api/student-enrollment/admissions-crm/{academicYearId}/configuration/cycle` | Update CRM cycle configuration |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/configuration/pipeline-stages` | Get or initialize fixed CRM pipeline milestones with display and governance metadata |
| `PUT /api/student-enrollment/admissions-crm/configuration/pipeline-stages/{stageId}` | Update one pipeline stage name, order, enabled state, color, SLA target, owner responsibility, and enforced allowed next stages |
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/configuration/form-templates` | Create an inquiry form template |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/configuration/form-templates` | List inquiry form templates |
| `GET /api/student-enrollment/admissions-crm/configuration/form-templates/{id}` | Get one inquiry form template |
| `PUT /api/student-enrollment/admissions-crm/configuration/form-templates/{id}` | Update one inquiry form template |
| `PATCH /api/student-enrollment/admissions-crm/configuration/form-templates/{id}/status` | Activate or deactivate one inquiry form template |
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/configuration/message-templates` | Create a WhatsApp, SMS, or email message template |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/configuration/message-templates` | List message templates |
| `GET /api/student-enrollment/admissions-crm/configuration/message-templates/{id}` | Get one message template |
| `PUT /api/student-enrollment/admissions-crm/configuration/message-templates/{id}` | Update one message template |
| `PATCH /api/student-enrollment/admissions-crm/configuration/message-templates/{id}/status` | Update message template approval status and active state |
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/communication/message-templates/preview` | Render one draft or approved message template without sending or creating a delivery log |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/communication/providers` | Get or initialize WhatsApp, SMS, and email communication provider settings |
| `PUT /api/student-enrollment/admissions-crm/{academicYearId}/communication/providers/{channel}` | Update one communication provider setting |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/communication/opt-outs` | List opt-out records by lead, channel, active state, or contact search |
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/communication/opt-outs` | Record one lead communication opt-out |
| `POST /api/student-enrollment/admissions-crm/{academicYearId}/communication/opt-outs/clear` | Clear one active communication opt-out with reason |
| `POST /api/student-enrollment/admissions-crm/leads/{leadId}/messages/send-template` | Render one approved template and create a delivery log in queued, provider-not-configured, or opt-out blocked status |
| `GET /api/student-enrollment/admissions-crm/{academicYearId}/communication/delivery-logs` | List communication delivery logs for one academic year |
| `POST /api/student-enrollment/admissions-crm/communication/delivery-logs/{deliveryLogId}/retry` | Queue a retry for one failed or blocked delivery log after provider and opt-out checks |
| `POST /api/public/student-enrollment/admissions-crm/{tenantId}/communication/delivery-logs/{deliveryLogId}/callback` | Reconcile a provider delivery callback using the provider callback secret and optional settings-based payload mappings |
| `POST /api/public/student-enrollment/admissions-crm/{tenantId}/communication/{academicYearId}/{channel}/delivery-callback` | Reconcile a provider delivery callback by provider message ID, academic year, and channel |
| `GET /api/public/student-enrollment/admissions-crm/{tenantId}/forms/{templateId}` | Read one active public inquiry form |
| `POST /api/public/student-enrollment/admissions-crm/{tenantId}/forms/{templateId}/submit` | Submit a public inquiry form and create a CRM lead |
| `POST /api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/otp` | Issue an OTP challenge for a public status token and create consent-aware provider delivery evidence |
| `POST /api/public/student-enrollment/admissions-crm/{tenantId}/status/{token}/verify` | Verify OTP and return applicant status |

## Milestone Plan

### Milestone 1: Backend CRM Foundation

Status: implemented for the current CRM foundation.
This milestone adds CRM-specific lead storage, permissions, minimal lead creation, duplicate preview, lead listing, and non-destructive legacy backfill.
The backfill links CRM leads to existing `Student` rows when possible and preserves existing development data instead of rewriting legacy records.
The CRM entrance-exam migration now backfills linked legacy exam slot, room, attendance, marks, percentage, status, and eligibility decision evidence into CRM exam records when a legacy student has an exam slot.

### Milestone 2: CRM Configuration Foundation

Status: implemented for the current CRM scope.
This milestone adds school-level admission cycle defaults, per-academic-year cycle overrides, inquiry channel enablement and defaults, exam readiness fields, merit ranking mode, applicant checklist items, lead scoring rules, lead routing rules, condition-aware journey rules, owner-pool routing strategies, fixed pipeline milestones, configurable display names, form templates, public-form consent validation, and configuration APIs.
New academic-year cycle configurations copy the school defaults once and then remain independently editable.
Inquiry channel settings now control enabled intake channels and default Inquiry Source, public form template, owner, acknowledgement template, and display order per cycle.
Default acknowledgement templates now create consent-aware CRM delivery logs for staff and public inquiries through the same provider, opt-out, and timeline pipeline as other template sends.
Message template configuration now supports WhatsApp, SMS, and email templates with variables, approval status, active state, and preview rendering before approval.
Communication provider settings now support channel enablement, provider name, sender identifier, settings JSON, and webhook URL for Phase 2 setup.
Configured provider settings JSON supports webhook or manual adapter selection, payload envelope mode, outbound headers, static parameters, and response provider-message-ID mapping.
Configured provider webhooks can now receive rendered CRM message payloads, drive Sent or Failed delivery log status, and capture provider message IDs from successful webhook response JSON.
The frontend settings surface now allows applying or saving school-level defaults while editing cycle defaults, inquiry channel defaults with academic-year Inquiry Source and staff owner selectors, exam readiness fields, applicant checklist items, registration-before-exam behavior, merit ranking mode, lead scoring rules, lead routing strategy, owner pool, target-standard, branch, and language-aware lead routing rules with staff and source selectors, condition-aware journey rules, pipeline stages, dynamic form templates with copyable public inquiry, campaign, and staff-shared owner links, message templates, communication providers, provider response message ID paths, provider callback mapping paths, and copyable provider callback URLs.

### Milestone 3: Inquiry Capture and Public Forms

Status: implemented for the current CRM scope.
Public form read, public submission, optional applicant DOB mapping, campaign tracking aliases, staff-shared owner tracking aliases, consent record capture, acknowledgement response, raw field submission storage, internal-only duplicate warning capture, status token creation, OTP challenge state, OTP provider dispatch, OTP verification, cycle-configured applicant checklist content, lead-specific applicant checklist progress, sanitized recent activity, exam schedule details, exam result details, and eligibility decision details are implemented.
The frontend settings dialog now creates and edits dynamic form templates with configurable channel, field type, required state, display order, select options, CRM system-field mapping, section name, helper text, placeholder, default value, conditional visibility, template preview, and active or inactive publishing state.
Public inquiry rendering now groups visible fields by configured section, applies default values, displays helper text and placeholders, and ignores hidden required fields during validation and CRM system-field mapping.
The frontend inquiry route now supports CRM quick-entry, duplicate preview with confidence and matched-field evidence before staff save, duplicate merge from warning leads, board view, list view, and exam view.
The frontend public routes now render dynamic inquiry forms and OTP-protected applicant status with resend, cooldown, expired, invalid, and locked OTP states.
Staff-created duplicate continuation now requires a reason in the quick inquiry dialog, and the backend records that reason when a duplicate is present.
Public form submission now triggers configured LeadCreated journeys after the CRM lead and status token are persisted.
Status OTP issue is protected by a resend cooldown, and repeated invalid verification attempts lock the active OTP challenge.
Status OTP delivery now uses configured CRM SMS, WhatsApp, or email providers based on consented contact channels.
Stored OTP delivery logs mask the raw OTP value while provider dispatch receives the actual code.

### Milestone 4: Ownership, Scoring, Tasks, and Automation

Status: implemented for the current CRM scope.
Manual owner assignment, manual priority with reason, configured operational stage movement with reason, baseline score recalculation, follow-up task creation, task listing, task completion, lead timeline events, manual archive, reopen with reason, stale/archive rule execution, stale owner follow-up task creation, stage-specific SLA reminder task execution, and condition-aware journey rule execution are implemented.
Lead-specific applicant checklist progress is implemented with requested, received, waived, and pending states, internal notes, staff timeline audit, and applicant-safe public status output.
Manual priority is stored separately from calculated lead score so staff urgency labels do not erase scoring-rule evidence.
Staff lead notes and call notes are recorded as timeline events and count as lead activity for stale automation.
The frontend CRM workspace now lets staff assign or clear lead owners, set manual priority, update applicant checklist progress, and create, review, or complete follow-up tasks from board and list leads.
The frontend CRM workspace now lets staff add lead notes or call notes from board and list leads.
The frontend CRM workspace now lets staff move active inquiry leads across operational stages while keeping exam and eligibility stages governed by exam workflows.
The frontend CRM workspace list view now supports bulk owner assignment, bulk follow-up task creation, bulk operational stage movement, bulk archive, and bulk template messaging through existing audited lead operations.
The frontend CRM workspace now supports stage, channel, campaign, owner, desired-class, score-band, and manual-priority filters for board and list review.
LeadCreated journeys now run automatically after staff lead creation and public form submission.
StaleMarked journeys now run automatically when stale/archive automation marks a lead stale.
SlaMissed journeys now run automatically when SLA reminder automation evaluates a lead.
SLA reminder automation uses the lead's current pipeline stage SLA target when configured, and otherwise falls back to the cycle default lead SLA.
The manual journey runner remains available for scoped replay and operations support.
The first analytics summary endpoint is implemented for funnel, lead-level scheduled, duplicate, stale, archive, eligibility handoff conversion rates, stage-configured first-response SLA, follow-up SLA misses, channel, source, campaign, date-range, staff workload, task, automation run, automation failure, exam, pass-rate, eligibility, class-wise exam performance, and exam governance metrics.
The analytics summary endpoint accepts optional date, channel, inquiry source, campaign, owner, stage, target class, and lead score filters.
Daily trend rows count leads, exams scheduled, exams completed, eligibility decisions, stale events, archive events, and SLA misses.
Authorized duplicate merge now archives the duplicate lead, records the surviving lead, and preserves the merge reason.
Journey action failures are captured as `AutomationFailed` timeline events and returned in the journey runner result.
Journey action failures do not block the primary lead creation, public form submission, stale/SLA automation, or exam workflow event.
Journey action failures also appear in analytics summary output with total failure count and the most recent failure details for workspace review.
The host registers `AdmissionCrmAutomationWorker` to run stale/archive and SLA automation hourly for each tenant's active CRM cycle configuration.
Stale/archive, SLA reminder, and configured journey rule executions are persisted as automation run history.
Automation run history captures run type, status, trigger context, task and message actions, stale and archive counts, failed actions, and details for analytics review.
Journey execution creates deduplicated internal tasks and message delivery logs from configured journey rules.
Journey rules can now filter by channel, Inquiry Source, campaign text, current stage, owner, target standard, desired class, branch, preferred language, manual priority, lead score range, lead inactivity age, and lead creation age before executing their actions.

### Milestone 5: Entrance Exam CRM

Status: implemented for the current offline entrance-exam CRM scope.
Direct inquiry-to-exam scheduling from CRM leads is implemented using cycle-configured exam readiness fields.
CRM-native registration form issue is implemented for pre-admission leads, and exam scheduling enforces it only when the admission cycle requires a form before exam.
Backfilled leads linked to legacy student records can satisfy that gate with the linked legacy registration form number.
The backend now supports slot capacity checks, authorized capacity override with reason, attendance, reschedule before result, marks upload, pass or fail, class-wise merit rank recalculation, waitlist, rejection, eligibility override governance, and read-only admission eligibility handoff context.
The eligibility handoff response now identifies missing admission fields before full admission can proceed.
The frontend exam workspace now lets staff mark attendance, enter marks, record merit rank, decide eligibility, waitlist, or rejection, capture override reason, and review missing admission fields before handoff review.
Exam scheduling, result upload, and eligibility decision now trigger configured ExamScheduled, ExamCompleted, EligibilityDecided, and stage-reached journeys.
Exam analytics now includes class-wise performance, attendance and pass rates, eligibility and waitlist rates, merit-ranked count, capacity overrides, and eligibility overrides.

### Milestone 6: Phase 2 Communication

Status: generic provider-adapter release baseline implemented.
WhatsApp, SMS, and email provider configuration is implemented as channel-level setup for Phase 2.
Draft or approved message templates can be previewed against sample, provided, or CRM lead variables without sending or creating delivery logs.
Approved message templates can now be rendered against a CRM lead and recorded as delivery logs.
Configured channel default acknowledgement templates are sent automatically after staff or public lead creation, and public-form acknowledgement variables include the applicant status path.
Delivery logs record provider-not-configured, opt-out blocked, queued, failed, sent, delivered statuses, and structured provider message IDs from webhook dispatch responses or callbacks.
Staff can filter delivery logs by provider message ID when reconciling provider dashboards or webhook payloads.
Provider webhook dispatch attempts now set Sent or Failed status based on the provider response.
Provider dispatch settings now support manual queueing, unsupported-adapter failure evidence, envelope payloads, custom headers, static parameters, and `responseProviderMessageIdPath` extraction from webhook responses.
Lead opt-out and opt-in records are enforced before send and retry operations.
Staff can list active and cleared opt-out history by lead, channel, active state, or contact search.
Retry operations now re-check provider configuration and opt-out state before moving a delivery log back to queued.
Secret-protected provider callbacks now reconcile queued, sent, delivered, failed delivery status, and provider message IDs without increasing the manual retry attempt count.
Provider callback settings can map status, status details, provider message ID, and callback secret from provider payload JSON paths.
Provider callbacks can now resolve delivery logs either from the internal delivery log ID route or from the provider message ID plus academic year and channel.
Admissions CRM provider settings now expose callback secret, provider callback URL copy action, callback status path, callback detail path, callback message ID path, and callback secret path controls before the raw settings JSON editor.
Late provider callbacks are handled monotonically so delivered messages cannot regress to sent or queued, and stale callback evidence is preserved on the lead timeline.
Journey-triggered message logging is implemented through the same template and delivery-log pipeline used by manual send actions.
Status OTP delivery uses the same provider configuration, opt-out, webhook dispatch, and delivery-log foundation without requiring a message template.
Generic webhook or manual queue dispatch is the release baseline for CRM communication.
First-party WhatsApp, SMS, and email vendor SDK adapters should be added after the target providers and payload contracts are selected.
Vendor-specific SDK adapters are not a blocker for this CRM release.

### Milestone 7: Final Regression and Publish Readiness

Status: local DDVS CRM release validation complete for the current wrap-up.
Backend build, CRM domain tests, Student application tests, Student docs validation, frontend TypeScript, focused frontend tests, frontend docs validation, read-only CRM preflight, mutating CRM smoke, and diff hygiene pass in this worktree.
Restricted-user browser denial remains opt-in and should run when `E2E_STUDENT_CRM_DENIED_EMAIL` is available.
Generated artifact cleanup and publish readiness review should run before opening or updating the release PR.

## Latest Wrap-Up Validation

The local seeded DDVS backend and frontend stack started successfully for CRM browser validation.
`npm run test:e2e:student-crm:preflight -- --use-default-credentials` passed the read-only CRM preflight before the mutating smoke.
`npm run test:e2e:student-crm -- --use-default-credentials` passed all 6 enabled CRM smoke tests, with the restricted-user denial case skipped because `E2E_STUDENT_CRM_DENIED_EMAIL` was not provided.
`dotnet build` passed with existing nullable warnings and 0 errors.
The focused CRM HTTP suite remains inconclusive because the repository test host stalled before producing results and was stopped after a bounded wait.
`dotnet test test\ShikshaMitra.Application.Tests\ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Students"` passed 10 of 10 tests.
`dotnet test test\ShikshaMitra.Domain.Tests\ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~AdmissionCrm"` passed 29 of 29 tests.
`dotnet test test\ShikshaMitra.Domain.Tests\ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~RolePermissionSeederContributor_Tests"` passed 6 of 6 tests.
`npm run ts` passed.
The focused frontend CRM and settings pack passed 54 of 54 tests.
`npm run test -- src/constants/student-menu.permissions.test.ts` passed 3 of 3 tests.
Backend and frontend Student docs validators passed.
Backend and frontend `git diff --check` passed with only LF-to-CRLF normalization warnings.

## Validation Plan

Run these backend checks from `Shikshamitra-BE`:

```powershell
dotnet build ShikshaMitra.sln --no-restore
dotnet test test\ShikshaMitra.HttpApi.Tests\ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~AdmissionCrm"
dotnet test test\ShikshaMitra.Application.Tests\ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Students" --no-build
powershell -ExecutionPolicy Bypass -File scripts\docs\validate-docs.ps1 -Module student
git diff --check
```

Run these frontend checks from `Shikshamitra-FE` after the CRM workspace or settings UI is added:

```bash
npm run ts
npm run test -- src/redux/student src/screens/student/enrollment
npm run test -- src/constants/student-menu.permissions.test.ts
npx playwright test student/student-module-role-access.spec.ts --project=chromium --reporter=list
npm run test:e2e:student-crm:preflight -- --use-default-credentials
npm run test:e2e:student-crm -- --use-default-credentials
node scripts/docs/validate-docs.mjs --module student
git diff --check
```

## Focused Test Coverage

| Coverage area | Backend tests |
|---|---|
| Minimal CRM lead capture | `AdmissionCrmApi_Tests.Should_Create_Minimal_Crm_Lead_Without_Dob_Or_Address` |
| Duplicate preview | `AdmissionCrmApi_Tests.Should_Preview_Duplicate_By_Contact_Number` |
| Applicant-profile duplicate preview | `AdmissionCrmApi_Tests.Should_Preview_Duplicate_By_Applicant_Parent_And_Class` |
| DOB-profile duplicate preview | `AdmissionCrmApi_Tests.Should_Preview_Duplicate_By_Applicant_Dob_And_Class` |
| Duplicate continue reason | `AdmissionCrmApi_Tests.Should_Record_Duplicate_Continue_Reason` |
| Duplicate merge | `AdmissionCrmApi_Tests.Should_Merge_Duplicate_Crm_Lead_With_Reason` |
| CRM lead list | `AdmissionCrmApi_Tests.Should_List_Crm_Leads_By_Academic_Year` |
| Default cycle configuration | `AdmissionCrmApi_Tests.Should_Return_Default_Cycle_Configuration` |
| School default cycle copying | `AdmissionCrmApi_Tests.Should_Copy_School_Default_Cycle_Configuration_To_New_Academic_Year` |
| Default pipeline milestones, metadata update, and transition guard | `AdmissionCrmApi_Tests.Should_Return_Default_Pipeline_Stages_In_Order`, `AdmissionCrmApi_Tests.Should_Reject_Manual_Stage_Transition_When_Not_Configured` |
| Public form template round-trip, field metadata, and hidden required field behavior | `AdmissionCrmApi_Tests.Should_Create_Public_Form_Template_With_Consent_And_Fields`, `AdmissionCrmApi_Tests.Should_Submit_Public_Form_With_Consent_And_Create_Crm_Lead` |
| Public form consent guard | `AdmissionCrmApi_Tests.Should_Reject_Public_Form_Template_Without_Consent` |
| Message template round-trip | `AdmissionCrmApi_Tests.Should_Create_Update_And_Approve_Message_Template` |
| Email message template guard | `AdmissionCrmApi_Tests.Should_Reject_Email_Message_Template_Without_Subject` |
| Message template preview | `AdmissionCrmApi_Tests.Should_Preview_Draft_Message_Template_Without_Sending` |
| Communication provider, adapter settings, opt-out list, delivery log, and retry | `AdmissionCrmApi_Tests.Should_Configure_Provider_Respect_OptOut_And_Record_Message_Delivery_Log` |
| Webhook dispatch response provider message ID extraction | `AdmissionCrmCommunicationDispatcher_Tests` |
| Provider delivery callback reconciliation and provider message ID persistence | `AdmissionCrmApi_Tests.Should_Reconcile_Delivery_Log_From_Secret_Protected_Public_Callback` |
| Public form submission, campaign tracking, and consent audit | `AdmissionCrmApi_Tests.Should_Submit_Public_Form_With_Consent_And_Create_Crm_Lead` |
| Public staff-shared owner attribution | `AdmissionCrmApi_Tests.Should_Assign_Configured_Owner_From_Public_Staff_Shared_Link` |
| Public form duplicate privacy | `AdmissionCrmApi_Tests.Should_Record_Public_Form_Duplicate_Internally_Without_Exposing_Details` |
| Public form applicant-profile duplicate privacy | `AdmissionCrmApi_Tests.Should_Record_Public_Form_Profile_Duplicate_Internally` |
| Public form DOB-profile duplicate privacy | `AdmissionCrmApi_Tests.Should_Record_Public_Form_Dob_Duplicate_Internally` |
| Channel default acknowledgement templates | `AdmissionCrmApi_Tests.Should_Send_Channel_Default_Acknowledgement_For_Staff_And_Public_Inquiries` |
| Public status OTP challenge and verify | `AdmissionCrmApi_Tests.Should_Issue_And_Verify_Public_Status_Otp` |
| Public status applicant checklist and lead-specific progress | `AdmissionCrmApi_Tests.Should_Issue_And_Verify_Public_Status_Otp`, `AdmissionCrmApi_Tests.Should_Update_Applicant_Checklist_Progress_And_Show_Public_Status` |
| Public status sanitized activity | `AdmissionCrmApi_Tests.Should_Issue_And_Verify_Public_Status_Otp` |
| Public status OTP failed-attempt lockout | `AdmissionCrmApi_Tests.Should_Lock_Public_Status_Otp_After_Repeated_Invalid_Attempts` |
| Public status OTP provider delivery | `AdmissionCrmApi_Tests.Should_Send_Public_Status_Otp_Through_Configured_Provider` |
| Owner, priority, score, archive, and reopen | `AdmissionCrmApi_Tests` owner-score-reopen scenario and `AdmissionCrmApi_Tests.Should_Set_Manual_Lead_Priority_Without_Changing_Score` |
| Configurable lead scoring rules | `AdmissionCrmApi_Tests.Should_Use_Cycle_Configured_Lead_Scoring_Rules` |
| Basic lead routing rules | `AdmissionCrmApi_Tests.Should_Assign_New_Lead_From_Cycle_Routing_Rules` |
| Branch and preferred-language lead routing rules | `AdmissionCrmApi_Tests.Should_Route_New_Lead_By_Branch_And_Preferred_Language_Rules` |
| Target-standard lead routing rules | `AdmissionCrmApi_Tests.Should_Route_New_Lead_By_Target_Standard_Rule` |
| CRM sensitive permission gates | `AdmissionCrmApi_Tests.Should_Reject_Sensitive_Crm_Operations_Without_Required_Permissions` |
| CRM tenant isolation | `AdmissionCrmApi_Tests.Should_Not_Expose_Crm_Lead_To_Another_Tenant` |
| Staff lead and call notes | `AdmissionCrmApi_Tests.Should_Add_Call_Note_To_Lead_Timeline` |
| Follow-up task lifecycle | `AdmissionCrmApi_Tests.Should_Create_List_And_Complete_Follow_Up_Task` |
| Lead timeline events | `AdmissionCrmApi_Tests.Should_Record_Crm_Timeline_For_Lead_Task_And_Exam_Actions` |
| Automatic LeadCreated journey execution and replay dedupe | `AdmissionCrmApi_Tests.Should_Run_Configured_Journey_Rules_For_New_Lead_And_Skip_Duplicates` |
| Condition-aware journey matching | `AdmissionCrmApi_Tests.Should_Run_Journey_Rule_Only_When_Lead_Matches_Configured_Conditions` |
| Public form LeadCreated journey execution | `AdmissionCrmApi_Tests.Should_Run_Lead_Created_Journey_When_Public_Form_Submitted` |
| Journey failure capture and analytics review | `AdmissionCrmApi_Tests.Should_Record_Journey_Failure_Without_Blocking_Automatic_Workflows` |
| Background stale/SLA automation path | `AdmissionCrmApi_Tests.Should_Run_Stale_And_Sla_Rules_From_Background_Automation_Path`, `AdmissionCrmApi_Tests.Should_Use_Stage_Sla_Target_For_Sla_Reminder_Task` |
| CRM exam scheduling | `AdmissionCrmApi_Tests.Should_Schedule_Crm_Entrance_Exam_From_Exam_Ready_Lead` |
| CRM exam reschedule history | `AdmissionCrmApi_Tests.Should_Preserve_Previous_Slot_Timeline_When_Entrance_Exam_Is_Rescheduled` |
| CRM exam lifecycle journey execution | `AdmissionCrmApi_Tests.Should_Run_Exam_Lifecycle_Journeys_When_Crm_Exam_Changes` |
| Cycle-configured exam readiness | `AdmissionCrmApi_Tests.Should_Use_Cycle_Configured_Exam_Readiness_Fields` |
| Cycle-configured registration form before exam | `AdmissionCrmApi_Tests.Should_Require_Crm_Registration_Form_Before_Exam_When_Configured` |
| Class-wise merit ranking | `AdmissionCrmApi_Tests.Should_Recalculate_Class_Wise_Merit_Ranks_When_Result_Uploaded` |
| CRM exam capacity override | `AdmissionCrmApi_Tests.Should_Prevent_Crm_Exam_Overbooking_Unless_Override_Recorded` |
| CRM exam override permissions | `AdmissionCrmApi_Tests.Should_Require_Override_Permissions_For_Crm_Exam_Capacity_And_Eligibility` |
| CRM exam result and waitlist | `AdmissionCrmApi_Tests.Should_Record_Crm_Exam_Attendance_Result_And_Waitlist_Decision` |
| CRM handoff missing admission information | `AdmissionCrmApi_Tests.Should_Show_Missing_Admission_Information_In_Crm_Eligibility_Handoff` |
| CRM exam eligibility override | `AdmissionCrmApi_Tests.Should_Require_Override_Reason_For_Failed_Crm_Exam_Eligibility_Upgrade` |
| CRM analytics summary and lead-level conversion KPIs | `AdmissionCrmApi_Tests.Should_Return_Crm_Analytics_Summary_For_Lead_Source_Task_And_Exam_Funnel` |

| Coverage area | Frontend tests |
|---|---|
| CRM API contract, dynamic form template fields, workspace utility rules, public campaign and staff-shared owner tracking, and public status OTP error handling | `npm run test -- src/screens/public/admissions-crm src/redux/student/admissions-crm src/screens/student/enrollment/admissions-crm` |
| CRM channel default Inquiry Source selector, owner selector, historical ID preservation, and public inquiry, campaign, or staff-shared link builders | `npm run test -- src/screens/student/enrollment/setting/admissions-crm-configuration-utils.test.ts src/screens/student/enrollment/setting/inquiry-source-utils.test.ts src/screens/student/enrollment/setting/inquiry-source-wiring.test.ts` |
| CRM live environment readiness | `npm run test:e2e:student-crm:preflight -- --use-default-credentials` |
| CRM quick inquiry, duplicate preview, public form consent, status token, OTP challenge, UI-driven exam attendance, result, eligibility, handoff, anonymous-denial, and opt-in restricted-user denial smoke | `npm run test:e2e:student-crm -- --use-default-credentials` |

## Known Validation Notes

The focused CRM API test suite covers the implemented backend slices for CRM lead capture, configuration, public forms, OTP, duplicate governance, ownership, priority, scoring, tasks, timeline, automation run history, condition-aware journey automation, entrance exam CRM, analytics, and communication foundation.
The latest broad Student HTTP API filter attempt was inconclusive because the vstest process hung without producing a result.
The Student application test filter passed 7 of 7 tests in this worktree.
The CRM Playwright flow spec is gated with `E2E_STUDENT_CRM=1`.
Without live E2E variables it should compile and report skipped tests without running authenticated setup.
The gated Playwright CRM flow now opens the exam workspace, saves attendance, marks, and eligibility through the UI, and verifies the eligibility handoff dialog before API handoff assertions.
The gated Playwright CRM flow now toggles cycle registration-before-exam behavior, verifies scheduling is blocked before issue, issues a form through the workspace UI, verifies timeline evidence, schedules after issue, and restores the cycle setting.
The restricted-user denial case is opt-in through `E2E_STUDENT_CRM_DENIED_EMAIL` and verifies that a signed-in user without CRM permissions cannot operate the protected workspace.
The latest broad frontend TypeScript run passes after fixing unrelated Academic tag and screen typing blockers.
The frontend now has a live CRM browser runner for dry-run checks, read-only seeded-environment preflight, credential checks, and remote-stage mutation protection before live CRM browser execution.
Live CRM Playwright execution should use the local seeded DDVS backend and frontend stack with the runner's default seeded credentials unless explicit E2E variables override them.
