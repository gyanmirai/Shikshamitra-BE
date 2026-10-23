---
module: hr
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-10-23
owner: backend-team
audience: humans-and-agents
---

## ESI enrollment response authorization (2026-10-16)

The focused HTTP permission/ESI selection passes 48/48 cases with zero skips, including `Esi_employee_actions_do_not_require_employee_profile_editing`. An authorized ManageEnrollment update now uses an internal enrollment reader for its response after retaining the action and employee-scope checks. Public GET still requires View; a View-only caller cannot update, and override approval remains separate. No permission grants or payroll calculation rules change. Evidence: `.artifacts/student-pending-repair/results/student-lifecycle-security-http-current-domain.trx`.

The deployment caller retains main-only execution, depends on the security regression workflow, verifies .NET 9, and validates startup/publish-profile gates before DbMigrator. Its explicit FullDemo profile preserves the existing default and grant seeding guards. `SecurityReleaseWorkflow_Tests` passes 1/1 locally; this does not certify an executed deployment.

## Staff terminology and attendance header regression coverage

Run `AttendanceImportHeaderAliases_Tests` and `HrStaffOutputLabels_Tests` in the Application test project.
All 18 tests passed on 2026-10-05.
They cover generated Staff headers, template round trips, existing Employee headers, mixed aliases across fields, casing and spacing, duplicate aliases in CSV and XLSX, and report display labels with unchanged field identifiers.
No payroll calculation, stored report definition, permission key, or database identifier is renamed.

The focused policy and security Domain run passed all 53 tests.
The HTTP API `PermissionConsistencyApi` run passed all 13 tests.
The broader Application run covering attendance imports, policy services, reports, I-card templates, and permission reads passed 165 tests and failed 42.
A clean checkout of `main` at `0e05f0cb` reproduced the same 42 failing test cases, with no feature-only failures.
Do not treat that baseline comparison as full payroll workflow acceptance.
Final authenticated browser acceptance must use a running backend with valid local database configuration.

# Validate HR backend changes

PF eligibility and ESIC basis/rate coverage is in `StatutoryCalculationSettings_Tests` and `StatutoryCalculationApi_Tests`.
The ESIC cases cover all nine independent basis combinations, full-month eligibility, payable contributions, daily-wage exemption, inactive Basic components, continued coverage, and unchanged older configuration JSON.
`IndependentEsiBasisPayroll_Tests` uses a fresh loopback `payroll_tests_*` PostgreSQL database through `PAYROLL_TEST_POSTGRES` to verify both mixed Gross/Basic choices through salary setup and actual payroll, with locked results preserved.
Run `SalaryTemplateCalculator_Tests` to verify Gross/CTC reconciliation across statutory rounding boundaries.
`StatutoryCalculationPostgres_Tests` requires a separate empty loopback `payroll_tests_*` database through `PAYROLL_TEST_POSTGRES`; it never resets an existing database.
The `PayrollStatutoryCalculationSettings` migration adds PF coverage history and calculation evidence columns without replacing historical payroll or removing unrelated tables.

TL;DR: run the smallest command that proves the changed HR behavior, then run docs validation before handoff.
Use broader API tests when a change touches contracts, permissions, payroll, attendance, leave, or policy resolution.

Salary-template coverage must include complete active creation, Needs setup recovery, arbitrary active tenant components, exactly one Basic, zero or one balancing earning, exact rounded gross reconciliation, dependency cycles, cross-tenant component rejection, permission boundaries, concurrency conflicts, idempotent retries, PF defaults, percentage validation, largest fractional remainder allocation and existing statutory rounding, PF ceilings, Gujarat professional-tax boundaries, and Monthly salary and Monthly cost to school (CTC) solving across discontinuities.
Compensation coverage must prove direct effective saves, effective-month resolution, immutable history, same-month supersession, scheduled future revisions, idempotent retries, controlled overrides, activation readiness, locked-payroll protection, separate one-time lines, and blocking protected-target drift.
Monthly payroll coverage must prove that PF uses prorated eligible wages, professional tax uses actual monthly Gross without attendance-prorating the slab amount, cessation-month treatment follows the effective professional-tax profile, ESI uses effective coverage and wages, eligible PwD relief is government funded, employer contributions remain outside net pay, unlocked previews refresh, and locked snapshots remain immutable.
Salary-summary API regressions compare preview and saved totals with overlapping professional-tax rules and with a cessation-month exemption.

## Required docs checks

Run these from the `Shikshamitra-BE` repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module hr
git diff --check
```

Run this from `../../../../Shikshamitra-FE` when frontend HR docs or integration docs changed:

```bash
npm run docs:hr:check
```

## Backend API test commands

Use these commands for backend HR API validation:

```bash
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier=Critical"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier!=Smoke"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier=Smoke"
```

Smoke tests require a running host and `APITEST_ENABLE_SMOKE=true`.

### Durable payroll PostgreSQL tests

Use two separate, empty PostgreSQL databases on loopback, each with a name beginning `payroll_tests_`.
Supply their connection strings securely through `PAYROLL_TEST_POSTGRES` and `PAYROLL_WORKFLOW_TEST_POSTGRES`.
The test factories create tables and FullDemo fixtures but never drop, reset, or truncate an existing database.
An already initialized database causes setup to fail, so use fresh disposable databases for each invocation.
These model-created databases test PostgreSQL behavior, not migration upgrade paths.

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests --filter 'FullyQualifiedName~PayrollPreviewPostgres_Tests|FullyQualifiedName~RunPayrollApi_Tests'
```

`PayrollPreviewPostgres_Tests` covers ten-request deduplication, generation-wide summaries, attendance edits during calculation, stale review and lock rejection, owner-checked lease release and renewal, database-error propagation, abandoned attempts, and bounded retries.
Its performance fixture expands synthetic seeded compensation to 50 and 1,000 scoped employees, supplies attendance, executes the real worker, and measures 20 requests per endpoint for p95.
Require 50 employees within 15 seconds, 1,000 within 60 seconds, and start/status/page p95 below two seconds.
Local timings are diagnostic evidence; repeat these gates on the documented staging infrastructure before release.

`RunPayrollApi_Tests` uses `PAYROLL_WORKFLOW_TEST_POSTGRES` and the start/status/result-page contracts, with the reviewed generation explicitly supplied for mutations.
Without the corresponding environment variable, these PostgreSQL tests are skipped, not passed.
The release runner must provide both variables and verify nonzero executed test counts.
An Application Control assembly-load failure may appear as no matching tests with a zero process exit code; treat that as blocked validation, never a pass.

Salary-slip fixtures must create effective compensation revisions before calculating permanent earnings.
ESI regression tests must distinguish agreed monthly classified wages for coverage from attendance-adjusted wages for contributions, including above-ceiling employees with unpaid days and contribution-period continuation.
`EsiContributionContinuity_Tests` covers finalized and paid evidence with missing or unlocked snapshots, draft exclusion, contribution-period boundaries, conflicting snapshots, and tenant/employee isolation.
`EsiContributionContinuityApi_Tests` verifies above-ceiling salary calculation continues both contribution shares after finalized or paid payroll and stops at the next contribution period without changing historical evidence.
`EsiContributionContinuityPostgres_Tests` runs both API scenarios on PostgreSQL when `PAYROLL_TEST_POSTGRES` points to an empty, disposable loopback `payroll_tests_*` database.

## Focused test map

Operational dashboard tests cover daily classification, count reconciliation, stable pagination, exact section grants, restricted UserScope, revoked read access, tenant isolation, request validation, and retirement of the combined Main reads.
Run `HRDashboardDailyStatusProjection_Tests`, `HRDashboardApi_Tests`, and `ProfessionalManagementApi_Tests` for these contracts.
Module API fixtures cover 120 onboarding applications and 120 appraisal assignments, current module dates independent of historical attendance, distinct overdue counts, restricted onboarding, and scoped review drill-down.
View permission without employee scope must fail through HTTP before any review or evidence is serialized.
Finish with local signed-in frontend checks against PostgreSQL after automated validation.

Use this table to pick focused tests:

| Change area | First backend tests |
|---|---|
| Employee profile, family, documents, status, or reporting | `EmployeeListApi_Tests`, `EmployeePersonalInfoApi_Tests`, `FamilyInfoApi_Tests`, `EmployeeCreationFamilyAccessApi_Tests`, `EmployeeFamilyInfoAccess_Tests`, `EmployeeDocumentApi_Tests`, `EmployeeStatusApi_Tests` |
| Employee profile dashboard | `EmployeeDashboardProjection_Tests`, `EmployeeListApi_Tests.Should_Get_Employee_Dashboard_From_Stored_HR_Data`, and the attendance-only resolver regression in `HrPolicyAppService_Tests` |
| Employee onboarding and public submission | `EmployeeApplication_Tests` for the forward-only terminal state graph and token regeneration, `EmployeeApplicationAppService_Tests` for validation, unique invitation numbers, legacy uploads, atomic scope changes, and deferred effects, `EmployeeOnboardingAcademicScopeAssigner_Tests` for the BulkAssign gate, `EmployeeApplicationUploadPolicy_Tests` for strict-new and legacy-stored MIME behavior, `EmployeePersonalInfoAppService_Tests` for explicit role approval and employee-group audiences, `RolesSeederContributor_Tests` for stable role metadata, `EfCoreSampleAppServiceTests` for identity-role metadata round trips, and `EmployeeApplicationPublicApi_Tests` for multipart validation and upload limits |
| Payroll settings | `SalaryTemplateCalculator_Tests`, `EmployeeCompensationRevision_Tests`, `SalaryTemplateComposerAppService_Tests`, `SalaryTemplateApi_Tests`, `EsiStatutoryCalculator_Tests`, `EsiStatutoryPreviewProjection_Tests`, `EsiStatutoryApi_Tests`, `EarningComponentAppService_Tests` dependency lifecycle tests, `EarningComponentDependencyConcurrency_Tests` two-transaction graph races, `EarningComponentApi_Tests`, `DeductionComponentApi_Tests`, `PfRuleApi_Tests`, `PayElementApi_Tests`, `HrPolicyApi_Tests` |
| Payroll preview, review, lock, or slips | `RunPayrollApi_Tests`, `SalarySlipApi_Tests`, `AttendanceImportLogsApi_Tests` |
| Attendance and imports | `TakeAttendanceApi_Tests`, `AttendanceTrackingApi_Tests`, `AttendanceImportLogsApi_Tests`, `AttendancePolicyEvaluationApi_Tests` |
| Leave policies and requests | `LeavePolicyApi_Tests`, `LeavePolicyAppService_Tests`, `LeaveManagementApi_Tests` |
| Calendar events and holidays | `EventApi_Tests`, `HolidayApi_Tests`, `EventTypeApi_Tests`, `HolidayTypeApi_Tests`, `CalendarViewApi_Tests` |
| Policy rules and audit | `PolicyAggregateRequiredIds_Tests`, `HrPolicyAppService_Tests`, `PolicyReportRegression_Tests` |
| Reports | `HrReportApi_Tests`, `PolicyReportRegression_Tests`, `EfCoreHrReportExportJobRepository_Tests` |

Policy regression coverage must prove semantic manual-holiday audit comparison, restore-name collisions, archived/current filtering before paging, leave-policy restore conflicts, normal-workflow archive rejection, and stable unique-constraint errors.
Report regression coverage must prove PDF row-cap metadata, inverted-date request suppression, newly queued export visibility, failed-export deletion wiring, UTC PostgreSQL timestamp boundaries, broad-owner to narrow-recipient rejection, compatible recipient access, cross-tenant rejection, owner and recipient scope revocation, schedule-owner permission revocation, invalid-first schedule isolation, legacy queued-snapshot capture, fail-closed legacy generation without an effective owner scope, retry generation fencing, and stale queued-snapshot rejection by the export worker.
Scheduled-delivery coverage must prove that delivery waits for unit-of-work completion, still runs without an ambient unit of work, and cannot fail an already committed schedule.

## Frontend integration checks

Use frontend checks when backend changes affect routes, payloads, permissions, or user flows:

```bash
npx vitest run src/redux/hr src/screens/hr src/utils/hr-audit-format.test.ts
npx playwright test e2e/hr/employee --project=chromium --reporter=list
npx playwright test e2e/hr-calendar-e2e.spec.ts --project=chromium --reporter=list
```

Run the full payroll cycle only against a disposable or seeded local environment:

```powershell
$env:E2E_HR_FULL_CYCLE='1'
npx playwright test e2e/hr/hr-module-full-cycle.spec.ts --project=chromium --reporter=list
```

## Deployment regression gate

The backend deployment workflow builds Release binaries, runs focused HR domain, application, HTTP, and migration-history regression suites, and then runs `ShikshaMitra.DbMigrator` before publishing the host.
Deployment must stop if an unknown non-empty migration history cannot be reconciled with the current baseline.
The migration bridge accepts only the validated legacy baseline and verifies the required baseline tables and folded admission fields before adding the current baseline alias.
The default leave-policy migration fails if duplicate active defaults already exist and then installs the tenant-and-academic-year uniqueness guard.

Attendance time-only values are stored as a stable zero-offset wall clock.
Payroll late-arrival grace comparisons must project persisted timestamps back to that clock before comparing shift start times.
`PayrollLateArrivalOffset_Tests` covers zero, positive, and negative reader offsets at and immediately after the grace boundary; `RunPayrollApi_Tests` verifies the persisted PostgreSQL workflow and obsolete unlocked-row cleanup.
Payroll overtime calculations use the canonical UTC wall-clock projection so the result cannot change with the host machine time zone or daylight-saving offset.
`AttendanceRegisterAdjustmentMapper_Tests` and `PayrollRunItemPreview_Tests` are mandatory deployment tests for this contract.

## Acceptance gates

Each HR change should satisfy these gates:

| Gate | Pass condition |
|---|---|
| Product behavior | [product.md](./product.md) still matches the shipped behavior |
| API behavior | [api.md](./api.md) names the changed endpoint family, permission, and partial status |
| Architecture | [architecture.md](./architecture.md) still points to the correct source ownership |
| Workflow | [workflows.md](./workflows.md) describes the end-to-end effect |
| Tests | Focused tests prove the changed behavior or the handoff names the remaining risk |
| Docs | HR docs validation passes |

## Known verification gaps

Check these gaps before claiming full coverage:

| Gap | Risk |
|---|---|
| Full-cycle payroll is gated and mutating | Continuous integration may skip payroll lock and salary slip proof |
| HR events and holidays share Calendar ownership | A route can exist without active HR menu navigation |
| TDS and professional tax are partial surfaces | Pages or domain objects can exist without shipped backend API behavior |
| Shift management permission mapping needs confirmation | Frontend constants and backend constants may not have one-to-one names |

## Built-in Gross Salary regression checks

The 2026-10-01 employee salary-basis regression verified `EMPLOYER_COST` and `GROSS` through compensation save, employee payroll update, bank-detail readback, and salary-structure update.
The API tests assert that those updates preserve compensation revision IDs, earnings, net salary, and employer cost.
Contract and domain tests reject `TAKE_HOME`, unknown bases, and incomplete template provenance.
Empty one-time additions remain valid without removing recurring compensation.
Validation passed: 49 focused API/security tests, 45 application calculation/contract tests, 24 attendance/proration tests, and the seed-backfill integration test.

For ESIC onboarding, run `CompensationRevisionApi_Tests`, `EmployeeStatusApi_Tests`, and `EsiStatutoryApi_Tests` against the disposable PostgreSQL test database.
Verify inactive enrollment updates, registration-pending activation warnings, zero applied contributions, and recalculation after supplying the insurance number.
On 2026-10-01, these focused API suites passed all 26 tests and `PermissionConsistencyApi_Tests` passed all 13 security checks.
The new missing-number activation test reproduced the failure before the guard was added.

Run `BuiltInGrossSalary_Tests`, `SalaryTemplateCalculator_Tests`, and `GrossSalaryComponentApi_Tests`.
Run `GrossEarningAllocation_Tests`, `SalaryTemplatePreviewProof_Tests`, `SalaryTemplateComposerAppService_Tests`, and `SalaryTemplateApi_Tests` for raw allocation, odd-paisa ties, fixed/mixed rules, read-only previews, proof expiry/binding, concurrency, and idempotent retries.
Build HTTP API tests with `dotnet test test/ShikshaMitra.HttpApi.Tests --artifacts-path .artifacts/payroll-api-tests --filter FullyQualifiedName~SalaryTemplateApi` while the local API server is running to avoid locked host DLLs.
Cover full, half, zero, fractional, and varying salary-basis days; fixed and variable percentage masters; all compensation bases; explicit remainder; invalid references; dependency cycles; and incomplete allocation.
Verify serialized compensation formulas are restored for payroll and copied into payslip line snapshots.
Run `SalarySlipGrossSnapshot_Tests` to verify drafts refresh when full-month amounts or formula inputs change without changing paid amounts.
Cover rounding residue with a zero remainder after proration, and reject earnings that depend on that remainder while allowing deduction references.
Verify a separate bonus leaves regular gross and gross-based Basic unchanged while increasing total earnings and net pay.
In the authenticated browser, create and reopen a Gross-based component, inherit its formula in a template, generate a salary structure, and inspect payroll and payslip summaries.

## Development and staging salary-template data

There is no schema migration or historical-template repair for the preview contract.
The trusted demo seeder retains Basic at 60 percent plus an explicit remainder and checks its earnings with the shared allocator before publishing.
API fixtures must obtain a preview proof rather than post an unvalidated active template.

For a fresh development database, create a separate PostgreSQL database owned by the application account, set `ConnectionStrings__Default` through the local secret provider, and run `dotnet run --no-launch-profile --project src/ShikshaMitra.DbMigrator` with the approved demo seeder profile.
Do not point this procedure at an existing shared database or reset unrelated school data.
Verify the seeded template through preview at whole and odd-paisa amounts, then create a disposable template through preview/save/reopen and archive it after verification.

For staging, update the backend and frontend together when deployment is separately authorized.
Keep seed completion markers and all existing grants intact; do not force-rerun blanket seeders.
Use an authorized staging administrator to list affected development templates, open each intended fixture, select a compatible sample amount/month, Calculate, and save the resulting active version.
Replace invalid disposable fixtures through the same preview/save contract and explicitly select a replacement before archiving a default template.
Only recreate employee test structures intentionally included in that staging fixture; existing employee revisions and payroll snapshots remain unchanged.
Record template/version IDs and confirm sample, employee structure, payroll preview, and payslip totals after the data update.

ESIC coverage includes default-on idempotent initialization, null school registration facts, direct saves, stale concurrency, retry keys, current/future invalidation, and preservation of earlier/locked employee snapshots.
Verify both INR 10,000 examples from the ESIC design reference and exact employer/employee reconciliation.
Exercise save/lock concurrency on PostgreSQL, plus tenant isolation and permission enforcement.

## ESIC correctness regressions

EsiCorrectness_Tests covers scoped continuity invalidation, repeated calls, preserved immutable months, pending registration and complete-month exceptions.
EsiContinuityInvalidationPostgres_Tests exercises authenticated preview endpoints, transaction rollback and invalidation of a worker paused during calculation.
Run that PostgreSQL test with PAYROLL_TEST_POSTGRES pointing to a fresh empty loopback payroll_tests_* database.
Never reuse a populated database for the test factory.
Drawer dismissal, month-input serialization and salary loading are verified in the frontend owning suites.

`SalarySlipAppService_Tests` creates approved compensation through template preview/save and compensation save before activating employees.
Its scoped identities must exist in the identity store, and academic fixture IDs must describe one valid hierarchy.
Overtime remains additional earnings rather than regular gross.

`EsiPayrollWorkflowApi_Tests` exercises pending registration, compensation, activation, review and payroll locking for `GROSS` and `EMPLOYER_COST`.
It also covers employees activated while ESIC was off, with employer-cost changes resolved explicitly through compensation.
Set `PAYROLL_REGISTRATION_TEST_POSTGRES` to a fresh empty loopback `payroll_tests_*` database.
The transaction fence requires PostgreSQL for this complete worker-to-lock journey.

`EsiPayrollWorkflowPostgres_Tests` runs actual earlier-month finalization, later-month preview calculation and payroll locking concurrently in both fence orderings.
Its August and September fixture explicitly enables ESIC in both months rather than inheriting the demo's disabled September scenario.
Set `PAYROLL_WORKFLOW_TEST_POSTGRES` to a separate fresh empty loopback `payroll_tests_*` database.
Finalization winning must supersede the running preview and reject the old lock; locking winning must preserve its immutable employee snapshot.
Run both workflow suites with `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FullyQualifiedName~EsiPayrollWorkflow` after setting their separate database variables.

### Final calculation audit matrix

| Scenario | Permanent regression coverage |
| --- | --- |
| Monthly salary and CTC, including the INR 10,000 examples | SalaryTemplateCalculator_Tests |
| Twelve CTC budgets, with and without PF, crossed with zero, one, half-month and full attendance | SalaryTemplateCalculator_Tests.Ctc_boundaries_reconcile_with_payroll_and_never_exceed_monthly_budget |
| Full-month quote reconciles with actual payroll; school cost never exceeds the monthly budget | SalaryTemplateCalculator_Tests |
| Standard/disability ceilings, employee-share exemption including one paisa above the boundary, exclusion add-back and rounding | EsiStatutoryCalculator_Tests |
| Generated overtime with ESIC on/off, above-ceiling employees and exclusion add-back | SalarySlipAppService_Tests and EsiStatutoryCalculator_Tests |
| Custom earning names cannot impersonate generated overtime | EsiStatutoryCalculator_Tests |
| Manual earning adjustments can be classified; other tenants, months and deductions stay outside the catalog | EsiPayrollWorkflowApi_Tests |
| Readiness summaries report the same genuine blockers as locking; missing identifiers remain non-blocking with calculated contributions | PayrollReadinessProjector_Tests and EsiPayrollWorkflowApi_Tests |
| Whole-month exceptions, leap years, invalid ranges and continuity period boundaries | EsiCorrectness_Tests and EsiStatutoryCalculator_Tests |
| Current/future recalculation, retries, stale edits and immutable earlier/locked payroll | EsiConfigurationResolution_Tests and EsiConfigurationPostgres_Tests |
| Earlier finalization, later calculation and locking overlap in both transaction orderings | EsiPayrollWorkflowPostgres_Tests |

Gross-based component fixtures must leave their component dependency ID empty.
Do not weaken the validator to accept contradictory gross and component-based dependencies.
Run the complete application and domain HR.Payroll filters in addition to the focused PostgreSQL workflows.
Report repository-wide failures separately from these payroll results.

### Payslip completion verification

Use the [acceptance specification](references/payslip-design.md) rather than treating focused tests as complete acceptance.
PayslipDocumentApi_Tests requires PAYROLL_REGISTRATION_TEST_POSTGRES pointing to a fresh disposable loopback payroll_tests_* database.
The fixture captures email locally and never sends production messages.
It verifies authenticated locking, frozen bank/school facts, identical artifact bytes, payment retry precision, non-monetary replacement, prior-version retrieval, duplicate requests, concurrent workers, uncertain SMTP outcomes and access denials.
PayslipPdfLayout_Tests covers immutable byte arrays, Indian issue-date boundaries, partial attendance, ESIC-off, covered zero-share, drafts and long tables.
Set PAYSLIP_VERIFY_ARTIFACTS to an ignored local directory to retain synthetic PDFs for text extraction and rendered inspection.
The layout test supplies authoritative calculated amounts; it does not replace calculation/reconciliation tests.
The payroll concurrency fixture now reviews the source month before direct finalization, matching the production guard.

On 2026-10-03, the focused PostgreSQL document/security checks passed three tests, and the combined earlier-finalization/later-calculation/locking test passed in both fence orderings.
Eight artifact/renderer checks passed before the subsequent repeating-header/page-number polish.
Repeat layout checks after renderer changes and inspect all generated pages.
The later renderer run passed eight artifact/layout tests and two employee-document response-mapping tests.
Five generated PDF scenarios passed text checks; rendered continuation pages retain their section headings and page identity without an orphaned deductions heading.

PayslipDeliveryLifecycleApi_Tests uses authenticated locking and a local capture transport on PostgreSQL.
It verifies document-storage failure rolls back the slip, employee lock and delivery intent, then succeeds on retry.
It also verifies replacement rollback, superseded automatic delivery, competing workers in new service scopes, uncertain-send recovery, missing recipient and an inactive requesting actor.
New service scopes demonstrate persisted-state recovery; they do not substitute for a complete host-process restart test.
The same regression calculates a changed-attendance correction candidate through PayrollSnapshotRefresher and asserts that the issued PDF, locked snapshot, adjustments and payroll input revision remain unchanged.
It rejects duplicate attendance dates and unsupported attendance status values before calculating a candidate.
The final combined lifecycle, PDF layout and response-mapping run passed 11 tests with no failures or skips.
Its ignored evidence log is .artifacts/payslip-final-candidate-and-artifact-tests.log.
The later correction implementation adds persisted draft, review, cancellation and unpaid reissue on top of this calculation foundation.

PayslipCorrectionApi_Tests requires a separate fresh PAYROLL_CORRECTION_TEST_POSTGRES database.
Its real API journey verifies a required reason, recorded attendance, stale concurrency tokens, invalid dates, denied correction actions, recalculation, review, reissue and preserved original PDF bytes.
`PayslipCorrectionPunchApi_Tests` uses a separate fresh `PAYROLL_PAYSLIP_CORRECTION_PUNCH_TEST_POSTGRES` database and the capture-only API factory.
It rejects punches on another UTC attendance date and reversed or equal punches without changing the correction draft or concurrency stamp.
The same journey corrects clock-in and clock-out through payroll, removes the resulting lateness and overtime, reviews and reissues while preserving the original PDF bytes.
`PayslipCorrectionAttendanceRules_Tests` covers nullable punches, status-only attendance, absent-day midnight pairs and equivalent timestamp offsets.
Competing payment and correction application serialize under the tenant fence, with payment rejected while the reissue is pending or the requested original revision is no longer current.
Repeated application returns one result document, and repeated cancellation retains the same cancelled evidence.
The final focused correction and state run passed nine tests with no failures or skips in .artifacts/payslip-correction-guard-tests.log.
The preceding correction/state/delivery lifecycle run passed ten tests in .artifacts/payslip-correction-final-api-tests.log.
The authoritative combined correction/state/lifecycle/PDF/mapping rerun passed twenty tests with no failures or skips in .artifacts/payslip-correction-authoritative-tests.log.
It verifies correction-specific later-preview supersession, old-lock rejection, recalculation and successful locking within the same contribution period.
It also verifies that a follow-up correction retains the latest applied attendance baseline.
These do not establish paid future-period settlement acceptance.

PayslipWorkerProcess_Tests requires its own fresh PAYROLL_PROCESS_TEST_POSTGRES database and an explicit PAYSLIP_WORKER_DOTNET executable when dotnet is not on PATH.
Run only Interrupted_worker_process_recovers_persisted_attempt_without_automatic_resend; it launches the conditional Delivery_worker_child test in separate operating-system processes.
The child attaches to existing test tables without seeders or grant restoration and runs the production PayslipDeliveryBackgroundJob with a capture-only synthetic recipient transport.
The parent terminates the first process after its committed PROCESSING claim reaches the transport.
The recovery process sees the persisted attempt and marks its expired claim UNCERTAIN without a resend.
Lease age is simulated separately so the test does not wait thirty minutes.
A deliberate new send captures the canonical PDF and employee recipient; a further process replays the completed attempt without another send.
The executed process restart regression passed one test with no failures or skips in .artifacts/payslip-worker-process-tests.log.
Synthetic process IDs, child TRX files and captured bytes remain under the temporary ShikshaMitraPayslipWorkerTests directory.
This proves separate delivery-worker process recovery; it does not replace production-host or browser distribution acceptance.

The complete EF migration chain, including AddPayslipDocumentsAndDelivery, applied to a fresh disposable PostgreSQL database on 2026-10-03.
The EF has-pending-model-changes command reported no changes since the last migration.
The resumed full application HR.Payroll filter passed 315 tests with no failures or skips.
The focused SalarySlipAppService_Tests filter passed 20 tests after repairing finalization fixtures to configure synthetic school identity, explicit non-ESIC coverage and the same academic-year calendar in preview and finalization.
Those fixtures use the actual durable preview and review workflow instead of bypassing the production review guard.
The test module sets the QuestPDF Community license explicitly, matching the local test host.
SalarySlipWorkflow now returns SalarySlipAlreadyFinalized before attempting freshness/review validation when a finalized or paid slip is finalized again.

Reproduce the application verification from the backend worktree with these commands:

```powershell
& 'C:/Users/VADHE/AppData/Local/EsicDotnet9/dotnet.exe' test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --no-restore --filter 'FullyQualifiedName~SalarySlipAppService_Tests'
& 'C:/Users/VADHE/AppData/Local/EsicDotnet9/dotnet.exe' test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~HR.Payroll'
```

The ignored evidence logs are .artifacts/payslip-finalization-fixtures.log and .artifacts/payslip-application-regressions.log.
The full correction and browser acceptance matrix remains open; focused unpaid reissue and actual delivery-worker restart checks are recorded above.

On 2026-10-03, PayslipSettlementApi_Tests, PayslipSettlementProjection_Tests and PayslipCorrectionState_Tests passed 15 tests on a fresh disposable PostgreSQL database.
The paid API journey covers negative recovery, positive follow-up arrears, zero differences, concurrent identical settlement requests, stale target previews, immutable paid-source PDF hashes, locked-target rejection and normal target review/locking after regeneration.
Projection tests cover cash and school-cost reconciliation, employer shares remaining separate, employee contribution recovery limited to corresponding source earnings, the aggregate recovery cap and rejection of unclassified recovery wages.
Use PAYROLL_SETTLEMENT_TEST_POSTGRES for a new loopback payroll_tests_* database and filter those three test classes.
The broader period-boundary, mixed-employee and direct-service security matrix still requires evidence.
The settlement API journey also rejects missing action grants, another tenant, an out-of-scope employee, a stale correction stamp and a target that is not later than the source.
The unpaid correction API journey injects a failure at the actual added-document persistence save after payroll updates, verifies transaction rollback and then successfully retries the same reviewed draft.
The focused unpaid correction fault test passed on a separate fresh PostgreSQL database.
PayslipBrowserFixture_Tests is an explicit opt-in fixture for the already-owned disposable browser database.
It creates a new synthetic Identity user, explicit test action grants and tenant-wide test scope without seeding or modifying existing users or grants.
Credentials are written only to the specified ignored artifact; never copy them into logs or committed files.

PayslipSettlementLockRace_Tests uses a separate fresh PAYROLL_SETTLEMENT_LOCK_TEST_POSTGRES database.
Both settlement and target locking begin from valid reviewed calculations and compete through the real authenticated HTTP endpoints on PostgreSQL.
Exactly one request may succeed; the losing operation must not create mixed payroll or document evidence.
If settlement wins, regenerate, review and lock the target before checking the reconciled issued amount.
If locking wins, retain the reviewed correction and the original target amount.
Both outcomes retain the paid source PDF hash and exactly one target document.

The opt-in browser settlement verifier checks immutable source amounts, the applied correction reference, target reconciliation and stored PDF hashes.
Its runtime migration-model assertion currently exposes the existing PostgreSQL timestamp-mode disagreement: runtime enables legacy timestamps, while the design-time factory disables them.
The canonical design-time pending-model check passes, but the attached runtime comparison reports timestamp-without-time-zone versus timestamp-with-time-zone changes across 1,254 existing columns.
Do not treat that runtime assertion as passing or generate a payslip migration for unrelated timestamps.

## Resumed payslip continuity and browser evidence, 2026-10-05

PayslipCorrectionContinuity_Tests adds nine repository-backed regressions for contribution-period boundaries, applied correction precedence, unchanged reviewed drafts, tenant/employee isolation and period-bounded invalidation.
The boundary cases cover February/March, March/April, August/September, September/October and October/March source evidence.
The focused nine-case run and the complete application payroll run passed with no failures or skips.
The final application filter passed 324 tests, including these new cases.
The domain payroll filter passed 105 tests with no failures or skips.
These checks do not replace an authenticated paid settlement across each contribution-period boundary.
The opt-in prepare-history-employee browser fixture attaches only to the already-owned loopback payroll_tests_* database without recreating tables or reseeding grants.
It creates or reuses one dedicated synthetic employee through the existing onboarding, enrollment, salary, attendance, review and locking pipeline.
It captures the employee's delivery locally and verifies the original paid document's checksum remains unchanged.
The fixture passed one test with no failures or skips.
Authenticated repeated, interrupted, pending and failed delivery-history selections used that second issued employee and retained the correct recipient.
The live-host browser Resend journey used a loopback-only SMTP capture that rejected every recipient except that employee's exact synthetic address.
The host worker recorded provider acceptance and the captured PDF checksum equalled the immutable stored artifact.
This establishes actual host polling and browser resend with capture transport, not a new process interruption/recovery test.
The previous operating-system delivery-worker interruption test remains separate evidence.
No real email, production database, existing-user password or grant restoration was used.

PayslipSettlementMixedEmployee_Tests requires its own fresh PAYROLL_SETTLEMENT_MIXED_TEST_POSTGRES database.
Its authenticated API journey settles a paid source into a later month while another employee's target is already locked and issued.
Settlement retries retain one correction and supersede the target run, rejecting its old review and lock.
Regeneration reconciles the corrected employee's net difference and permits review and locking.
The unrelated locked employee retains the same money, coverage decision, employer contribution and issued document checksum.
The paid source checksum and payment status remain unchanged.
The executed PostgreSQL regression passed one test with no failures or skips.
Broader contribution-boundary settlement races and the remaining jurisdiction and distribution matrix remain outstanding.

The final mixed-employee rerun also compares regenerated net pay to the issued target response and verifies issued earnings minus deductions equals net at the currency precision.
It passed one test with no failures or skips after those assertions were added.
The runtime migration diagnostic was rerun on a new empty owned database and applied all 106 migrations in this original checkout.
No migration remained pending, while the unchanged runtime/design-time timestamp mismatch remained present as expected.
The strict model-equality assertion remains intact and is not counted as passing.
Both timestamp-mode source files are unchanged from this checkout's HEAD and the fetched current main.
Current main additionally contains two finance migrations and must be reconciled before repeating migration acceptance on the publication branch.
No manual snapshot repair, timestamp conversion or unrelated migration was added.

The final mixed-employee test also invokes the actual correction service without ABP method interception.
It retains 21 explicit permission denials, seven exact employee-scope denials and seven other-tenant denials against committed document/correction IDs.
The permission cases include read-only access, missing Adjustments and missing payroll Lock, including an applied settlement retry.
Each scope/tenant mutation runs in a separate transaction disposed without commit, and a final authenticated read verifies the paid source PDF remains unchanged.
The final guarded PostgreSQL rerun passed one test with no failures or skips in 2m49s.
These are 35 boundary assertions within one workflow test, not 35 xUnit cases.

The final signed-settlement, settlement-versus-lock, settlement-projection, correction-state and PDF-layout selection passed 24 tests with no failures or skips in 4m47s.
The authenticated paid API journey covers negative, positive and zero differences, concurrent/retried settlement, stale or ineligible targets and the protected paid original.
The separate authenticated settlement-versus-lock journey permits exactly one winner and reconciles the issued target through regeneration when settlement wins.
These ran on separate fresh loopback databases and do not certify every contribution-boundary or deterministic race ordering.
The final unpaid correction API rerun passed one test with no failures or skips in 2m47s on another fresh disposable database.
It injects failure at actual replacement-document persistence, verifies rollback and retained review, then retries reissue while a competing payment is rejected.

## Paid settlement concurrency qualification

PayslipSettlementLockRace_Tests additionally controls both transaction orderings using the existing PostgreSQL command barrier.
The first operation pauses after acquiring the tenant revision fence, and the other operation is observed competing for that fence before release.
Settlement-first requires the old lock to fail and the regenerated target to reconcile before issuance.
Lock-first retains the reviewed correction and issues the original target amount.
The original simultaneous race and both controlled orderings passed three tests with no failures or skips.
Use separate fresh PAYROLL_SETTLEMENT_FIRST_TEST_POSTGRES and PAYROLL_LOCK_FIRST_TEST_POSTGRES databases, in addition to PAYROLL_SETTLEMENT_LOCK_TEST_POSTGRES.

PayslipSettlementPreviewRace_Tests settles through the authenticated API while a target preview worker is already running and paused during its attendance read.
Both the completed target and running worker remain superseded after the worker resumes.
The worker must publish no result rows, restore no deleted employee snapshot and provide no current fallback result.
Old locks fail; regeneration, review and locking reconcile one issued target while retaining the paid original's PDF checksum.
This PostgreSQL regression passed one test with no failures or skips.
Use a fresh PAYROLL_SETTLEMENT_PREVIEW_TEST_POSTGRES database.

PayslipSettlementBoundary_Tests covers paid-source statutory differences across February/March, March/April, August/September and September/October through actual preview, review, settlement and locking endpoints.
Its historical test configuration explicitly enables the two selected owned months because the full-demo fixture otherwise disables September and starts enabled calculation in October.
A new compensation revision in a different effective month must use that month's concurrency baseline rather than the prior month's revision number.
Keep the coverage, exact net and employer-cost reconciliation, repeated settlement, stale review and protected paid-document assertions authoritative.
The final four authenticated boundary cases passed with no failures or skips in 6m11s on separate fresh PostgreSQL databases.
The initially failed fixture runs remain recorded separately and are not included in the passing count.

PayslipDocumentPayrollMatrix_Tests passes seven synthetic scenarios through actual calculation, review, locking, issued-document reads and canonical PDF downloads.
The single PostgreSQL test covers full monthly salary, full CTC, one unpaid day, mid-month joining, an entirely unpaid covered month, a reviewed and classified additional earning, and ESIC-off payroll.
It requires the existing review guard before creating the additional earning, exact currency/component reconciliation, no employer fields in the employee contract and unchanged prior PDFs after later settings changes.
The final run passed one test with no failures or skips in 1m59s.
Use a separate fresh PAYROLL_PAYSLIP_MATRIX_TEST_POSTGRES database.
Optional PAYSLIP_VERIFY_ARTIFACTS exports the actual issued PDFs and corresponding API evidence for independent content inspection.
This is one workflow test with seven scenarios, not seven xUnit cases.
The fixture explicitly retains first-month comparison and low-attendance warnings while rejecting every blocking payroll issue.
It checks the persisted four-decimal proration factor without relaxing exact currency assertions.
All seven issued PDFs additionally passed static text/content checks on Node20.19.5 and PDF.js5.6.205 against the recorded API facts.
Those checks preserve all employee-name glyphs across observed line wrapping and do not replace authenticated browser or native-print acceptance.

## Payslip employment-boundary and identity-correction qualification

The document matrix now also covers the employee-share exemption, above-ceiling non-coverage, leaving-month payroll and attendance-generated overtime.
Its overtime case starts at the monthly coverage ceiling and proves that generated overtime does not incorrectly end coverage or enter the regular monthly salary base.
The exemption case retains the insurance identity and employer contribution while charging no employee ESI.
The non-covered case retains the existing professional-tax deduction instead of assuming every deduction is zero.
Configured department and designation are checked through the employee API, issued response and stored PDF.
An authenticated API reproduction retained full-month attendance after a retroactive exit and failed because the calculation still paid the full month.
The pipeline now uses the existing lifecycle eligibility rule to filter attendance and approved leave before ordinary and correction calculations.
The permanent leaving case retains post-exit attendance, a post-exit overtime punch and approved paid leave, then requires the exact eligible weekday count and no post-exit overtime amount.

The subsequent disposable browser-verification host uses the rebuilt application assembly matching the final document/API test output.
Existing synthetic-identity authentication and settings/document reads succeeded without creating new grants, changing passwords or reseeding the database.
Both returned PDF revisions match their persisted hashes.
Anonymous requests return no settings or PDF data, and an invalid bearer returns 401.
The host has automatic email off and a process-only loopback SMTP destination; no delivery was requested in this qualification.
Two consecutive authenticated settings writes using the current concurrency stamps verified the valid API ordering used by the frontend late-response regression.
The original synthetic settings were restored afterward.
Final Codex browser navigation still fails before authentication, so these API results do not complete browser acceptance.

The final document matrix includes twelve scenarios after adding the retained employer-share-relief configuration.
The actual payroll evidence records government funding of 325, no school employer contribution, school cost of 10000, employee deduction of 75 and net pay of 9925.
The employee contract omits government contribution evidence, and its stored PDF shows the employee deduction without employer or government contribution lines.
The single API matrix test passed all twelve scenarios on a separate freshly created disposable PostgreSQL database without email delivery.
All twelve exported official artifacts passed PDF.js5.6.205 content reconciliation on Node20.19.5, including complete text mappings and canonical amounts.
The newly issued relief page was also inspected through read-only Poppler rendering without observed clipping or overlap.
No statutory rule, stored historical PDF or compensation fact was rewritten for this check.
It also checks that the employee is excluded from the following payroll month and that earlier issued artifacts remain unchanged.
The final matrix run passed one test with eleven scenarios, alongside the identity-correction test below.

PayslipDocumentFactsCorrection_Tests uses its own fresh PAYROLL_PAYSLIP_FACTS_TEST_POSTGRES database.
Its single authenticated workflow test covers both unpaid and paid CTC payroll, recorded bank/name/department/designation changes, concurrent replacement requests and retries.
The replacement must preserve the entire recorded monetary, attendance and statutory projection while adopting the corrected identity facts.
Both original artifacts and their provider-accepted capture-delivery histories remain accessible, and a new stale-document replacement is rejected.
The final two-test run passed with no failures or skips in 3m34s.
All fifteen exported issued artifacts additionally passed static extraction checks on Node20.19.5 and PDF.js5.6.205 against their actual API facts.
No stored document was rewritten and no salary facts were generated by the verification script.

The unpaid correction/reissue and paid signed-settlement API regressions were repeated after the employment-boundary fix.
Both tests passed with no failures or skips in 2m56s on separate fresh PostgreSQL databases.
They retain the PDF-persistence rollback, stale review, payment competition, signed differences, target eligibility and idempotency assertions.
An initial no-build application run used a stale default output with315 tests and is not evidence for this source change.
Rebuild the application test project before accepting its results or use an explicitly verified current output directory.
The subsequent fresh build passed all324 application payroll tests with no failures or skips in 2m14s, including the nine continuity regressions.

The subsequent final document selection passed ten tests with no failures or skips in 3m15s after the presentation-label change.
It includes the eleven-scenario PostgreSQL matrix, paid/unpaid identity-correction workflow and eight PDF layout/date/immutability cases.
The recorded payroll-basis-minus-payable aggregate is labelled Non-payable days rather than Unpaid days because it also includes days outside employment.
Static extraction against all fifteen actual issued artifacts verifies that label, complete glyph mappings, recorded component amounts and no employer-contribution fields.
The pre-change label probe fails against the preserved earlier PDFs and remains separate reproduction evidence.
Read-only Poppler inspection covered the fifteen-document contact sheet, the full leaving-month page and all three long-name/45-earning pages without observed clipping or overlapping content.
This content and layout qualification does not establish legal-format, authenticated browser or native-print acceptance.
The324 application and105 domain tests were run before this final presentation-only label change; the ten-test document selection is the final-source verification for that change.

## Contribution-boundary races and delivery action permissions

The final `PayslipSettlementLockRace_Tests` selection adds both controlled transaction orderings for March/April and September/October.
It uses a paid source with eligible monthly wages and a target whose agreed monthly wages exceed the ceiling in the next contribution period.
The target retains above-ceiling non-coverage while source-period component differences reconcile independently.
Assertions retain one valid winner, exact net and school-cost differences, source document and correction references, idempotent retries, immutable paid originals and canonical issued-PDF hashes.
The original simultaneous race, both controlled orderings and four contribution-boundary cases passed seven tests with no failures or skips on separate fresh PostgreSQL databases.
Use the existing race connection variables and separate `PAYROLL_BOUNDARY_03_SETTLEMENT_FIRST_TEST_POSTGRES`, `PAYROLL_BOUNDARY_03_LOCK_FIRST_TEST_POSTGRES`, `PAYROLL_BOUNDARY_09_SETTLEMENT_FIRST_TEST_POSTGRES` and `PAYROLL_BOUNDARY_09_LOCK_FIRST_TEST_POSTGRES` databases.

`PayslipAutomaticDeliveryPermission_Tests` reproduced a worker permission mismatch through authenticated calculation, review and locking followed by the actual delivery job.
Automatic issuance had recorded Calculate/Review permission instead of the Lock permission that authorizes its source action.
The failing regression showed a Lock-only approver rejected and a Calculate-only actor accepted after Lock was revoked.
Automatic lock delivery now records and rechecks `RunPayroll.Lock`.
Direct issuance and manual delivery retain `GenerateSalarySlip.Add`.
The controlled test permission checker supplies current action grants while persisted actor identity and employee scope are still resolved; this does not claim production permission-store revocation testing.
The regression retains exact synthetic recipient and stored-PDF hashes, duplicate-attempt protection and successful manual sending.
Use a separate fresh `PAYROLL_PAYSLIP_LOCK_PERMISSION_TEST_POSTGRES` database.
The post-fix selection passed the new workflow test and thirteen permission API tests with no failures or skips.

The final formatted-source selection passed two tests with no failures or skips: the automatic-delivery permission workflow and `PayslipWorkerProcess_Tests.Interrupted_worker_process_recovers_persisted_attempt_without_automatic_resend`.
The latter launches distinct operating-system processes, terminates the processing worker, attaches to persisted state without reseeding, preserves the uncertain result, then captures one explicit retry and rejects duplicate execution.
This is actual worker-process interruption and restart evidence, separate from API-host restart and dependency-injection scope replacement.
The two-test selection overlaps the fourteen-test selection; do not sum them as unique coverage.
Fresh databases use `PAYROLL_PAYSLIP_LOCK_PERMISSION_TEST_POSTGRES` and `PAYROLL_PROCESS_TEST_POSTGRES`.
The rebuilt disposable API host requires separate supported authentication and read-only PDF checks; no real mail, production data or browser acceptance follows from these API tests.

## Partial distribution and recipient-repair acceptance

`PayslipPartialBatchDelivery_Tests` runs authenticated onboarding, ESIC enrollment, compensation, preview, review and a three-employee lock on its own fresh PostgreSQL database.
It injects a component-integrity fault into one disposable unlocked draft before locking, without modifying an issued document or disabling a production guard.
The lock must report two finalized employees and one failure, with no slip, document or delivery intent for the failed employee.
Both successful employees receive only their own stored-PDF capture, and repeated execution creates no additional messages.
Disabling automatic email preserves already committed intents but creates no intent when pipeline regeneration subsequently repairs and finalizes the failed employee.
The test also verifies the initialized fixture's off default, repeated settings reads and idempotent Save.
It does not establish fresh production-school initialization or an academic-year switch.
Use a separate fresh `PAYROLL_PAYSLIP_PARTIAL_DELIVERY_TEST_POSTGRES` database.
This workflow passed one test with no failures or skips in 1m48s.

The expanded `PayslipDeliveryLifecycleApi_Tests` reaches both missing and invalid persisted address failures without changing finalized payroll.
Its new HR correction-and-retry path fails at the actual employee personal-info API with HR:Employee:010034 because employee email is intentionally immutable after onboarding.
The combined selection therefore reports two tests, one passed and one failed; it is not a green delivery suite.
The failing expectation remains meaningful evidence that the accepted recipient-recovery requirement conflicts with the existing protected Identity-linked email workflow.
No successful repair, later retry assertions or the later actor-revocation assertions were executed in this run.
Do not repair the fixture directly and claim that proves HR can correct an address.
Resolve the recipient-design decision before implementing recovery, while preserving the existing email protection.
The full original requirement remains open; it was not weakened or skipped to make the suite pass.

## Fresh-school settings persistence

`PayslipSchoolSettings_Tests` creates a new synthetic school through the authenticated tenant API instead of relying on the payroll factory's preconfigured school.
Three settings reads return automatic email off, the actual school name, no fabricated address and no persisted configuration row.
The synthetic administrator receives an explicit tenant-wide scope through the supported scope API in that newly created school only.
Saving and repeating the same idempotent request produces one settings row; reusing that key with a different payload is rejected.
Explicitly denied reads and saves create no row, and a stale concurrency token cannot change the saved configuration.
Creating and reading two actual academic-year records preserves the same school settings and concurrency stamp.
The original test host is disposed, and a second host attaches to the same owned database without recreating tables or reseeding identities, grants or settings.
The saved configuration survives that host recreation, and the other tenant's configuration remains unchanged.
The capture-only transport receives no messages.
Use a separate fresh `PAYROLL_PAYSLIP_SCHOOL_SETTINGS_TEST_POSTGRES` database.
The final rebuilt selection, including the permission and concurrency assertions, passed one test with no failures or skips in 2m23s on 2026-10-05.
The preceding 2m16s pass covered the earlier version of the same test and is retained separately, not added as unique coverage.
This is backend settings persistence evidence, not browser year-selector acceptance or delivery-worker process restart evidence.

The existing school-settings regression now consumes the actual successful PostgreSQL-backed response and deliberately loses its HTTP acknowledgement through a DelegatingHandler.
An authenticated read proves the settings committed before the identical original payload/key is replayed with its old concurrency stamp.
The replay returns the same committed version and leaves one configuration row, while differing-payload replay and a new stale-token request remain rejected.
Permission denial, tenant isolation, new-school defaults, academic-year persistence, host recreation and no-email assertions remain intact.
This strengthened fact passes one case with zero failures/skips in 1m08s on 6 October2026.
Its first invocation failed before host creation because the empty disposable database had not been explicitly created; that failure is retained separately from the corrected fresh-fixture run.
The companion [frontend settings transport tests](https://github.com/gyanmirai/Shikshamitra-FE/blob/main/docs/hr/testing.md#settings-acknowledgement-recovery) verify truthful lost/unreadable-response feedback and dirty-form request retention using the real RTK Query path.
These API/transport checks do not prove authenticated browser use or a physical socket interruption.

## Correction evidence equality and issuance recovery

`PayslipCorrectionInsurance_Tests` reproduced a zero-pay CTC correction failing review with a stale-calculation conflict before the missing-insurance guard ran.
A read-only recalculation against that disposable PostgreSQL fixture found only `EmployerCost` and `NetPay` differed: persisted zero versus recalculated negative zero.
PostgreSQL jsonb normalizes those equivalent representations, so correction review, unpaid reissue and paid settlement now compare exact JSON values rather than serialized text.
The comparison uses no monetary tolerance or rounding and still includes every calculation field, nested component and array position.

`PayslipCorrectionCalculation_Tests` retains twelve permanent cases.
Before the fix, the two equivalent-representation cases failed and the ten changed-evidence rejection cases passed.
After the fix, the combined calculation-equality, correction-state and settlement-projection selection passed all 26 tests with no failures or skips.
Changed amounts below one paisa, component amounts, coverage, insurance identity, configuration version, issue codes, component order, attendance source and null-versus-empty values remain conflicts.

The final authenticated insurance workflow passed one test with five scenarios in 1m31s on its own fresh `PAYROLL_PAYSLIP_CORRECTION_INSURANCE_TEST_POSTGRES` database.
Full pay, zero-pay CTC and employee-share exemptions retain the missing-number issue and calculated contributions where applicable; review and reissue remain available while registration is pending.
Pending registration allows compensation preparation, review and reissue without changing any original PDF bytes.
Adding a registration number changes follow-up status, not calculated amounts.
Above-ceiling and ESIC-off corrections require no insurance number and omit employee ESIC deductions and employer amounts as applicable.
The first fixture attempt used an obsolete revision token for a new compensation month; the test was corrected to follow the existing month-specific concurrency contract.
The subsequent API failure was the reproduced negative-zero product bug and remains separate from that fixture error.

`PayslipSchoolIssuance_Tests` omits only the synthetic school settings row in its fresh owned fixture database.
Salary preparation, activation, calculation and review succeed without invented school facts, but payroll locking returns the actionable school-details error.
That rejected lock leaves no salary slip, official document, locked employee snapshot or delivery intent.
An invalid settings save creates no configuration; saving the required school name and address then permits recalculation, review, locking and first-revision issuance.
Use a separate fresh `PAYROLL_PAYSLIP_SCHOOL_ISSUANCE_TEST_POSTGRES` database.
The final school-issuance, unpaid-correction and paid-settlement API selection passed three tests with no failures or skips in 3m46s on 2026-10-05.
The unpaid regression includes PDF-persistence rollback and competing payment/reissue requests; the paid regression retains exact settlement reconciliation, retries and the protected paid original.

A read-only export of the recovery fixture's stored PDF matched its persisted checksum and was parsed using Node 20.19.5 and PDF.js 5.6.205.
The one-page A4 artifact contains the configured school name/address, employee identity, employee ESIC and frozen net pay, without employer-cost fields, NUL characters or replacement glyphs.
This is stored-artifact content evidence, not fresh browser rendering, native printing or Gujarat legal-format acceptance.
Recipient-design, browser, jurisdiction, current-main integration and the existing runtime timestamp-model questions remain open.

## Persisted delivery authority and claim-to-send changes

`PayslipDeliveryAuthority_Tests` prepares, reviews and locks synthetic payroll through the authenticated API on a fresh owned PostgreSQL database.
It creates one new synthetic Identity user, a custom non-administrator role and explicit tenant-wide scope for that user only.
Permission grants and revocations use the actual permission-management API, including its cache invalidation.
The worker receives the real ABP PermissionChecker and resolves persisted identity, role membership and employee scope; its authority is not simulated through the API test permission header.
The API bootstrap retains TestAuth, so this test does not establish browser authentication or the production sign-in journey.

The final workflow verifies nine attempts: two authorized captures and seven ACCESS_REVOKED failures without a provider call.
Revoking a direct user grant leaves an inherited role grant effective, while removing the final effective grant prevents delivery.
Scope revocation and an inactive Identity user also prevent delivery despite a remaining action grant.
Three additional cases commit PROCESSING first, then revoke the role grant, scope or active identity before the send-phase authority check.
The test boundary delegates both fence calls to the actual PostgreSQL store and performs the change before acquiring the second fence, allowing scope writes to commit without a test-induced deadlock.
Repeated execution of terminal failed attempts sends nothing.
The two captured recipients and attachment hashes match the selected official document, whose stored checksum and Finalized source state remain unchanged.
No existing identity, grant, production data or real email is modified.

Use a separate fresh `PAYROLL_PAYSLIP_AUTHORITY_TEST_POSTGRES` database.
The final formatted-source workflow passed one test with no failures or skips in 1.9379min on 2026-10-05.
The preceding six-attempt version passed one test in 2.1701min; these runs overlap and are not two unique tests.
The original recipient-repair lifecycle remains failed at the protected employee email update.
Its later actor assertions are still unexecuted in that combined journey, while the independent authority workflow above supplies direct persisted-authority evidence.
Neither this regression nor the retained worker-process restart test resolves recipient recovery, fresh browser acceptance or legal applicability.

## Migrated payslip timestamps and delivery leases

`PayslipTimestampMigration_Tests` applies all 106 actual migrations to a new owned PostgreSQL database and uses a database session timezone different from the process timezone.
The authenticated compensation, review and locking workflow reproduced a 330-minute shift between frozen issuance evidence and the persisted issued document.
Explicit timestamp types for the four new payslip aggregates now match their existing migrated schema without changing the global legacy timestamp switch, generated migrations or immutable documents.
The mapping-only rerun then reproduced a UTC worker retaining PROCESSING after 31 actual elapsed minutes because the lease compared local and UTC wall-clock ticks.
Lease recovery now compares both instants in UTC.

The final regression passed one test with no failures or skips in 3.2016 minutes on 2026-10-05.
It verifies correct issuance time to PostgreSQL microsecond precision, fresh-claim protection, expired-claim uncertainty for both local and UTC clocks, no automatic resend and one deliberate retry with the exact stored PDF hash.
Its controlled interruption occurs after the persisted claim and before the second transaction fence; it is not an OS process-restart test.
Use a fresh `PAYROLL_PAYSLIP_TIMESTAMP_MIGRATION_TEST_POSTGRES` database and the existing worker process regression for actual restart acceptance.

A read-only model probe on that fully migrated database reports 106 applied migrations, zero pending migrations, zero differences in the four new payslip tables and 1,239 remaining unrelated timestamp differences.
The strict runtime pending-model check still fails and is not suppressed or treated as a pass.
The older populated browser fixture applied only one delta migration over runtime-created tables, so its timezone-free timestamps do not independently prove absolute issuance-time drift.
The fully migrated API regression above supplies that proof without converting the older fixture or rewriting any stored PDF.

The expanded migration regression also reproduced an 89-microsecond difference between replacement history metadata and its canonical frozen facts because the replacement path captured the clock twice.
Replacement issuance now reuses its captured facts timestamp; a repeated request retains the same document identity and timestamp at PostgreSQL microsecond precision.
The protected original PDF hash remains unchanged.

`PayslipDocumentApi_Tests` reproduced the payment API accepting an instant ten minutes in the future when comparing UTC input against the local server clock.
Payment validation now compares both instants in UTC.
Both UTC and offset representations of that future instant return the existing ProblemDetails conflict code and leave the source Finalized and its PDF unchanged.
A valid past payment and its idempotent repeat still succeed.

The broader 46-test selection passed 45 cases and failed one unpaid-correction case during unrelated demo seeding with a PostgreSQL read timeout while another heavy fixture process was running.
The failed log is retained; no query timeout, assertion, permission or configuration was relaxed.
The final-source rerun serialized the three fresh fixture cases and passed all three with no failures or skips in 6.5426 minutes on 2026-10-05.
It covers unpaid correction, document/payment history and the fully migrated issuance, replacement and lease regression.
The broader selection's actual OS worker-process interruption and recovery case passed in 3m9s, with separate interrupted, recovered, accepted and repeated worker PIDs and one matching stored-artifact capture.
The final metadata fix affects replacement issuance only; the broader binary predates that one-line fix, and the final three-case rerun exercises the affected replacement paths.
These runs overlap and must not be added as separate unique test totals or treated as current-main integration evidence.

## Direct payslip service boundaries

`PayslipDirectGuard_Tests` prepares and locks covered payroll through the authenticated API, then invokes the four payslip application services without ABP/Castle proxies.
The fixture creates two active synthetic non-administrator identities and assigns tenant-wide scope to one through the supported scope API.
Explicit test permission headers verify each direct action guard, including read-only access, correction adjustments, review and payroll locking.
These controlled action checks are separate from the actual ABP permission-provider delivery-authority regression above.

The regression denies access to protected documents, history, payment, correction, delivery and school settings for an unscoped identity carrying a forged administrator claim.
Changing the current tenant also denies the original tenant's resources, including a draft already loaded in the same unit of work, and cannot save foreign school settings.
Authorized direct payment, delivery queueing, correction calculation/review/cancellation, factual replacement and settings writes supply positive controls.
A deliberately uncommitted transaction restores the original payment status, settings and canonical PDF, removes the correction and delivery records, and leaves no persisted delivery background job or capture.
Use a separate fresh `PAYROLL_PAYSLIP_DIRECT_GUARD_TEST_POSTGRES` database.
This is authenticated fixture and direct-service evidence, not browser sign-in acceptance or recipient-address recovery.

The final regression passed one test with no failures or skips in 2.6831 minutes on 2026-10-05.
It contains 35 action-permission denials and 28 tenant/scope denials, plus the authorized workflow and rollback assertions.
The earlier 3.2451-minute pass predates the added foreign-settings and persisted-job checks, so these overlapping runs are not separate unique coverage.
An intermediate run failed a pre-commit job count because it queried the tenant payroll DbContext instead of ABP's job repository context.
A rollback-only probe found one job through the actual repository before rollback and zero afterward; the permanent regression now uses that repository and retains both assertions and the source-tenant check.
No production queue, authorization, transaction or configuration was changed to satisfy that test.
The existing `PermissionConsistencyApi_Tests` selection passed all thirteen tests with no failures or skips in 2.6731 minutes on the same final test binary.

## Delivery currency and reissue serialization

`PayslipDeliveryCurrency_Tests` prepares three covered employee payrolls, locks them and reviews reasoned unpaid corrections through the authenticated API on a fresh owned PostgreSQL database.
Automatic delivery uses a capture-only provider.
The regression covers a replacement committed before worker claim, a replacement committed after PROCESSING but before the send-phase fence, and a provider call already holding that fence when reissue starts.

The first two orderings require the original attempt to become SUPERSEDED with DOCUMENT_REPLACED and no provider call.
The third observes PostgreSQL lock wait evidence while the synthetic provider is paused, proving the reissue transaction cannot commit until that call completes.
An accepted original remains historical evidence when the replacement subsequently becomes current.
Every replacement queues one automatic attempt, and competing workers and repeated executions produce only one replacement capture.
All captured recipients, filenames and attachment hashes match their selected stored revision, while both original and replacement downloads retain their recorded checksums.

The test delegates both worker fences to the actual PostgreSQL store.
Its controlled API permission checker verifies this currency workflow only; the separate delivery-authority regression supplies actual ABP permission-provider evidence.
Use a separate fresh `PAYROLL_PAYSLIP_CURRENCY_TEST_POSTGRES` database.
This regression does not resolve recipient recovery, browser sign-in, native printing or Gujarat legal applicability.

The final regression passed one test with no failures or skips in 2.9544 minutes on 2026-10-05.
That single xUnit case exercises all three orderings and asserts exactly four captures across the three employee workflows.
It required no production worker, correction, permission or configuration change.

## Draft document boundaries and log privacy

`PayslipDraftBoundary_Tests` reads draft payroll and its PDF through the authenticated API before review or locking.
It verifies there is no issued revision, that the official PDF endpoint rejects the draft and that sending the source slip identifier cannot queue a delivery.
Review alone still creates no official document or automatic attempt.
After locking, preview, download and the captured automatic attachment must use the exact stored first revision.

The fixture omits only its own newly configured school row before issuance and restores it through the supported settings API.
The unchanged implementation returned a preview despite missing saved school facts, and its structured request logs contained TotalEarning, TotalDeduction and NetSalary fields.
The draft renderer now reuses the required saved school identity/address and the existing actionable missing-details error.
This rendering guard does not block payroll preparation, create an activation requirement or regenerate an issued document.
Request diagnostics retain employee/period/status/timing fields while excluding those salary totals.

The test captures log field names rather than their sensitive values and uses a separate fresh `PAYROLL_PAYSLIP_DRAFT_TEST_POSTGRES` database with capture-only email.
Optional `PAYSLIP_VERIFY_ARTIFACTS` exports the exact draft/issued response bytes and synthetic evidence for independent content and layout inspection.
This workflow does not prove a fresh browser, native printing or Gujarat applicability acceptance.

The product reproduction failed the missing-school response and all three request-log privacy assertions before the fix.
An earlier harness attempt failed because it queried the persisted draft using a display month instead of the canonical yyyy-MM value; that is retained separately from the product reproduction.
The final API regression passed one test with no failures or skips in 3.0401 minutes on 2026-10-05.
The same final source passed twenty salary-slip application tests and ten PDF-layout/response-mapping tests with no failures or skips.
The existing permission-consistency API selection passed all thirteen cases with no failures or skips in 2.7672 minutes on that same final test binary.
The Release API-host build succeeded with 133 analyzer/nullability warnings and no errors.

PDF.js 5.6.205 on Node 20.19.5 extracted both exact API artifacts and verified configured school identity/address, employee facts, preview/official labels, INR 9,925 net pay, INR 75 employee ESI, no employer fields and no NUL/replacement characters.
Poppler rendered both final A4 pages, which were visually inspected for readable labels, aligned values and clipping.
These static checks and canonical-byte assertions remain separate from the outstanding authenticated Codex browser, native-print and jurisdiction acceptance.

## Correction history transport ordering

`PayslipCorrectionReadOrder_Tests` onboards, enrolls, calculates, reviews and locks synthetic covered payroll through the authenticated API on a fresh owned PostgreSQL database.
It creates a reasoned unpaid correction and recalculates twice, using each acknowledged concurrency token.
An earlier history request is processed before the second write, while its response body is consumed after that write.
The earlier body retains the first candidate and token; a current history read returns the second candidate and token.
A stale review returns the existing 400 response and payslip conflict code without changing the correction.
Review with the latest token succeeds, reissue produces revision two, and the original stored PDF checksum remains unchanged.
The fixture uses capture-only delivery and a separate `PAYROLL_PAYSLIP_CORRECTION_READ_TEST_POSTGRES` database.
The final-source test passed one case with no failures or skips on 2026-10-05.
The earlier expectation of HTTP 409 was a harness error against the existing 400 mapping, not a product change or a weakened stale-review guard.
An initial no-match execution is retained as unexecuted, not counted as a pass.
The companion frontend regression uses actual RTK Query cache behavior to reject the late read and block cached actions after failed-history reopening.
The API and component results remain separate from fresh authenticated browser and jurisdiction acceptance.

## Frontend document-status freshness

The [owning frontend testing notes](https://github.com/gyanmirai/Shikshamitra-FE/blob/main/docs/hr/testing.md#document-status-freshness-and-preserved-drafts) describe the controlled HTTP/RTK Query and permission regressions.
The payslip-directory selection passed 63 tests in thirteen files with no failures or skips, including an HTTP 403 scope-denial regression.
It covers pending and failed status reads, retained reasons and attendance, source replacement, Retry, implicit disabled-form submission and View/Add revocation.
Its real PDF-fetch hook also retains the loaded artifact URL without another PDF request during metadata refresh.
An authorization denial instead hides protected cached content and revokes that URL, including during a pending Retry until a successful current-selection read restores access.
The seven initial UI failures and the separate artifact-refetch reproduction are recorded with their test boundaries in the implementation handoff.
This increment changes no backend contract, authorization rule, database mapping, immutable document or delivery provider.
The prior authenticated PostgreSQL and direct-service guards remain separate evidence and were not rerun for this frontend-only change.
These checks do not resolve fresh browser access, native printing, recipient recovery or Gujarat wage-slip applicability for either school category.

The [real-query employee-history notes](https://github.com/gyanmirai/Shikshamitra-FE/blob/main/docs/hr/testing.md#real-query-employee-history-selection) add two controlled late-success/failure cases with issued employees and distinct recipients.
They verify actual RTK Query subscription changes, retained cache, repeated/interrupted selection and the visible Retry action without another backend implementation or a real delivery.
These automated HTTP checks remain separate from final authenticated browser acceptance.

## Delivery requester contract

PayslipDeliveryActor_Tests uses a fresh PAYROLL_PAYSLIP_ACTOR_TEST_POSTGRES database and the authenticated onboarding, salary, attendance, review and locking pipeline.
Its pre-fix HTTP response failed because the requesting actor had no readable display-name field.
The regression checks automatic history and manual/duplicate responses against the persisted requesting identity, rather than substituting the current reader's name.
Deliberately inconsistent foreign and missing requester IDs are display-projection fixture cases and must return no foreign account name.
The new foreign fixture account has no grants, no real email is sent, and the test does not rewrite immutable PDFs or payroll amounts.
The [frontend requester notes](https://github.com/gyanmirai/Shikshamitra-FE/blob/main/docs/hr/testing.md#delivery-requester-visibility) describe the companion label and unavailable-account cases.
Exact executed counts and final source hashes belong to the implementation handoff; browser and legal acceptance remain separate.

## Reissued statutory coverage evidence

PayslipCorrectionEvidenceApi_Tests uses the authenticated onboarding, registration, compensation, attendance, calculation, review, locking and correction endpoints on a fresh PAYROLL_PAYSLIP_CORRECTION_EVIDENCE_TEST_POSTGRES database.
Its synthetic employee has monthly gross of INR 22,000 and an issued above-ceiling source month.
An approved whole-month ForceCovered exception supplies the changed source evidence through the existing exception API.
This tests that API's supported behavior, not legal entitlement to such an exception or Gujarat wage-slip certification.

The reviewed but unapplied correction must not establish continuity in a later payroll.
Applying the correction must supersede the later unlocked preview and reject its saved review and lock request within the contribution period.
Recalculation must then include employee ESI of INR 165, separate employer ESI of INR 715 and the configured INR 200 Professional Tax.
Exact net pay is INR 21,635 and school cost is INR 22,715.
The later payroll must review and lock successfully, with its finalized slip matching that calculation.
An already reviewed preview in the next contribution period must remain current.
Replaying source reissue must return the same replacement, retain two versions and preserve the original PDF checksum.
The capture transport must remain empty.

The test uses the current source month without skipping March or September.
In those boundary months, the immediately following period must remain current and must not inherit coverage; only the calendar branch actually executed supplies acceptance evidence.
All attendance fixtures are prepared before calculation, and the correction uses one valid calculated-to-reviewed transition.
The initial failed expectations omitted Professional Tax and then misstated the sum of INR 200 and INR 165.
Another fixture expectation used HTTP 400 for a stale payroll review; the existing problem-details mapper returns HTTP 409 with PAYROLL_PREVIEW_STALE for both review and locking.
Those fixture failures are retained separately from product defects and final verification.
The owning handoff records the executed result and database rather than inferring success from this test description.
The final formatted-source API run passed one test with no failures or skips in 2.7693 minutes on 2026-10-05.
It exercised October 2026 reissue, November 2026 stale review/lock rejection and recalculated locking, while the reviewed April 2027 preview remained current.
The March/September conditional branch was not executed in that run and is not counted as additional boundary coverage.

## Paid CTC settlement and recovery

PayslipCtcSettlement_Tests covers monthly school budgets of INR 10,000 and INR 4,000 through the authenticated employee, compensation, attendance, payroll, document-payment and correction endpoints.
The lower budget exercises the employee ESI share exemption while retaining employer contributions.
Each theory case requires its own fresh PAYROLL_PAYSLIP_CTC_SETTLEMENT_AMOUNT_TEST_POSTGRES database through the existing guarded, capture-only factory.

The source payroll must reconcile earned gross plus employer contributions to the full-month CTC budget before issuance.
A reasoned paid attendance correction must retain a separate employer delta and the original issued PDF.
Recovery into a zero-pay target must fail without applying the correction, superseding the target or changing the paid original.
Restoring only owned fixture attendance must invalidate the prior calculation and review authority.
The source correction must be recalculated and reviewed, and the stale target must still reject settlement until regenerated.

Two simultaneous settlement requests must apply one source delta through the ordinary target payroll pipeline.
Target net pay and school cost must change by exactly the frozen source differences, while the target's current ESIC wages and coverage remain unchanged.
The regenerated target must review, lock and issue a reconciled canonical document.
A subsequent retry must remain idempotent, with one applied correction, a recorded source document/period and an unchanged paid original checksum.
The capture transport must remain empty.
The final executed result, failed attempts and fixture names belong to the implementation handoff rather than being inferred from this test description.

The issued INR 10,000 CTC settlement PDF exposed the internal automatic-calculation label on an employee ESI refund in its earnings table.
The exact stored bytes reconciled with the API but failed the employee-label extraction check.
The shared PDF component-name formatter now gives ordinary ESI and valid source-month ESI correction lines the same employee label in both tables.
The projection puts the display label into the PDF model used for issuance, and the renderer handles retained raw labels when explicitly generating a replacement.
Frozen salary component facts retain the calculated source names.
Custom component names and malformed source-month names remain unchanged.
This formatting changes neither payroll component evidence nor previously stored document bytes.
Four permanent projection regressions preserve amounts, monthly-rate values, source months and original DTO names.
The read-only PDF probe verifies stored checksums, exact recorded money, A4 pages, employee-only fields and complete accessible text on both CTC sources and targets.
The final formatted-source selection passed both authenticated API cases and twelve layout/projection cases with no failures or skips in 4.6120 minutes on 2026-10-06.
PDF.js5.6.205 on Node20.19.5 verified the four exact stored source/target artifacts against their API evidence after the label fix.
All four issued pages and the separate three-page long-name layout fixture were rendered with Poppler and visually inspected for clipping, overlap, label readability, continuation identity and amount alignment.
The layout fixture is not an issued payroll artifact, and these checks do not constitute browser or legal acceptance.
Fresh Codex browser, native printing and Gujarat legal applicability remain separate acceptance work.

## Empty document sections and stored-artifact qualification

The actual ESIC-off issued PDF reproduced an empty Payment & Statutory panel when neither bank nor statutory details were recorded.
The pre-fix stored-artifact regression failed on that panel while its monetary, identity, date and A4 assertions passed.
The shared renderer now omits the panel when it has no recorded values and labels bank-only and statutory-only panels according to their content.
This changes new document rendering only and does not rewrite immutable stored PDFs or infer missing facts.

PayslipDocumentPayrollMatrix_Tests covers fourteen authenticated payroll scenarios, including ESIC-off payroll with bank details saved through the employee payroll contract and accepted Gujarati employee profile facts.
The off-bank case must preserve the recorded payment fields while omitting ESI amounts and statutory presentation.
Together with the document-facts correction workflow, this exports eighteen issued original/replacement artifacts and their response facts for read-only frontend qualification.
Separately generated renderer fixtures are not counted as issued payroll documents.

The repeatable frontend artifact command and its explicit environment requirement are owned by the [frontend HR testing guide](https://github.com/gyanmirai/Shikshamitra-FE/blob/main/docs/hr/testing.md).
Exact executed test results, source hashes, rendered-page inspection and unexecuted browser acceptance are recorded separately in the implementation handoff.
The old defective PDF remains unchanged as reproduction evidence.
Fresh authenticated Codex browser, native printing and Gujarat legal acceptance remain outstanding.

## Accepted Gujarati profile facts and portable PDF rendering

The fourteenth payroll document matrix scenario updates a synthetic employee's Gujarati name, department and designation through the actual personal-information endpoint before calculation, review, locking and issuance.
The returned facts and stored PDF must preserve that identity and reconcile employee deductions, employer contributions and net pay.
The capture-only facts-correction workflow additionally exports unchanged originals and paid/unpaid replacements.
The complete frontend qualification fixture contains eighteen issued PDF/response pairs, distinct from renderer-only samples.

The original generator lost Gujarati conjuncts as NUL and extracted some vowel marks in visual order, despite rendering the page correctly.
The approved QuestPDF 2026.9.1 upgrade uses embedded Noto Sans Gujarati with bundled Lato and strict missing-font/glyph checks.
`PayslipPdfFormatting.LogicalText` records each original value in a standard tagged span and keeps it on one page.
This prevents page-specific structure trees from repeating a replacement that spans pages.
Previously issued bytes remain immutable, including the defective reproduction artifact.

PayslipPdfLayout_Tests retains raw no-NUL map checks for Latin documents and validates recorded Unicode and exact page/MCID coverage for shaped glyphs in Gujarati documents.
Its negative cases remove the logical value, substitute another employee, change the MCID, or introduce a continued span; each must fail without changing the original PDF.
The separate forty-five-component Gujarati fixture verifies pagination without duplicated logical values.
PDFsharp-MigraDoc 6.1.1 is a test-only parser dependency; product rendering remains QuestPDF.
FinancePdfPortability_Tests covers the affected report generator and all three existing receipt variants with strict validation, Gujarati text and bundled fonts.
These generator cases do not establish authenticated Finance workflow acceptance.
PdfPortability_Tests covers the existing HR, Academic, Library, appraisal and generic document generators with explicit fallback font styles and strict missing-glyph checks.
The existing employee-card generation tests exercise the unchanged SVG path against the upgraded engine.

Run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~PayslipPdfLayout_Tests|FullyQualifiedName~FinancePdfPortability_Tests|FullyQualifiedName~PdfPortability_Tests"` for rendering regressions.

The payslip layout regressions assert exact component paise and matching net-pay words for a paid correction settled in a later payroll.
They also reject stale rounded words supplied by prior document facts when producing an explicit replacement model.
Set PAYROLL_PAYSLIP_MATRIX_TEST_POSTGRES and PAYROLL_PAYSLIP_FACTS_TEST_POSTGRES to separate fresh owned loopback payroll_tests_* databases for the authenticated payroll/document scenarios.
Set PAYSLIP_VERIFY_ARTIFACTS to an ignored output directory; run the API selection separately from renderer fixtures so sample filenames cannot overwrite issued artifacts.
The [frontend qualification guide](https://github.com/gyanmirai/Shikshamitra-FE/blob/main/docs/hr/testing.md) owns exact stored-PDF extraction using PDF.js 5.6.205 on Node 20.
Record exact executed results, failed attempts, fixture names and checksums in the implementation handoff.
Browser, native printing, migration baseline and Gujarat applicability remain independent acceptance checks.

## Framework delivery polling after commit

PayslipBackgroundPolling_Tests starts the real ABP IBackgroundJobWorker against a fresh owned capture-only PostgreSQL fixture.
Set PAYROLL_PAYSLIP_POLLER_TEST_POSTGRES to a separate loopback payroll_tests_* database and run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FullyQualifiedName~PayslipBackgroundPolling_Tests`.
The fixture enables job execution while keeping unrelated periodic workers disabled, then starts and stops only the framework queue poller explicitly.
It does not substitute a direct PayslipDeliveryBackgroundJob call for queue dispatch.

A failed background-job insertion during payroll lock must roll back the issued document and leave the preceding queue unchanged.
Already committed preview jobs can exist in that queue; they are not payslip delivery intent.
A successful reviewed lock must persist a queued delivery with explicit employee-document and tenant identifiers without sending inside the request transaction.
The poller must then record provider acceptance, remove the completed original queue entry and capture exactly one synthetic-recipient attachment whose checksum matches the stored and downloaded PDF.
This supplements the separate actual OS interruption/restart regression; it does not replace process-restart evidence or prove inbox delivery.
Record the executed result and all failed attempts in the implementation handoff.
The formatted-source polling regression passed one test with no failures or skips in 1.6219 minutes on 2026-10-06.
Its first assertion assumed an empty global job queue and failed on a previously committed payroll-preview job; the corrected regression compares preceding queue identifiers and separately rejects payslip-delivery intent after rollback.
Fresh authenticated browser acceptance and current Gujarat applicability remain independent completion gates.

## Controlled employee account email and recipient repair

EmployeeAccountEmailApi_Tests exercises the real authenticated account-email GET/PUT against a fresh capture-only PostgreSQL fixture.
The same regression now drops the first HTTP acknowledgement only after consuming a successful real account-email response.
A subsequent authenticated read proves Employee/Identity changes committed before the transport exception, and an identical payload/key retries safely despite its original concurrency token.
The acknowledgement preserves the committed token/security stamp and leaves exactly one audit event for that request.
This is an authenticated PostgreSQL/TestServer transport-fault check, not a browser navigation or socket/process interruption claim.
Set PAYROLL_REGISTRATION_TEST_POSTGRES to a separate owned loopback payroll_tests_* database and run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FullyQualifiedName~EmployeeAccountEmailApi_Tests`.
Do not share that fixture with PayslipDeliveryLifecycleApi_Tests because both initialize their own test data.

The regression covers joint Employee/Identity changes, email-based and distinct usernames, security-stamp rotation, token and authorization revocation, both exact permissions, direct-service enforcement, tenant and employee scope.
It also covers duplicate addresses, stale edits, verification and reason validation, simultaneous requests, acknowledged replay after a newer edit, audit-failure rollback and successful recovery.
Generic personal-info email writes remain rejected; generic Identity email changes are rejected while an ordinary same-email Identity edit remains allowed.
The fault-injection assertion reads revoked credentials again through the actual managers to detect mutable-cache state leaking across rollback.
The original regression reproduced a revoked cached token after a failed transaction, and a separate authenticated Identity PUT reproduced the linked-email bypass.
Both assertions remain intact after the fixes.

PayslipDeliveryLifecycleApi_Tests repairs an invalid synthetic recipient using the new typed account operation and explicitly retries delivery.
The capture must target the corrected account address with the same immutable PDF checksum; a replay of the failed attempt cannot send, and competing workers cannot duplicate the new attempt.
The expanded account regression and the recipient-repair regression each passed one test with zero skips on 2026-10-06 before the final frozen-source rerun.
These are authenticated API checks, not browser acceptance or inbox-delivery proof.
The final separately rebuilt account regression passed one comprehensive fact with zero skips in 2.6663 minutes.
The full correction/settlement/delivery selection then passed all 52 cases with zero skips in 35.4484 minutes, including the repaired recipient journey and running-worker supersession.
The actual worker restart fact passed separately with four distinct OS process identifiers and no automatic resend of an uncertain attempt.

PayslipDeliveryCurrency_Tests additionally combines the controlled account-email API with actual delivery claims and provider acceptance on PostgreSQL.
Run that class against a separate fresh owned loopback PAYROLL_PAYSLIP_CURRENCY_TEST_POSTGRES fixture.
Its account-change scenarios check queued changes, changes between claim and send, and a provider-first request observed waiting on the real PostgreSQL transaction fence.
The paid original, net pay and PDF checksum must stay unchanged, and competing workers and replayed attempts must not duplicate transport.
The initial separate fact passed with zero skips in 2.3755 minutes on 2026-10-06.
Running two independent factories against that same empty-database fixture then reproduced a table-already-exists failure before the second host could start.
The account-change scenarios now run inside the existing comprehensive fact and reuse its single fixture; neither the original assertions nor the new recipient, blocking, retry and immutable-document assertions are removed.
No existing database reset, schema-drop fallback or production factory change is introduced.
These scenarios are assertions within one comprehensive fact, not independently counted tests.
The integrated comprehensive fact passed with zero skips in 1.6207 minutes on 2026-10-06 after recovering the stopped owned PostgreSQL cluster without resetting its data.
This rerun expands one member of the earlier 52-case selection; it is not 53 disjoint cases or a rerun of every other member.

The opt-in PayslipBrowserFixture_Tests creator attaches to an existing owned disposable payroll_tests_* database without running seeders.
Its new synthetic identity receives the two explicit controlled-email permissions in addition to its existing payroll test permissions.
It never changes an existing user's password, role membership, permissions or scope.
Set PAYSLIP_BROWSER_FIXTURE=create-synthetic-identity and an ignored PAYSLIP_BROWSER_FIXTURE_OUTPUT path only for an authorized local browser fixture.
Fixture creation, API assertions and actual authenticated browser acceptance are separate evidence.
The revised opt-in creator passed one case with zero failures or skips in 22 seconds on 2026-10-06 against the existing owned renderer-matrix fixture after normal cluster recovery.
The ignored result records its credential-file location without printing credential values, and a separate readiness check verifies both controlled-email permissions.
This successful fixture creation does not turn the blocked application/browser startup into an authenticated browser pass.

When the owned browser fixture has no login application, the opt-in `prepare-synthetic-login-client` mode prepares a dedicated confidential application through the supported OpenIddict managers.
Supply `PAYSLIP_BROWSER_CLIENT_ID` with the `payslip-browser-client-` prefix and a newly generated local `PAYSLIP_BROWSER_CLIENT_SECRET` of at least 32 characters.
The mode attaches only through the existing disposable loopback fixture guard, uses the normal password and refresh grants, and never invokes a permission seeder or changes an existing user.
It rejects a replay with a different secret instead of replacing an existing application.
Run the exact method `Prepare_owned_synthetic_login_client_without_reseeding_user_grants` only for an authorized capture-only browser runtime.
Preparation and compilation of this fixture mode do not establish successful browser sign-in.
The exact fixture method passed one case with zero failures or skips on 2026-10-06 against the preserved renderer-matrix database.
Existing users and grants remained unchanged, and no identity was reseeded or reset.

## Current-main migration and process evidence

The current-main reconciliation generated its EF model snapshot with the configured .NET 9 tooling and retained the existing two payslip migrations.
The design-time pending-model check passes without a duplicate reconciliation migration.
The temporary `20261006111009_PayslipSnapshotReconciliation` scaffold is retained only as ignored generation evidence, outside the compiled migrations directory.
The generated snapshot is retained; only `20261003081008_AddPayslipDocumentsAndDelivery` and `20261003093802_AddPayslipCorrectionDrafts` introduce the feature schema.
The fresh-database regression checks the complete compiled migration list and verifies each of the four payslip tables and four salary-slip payment columns is introduced exactly once before applying the chain.
The preserved browser fixture was created from the runtime model and has no EF migration history; running the full chain over its existing tables is unsafe.
Upgrade verification restores its hash-verified pre-main backup into a new owned clone and executes only the established current-main UpOperations, without fabricating history or reseeding identities and grants.
The clone replay preserves seventeen protected payroll/identity tables, and read-only inspection confirms the original fixture already has the upgraded schema with unchanged issued evidence.
`PayslipTimestampMigration_Tests` passed against a fresh owned PostgreSQL database after applying all 111 real migrations.
Its runtime/design-time timestamp discrepancy assertion remains enabled and still reports the unchanged baseline difference; this is not a strict global runtime-model pass.
The focused permission, PDF-layout, portability and mapping selection passed 45 cases with zero failures or skips.
The delivery-worker interruption/restart regression separately passed one case with zero failures or skips on the reconciled source.
Its four actual worker process identifiers were 39408, 41916, 41616 and 37540.
The interrupted claim recovered as uncertain with no automatic resend; one explicit accepted retry captured the unchanged PDF, and a repeated child made no transport call.
The ignored process proof records SHA256 `8C0A803F1E6F8E5FC2369A0DC6BE1F3C6CE8803F3697EDF52617510F66B5A3EB` for the accepted synthetic attachment.
These executed checks do not establish final authenticated browser, native printing, clean locked frontend installation or Gujarat legal-format acceptance.

The reconciled PostgreSQL correction, settlement and delivery selection passed all 52 cases with zero failures or skips on 2026-10-06.
Its twenty classes used twenty-six separate fresh owned loopback fixtures and completed in 27.6121 minutes.
That execution preceded the additive send/history correlation field; the modified delivery-lifecycle fact subsequently passed separately with its request-key assertions intact.
Six additional current-main cases passed for standard and exempt CTC recovery, unproxied service guards, the document payroll matrix, preserved facts and controlled Identity-managed account email.
The real API process separately accepted the existing synthetic identity and returned fourteen renderer-matrix PDFs with hashes matching the saved immutable artifacts.
Four further saved correction PDFs belong to other fixture databases and are qualified separately; their absence from this runtime is not a document-history failure.
These public API reads use normal OpenIddict authorization, not the test authorization handler.

The first live capture-only transport check preserved two explicit attempts as `UNCERTAIN`, with identical request replays returning the same attempt and no captured message.
That first loopback capture server had no SMTP AUTH support, while the configured ABP MailKit path attempts authentication when `UseDefaultCredentials` is false.
Both failed attempts remain preserved; the subsequent authenticated capture verification below uses one deliberate new request.
The worker now records only the exception class when the provider throws, retaining the uncertain outcome.
The document API regression passed with a provider exception containing a protected-details marker and proved that the marker and raw exception object never enter that warning.
This logger regression is distinct from the later successful live SMTP transport check.

The final current-main domain payroll selection passed 105 cases with zero failures or skips.
The application payroll selection initially passed 323 cases and failed one outdated employee/staff message assertion.
The existing resolver correctly threw for missing working units; its production behavior was not changed.
The repaired assertion checks the complete current staff message and employee identifier, and the full 324-case application selection then passed with zero skips.
The final permission-consistency selection separately passed thirteen cases with zero failures or skips.
The read-only migrated-model probe reports 111 applied migrations, none pending and 1,242 unrelated runtime timestamp differences on current main.
The four new payslip tables match the generated snapshot, while the strict global runtime-model check remains unpassed.
No timestamp policy, warning suppression or unrelated migration is introduced.
The local API launcher also leaves the separate startup `Settings:SmtpPassword` key empty to prevent copying a configured credential into the owned fixture; this does not reset its stored setting.

## Authenticated capture-only transport

The approved disposable renderer-matrix tenant received dedicated synthetic SMTP credentials through the normal ABP settings manager.
Global settings, existing identities, grants, employees and issued evidence remained unchanged.
SMTP authentication stayed enabled, and the capture listener remained restricted to `127.0.0.1:2526` and its synthetic recipient allowlist.
Negative protocol checks reject anonymous transport, incorrect credentials, an unrelated sender or recipient, a second recipient and DATA after reset.
No credential values are emitted in the proof or included in the review evidence index.

One deliberate normal OpenIddict-authorized API request reached `PROVIDER_ACCEPTED`, and replaying its persisted request key returned the same attempt with one captured message.
The captured MIME message has one employee recipient, no CC/BCC and one PDF attachment.
The decoded attachment SHA256 equals both the immutable stored PDF and the authenticated API download.
The attachment uses the readable stored period/revision filename; the API's existing document-ID download filename is recorded separately.
DOC-12 requires identical bytes and MAIL-10 requires a readable attachment filename, not identical names across those interfaces.
The verification helper initially confused the request label `OCTOBER-2026` with stored period `2026-10`; those failed probes remain recorded before the corrected read-only document-metadata check.
No issued PDF, payroll facts, production mail configuration or real recipient is changed.
This proves local provider acceptance and artifact correspondence, not inbox receipt or final Codex Send/Resend browser acceptance.

## Current-main completion verification on 7 October

This section records later executed evidence without relabelling the earlier failures or unrun checks above.
The reconciled backend is based on main `0b150cee4ccdb2a3ce85f264c1b73cedf3f83d39`.
The cumulative payslip implementation was preserved during the normal fast-forward synchronization, with protected source and patch backups.
The host build passed with zero errors and 739 existing warnings.
The current-main application payroll selection passed 324 cases with zero failures or skips in 2 minutes 45 seconds.

Before this additive main synchronization, the final payslip implementation rebuilt and passed the complete 20-class, 52-case PostgreSQL acceptance selection on 26 fresh owned loopback fixtures with zero failures or skips.
The source-preservation comparison confirms that those feature rules were unchanged by the later main synchronization.
That earlier execution remains separately identified rather than claimed as a new 52-case run against the latest whole tree.
The ledger also retains 36 PDF/shared-portability, 105 domain payroll and 13 permission cases, and the actual four-process delivery interruption/restart proof.
Counts overlap across selections and are not summed into a fabricated total.

Authenticated Codex workflows completed reasoned unpaid recalculation, cancellation, review and reissue, and a paid October correction applied exactly once in the selected unlocked November payroll.
The recorded October net of INR 9,473.45 plus November net of INR 10,526.55 reconciles to INR 20,000, with employer contributions retained separately in school cost evidence.
Issued source bytes and earlier revisions remain preserved.
The final current-main runtime renders the same November revision 3 with exact paise at all three required widths.
The stored, downloaded and capture-only MIME attachment evidence retains the same immutable 60,352-byte PDF checksum.
Normal synthetic login, local OIDC discovery and loopback runtime guards remain enabled, without reseeding grants or resetting a real identity.

The complete 111-migration chain and preserved-fixture upgrade evidence retain the two original feature migrations and tooling-generated snapshot.
The duplicate snapshot-reconciliation scaffold remains ignored and unapplied.
The strict whole runtime-model gate still fails on 1,242 baseline timestamp differences, with zero feature-table/payment-column differences; no mass timestamp repair or warning suppression was introduced.
The frontend deployment-workflow assertion also remains a separately reported baseline failure.
Native printer selection, physical output and real inbox receipt are not claimed.

The user deferred DOC-15 on 7 October so engineering can finish this iteration.
Current Gujarat applicability, required fields and signature authority for both private unaided and government-aided schools remain unverified follow-up work.
The existing signature placeholder is not a completed signature or legal certification.
The approved controlled Identity-managed account-email workflow remains MAIL-07, with its existing joint concurrency, idempotency, permission, tenant, scope and recipient-currency guards.
No statutory rule, activation restriction or immutable PDF was changed to defer legal review.

## Enrollment and salary document acceptance

Run StatutoryEnrollment_Tests for flags, number formats, independent suppression, school amounts and proration.
Run SalaryDocumentAndVariance_Tests for exact CTC deltas, frozen PT semantics and logo/presentation validation.
Run IndependentEsiBasisPayroll_Tests with a fresh loopback PAYROLL_TEST_POSTGRES database for profile retries, stale writes, locked history and saved-evidence statements/registers.
Run EsiPayrollWorkflowApi_Tests with a separate fresh PAYROLL_REGISTRATION_TEST_POSTGRES database for pending registration, activation, review and lock.
Use synthetic identities and adjusted test dates.
Render Compact/Detailed payslips and both reports through the application PDF services, then inspect the rendered pages.
Apply the migration SQL to a disposable copy of the previous schema and verify known identifier normalization without changing historical money or documents.

## Reporting workspace replacement

Run the current reporting suites and cutover checks documented in [HR reporting workspace](./references/reporting-workspace.md#validation).
The removed Reports Center and schedule tests describe retired behavior and are replaced by template, run, immutable-output, and source-authorization coverage.

Validated locally on 19 October 2026 against backend main `25f619f0`.

| Check | Result |
| --- | --- |
| Reporting workspace API and disposable PostgreSQL tests | 16 passed |
| Reporting engine, salary documents, scope, calendar, and worker application checks | 39 passed |
| PostgreSQL population | Filtering and distinct counts verified over 25,001 synthetic staff records |
| Payroll reconciliation | Issued evidence, saved drafts, component identities, and unchanged official PDF verified |
| Captured output | Source changes and duplicate concurrent export delivery preserve stored rows and download bytes |
| Expiration | Expired result rows and both files removed while private history remains identifiable |
| Restored-database cutover | Legacy template, export, and old job removed; new template and unrelated job retained |
| Protected cutover evidence | Unchanged hashes for 36 employees, 62 attendance rows, 33 payroll snapshots, one salary slip, one official PDF, and two audit events |

The cutover fixture contained no leave requests, and the cleanup does not target leave tables.
No live database cleanup or release was performed.

Broader checks retain confirmed main-branch failures.
The same 19 attendance-register API failures reproduce on unchanged main, with three remaining cases passing in both checkouts.
Seven finance late-fee application failures also reproduce on unchanged main.
The security domain suite passes 54 of 55 cases; its deployment-workflow assertion also fails on unchanged main.
Deployment workflows remain unchanged under the task's explicit scope restriction.

## Salary summary and pending registration regression

SalarySummaryApi_Tests covers Gross and Employer Cost previews, summary parity, pending PF/ESIC identifiers, identifier-only updates, immutable compensation history, malformed months, denied permissions, employee scope and tenant isolation.
StatutoryEnrollment_Tests covers independent application flags and contribution parity with absent identifiers under payable-day proration.
EsiPayrollWorkflowApi_Tests and IndependentEsiBasisPayroll_Tests require distinct fresh disposable PostgreSQL databases and exercise preview, review, lock, contribution continuity and frozen history.

## Complete table export tests

Run HrTableExport_Tests, HrTableExportApi_Tests and the PostgreSQL reporting suite.
Cover source-view access without Export grants, current revocation, owner and tenant isolation, complete populations beyond 20,000 rows and immutable typed downloads.
Cover document checklist pagination, policy filters and performance report scope.

## Named ESIC policy regression checks

Run `EsiPolicy_Tests`, `EsiPolicyCalculation_Tests`, `EsiPolicyRepricing_Tests`, `EsiPolicyApi_Tests` and `SalaryTemplateComposerAppService_Tests`.
Run `EsiPolicyPostgres_Tests` with `PAYROLL_TEST_POSTGRES` pointing to a separate empty loopback `payroll_tests_*` database; the factory refuses to reset existing tables.
Cover tenant policy isolation, selection-only permissions, denied writes, stale concurrency, retries, duplicate dates, archive, missing effective versions and external management.
Verify two employees using different policies, month-boundary version resolution, contribution rounding and protected CTC without mutation of saved evidence.
Apply the EF migration to a disposable PostgreSQL database and verify legacy assignments, ambiguous setup and historical JSON preservation.
Run the existing ESIC domain, enrollment, contribution-period and rounding regression suites alongside these checks.

### Reporting table query coverage

Run `HrReportingTableQuery_Tests`, `HrReportingEngine_Tests`, `HrTableExport_Tests`, `HrReportingWorkspaceApi_Tests` and `HrTableExportApi_Tests`.
Run `HrReportingPostgres_Tests` with a fresh loopback `reporting_tests_*` database.
The chunk-boundary scenario verifies global filtering, stable ordering, filtered paging, complete Excel export and another-user denial for the query endpoint.
