---
module: student
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-09-29
owner: backend-team
audience: humans-and-agents
---

# Validate Student Enrollment Admissions changes

TL;DR: run domain, application contract, EF mapping, and HTTP contract tests for Admission Offering changes, then validate module docs and migration safety.

For onboarding service consent, run `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~Onboarding_service_consent_requires_an_audited_confirmation_without_bypassing_first_payment` and `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~StudentOnboardingApiContract_Tests`. The application scenario covers invalid and stale confirmation, an audited consent handoff, the still-required confirmed first payment, idempotent retry, and one enrollment; the HTTP contract checks route and permission. These do not replace live signed-record review or an authorized school-data walkthrough.

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
| Running host | PID 30256, parent 21436, started 07:38 IST; serves port 44332 from `src/ShikshaMitra.HttpApi.Host/bin/Debug/net9.0/ShikshaMitra.HttpApi.Host.dll` |
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
