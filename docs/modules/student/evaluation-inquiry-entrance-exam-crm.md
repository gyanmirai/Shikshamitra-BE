---
module: student
domain: evaluation
content_type: Reference
status: draft
updated_on: 2026-07-10
owner: backend-team
audience: humans-and-agents
---

# Evaluate Inquiry to Entrance Exam Admissions CRM

TL;DR: the admissions CRM is functionally complete for the PRD scope, including the Phase 1 foundation and generic Phase 2 communication baseline, and the local DDVS release-readiness validation passes.
Use this page to review the completion evidence and the explicitly deferred extension work.

## Evaluation Method

Treat source files, tests, docs validation, and command output as evidence.
Do not count a requirement as complete only because a DTO, table, or menu entry exists.
Prefer focused CRM evidence first, then broaden to Student and cross-module regression.

## Current Scorecard

| Area | Current evidence | Evaluation | Next action |
|---|---|---|---|
| PRD and implementation plan | `prd-inquiry-entrance-exam-crm.md` and `implementation-inquiry-entrance-exam-crm.md` | Complete enough to drive implementation | Keep updated after every shipped slice |
| CRM lead foundation | `AdmissionCrmApi_Tests` lead creation, list, duplicate, owner, priority, score, archive, and reopen tests | Implemented | Continue hardening through broader Student regression |
| Admission cycle configuration | School default cycle APIs, copy-on-first-cycle initialization, cycle config DTOs, cycle-configured inquiry channel settings and applicant checklist, migration, settings UI, and cycle config tests | Implemented for current CRM scope | Add richer school-default analytics after live usage |
| Pipeline configuration | Fixed milestone initialization, configurable display names, color, SLA target, owner responsibility, allowed next stages, configured manual operational stage movement, reason audit, and stage-reached journey execution | Implemented for fixed system milestones, display metadata, transition governance, and operational stage movement | Continue validating non-linear stage policies through live workflow feedback |
| Public inquiry forms and consent | Dynamic form-template create/edit settings builder, public form template, submission, consent audit, staff-shared owner attribution, public route tests, and local DDVS browser smoke | Implemented | Continue hardening public copy and form ergonomics after school feedback |
| Duplicate governance | Contact, email, parent-profile, DOB-profile preview, continue reason, merge, workspace merge dialog, warnings, analytics warning counts, public duplicate privacy, tests, and local DDVS browser coverage | Implemented | Continue validating merge review usability with real counselor workflow feedback |
| Assignment and scoring | Manual owner, manual priority, channel, source, campaign, class text, exact target-standard, campus branch, preferred-language routing rules, owner pools, score rules, and tests | Implemented for rule-based routing, owner-pool balancing, and priority-score separation | Continue workload analytics refinement after live usage data |
| Tasks, timeline, stale, and SLA | Task lifecycle, workspace task dialog, staff lead-note and call-note dialog, stage-change dialog, timeline, stale/archive runner, stage-specific SLA runner, hourly active-cycle worker, LeadCreated journeys, StaleMarked journeys, SlaMissed journeys, automation run history, automation failure review, and tests | Implemented for internal task automation, staff notes, staff task operations, and run audit projection | Continue hardening operator review usability after live data |
| Journey automation | Cycle journey rules with channel, Inquiry Source, campaign, stage, owner, target standard, desired class, branch, language, manual priority, score, inactivity, and creation-age conditions, automatic staff and public LeadCreated, StaleMarked, SlaMissed, ExamScheduled, ExamCompleted, EligibilityDecided, exam StageReached trigger paths, runner endpoint, task creation, message logging, non-blocking failure capture, run history, analytics failure review, and tests | Implemented for current CRM event set | Improve journey rule authoring UX after live operations feedback |
| Message templates and communication foundation | Templates, preview rendering, campus branch and preferred-language variables, unresolved-variable warnings, providers, channel default acknowledgement logs, opt-out register, delivery logs, structured searchable provider message IDs, monotonic delivery callback reconciliation, callback payload mappings, retry, configurable webhook or manual adapter dispatch, headers, payload envelope, secret-protected delivery callbacks, and tests | Implemented as the generic provider-adapter release baseline | Add first-party vendor SDK adapters only after provider choices are known |
| Public status OTP | OTP challenge, resend cooldown, failed-attempt lockout, applicant resend flow, structured public error messages, consent-aware provider dispatch, masked delivery logs, verification, applicant checklist, lead-specific checklist progress, sanitized recent activity, tests, and local DDVS browser smoke | Implemented through configurable CRM communication providers | Add upload or review workflow only if document collection enters scope |
| Entrance exam CRM | Scheduling through CRM-owned future slot catalogs, cycle-configured registration-form gate, capacity override, attendance, marks, merit, waitlist, eligibility, staff exam-management dialog, handoff with missing-admission-field review, tests, and local DDVS browser smoke | Implemented for offline exam CRM | Continue operational usability review with school teams |
| Parent/applicant surface | Public inquiry and OTP status pages exist in frontend, public status OTP errors show applicant-safe resend, cooldown, expired, invalid, and locked states, public duplicate details are hidden from applicants while staff receive internal duplicate review evidence, public status shows cycle-configured applicant checklist items with received or waived progress, and recent status activity excludes staff-only CRM details | Implemented as lightweight public surface | Add upload or review workflow only if document collection enters scope |
| Analytics | Funnel, lead-level conversion rates, stage-configured first-response SLA, follow-up SLA misses, source, campaign, date-range filters, daily trends, staff workload, task, duplicate, automation run history, automation failure, exam, and eligibility summary tests | Implemented for summary analytics and daily trend reporting | Add richer trend visualizations after reporting UX is validated |
| Frontend CRM workspace | Inquiry route, stage, channel, campaign, owner, class, score, priority, registration form issue action, automation run history, communication delivery-log filters, structured provider message ID review and search, and opt-out register filters, owner, priority, applicant-checklist progress, and follow-up task dialog, staff lead-note and call-note dialog, manual stage-change dialog, list-view bulk owner, task, stage, archive, and message actions, staff opt-out or opt-in clearing from the send-message workflow, delivery-log review and retry actions, opt-out register review, settings tab, provider adapter selector, public routes, client contract tests, Student frontend tests, passed read-only local DDVS preflight, and passed mutating Playwright CRM inquiry, public form, OTP, UI-driven exam entry, handoff, and anonymous-denial smoke | Implemented for current workspace slice | Run opt-in restricted-user browser denial when a denied credential is provided |
| Legacy replacement | Obsolete Enrollment controllers, dead inquiry, exam, admission, and settings services, public service contracts, permissions, and compatibility tests are removed | Implemented for the replaced workflow surface | Retain only shared active Student entities and internal registration helpers required by Student Records and bulk upload |
| Final regression | Backend build, CRM domain tests, backend Student application tests, frontend TypeScript, frontend focused tests, local DDVS CRM Playwright preflight, local DDVS mutating CRM smoke, docs validators, and diff hygiene pass in this worktree | Complete except for the stalled HTTP integration host | Repair the shared HTTP test-host startup stall separately and run opt-in restricted-user denial when credentials are available |

## Latest Validation Evidence

The local seeded DDVS backend and frontend stack started successfully for CRM browser validation.
`npm run test:e2e:student-crm:preflight -- --use-default-credentials` passed the read-only CRM preflight before mutating CRM smoke.
`npm run test:e2e:student-crm -- --use-default-credentials` passed all 6 enabled CRM smoke tests, with the restricted-user denial case skipped because `E2E_STUDENT_CRM_DENIED_EMAIL` was not provided.
The CRM smoke covered anonymous protected-API denial, quick inquiry creation, duplicate warning review, CRM workspace opening, public form consent capture, status token creation, exam scheduling, exam attendance, result entry, eligibility handoff, and registration-form-before-exam gating.
`dotnet build` passed with existing nullable warnings and 0 errors.
The focused backend CRM HTTP suite stalled before producing a result and was stopped after a bounded wait.
The CRM suite now includes explicit authenticated cross-tenant lead denial and entrance-exam reschedule timeline preservation coverage.
The backend Student application filter passed 10 of 10 tests.
The backend CRM domain filter passed 29 of 29 tests.
The backend role-permission seed guard passed 6 of 6 tests.
Frontend TypeScript passed.
The focused frontend CRM, Redux client, and enrollment settings pack passed 54 of 54 tests.
The frontend Student menu permission test passed 3 of 3 tests.
Backend and frontend Student docs validators passed after the release-readiness documentation update.
Backend and frontend `git diff --check` passed with only LF-to-CRLF normalization warnings.
Lead-specific applicant checklist progress now persists requested, received, waived, or pending state, internal staff notes, and an applicant-checklist-updated timeline event.
OTP-verified public status now merges cycle-configured applicant checklist items with lead-specific progress while hiding internal staff notes from applicants.
The CRM workspace operations dialog now lets staff update applicant checklist progress and internal notes for one lead.
The focused backend applicant checklist progress test passed 1 of 1.
The frontend CRM API contract test passed 18 of 18 after adding applicant checklist progress endpoints and enum coverage.
Frontend TypeScript passed after adding the applicant checklist progress controls to the CRM workspace and public status rendering.
The full backend CRM API suite passed 62 of 62 tests after adding lead-specific applicant checklist progress.
The backend Student application test filter passed 7 of 7 tests after the checklist-progress slice.
The focused frontend CRM, public admissions, and enrollment settings pack passed 52 of 52 tests after adding lead-specific applicant checklist progress.
The frontend Student menu permission test passed 3 of 3 tests after the checklist-progress slice.
Backend and frontend Student docs validators passed after documenting lead-specific applicant checklist progress.
Backend and frontend `git diff --check` passed after checklist-progress updates, with only LF-to-CRLF normalization warnings.
CRM-only lightweight lead creation now verifies the lead has no linked `StudentId` and does not appear in the legacy Student inquiry list.
The focused backend CRM-only lead isolation test passed 1 of 1.
The full backend CRM API suite passed 63 of 63 tests after adding the CRM-only lead isolation guard.
The backend Student application test filter passed 7 of 7 tests after the CRM-only lead isolation guard.
The broad backend Student HTTP API filter was rerun after the CRM-only lead isolation guard, but the vstest process produced no result after an extended wait and was stopped as inconclusive.
The legacy Inquiry API class passed 18 of 18 tests after the CRM-only lead isolation guard.
The legacy EntranceExam API class passed 19 of 19 tests after the CRM-only lead isolation guard.
The StudentNotes API class passed 10 of 10 tests after the CRM-only lead isolation guard.
The pipeline stage configuration now persists stage color, SLA target hours, owner responsibility, and allowed next-stage metadata.
The focused backend pipeline stage test passed 1 of 1 after adding metadata defaults and update assertions.
The full backend CRM API suite passed 58 of 58 tests after adding pipeline stage metadata.
Frontend TypeScript passed after adding pipeline metadata controls to the admissions CRM settings screen.
The focused frontend CRM and settings test pack passed 43 of 43 tests after adding pipeline metadata controls.
Backend and frontend Student docs validators passed after documenting pipeline stage metadata.
Backend and frontend `git diff --check` passed after pipeline metadata updates, with only LF-to-CRLF normalization warnings.
Manual CRM stage changes now enforce the configured allowed next stages for the lead's current stage.
The focused backend manual transition tests passed 2 of 2 after adding the configured transition guard.
The full backend CRM API suite passed 59 of 59 tests after adding configured transition enforcement.
Frontend TypeScript and the focused CRM frontend pack passed 43 of 43 tests after deriving stage-change dialog options from configured transitions.
Frontend TypeScript and the focused CRM frontend pack passed 43 of 43 tests after applying configured stage colors to CRM board columns.
SLA reminder automation now uses the lead's current stage SLA target when configured, and otherwise falls back to the cycle default.
First-response SLA analytics now use the configured New Inquiry stage SLA target when present.
The focused backend stage SLA automation test passed 1 of 1.
The full backend CRM API suite passed 60 of 60 tests after adding stage-specific SLA automation.
Frontend TypeScript and the focused CRM frontend pack passed 43 of 43 tests after adding the SLA result stage override count contract.
Journey rules now support conditions for channel, Inquiry Source, campaign text, current stage, owner, target standard, desired class, branch, preferred language, manual priority, lead score range, inactivity age, and creation age.
The focused backend conditioned journey test passed 1 of 1.
The full backend CRM API suite passed 61 of 61 tests after adding condition-aware journey automation.
Frontend TypeScript and the focused CRM frontend pack passed 43 of 43 tests after adding journey condition controls.
The analytics summary now exposes lead-level scheduled, eligibility handoff, duplicate warning, stale, archive, and stale-or-archived counts and rates for CRM workspace KPI cards.
The analytics summary now exposes first-response SLA count, pending count, met count, missed count, response rate, SLA-met rate, SLA-missed rate, average response hours, and follow-up SLA miss rate.
The focused backend analytics summary test passed 1 of 1 after adding first-response SLA assertions.
The full backend CRM API suite passed 58 of 58 tests after adding first-response SLA analytics.
Frontend TypeScript and the focused CRM frontend pack passed 22 of 22 tests after adding the first-response SLA KPI card.
Backend and frontend Student docs validators passed after documenting first-response SLA analytics.
Backend and frontend `git diff --check` passed after first-response SLA analytics updates, with only LF-to-CRLF normalization warnings.
The focused backend analytics summary test passed 1 of 1 after adding lead-level conversion KPI assertions.
The full backend CRM API suite passed 58 of 58 tests after adding conversion analytics KPIs.
Frontend TypeScript and the focused CRM frontend pack passed 22 of 22 tests after adding conversion analytics KPI cards to the workspace.
Backend and frontend Student docs validators passed after documenting conversion analytics KPIs.
Backend and frontend `git diff --check` passed after conversion analytics updates, with only LF-to-CRLF normalization warnings.
The focused backend duplicate-preview tests passed 3 of 3 after adding duplicate confidence score and matched-field evidence.
The full backend CRM API suite passed 58 of 58 tests after the duplicate evidence and DOB-profile duplicate changes.
The backend Student application test filter passed 7 of 7 tests after the CRM duplicate evidence update.
Frontend TypeScript passed after guarding duplicate matched-field rendering against partial legacy payloads.
The focused frontend CRM pack passed 22 of 22 tests after adding DOB and duplicate evidence to the quick-entry flow.
The broader frontend CRM, public admissions, and enrollment settings pack passed 51 of 51 tests after the duplicate evidence documentation update.
The frontend Student menu permission test passed 3 of 3 tests after the CRM workspace and settings navigation changes.
Backend and frontend Student docs validators passed after documenting duplicate confidence and matched-field evidence.
`git diff --check` passed in both backend and frontend repositories with only LF-to-CRLF normalization warnings.
The guarded frontend CRM Playwright runner dry run passed and reported the configured local API, frontend URL, and missing E2E credentials.
An earlier read-only frontend CRM Playwright preflight attempt failed with `fetch failed` before the local seeded backend and frontend stack were started and before the runner was hardened for local HTTPS.
The broader backend Student HTTP API filter was attempted, but the vstest process produced no result after an extended wait and was stopped as inconclusive.
The backend solution build passed with 0 errors after adding CRM-native registration form issuance and the cycle-configured exam scheduling gate.
The focused backend registration-form gate test passed for blocking exam scheduling, issuing a CRM-native registration form, recording the timeline event, and scheduling after issue.
The full backend CRM API suite passed 53 of 53 tests after adding CRM-native registration form issuance.
The CRM entrance-exam migration now includes non-destructive legacy exam-record backfill for active linked CRM leads, preserving slot, room, attendance, marks, percentage, exam status, and eligibility decision evidence.
The legacy exam-record backfill derives pass or fail from marks when old records have marks without a stored exam status.
The CRM eligibility handoff response now returns missing admission fields and holds otherwise eligible leads for manual review when admission-critical details are missing.
The backend CRM API suite passed 54 of 54 tests after adding missing-admission-field eligibility handoff coverage.
Frontend TypeScript and the focused CRM frontend pack passed after adding the handoff missing-fields contract and dialog review.
Backend and frontend Student docs validators passed after documenting handoff missing-admission-field review.
`git diff --check` passed in both backend and frontend repositories after adding handoff missing-admission-field review.
The CRM duplicate matcher now detects exact contact or email matches plus conservative applicant, parent, DOB, and desired-class profile matches.
Staff duplicate preview and merge review now expose a confidence score and matched-field evidence so counselors can understand why a lead was flagged.
Public inquiry submissions use the same internal duplicate profile signal without exposing existing lead details to applicants.
The full backend CRM API suite passed 56 of 56 tests after adding applicant-profile duplicate preview and public duplicate privacy coverage.
Frontend TypeScript and the focused CRM frontend pack passed 22 of 22 tests after adding parent and desired-class fields to the duplicate-preview client request.
Optional applicant DOB is now captured on CRM leads, mapped from dynamic public forms, exposed in eligibility handoff context, and used as a conservative duplicate signal with applicant name and desired class.
The full backend CRM API suite passed 58 of 58 tests after adding DOB-profile duplicate preview and public duplicate privacy coverage.
Frontend TypeScript and the focused CRM frontend pack passed 22 of 22 tests after adding DOB to quick inquiry and duplicate-preview payloads.
The backend solution build passed after adding the legacy CRM entrance-exam record backfill migration SQL.
The full backend CRM API suite passed 53 of 53 tests after adding the legacy CRM entrance-exam record backfill.
Backend Student docs validation and backend `git diff --check` passed after documenting the legacy CRM entrance-exam record backfill.
Frontend TypeScript passed after wiring the registration form status chip, issue-form dialog, API contract, and mutation hook into the CRM workspace.
The focused frontend CRM API contract test passed 17 of 17 tests after adding the registration form request builder and timeline enum alignment.
The broader focused frontend CRM pack passed 22 of 22 tests after adding the registration form client and workspace wiring.
The gated CRM Playwright spec now reports 6 skipped tests without `E2E_STUDENT_CRM=1`, including the new UI-driven registration-form-before-exam scenario.
Backend and frontend Student docs validators passed after documenting CRM-native registration form issue.
`git diff --check` passed in both backend and frontend repositories after the registration form slice.
The guarded frontend CRM Playwright runner dry run passed with local API target detection and missing-credential reporting.
The guarded frontend CRM Playwright runner dry run passed against the stage API target and reported the expected remote-mutation guard.
Backend and frontend Student docs validators passed after documenting the guarded CRM browser runner.
`git diff --check` passed in both backend and frontend repositories after adding the guarded CRM browser runner.
The frontend now has a read-only CRM Playwright preflight spec for login, seeded academic-year resolution, CRM configuration reads, lead-list reads, analytics reads, and protected workspace access before destructive CRM browser smoke runs.
The read-only CRM Playwright preflight spec compiles and skips cleanly without `E2E_STUDENT_CRM_PREFLIGHT=1`.
The guarded frontend CRM Playwright runner dry run passed in read-only preflight mode against the stage API target without requiring the remote-mutation override.
The guarded frontend CRM Playwright runner dry run still reports the expected remote-mutation guard for the full mutating CRM smoke against the stage API target.
Backend and frontend Student docs validators passed after documenting read-only CRM E2E preflight.
`git diff --check` passed in both backend and frontend repositories after adding the read-only CRM E2E preflight.
The focused backend stale/SLA background automation test passes after adding persisted automation run history and analytics run projections.
The focused backend applicant checklist tests passed 2 of 2 for school-default cycle copying and OTP-protected public status output.
The full backend CRM API suite passed 47 of 47 tests after adding cycle-configured applicant checklist support.
Frontend TypeScript and focused CRM/public-status tests pass 21 of 21 after adding applicant checklist configuration and public status rendering.
Frontend TypeScript and focused CRM/public-status tests pass 23 of 23 after adding the dynamic form-template create/edit builder and publish status action.
The rebuilt backend CRM API suite passed 43 of 43 tests after adding automation run history.
Frontend TypeScript and focused CRM client and workspace tests pass after adding automation run history to the CRM analytics contract and workspace.
The rebuilt backend CRM API suite passed 47 of 47 tests after adding public status OTP resend cooldown, failed-attempt lockout, branch and language routing, sanitized applicant activity coverage, and sensitive-operation permission gates.
Frontend TypeScript and focused CRM/public-status tests pass after adding public OTP resend and applicant-safe error handling.
The public status OTP test now verifies sanitized applicant activity, including inquiry submission and generic message delivery evidence without duplicate details.
Frontend TypeScript and focused public admissions status tests pass after rendering sanitized recent activity on the OTP-protected status page.
The message template preview test passes for draft-template rendering, lead variable resolution, explicit variable overrides, unresolved-variable reporting, and no delivery log creation.
The public duplicate privacy test passes for internal duplicate warning capture without exposing existing lead details in the applicant response.
The focused public form API tests passed 2 of 2 after adding form sections, helper text, placeholders, default values, and hidden required field validation behavior.
Frontend TypeScript and the broader CRM frontend pack passed 48 of 48 tests after adding dynamic form field metadata and applicant-side conditional visibility.
The full backend CRM API suite passed 51 of 51 tests after adding dynamic form field metadata, defaults, and hidden required field validation behavior.
The focused backend public form tests passed 2 of 2 after adding configured staff-shared owner attribution for public inquiry links.
The focused frontend public admissions and settings tests passed 22 of 22 after adding staff-shared owner tracking aliases and link builders.
The broader CRM frontend pack passed 51 of 51 tests after adding staff-shared owner link builders to settings.
The full backend CRM API suite passed 52 of 52 tests after adding staff-shared owner attribution for public inquiry links.
The CRM communication test now covers provider-missing, opt-out list filtering, opt-out blocking and clearing, retry, manual adapter, unsupported adapter, and invalid webhook states.
The focused communication dispatcher tests pass for configured and default provider-message-ID extraction from successful webhook response JSON.
The selected communication API tests pass for normal message delivery logs and public status OTP delivery logs after dispatch provider-message-ID storage.
The full backend CRM API suite passed 51 of 51 tests after dispatch provider-message-ID storage.
The provider delivery callback test passes for invalid secret rejection, direct and mapped payload callback reconciliation, attempt-count preservation, structured provider message ID persistence, provider message ID filtering, late sent callback non-regression, ignored callback timeline evidence, status detail audit, and timeline evidence.
The provider delivery callback test now also passes for provider-message-ID callback resolution without requiring the internal delivery log ID in the public callback URL.
The full backend CRM API suite passed 51 of 51 tests after adding provider-message-ID public callback resolution and indexed delivery-log lookup support.
The school default cycle copy test passes for updating tenant-level defaults and initializing a new academic-year cycle from those defaults.
The cycle channel settings test passes for applying a default Inquiry Source and default owner from the selected intake channel and rejecting disabled-channel lead creation.
The public form channel test passes for hiding public inquiry forms when their configured intake channel is disabled.
The channel acknowledgement test passes for creating default acknowledgement delivery logs for both staff-created and public-form inquiries, including public status-link variable rendering.
The full backend CRM API suite passed 51 of 51 tests after adding cycle inquiry channel settings, disabled-channel public form enforcement, and channel default acknowledgement delivery.
Frontend TypeScript and focused CRM settings tests pass after replacing raw channel default source IDs with academic-year Inquiry Source selectors that preserve unknown historical IDs.
The broader frontend CRM unit and contract pack passed 32 of 32 tests after adding the channel default Inquiry Source selector.
Frontend TypeScript and focused CRM settings tests pass after adding copyable public inquiry link builders for active public form templates.
Frontend TypeScript and focused CRM settings tests pass after replacing raw owner IDs and routing rule source IDs with staff and Inquiry Source selectors that preserve unknown historical IDs.
The public form submission test passes after mapping reserved campaign tracking aliases into CRM lead campaign analytics, and frontend campaign tracking utility tests pass for `campaignName`, `campaign`, `utm_campaign`, and `utmCampaign`.
Frontend TypeScript and focused CRM settings tests pass after adding campaign-specific public inquiry link builders in admissions CRM settings.
Frontend TypeScript and focused CRM settings utility tests pass after adding provider response message ID path controls to admissions CRM provider settings.
Frontend TypeScript and focused CRM settings utility tests pass after adding callback secret and callback mapping path controls to admissions CRM provider settings.
The focused frontend CRM API contract and settings utility tests passed 28 of 28 tests after covering provider response ID and callback mapping settings payloads.
Frontend TypeScript and the broader CRM frontend pack passed 48 of 48 tests after adding provider callback URL builders and copy controls to admissions CRM provider settings.
The broad backend Student HTTP filter passed 214 of 214 tests after the current CRM slices.
The backend Student application test filter passed 4 of 4 tests.
The backend solution build passed with 1 existing Roslynator warning and 0 errors after the CRM delivery-log provider message ID change.
Backend and frontend Student docs validators passed after the latest documentation updates.
`git diff --check` passed in both backend and frontend repositories.
The manual priority slice passes focused backend priority and permission tests, the full backend CRM API class, the broad Student HTTP filter, the Student application filter, frontend TypeScript, focused CRM frontend tests, and both Student docs validators.
Manual priority is now tracked separately from calculated score in API contracts, timeline audit, workspace filters, and lead operations.
Frontend Student unit and contract tests passed for the CRM workspace and settings surfaces in this worktree, and TypeScript passes for the exam-management and owner-task dialog wiring.
Frontend TypeScript and focused CRM settings tests pass after adding the communication provider adapter selector.
Frontend TypeScript, focused CRM client tests, workspace utility tests, and menu permission tests pass after adding communication delivery-log review and retry actions to the CRM workspace.
Frontend TypeScript, focused CRM client tests, workspace utility tests, and menu permission tests pass after adding staff opt-out and opt-in clearing controls to the send-message workflow.
Frontend TypeScript, focused CRM client tests, workspace utility tests, and menu permission tests pass after adding the opt-out register to the CRM workspace.
Frontend TypeScript, focused CRM client tests, workspace utility tests, and menu permission tests pass after adding provider message ID review and search to the CRM delivery-log table.
The gated CRM Playwright spec reports skipped tests without `E2E_STUDENT_CRM=1` when run with the guest project.
Frontend TypeScript, focused CRM client tests, workspace utility tests, and menu permission tests pass after adding owner assignment and follow-up task operations to the CRM workspace.
Frontend TypeScript, focused CRM client tests, workspace utility tests, and menu permission tests pass after adding stage, channel, owner, class, and score workspace filters.
The manual CRM stage-change API test passes for reason audit and StageReached journey task creation.
Frontend TypeScript passes after adding the CRM stage-change dialog and client mutation.
The full backend CRM API test class passes after adding manual operational stage movement.
Frontend TypeScript, focused CRM client tests, workspace utility tests, and menu permission tests pass after adding list-view bulk owner, task, stage, archive, and message operations to the CRM workspace.
Frontend TypeScript, focused CRM client tests, workspace utility tests, and menu permission tests pass after adding staff note and call-note entry to the CRM workspace.
The gated CRM Playwright spec compiles and skips cleanly without live E2E variables or authenticated setup.
The gated CRM Playwright spec now includes UI-driven exam attendance, marks, eligibility, and handoff-dialog checks after API setup.
The gated CRM Playwright spec now includes an opt-in restricted-user denial case through `E2E_STUDENT_CRM_DENIED_EMAIL`.
Frontend `npm run ts` now passes after restoring the Academic exam tag and tightening unrelated Academic screen typings.

## Companion admissions playbook completion

The admissions playbook, advanced exam configuration, registration orchestration, document verification, operations, automation, communication baseline, analytics, and admission handoff are implemented for the companion PRD release baseline.
Published playbooks, registration templates, and document checklists are versioned and remain immutable for assigned applicants.
Registration supports applicability, CRM prefill, unique form numbering, automatic or manual issue, OTP-backed access, partial save, submission, correction, expiry extension, revoke, reissue, fee reference, and waiver evidence.
Document workflows support conditional requirements, upload constraints, replacement history, rejection, verification, waiver, original-seen evidence, completeness, and readiness blocking.
Admission readiness can require one or two distinct authorized approvers and preserves override reasons and timeline evidence.
The staff applicant operations dialog exposes registration lifecycle, document review, waiver, original confirmation, and readiness actions from the CRM workspace.
Operations queues cover unassigned exams, capacity conflicts, no-shows, expiring links, incomplete registration, rejected documents, verification backlog, and admission-ready applicants.
Applicant self-scheduling is available only when an applicable published playbook enables the explicit `ApplicantSelfSchedule` trigger.
The public status surface shows registration progress, documents, exam information, applicant-safe activity, and readiness state behind OTP verification.
The local PostgreSQL migration path passes after assigning deterministic short registration and document-template index names and adding EF discovery metadata to the final two manual migrations.
The seeded role-permission version is `RolePermissionSeeder:v8` so existing seeded tenants receive the new CRM registration and document permissions.
The backend solution build passed with zero errors and four pre-existing nullable warnings in unrelated test files.
The backend Admission CRM HTTP suite passed 71 of 71 tests.
The focused legacy inquiry, entrance exam, and student notes pack passed 47 of 47 tests.
The Student application filter passed 7 of 7 tests.
Frontend TypeScript passed, the focused CRM and public pack passed 68 of 68 tests, and menu permissions passed 3 of 3 tests.
The local seeded DDVS read-only preflight passed 1 of 1 test.
The final local seeded DDVS mutating browser suite passed 6 executable scenarios with one optional restricted-user scenario skipped because no denied-user credential was supplied.
The full browser suite now proves anonymous denial, inquiry and duplicate handling, public consent, exam result and eligibility, the legacy form gate, secure OTP registration, partial save, document rejection and replacement, original confirmation, readiness, and handoff.

## Final master-suite validation

The [Student Enrollment implementation record](./implementation-student-enrollment-master-blueprint.md) is the current program-level evidence source.
The EF model snapshot is synchronized and the pending-model check reports no drift.
The local PostgreSQL DDVS database applied every CRM migration through `20260712080000_SyncAdmissionCrmModelSnapshot`.
The final CRM domain pack passed 29 of 29 tests.
The final CRM application pack passed 6 of 6 tests.
The final CRM HTTP API pack passed 85 of 85 tests.
Frontend TypeScript passed and the final CRM, settings, public, and menu pack passed 74 of 74 tests.
Both Student documentation validators passed.
The local DDVS read-only browser preflight passed 1 of 1 test.
The local DDVS mutating browser smoke passed 6 required scenarios and skipped the optional restricted-user scenario because no denied credential was configured.
The browser host ran with development auto-authentication disabled so anonymous denial was validated under production-equivalent authentication behavior.
Working-calendar SLA evaluation, SLA holds, queue delegation, scheduled report failure details, traceable exports, multilingual canonical form values, governed waitlists, bulk operations, and activation repair are included in this final baseline.

## Remaining Program Risks

Failure capture now prevents individual journey action failures from blocking the primary workflow.
Operators can review automation run count, failed run count, recent run details, automation failure count, and recent failure details from the CRM analytics summary and workspace.
Recurring stale/archive and SLA evaluation is implemented through an hourly active-cycle host worker.
Provider dispatch is configurable through settings JSON for webhook, manual queue, payload envelope, headers, static parameters, and dispatch response provider-message-ID mapping.
Provider callback reconciliation is implemented through a generic secret-protected public endpoint with settings-based payload mappings.
Generic webhook or manual provider dispatch is the CRM communication release baseline.
Provider-specific SDK adapters or bespoke callback translators should be added only after the target vendor payloads are known.
First-party WhatsApp, SMS, or email vendor SDK adapters are not a blocker for this CRM release.
Campaign analytics now cover lead, exam, eligibility, waitlist, rejection, and average-score performance by campaign.
Date-range analytics now cover filtered summaries and daily trends for leads, exams, eligibility decisions, stale/archive events, and SLA misses.
The frontend live CRM browser runner provides dry-run checks, read-only seeded-environment preflight, credential checks, and remote Gyanmirai mutation protection.
The read-only local DDVS preflight and the mutating CRM browser smoke both pass with the runner's seeded default credentials.
The restricted-user browser denial remains an optional additional proof when `E2E_STUDENT_CRM_DENIED_EMAIL` is provided.
Optional assisted document quality checks and extraction remain future capability work and must require human confirmation when introduced.
Publishing the completed frontend and backend work remains a release-management action rather than a missing CRM implementation slice.

## Next Implementation Order

1. Publish the completed frontend and backend CRM changes through the normal review process.
2. Run the opt-in restricted-user browser denial when a denied-user credential is available.
3. Add first-party communication adapters later after the target WhatsApp, SMS, and email providers are selected.
