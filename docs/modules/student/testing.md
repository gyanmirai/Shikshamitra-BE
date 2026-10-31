---
module: student
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-10-31
owner: backend-team
audience: humans-and-agents
---

# Validate Student Enrollment Admissions changes

## Student Records permission menu alignment

The October 30 menu fix keeps the existing permission identifiers and workflow enforcement, grouping five internal profile workflows under Student List.
Final local checks pass 71 Domain security/catalogue/seeding cases, 15 Academic scope synchronization cases, 84 permission HTTP cases and one PostgreSQL permission/upgrade case, all with zero failures and zero skips.
The PostgreSQL gate ran against a newly created marked disposable database, which was removed after the run; real school grants and data were untouched.

Full-source EF compilation now passes with normal nullable checks and analyzers, including every migration designer.
The October 31 warning cleanup passes the complete 15-project backend solution build with zero warnings and zero errors.
The complete migration model still matches the unchanged snapshot/latest target, and the five provider/fallback time-mapping cases pass.
The [serialized local build helper](../../common/development/backend-builds.md) removes insufficient child-process heap caps without excluding source or changing application settings.
Final binary hashes and source timestamps confirm all seven production project outputs are current.

The PostgreSQL startup failure was caused by scope synchronization materializing a large AutoInclude hierarchy graph.
Scalar hierarchy projections preserve required filtered parents, tenant/soft-delete filters, teacher eligibility, manual scopes and cache behavior.
Six added cases cover deleted parents and foreign-tenant references; the original nine synchronization cases also pass.
The first corrected PostgreSQL run completed scope seeding in 1535 ms, and a second run against the settled build also passed; these local observations do not establish production latency.

See [frontend verification](../../../../Shikshamitra-FE/docs/security/testing.md#student-three-action-permission-contract) for the 253-case Security gate, additional Student contact test repairs and synthetic role/user browser checks.
These focused suites and relevant full project builds do not certify every solution test, GitHub Actions, deployed role configuration or staging/production acceptance.
The subsequent Domain-fixture repair passes the full suite: 1650 tests, zero failures and zero skips.
Assessment rejection now tests unsupported versions 0/3 rather than supported version 2; normalization must retain the requested version before the reader rejects it.
Fee expectations retain fractional input and follow the documented whole-rupee round-up rule; timetable conflict priority/details are verified in explicit colon/dot cultures with culture restoration.
Staff-profile assertions use the baseline role catalogue and explicit internal test-persona metadata, preserving Student/Parent portals and the existing identity-only CRM negative actor without creating employee profiles or changing roles, aliases, grants or scopes.
All existing seeder execution bodies remain unchanged, and four compiled before/after catalogue comparisons have zero differences.
The fresh post-repair 15-project Debug solution build passes with zero warnings/errors. Final selections pass the full 1650-case Domain suite, 37 Application cases, 196 HTTP cases, six migration-model/provider cases and one real PostgreSQL permission/upgrade case, all without skips.
The first PostgreSQL attempt overlapped the other suites and timed out in FeeCollection demo-startup standard loading before permission assertions; an unchanged isolated rerun passed. Both marked disposable databases were removed. Parallel-load startup reliability remains unverified; these results do not certify every solution test, a fresh Release build or deployed acceptance.

## House Management security checks

`StudentModuleVisibilityPermissionContract_Tests` pins the 33-feature-group/111-action catalogue and exact House View/Add/Delete leaves. `StudentRecordsPermissionMenu_Tests` verifies that Student List contains all 16 existing profile-workflow actions without five extra feature groups or renamed permission identifiers. `NavigationVisibilityPrerequisiteContract_Tests` requires the existing Student and Student Records visibility flags for all three House actions. `PermissionConsistencyApi_Tests` covers exact grants, denial/Follow role, revocation, no teacher/development fallback and atomic visibility prerequisites.

```powershell
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~StudentModuleVisibilityPermissionContract_Tests|FullyQualifiedName~NavigationVisibilityPrerequisiteContract_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~House_actions_require|FullyQualifiedName~Students_catalogue_contains|FullyQualifiedName~Student_actions_follow|FullyQualifiedName~Teacher_assignment_and_development_environment"
```

Run `HouseManagementDomain_Tests`, `HouseManagement_Tests` and `HouseManagementApi_Tests` in addition to these catalogue checks. They cover house validation, independent leaders, preview/stamp conflicts, scoped and mixed-batch authorization, same-year placement cleanup, legacy deletion safeguards and endpoint contracts. Local SQLite/synthetic tests do not establish PostgreSQL concurrency, live persistence, migration deployment or authenticated school acceptance.

```powershell
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~HouseManagementDomain_Tests"
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~House_management"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~HouseManagementApi_Tests|FullyQualifiedName~HouseSettingsApi_Tests"
```

## Configured Inquiry capture

Primary-contact regression coverage includes `AdmissionPrimaryContactSelection_Tests`, `AdmissionInquiryConfiguredFields_Tests`, `AdmissionFormDefinitionValidation_Tests`, `AdmissionFirstParentContactMapping_Tests` and `AdmissionWorkflowRuntimeIntegration_PrimaryContact_Tests`. They exercise required generic versus optional structured ownership, hidden email definitions, explicit clears, parent-only sibling proof, public-parent aliases and pending/confirmed Guardian transitions. Journey configuration and name prefill are covered by `AdmissionJourneyIdentityConfiguration_Tests`, `AdmissionStructuredNamePrefill_Tests`, `AdmissionWorkflowRuntimeIntegration_JourneyIdentity_Tests` and existing initial-values/finalization-contact suites. Family writes include `StudentRecordMainContact_Tests` and owning Registration/Domain contact tests. Fresh source-linked helper checks do not replace current Application/Contracts compilation and service integration execution.

The configured-field helper and runtime integration suites cover Inquiry-only publication minima, full/split names, coherent generic/Father/Mother contacts, explicit parent selection, registered-binding ownership, legacy captured-role edits, preservation of unconfigured email, configured blank values, public source attribution and absence of staff-account provenance, scoped duplicate reference metadata, and rejection of forged sibling-role changes. Run the Inquiry-specific visibility-validator regressions as well: hidden-step questions cannot block capture, while visible required questions retain validation. Preserve the existing Registration validator path. These tests do not establish staging deployment or email delivery; normal Application/Contracts compilation and isolated test-only dependency compilation must be reported separately.

## Finalization main-contact compatibility

Run `AdmissionFamilyContactCapture_Tests`, `AdmissionFirstParentContactMapping_Tests`, the `First_parent_` integration scenarios and `Finalization_main_contact_preview` in the Application test project. These cover legacy Guardian handoff, explicit and malformed provenance guards, accepted parent-answer recovery, read-only preview and idempotent Student/Onboarding case reuse. Run `ShikshaMitraLocalization_Tests` in Domain tests and `Finalization_contact_errors_should_explain_the_required_review` in HTTP tests for readable errors. A focused diagnostic build does not establish full-checkout compilation, migration validation or deployed acceptance.

## Resume admission

`AdmissionJourneyResumeIntegration_Tests.cs` covers immutable closure evidence, exact stage and reference restoration, replay/second-closure rejection, legacy evidence and ambiguity, completed workflow finalization, tenant/stamp guards and part-paid Finance preservation. `AdmissionJourneyApiContract_Tests` asserts existing permissions at each entry point. `AdmissionJourneyResumePostgres_Tests` exercises two real serializable HTTP resume requests and one persisted resume event in the isolated `records_tests_*` database harness. Provider tests require `STUDENT_RECORDS_APPLICATION_POSTGRES`; an opt-in skip is not a passed concurrency check.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Journey_resume|FullyQualifiedName~Journey_end_"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~AdmissionJourneyApiContract|FullyQualifiedName~Admission_resume_postgres"
```

## Optional onboarding fees

`Current_onboarding_activates_without_fee_assignment_or_payment_and_keeps_finance_pending` covers activation and idempotent retry with no selected structure, no dues/assignment/payment metadata and truthful pending fee flags. `Activation_preview_*` includes legacy saved fee-step projection without a read-side evidence rewrite and the retained live-seat gate. `Optional_fees_*` covers missing Roll rejection and manual Student/Parent delivery intents with automatic sending disabled, for both configured credential timings. Existing `Automatic_onboarding_*` cases retain identity/document and account/snapshot regression coverage.

Focused local Application tests passed 6 activation/preview cases and 20 related regression cases on 29 October 2026. These use isolated test persistence and credential request records, not real mailbox delivery or deployed acceptance. No Finance implementation was changed.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Current_onboarding_activates_without_fee_assignment|FullyQualifiedName~Activation_preview_|FullyQualifiedName~Optional_fees_|FullyQualifiedName~Automatic_onboarding_|FullyQualifiedName~One_click_credentials_prepares_both|FullyQualifiedName~Current_offering_does_not_fabricate"
```

## Admission window calendar dates

`AdmissionCycleCalendarDate_Tests` covers create/edit, inherited clone, window revision and nullable
reopen inputs with IST, UTC, positive/negative offsets, timezone-free ISO dates/times, invalid dates,
and UTC-midnight response values. The tests use the actual ABP date-converter modifier with UTC,
local and unspecified clock modes, guarding its override of property converters. The separate scheduled-action regression retains the precise
publish/close instants, including seconds, and checks lifecycle schedule clearing.
`Cycle_calendar_dates_round_trip_through_abp_serialization_persistence_and_governed_commands` uses
the actual ABP serializer and scoped application service with test persistence for create, edit,
clone, schedule, publish, revise, close and reopen. Keep the existing Domain cycle/window rules
in the regression gate. Local automated checks do not establish deployed staging acceptance.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~AdmissionCycleCalendarDate_Tests|FullyQualifiedName~Cycle_calendar_dates_round_trip|FullyQualifiedName~AdmissionCycleContract_Tests|FullyQualifiedName~AdmissionCycleColumnFilters_Tests"
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~AdmissionCycle_Tests"
```

On 2026-10-29, the focused gate passed 68 Application tests and 12 Domain tests, with no skips.
An isolated process reproduced the original UTC/IST mismatch before the fix and retained
`2026-06-01` / `2027-03-31` in both timezone simulations afterward. The integration test also caught
ABP replacing property converters; property-level normalization opt-out is required, not a global
clock change. Existing compiler warnings remain; the completed focused test builds disabled
analyzers after the initial analyzer build was stopped. Local authenticated browser create/edit
retained June 1 / March 31 and then June 2 / March 30 after a fresh reload. Staging deployment
and correction of previously shifted records remain separate verification steps.

The live PostgreSQL check also caught schedule timestamps matching in the save response but
shifting by 5h30m in a fresh GET. `AdmissionJourneyUtc_Tests.Cycle_schedules_lifecycle_and_history_preserve_instants_under_legacy_Npgsql`
guards this provider-specific gap: actual PostgreSQL parameter type, UTC/Local/Unspecified inputs,
nullable values, offset-bearing reads, and lifecycle/history timestamps. Run it with the existing
journey UTC and cycle model tests; SQLite serialization tests alone cannot establish this contract.

```powershell
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests --filter "FullyQualifiedName~AdmissionJourneyUtc_Tests|FullyQualifiedName~AdmissionCycleModelMapping_Tests"
```

The initial provider/model gate exposed a stale test assertion: it expected no converter for
`AdmissionApplicationStageInstance.EnteredAt`, although the existing SQLite helper deliberately
normalizes DateTime to DateTime. The corrected test verifies that provider type, UTC normalization
for UTC/local/unspecified inputs and null preservation. A real in-memory SQLite regression using
the production stage configuration executes SQL `MIN`/`MAX`/`GROUP BY`, including an all-null group,
then checks UTC materialization after clearing tracked entities. Its isolated parent-table-free
fixture verifies scalar mapping and queries, not workflow foreign keys. No production mapping
change was needed. The corrected provider/model gate plus the credential-email parameter test
passed 8/8 with no skips. After the provider fix, the focused Application gate passed again
(68/68), and the local Host build completed with no warnings or errors.

Authenticated local PostgreSQL acceptance on 2026-10-29 scheduled a separate QA draft for
12:38:13 / 12:40:13 IST. Save and independent GET returned the same UTC instants. The existing
one-minute worker automatically published it at 12:38:58 and closed it at 12:40:58; history
recorded both scheduled lifecycle actions, both schedules cleared, and the June 2 / March 30
calendar window remained unchanged. The QA cycle had no offerings or applications. The existing
published General Admission Flow retained June 1 / March 31 and no schedules. This verifies the
restarted local Host, not deployed staging.

## Payment joining date

`AdmissionPaymentJoiningDate_Tests.cs` extends the owning runtime integration fixture with candidate-preview purity (no account or persisted date), missing-form proration, preview/preparation parity, application-stamp conflicts, prepared-date locking, submitted Registration answer synchronization, the shared Registration edit guard, and invalid date rejection. `AdmissionJoiningDate_Tests` covers application-local date storage and locked corrections. Retain the existing `Proration_` integration cases for policy-version, regular/one-time fee, legacy binding, and Onboarding handoff behavior. The implementation uses existing Finance calculations and endpoints without Finance-module changes or migrations.

The local owning gate passes 25 application cases (new date scenarios plus existing proration regressions), a separate five-case exact-permission/tenant/deferred-Onboarding gate, two persistence/submitted-snapshot rechecks, and two Domain cases, with zero skips. The persistence regression caught ABP rehydrating an ISO ExtraProperties string as `DateTime`; the application accessor and captured joining-date prefill now normalize that representation without changing the calendar day. These checks do not establish live payment acceptance. The attempted Development Host uses localhost PostgreSQL but startup is blocked by missing `AbpSettingDefinitions`; no migration, reseed, credential/configuration change, or payment collection was performed to bypass that blocker.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Payment_joining_date_preview_and_capture|FullyQualifiedName~Prepared_fee_schedule_should_lock|FullyQualifiedName~Payment_date_|FullyQualifiedName~Proration_"
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~AdmissionJoiningDate_Tests"
```

## Institute Setup academic form choices

`AdmissionAcademicOptionsUnit_Tests` covers legacy academic descriptors, the lowercase public-choice JSON contract, and owning View permissions without opening unrelated form sources. `Academic_form_choices_use_Institute_Setup_IDs_and_parent_membership` verifies configured class/division GUIDs, year/medium/class boundaries, public-offering choices and rejection of former hardcoded labels. Retain `Custom_record_dynamic_choices_are_record_bound_and_read_only_lookup_returns_only_selected_labels` when changing Records choice projection. These checks use the test database; they do not establish acceptance by a previously running Host process.

## Onboarding App Login (28 October 2026)

The final owning Application regression passes **125/125**, zero skips, across existing credential/dispatcher/sibling/email-edit/validation scenarios and the new Onboarding-only Parent confirmation cases. Domain outbox checks pass **8/8**. After the final policy/mailbox projection correction, a separate focused gate passes **6/6**; the full regression was not repeated solely for these narrow guards.

`StudentOnboardingExistingParent_Tests` covers Father/Mother/Guardian and legacy-flow linking without Admissions confirmation, activation without an unrequested Identity Users Update grant, exact target/source tenant/scope/stamp/action checks, original initial-setup evidence, conflicting roles/source drift, Guardian owner-verified email changes and help without adding/replacing an existing password. The scoped review findings have failing-then-passing regressions for unconfirmed-candidate email correction and activation without linking. Help transport is substituted; real identity, persistence, token and authorization services run.

The final six cases verify that saved rules disabling adult access hide both confirmation and link capabilities; Mother/Guardian may save the family's existing Father mailbox without account/link/email/Admissions writes; Student/adult destinations remain distinct in both edit directions; and actual conflicting identities remain blocked. Shared adult contact addresses do not authorize two adult accounts or automatic reuse.

The subsequent linked-email correction has a **12/12** focused gate (zero skips), including eight `StudentLinkedPortalEmailCollision_Tests` cases and impacted Student/shared-parent/Guardian verification and original recipient-provenance regression. Its red run reproduced six collision/late-drift failures while adult sharing already passed. Requests reject an identity-free opposite saved mailbox for linked Student/Father/Guardian; shared parents check every linked child; verification send and confirmation recheck canonical opposite profiles even when the existing own-email topology is unchanged. Confirmation drift uses a genuine protected owner token. Adult/adult sharing still verifies, and unproven raw Student fallback is not promoted into a Student login collision. Current Application/dependency compilation passes as part of this focused gate; the previous isolated Host result predates this internal service/reader correction, and no additional Host rebuild was requested.

The final serialized isolated Host build passes with **0 errors** and existing warnings. Its artifacts are separate from the running user Host; the earlier ordinary-output build encountered existing runtime/debugger file locks and was not treated as a successful build. No user backend was stopped or restarted. Student documentation and whitespace validation pass; all-module docs retain the two existing Finance reference-file mojibake blockers described below. No live email, authenticated runtime/deployment, migration or mailbox acceptance is established by these backend checks. Detailed command/TRX evidence remains in the session artifacts.

## Parent defaults and optional guardians (27 October 2026)

The final rebuilt Application gate passes 33 cases: `AdmissionFamilyContactCapture_Tests`, `StudentPortalOwnerSelection_Tests` and the `Optional_guardian_`, `Optional_mother_link_projection`, `Record_main_contact_`, `Main_school_`, `Guardian_access_capability`, `Confirmed_guardian_intent` and `Contact_only_parent_profiles` scenarios. Eleven Domain family/message-policy cases also pass.

These cover optional profile handoff, main-contact validation, immutable intake retry, selected-person communication without borrowing another mailbox, retained non-parent audience mode, explicit adult authorisation, exact linked-owner projection, email collisions, Optional/Not needed policy capability and legacy automatic preparation respecting a confirmed guardian case choice. Preservation tests compare a real existing password hash, existing child/sibling link IDs and relationships, retained sibling IDs and repeat-activation identity.

The separate existing account/sibling/email gate passes 52 cases. Its three contact-only rows initially omitted the new adult confirmation; their inputs were corrected and all three pass in the final 33-case run, retaining their existing setup blocker and no-creation assertions. Existing tenant, staff scope, exact action/identity grants, disabled policy, drift, shared-parent verification, password preservation and duplicate-delivery protections remain covered.

The serialized local Host build passes with zero errors and three existing Finance-controller nullable warnings. Student docs validation passes; all-module validation remains blocked by two existing Finance reference-file mojibake findings. No migration or Finance source change is required. The already-running local Host was not replaced, and in-app browser form input stalled; these automated checks do not establish current live UI persistence, mailbox delivery or deployed acceptance.

## Onboarding app-login email edits (24 October 2026)

- The focused Application gate passes 79 cases covering Student/Father/Mother profile email capture, stale/invalid/tenant/action rejection, current family-match reads, protected owner verification, shared-parent confirmation, pending-change cancellation, password/link preservation, current-recipient manual resend, and existing credential eligibility/dispatcher/sibling flows. Verification tests extract the real protected token from a fake email sender; duplicate dispatch does not send twice. The overnight run includes a machine-suspension interval and is not performance evidence.
- Three additional Records access-review/renewal regressions, eight credential-request Domain cases and 30 Onboarding HTTP contracts pass. The latest serialized Host build passes with zero errors. Earlier fixture/DI failures and tracked-profile self-invalidation were fixed before the final passing gate; topology/stamp checks were retained.
- Evidence: `.artifacts/onboarding-email-complete.log`, `onboarding-email-records-regression.log`, `onboarding-email-domain-final.log`, `onboarding-email-contract-final.log` and `onboarding-email-build-final.log`; the Application TRX is `test/ShikshaMitra.Application.Tests/TestResults/onboarding-email-complete.trx`.
- The reviewed localhost Host was restarted with seeders, migrations and delivery workers disabled. A synthetic Student email add/update was saved through the UI; a read-only database check found zero Student links, zero credential requests for that case and both existing parent-child links retained. Its pinned legacy Student access remains Not needed, and Send stays blocked rather than silently enabling access.
- No migration or Finance change is required. Actual owner-mailbox verification, real SMTP delivery, production-provider concurrency and deployed acceptance are not established by these isolated tests. Production must execute the existing durable ABP background jobs with the correct frontend client URL; local delivery remains intentionally disabled.

## Retired staff ownership and handling history

Run `AdmissionRetiredOwnership_Tests` domain cases and the application `Retired_ownership` scenarios. Retained regressions cover public capture/replay, published form and creator scope, follow-up notes and concurrency, workflow transitions, and current Onboarding completion. New history must show the authenticated action actor, never a designated case owner. Verify legacy assigned-only policies cannot block permitted staff; keep tenant/academic isolation, lifecycle gates and Finance assertions.

Focused verification passed six domain cases and 56 application scenarios across the main run, corrected public-link fixture rerun and legacy unmatched access check. Coverage includes owner-free follow-ups, retained scope/concurrency, public capture replay, and finalization/Onboarding handler history. A real local inquiry and follow-up were also persisted without ownership and returned the authenticated staff name.

Local automated checks do not establish staging or production acceptance. Browser verification must use current Host binaries and a real API; no simulated persistence is acceptance evidence.

## Application numbering

`AdmissionApplicationNumbering_Tests` covers formats, reset/year compatibility, numeric ranges and monotonic counters. The `Numbering_` scenarios in `AdmissionWorkflowRuntimeIntegration_Tests` cover persisted legacy/new numbers, automatic APP-to-school-prefix continuation (50 → 51), non-consuming actual-next previews, RTE/Management/Staff registration, replay, revision edits, stale saves, locked sequence settings, academic/calendar/continuous scope, configured minimum and tenant isolation. `Numbering_postgres_http_settings_concurrent_allocations_rollback_and_tenant_isolation` uses the existing disposable loopback `STUDENT_RECORDS_APPLICATION_POSTGRES` harness to exercise actual GET/PUT/POST preview validation, repeated non-consuming preview, twelve parallel allocations after legacy 50, rollback and tenant isolation. Preview retains View authorization at both controller/service entry points; anonymous calls must fail. These are local checks, not staging/production proof.

## Class-wise combined assessment subjects (2026-10-21)

Focused local validation covers nullable subject drafts, all-subject final submission, all three pass rules, whole-exam retakes, immutable booked profiles, mixed-class shared sessions, Offering profile coverage, scoped catalog lookup and Academic subject-reference protection. A started subject attempt remains non-cancellable after its marks are cleared; an untouched blank draft remains cancellable. Reference protection accepts the same nested JSON property casing as the policy reader.

| Check | Current local evidence |
|---|---|
| Domain Assessment filter | 72 passed after the cancellation audit fix |
| Application Assessment and Subject usage regression | 38 passed; the three provider-only cases were subsequently run separately |
| Draft/cancellation and case-insensitive subject usage audit | 5 passed |
| Workflow/Assessment HTTP contracts | 25 passed |
| Assessment EF mapping | 1 passed |
| PostgreSQL roster and resource-lock checks | 3 passed, no skips, using new guarded disposable loopback databases |

TRX evidence is under the corresponding test projects' `TestResults/assessment-subjects-*.trx`. Builds use `.artifacts/assessment-subjects-test`, serialized MSBuild and disabled shared compilation. The latest Host leaf build passed with zero errors; existing dependency warnings are not treated as a full solution/analyzer gate.

The additive `20261021100000_AddAdmissionAssessmentSubjectScores` migration was reviewed and applied only to the verified localhost `shikshamitra_local` database. EF reported no pending model changes. No application database was dropped or reseeded by this work. Final browser QA used the user's debugger Host after the duplicate background test Host was stopped to free its ports.

Authenticated UI saved/reloaded and published `QA Subject Admission 21Oct` with Class 5 (three subjects, 100 marks, overall and per-subject passing) and Class 1 (two subjects, 40 marks, overall passing). Both class Offerings in the separate `QA Subject Assessment 21 Oct` cycle are Open/Ready. Synthetic applications `APP-2026-000006` and `APP-2026-000007` were created through staff and public inquiry flows, respectively, then booked into one shared session and marked Present. Read-only persistence checks confirmed distinct frozen profiles, a Class 5 partial draft with Mathematics zero and the other scores null, and final server-calculated outcomes: Class 5 failed at 71/100 because English 11 was below 12; Class 1 passed at the overall boundary 16/40 with one subject zero. The Class 5 retake request saved; an overlapping new appointment was correctly rejected by both preview eligibility and booking validation.

The non-overlapping Class 5 retake booked with fresh nullable scores, retained the first attempt and respected the session-start attendance/result guard.
An Applicant 360 partial draft saved `20 + 8 + null`; final submission at `20 + 8 + 12 = 40/100` passed both configured thresholds.
Both applicants then completed Form, Document verification, the existing Finance handoff and Admission Decision through normal Student finalization; no bypass or direct database write was used.

Read-only final audit `.artifacts/assessment-subjects-final-qa.json` confirms both applications have AdmissionFinalized status, five completed stages with completion timestamps, saved Student/Onboarding case links and explicit payment deferral.
For `APP-2026-000006`, one accepted synthetic Birth certificate PDF is linked to Student and the frozen Onboarding document step is Required + Completed.
For `APP-2026-000007`, the required Birth certificate is frozen in Onboarding with zero evidence and a Required + Pending step, preserving the existing activation blocker.
Both have null StudentEnrollmentId and zero enrollment rows; no activation, payment posting or credential delivery was performed.
The user's debugger Host served the final tests after the duplicate background test Host was stopped to free ports.

The existing legacy Npgsql timestamp read/write convention was diagnosed read-only; it requires a separate compatibility audit before any global converter or historical-data change.
The in-app browser's native inline PDF rendering remains uncertified; upload, downloaded-byte equality and document acceptance passed.
Frontend evidence belongs to [frontend testing](../../../../Shikshamitra-FE/docs/student/testing.md).

## Activation readiness synchronization (2026-10-18)

`test/ShikshaMitra.Application.Tests/Students/Enrollment/StudentOnboardingActivationPreview_Tests.cs` covers stale stored Finance steps with both current Onboarding confirmation and fully paid Admissions evidence, activation and idempotent retry, unchanged preview history/stamps, and rejection of unconfirmed fees or an expired seat. The focused activation/regression filter passes 9 cases; the existing Admission fee-confirmation reader suite passes 17 cases. `StudentOnboardingApiContract_Tests` passes 26 route/permission contracts, retaining View for preview and Activate for the command.

The rebuilt local Host also passed an authenticated in-app activation: one active enrollment, committed seat, retained GR/Roll/Division and refreshed persisted Finance readiness. Required guardian App Login remained pending under the existing manual policy; no credentials were sent or permissions changed. These results establish local behavior, not staging or production acceptance. See [frontend checks](../../../../Shikshamitra-FE/docs/student/testing.md#activation-dialog-readiness-2026-10-18).

## App Login settings (2026-10-16)

The focused Application run passes 18 cases with zero failures/skips: six new App Login scenarios plus the 12 retained one-click cases. It covers settings persistence, Optional completion and post-completion preparation, Not needed denial, both automatic timing stages, explicit parent selection, pending delivery worklists, unchanged school flags, owner drift, permissions/scope and idempotency. A new automatic-parent scenario reproduced a duplicate unflushed queue intent; flushing the queue inside the existing transaction resolves it. Domain readiness/request tests pass 28 cases, covering enum validation, boolean compatibility, three-state snapshots and retained request transitions.

The additional credential delivery/eligibility/input regression cohort passes 36/36 cases with zero failures/skips, including transport retry, password preservation, duplicate worker runs, revoked links/grants/scope, cross-tenant denial, saved-recipient provenance, published documents and completion timing.

These tests use isolated current-source assemblies, synthetic SQLite fixtures and mocked transport; they do not certify real mailbox delivery or deployment. No schema migration is added.

The local follow-up rebuilt and restarted the Host on 44332/5000 using the existing Development launch profile and current isolated outputs. Authenticated settings PUT/GET verified manual delivery, account access values, invalid-enum rejection and `Students.Onboarding.ConcurrencyConflict` rejection (the existing HTTP mapping is 400). The exact original configuration was restored and re-read after testing, including all numbering/history fields.

Live verification exposed a filter-options read materializing recursively auto-included academic entities. Scalar projections now retain only the scoped IDs, labels and parent links. `Onboarding_filter_options_uses_scoped_scalar_academic_lookups` passes, checking labels/parents, an empty unrelated year and no tracked Board/Standard graphs. For the one-case local fixture, the earlier request took 131.626 seconds; the first post-restart read took 8.685 seconds and three subsequent samples took 363, 148 and 100 ms. These are local observations, not a controlled benchmark or p95; query counts were not measured.

Real credential delivery remains unverified: the current case has no saved recipient email, its seat reservation is expired and the eligible published fee-structure list is empty. An attempted explicit send correctly returns `Students.Onboarding.PortalAccountInvalid` without creating an account/link or email intent. No activation, fee setup, payment or real credential email was performed; Finance source and configuration were left untouched.

## Student permission regression

Run `StudentModuleVisibilityPermissionContract_Tests` for the complete canonical catalogue and `PermissionConsistencyApi_Tests` (including its `StudentPermissionConsistencyApi_Tests.cs` partial) for role/user grants, revocation, Development behavior and legacy denial compatibility.
Run `StudentActionPermission_Tests` for direct-call denial before protected reads/writes, and the Records authorization suites for canonical action mappings.
Attendance API tests must cover missing Add with a valid assignment, Add with an invalid assignment, assigned-teacher marking and lock/unlock, and assignment revocation.
Roll batch tests must permit an authorized scoped operator without an administrator role while preserving approval and concurrency rules.
Run application/API suites serially on memory-constrained workstations; local fixtures do not certify staging or production grants.

The historical permission baseline, before the repair below, passed 52 Domain tests and 108 of 109 HTTP tests (zero skips). All 84 selected Student checks passed: 31 Attendance, 25 Onboarding contracts, 14 Records authorization, one Attendance Settings check and 13 Student grant/deny compatibility cases within Permission Consistency. The sole failing HTTP case is the unchanged HR `Esi_employee_actions_do_not_require_employee_profile_editing`: its ManageEnrollment-only caller updates enrollment but the existing response read requires StatutorySchemes.View (expected 200, actual 403). No HR source changed. Evidence: `.artifacts/student-permission-http-final/student-permission-http-final.trx`.

The final focused Application run passed 67/67 tests, zero skips: direct action denials, registration lifecycle action separation, Records authorization, roll batches, Onboarding settings/class/year scope and batch grant reads. The first run's 13 fixture-startup failures came from accidental conventional registration of a private test harness; excluding that manually constructed harness from registration fixed the test setup, and the complete selected suite passed on rerun. Evidence: `.artifacts/student-permission-application-r2/student-permission-application-r2.trx`.

The isolated HTTP/Application builds compiled the complete current production source, including migration history, without source exclusions; shared compilation and build analyzers were disabled to fit workstation memory. This is not a full analyzer or staging/production gate. Existing developer servers and live permission assignments were not changed. The latest backend source must be loaded by restarting the developer Host before browser acceptance of the final scope changes. Retired specialized positive grants require explicit canonical reassignment during the coordinated frontend/backend rollout; no live grants or seed data were widened.

## Student security follow-up (2026-10-16)

The baseline notes below describe the earlier diagnosis. The pending-check repair section records the final local status.

Teacher attendance reads now intersect active employee-linked ClassTeacher assignments with the existing tenant, academic-year and UserScope constraints. The roster checks assignment before loading student identities/statistics; history applies assignment filtering before count/paging. Database-configured administrators retain their existing scope exception and still require the action permission. An assignment removed after login takes effect on the next request.

The fresh HTTP selection initially passed 116 of 117 cases. The remaining positive-history fixture lacked the now-required teacher assignment; both historical fixtures were made explicit and the shared synthetic employee identifiers made unique. The final three-case scope rerun passes **3/3**, including assigned history, rejection/filtering of unassigned history and immediate roster/history revocation. This is overlapping evidence across runs, not a claim that the complete suite passed in one run. Evidence: `.artifacts/student-security-followup/results/student-followup-final.trx` and `student-scope-r2.trx`. The independently known HR ESI failure from the earlier permission run is excluded from that 117-case selection and remains open.

Normal OAuth against the isolated PostgreSQL clone confirms an assigned teacher receives 200 with three students for Class 3A and 403 for unassigned Class 3B despite broad generic UserScope. User override checks verify Deny over role Add, Allow over a View-only role, persistence through role edits, Follow role removal, View denial and denied self-management. All 34 request/state checks pass and temporary overrides/role changes are restored in cleanup. Evidence: `.artifacts/student-role-browser/followup/Overrides.json` and `ReadScope.json`. No original database grants are changed.

Locked correction application/review now checks MarkAttendance.Add instead of Delete, in addition to LeaveAndAbsent.Add and existing scope/revision/separate-reviewer rules. This keeps sensitive updates under Add/Update as approved. The Application and complete HTTP test project leaf builds pass with no source exclusions; shared compilation/analyzers and dependency recompilation are disabled for workstation memory. The HTTP output's Application DLL hash matches the rebuilt leaf output. This is a focused build, not a full solution/analyzer gate.

The final correction selection passes **3/3**, zero skips: Add-versus-Delete authorization on a locked register, retroactive leave/credit review and requester self-review rejection. Evidence: `.artifacts/student-security-followup/results/student-correction-final.trx`.

The final override/stale-correction selection passes **4/4**, zero skips. All three canonical Student actions (View, Add and Delete) independently verify exact role grants, sibling-action denial, user Deny, Follow role, role revocation, direct Allow and removal of that Allow. The fourth case rejects correction approval after leave-source drift without mutating the submitted record/pending request. Evidence: `.artifacts/student-security-followup/results/student-overrides-final.trx`.

Report-release verification exposed an obsolete extra environment argument in the test harness's manually constructed permission checker. Removing that obsolete argument (and the unused fake environment) makes the full **8/8 report-release cases pass**, zero skips, using the real checker/providers. Coverage includes persisted read/generation grants, stale role claims after database membership removal, stricter masking, expiry, queued jobs and revocation after actual workbook generation before blob publication. Production report/authentication logic is unchanged. The repaired test project compiles separately and runs with copied dependency outputs; its Application DLL hash matches the fresh leaf build. Evidence: `.artifacts/student-security-followup/results/student-report-release-final.trx`.

The [frontend follow-up](../../../../Shikshamitra-FE/docs/student/testing.md#student-security-follow-up-2026-10-16) records the landing/Validated Edit fixes, actual normal-login override browser checks and responsive proof.

Remaining workflow triage must distinguish fixtures from product behavior. Retired stage/Review-command failures stop before their intended operation. Roll school-mode fixtures fail while saving numbering settings because they create a rule without the required saved Draft/publication history. The three Directory provisioning assertions conflict with the current activated-enrollment eligibility query, which includes an onboarding case linked to a real enrollment before overall case completion; that visibility contract needs reconciliation rather than a blind assertion change. Document readiness assertions also require investigation. A broad failing run is not proof that every named production workflow is broken, and focused green permission checks are not proof that the entire Student module is accepted.

The historical broad serial Application snapshot, before repair (`FullyQualifiedName~ShikshaMitra.Students`) completed in 58 minutes 47 seconds: **856 total, 679 passed, 173 failed, four skipped**. All eight report-harness failures are cleared by the separate repaired 8/8 run above, putting **165 snapshot failures in the then-pending backlog**. These overlapping cohorts are not a single clean full-suite result. The later repair build also discovers one concurrently added filter-options test; that additional case passes **1/1**, zero skips, checking scoped scalar academic lookups, hierarchy labels/parents, an unrelated year and no tracked Board/Standard graphs. Evidence: `.artifacts/student-security-followup/results/student-workflows-final.trx`, `student-filter-options-final.trx`, `verification-summary.json` and the exact remaining names/messages/stacks in `workflow-failures.json` under `.artifacts/student-security-followup/`.

| Historical repair group | Cases | Repair at that checkpoint |
| --- | ---: | --- |
| Retired Admissions stages/handlers | 138 | Modernize Eligibility/Review/Offer journey fixtures and expected errors to supported stages, then rerun. |
| Onboarding collection prerequisites | 8 | Supply approved collection/defer prerequisites and reasons in fixtures; preserve the existing fee guard. |
| Numbering Draft/publication fixtures | 7 | Save Draft and publish versioned rules before the target Roll operations. |
| Directory activation visibility | 3 | Reconcile activated-enrollment visibility with the older provisioning-only expectations. |
| Admission decision prerequisites | 2 | Complete preceding stages and activate the decision stage before recording a decision. |
| UTC instant persistence | 1 | Investigate `SubmittedAt.Kind` after database reload. |
| Other workflow assertions/setup | 6 | Investigate academic collection filtering, cycle reference configuration, applicant lookup, document requirement/readiness and onboarding account-required assertions. |

The four skipped cases require the disposable PostgreSQL connection for workflow publication locks, Assessment roster scale and two resource/interviewer lock checks. Their provider run was pending at that checkpoint; current repair results are recorded below. Those historical failures and skips were release verification work, not 169 confirmed production security defects.

The historical gates below are superseded individually by the pending-check repair. Local clone and SQLite results do not establish staging/production acceptance. A work-remaining percentage cannot be derived reliably from overlapping test pass rates.

### Pending-check repair (2026-10-16)

The earlier 856-case snapshot and 165-case triage backlog are historical. Retired Eligibility/Review/Offer success fixtures now use supported journeys or explicit rejection/no-mutation contracts. Removed obsolete cases are not counted as passes. Fixtures retain Finance ownership, collection deferral reasons, confirmed published fees, real enrollment visibility, published numbering rules and concurrency/scope checks.

| Local verification gate | Current result |
| --- | --- |
| Student Domain, role seeds and deployment contract | **634/634 passed**, zero skipped (`student-domain-source-final-r2.trx`). |
| Full Student Application regression with PostgreSQL | **806/806 passed**, zero failed/skipped, including all four PostgreSQL cases; 50.20 minutes (`student-full-final-r2-postgres.trx`). |
| HTTP permissions/ESI | **48/48 passed**, zero skipped on the current Domain DLL (`student-lifecycle-security-http-current-domain.trx`). |
| Updated contracts and modern PostgreSQL | **75/75 passed**, zero skipped (`student-postgres-fixture-modern-r3.trx`). |
| Legacy-mode PostgreSQL | **4/4 passed**, zero skipped (`student-postgres-fixture-legacy-r3.trx`). |
| Configured frontend security regression | **138/138 passed**, 17 suites. |
| Additional lifecycle/report UI regression | **100/100 passed**, ten suites; focused TypeScript reports zero diagnostics across 21 files and typed ESLint passes 17 files. |

PostgreSQL checks cover publication serialization, Assessment scale and both room/interviewer locks on fresh loopback databases. The roster fixture now uses production UTC conversion in modern and legacy Npgsql modes. At 100/1,000/5,000 candidates, the 25-row page used 14 SQL commands and 121 expanded history rows; warm reads were 87.8/152.7/573.4 ms in that local observation. This is bounded query evidence, not a production load benchmark.

Unsupported workflow create/update/clone/version input is rejected before definition mutation. SQLite timestamps preserve UTC kind while PostgreSQL storage remains unchanged. ESI updates use a private response reader after ManageEnrollment and scope checks; public GET retains View.

Close/archive/deactivate, cancellation, withdrawal/release/reversal and document void use the owning Add/Update action in UI, controllers, services and aliases. Actual removal retains Delete. Legacy cancellation denials also constrain Add without losing their former Delete denial. Attendance AI report entry requires Reports.Add and closes after revocation. No live grants were widened or seeders executed.

Normal OAuth against the disposable clone passes the 34-check restored user override matrix, 15 lifecycle checks, three additional Onboarding cancellation checks and three assignment-read checks: assigned Class 3-A returns 200 with three rows, while unassigned Class 3-B returns 403 despite wider generic UserScope. Browser checks verify saved form create/update/delete, direct denial without View, 21 loaded read/control screens or tabs, and dark/responsive layouts. Add/Update exposes Offering Deactivate without Delete; Delete-only excludes lifecycle updates. See [frontend browser evidence](../../../../Shikshamitra-FE/docs/student/testing.md#student-security-follow-up-2026-10-16).



The final case-drawer browser recheck confirms Add/Update exposes enabled Onboarding cancellation; Delete-only hides Case Settings/cancellation and disables readiness refresh. No case was cancelled. Identity directory lookup returns 403 for the Student-only Add fixture: the current owner remains visible, while selecting another staff member requires existing Identity directory read authority. This related permission was not auto-granted. Existing roles also need canonical-grant review during coordinated rollout.

CI callers now gate deployment on their reusable security workflows while retaining main-only execution. Backend startup/profile validation runs before DbMigrator; explicit FullDemo preserves the existing default. No migration, real email, payment or deployment was performed. Dedicated QA servers/tabs were closed; regular developer servers were preserved. Target-host refresh, staging/deployment, actual delivery and production performance remain separate gates. Legacy report preview/export lacks a matching backend, and Notifications retain existing placeholders.

Evidence: `.artifacts/student-pending-repair/results/`, production PDB/source checks and Application test source checksums, plus frontend `.artifacts/student-security-followup/`. Cancelled diagnostic runs and no-test discovery attempts are excluded from pass counts. Overlapping cohorts are not summed into a module-completion percentage.

## Roll-number flow validation (2026-10-16)

| Focused gate | Coverage |
|---|---|
| `StudentRollOrdering_Tests`, `StudentRollNumberBatch_Tests` | 23 passing Domain tests: grouping, direction, name format, natural numeric ordering, missing inputs, deterministic ties, expiry and approval lifecycle. |
| Application methods `Roll_batch_` | 6 integration scenarios: complete server cohort and immutable paging, append-after-exit cursor, nullable historical placements, committed replay, stale roster rejection, swaps/datetime history, independent batch approval, canonical Add authority with assignment scope and tenant bounds. The current permission revision replaces the earlier administrator-only expectation. |
| `StudentRollNumberBatchContract_Tests` | 11 passing route/input/error-mapping checks; complete server cohorts cannot be supplied by the client. |
| `Roll_batch_Postgres_` | Explicitly skipped without `STUDENT_RECORDS_APPLICATION_POSTGRES`; the existing factory only accepts a fresh disposable loopback `records_tests_*` database. The gate checks JSONB preview round-trip, stale competing proposals, replay and persisted final rolls. |

Run Application and HttpApi tests serially with isolated artifacts, `UseSharedCompilation=false` and bounded build memory.
Local focused compilation excludes generated migration Designer history through the existing ignored validation target; it compiles the current model, snapshot and migration bodies, not every historical Designer.
The Student-only migration adds the batch table and indexes; the existing model snapshot has only that entity addition.
No live school database migration, production PostgreSQL concurrency run, real-student mutation, staging deployment or Finance change is established by these local checks.
Final Application compilation has zero errors; existing warnings remain.
The protected Finance baseline matches all 755 backend files; the matching frontend check covers 824 files.

## Optional Payment collection and audited deferral

Run the Domain `AdmissionFeeCollectionDeferral_Tests` and Application `Payment_deferral_advances_with_or_without_structure_and_retry_and_finalization_preserve_reason_without_finance_rows` cases. They cover required/trimmed/bounded reasons, active-stage validation, stale concurrency, repeated workflow keys, truthful transition facts, normal approval/finalization, Onboarding reason projection and unchanged Finance accounts, assignments, dues and collection rows. These source-level checks do not establish deployed browser acceptance or migration application.

## Finance scope cleanup (2026-10-13)

Local admission fee-account, collection, receipt and refund extensions were removed. Existing Finance source, seeders, dashboard projection, localization and all 70 Finance snapshot blocks match the current branch baseline. Admissions and Onboarding retain published fee-structure selection and confirmation through their existing contracts; finalization and activation retain Student ownership without the removed collection-linking hooks. The Student custom-profile migration and UTC/provider changes remain.

The full solution compiled with zero errors using isolated outputs, serialized compilation and analyzers disabled; existing compiler warnings remain. Focused checks passed 87 Application cases for automatic Onboarding, Credentials, special registration and inline Records, 38 Attendance/Directory/Records HTTP cases and five UTC/provider cases, with zero failures or skips. Student docs and Git whitespace checks passed. All-module docs validation stops on an unchanged encoding issue in `docs/modules/finance/references/charge-management-e2e-test-plan.md`.

Logs, TRX reports, both repository backups and the preservation/boundary audits are under `D:/CodexArtifacts/student-finance-scope-cleanup-20261013-1791913960166/`. Checks used fresh isolated assemblies and synthetic test hosts. No database migration, normal school-data mutation, real payment, email, deployment or authenticated browser journey was performed for this cleanup.

## One-click Credentials (2026-10-13)

The final focused Application run passed **50/50 cases**, with zero failures or skips. It includes 12 one-click command scenarios, saved-recipient/DTO validation, credential dispatcher regressions, current enrollment/identifier/document eligibility and retained idempotent portal preparation. The required Offering-document scenario checks actual published evidence and rejects onboarding overrides. Two older identifier fixtures were repaired to use the current activation prerequisites and a persisted alternate Division; production eligibility guards were not weakened.

The Domain request-state gate passed **7/7 cases**, with zero failures or skips. It covers completion-time deferral and clearing the manual-intent marker when a new legacy request is made. Reports are `D:/CodexOnboardingValidation-20261013/credentials-results/credentials-accepted-final.trx` and `credentials-domain.trx` under the same directory.

The real PostgreSQL HTTP gate passed **1/1 case**, with zero failures or skips (`credentials-postgres.trx`). Its case-specific outbox trigger fails only after account, link, handoff and case changes have been flushed. The failed HTTP command rolls all of those changes and its audit events back; retrying with the original case stamp persists exactly one account, link and marked outbox request. Replaying the same request does not duplicate them. The saved automatic-email settings remain off and unchanged, while a completed case retains its pending Credentials task.

The PostgreSQL factory creates a fresh loopback `records_tests_*` database from the current model, disables background jobs/workers and uses synthetic records. Its permissive test grants establish transaction/HTTP persistence behavior, not production authorization; direct-service grant, tenant, academic scope and owner-provenance denial cases are covered by the Application cohort. It does not run a full Admission or Finance journey. The Application command fixtures separately use the actual Admission/finalization/activation handoff, with a mocked email sender.

Run the Application scope with:

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~One_click_credentials|FullyQualifiedName~StudentCredentialsValidation_Tests|FullyQualifiedName~StudentCredentialEmailDispatcher_Tests|FullyQualifiedName~Credential_email_rechecks|FullyQualifiedName~Onboarding_portal_accounts_are_persisted_idempotent_scoped_and_passwordless" --artifacts-path D:/CodexOnboardingValidation-20261013 -m:1 -nr:false -p:UseSharedCompilation=false
```

The HTTP filter is `FullyQualifiedName~One_click_credentials_postgres_outbox_failure_rolls_back_flushed_account_and_same_stamp_retry_persists_once`; it requires `STUDENT_RECORDS_APPLICATION_POSTGRES` naming a fresh loopback `records_tests_*` database and refuses an existing or nonlocal database.

These checks freshly compiled current Application, Contracts, Domain, EF, HttpApi and Host dependencies to isolated outputs with serialized builds and analyzers disabled. The validation-only target excludes generated migration `*.Designer.cs` files for memory reasons while retaining migration classes and the current model. This is not a normal full-solution/analyzer or migration-history pass. No schema migration, Finance source change, real portal account/password/email, normal school-data mutation, deployment or inbox delivery was performed. Existing development servers were not stopped or restarted. Latest-source authenticated browser acceptance remains unverified because the frontend localhost port was unavailable; [frontend testing](../../../../Shikshamitra-FE/docs/student/testing.md) records focused UI results and the four unrelated retired readiness-screen fixture failures.

## Inline retained Registration form (2026-10-13)

`Inline_form_parent_contact_edit_creates_contact_only_parent_and_preserves_original` covers adding a mother's mobile number through the retained form when no mother profile/name exists, both with and without an existing family profile. Its retained schema also includes unanswered `Identity.DocumentName` and profile-photo questions. Current form projection must supply `null` for these absent answers before validation/mapping without adding answers to the original submission. It verifies Applied status, contact-only profile reload, preservation of the existing father, current-value projection and unchanged original evidence. Run with `FullyQualifiedName~Inline_form_parent_contact_edit`; this isolated Application test does not establish a successful save in a deployed school database.

`Inline_form_postgres_http_creates_contact_only_mother_preserves_father_submission_and_audit` exercises the actual HTTP snapshot/save/replay/readback endpoints using a fresh loopback database through `StudentRecordsApplicationPostgresTestFactory`. It preserves the existing father, empty guardian, main-contact selection and immutable submission, and asserts one Applied correction and guardian review. The self-contained frozen form includes unanswered document-name and photo questions. Optional `STUDENT_RECORDS_INLINE_FORM_DEFINITION` accepts schema-only diagnostic metadata; fixture IDs and defaults are replaced and all answers remain synthetic. This current-model HTTP gate does not validate deployed migrations, Azure assemblies or a live school save.

The focused Application gates passed **27 existing custom-record regression cases** and **18 final inline-form/action-permission cases**, with no final failures or skips. The latter contains ten inline scenarios, three direct-form permission denials and five canonical document action denials. It covers atomic mapped/custom save, immutable original submissions, actor/reason/before/after audit, stable retry, stale snapshot rejection, unchanged generic approval policy, retained schema across year context, masked-field storage preservation, repeated hidden answers, nullable/identity/placement preservation, binary summaries, newly-required row rules and empty required structured answers.

The final Application report is `D:/codex-artifacts/student-records-inline-form/test-results/student-record-inline-form-final.trx`; the prior custom regression report is `test-results/student-record-inline-form.trx` under the same artifact root. Its broader first run also contained one inline fixture failure from inserting a second unique school configuration, corrected before the final passing rerun.

The fresh HTTP metadata gate passed **3/3 cases**, zero failures/skips, covering existing Details authority, additive full-form fields and exact snapshot/save routes with read/create grants. Report: `test-results/student-record-details-api-contract-final.trx`. Fresh HttpApi and Host builds completed with zero errors; HttpApi has three existing nullable warnings and Host has no warnings. These metadata cases do not start an authenticated network flow.

Application, Domain and Contracts were compiled to isolated outputs, with serialized compilation, shared compilation and analyzers disabled. Dependent projects reuse the already compiled isolated EF assembly because this change adds no entity/model or migration. These are focused test-host gates, not a full-solution, analyzer, PostgreSQL race, staging/production or authenticated browser save pass. The existing development API is not restarted by these checks.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path D:/codex-artifacts/student-records-inline-form --no-restore -p:BuildProjectReferences=false -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:RunAnalyzersDuringBuild=false --filter "FullyQualifiedName~Inline_form_|FullyQualifiedName~Record_document_direct_service"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path D:/codex-artifacts/student-records-inline-form --no-restore -p:BuildProjectReferences=false -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:RunAnalyzersDuringBuild=false --filter "FullyQualifiedName~StudentRecordDetailsApiContract_Tests"
```

## Automatic onboarding requirements (2026-10-13)

### Scoped worklist query checks

`StudentOnboardingWorklist_Tests.cs` covers combined name/owner/placement/Roll filters, matching counts before paging, duplicate-name stable ordering, academic/tenant isolation and rejected operators/ranges/sorts.
`StudentOnboardingWorklistPostgres_Tests.cs` covers actual PostgreSQL translation, HTTP query binding, stable pages and restricted staff scope through the existing disposable application factory.
Run the Application methods with `FullyQualifiedName~Onboarding_worklist`; the HTTP method filter is `FullyQualifiedName~Onboarding_worklist_postgres_filters_scope_counts_and_stable_sorts_before_paging`.
The HTTP scenario requires `STUDENT_RECORDS_APPLICATION_POSTGRES` with a fresh loopback `records_tests_*` database name and refuses existing or nonlocal databases.
Use isolated artifact outputs while the normal development Host is running, and keep these read-query gates separate from activation, identifier-write, Finance and browser evidence.

The 2026-10-13 Student Setup worklist gate recorded **5/5 Application tests** and **1/1 PostgreSQL HTTP test**, with zero failures or skips. Those historical local TRX files are no longer retained; rerun the focused filters above for current evidence.
The PostgreSQL factory created a fresh loopback database, seeded mandatory steps through the current requirements owner and persisted real seat reservations; no normal school database, activation, Finance payment, account or email was changed.
The identifier/case/capacity Domain regression also passed **76/76 tests**, zero failures/skips. Its filter is `FullyQualifiedName~StudentOnboardingIdentifierRules_Tests|FullyQualifiedName~StudentOnboarding_Tests|FullyQualifiedName~AdmissionSeatAllocation_Tests`; the report is `D:/CodexOnboardingValidation-20261013/results/setup-identifier-capacity-regression.trx`.

These runs freshly compiled the current source with serialized builds, isolated outputs and analyzers disabled. A validation-only MSBuild target excluded generated migration `*.Designer.cs` sources after the ordinary compile exhausted available memory; regular migration classes and the current model snapshot remained included.
This establishes current-model query translation and focused runtime behavior, **not** a normal full-solution, analyzer, migration-history, deployment or live saved-assignment pass. No production project/source configuration was changed for that compilation scope.
Logs are `.artifacts/onboarding-worklist-application-gate.log`, `.artifacts/onboarding-worklist-postgres-gate.log` and `.artifacts/onboarding-identifier-capacity-regression.log`; the normal development Host was not stopped or restarted by this worklist gate.

`AutomaticOnboardingRequirements_Tests.cs` extends the partial `AdmissionWorkflowRuntimeIntegration_Tests` class; filter its `Automatic_onboarding` methods rather than the source filename.
The scenarios cover no-profile admission document blockers, atomic failure on undetermined published conditions before Student creation, idempotent pending-case adoption, retained saved delivery/profile metadata and immutable enrolled/terminal snapshots.
`AutomaticOnboardingRegression_Tests.cs` adds unchanged-preview evidence/stamp preservation, missing actual Student identity blockers, snapshot-less historical reads with dormant profile conflicts and the positive guardian-account completion journey.
`StudentCredentialReadiness_Tests` covers account requirements independently of dormant email preferences and new-school login defaults.
Run isolated outputs when the normal development Host is running:

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Automatic_onboarding" --artifacts-path D:/CodexOnboardingValidation-20261013 -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~StudentCredentialReadiness_Tests|FullyQualifiedName~StudentOnboarding_Tests" --artifacts-path D:/CodexOnboardingValidation-20261013 -m:1 -nr:false -p:UseSharedCompilation=false
```

Final local verification passed **18/18 Application tests** (`Automatic_onboarding` plus `StudentOnboardingSettingsValidation_Tests`) and **54/54 Domain tests**, with zero failures or skips. Reports are `D:/CodexOnboardingValidation-20261013/results/automatic-requirements-final-r4.trx` and `onboarding-domain-final-r2.trx`; earlier reruns overlap and must not be summed.

The positive synthetic journey uses the current Form/Admission decision workflow, confirms the selected fee structure before placement, allocates GR/Roll, activates one enrollment, retains required guardian access as pending, provisions a verified saved guardian email and completes Onboarding with the existing Records handoff. A valid owner reset token establishes the first password; repeated staff provision/activation preserves the same user, link, enrollment and password. No Finance payment, credential-email request or actual external email is created by this test.

The serialized isolated Host build passed with zero errors (`WarningLevel=0`); this is not a claim that existing compiler warnings were fixed. Its Domain/Application copies match the tested assemblies by SHA256: Domain `F8AE1C06D7028C8CA7B9B6AC740B5798013BBE45D4B3A3091D41C3CB9C007DE3`, Application `55F61D08465B675C6498896C03378B76C24899D8912951C6A92ADEEF1A1F7388`. An initial overlapping Host build failed on a compiler-output lock; the successful retry ran after compilation had finished.

The task-owned final Host on `localhost:5001` disabled startup migrations, seeding, background jobs and workers. Authenticated settings save/reload succeeded and DDVS's original Student/Parent choices were restored. Scalar hierarchy validation avoids recursive academic AutoIncludes; the first cold save took 26.67 seconds, while the subsequent save took 183.95 ms. This is local timing, not a production performance guarantee. The existing normal development Host was untouched. [Frontend Student testing](../../../../Shikshamitra-FE/docs/student/testing.md) records UI and responsive evidence. No schema change, deployment or inbox delivery is certified by these checks.

## Attendance workspace alignment (2026-10-12)

`StudentAttendanceApi_Tests.Workspace.cs` covers Directory population parity, server paging/search (including numeric roll), attendance-only read permission, disabled dated eligibility, exact active teacher assignment despite broader scope/forged administrator claims, assignment revocation, Add-authorized assigned-teacher Lock/Unlock, year/tenant denial and full-roster review before Lock. The attendance test fixture creates real configured database administrator membership for administrator scope scenarios and restores it after each test; claims and generic staff scope are not administrator authority.

Run the API project with `FullyQualifiedName~StudentAttendanceApi_Tests|FullyQualifiedName~StudentDirectoryApiContract_Tests` and the Application project with `FullyQualifiedName~.Directory_|FullyQualifiedName~.Legacy_override_` using isolated artifacts. Directory scenarios are methods on the partial `AdmissionWorkflowRuntimeIntegration_Tests` class; the source filename is not a test-class filter. The Application regression gate protects the extracted shared Directory inclusion, hierarchy and identity-masking rules. Source tests and a test host do not prove staging or production acceptance; record actual local test and browser results separately.

The current isolated API gate passed **29/29 tests**, zero failures/skips, on 2026-10-12 (`.artifacts/attendance-workspace/results/attendance-workspace-api-r3.trx`). Shared Directory Application regression selected nine scenarios: six passed and three existing provisioning-mode assertions failed (`student-directory-regression-r2.trx`). Their unchanged fixture creates a persisted linked enrollment then expects Directory exclusion; the unchanged HEAD eligibility predicate includes that activated onboarding. The extracted default population predicate was compared with HEAD and is identical apart from whitespace. These unrelated lifecycle assertions remain unchanged; this is not a green Application-suite claim. Authenticated local browser proof is a separate gate; staging/production acceptance is not asserted.

## Parent-only sibling admission and onboarding (2026-10-23)

`StudentSiblingParentLinkedAccountRegression_Tests` covers the distinction between retaining a reviewed link and discovering an account eligible for a new sibling link. The confirmed-family first-manual-account case must retain one idempotent intent, dispatch through a substituted email sender and never acquire the sibling-reuse flag. Removing the original child's portal link must preserve the other child's exact link, parent password and portal visibility without another credential request. Current-owner email, role, active-state, relationship, verification and pinned-selection drift remain blocked. Keep the existing `Sibling_parent_onboarding_` cases for original setup recovery, tenant/scope/permission checks, legacy-source attestation, disabled access and non-relabeling. Staging response evidence and red/green test logs are retained only under ignored `.artifacts/parent-link-regression/`; a local fix does not establish staging deployment or real mailbox delivery.

On 27 October 2026, both new positive scenarios first reproduced their respective false review blocks. After the fix, 53 distinct focused cases were verified across the sibling-onboarding, retained-link, dispatcher and optional-guardian runs. The broader run initially passed 52/53; its new real-dispatch fixture lacked an absolute browser origin. Reusing the existing sibling retry fixture's test-only origin produced a final 3/3 regression pass without further production changes. Source-child link removal retained the target child's portal visibility and password; six owner/identity/provenance drift checks remained denied. Sanitized staging GET evidence, TRX files and the deduplicated verification summary remain ignored artifacts. No staging data, access grants, passwords or outbound email were changed by diagnosis.

The first-family fixture additionally records the source Father's saved admission and Student profile details while intentionally leaving that source without a portal account; its final rerun passed. An isolated Host build completed with zero errors and existing repository warnings. The running local Host was not stopped or replaced, and staging deployment/post-deployment acceptance remain separate gates.

`AdmissionSiblingParentFlow_Tests.cs` covers same-email/different-mobile suggestions, explicit source selection, both captured Father/Mother options, current Student parent projections, wrong-source rejection, unchanged shared parent contact, idempotent replay, authored Inquiry parent contacts, public trusted-selection rejection, non-parent role validation and selective matching among more than 2,000 pending applicants.
Compatibility cases retain canonical duplicate/replay behavior without a new first-name/last-name/date-of-birth hard block, and preserve legacy Guardian/Relative primary display names without offering those contacts as Father/Mother candidates.
The combined Application gate passed **53/53**, zero failures/skips (`test/ShikshaMitra.Application.Tests/TestResults/sibling-parent-final-verified.trx`). It includes 25 `StudentSiblingParentOnboarding_Tests` scenarios covering manual-first preparation, both-parent selection, tenant/scope/permission denials, historical exact-source reuse, contact/identity drift, required/optional late linking, original failed-setup recovery and production-DI dispatch cancellation. The real application-service journey preserves the parent password and returns both eligible children through the portal API without a sibling email intent. Controlled transport substitutes establish dispatch behavior, not real mailbox delivery.
The focused Admissions compatibility gate also passed **8/8** (`sibling-admissions-compatibility-final.trx`), including the four new name/DOB and legacy-header regressions. These cases overlap the combined gate and must not be summed as independent coverage.
After retaining the canonical Records return-authorization check in late linking, a freshly compiled focused gate passed **6/6** (`sibling-parent-late-link-return-guard.trx`). It verifies required/optional late linking, direct exact-grant denials, stale/tenant/scope/identity rejection and the additional invalid-return-transition regression before link, email, enrollment or password mutation. Five cases overlap the combined gate; the owned Onboarding test file now contains 26 executable cases.
Run `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter 'FullyQualifiedName~Sibling_parent_|FullyQualifiedName~Special_registration_requires_explicit_matching_family_choice|FullyQualifiedName~Basic_inquiry_workflow_should_require_interest_and_preserve_duplicate_handling'` using isolated artifacts.
The HTTP contract/security gate passed **42/42** (`test/ShikshaMitra.HttpApi.Tests/TestResults/sibling-parent-http-final.trx`): route/exact-permission contracts, anonymous and insufficient-grant denials, stamp/scope boundaries for original-setup retry and late parent linking, plus existing email-permission regressions. Provider-backed parameterized/composable query translation passed **2/2** (`test/ShikshaMitra.EntityFrameworkCore.Tests/TestResults/sibling-parent-sql-translation.trx`). The exact repository PostgreSQL query also executed successfully with bound parameters in a read-only local transaction; its zero-match result is a syntax/runtime smoke check, not a positive PostgreSQL family journey.
The local Host was rebuilt and restarted with migrations, seeding, automatic authentication, background jobs and workers disabled. No live admission, credential account or email was created by these automated gates. Authenticated browser save/reload, the controlled-mailbox persisted journey, true concurrent PostgreSQL race stress and Flutter refresh remain separate acceptance checks. Frontend checks are recorded in [Student testing](../../../../Shikshamitra-FE/docs/student/testing.md). Finance code is unchanged; no schema migration is required.

The subsequent user-authorized local browser journey created synthetic `Aarav SiblingQA`, Inquiry `INQ-2026-000012`, application `GR-2026-0009` (`3a247b73-c75e-7056-c934-0a8d3904e838`). The saved Inquiry form has no email field; Registration collected the controlled parent mailbox. Read-only PostgreSQL inspection found that the generic Registration contact email saved but the selected Father snapshot remained empty. Two conspicuously marked QA-only PDF fixtures passed the existing upload/review controls with synthetic-test audit notes. The existing published fee structure was confirmed and its permitted Collect Later action recorded; no payment or Finance configuration change has been made.

The contact-handoff correction passes **61/61**, zero failures/skips (`test/ShikshaMitra.Application.Tests/TestResults/sibling-first-parent-mapping-final.trx`): 19 helper/persisted mapping cases and 42 overlapping sibling/legacy cases. It includes Inquiry without email followed by generic Registration email and approved finalization, pre-fix submitted-record catch-up, Father/Mother-only canonical projection, explicit structured answers/clears before generic edits, pre-fix unmarked Inquiry answers, untouched Registration prefill exclusion, generic corrections/clears, and confirmed-sibling/legacy/return exclusions. Independent source security review is clear. A normal serialized Host build passed with zero errors and 191 existing warnings; no analyzer suppression was used for that build.

The guarded restart loaded the exact current Application assembly (`FA7197C897DA3ACE3B0EECA510E7B3CD5DB4166BBC6B33C2A192900D7E58DFAE`) into task-owned Host PID 47744. The same approved browser application then finalized successfully without reopening its submitted form. Read-only PostgreSQL evidence verifies its selected Father email now matches the controlled mailbox, Student `3a247b71-a134-0c78-7acb-74643b2b9662` and Onboarding case `3a247bc2-e667-2c79-3cb1-e3c7769aa420` exist, and mailbox identity, credential-request and Student portal-link counts remain zero. This proves the live pre-fix-record handoff repair and no automatic first credential intent; activation, manual delivery and actual sibling linking remain separate pending steps.

The first synthetic student subsequently activated through Save & Activate (Class 1/A, GR-005, Roll 2); PostgreSQL verified active enrollment and still zero identity/request/link counts before the user-authorized manual send. That click created exactly one passwordless parent identity/link and one manual Pending intent. Before transport, the user corrected the mailbox from the originally supplied address to `rajahir7222@gmail.com`. The exact delivery QA process was stopped and its untouched request `3a247bc5-fe28-03e2-1105-e680ac8c30e7` was cancelled through a narrowly scoped, transactional domain command after checking original staff permission, case scope, original identity/link and Pending/zero-attempt/no-password/no-sent state. Independent reads verify Cancelled, AttemptCount 0, SentAt null and no encrypted initial password; no email was sent. The old identity/password/link remain unchanged. A broad Family correction request was cancelled without applying because its form defaults proposed unrelated empty address changes. The user chose a fresh synthetic sibling pair with the corrected mailbox rather than bypassing Records approval; that replacement journey remains pending. No Finance change or payment occurred.

The corrected-mailbox replacement journey saved `Aarav MailQA`, Inquiry `INQ-2026-000013`, application `GR-2026-0010` (`3a247bd7-c874-6f81-7040-0e7468056257`). Registration Save & Next persisted both the selected Father and primary contact email correctly before finalization; no identity, email intent or portal link was created automatically. A separate authorized production `IAdmissionInquiryAppService.DuplicateCheckAsync` probe on actual PostgreSQL matched that exact Father/source Applicant with `CanLinkFamily=true`, `MatchByEmail=true`, `MatchByMobile=false` for a different submitted mobile. It checked the actual creator's permissions and family/application scope, verified `transaction_read_only=on`, and made no writes. Existing published Inquiry forms do not collect email and no Direct/Both offering exists, so this positive service/database evidence is distinct from the browser same-mobile sibling journey; school configuration was not changed to manufacture an email-input GUI path.

Fresh `Aarav MailQA` then finalized and activated normally (Student `3a247bd6-59a3-01c2-0492-671a70c6b717`, case `3a247bf0-a71b-e37f-c82e-0a5031900019`, Class 1/A, GR-006, Roll 3).
Independent PostgreSQL reads confirm active enrollment, the corrected Father email and zero mailbox identity/request/Student portal-link counts before manual sending.
Browser proof is `.artifacts/sibling-parent-browser/08-aarav-mailqa-active-manual-credentials-ready.png` in FE.
After explicit action-time approval, one manual browser click created one parent identity/link and request `3a247bf5-0550-d081-dec0-dc379440e405`.
A narrowly pinned local QA runner invoked the production dispatcher for that request only, with all general workers still disabled; it recorded Sent, AttemptCount 1 and SentAt `2026-10-23T17:25:59.902315Z`.
Independent database reads agree, the encrypted initial-password field is cleared, and the mailbox owner confirmed receipt; owner password setup remains pending and no email-confirmation/password-change flags were bypassed.
Fresh `Anaya MailQA`, Inquiry `INQ-2026-000014`, application `GR-2026-0011` (`3a247bfa-de6d-c4aa-1749-66a220686412`) used explicit saved-Father selection and Add sibling confirmation in the browser.
Registration reused the saved contact; independent reads verify the same family and unchanged Father/primary email and mobile.
Admission finalized through the normal checks (Student `3a247bfa-0329-8b5c-526b-3b65536ffe98`, case `3a247c02-6d54-8b5b-9d08-1c831165a08d`).
The first Save & Activate click was blocked by the parent-link authorization guard before any division, roll, enrollment or second portal-link write.
Read-only evidence established that admin-ddvs had the owning Onboarding action and Users.Create, but no exact Users.Update grant; this follows the existing portal/Records identity-reuse contract, not a new Add/Edit alias or administrator-role bypass.
After explicit approval, the normal Security user editor saved/reloaded only `AbpIdentity.Users.Update` as a direct U Allow for local admin-ddvs; no role-wide grant or seeder repair was used.
Persisted audit `3a247c10-1d5a-8af8-5ebc-241a8812aee6` at `2026-10-23T17:51:04.260229Z` returned HTTP 204 and records exactly that one Allow in both controller and service entries; independent reads confirm no role-provider Update grant or exact user denial.
Recovery would restore Follow role (`inherit`) for that one user/permission through the normal editor; no inverse was applied, and the explicitly authorized grant remains in place.
An interrupted sequential setup had saved GR-007/Roll 4 before activation; the existing safe resume preserved both identifiers and completed activation/linking after the permission refresh.
The final case is active in Class 1/A, with the same parent identity linked automatically and no second credentials intent.
An isolated read-only probe retained the original password hash and security stamp only in process memory between the first Sent email and the second activation; its final comparison passed and exited zero.
It verifies both fresh Father links and active enrollments in the same exact source family, the same one mailbox identity, unchanged password and security stamp, one Sent request with AttemptCount 1 and unchanged SentAt, and zero requests for the second child.
EmailConfirmed remains false and ShouldChangePasswordOnNextLogin remains true, matching the owner's stated pending setup; the portal's normal confirmation/password-change guard remains in place, and actual two-child owner login/Flutter visibility is not yet proven.

## Special-track offerings and Registration entry (2026-10-11)

`AdmissionSpecialRegistration_Tests.cs` covers General/RTE same-cycle/class coexistence, same-track duplicate rejection, selected-type bulk preview/commit, unused RTE updates, frozen used routing, and invalid enum rejection.
The focused special Registration scenarios exercise RTE/Management/Staff and retained Management Both entry, same-request retry, explicit sibling linking/mobile mismatch, inaccessible families, General/Inquiry-first rejection, inactive/closed/unconfigured offerings, tenant denial and scoped replay denial.
`AdmissionSpecialRegistrationLookup_Tests.cs` checks server filtering before count/paging with 25 earlier Inquiry rows, retained Both and Direct options, unchanged default list results, search/scope/tenant denial, and opening/closing at IST midnight while UTC is still on the previous date.
The 2026-10-12 focused gate passed 49/49 tests (`D:\CodexArtifacts\2026\special-registration\results\special-registration-final.trx`). The larger lookup fixture exposed recursive academic AutoIncludes in entity-valued read projections; the read now projects only academic scalar columns, and the same fixture passes without the large SQLite temporary data expansion. Contracts/Application/Application.Tests were freshly compiled; the unchanged EF assembly was reused after checking every EF source timestamp and copied SHA-256 (`ef-reuse-evidence.json`). This is not a full-solution/EF build or deployment claim. Student docs pass; global docs remain blocked by existing Finance document mojibake. Updated-source authenticated browser acceptance remains separate.

The user-authorized Pipeline extension is covered by `AdmissionSpecialRegistrationPipeline_Tests.cs`: RTE, Management, Staff and retained Management Both registrations appear before Decision with nullable Inquiry identity, guardian contact/search, zero Inquiry follow-ups, scoped options, My Cases/academic/tenant denial, and permanent Applications ownership after Decision entry and later Form correction. Inquiry follow-up reads remain unavailable for direct cases under the existing not-found contract. No Inquiry is created. Existing historical General, missing-source, terminal-case and Inquiry follow-up regressions remain unchanged.

The broader 65-case run passed 61 existing scenarios; four new assertions initially expected the wrong follow-up exception type. After aligning only those assertions with the unchanged not-found contract, all four new scenarios passed (`special-registration-handover-final.trx`). Thus 65 unique scenarios are verified across the broader and focused rerun, rather than claimed as a single green full-suite run. Contracts/Application/Application.Tests compiled with zero errors. No schema change is needed.
The user subsequently rebuilt and restarted the normal local Host output; its Application and Contracts DLL timestamps postdated the relevant source. Authenticated reads against that Host confirmed both direct RTE drafts in Pipeline, guardian-mobile search, mobile Continue/Back with retained list context, and exclusion from the Decision-only Applications worklist.
The initial Pipeline/options and list requests returned HTTP 200 in 44.25 and 44.79 seconds; the later search returned HTTP 200 in 1.84 seconds. Initial latency remains an unclassified local performance follow-up, not a production PostgreSQL benchmark.
Decision transfer, correction permanence and finalization/Onboarding handoff are integration-test evidence; no admission decision or finalization was performed in this browser check. See [frontend Student testing](../../../../Shikshamitra-FE/docs/student/testing.md) for drawer save/reload, viewport and browser artifacts. Staging and deployment remain separate gates.
`Special_registration_finalizes_through_common_decision_and_onboarding` exercises the real Registration endpoint through the current Form/Admission Decision workflow, idempotent finalization and Onboarding handoff without an Offer stage.
These scenarios reuse the accepted current Form/Admission Decision fixture; unrelated historical Inquiry/Custom/Offer fixtures and governed Records return tests remain unchanged.

The focused Application regression passed 12/12, including reserved/activated placement, class-wide access, shared applicant protection, same-principal revocation and UTC event round-trip. Actual PostgreSQL probes allowed Alpha after its Records move, denied Beta application/applicant/note/download, confirmed no denied note persisted, and excluded Beta from the list. Deactivating the temporary scope restored the original HOD assignment and denied Alpha with the same token.

Attendance adds nullable canonical `GrNumber` while retaining legacy numeric `GrNo`. Live roster verification returned `QAS26-000001`. Admission event fields use timezone-aware Npgsql parameters for the existing timestamptz columns; a new local end-journey decision persisted and reloaded the same UTC instant. Previously shifted historical rows were preserved and require a separately reviewed backfill. The synthetic Class 3 probe application is now withdrawn.

The final current Attendance HTTP class passed 23/23, zero skips, including prefixed/numeric-only GR, marking, historical scope, action permissions, leave reconciliation and locked corrections. Its identifier-only run passed 2/2; those cases are included in the 23 and should not be summed. The new fixture uses the existing narrow update reader rather than loading the full Student navigation graph. Earlier stalled runs were superseded by these passing current-source checks. Evidence is `.artifacts/student-scope-repair/results/repair-attendance-all-final.trx` and `repair-attendance-identifiers-final.trx`; the command uses `--filter FullyQualifiedName~StudentAttendanceApi_Tests --artifacts-path .artifacts/student-scope-repair/http-final --no-build`.

The current Host build passed with zero errors and 747 compiler warnings. Only the user-approved `20261012061916_MobileAcademicResponses` migration was applied to the verified localhost `postgres` database, without seeding; no migrations remain pending. A design-time model comparison reports three unrelated table drops (`AppStudentManagementSettings`, `AppStudentRollNumberAssignments`, `AppAcademicTerms`); no extra migration or schema deletion was generated or applied. Frontend checks and supporting browser evidence are in [frontend testing](../../../../Shikshamitra-FE/docs/student/testing.md#student-scope-repair-verification-2026-10-12). This is local evidence, not full-suite or deployment acceptance.

## Student scope repairs (2026-10-12)

Finalized application reads now share a placement-aware scoped query across lists, applicant identity, converted inquiries, activities, documents, assessments, queues and related projections. Before placement, Offering access remains authoritative; after placement, reserved Onboarding division or current academic-year enrollment controls access. Enrollment takes precedence over a completed case's older placement snapshot. Tenant and action permissions remain required.

The focused Application regression passed 12/12, including reserved/activated placement, class-wide access, shared applicant protection, same-principal revocation and UTC event round-trip. Actual PostgreSQL probes allowed Alpha after its Records move, denied Beta application/applicant/note/download, confirmed no denied note persisted, and excluded Beta from the list. Deactivating the temporary scope restored the original HOD assignment and denied Alpha with the same token.

Attendance adds nullable canonical `GrNumber` while retaining legacy numeric `GrNo`. Live roster verification returned `QAS26-000001`. Admission event fields use timezone-aware Npgsql parameters for the existing timestamptz columns; a new local end-journey decision persisted and reloaded the same UTC instant. Previously shifted historical rows were preserved and require a separately reviewed backfill. The synthetic Class 3 probe application is now withdrawn.

The final current Attendance HTTP class passed 23/23, zero skips, including prefixed/numeric-only GR, marking, historical scope, action permissions, leave reconciliation and locked corrections. Its identifier-only run passed 2/2; those cases are included in the 23 and should not be summed. The new fixture uses the existing narrow update reader rather than loading the full Student navigation graph. Earlier stalled runs were superseded by these passing current-source checks. Evidence is `.artifacts/student-scope-repair/results/repair-attendance-all-final.trx` and `repair-attendance-identifiers-final.trx`; the command uses `--filter FullyQualifiedName~StudentAttendanceApi_Tests --artifacts-path .artifacts/student-scope-repair/http-final --no-build`.

The current Host build passed with zero errors and 747 compiler warnings. Only the user-approved `20261012061916_MobileAcademicResponses` migration was applied to the verified localhost `postgres` database, without seeding; no migrations remain pending. A design-time model comparison reports three unrelated table drops (`AppStudentManagementSettings`, `AppStudentRollNumberAssignments`, `AppAcademicTerms`); no extra migration or schema deletion was generated or applied. Frontend checks and supporting browser evidence are in [frontend testing](../../../../Shikshamitra-FE/docs/student/testing.md#student-scope-repair-verification-2026-10-12). This is local evidence, not full-suite or deployment acceptance.

## Local Student scope end-to-end QA (2026-10-12)

Actual localhost DDVS writes completed two synthetic Admissions and Onboarding activations. Coverage included required Form validation, qualification, Assessment capacity/future-attendance/mark limits, published Finance fee selection, required documents and replacement history, waitlist finalization denial, idempotent finalization/activation, readiness snapshots, placement guards, GR/Roll allocation, independently approved Records placement, Attendance draft/submit/lock/corrections, forged mixed-roster atomic rejection, action-permission denial, leave scope, and locked leave-credit reconciliation. Student accounts were not required or provisioned; no QA payment or receipt was posted. The existing staff Academic UserScope was the authorization reference.

The original QA confirmed three defects: finalized Admission authorization used the class Offering for outside-division students; Attendance omitted canonical prefixed `GrNumber`; Admission event timestamps reloaded with shifted instants. Reproduction evidence is preserved in the frontend `.artifacts/student-e2e-20261012/`. Their subsequent repair status is recorded above; the original QA did not change source behavior.

Focused Application validation passed 16/16 plus six additional Admissions/ancestor scope cases. `StudentAttendanceApi_Tests` passed 21/21. These are focused results, not the complete backend suite. API denial responses for several Student scope operations are HTTP 400 with safe domain-error data; they were classified as denial after inspecting the body, rather than as data leaks. The same HOD token lost QA Records/Attendance access after scope revocation, and the old generated export was denied with `Students:Records:ReportAuthorityChanged`. Original HOD active scope and Admin/HOD permission overrides were restored and verified. QA students, documents, workflow/configuration, published fee setup, dedicated division, audited placement, attendance and leave records remain locally for reproduction. The first active QA Collection Policy became the default during normal policy creation; it remains referenced by the QA fee structure.

Tests used isolated artifact output paths and TRX files under `.artifacts/student-e2e-20261012/results/`. The additional test filter names methods in the partial `AdmissionWorkflowRuntimeIntegration_Tests` class; filtering by the fixture filename `AdmissionApplicantUserScope_Tests` alone does not select those cases. The local API was restarted externally during QA; checks resumed afterward. Supporting browser coverage and unresolved frontend suite failures are recorded in [frontend testing](../../../../Shikshamitra-FE/docs/student/testing.md#local-student-scope-end-to-end-qa-2026-10-12). Cross-tenant live credentials, complete certificate/ID issuance, student portals/email delivery, year-end transitions, staging and deployment remain outside this acceptance evidence.

## Staff Academic UserScope checks (2026-10-09)

Student browser scope belongs to the logged-in staff member. The final focused Application run passed 24 tests with no skips: strict placement matching, full Academic ancestor tuples, division/class/medium/year/tenant boundaries, spoofed attendance IDs, missing bulk IDs, cross-tenant rejection, applicant direct-ID protection, scoped overview/activity rows, shared identity edit rejection, and settings publication rules. Profile fixtures use persisted completed historical-import cases so scope assertions do not depend on retired Admission stage fixtures. Settings success fixtures explicitly assign tenant scope.

The focused `StudentAttendanceApi_Tests` run passed all 20 tests with no skips, including historical attendance filtering under full Academic assignment tuples. Application build and test-project compilation passed. These focused runs do not certify the complete backend suite or PostgreSQL provider acceptance. Browser verification was omitted at the user's request.

From the backend repository, run `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter 'FullyQualifiedName~StudentRecordScope_Tests|FullyQualifiedName~Applicant_identity_notes_and_activity_direct_ids_require_academic_scope|FullyQualifiedName~Division_scope_checks_must_use_the_persisted_year_and_class|FullyQualifiedName~Applicant_overview_filters_sibling_applications|FullyQualifiedName~Academic_ancestors_allow_navigation|FullyQualifiedName~Student_record_profile_respects_division_boundary|FullyQualifiedName~AdmissionAssessmentScope_Tests|FullyQualifiedName~StudentRecordsSettings_Tests'`, then `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~StudentAttendanceApi_Tests`. This verification used isolated `--artifacts-path` outputs. Evidence is in ignored `.artifacts/student-user-scope/results/student-scope-release.trx` and `student-attendance.trx`.

Student documentation passes `pwsh -NoProfile -File scripts/docs/validate-docs.ps1 -Module student`. Windows PowerShell 5's default text decoding reports a false mojibake error on existing Unicode text; PowerShell 7 reads the UTF-8 documents correctly.

## Offering clone cycle dates (2026-10-11)

`AdmissionOfferingCloneWindow_Tests.cs` uses the current Form/Admission Decision fixture for General and RTE.
It saves source custom dates through the unchanged update API, verifies clone null overrides/target-cycle inheritance and retained routing/forms/state, then revises the target cycle through its controlled command and verifies refreshed clone effective dates without changing the source.
Run `FullyQualifiedName~Offering_clone_inherits_target_cycle_window|FullyQualifiedName~AdmissionOfferingContract_Tests` with the Application test project and isolated artifacts.
No EF/schema, live database or running API change belongs to this clone-only adjustment.

## Five-stage admission acceptance (2026-10-08)

New application workflows permit Form (1), Assessment (2), Payment (4), Document verification (5), and Admission Decision (7), retaining numeric persistence. Eligibility, Review, and Offer configuration/direct operations are retired. Ordinary managed-stage skip cannot bypass required evidence; separately authorized route override and unrelated historical stage behavior remain governed by existing contracts. Approved completed applications finalize without Offer while retaining identity, guardian, duplicate, scope, concurrency and idempotency checks.

Focused Application runtime/finalization verification passed 17 tests, and the initial Domain proof passed 25 tests. The later affected Domain run passed 66 tests. The final fee-reader/Onboarding-reader/no-Offer finalization run passed 14 tests, including snapshot timestamps from UTC, Local and Unspecified clocks. Counts overlap between reruns and must not be summed. The final Host artifact build passed with zero warnings/errors and was copied into the separate local QA runtime before acceptance. Focused tests do not imply the complete backend suite or production deployment passed.

Persisted local DDVS application `APP-2026-000001` (**QA FiveStage Journey**) completed all five stages through authenticated UI actions: draft/submit/required Form verification, attendance and scored Assessment, published Finance fee confirmation, rejected document replacement and acceptance, Waitlist followed by Approval, and workflow completion. API probes confirmed incomplete Assessment, absent fee selection, missing/rejected documents, pending Waitlist, and premature decision actions fail. Payment summary retained zero transactions and no settled amount. UI finalization created Student `3a242eb1-43e9-34f6-8b9e-ac328c6a9c19` and case `3a242ecd-7c87-f241-29a4-e0bfedee099f`; two repeated requests returned those same identities.

Normal Onboarding readiness/placement/identifiers/activation created enrollment `3a242ed1-a50a-4d98-e012-3c34d14ae88a`, Class1/A, GR `QA5-000001`, roll4. Activated profile was readable under the existing Provisioning policy, while Directory correctly excluded the student before completion. Normal Finance signed-preview/assignment APIs assigned the exact selected structure, then the Cash collection API recorded the synthetic local QA first installment ₹291 (₹83 + ₹208), receipt `FIN-2026-000001`. Exact idempotent replay returned the same receipt; both first-installment ledger dues became zero. Existing Finance service-handoff retry completed Onboarding (status4), and Directory returned one `CompletedOnboarding` student with full profile access. This was persisted synthetic ledger data, with no real money or external transaction. No fake Paid flag, account provisioning, waiver or override was used. Student Records profile correctly rejected access before activation. Frontend layout and exact browser checks are recorded in [Student testing](../../../../Shikshamitra-FE/docs/student/testing.md).

QA references were created through normal APIs: a separate published structure using existing fee heads/terms, a QA collection policy and a class/year-scoped GR series. With no prior active collection policy, ordinary Finance behavior made the first QA active policy the year's default; existing configuration was not overwritten. Custom document codes were generated on workflow save; the local Student document category catalog was empty, so the uploaded synthetic PNG was uncategorized. Future fee snapshot and Onboarding confirmation timestamps now convert the clock to UTC; existing saved QA timestamps were preserved, and a live Onboarding confirmation after the fix persisted correct UTC. No school data reset, migration, production deployment or full-suite claim was made.

## Synthetic Student records

`StudentRecordsDemoRepairSeederContributor` runs immediately after `StudentSeeder` in FullDemo and FastDemo with its own `StudentRecordsDemoRepairSeeder:v1` completion marker. It adds an active academic-year enrollment and an explicitly synthetic historical-record approval for the existing StudentSeeder accounts. Minimal does not run it. Normal seeding reaches this new step even when `StudentSeeder:v1` was already completed.

The repair requires the seed-generated student/account ID link, `student-*` username, matching `@example.invalid` email, seeded GR/roll values, and valid tenant-owned year/class/division placement. It uses the existing synthetic `admin-ddvs` account as the distinct decision actor. It does not assert completed Admissions, onboarding checklist, or Finance evidence. Existing enrollment, onboarding, or historical-approval records (including deleted and rejected records) block repair, preserving operator decisions. Occupied active roll numbers also block insertion.

Run the `Demo_record_repair_` application scenarios and the Directory/dashboard regressions. Verify repeatability, year filtering, real-account exclusion, profile/tenant boundaries, existing onboarding, and deleted/rejected approval preservation. Separately reload the authenticated local Directory and dashboard for the selected academic year; automated fixtures alone do not prove a deployed seed run.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Demo_record_repair|FullyQualifiedName~Directory_|FullyQualifiedName~StudentModuleDashboard"
```

## Offering simplification regressions

Use the current special-track gate above for workflow-owned Registration forms, ignored stale caller form IDs, preserved unused special routing, frozen used routing, General/RTE coexistence, same-track duplicate rejection, and guarded special Registration entry. Fresh General direct entry remains denied; configured RTE/Management/Staff Registration is supported. Broader `Offering_simplification_`, `AdmissionEligibilityOfferingCompatibility_Tests`, historical Form/Inquiry/RTE, and Student Records return/resume/cancellation cases still provide intended regression coverage, but legacy factories must be adapted to supported stages before they can establish a green wider gate. Authenticated preview/save/reload and special-entry visibility/permission checks remain separate [frontend browser evidence](../../../../Shikshamitra-FE/docs/student/testing.md).

## Predefined admission fields and profile handoff

Run `AdmissionAdditionalStudentFieldCatalog_Tests`, `AdmissionStudentProfileMapping_Tests` and the focused cases in `AdmissionStudentFieldHandoff_Tests`. They cover metadata destinations, protected identifier storage, photo validation/save/reload, scoped sibling paging/hydration, absent-value preservation, pending-profile opt-in, Records review idempotency and masked-section rollback. Records action authorization tests verify the internal review helpers retain View/Create permissions.

Rollback cases must use the existing copied SQLite database and normal UOW manager harness; the common application fixture disables transactions. Frontend authenticated acceptance remains a separate gate for the shared photo/identity/location controls, form save/reload and finalization review copy.

## Credential email and access verification

The credential-card change has focused rendering coverage in `StudentCredentialEmailTemplate_Tests` and delivery integration in `StudentCredentialEmailDispatcher_Tests`. Checks include the visible four-row credential card, an unlinked Google Play badge, retained full-length secrets, HTML encoding, blocked-image alt text, optional-name fallback, existing-password reuse and conditional recovery links. Preview files use synthetic details only; no email transport or account provisioning runs in the preview helper. The current password policy and first-login change requirement remain unchanged. Browser checks inspect the exact generated HTML at desktop and narrow widths; they are not Gmail/Outlook inbox or mobile-app acceptance.

The short-initial-password regression in `StudentCredentialEmailDispatcher_Tests` checks that the actual emailed password is at most 10 characters, includes uppercase and lowercase letters, numbers and a symbol, validates against the saved Identity account and retains mandatory first-login password change. Run the dispatcher and template suites together; existing-account and transport-retry cases must still preserve the stored password. Existing longer passwords are never truncated or reset by resend. The configured Identity password policy remains enforced; an incompatible school policy must not be silently weakened.

On 26 October 2026, the combined dispatcher and template suites passed all 24 cases after reproducing the old generator's length-limit failure. Host rebuild completed with zero errors. This validates local generation and Identity password checks, not a new real mailbox send or mobile-client login. Evidence: `.artifacts/credential-password-length-red.log`, `.artifacts/credential-password-length-green.log` and `.artifacts/credential-password-host-build.log`.

On 26 October 2026, 28 distinct focused cases passed across the dispatcher, template, two sibling-dispatch regressions and two linked-owner/shared-parent email regressions (overlapping template reruns are counted once). The exact renderer was inspected at desktop, 360 px and 320 px widths; a synthetic 53-character password remained complete with no horizontal overflow. Student docs validation and whitespace checks passed. Real mailbox delivery, Outlook/Gmail client rendering and deployment were not exercised. Evidence stays under ignored `.artifacts/credential-email-ui-*` files.

| Gate | Required evidence |
|---|---|
| Domain | `StudentCredentialReadiness_Tests`, `StudentCredentialEmailRequest_Tests`: legacy disabled defaults, invalid policy, exact noted overrides, claims, cooldown and secret cleanup. |
| Application | `StudentCredentialEligibility_Tests`, `StudentCredentialEmailDispatcher_Tests`, existing Onboarding portal and Records transitions: actual enrollment/document/timing gates, retry hash preservation, sibling handling, recipient/link/actor revocation and exit access. |
| HTTP | Portal token regression covers wrong initial password, protected password change, mailbox confirmation, aliases and exit/refresh denial. Existing Onboarding/Records contract tests remain required. |
| Browser and email | [Frontend checks](../../../../Shikshamitra-FE/docs/student/testing.md) own saved settings/reload, status and document dialogs. Controlled emails must go only to the user-approved test recipients; transport acceptance is distinct from inbox confirmation. |

Focused local compile/test results and remaining acceptance limits must be reported separately from deployment readiness.

## Student Attendance verification

| Gate | Focus |
|---|---|
| Domain | `test/ShikshaMitra.Domain.Tests/Students/Attendance/`: 32/32 passed on 1 October 2026 after the final review, with no skips. Covers policy precedence/ambiguity, schedules, calendar closure/fail-closed behavior, provisional missing calendars, dated membership/range boundaries, common calculation and conflicting historical records. |
| HTTP | `test/ShikshaMitra.HttpApi.Tests/Students/Attendance/`: 27/27 passed on 1 October 2026 against the fresh final Application build, with no skips. Covers complete/partial roster, atomic invalid batches, stale/idempotent writes, individual schedules, review/no-self-approval, unknown start, calendar closure, settings authorization, school-day leave overlap, audited retroactive leave credits, immutable correction previews and stale leave-source approval rejection. Offset POST dates match date-only GET roster revisions. Tenant-wide, narrow-division and empty scope checks accompany real leave create/list/approval. Fixtures use formal enrollment and scalar academic queries. |
| Actual provider | `test/ShikshaMitra.EntityFrameworkCore.Tests/EntityFrameworkCore/StudentAttendancePostgres_Tests.cs`: passed on 1 October 2026 against a newly created empty loopback PostgreSQL database. Verified migration/backfill, unchanged Student identity, ambiguous history preservation, enrollment/date uniqueness, untouched rollback/re-upgrade and used-feature rollback guard. Requires a new empty attendance_tests_* database; an environment-skipped run is not provider verification. |
| Integration | Check register/profile/daily/dashboard agreement, a historical class move, null/provisional metrics, approved leave treatment and current-school target/cutoff. |
| Active-only Daily | `StudentAttendanceApi_Tests.ActiveDirectoryVisibility.cs` covers current/past Active Directory parity, inactive/departed/incomplete-onboarding exclusions, rejected hidden-student writes, batched queue parity, unchanged saved history and enrollment revisions, hidden Pending drafts and visible resubmission without roster-review drift. Reports/History retain hidden recorded observations. |
| UI | [Frontend Student checks](../../../../Shikshamitra-FE/docs/student/testing.md) own focused tests, lint/typecheck and authenticated browser walkthrough. Source checks do not replace browser evidence. |

On 23 October 2026, the isolated-output HTTP run passed all 41 selected cases with zero skips: the complete `StudentAttendanceApi_Tests` class, including eight Active Directory visibility cases, plus the batched register snapshot/query-count regression.
Build and test evidence is retained in `.artifacts/attendance-active-list-20261023/regression-validation.log`.
Student module documentation validation also passed; this local verification does not certify a staging deployment or browser walkthrough.

Run focused checks from the backend root:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests --filter FullyQualifiedName~Students.Attendance
dotnet test test/ShikshaMitra.HttpApi.Tests --filter 'FullyQualifiedName~StudentAttendanceApi_Tests|FullyQualifiedName~AttendanceSettingsAuthorizationApi_Tests|FullyQualifiedName~StudentLeaveApi_Tests'
dotnet test test/ShikshaMitra.HttpApi.Tests --filter 'FullyQualifiedName~StudentAttendanceApi_Tests.Daily_attendance_|FullyQualifiedName~StudentModuleDashboardApi_Tests.Batched_register_snapshot'
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests --filter FullyQualifiedName~StudentAttendancePostgres_Tests
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
```

Only the disposable database supplied through `STUDENT_ATTENDANCE_TEST_POSTGRES` may be used for provider fixtures.
Never migrate, reset or seed an existing school database to obtain test results.
If a debugger locks normal Host outputs, build into isolated outputs; do not treat a running old Host as proof of the current implementation.

Final HTTP evidence is `.artifacts/attendance-verification/attendance-final-api.trx` and `.log` (27 executed, 27 passed; 10.5901 minutes including shared FullDemo startup). Application SHA-256 is `D28B1C567F38E328E25CD24446D0C35D11C3C74539F587E1277AF0332D24F587`; all six copied production assemblies were checked against their source outputs before runtime. The earlier 20-pass/7-fail run is retained as `attendance-before-date-scope-fixes.*`; its date-kind roster mismatch and tenant-wide leave-list mismatch were fixed rather than bypassed. Application, EntityFrameworkCore and HTTP compilation passed, alongside the focused Domain and actual-provider gates above. Authenticated browser inspection is limited to the frontend notes; live school save/reload and correction/report acceptance still require a walkthrough on the final Host build.

## Admission fee selection checks

The 1 October 2026 focused verification passed 26 Domain tests, 23 Application tests and four HTTP contract tests.
Application coverage includes the real Payment-first and Offer-first runtime, persisted selection and payment locks,
and Onboarding completion remaining blocked until the saved selection is confirmed despite settled Finance dues.
The focused Application build temporarily excluded an unrelated broken Student Records test without changing its source.
A subsequent normal build was attempted after that file was repaired by separate work; it is blocked by
`StudentRecordRenewalIntegration_Tests.cs:61` (`FeeHeadModule`) and `:70` (`ConcessionFeeHeadScope.All`).
These results do not certify the complete current test project or deployment readiness.

The signed-in local browser check on 1 October used isolated published QA references and synthetic
application `APP-2026-000002`. The selected published revision and mandatory/optional heads persisted
through reload. A simulated INR 200 admission cash receipt locked the selection and preserved existing
automatic progression; the later Offer retained the same selection. After admission finalization,
explicit Onboarding confirmation persisted through another full reload. No real money, external
acknowledgement or Student Finance assignment was created. Enrollment activation, Finance assignment
and full Onboarding completion were not exercised in this browser run; exact assignment matching and
completion gates remain covered by the focused integration tests.

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~AdmissionFeeSelection|FullyQualifiedName~AdmissionPaymentStagePolicy"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~AdmissionFeeSelection|FullyQualifiedName~AdmissionFeeStructureReader|FullyQualifiedName~Fee_selection|FullyQualifiedName~OnboardingFeeReadinessReader"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~AdmissionFeeSelectionApiContract"
```

Coverage must include actual stage-entry ordering, both Offer/Payment orders, published class/year references, optional heads, missing selection gates, application concurrency, persistence reload and locks after pending payment.
Verify denied read capability, Onboarding-only access before handoff, tenant and academic scope, confirmation against the exact Finance revision and included heads, and provisioning completion after explicit confirmation.
Existing Payment, Offer and Finance-readiness tests remain required regression checks because selection does not replace their monetary or lifecycle rules.
HTTP reflection contract tests prove routes and declared action grants; they do not establish denied requests through a deployed host.

## Student Records checks

Run `FullyQualifiedName~Custom_record_` in Application tests for final-submission/draft separation, typed false/zero, immutable original evidence, audited direct/pending corrections, idempotency/stale guards, conditional hide retention, all five privacy groups, legacy empty sections and returning submissions on a reused enrollment. Profile API contract tests cover additive academic-year/milestone fields; Details has the same Records View boundary. Apply the additive custom-profile migration only to an explicitly approved environment. Passing local SQLite fixtures does not establish a migrated live database, PostgreSQL concurrency or authenticated browser acceptance.

The focused captured-detail suite additionally checks independent approval application, source-bound dynamic choices/selected-only hydration, more than twenty school master choices, current canonical enum controllers and public controllers without a Compliance API dependency. Restricted details/options reads must withhold the source option dictionaries as well as values, and change history must withhold any recorded submission that is no longer readable. Repeatable sections validate each row's conditional state, entry bounds and protected structure; hidden unchanged answers retain their saved values. The finalization-only profile/evidence regression uses the current Document verification → Admission decision fixture and preserves its original Records-deny/Admissions-evidence assertions.

Local verification on 2026-10-12 passed 36 of 37 selected Application cases, including all 24 captured-detail cases. The remaining finalization-only profile/evidence test failed before reaching Records because its fixture used a retired Custom workflow stage. After changing that fixture to the supported Document verification/Admission decision owners, the final safeguards run passed all four selected cases with zero skips, including that repaired regression, public scalar rules, a master catalogue larger than twenty choices and restricted history masking. The fresh Host build and four HTTP contract cases also passed. Evidence is retained under `D:\codex-artifacts\student-records-redesign-be\test-results` in `student-record-redesign-regression.trx`, `student-record-durable-safeguards.trx` and `student-record-http-contracts.trx`; these are scoped results rather than a full-suite pass.

With explicit user approval, the tested Host was started on `https://localhost:44332` and only `20261012120000_AddStudentRecordCustomProfiles` was applied to the verified local PostgreSQL database. No seed, backfill, grants or unrelated migrations were applied; the existing RemoveSubstitutionSettings and AddFeeStructureRevisions migrations remain pending. Release must apply the custom-profile migration to its target environment through the normal deployment process.

Authenticated browser verification against this Host loaded active Student List, profile Details, frozen Registration and linked Onboarding, Documents and existing change history. Required-field/reason feedback and dirty-close protection were exercised using a discarded draft; no saved student correction or approval was made. Desktop, tablet and mobile proof and frontend validation are recorded in [Student testing](../../../../Shikshamitra-FE/docs/student/testing.md). Persistent corrections and approval policies are covered by isolated integration fixtures. Staging, production, full-suite acceptance and production PostgreSQL concurrency for custom corrections remain separate gates.

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~StudentRecord"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Students.Records|FullyQualifiedName~Record_|FullyQualifiedName~Directory_|FullyQualifiedName~Legacy_override_|FullyQualifiedName~Student_record_profile_|FullyQualifiedName~Enrolled_profile|FullyQualifiedName~StudentRecordsSettings_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~StudentDirectoryApiContract_Tests|FullyQualifiedName~StudentRecordWorkflowApiContract_Tests|FullyQualifiedName~StudentRecordsAuthorizationApi_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~StudentRecordsApplicationPostgres_Tests"
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~StudentRecordsPostgres_Tests"
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```

### Final automated evidence (2026-10-01)

| Check | Result and limits |
| --- | --- |
| Domain | 25/25 passed, no skips. Includes policy, transition, change, report/duplicate and legacy-entry invariants. |
| Application | 113 selected cases verified across the combined run, focused repairs, six new apply-serialization regressions, two combined guardian/renewal journeys, four readiness error-contract cases and eight report release cases, with no skipped cases. The combined run passed 90/93; two graduation fixtures required actual certificate voiding before correction, and the repeated-return fixture required one published school policy and a preview request key. Both graduation cases and the return case subsequently passed; the added serialization selection passed 6/6, guardian journey selection passed 2/2, error-contract selection passed 4/4 and report release selection passed 8/8. This is not a single clean full-suite run. |
| HTTP contracts | 22/22 passed, no skips. Route/action-grant metadata, not authenticated deployed requests. |
| HTTP authorization pipeline | 13/13 passed, no skips. Real middleware/controllers/services with isolated SQLite data, the existing test authentication handler and header-based permission checker. This does not verify production OpenIddict or persisted grant-provider behavior. |
| Actual PostgreSQL | 1/1 passed, no skips, against fresh `records_tests_20261001_030417`. Tests the database mechanisms below, not complete application-service execution under PostgreSQL. |
| PostgreSQL application commands | 1/1 passed, no skips, against fresh `records_tests_app_968845d32e6c4a43a6d18bbe36ec3dd6`. Real HTTP settings, historical entry approval, preview and simultaneous transition commands; natural provider conflict, rollback, capacity/roll revalidation and idempotent replay. Uses test authentication/grants and model-created tables. |
| PostgreSQL renewal activation | 1/1 passed, no skips, against fresh `records_tests_renewal_11e44978987c461582531369ab48d9fe`. Two real HTTP activation requests for one PendingChecklist case; natural serialization conflict, one target enrollment/event, stable GR, stale rejection and retained Finance completion gate. Uses test authentication/grants and model-created tables. |
| PostgreSQL approval stage | 1/1 passed, no skips, against fresh `records_tests_approval_c980885677314b8f90d39877f4b8a5dd`. Two persisted reviewers concurrently approve the same stage; one commit and one natural optimistic HTTP 409, one stage/event, requester denied, stale stamp rejected and refreshed remaining review scheduled. Uses test authentication/grants and model-created tables. |
| PostgreSQL same-year return | 1/1 passed, no skips, against fresh `records_tests_return_05a768e2557e451a9da756666f6194c7`. Concurrent same-key ReuseProfile requests create one batch/return/review; replay before and after activation preserves IDs, and activation reopens the original enrollment once with stable GR and roll. Uses test authentication/grants and model-created tables. |
| PostgreSQL scheduled workers | 2/2 passed, no skips, against fresh `records_tests_midnight_0f79cec4326341b0aa7d169dcf7e15ff`. Actual change/transition worker selectors and application commands at the India institution-midnight boundary, recorded actor revocation and repeated ticks. Uses an isolated mutable clock and test authentication/grants. |
| PostgreSQL overlapping change workers | 1/1 passed, no skips, against fresh `records_tests_worker_5e74ef8aaf2d4af0b0ad9ba0a594d679`. Two actual worker scopes select one due change and reach separate serializable writes; natural provider conflict, preserved Applied winner, stable repeated tick and unchanged identity/placement/enrollment. Uses the isolated clock and test authentication/grants. |
| PostgreSQL overlapping transition workers | 1/1 passed, no skips, against fresh `records_tests_tworker_0084e6fac4c74803b65d0448217e2bb2`. Two actual worker scopes reach serializable enrollment updates for one effective withdrawal; natural provider conflict, one Applied event, preserved winning result and unchanged identity/placement. Repeated ticking preserves saved history and stamps. Uses test authentication/grants and the isolated clock. |
| Populated local migration copy | 1/1 passed, no skips, against `records_tests_copy_b491993868d642f7880542231f937a1e`, restored from the verified pre-Records local backup. All original columns in 478 tables / 16,440 rows, including 271 Students, and original indexes/constraints were preserved through exactly three migrations and idempotent reapply. This copy has zero enrollments. |
| Current API build | Host build passed with zero warnings/errors after refreshing shared contracts and Application dependencies. |
| Documentation | Student validation passed. The all-module validator stops on a mojibake issue in unchanged `docs/modules/finance/references/charge-management-e2e-test-plan.md`; that unrelated Finance document was not modified. |

TRX evidence is under the respective test project's `TestResults`: `records-domain-complete.trx`, `records-application-verified.trx`, `records-workflow-repairs.trx`, `records-sameyear-final.trx`, `records-http-verified.trx`, and `records-postgres-final.trx`. The workflow-repairs run passed both graduation cases and initially failed the return preview's missing request key; the sameyear-final run verifies the corrected return fixture.

`records-http-authorization.trx` records the separate 13-case HTTP pipeline run. It proves denied exact actions despite related View/Create grants, anonymous 401 and ungranted 403, disabled database identities despite a valid principal, tenant non-disclosure, forged admin claims without database scope, and forbidden placement/promotion targets within otherwise permitted academic hierarchies. Rejected targets leave no request, batch, transition or target enrollment and preserve the source class. The initial run exposed a real narrow-class scope bypass; the feature-local target guard now uses explicit wildcard/exact division grants. Scope BusinessExceptions retain the existing HTTP 400 plus `UserScope:AccessDenied` contract. Final no-mutation assertions project only the canonical Student class field, avoiding full navigation-graph materialization that exhausted SQLite temporary resources on earlier attempts. These tests neither change live grants nor establish production token authentication.

After tightening target scope, `records-target-scope-allowed-paths.trx` verifies four existing Application cases again, all passing with no skips: permitted placement validation plus Finance-backed class-impact rejection, paid annual renewal preserving both enrollments, repeated same-year returns preserving enrollment/renewal/Finance history, and reviewed short-renewal return with idempotent linkage. These are reruns within the 113 unique selected cases above, not four additional unique tests.

`records-serialization-apply.trx` records six passing cases in `StudentRecordApplySerialization_Tests.cs`. They run real apply services and repositories on a copied ordinary SQLite connection with a real EF transaction and transactional ABP unit of work. A one-time raw/wrapped provider-neutral SQLSTATE `40001` fault is injected after writes are flushed: student/enrollment/contact changes roll back, the case retains ConcurrencyConflict, a second automatic apply does nothing, explicit transition retry or replacement change succeeds, and unrelated exceptions propagate. The ordinary Application fixture uses `AddAlwaysDisableUnitOfWorkTransaction`; these tests override that behavior locally and restore their connection configuration afterward. Earlier attempts correctly failed rollback assertions against the nontransactional fixture. This is injected-error handling and real SQLite rollback proof, not a natural PostgreSQL application race.

Domain checks cover structural rules, immutable snapshots, condition matching, masks, approval ordering, self-approval, explicit retry, renewal state, shared owner roll-rule resolution and frozen proposals. Application scenarios execute real services for school/year policy precedence, stale updates, disabled database actors despite authenticated claims, canonical profile/scope, legacy entry, private photo expiry, changes, duplicate review, guardian reviews, issued documents, report jobs, exit idempotency, scheduled/future exits, corrections, and renewal/return.

`StudentRecordRenewalIntegration_Tests.cs` uses actual Finance collection services, persisted dues, a pinned collection policy and assignment snapshot, partial/full payments, and preserved enrollment/GR identity. Required-portal coverage checks wrong mailbox denial, actual Student/Parent account creation, owner action grants, verified saved email, idempotent linking, completion after required handoffs, and a persisted pending KeepUntilReview access review. It does not prove the complete guardian-change, access-review-resolution and renewal browser journey. The repeated-return case completes a paid annual renewal and two reviewed withdrawals/returns in the same year, reuses the same enrollment and Finance references, retains three completed reviews, and rejects a different-class return with an immutable fee assignment.

`records-guardian-renewal-journey.trx` records two passing cases from `StudentRecordGuardianRenewalJourney_Tests.cs`, one per SuspendUntilReview / KeepUntilReview school policy. Real SaveDraft/Publish and independent approval create a request-linked access review; a later opposite policy does not change the submitted snapshot. Pending review blocks a paid required-portal renewal under both modes. Wrong existing identity and unverified retained identity are rejected; the wrong-identity path uses a copied SQLite database and an explicit transactional unit of work to prove that flushed link revocation rolls back. Verified resolution restores readiness, completes renewal and preserves the sibling link and shared active guardian account. The fixture marks the parent's email confirmed directly; separate owner tests cover account setup. This is a combined service journey on SQLite, not browser execution or PostgreSQL guardian execution.

`records-readiness-owner-codes.trx` verifies four preview error-contract cases. Qualified Onboarding codes (`Students.Onboarding.DivisionFull` / `RollNumberDuplicate`) and existing colon-qualified codes retain their owner contract; only local short codes receive the Records prefix. The actual PostgreSQL command gate exposed the previous double-prefix projection defect after its concurrency assertions had already succeeded.

`records-report-release-gates.trx` verifies eight real-service cases with the actual production `ShikshaMitraPermissionChecker`, persisted ABP user grants and cache invalidation wired into the report graph. Removing the generation Add grant after completion still permits download with current Reports.View/source View; removing source View denies download. Removing a database identity-reader role defeats a stale role claim. Stricter masks invalidate completed/queued jobs, and newly generated XLSX XML contains the restricted identity instead of the original name. Completed/queued expiry blocks download/start. Revocation of Add/source View or expiry after actual workbook generation prevents completion and blob publication. The runner now rechecks Add and expiry before saving the file. Test construction supplies the unit of work normally provided by the ABP proxy for manual GetList calls; shared fixtures are unchanged. This is SQLite service/real permission-provider proof, not production OpenIddict HTTP or distributed cache/concurrency proof.

Actual-provider checks migrate a synthetic historical Student through all three Records migrations without changing identity; inspect concurrency metadata and the active-only renewal index; permit completed history plus one active review; reject a second active review; and guard downgrade when historical reviews cannot fit the old unique index. Two genuine SERIALIZABLE transactions read the same reservation predicate: the competing commit returns PostgreSQL `40001` and only one reservation persists. Additional checks prove identity/request rollback after a foreign-key failure, concurrent request-key uniqueness, stale-stamp rejection, populated-governance downgrade refusal, and unused-feature downgrade/reapply. These checks do not establish concurrent preview/approval/enrollment/roll allocation through the complete application. No whole-model snapshot-match claim is made because unrelated Attendance model changes remain pending in the shared checkout.

`records-postgres-real-application.trx` separately proves one complete concurrent application scenario: two independently reviewed historical students receive the same automatic roll proposal for one final seat, then submit real promotion commands on two PostgreSQL SERIALIZABLE connections. A test-only SQL barrier coordinates genuine inserts after predicate reads without changing commands or injecting an exception. The naturally observed `40001` yields one Applied winner and a loser rejected with RollProposalChanged; loser batch/transition/target enrollment and source mutations roll back. Winning-key replay returns the same IDs; a fresh loser preview reports the unchanged Onboarding DivisionFull contract. Both GR numbers remain stable. The factory refuses existing/non-loopback databases, retains its fresh database and creates tables from the current model; the separate migration gate remains authoritative for migration SQL. Test-only SplitQuery avoids an eager scope-seeder graph timeout encountered before Records commands in the first attempt. This case uses test authentication/header grants, not production tokens, and does not cover every approval, return, renewal or scheduled-job race.

`records-renewal-race.trx` adds one actual PostgreSQL renewal activation race. Historical entry and school/Onboarding configuration use real HTTP commands; the owner permits optional returning-student identity/family/contact evidence while permanent GR, placement and Finance completion remain mandatory. Two serializable requests reach the actual enrollment insert on distinct PostgreSQL connections. One wins; the natural `40001` loser receives `Students:Records:ConcurrencyConflict` after rollback. Exactly one target enrollment, one renewal and one RenewalActivated event persist; the source enrollment becomes inactive and GR remains unchanged. Reusing the stale stamp is rejected; a fresh stamp still requires missing Finance evidence before completion. The first run exposed a generic HTTP 500 for the loser; the existing Onboarding serialization classifier now translates that failure in the Records activation command. The repaired case passed with no skips. The barrier only coordinates real SQL and injects no failure. This covers PendingChecklist activation, not concurrent completion, return, approval or deployed authentication.

`records-approval-race.trx` verifies concurrent approval of stage one under a published two-stage school policy. Both separate reviewers have persisted database role membership in the isolated tenant; the requester is denied self-review. The actual ReadCommitted transition updates are coordinated on distinct PostgreSQL connections. One review commits and the optimistic loser returns HTTP 409; exactly one stage-one event and its winning actor persist, while Student status, GR and active source enrollment remain unchanged. A stale review then returns the Records concurrency code. Refreshing the remaining reviewer advances only stage two, records both reviewers once and schedules the future withdrawal without applying it. Production code and live grants were unchanged. The barrier now accepts the expected transaction isolation while retaining Serializable defaults for existing promotion/renewal tests. This covers concurrent approve/approve at one stage, not approve/reject, renewal completion, returns, worker deployment or production token/grant behavior.

`records-return-race.trx` verifies a same-year ReuseProfile return after a real HTTP withdrawal. Two same-key requests reach actual serializable writes on distinct PostgreSQL connections; one wins and the natural `40001` loser is rejected. One batch, return transition and PendingChecklist review persist; the source remains inactive and Student remains Left until reviewed activation. Request replay preserves transition/review IDs both before and after activation. Activation reaches Provisioning by reopening the original enrollment ID without an additional enrollment, changing class/division/roll or replacing GR; one RenewalActivated event persists. Optional returning-student evidence is configured through the real Onboarding owner API. Production code and live grants were unchanged. This does not prove Finance history preservation/completion, concurrent activation of that return, or FreshApplication/ResumeStage concurrency.

`records-midnight-boundary.trx` records two passing cases through actual PostgreSQL HTTP commands and the protected change/transition worker ticks. Both remain unchanged at 23:59:59 IST; a tick at 00:00 IST applies the scheduled contact change and effective-date withdrawal, and repeated ticks preserve saved stamps and one Applied transition event. Disabling the recorded database actor before the due tick instead persists Change Conflict / Transition Exception with AuthorityChanged and preserves contact, Student and enrollment state. The Changes worker now reads the same `IClock` as its application and the Transition worker. The first SQLite attempts exposed a large test proof query and unsupported DateTimeOffset selector translation; proof reads now use scalar projections and the final gate runs the unchanged real selectors on PostgreSQL. The factory reuses the strict fresh/loopback protections and test-only SplitQuery. It verifies due eligibility using the existing Asia/Kolkata calendar; live workers poll every minute, so this does not promise execution at the exact second or prove concurrent worker deployment behavior.

`records-worker-race.trx` verifies two actual change-worker ticks, each with a separate service scope, selecting the same due scheduled contact correction. Real HTTP commands establish historical entry, publish school rules and schedule the change. Both applications reach serializable contact updates on distinct PostgreSQL connections; PostgreSQL naturally raises `40001` for one. The committed change remains Applied with no conflict code, one change/contact row and no fabricated approval decisions. Student identity/placement fields and the enrollment stamp remain unchanged. Another actual tick preserves the complete saved state, including parent/change stamps and contact audit metadata. The first test run incorrectly required the parent stamp to stay unchanged during a valid contact edit; the final check compares business fields before/after and saved stamps after the repeat tick. Production code was unchanged. This proves overlapping worker scopes on one due contact edit, not separate deployed hosts, guardian side effects, transition-worker overlap or live polling timing.

`records-transition-worker-race-localization-failure.trx` preserves the first failed transition-worker gate. The HTTP validation stack raised an ABP localization exception before the first legacy-override command entered Records business logic: the physical Development resource folder contained both `en.json` and the untracked Attendance resource `en-attendance.json` declaring culture `en` in one JSON contributor. That fresh database contained no transition or transition-event rows. The user authorized the shared repair: all 35 Attendance translations were consolidated into the canonical English resource, all 630 existing translations were verified unchanged, and the duplicate file was removed. The Domain.Shared rebuild passed with zero errors and seven warnings. This initial failure was a shared localization integration failure, not evidence for or against the worker race.

`records-transition-worker-race.trx` verifies the repaired run through historical-entry approval, published school rules, future withdrawal preview/submission and actual transition-worker execution. A pre-midnight tick preserves the scheduled case and active roster. At the effective institution date, two separate worker scopes reach enrollment writes on distinct serializable PostgreSQL connections; the provider naturally raises `40001`. The winning case remains Applied without an error, the original enrollment becomes inactive, the Student becomes Left, and exactly one Applied event records the original actor. Identity, GR, roll and placement remain unchanged; another actual tick preserves the complete saved result and history. The Application DLL still matched SHA256 `6724A27552D68D305A457451DE5B88676DA8FD4A153DCE28719767A9BACB1913`; the rebuilt Domain.Shared DLL was `DF870E0B586E92370FD27E23756BB8E93D1741BF9CFDB0D7E3CF2D22CB4138E3`. This covers one Direct-policy effective withdrawal, not every transition outcome, separate deployed hosts or live polling timing.

`records-populated-copy-migration.trx` verifies preservation on a separately restored local backup (SHA256 `A44DC66F75B09FC6877E32081EB3DB98E42EE3002D7A787FDF4A94753B04056F`). The test refuses non-loopback targets and requires the strict disposable-copy name and exact pre-Records migration. It hashes sorted JSON of every original column so additive certificate metadata is allowed without hiding data rewrites, compares all original indexes/constraints, requires exactly the three Records history additions and rechecks after idempotent migration retry. No seeder or live-database write runs. The retained copy contains 271 Students but zero enrollments; populated enrollment, Finance and issued-document history still require a representative pilot copy alongside the synthetic provider tests.

`records-admission-return-race.trx` passed **1/1**, zero skips, in approximately three minutes on the disposable database `records_tests_aret_7a29effaeee64cddb62a1aa1325de87a`. `Record_postgres_fresh_admission_returns_serialize_same_and_distinct_keys_without_duplicate_journeys` verifies same-key Inquiry requests and distinct-key Direct Registration requests through authenticated HTTP. Each case reaches two serializable PostgreSQL connections, observes a natural `40001`, creates exactly one return/batch/handoff/applicant/owner journey, and replays identical linked IDs. The losing distinct key cannot create another pending return. Every source business column, actor/history column, and complete personal-detail row remains unchanged; adding the dependent transition advances only the Student/enrollment relationship audit revisions. This matches [ABP 9 relationship tracking](https://github.com/abpframework/abp/blob/9.0.0/framework/src/Volo.Abp.EntityFrameworkCore/Volo/Abp/EntityFrameworkCore/ChangeTrackers/AbpEfCoreNavigationHelper.cs). Complete persisted rows, including audit revisions, stay identical after winning-key replay and a rejected competing retry. The barrier synchronizes the first parent-batch insert before a unique key can block the second request. Fixture validation uses the actual authenticated Offering availability endpoint; test-only HTTP timeouts are four minutes for constrained resources. This gate covers Fresh Application creation/replay, not concurrent finalization, Resume activation, multiple deployed hosts, or terminal-return closure. Production code, school data and live grants were not changed for this gate.

### Local runtime and browser evidence

After verified local backups, the three migrations were applied to localhost PostgreSQL (`localhost:5432/postgres`): `20260930175531_AddStudentRecordGovernanceAndOperations`, `20260930182702_AddStudentRecordTransitionTargetScope`, and `20261001021100_AddStudentRecordRenewalHistory`. The last migration only replaces the renewal unique index with an active-only predicate; completed/cancelled history remains intact. The current isolated Host was rebuilt and restarted; no seeders or staff-grant changes were performed. This is local verification, not a production rollout.

After the scheduling and report guards, the verified Records Host was refreshed to PID 26064. That Host build passed with zero warnings/errors. Its Host DLL SHA256 matched the one used by the passing midnight gate (`8E8B8ECD2C1E009F1FA3B9337DF92F1C40690DF366E3EFE965DBDB7493D679CF`), and its Application DLL matched the passing report-release runtime (`623F55844F5E70E762E18B576CEA110B3E797E89B9F0B7C283E78DD8C825AB39`). The authenticated browser reloaded Directory and Settings/Change Rules; Directory/options and Settings/roles requests returned HTTP 200. The selected school/year directory remained empty. Settings showed the saved draft and starter rules; no policy was saved or published during that smoke check.

After the renewal race fix, the Application build passed with zero errors and 130 warnings. The repaired PostgreSQL test passed against Application DLL SHA256 `6724A27552D68D305A457451DE5B88676DA8FD4A153DCE28719767A9BACB1913`. The local Host was refreshed to PID 2028 with a zero-warning/error Host build, and its Application copy matched that test runtime. Authenticated Directory/options reads returned HTTP 200 and the browser displayed the expected empty state against this Host. Both refreshes ran no migrations, seeders or grant changes.

The signed-in local browser loaded Directory, all four Settings task links, document policy choices, Certificates/Identity cards, and Reports. Temporary guardian-access edits were discarded after verifying dirty navigation protection. A real Directory report job reached Completed; its 6,344-byte Excel download returned HTTP 200 with the expected media type. The selected directory was empty, so this proves a zero-row export. Directory and Settings were rechecked against the refreshed Host. Direct Changes and Transitions navigation displayed No permission for the existing role. Directory, Settings overview, transition settings, Documents certificate/history and Reports preview were visually reviewed at 390×844, 768×1024 and 1440×900; a clipped mobile pagination footer was repaired and rechecked. Full student mutations, populated exports, document issuance and populated responsive browser journeys remain unverified.

### Known limitations and release gates

The latest browser connection recovered after earlier interaction timeouts. The existing signed-in school account rendered Directory's Active and Former views with filters, table headers, pagination and the expected zero-row empty state; neither view reported captured page errors/warnings. The user confirmed the school is still in testing with no expected record data. Year-End & Transitions still rendered No permission, and the sidebar omitted Transitions/Changes for this role. This verifies access denial and empty Directory rendering; it does not verify populated profiles or the return screens. No live student, policy or permission changes were made.

Class changes with an existing immutable Finance assignment are blocked at Placement request/application and same-year Return readiness/activation with `Students:Records:FinancialPlacementImpactRequired`. Finance currently has no class-impact/reassignment contract; its readiness reader cannot certify that an old assignment matches a changed class. The user deferred wrong-class renewal fee correction from this implementation. Before activation, PendingChecklist cancellation/recreation is available when no target dependencies exist. Completion cannot bypass these guards.

The approved Admissions return connection is implemented in source: Offering-configured Inquiry/DirectRegistration entry, explicit Both selection, frozen Records Status approval, server-bound configured intake and privacy masks, real New inquiry, AwaitingAdmission state, and tagged Onboarding return cases with reviewed same-year enrollment reuse. The focused frontend selection passed 20 tests across three files, scoped lint and affected-source TypeScript. The later full-project TypeScript check also passed with the project's configured 8 GiB heap; frontend-owned evidence is in [Student testing](../../../../Shikshamitra-FE/docs/student/testing.md). Browser retry attached successfully, but the signed-in role has no Transitions permission, so it displayed the expected denial screen; the full return journey remains unverified. Current Application source and referenced Domain/contracts build passed with zero errors after the final Resume options edit. The ten Domain transition tests passed without skips, including the three new return cases. EF source build including the additive return migration passed with zero errors; its idempotent SQL script generated successfully. Wrong-class fee correction is deferred.

The second nine-case Application return run (`records-admission-return-application.trx`) passed eight and failed the helper-only case (9 minutes 11 seconds; zero skips). Passing evidence includes actual configured Inquiry contact/qualification and application start/finalization, explicit Both entry, Records approval with self/unqualified/revoked-reviewer denials, public Direct finalization/activation/Finance collection/completion retry, public Resume finalization and activation, and independently reviewed historical first enrollment. Permanent GR/profile, shared guardian/sibling state and initial completed case history are asserted. The helper-only fixture omitted the save that real Onboarding activation performs before provisioning; its repaired focused rerun passed (`records-admission-return-helper.trx`, 3 minutes 30 seconds, zero skips). All nine unique return checks therefore passed across these focused runs; this is not a single clean nine-test run. An earlier run failed invalid Direct/Both fixture workflow configuration; production validators were preserved. These results do not establish production authentication, live browser journeys or fresh/resumed Admission return concurrency under PostgreSQL.

`StudentRecordAdmissionReturnMigration_Tests` passed both checks without skips (48 seconds; `records-admission-return-migration.trx`). The model check verifies the two affected Records entities match runtime and the target-model difference touches only the return-handoff/transition tables. The real PostgreSQL test used fresh `records_tests_admission_return_f4f7443d196745b694ce3d777ec899d9`, migrated from the preceding Attendance migration, preserved legacy duplicate application handoffs byte-for-byte, accepted a pending Inquiry with no Application ID, enforced new transition/application/inquiry uniqueness, refused a history-losing downgrade, and re-upgraded the unused fixture safely. This synthetic migration check does not replace representative school-copy or authenticated journey verification.

The reported local PostgreSQL `42703` for `AppStudentEnrollments.AttendancePlacementHistoryJson` was traced to the database ending at `20261001021100_AddStudentRecordRenewalHistory`. Applied only the existing owner migration `20261001043143_AddUniversalStudentAttendance` after a verified local backup. Transactional digest checks preserved Student, Identity, role membership, permission grants, Records tables and every original enrollment column; the migration's own Attendance bridge/backfill remains owner-defined. Both new enrollment columns and the applied migration history were verified. No seeders or host restart ran. Browser retry reached sign-in, so a signed-in dashboard/API retry remains unverified.

After the return migration/model checks passed, applied `20261001060643_AddStudentRecordAdmissionReturn` to the same verified local database with another inspected backup. Transactional original-row checks preserved Student/Identity/roles/grants, other Records tables, every existing enrollment and original Admission handoff data. All five additive handoff columns and the latest migration history were verified. The shared host was not restarted, and its live source/DLL freshness and authorized browser return journey remain separate checks.

The user approved automatic closure on 2026-10-03. `StudentRecordAdmissionReturnClosure_Tests` exercises the real Inquiry/Direct rejection owners and the real preactivation Onboarding cancellation owner, preserving the original completed case, profile, inactive enrollment and submitted history; old-key retries stay Cancelled, repeated closure adds no duplicate event, and a new reviewed request succeeds. This exposed and fixed reuse of the permanent Student ID as a new applicant's unique ReservedStudentId: each journey now reserves its own unused ID while retaining ExistingStudentId. The focused Application run passed five tests without skips (`records-return-closure.trx`), including the public Direct activation/completion test's postactivation cancellation denial and the ordinary rejection regression. The ordinary fixture now uses the existing Offering-aware start helper. No new migration is required. Offer withdrawal/decline and reopenable Inquiry outcomes retain their owner lifecycle semantics. Required Inquiry Upload fields retain the existing Admissions renderer limitation.

The extended real PostgreSQL HTTP gate passed one test without skips in 1 minute 44 seconds (`records-return-closure-postgres.trx`, fresh `records_tests_closure_ee893f65cb6543a093ca3082601f3415`). It retains natural SERIALIZABLE conflict coverage for same-key Inquiry and distinct-key Direct creation, then submits/rejects the actual Direct application, replays the winning key as Cancelled, and starts a distinct reviewed request. It asserts distinct reserved applicant identities, one closure event, preservation of every permanent profile business column, unchanged full enrollment/additional-detail rows at closure, and no duplicate enrollment. The initial extended run replayed the losing race key; the fixture was corrected to replay the actual winner. These HTTP tests use the test factory's permissive action checker and prove application/provider behavior, not production authentication or persisted role authorization. Student-only backend/frontend docs validators and diff hygiene passed; project-wide docs checks still report unrelated Finance reference frontmatter and frontend testing-module structure issues.

The 2026-10-03 browser check confirmed the current Admin login and diagnosed absent persisted Transitions action grants. With explicit temporary permission approval, the four Transitions grants were enabled through the existing Security UI; authenticated transition/renewal empty states, Returning students form and disabled batch preview/submit controls rendered. No eligible Student was offered, so a populated return journey remains unverified. All four temporary grants were removed through the UI afterward, and the original denial returned. A read-only local database comparison verified all other 9,431 permission rows retained the same digest. Frontend evidence belongs in [Student testing](../../../../Shikshamitra-FE/docs/student/testing.md). The shared API host was not restarted for closure changes; new closure runtime acceptance is still separate from test-host proof.

`StudentRecordAdmissionReturnOnboardingRacePostgres_Tests` passed one real HTTP/PostgreSQL test without skips in 1 minute 42 seconds on 2026-10-03 (`records-return-owner-race.trx`, fresh `records_tests_ownerrace_c819f680858047d18e47592088e937e6`). Two students complete actual Direct Admission and Onboarding placement/identifier readiness before concurrent activation and cancellation reach separate database connections. The run observed both outcomes: cancellation won with a natural PostgreSQL 40001 mapped to `Students.Onboarding.ConcurrencyConflict`/HTTP 400; activation won with an optimistic cancellation conflict/HTTP 409. Exactly one command committed each time, preserving one enrollment, permanent GR and additional details; cancellation preserved the complete inactive enrollment and closed the linked return once. The first run exposed an activation HTTP 500, repaired using the existing serialization conflict mapper. Activation concurrency conflicts now skip failure-state repair so the competing winner is preserved. The test barrier observes the owners' actual Serializable/ReadCommitted isolation and never injects provider failures. This factory uses test authentication/grants and model-created tables; production authorization, migration and populated browser acceptance remain separate gates.

After the activation conflict fix and shared test-barrier extension, the existing creation/rejection PostgreSQL gate passed again without skips in 1 minute 32 seconds (`records-return-closure-postgres.trx`, fresh `records_tests_ownerrace_fb12975a06e84004b0d6602bda8a7479`). Same-key Inquiry, distinct-key Direct, rejected-return replay and a distinct new reviewed journey remain verified.

Automatic allocation reuses the Onboarding owner and supports validated compatible rules. Rules requiring roster recalculation, `preserveExisting=false`, first-academic-day/manual-bulk timing or destructive insertion remain blocked until owner workflows exist. Do not describe every advanced numbering configuration as operational.

Before production release, verify the new Admissions return entry/enrollment reuse journey and migration; verify migration/backfill on a representative copy with populated enrollment/Finance/document history; remaining mixed approval/Admissions-return/renewal-completion and other transition-outcome concurrency under PostgreSQL; production authentication and persisted permission grants; live guardian change and review resolution; live worker deployment/timing; distributed report authority/cache behavior; and authenticated full journeys across desktop, tablet and mobile. The passing local-copy preservation, promotion, renewal activation, approval-stage and same-year return races, midnight and overlapping change/withdrawal worker ticks, report release checks and SQLite guardian journey narrow these gates but do not complete them. Use authorized pilot roles and representative student data. Passing automated tests and local smoke checks do not certify these remaining gates. Wrong-class renewal fee correction remains deferred and must be governed by Finance if introduced later.

TL;DR: run domain, application contract, EF mapping, and HTTP contract tests for Admission Offering changes, then validate module docs and migration safety.

For retained historical route-based onboarding service consent, run `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~Onboarding_service_consent_requires_an_audited_confirmation_without_bypassing_first_payment` and `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~StudentOnboardingApiContract_Tests`. This legacy scenario covers invalid and stale confirmation, an audited consent handoff, historical first-payment requirements, idempotent retry, and one enrollment. It does not define the current Offering-linked flow: that flow confirms a fee structure only, has no first-payment or service-consent gate, and preserves active enrollment when app access fails. These tests do not replace a deployed school-data walkthrough.

The room-derived class-capacity integration scenario allocates a persisted room, finalizes a direct RTE application, verifies the same occupied and remaining seats from a parallel General offering, and confirms that a full class still allows a new registration. Run `AdmissionWorkflowRuntimeIntegration_Tests` after changing routing or capacity; a calculator-only test does not cover the shared query. Applying the new migrations to a disposable PostgreSQL database and checking the live API remain release gates.

`20260924090000_AddAdmissionFormPurpose` uses active Offering references to classify existing Inquiry and Registration Forms; a name-based guess is only a fallback for unreferenced forms. If the same form is referenced in both slots, migration stops with a clear error instead of silently invalidating an active Offering. Resolve that existing-data conflict before rollout, then rerun migration verification against a disposable copy. The EF mapping test checks operation order and generates PostgreSQL Up/Down commands for all three new migrations without connecting to a database; only a disposable PostgreSQL migration run can prove SQL execution.

## Workflow route and document configuration checks

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~AdmissionWorkflowRouteDiagnostics|FullyQualifiedName~AdmissionWorkflowGraphValidator|FullyQualifiedName~AdmissionDocumentCollectionRules|FullyQualifiedName~AdmissionDocumentPolicyAndEvidence"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Managed_documents|FullyQualifiedName~Document_class_scope|FullyQualifiedName~Document_handoff|FullyQualifiedName~Document_finalization|FullyQualifiedName~Document_onboarding_replacement"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionDocumentApiContract
```

Route diagnostics reject identical simple conditions from one stage even when their priorities differ, unsupported managed Payment outcomes, and Offer/Seat outcomes excluded by the source policy. An Offer with Accepted disabled cannot use a default continuation; it needs a special outcome path. Complex preserved rules remain subject to the existing condition and graph validators.

The document integration files are partial members of `AdmissionWorkflowRuntimeIntegration_Tests`; filter by the method names above, not by their filenames. Confirm that the test report contains executed tests: a zero-test run can still return exit code zero.

On 25 September, this application filter executed and passed 24 tests with zero skips. Coverage includes early linked-Form collection and evidence reuse, class applicability, denied early review/unlinked upload, replacement review, private evidence scope, and finalization/handoff preservation and rollback. The Document HTTP contract suite passed nine tests. These are automated integration/contract results; live browser upload through finalization remains a separate acceptance check.

The focused `FullyQualifiedName~Rte_direct_registration_starts_at_form` selection subsequently passed both theory cases with zero failures or skips.
It verifies finalization from direct approval and from an accepted optional offer, and confirms an issued but unaccepted offer remains blocked.
Both cases execute actual finalization and Student/Onboarding linkage assertions.
The duplicate-offering fixture includes the required intake profiles so it continues to exercise duplicate rejection rather than failing earlier configuration validation.

The isolated Host build succeeded with zero errors and existing compiler/analyzer warnings.
Loading the current Host exposed missing intake-profile columns in the local PostgreSQL database.
After confirming `postgres` at `::1:5432`, a readable full custom-format backup was saved under ignored `.artifacts/backups/before-intake-profile-20260925.dump`.
The reviewed script applied only `20260925121515_AddAdmissionIntakeProfiles` from `20260925100000_AddAdmissionApplicationCounsellor`, in a transaction, with SHA256 `98DBADAE7B65C2771D7EAB6CBD7672EDE31B12F0201DCE43583D196CCA1D6FE0`.
It added two nullable Offering references, the intake-profile table, indexes, and foreign keys; the new table remained empty and no seeder ran.
The authenticated browser then finalized synthetic `APP-2026-000002` from its accepted optional offer and displayed the created Student identity and Continue onboarding link.
A scoped read-only query confirmed one retained PNG evidence row at Version 1 with its accepted review linked to that Student, and exactly one Student Onboarding case.
The browser opened that matching live case; the custom evidence had no document-category mapping, so this does not claim mapped onboarding-document completion or Student activation.
Student documentation validation and diff hygiene passed; the all-module documentation run is blocked separately by a mojibake error in the Finance charge-management test plan.

Document checks cover authoritative Offering class applicability, fail-closed missing class context, same-version forward Form collection links, shared evidence identity, and replacement invalidating prior review. Live API, permission-denial, finalization/handoff, and responsive browser acceptance remain separate release gates; domain-only success does not establish those outcomes.

## Admission Cycle validation

Run the focused domain and contract suites:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionCycle_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AdmissionCycleContract_Tests
```

Coverage includes code normalization, positive date ranges, Draft immutability boundaries,
publish/close/reopen/archive transitions, window revision, schedule ordering, institution-timezone
boundary evaluation, DTO validation, and the exact View/Add/Delete permission mapping. The frontend
pure-rule suite covers action availability and unused-Draft deletion.

Before release, generate and review the Admission Cycle migration, build the API independently from
any running debugger, and test one small, one medium, and one multi-board school configuration.
For each pilot, verify: future Academic Year selection; application dates beginning before the
Academic Year; open-date and close-date midnight behavior; publish readiness; active-Offering close
acknowledgement; reopen with extended date; archive dependency denial; stale concurrency denial;
server paging/search; View-only, Add, and Delete role behavior; and history visibility. Record any
board-specific result as configuration feedback, not a hard-coded board rule.

The migration `20260915105105_ImplementAdmissionCycleGovernance` is additive: it adds lifecycle and
schedule columns plus `AppAdmissionCycleHistories`. Automated tests and generation do not apply the
migration or restart the externally managed API host.

## Focused commands

Run from `Shikshamitra-BE`:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionOffering_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AdmissionOfferingContract_Tests
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~AdmissionOfferingModelMapping_Tests
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionOfferingApiContract_Tests
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflow
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflow
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflow
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflowRuntimeIntegration
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```

## Coverage map

| Surface | Evidence |
|---|---|
| Effective dates and availability boundaries | `test/ShikshaMitra.Domain.Tests/Students/Enrollment/AdmissionOffering_Tests.cs` |
| Bulk inactive/activation readiness and exact View/Add/Delete action aliases | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionOfferingContract_Tests.cs` |
| Defaults, nullability, indexes, restrictive FKs | `test/ShikshaMitra.EntityFrameworkCore.Tests/Students/Enrollment/AdmissionOfferingModelMapping_Tests.cs` |
| Required HTTP routes and concurrency inputs | `test/ShikshaMitra.HttpApi.Tests/Students/Enrollment/AdmissionOfferingApiContract_Tests.cs` |
| Track-aware same-class create/update rejection, shared room-derived capacity, non-blocking full-class warning, and inactive-enrollment seat release | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionWorkflowRuntimeIntegration_Tests.cs` and `AdmissionClassCapacityIntegration_Tests.cs` |
| Capacity read/configuration HTTP permissions and routes | `test/ShikshaMitra.HttpApi.Tests/Students/Enrollment/AdmissionOfferingApiContract_Tests.cs` |
| Same-cycle duplicate signals, scoped prior-cycle family linking, name-only rejection, and inaccessible-cycle denial | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionWorkflowRuntimeIntegration_Tests.cs` |
| Workflow graph, controlled conditions, handlers, and runtime state | `test/ShikshaMitra.Domain.Tests/Students/Enrollment/AdmissionWorkflowGraphValidator_Tests.cs` and `test/ShikshaMitra.Domain.Tests/Students/StudentEnrollments/Admissions/AdmissionWorkflowConditionEvaluator_Tests.cs` |
| Required-stage skip conflicts and runtime-unavailable publication blocks | `test/ShikshaMitra.Domain.Tests/Students/Enrollment/AdmissionWorkflowGraphValidator_Tests.cs` |
| Frozen published version, complete version-history definitions, per-version runtime analytics, transition history, retry idempotency, and future-application version selection | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionWorkflowRuntimeIntegration_Tests.cs` |
| Form version pinning and runtime lifecycle | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~Published_Form_Version_Should_Remain_Frozen` and `--filter FullyQualifiedName~Form_Runtime_Should_Save_Submit_Audit_And_Reopen` |
| Form aggregate lifecycle and soft-delete-safe defaults | `dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionFormDefinition` |
| Approved choice sources and authoritative option membership | Application build plus form runtime integration tests; source keys are allow-listed by `AdmissionFormDataSourceRegistry` and checked again during submit |

## Assessment validation

Use isolated output directories when the API host holds normal build output files open.

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --artifacts-path .artifacts/assessment --filter "FullyQualifiedName~AdmissionAssessment|FullyQualifiedName~AdmissionWorkflowGraphValidator"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path .artifacts/assessment --filter "FullyQualifiedName~AdmissionWorkflowRuntimeIntegration|FullyQualifiedName~AdmissionAssessmentScope"
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --artifacts-path .artifacts/assessment --filter FullyQualifiedName~AdmissionAssessmentModelMapping
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path .artifacts/assessment --filter FullyQualifiedName~AdmissionAssessmentApiContract
```

Domain tests cover legacy policy compatibility, scoring bounds, attendance, review, retakes, waiver evidence, cancellation, and session capacity.
Application integration tests cover published managed workflows, blocked advancement, passing retakes, reviewer separation, wrong application/stage ownership, tenant isolation, scheduled capacity, stale stamps, and cancellation.
The scope query test checks that a narrow standard grant does not expand to another standard under the same board or another academic year.
`AdmissionWorkflowRuntimeIntegration_AssessmentRoster_Tests.cs` covers initially unbooked applicants, attendance/result queues, failed-route readiness, passing retakes, completed history, session/search/count scope, pagination and cross-tenant rejection. `AdmissionAssessmentApiContract_Tests` verifies the additive query-bound roster route and View authorization at both controller and direct application-service boundaries. Run the focused roster cases with `--filter FullyQualifiedName~Assessment_roster` using the Application test project above.
The 2026-10-07 workbench verification passed 13 selected Application assessment/roster/scope scenarios and 10 assessment API metadata tests; the isolated host build passed with no errors. Application tests ran from an isolated copy of the freshly compiled test runtime because another local test process locked shared test outputs. These tests do not replace authenticated PostgreSQL/browser acceptance or prove tenant-wide reporting-scale performance.

`AdmissionAssessmentRosterPostgres_Tests` executes the actual roster, access, read and runtime-fact services using Npgsql/EF against a fresh loopback `admissions_assessment_tests_*` database. Set `ASSESSMENT_ROSTER_TEST_POSTGRES` to a connection string with a new disposable name, then run the Application project with `--filter FullyQualifiedName~AdmissionAssessmentRosterPostgres_Tests`. The fixture rejects existing databases, creates only synthetic admission tables and data, injects scope/permission collaborators without creating identities/grants, and removes only its own newly created database. Its model preserves the relevant production lookup indexes but omits unrelated ERP tables, foreign-key graphs and aggregate extra-property payloads; it does not certify the production schema or authorization pipeline.

On 2026-10-07, PostgreSQL 18.6 with Npgsql 9 ran all 100/1000/5000 scoped-candidate cases successfully. Warm service reads took 80.3/187.8/289.2 ms respectively, with 12 SQL commands and 121 complete history rows for a 25-row page at every size. The fixture uses seven mixed queues, four cancelled historical attempts per applicant, two sessions, and real configured JSON routes/form facts (including spoofed managed facts). It checks complete counts before paging, a late sorted page, queue/search/session filters, session-date ordering, self-review denial and preserved cancelled history. These are individual local observations, not production latency guarantees or concurrency/load evidence. Candidate classification and route facts still scale with the selected offering/stage/search population; staging should verify representative payloads, simultaneous callers, cold starts and larger scopes before a broad rollout.
Mapping and contract tests check persisted evidence, restrictive links, unique attempt numbers, concurrency, additive routes, and distinct permissions.
Generate and inspect the additive migration SQL without applying it to a shared database; verify the model snapshot matches the current EF model.

### Test infrastructure and lifecycle ordering

The certification fixture completes submitted-form stages before approving the application, pauses at the Approval stage, performs the actual review and approval decisions, and then completes the remaining workflow.
This preserves the production Form requirement for a submitted application and the Approval requirement for an approved application.
It refreshes application concurrency after workflow advancement before issuing an offer.

The shared HTTP factory no longer references the obsolete `AdmissionCrmTransactionFailureInterceptor` or seeds the deleted `AdmissionCrmRouteDefinition`; both belonged to the removed Admissions CRM HTTP suite.
Keep unrelated test-only interceptors, including SQLite owned-tag identity generation, intact when maintaining the factory.
Full HTTP tests bootstrap shared demo data and can take several minutes during academic permission scope synchronization. The focused reflection contract tests do not boot this host; report authenticated server checks separately from contract metadata checks.

## Managed Inquiry validation

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionInquiryPolicyAndManagement_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Managed_inquiry_should|FullyQualifiedName~Basic_inquiry_workflow_should|FullyQualifiedName~Inquiry_list_should_support_unassigned_and_counsellor_queues|FullyQualifiedName~Inquiry_communication_should_create_a_traceable_draft"
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~AdmissionInquiryManagementModelMapping_Tests
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionInquiryManagementApiContract_Tests
dotnet ef migrations has-pending-model-changes --project src/ShikshaMitra.EntityFrameworkCore/ShikshaMitra.EntityFrameworkCore.csproj --startup-project src/ShikshaMitra.DbMigrator/ShikshaMitra.DbMigrator.csproj --no-build
```

The inquiry runtime integration check covers unassigned and assigned queues, offering-scoped counsellor lookup, server-filtered first-contact and open-follow-up queues, list-row enrichment, editable preferred language, and a traceable one-recipient Communication draft. The HTTP contract check keeps edit, timeline, CSV export, bulk assignment, work-queue, row SLA/readiness, communication route, permission, request, and result fields public.

Application Experience coverage additionally verifies persisted form purpose, purpose-preserving clones and versions, purpose-safe Offering configuration, the seven canonical document-condition facts, and the nine lifecycle templates remaining in the Communication contract.

The 2026-09-11 focused verification passed 10 Domain policy/aggregate tests, two real Application lifecycle scenarios, one EF mapping test, and nine HTTP route, permission, and safe-contract checks.
The Basic scenario proves existing application start behavior remains available without an enabled Inquiry policy.
The Managed scenario proves contact and qualification gates, persisted linkage, and successful application start after readiness.
EF reports no model changes after the migration, and reviewed idempotent SQL contains only the two Inquiry tables, nine supporting indexes, restrictive foreign keys, and migration-history insert.
The migration has not been applied, and the externally managed running Host has not been restarted for this delivery.

## Payment validation

Payment policy and aggregate tests cover money precision, methods, installments, non-cash references, independent verification, rejection, gateway settlement, failure, reversal, waiver, and retry safety. The application lifecycle test proves one transaction survives record and verification retries, the same collector cannot satisfy dual control, pending payment blocks workflow completion, and verified settlement permits advancement. EF mapping checks cover restrictive foreign keys and unique reconciliation indexes; HTTP contract tests cover all five action permissions.

The additive Payment migration SQL was generated and reviewed from `20260911045603_AddAdmissionInquiryManagement` to `20260911064407_AddAdmissionPaymentRuntime`. It creates only `AppAdmissionPaymentTransactions` and its keys/indexes. It has not been applied.

## Acceptance gates

Document Verification checks use `.artifacts/document-verification` with `AdmissionDocumentPolicyAndEvidence` and `DocumentFileValidator` Domain filters, `Managed_documents`, `Document_handoff`, and `Document_finalization` Application filters, `AdmissionDocumentModelMapping` EF filter, and `AdmissionDocumentApiContract` / `StudentAdmissionEvidenceApiContract` HTTP metadata filters. Application tests execute real services and isolated test storage; HTTP reflection checks prove route/permission metadata, not an authenticated network request. Validate the additive migration script and pending-model state before any explicitly authorized database rollout.

Coverage includes legacy opt-in behavior, conditional missing/blank values, mandatory skip/override gates, stale stamps, replacement review reset, both-mode original checks, immutable reviews, independent collector separation, private downloads, tenant/stage scope, same-Student idempotent linkage, preservation of existing Student documents, and explicit transaction rollback during finalization.

The 2026-09-10 focused verification passed 92 Domain tests (documents, shared files, assessments, graph validation), 54 Application runtime/scope tests, 20 HTTP route/permission metadata checks, and 2 EF mapping checks. Application coverage includes rejecting an incomplete both-mode upload before original inspection, UTC evidence timestamps, and preserving the previous onboarding upload through rollback before cleaning it after a successful replacement. Generated migration SQL contains only the two new tables and supporting indexes/foreign keys; the EF model matches its snapshot. These automated checks do not substitute for a separately recorded authenticated browser/server smoke test.

Do not release if an active offering can bypass published configuration, an out-of-scope hierarchy is visible or writable, runtime versions are not frozen, identity changes after applications exist, or migration SQL drops historical data.

## Eligibility validation

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --artifacts-path .artifacts/eligibility-build --filter "FullyQualifiedName~Eligibility|FullyQualifiedName~AdmissionWorkflowGraphValidator"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path .artifacts/eligibility-build --filter "FullyQualifiedName~AdmissionWorkflowRuntimeIntegration|FullyQualifiedName~AdmissionAssessmentScope|FullyQualifiedName~SqliteEligibilityCalendarDate"
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --artifacts-path .artifacts/eligibility-build --filter FullyQualifiedName~AdmissionEligibilityModelMapping
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path .artifacts/eligibility-build --filter FullyQualifiedName~AdmissionEligibilityApiContract
```

Eligibility tests cover legacy/disabled policy compatibility, schema and calendar boundaries, unknown versus blank answers, decimal numeric equality, whole-choice membership, explicit upstream stage selection, linked-Student DOB freshness independent of the application stamp, immutable exception history, real configured role/separate reviewer checks, tenant and offering UserScope denial, negative routing, skip/override/finalization gates, successful finalization retry, and the real reopen/edit/resubmit/re-evaluate workflow. Document freshness tests append an external immutable correction after source completion to verify invalidation; normal document APIs continue to prevent historical edits.

Offering compatibility tests exercise exact application/inquiry form versions, create/update/activation rejection, incomplete inactive drafts, persisted mismatch readiness and list filtering, and legacy/disabled behavior. Keep captured query ID collections as `List<Guid>`: the current compiler can bind array `Contains` to a span overload that EF cannot evaluate. The SQLite application-test provider adds only the missing `DateTimeOffset.Date` translation; a focused SQL-filter test covers midnight, end-of-day, offset crossing and null. A separate Npgsql `ToQueryString` test verifies the mapped production date, membership and field-subquery predicates without opening a connection. Production queries continue to filter and paginate in SQL.

EF tests inspect tenant/concurrency mappings, unique evaluation sequence and exception decision keys, and restrictive foreign keys. HTTP tests inspect actual controller/service authorization metadata and response contracts; they do not claim an authenticated network request. Generate and review Eligibility's additive migration and verify the model snapshot before a separately authorized deployment.

Validation on 2026-09-10 passed 131 focused domain tests, 91 application regressions (including 54 existing admissions cases), 27 HTTP contract metadata checks, and four EF mapping/query-translation checks. The refreshed isolated host build passed, the model check found no pending changes, and all module documentation and diff checks passed. No tests were skipped. The initial offering regression failures identified a captured-array EF expression issue and SQLite's missing calendar-date member translation; both were corrected and the same assertions passed on the final run.

The reviewed guarded local rollout then applied `20260910133935_AddAdmissionEligibility` once to the local PostgreSQL database (`localhost:5432/postgres`, resolved to `::1/128`). Its result reported committed and verified: migration history contains 52 entries with this migration latest, both new tables are empty, and the schema has ten indexes including the two primary-key indexes plus six restrictive foreign keys. No seeds or unrelated migrations ran.

### Live verification on 2026-09-11

The API was restarted externally after the earlier automatic approval review rejected the agent's restart command.
That rejection remains historical evidence; a manual restart is no longer a pending action.

| Check | Observed result |
|---|---|
| Running host | PID 30256, parent 21436, started 07:38 IST; serves port 44332 from the Debug/net9.0 output of `src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj` |
| Compiled implementation | Six running-output DLL/PDB identities match; all 57 checked Eligibility, Document, workflow/finalization and Offering source checksums match the current workspace |
| Public API metadata | Student Swagger returns HTTP 200 with normal TLS verification; includes six canonical Eligibility routes, seven Document Verification routes, current action flags, Student admission evidence and onboarding document routes |
| Database | The host has an established connection to `::1:5432`; a fresh read-only metadata query confirms local `postgres`, history 52, both migrations present, Eligibility latest, two Eligibility tables, ten indexes and six foreign keys |
| Authenticated setup reads | The Codex browser loaded real reviewer roles, published form choices and category metadata from the current API |

The existing offerings use legacy workflows, so a complete live managed applicant mutation journey has not been tested.
The Student admission-evidence live read initially failed because its profile client called a missing endpoint.
The profile repair described below is prepared in isolated output; the running debugger has not been rebuilt or restarted as part of that repair.
These coverage gaps remain distinct from the passing automated services/tests and the verified live setup reads.

## Student profile and admission-evidence integration

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path .artifacts/student-profile-readiness --filter "FullyQualifiedName~Student_record_profile|FullyQualifiedName~StudentRecordScope|FullyQualifiedName~Document_handoff|FullyQualifiedName~Document_finalization|FullyQualifiedName~Document_onboarding"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path .artifacts/student-profile-readiness --filter "FullyQualifiedName~StudentRecordProfileApiContract|FullyQualifiedName~StudentAdmissionEvidenceApiContract"
dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj --artifacts-path .artifacts/student-profile-readiness
```

Profile service tests read canonical Student identity and placement, preserve null optional values, and prove that reads do not mutate the Student concurrency stamp.
They cover missing records, foreign tenants, absent scope, another standard in the same board, assigned and unassigned divisions, explicit wider grants, and the finalized Student GUID leading to accepted admission evidence.
The original narrow-standard denial assertion exposed an existing direct-record guard bug: ancestor IDs from narrow assignments were treated as wider grants.
The shared Student guard now checks only explicit wildcard grants or the exact division within the assigned academic year; profile and admission-evidence list/download denials use this guard.
Separate focused cases exercise each wider grant and the academic-year boundary.
Existing document handoff, finalization, onboarding readiness, and rollback checks run alongside the profile tests.
The HTTP tests inspect the explicit GUID route, both permission attributes, the result envelope and the scalar DTO allow-list; these are metadata checks, not authenticated network requests.
This repair requires no migration and does not modify the live database or running API process.

Final verification on 2026-09-11 passed all 36 selected Application tests, all six HTTP contract metadata tests, the isolated Host build, and the module documentation/diff checks, with no skipped tests.
The Application selection contains seven real profile scenarios, six explicit scope-grant/year cases, and 23 existing document handoff/finalization/onboarding regressions.

For onboarding configuration changes, validate schema version 1 and an object-shaped JSON payload, verify the filtered unique school and academic-year indexes in the generated migration, and exercise GET/PUT/DELETE concurrency behavior. Frontend persistence tests must also confirm that removing an academic-year override inherits the saved school configuration while removing the school override restores built-in defaults.
The prepared Host is `.artifacts/student-profile-readiness/bin/ShikshaMitra.HttpApi.Host/debug/ShikshaMitra.HttpApi.Host.dll`.
An external API restart on 2026-09-11 loaded the normal-output Host into PID 47960; the read-only proof matched the current profile and exact Student direct-record scope sources to the loaded build.
The public Swagger response included the GUID profile endpoint, and the process had established database connections only to local PostgreSQL at `::1:5432`.
The subsequent authenticated browser check populated the real Student name, GR number, roll number, class and medium, then loaded Documents and the expected empty linked admission-evidence result; Refresh also succeeded.
No Student or evidence mutation was performed during that live check.
The earlier restart restriction is historical and is not a current profile-delivery blocker.

## Review validation

Run from the backend repository:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --artifacts-path .artifacts/admission-review-build --filter FullyQualifiedName~AdmissionReview
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path .artifacts/admission-review-build --filter FullyQualifiedName~Review_
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --artifacts-path .artifacts/admission-review-build --filter FullyQualifiedName~AdmissionReviewModelMapping
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path .artifacts/admission-review-build --filter FullyQualifiedName~AdmissionReviewApiContract
```

The Application suite executes real service methods for draft/submit/decision separation, configured roles and assignees, tenant/stage/UserScope isolation, concurrency, immutable history, correction/resubmission, selected upstream facts, canonical identity and document freshness, explicit negative routes, skip/override denial, historical reaffirmation and successful finalization retries.
Offering compatibility tests exercise current reviewer role/activity, exact standard scope on create/update/activate, and disabled/legacy compatibility.
Run the existing application workflow, Assessment scope, Eligibility calendar, Student profile and direct-record scope selections alongside this change because they share decision, form and finalization paths.
HTTP tests inspect route/permission metadata and safe DTO boundaries; they do not claim authenticated network mutations.
Review migration `20260911034211_AddAdmissionReview` adds only the three Review tables, ten indexes and seven restrictive foreign keys; no seed data or existing records are changed by its Up operation.
Inspect the generated SQL and verify no pending EF model changes before any separately authorized rollout.

The 2026-09-11 automated verification passed 25 Review Application cases and 104 existing Application regressions, with zero failures or skips.
The combined Domain selection passed 157 tests, HTTP route/permission metadata passed 37 tests, and EF mapping/production-query translation passed five tests.
The initial Review test run exposed a missing condition-field registration; publication now explicitly supports only emitted `review.status` and `review.decision` fields and rejects invented Review fields.
Application evidence includes default-reviewer standard/role/activity compatibility, approval followed by skip denial, middle-name changes without an application-stamp change, and explicit negative decisions using the existing admission endpoints.
These isolated checks and frontend fixture checks do not establish a live configured applicant mutation journey.
The final isolated Host build succeeded with zero errors and seven existing compiler/analyzer warnings.
The compiled model has no pending changes, and the generated Eligibility-to-Review SQL contains only the reviewed additions and its migration-history insert within one transaction.
The prepared Host is `.artifacts/admission-review-build/bin/ShikshaMitra.HttpApi.Host/debug/ShikshaMitra.HttpApi.Host.dll`; loading it into the externally managed API remains a separate operational action.

The reviewed migration was committed and verified on local PostgreSQL (`postgres`, `::1/128`, port 5432) at 2026-09-11 03:53:53 UTC using a pinned SQL hash, a verified backup and transactional pre/post checks.
Migration history now contains 53 entries with `20260911034211_AddAdmissionReview` latest; its three tables have thirteen indexes including primary keys and seven restrictive foreign keys.
All three Review tables remain empty, no seeders ran, and no unrelated migration was applied.
The current API is still the older PID 47960; an external rebuild/restart must load the prepared Review implementation before authenticated Review HTTP behavior can be verified.
The successful profile/Documents read remains valid historical evidence and does not establish that the new Review endpoints are live.

## Managed Form validation

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionFormStagePolicy_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~Managed_Form_Should_Hold_The_Workflow_For_Verification_And_Audit_Corrections
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionFormStageApiContract_Tests
```

The Domain suite covers bounds, cross-field rules, section uniqueness, unsupported versions, and legacy opt-out. The real Application workflow test freezes a compatible form, submits without automatic advancement, verifies a section, requests correction, proves prior verification becomes stale, resubmits as attempt two, verifies again, advances, and checks immutable audit history. HTTP reflection tests cover all three routes, controller and service permissions, and safe DTO metadata. This feature reuses existing form persistence and needs no migration.

## Managed Offer validation

Run the focused Offer policy/aggregate/route tests, the real application lifecycle test, EF mapping test, and HTTP contract metadata tests. Coverage includes legacy opt-out, content and validity limits, issue retry safety, workflow blocking before response, acceptance confirmations, decline, extension, withdrawal, reissue, explicit negative-outcome routing, permission separation, safe DTOs, and the filtered idempotency index.

The 2026-09-11 focused run passed 26 Domain tests, one real Application workflow test, one EF mapping test, and seven HTTP contract tests with zero failures or skips. The DbMigrator and HTTP API builds completed with zero errors; existing repository warnings remain. Generated SQL from `20260911064407_AddAdmissionPaymentRuntime` to `20260911071841_AddManagedAdmissionOffers` adds only four nullable decision-history columns, one filtered unique index, and the migration-history row in a transaction. It has not been applied.

## Managed Seat validation

Run the focused Seat policy, allocation and route tests; the database-backed managed workflow tests; the EF mapping test; and the HTTP contract tests. Coverage includes legacy opt-out, policy bounds, automatic reservation on entry, capacity protection, idempotent retry, confirmation gating, FIFO waitlisting, promotion after capacity growth, post-advance release, explicit negative routing, tenant uniqueness, permission separation, and safe DTOs.

The 2026-09-11 focused run passed 16 Domain tests, two real Application workflow tests, one EF mapping test, and eight HTTP contract tests with zero failures or skips. The HTTP API build completed with zero errors. EF reports no pending model changes. Generated SQL from `20260911071841_AddManagedAdmissionOffers` to `20260911084131_AddManagedAdmissionSeats` creates only the two Seat tables, their foreign keys and indexes, and the migration-history row in a transaction. It has not been applied.

## Class capacity and counsellor validation (2026-09-25)

This review applied the current Admissions migrations to a newly created disposable localhost PostgreSQL database named `shikshamitra_admissions_disposable_20260925`; no live deployment or existing API restart was performed by this review. The disposable database's migration history contains 79 entries, ending at `20260925100000_AddAdmissionApplicationCounsellor`. Read-only checks confirmed the application counsellor column and that class-room allocation foreign keys reference `academic_Rooms`, `academic_Standards`, and `academic_Years`. The existing local `postgres` database was initially observed at 77 migrations and later at 80, including the two new Admissions migrations; the source of that concurrent change was not established, so do not assume it was unchanged. EF reported no pending model changes. The focused Application selection passed 25/25 tests, including RTE direct registration and managed-offer counsellor assignment; capacity boundary/integration selection passed 5/5; EF mapping/migration selection passed 6/6; and HTTP route/permission contracts passed 5/5. A later focused integration rerun also passed the explicit assertion that an RTE/Direct Registration offering cannot accept an Inquiry. Authenticated browser inspection reached the Applications list and direct-registration empty state, but no configured direct-registration offering was available for an end-to-end applicant journey.

## Public inquiry link compatibility validation (2026-10-07)

The focused `Public_link`/`Public_intake` Application selection passed 18 tests with no failures or skips. It covers absent active Inquiry Offerings, unmatched setup/form diagnostics, corrected retries without prior insertion, compatible period options, inactive/direct-registration Offerings, archived setups, draft forms, unscoped option reads, owner/fallback readiness, expiry, and existing public intake. Four HTTP intake-contract tests passed, including the explicit authenticated options route, Offering View authority at both service and controller, and query-bound period. The Host build succeeded and a refreshed localhost Host served authenticated options and link creation; the [frontend verification](../../../../Shikshamitra-FE/docs/student/testing.md) records save/reload and temporary-link revocation.

Student documentation validation passed; the all-module gate remains blocked by unrelated Finance mojibake. No schema migration or staging deployment was performed.

## Simplified inquiry intake validation (2026-10-04)

Seven focused Application tests and thirteen existing Domain policy/aggregate tests passed with zero failures or skips.
`AdmissionIntakeStandardBehavior_Tests.cs` covers old profile and snapshot requirements, optional QR-code detail, capture and Offering confirmation without policy-required email/date of birth, automatic duplicates with a separate-family reason, immediate application start without contact/qualification, normalized setup writes, historical JSON preservation, and the active scoped staff lookup.
The existing Inquiry workflow test also verifies optional contact/qualification actions and locking after application start.
The local Host build succeeded and its refreshed process served a successful authenticated intake setup create/reopen flow; see [frontend browser checks](../../../../Shikshamitra-FE/docs/student/testing.md).
Student documentation validation passed; the all-module gate stops on existing Finance reference frontmatter/mojibake issues.
This change introduces no schema migration and was not deployed to production.

## Intake counsellor role lookup validation (2026-10-10)

The focused Application selection passed 3 tests with zero failures or skips: the two `Counsellor_role_lookup` integration regressions and the existing broad candidate-scan regression.
The role cases prove canonical `Admissions Counselor` membership before the 50-result limit, active identity and admission scope exclusions, default broad lookup compatibility, and membership revocation on the next lookup.
The five `AdmissionIntakeSetupApiContract_Tests` passed, including optional query-bound `counsellorOnly=false` and unchanged Admission Offerings View enforcement on the controller and application service.
The isolated Host build passed with zero warnings or errors; these checks used single-worker builds with Roslyn analyzers and shared compilation disabled after the initial build exhausted available local memory.
No permissions, migrations, runtime assignment validation, or deployed data were changed by the role lookup implementation.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --artifacts-path .artifacts/intake-role-review --filter "FullyQualifiedName~Counsellor_role_lookup|FullyQualifiedName~Counsellor_lookup_should_scan" -m:1 -p:RunAnalyzers=false -p:RunAnalyzersDuringBuild=false -p:UseSharedCompilation=false
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --artifacts-path .artifacts/intake-role-review --filter FullyQualifiedName~AdmissionIntakeSetupApiContract_Tests -m:1 -p:RunAnalyzers=false -p:RunAnalyzersDuringBuild=false -p:UseSharedCompilation=false
```

## Counsellor lookup scale regression

The intake counsellor lookup resolves each candidate page in a batch rather than resolving scope separately for every user.
Eligibility still requires an active current-tenant identity and effective scope from current role membership, direct assignments, or reporting inheritance.

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~UserScopeResolution_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Counsellor_lookup_should_scan_many_active_users|FullyQualifiedName~Setup_writes_should_ignore_retired_requirements"
```

The Domain fixture contains 120 candidates and verifies fixed batch reads, administrator/direct/role/reporting access, denied access, request-local reuse, revocation on the next request, and failure without cached authority.
The database-backed Application fixture contains 120 active identities with one eligible identity in the second candidate page.
It must return that identity while excluding the unscoped identities.

Before release, use a disposable local tenant with representative identity, scope-assignment and reporting-hierarchy counts.
Run the authenticated `GET /api/student/enrollment/admissions/intake-profiles/counsellors` once cold, once for warm-up, then 20 times for timing samples.
Record fixture counts, cold latency, query counts, p95 and every response status; an HTTP failure or request approaching the gateway timeout blocks release.
Repeat with sparse scope assignments so the lookup scans several candidate pages, and with simultaneous callers.
The existing 50-result limit remains part of this endpoint and must not be mistaken for a load failure.

After deploying the backend build to staging, verify the same endpoint with the QA account and representative tenant data.
Require a successful response before declaring QA unblocked, then follow the [frontend browser checks](../../../../Shikshamitra-FE/docs/student/testing.md) for visible options, selection, setup save and reload.
Local synthetic tests alone do not certify staging or production performance.
## Student dashboard validation

Run from the backend repository:

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~StudentModuleDashboardApi_Tests --artifacts-path .artifacts/student-module-dashboard/build --logger "trx;LogFileName=student-module-dashboard.trx"
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -AllModules
git diff --check
```

Tests use real application services, scope resolution, active database identities and SQLite persistence; the permission test header only controls exact grants. Coverage includes anonymous/denied callers, action-without-visibility, unknown identity/tenant/year/section, translated aggregates, direct service authorization, forged administrator claims, no academic scope, sibling-division exclusion and verified Parent-only associations despite staff grants. The workload scenario grows owned inquiries from 10 to 510, retains 10 other-owner inquiries and one owned/one other-owner setup case, checks constant query count and payload under 16 KiB, and records cold latency plus 20 samples after three warmups. Fixture latency is local SQLite API evidence, not a production PostgreSQL SLA or query-plan benchmark.

Admin regression cases add scoped filter authority without Directory permission, a bounded four-case setup preview, recorded task counts, primary-blocker precedence, and Records dashboard/list reconciliation excluding the requester, wrong stage roles and a prior approver.
Attendance parity compares the batched owning projection with individual rosters for missing calendar, working/holiday dates, draft, clean submission and approved-leave/revision changes.
The scale fixture seeds 50 divisions and checks a constant SQL read count for 1, 10 and 50 requested registers.
Run the owning Attendance API regressions alongside dashboard tests after changing these shared evaluators:

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~StudentModuleDashboardApi_Tests|FullyQualifiedName~StudentAttendanceApi_Tests" --artifacts-path .artifacts/student-dashboard-v3 -m:1 -p:UseSharedCompilation=false -p:RunAnalyzers=false
```

This local command skips analyzer execution and does not substitute for the repository's normal analyzer/release gates.
Keep fixture Student reads on `GetQueryableWithoutAutoIncludesAsync`; full Student graph reads can produce an unnecessary SQLite temporary sort and exhaust disk space in a large synthetic cohort.
Record local test results separately from an authenticated PostgreSQL browser retry and deployment verification.

Local validation on 2026-10-14: dashboard API tests passed 18/18 and owning Attendance API regressions passed 31/31 on the current production source.
The batched daily projection used 45 SQL reads for each of 1, 10 and 50 requested divisions in the SQLite fixture, preserving exact count equality and a local ceiling of 50 reads.
This is a bounded-query regression result, not a PostgreSQL latency or throughput guarantee.
The isolated local build retained nullable checking and skipped analyzer execution with the command above.
Student-only documentation validation passed; the all-module validator remains blocked by existing mojibake in `docs/modules/finance/references/charge-management-e2e-test-plan.md`, which this Student change does not modify.

For release, verify selected-year and scoped counts against the owning worklists using a signed-in school environment and measure PostgreSQL execution plans/p95 with representative tenant sizes. Local browser sample-response evidence remains owned by [frontend tests](../../../../Shikshamitra-FE/docs/student/testing.md#student-dashboard-validation).

## Offering-linked Onboarding verification (2026-10-09)

Current-source focused Domain tests passed **37/37**, zero failures or skips, in 71 ms. Current-source focused Application tests passed **8/8**, zero failures or skips, in 3 minutes 14 seconds. The final runs retained repository `Nullable=enable` and normal analyzers. The running compiler's generated response file was inspected to confirm nullable analysis, analyzer arguments and the serial compiler response file.

The Domain filter is `FullyQualifiedName~StudentOnboardingIdentifierRules_Tests|FullyQualifiedName~StudentOnboardingLegacyVersion_Tests`. Coverage includes independent identifier locks, legacy/canonical GR protection, correction reasons, duplicate Offering rules, saved Draft before Publish, immutable published/superseded history, stale sibling publication, history-removal/status-demotion rejection, and authoritative school-history inheritance when first creating a year override.

The Application filter is `FullyQualifiedName~Offering_fee_reference_confirmation_should_gate_activation_without_finance_payment_or_assignment|FullyQualifiedName~Document_handoff_accepted_workflow_evidence_satisfies_frozen_onboarding_requirements_without_waivers`, in the partial `AdmissionWorkflowRuntimeIntegration_Tests` fixture. The selected cases are:

- Offering fee reference: `(prepareAccess:false, failAutomaticAccounts:false)`, `(true,false)` and `(true,true)`.
- Frozen document evidence: Upload, Both, Original and Custom accepted cases, plus a Waiver case whose readiness must remain false.

The Offering cases exercise actual finalization, frozen initial checklist truth, independent GR/Roll assignment, fee-structure confirmation without Finance assignment/dues/payment, activated Records visibility after fresh reads, and an aborted automatic-account database transaction that preserves enrollment and records access pending. They also test persisted policies for an initially empty locked division, automatic next GR/Roll despite manual configuration, Roll 1 in separate divisions, editable placement with locked Roll collision rejection, canonical-only GR preservation, and CBSE numeric projection beyond a canonical-only GR held by another board. The numeric Records API's first assignment is verified through persisted `GrNo`; it does not introduce a new formatted-GR editor contract.

Final TRX artifacts are `.artifacts/onboarding-standard-results/onboarding-domain-standard.trx` and `.artifacts/onboarding-standard-results/onboarding-application-standard.trx`. Logs are `.artifacts/onboarding-standard-domain-tests.log` and `.artifacts/onboarding-standard-application-tests.log`.

Builds used `--no-restore --artifacts-path .artifacts/onboarding-build -p:BuildProjectReferences=false -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1`, with `-p:CompilerResponseFile=C:\Repo\2026\Shikshamitra-BE\.artifacts\onboarding-compiler-serial.rsp`; that response file contains `/parallel-`. Process-only `DOTNET_GCHeapHardLimit=0xC0000000` and `DOTNET_GCConserveMemory=7` bounded the verification process. No nullable or analyzer override was used in these final checks.

Current Domain.Shared, Domain, Application.Contracts, Application, Domain.Tests, Application.Tests, HttpApi and Host compiled with normal repository options. Production module results were respectively 7, 269, 314, 141, 3 and 1 warnings, all with zero errors. The final HttpApi and Host builds took 11.48 and 8.71 seconds. The existing EF artifact was deliberately reused after confirming its source was unchanged and its successful binary postdated that source; dependencies were built in order and rechecked against fresh shared output. This is current changed-module compilation and focused test evidence, not a claim that every repository project was rebuilt or every legacy test ran. Earlier full-reference attempts exhausted local memory in unchanged EF compilation; diagnostic builds with reduced compiler checks are superseded by the normal changed-module checks above.

The latest task-owned API at `http://localhost:5001` started from staged current normal-compiled modules while preserving the verified unchanged EF binary. Health returned 200; CORS preflight returned 204 for `http://localhost:3001`. Background jobs, background workers, startup migrations and test-login seeding were disabled only for that process. The existing port 5000 host was untouched. Actual synthetic Admissions-to-Onboarding-to-Records browser evidence is recorded in [frontend testing](../../../../Shikshamitra-FE/docs/student/testing.md).

No Finance source/API or EF schema change is required. GSEB school-level uniqueness and promotion decisions remain backlog. QA credential emails were disabled; SMTP acceptance, inbox arrival, staging and production deployment are not certified by these local checks.

## Optional Intake setup and manual public links (2026-10-10)

Local verification passed 36 focused application cases across the initial successful cases and repaired fixture reruns, plus three `AdmissionIntakeProfileRules_Tests` domain cases. Coverage includes no-profile Offering create/activate/readiness, clearing setup and stale-marker readiness, manual-link persistence/options/anonymous form loading, duplicate submission idempotency, null-profile capture against both configured and unconfigured Offerings, suppression of historical workflow defaults, active-role/scope manual assignment, Unassigned queues, configured pool/capacity, setup usage/archive, and public-link compatibility/expiry/owner/fallback errors. Fixtures use currently supported Form and Admission decision definitions; historical policies are persisted only when explicitly testing legacy runtime behavior. Direct concrete-service calls run inside an explicit test unit of work.

The local DbMigrator and isolated HttpApi Host compiled with zero errors using serialized builds and disabled analyzer execution; existing compiler warnings remain. EF reports no pending model changes. `20261010034554_MakePublicInquiryAssignmentOptional` changes only `AppAdmissionPublicInquiryLinks.CounsellorAssignmentProfileId` to nullable. Generated Up/Down scripts were reviewed; Down fails before altering rows when manual links exist instead of assigning fabricated profile IDs. The generated Up script was applied only to the verified localhost `postgres` database at baseline `20261007120000_RemoveChargeBankRouting`; the column is nullable and the new migration marker is present.

Evidence logs are `.artifacts/optional-intake-be-tests-final.log` (initial successful cases), `.artifacts/optional-intake-be-repaired.log` (fixture repairs), `.artifacts/optional-intake-domain-tests.log`, `.artifacts/optional-intake-host-build.log`, and generated `.artifacts/optional-public-link-up.sql` / `optional-public-link-down.sql`. Relevant application cases that initially stopped on retired workflow fixtures or missing test units of work were rerun after test-only repairs. The additional inactive/unscoped routing guard passed in its separate rerun, recorded in `.artifacts/optional-intake-inactive-guard.log`.

Actual requested localhost inquiry-link creation remains pending the user's restart of the latest API build; frontend draft and UI proof are recorded in [frontend testing](../../../../Shikshamitra-FE/docs/student/testing.md). The existing API still uses the earlier process build. No staging migration, deployment, commit or seed change was performed.

## Finalized admission history display (2026-10-10)

The focused decision contract and history actor-projection run passed 17 tests. Known actors resolve to their saved display name; unknown actors remain in the history with a null name. Existing Applications View authorization and scoped application lookup remain in front of the history read. Contracts, Application, EntityFrameworkCore and HttpApi Host compiled with zero errors using serialized incremental builds; existing warnings remain. The first unrestricted build exhausted available disk space and the first reference-limited test build found an unrelated stale EF migration assembly, so EF was rebuilt before the successful test run. No persistence or migration change belongs to this display-field change. Local API/browser verification confirmed the actual recorded finalizing staff name. Evidence logs are `.artifacts/finalized-summary-tests.log`, `.artifacts/finalized-summary-contract-build.log`, `.artifacts/finalized-summary-app-build.log`, `.artifacts/finalized-summary-ef-build.log` and `.artifacts/finalized-summary-host-build.log`.

## Intake setup restore (2026-10-11)

The focused `Intake_Setup` application filter passed four tests, including restore/rearchive, current-stamp idempotency, stale-stamp rejection, cross-tenant rejection, preserved profile references and inactive historical Offering/link state. `AdmissionIntakeSetupApiContract_Tests` passed five tests and retains Admission Offerings Add authority on the Restore controller and application service. The isolated HttpApi Host build succeeded without a schema change. These tests establish source/integration behavior; the existing localhost host needs a rebuild/restart before browser restore persistence can be accepted.

## Shared school Assessment sessions (2026-10-14)

Local verification passed 61 Domain tests, six shared-session Application scenarios across the initial run and focused repairs, 17 HTTP permission contract tests and one EF mapping test. Shared-session coverage includes a combined two-Offering roster, proposals extending beyond the visible page, stable preview fingerprints across pages and unselected application edits, atomic confirmation and idempotent retries, retained stage/policy identity, cancellation plus room release, metadata-only approval preservation, legacy session room adoption and school-local time conversion. Individual interview coverage proves eight sequential slots in a two-seat room, explicit-slot reservation before automatic allocation, appointment persistence and invalid-capacity rejection. These overlapping runs are not summed into a full-suite claim.

Actual PostgreSQL validation passed the combined roster case at 100, 1,000 and 5,000 candidates with 14 SQL commands at each size, plus two resource-lock/reservation cases. Local warm roster durations were approximately 49, 256 and 408 ms. Locks require a transaction, serialize the same tenant until commit and isolate other tenants. Interview reservation checks preserve teacher/day/live-status filtering, adjacent-window acceptance and ignore-self behavior. SQLite cannot translate its TimeSpan overlap predicate; the portable guard server-filters the employee/date/live reservations, projects their windows and evaluates only that bounded set. The equivalent Npgsql predicate and production portable guard were both verified.

The existing Assessment Application regression filter passed 17 active scenarios; two unrelated Eligibility/Review fixtures still fail on retired workflow definitions before Assessment execution. Those fixture failures remain unchanged. Current EF, Application, HttpApi and Host builds passed with zero errors; existing compiler warnings remain. Earlier memory-exhausted attempts are superseded by serialized successful builds. Evidence is under `D:/CodexArtifacts/`: `assessment-v2-domain-tests.log`, `assessment-v2-planning-tests.log`, `assessment-v2-planning-repaired-success.log`, `assessment-v2-interview-verified.log`, `assessment-v2-postgres-roster.log`, `assessment-v2-postgres-lock-and-room.log`, `assessment-v2-http-contract-tests.log` and `assessment-v2-model-tests.log`; TRX reports are in `assessment-v2-test-results/`.

Migration `20261014150323_AddSharedAdmissionAssessmentSessions` is additive to the existing Finance baseline. The final source retains the unfiltered legacy SessionId index alongside the filtered unique live interview-slot index. EF reports no pending model changes. A separate disposable PostgreSQL migration database reconstructed the pre-Assessment baseline with generated Down, then applied the current generated Up: two legacy sessions and two attempts retained their identities, scope, booking counts and policy checksums; Finance schema and migration history remained unchanged. The existing Finance migration files were preserved. The original database already had an externally applied Assessment marker with an older index shape, so that marker alone is not proof of the corrected migration. Evidence: `assessment-v2-pending-model.log`, `assessment-v2-up.sql`, `assessment-v2-down.sql`, `assessment-v2-migration-qa.log` and `assessment-v2-snapshot-final-delta.diff`.

Bounds are explicit: at most 20 Offering/stage links, 200 items per page/confirmation batch and 200 automatic proposals. New arrivals require a new explicit preview/confirmation. Applicants are projected together before global queue classification/counting/paging; policy and route evaluation materializes candidate summaries, so PostgreSQL scale/latency measurements remain a release check rather than an asserted production SLA.

Assessment writes check current timetable occupancy and share transaction resource locks with generic Room Reservation writers. Later Timetable save/publication has no common reservation guard and can introduce a reverse conflict. This integration boundary needs a separate common-resource decision; current source does not prove universal reverse Timetable conflict prevention.

Runtime QA uses a logical PostgreSQL snapshot restored into a random disposable `assessment_qa_*` database. It never downgrades or migrates the original database, retains original identities/scopes and disables seeders, startup migrations, background jobs/workers and test-login seeding. Tenant connection strings must be absent or rewritten in the clone before starting a host. No shared Redis integration was found; the host uses its process-local cache. Outbound worker delivery and SMTP acceptance are excluded from this QA scope.

The fresh isolated QA API at `http://localhost:5001` binds loopback only; health returned 200 Healthy, the anonymous protected resources route challenged sign-in, and CORS returned 204 with the exact `http://localhost:3001` origin and credentials enabled. The QA process supplies the frontend origin because Development configuration overrides the base CORS list. This changes neither source authentication/cookie policy nor the original user Host/database. The clone initially contained compatible Offerings, rooms and active linked staff but no current active Assessment applications. Authenticated normal UI intake subsequently created two synthetic Class 1/Class 2 applications; a shared School exam preview/confirmation, reload, explicit partial attendance, bulk results and explicit bulk Continue persisted successfully. The downstream application showed Admission form and read-only completed Assessment evidence. No grants, original records, staging/production deployment or outbound delivery were changed or established. The first cold intake handoff took 45.7 seconds and the second returned 200 in 1.87 seconds; the cause of the cold delay is unconfirmed and these timings are not a production SLA. Runtime evidence: `assessment-v2-qa-host.log` and `assessment-v2-clone-runtime-observation.log`; detailed frontend acceptance belongs to [frontend testing](../../../../Shikshamitra-FE/docs/student/testing.md).

The temporary QA Host was stopped through its owned foreground session after browser acceptance; port 5001 was confirmed closed. The original user Host (PID 19344, port 44332), cloned evidence databases and all artifacts were preserved.

## Portal owner-email guidance (2026-10-12)

The two Student/Guardian portal workflow scenarios passed after asserting case-detail saved-owner values, missing Mother email, mismatch rejection, Father-versus-Other-guardian separation, successful correct-owner provisioning, idempotency, tenant/user scope and unchanged owner password setup. The expanded guardian scenario verifies the case-only email correction, required reason, stale stamp and cross-tenant rejection, preservation of all other snapshot values and school settings, one audit event on idempotent retry, and no email request/account creation during correction. Pending credential delivery keeps an enabled case incomplete after account provisioning.

Contracts, Application, HttpApi and Host compiled with zero errors using incremental serialized builds; existing warnings remain. Local Host was rebuilt/restarted against the verified localhost Development database without seed-grant changes. The affected case's parent rule correction persisted, while Father email is submitted to Records and awaits its existing one-reviewer approval. No real portal account or credential email was created/sent. Sender acceptance, inbox receipt, staging and deployment remain unverified. No schema migration or dispatcher/password logic changes belong to this fix. Evidence: `.artifacts/guardian-email-correction-be-tests.log`, the `.artifacts/guardian-email-correction-*-build.log` files and frontend screenshots under `.artifacts/guardian-email-ui/`.


## Assessment display progress (2026-10-15)

`AdmissionAssessmentProgress_Tests` and the focused roster lifecycle/scoping integration methods passed (7 tests). Coverage includes start/end boundaries, ended sessions awaiting results, zero marks, qualitative outcomes, waiver, absence/retake, individual-slot precedence, status filtering/counts before pagination and explicit workflow continuation. The bounded test build used freshly compiled Application/Contracts outputs and existing unchanged dependency artifacts; it is not a full solution rebuild. No migration is introduced by the display contract. Student docs validation passes; the broad validator reports unrelated missing mobile-project links.

The additional PostgreSQL scale run initially failed because the isolated fixture did not explicitly map Standard's get-only `TenantId`. The fixture now includes that production-equivalent property mapping. Its rebuild/rerun was stopped under workstation memory pressure, so the corrected scale test remains an open verification gate; earlier scale results are not evidence for this revision.

## Archived workflow re-open checks (2026-10-16)

`AdmissionWorkflowDefinition_Tests` checks immutable same-version restoration, invalid transitions and prior/sibling publication status.
`Reopen_` Application scenarios check current-stamp restoration, stale rejection, tenant isolation, a single audit entry and preservation of a newer Published version.
`AdmissionWorkflowApiContract_Tests` checks the Re-open route, Publish permission and query stamp binding.

`AdmissionWorkflowPublicationLockPostgres_Tests` uses the existing `ASSESSMENT_ROSTER_TEST_POSTGRES` disposable loopback test connection convention.
It verifies same-family blocking, different tenant/code independence, transaction requirement and release at commit; without that environment variable it is explicitly skipped.
SQLite Application tests do not establish production-provider concurrency.

`dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Reopen_|FullyQualifiedName~AdmissionWorkflowPublicationLockPostgres_Tests"`

Local verification passed 12 Domain tests, all three Reopen Application scenarios and the PostgreSQL publication-lock test (4/4, no skips). The provider test created and removed its own guarded `admissions_assessment_tests_*` database on loopback; it did not run against the application database. This proves the locking mechanism, not an end-to-end simultaneous Publish/Reopen application-command test on PostgreSQL.

The six workflow HTTP contract tests passed with no skips using isolated test output. The ordinary HTTP test build was blocked by five unrelated `CS0121` errors in the concurrently edited `StudentRollNumberBatchContract_Tests.cs`; an ignored validation-only target excluded that one test source without changing tracked project files or normal test outputs. These checks verify controller/service metadata and direct controller behavior, not deployed authentication.

HttpApi and Host leaf builds passed. Because concurrent builds exhausted workstation memory, the successful builds disabled analyzers/shared compilation and skipped dependency compilation only after checking that every workflow dependency DLL was newer than its feature source and contained the required methods/types. SHA256 comparison confirmed Host copied those exact normal dependency outputs. This is focused feature validation, not a full solution/analyzer gate. Student documentation validation passed; no schema migration was added.

## Inquiry age capture context verification (21 October 2026)

The two Institute Setup minimum-age context cases (configured 5 years and unconfigured 0) and the existing closed-offering capture-context regression pass: 3/3 application integration tests. They verify that the age cutoff comes from the cycle's Academic Year, including an academic start different from the admission-period start. The isolated API Host build passes with no errors; existing unrelated warnings remain. No migration or inquiry-blocking age rule was introduced.

Local browser verification used this build to create and edit synthetic INQ-2026-000009. Existing class minimum 6 years and academic start 1 June 2026 were displayed, underage saves succeeded, DOB persisted after reload, and an exact-minimum DOB removed the advisory. Acknowledgement delivery was disabled. This does not establish stage/live deployment.

## Form section verification retirement (23 October 2026)

- Focused Domain coverage passes 15 tests for Form normalization, preserved policy fields and raw historical compatibility. The isolated Application run passes 5 tests for legacy manual continuation, correction/history, automatic submission and document handoff. The existing legacy Inquiry-to-Form handoff also passes separately, for 6 integration cases total.
- Stored published JSON and verification history are preserved. Effective Form policy ignores retired section-verification requirements; new workflow saves clear those flags. Required-field, submission-limit, deadline, correction authorization and application concurrency gates remain.
- The wider Domain batch has one unchanged Assessment-version expectation failure: `Unknown_policy_version_is_rejected_instead_of_silently_downgraded` expects version 2 rejection although the current Assessment policy accepts versions 1 and 2. Focused Form cases pass independently.
- Evidence: `.artifacts/form-stage-domain-focused-test.log`, `.artifacts/form-stage-application-test.log`, `.artifacts/form-stage-legacy-handoff-test.log`. Student documentation validation passes. No migration, API restart, deployment or live financial operation was performed for this change.

## Current-page identifier assignment (23 October 2026)

- The focused Application run passes 29/29 cases: legacy Roll batch ordering/approval/concurrency plus bounded current-page targeting, preservation of other-page and assigned students, rejected wrong-division targets, policy metadata and configured approval, database-admin Unlock with spoofed admin claims rejected, staff Lock and 17 canonical action-denial checks.
- The additional pending-case rerun passes after extending the stale/off-page/tenant scenario with a fresh one-click assignment, no unnecessary reason/approval, positive number 13 following reserved number 12, and unchanged off-page student. An initial fixture failure was fixed by reusing the canonical default-checklist/snapshot seeding helper, not by weakening production readiness.
- The Roll batch HTTP contract suite passes 11/11 cases, including optional target-list bounds, duplicate/empty IDs and rejection of client-targeted Renumber. Existing endpoint authorization, stamps and stable submission keys remain covered.
- Isolated serialized Host build passes with zero errors; existing warnings remain. Student docs validation and `git diff --check` pass. Evidence: `.artifacts/current-page-roll/results/current-page-roll-final.trx` and `current-page-roll-direct.trx` in that directory. These local SQLite and contract checks do not establish production-provider race coverage, deployed behavior or real account/email delivery. No migration or Finance module change was introduced.

## Fixed Payment checkpoint verification (28 October 2026)

- AdmissionPaymentStageConfiguration_Tests: 9 passing cases for removal of retired payment/paymentCompletion data, preservation of extensions, empty normalization and rejection of malformed stage-root JSON.
- The focused Application run passes 28 cases, including 14 readiness unit cases, four new Payment_checkpoint_ integration cases and existing fee-selection scope/concurrency, deferral validation/replay/finalization and Onboarding regressions. Previously pinned Full/off JSON no longer blocks a confirmed 300/1000 payment or a reason-based deferral. Finance-verified zero payable advances without creating payment evidence. Inactive stages, unprepared balances, unverified/historical evidence and missing permissions remain guarded.
- Changed Domain.Shared, Contracts and Application plus test leaf builds pass with the pinned .NET SDK and analyzers disabled, using isolated outputs. The unchanged prebuilt Domain/EF/TestBase dependencies are reused; EF source is older than the reused DLL. This is focused validation, not a fresh solution/analyzer build or production-provider sign-off.
- No migration, live payment or Finance module change is involved. Existing stored workflow JSON/history is retained; only draft normalization strips retired configuration. The existing paymentCompletion API envelope reports operational readiness capabilities, not configurable rules. Rebuild/restart the debugger Host to serve the new implementation locally.
## School Exit validation

`StudentSchoolExitOutstanding_Tests` exercises the exact reviewable-owner whitelist, mandatory recorded approval evidence and stale evidence after increased debt/new books. `StudentSchoolExitIntegration_Tests` adds direct-policy pending-book approval enforcement, requester separation, unchanged library/roster state, missing Finance evidence and tenant isolation. Run alongside existing Records transition/proration regressions:

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~StudentSchoolExit|FullyQualifiedName~Record_transition|FullyQualifiedName~WithdrawalProration" -p:UseSharedCompilation=false
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
```

Focused tests do not establish browser, deployed permission, worker concurrency on PostgreSQL, or production acceptance.
## Resume admission acceptance (29 October 2026)

The focused `AdmissionJourneyResumeIntegration_Tests` run passes 16 cases. It covers immutable closure evidence, exact runtime restoration at Form/Assessment/Documents/Payment/Approval, same application number and form snapshot, repeated withdrawal/replay protection, tenant/stamp/closure guards, conservative legacy recovery, finalization-boundary restoration, slow closure cleanup and seat-capacity rollback/reacquisition. The Finance integration retains the same account/due/receipt with 400 paid and 600 outstanding; Resume does not collect or reprice money.

`AdmissionJourneyApiContract_Tests` plus `Admission_resume_postgres` pass eight checks using a fresh disposable loopback `records_tests_resume_*` database. They cover authenticated View/Decide separation, anonymous denial, simultaneous commands producing one success and one safe conflict, idempotent replay and a concurrent Start resolving the existing application. This establishes provider concurrency behavior, not production migration or deployed acceptance. Test evidence is under `.artifacts/admission-resume/results/`.

Local signed-in browser acceptance used synthetic Class 1 / CBSE / English application APP-002 (`3a2499dd-6b7a-ba04-baaf-e4fa16f58dc1`). Desktop 1440 x 1000 and mobile viewport 390 x 844 each completed End Journey -> Resume -> the same Entrance assessment stage (`3a2499dd-6b94-37b3-11be-6a70a6a4bddb`). Two End and two Resume events remain in history. Acknowledgement was disabled; no real student, payment, portal account or credential send was used. This is localhost acceptance, not staging or physical-device acceptance.

The focused Host publish succeeds with isolated outputs and analyzers disabled; existing warnings remain. No schema migration is introduced. Dedicated refunded/full-paid, waitlist/expired-seat, expired-offer and Records-return browser scenarios were not executed; their existing owner rules remain authoritative. Local success does not establish those scenario-specific or production outcomes.

## Inquiry inheritance regression checks

Run Domain `AdmissionFormInheritanceComposer_Tests`, Application `AdmissionFormInheritance_Tests`, `AdmissionEffectiveFormStore_Tests`, `Inherited_registration_*`, `StudentRecordInheritedIdentity_Tests` and the retained-schema Records regression. HTTP `AdmissionFormInheritanceApiContract_Tests` checks endpoint contracts and existing authorization grants. Include existing form validation, prefill, application-start concurrency, correction, family ownership and Records suites when reviewing the integrated change.

Required cases include source-first sections/steps and columns, source-specific labels/options/translations, semantic-identity and reserved-starter deduplication, unrelated custom-code collisions, locked source requirements/validation/placement, effective conditional rules, repeatable groups, unsupported fields, readonly full-name/context references, corrupt snapshot rejection, save/clear persistence, start retry and retained submission/Records evidence. Full-name references must not retain a conditional required rule that could create an unanswered disabled control. Application tests verify persistence through their configured provider; they do not establish PostgreSQL production concurrency or browser behavior. Run the full Release solution build with the pinned SDK and isolated artifacts before handoff.

Local verification on 30 October 2026 passed the full Release solution build with the pinned .NET 9 SDK, isolated artifacts and analyzers enabled: zero errors, 898 warnings. Fresh post-build selections passed 20 Domain composition tests, 49 Application inheritance/runtime/identity tests, one retained-schema Records integration test and four HTTP contract tests, with no skips. The earlier separate 58-case existing validation/prefill/contact regression selection also passed. The runtime integration selection verifies exact source labels, prefill, explicit clears, required effective fields, unknown-write denial, start idempotency and immutable schema persistence; the Records case retains inherited questions and submitted answers. These provider tests do not establish production PostgreSQL concurrency.

## Inquiry identity mapping recovery

Publication hardening tests reject alias-only/custom mappings, custom aliases shadowing system identity, incorrect input types and incomplete selected-parent contacts behind a generic pair. An actual `PublishAsync` call rejects an alias-only student mapping and preserves the draft, field count and binding key. Existing published forms are exercised through real create/edit services with `FIELD_1` explicitly bound to Student name and historical code aliases; raw answers and field counts remain stable. No runtime identity questions or tenant form migration are introduced.

The final focused selection passes 59 cases with no skips. The selector cases failed before the runtime/publication ownership rules were aligned. The isolated test compilation excludes EF migration designer/snapshot files and disables analyzers; it does not execute migrations or establish deployed PostgreSQL acceptance.

The separate normal Application project build with analyzers enabled passes with zero errors and 796 warnings. It uses no focused-test compile exclusion and does not claim a full solution/migration build.

The focused `AdmissionInquiryConfiguredFields_Tests` and configured/partial Inquiry runtime selection passes 46 cases with no skips. Coverage includes partial Father/Mother mobile ownership, legacy generic DTO compatibility, optional published identity, same-Offering edit preservation, verified sibling name recovery, forged-role denial, public attribution and publication minimums. Unmapped custom questions are not relabelled or reinterpreted as identity.

The isolated focused test build uses the pinned SDK and excludes unchanged EF migration designers/model snapshot from that test dependency compilation under local memory pressure; analyzers are disabled for that test run. It is not a full migration/solution build or deployed-tenant acceptance. No schema migration or tenant form repair is part of this change.

The separate normal Application project build, without the focused-test exclusion and with analyzers enabled, passes with zero errors and 796 warnings. This does not claim a full solution/migration build.

The verified localhost API uses the same Release Application assembly as the post-build tests and the existing loopback launch configuration; migrations, seeders and delivery workers are disabled. Authenticated browser checks verify source discovery/preview and an isolated Registration draft's two approvals and requiredness surviving full reload. No active Offering or applicant journey was changed; see [frontend browser evidence](../../../../Shikshamitra-FE/docs/student/testing.md#inquiry-inheritance-checks). Student docs validate under PowerShell 7; Windows PowerShell 5.1 misreads the existing BOM-less UTF-8 pages and reports false mojibake. Local checks do not establish staging or production acceptance.

### Portal profile photo regression gates

`MobileStudentPhoto_Tests` covers parent/self submissions, exact retry and stale snapshot behavior, live View/Add denial, revoked association access, school approval and application conflict retaining the previous photo, and upload type/size boundaries. `MobileStudentPhotoApiContract_Tests` covers the five-route permission contract, preservation of the profile read, an oversized stream with a false reported length, and exact-limit HTTP delegation.

Run pinned .NET 9 focused checks:

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Portal_photo|FullyQualifiedName~Mobile_profile|FullyQualifiedName~Record_photo"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~MobileStudentPhotoApiContract_Tests|FullyQualifiedName~MobileStudentProfileApiContract_Tests"
```

Application fixtures use SQLite with transactions disabled. StudentPhotoConcurrencyPostgres_Tests separately exercises the real EF row fence against an explicitly configured disposable loopback photo_tests_* PostgreSQL database, including ReadCommitted contention and an admission-style prior Serializable snapshot. Set STUDENT_PHOTO_POSTGRES only to that isolated test database. Image validation requires SkiaSharp SKCodec.GetPixels to return Success after PNG chunk/CRC and JPEG complete-scan framing checks, with a 4096-by-4096 pixel allocation bound. SkiaSharp and its Linux NoDependencies native assets are pinned to 4.153.1; Windows assets arrive through the main package. Tests include valid JPEG/PNG, CRC-repaired corrupt PNG pixels, an empty JPEG scan, and nonzero truncated JPEG input. Local tests and source checks do not establish deployed permission/association revocation or live deployment acceptance.
## Inherited Inquiry presentation (30 October 2026)

The focused Domain composer suite passes 25 tests, including all four Inquiry/Registration layout combinations, preserved multi-section source steps and source columns, immutable input definitions, and dormant single-page step rules on shared questions. The inheritance/effective-form Application selection passes 45 tests, including output DTO serialization of source columns for frozen runtime schemas. Existing snapshots are not migrated. Browser component QA does not establish actual applicant persistence or deployment acceptance.

### Inquiry-first locked definitions

The current composer selection passes 33 cases and the Application composition selection passes 16 cases. They cover recommended-template overlaps with reserved legacy aliases, original source provenance, source-first sections/steps/grid, locked source requirements/validation/placement, read-only full-name reference beside structured Registration names, original Inquiry immutability, incompatible types and rules, source switching, and JSON roundtrip of the saved Registration policy. Originally empty authored Registration sections/steps remain available; only sections emptied by duplicate suppression disappear. Matching does not use labels or translations.

These gates used the pinned SDK and isolated `inquiry-first-runtime` artifacts. The final Domain test build used `BuildProjectReferences=false`, `RunAnalyzers=false` and `-m:1` against already compiled dependencies; Application composition tests ran through `dotnet vstest` against the settled test assembly. The focused dependency compilation excluded unchanged EF migration designers/model snapshot under memory pressure; these gates are not a full migration/solution build or live browser acceptance. Original frozen application schemas remain unchanged. See the frontend testing page for the separate actual save/reopen check.
