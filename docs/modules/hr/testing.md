---
module: hr
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-09-17
owner: backend-team
audience: humans-and-agents
---

# Validate HR backend changes

TL;DR: run the smallest command that proves the changed HR behavior, then run docs validation before handoff.
Use broader API tests when a change touches contracts, permissions, payroll, attendance, leave, or policy resolution.

Salary-template coverage must include complete active creation, Needs setup recovery, arbitrary active tenant components, exactly one Basic, zero or one balancing earning, exact rounded gross reconciliation, dependency cycles, cross-tenant component rejection, permission boundaries, concurrency conflicts, idempotent retries, PF defaults, percentage validation, two-decimal midpoint-away-from-zero rounding, PF ceilings, Gujarat professional-tax boundaries, and Gross, Take-home, and Employer Cost solving across discontinuities.
Compensation coverage must prove direct effective saves, effective-month resolution, immutable history, same-month supersession, scheduled future revisions, idempotent retries, controlled overrides, activation readiness, locked-payroll protection, separate one-time lines, and warning-only protected-target drift.
Monthly payroll coverage must prove that PF uses prorated eligible wages, professional tax uses actual monthly Gross without attendance-prorating the slab amount, cessation-month treatment follows the effective professional-tax profile, ESI uses effective coverage and wages, eligible PwD relief is government funded, employer contributions remain outside net pay, unlocked previews refresh, and locked snapshots remain immutable.

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

## Focused test map

Use this table to pick focused tests:

| Change area | First backend tests |
|---|---|
| Employee profile, family, documents, status, or reporting | `EmployeeListApi_Tests`, `EmployeePersonalInfoApi_Tests`, `FamilyInfoApi_Tests`, `EmployeeDocumentApi_Tests`, `EmployeeStatusApi_Tests` |
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

Run `BuiltInGrossSalary_Tests`, `SalaryTemplateCalculator_Tests`, and `GrossSalaryComponentApi_Tests`.
Cover full, half, zero, fractional, and varying salary-basis days; fixed and variable percentage masters; all compensation bases; explicit remainder; invalid references; dependency cycles; and incomplete allocation.
Verify serialized compensation formulas are restored for payroll and copied into payslip line snapshots.
Run `SalarySlipGrossSnapshot_Tests` to verify drafts refresh when full-month amounts or formula inputs change without changing paid amounts.
Cover rounding residue with a zero remainder after proration, and reject earnings that depend on that remainder while allowing deduction references.
Verify a separate bonus leaves regular gross and gross-based Basic unchanged while increasing total earnings and net pay.
In the authenticated browser, create and reopen a Gross-based component, inherit its formula in a template, generate a salary structure, and inspect payroll and payslip summaries.
