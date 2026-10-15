---
module: student
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-10-14
owner: backend-team
audience: humans-and-agents
---

# Validate Student Enrollment Admissions changes

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

The 2026-10-13 Student Setup worklist gate passed **5/5 Application tests** and **1/1 PostgreSQL HTTP test**, with zero failures or skips. Reports are `test/ShikshaMitra.Application.Tests/TestResults/rajba_RAJ-LAPTOP_2026-10-13_13_36_48.trx` and `test/ShikshaMitra.HttpApi.Tests/TestResults/rajba_RAJ-LAPTOP_2026-10-13_13_41_01.trx`.
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

`StudentAttendanceApi_Tests.Workspace.cs` covers Directory population parity, server paging/search (including numeric roll), attendance-only read permission, disabled dated eligibility, exact active teacher assignment despite broader scope/forged administrator claims, assignment revocation, admin-only Lock, year/tenant denial and full-roster review before Lock. The attendance test fixture creates real configured database administrator membership for administrator scenarios and restores it after each test; claims and generic staff scope are not administrator authority.

Run the API project with `FullyQualifiedName~StudentAttendanceApi_Tests|FullyQualifiedName~StudentDirectoryApiContract_Tests` and the Application project with `FullyQualifiedName~.Directory_|FullyQualifiedName~.Legacy_override_` using isolated artifacts. Directory scenarios are methods on the partial `AdmissionWorkflowRuntimeIntegration_Tests` class; the source filename is not a test-class filter. The Application regression gate protects the extracted shared Directory inclusion, hierarchy and identity-masking rules. Source tests and a test host do not prove staging or production acceptance; record actual local test and browser results separately.

The current isolated API gate passed **29/29 tests**, zero failures/skips, on 2026-10-12 (`.artifacts/attendance-workspace/results/attendance-workspace-api-r3.trx`). Shared Directory Application regression selected nine scenarios: six passed and three existing provisioning-mode assertions failed (`student-directory-regression-r2.trx`). Their unchanged fixture creates a persisted linked enrollment then expects Directory exclusion; the unchanged HEAD eligibility predicate includes that activated onboarding. The extracted default population predicate was compared with HEAD and is identical apart from whitespace. These unrelated lifecycle assertions remain unchanged; this is not a green Application-suite claim. Authenticated local browser proof is a separate gate; staging/production acceptance is not asserted.

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
| UI | [Frontend Student checks](../../../../Shikshamitra-FE/docs/student/testing.md) own focused tests, lint/typecheck and authenticated browser walkthrough. Source checks do not replace browser evidence. |

Run focused checks from the backend root:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests --filter FullyQualifiedName~Students.Attendance
dotnet test test/ShikshaMitra.HttpApi.Tests --filter 'FullyQualifiedName~StudentAttendanceApi_Tests|FullyQualifiedName~AttendanceSettingsAuthorizationApi_Tests|FullyQualifiedName~StudentLeaveApi_Tests'
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
