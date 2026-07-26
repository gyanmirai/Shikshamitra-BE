---
module: hr
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-07-26
owner: backend-team
audience: humans-and-agents
---

# Validate HR backend changes

TL;DR: run the smallest command that proves the changed HR behavior, then run docs validation before handoff.
Use broader API tests when a change touches contracts, permissions, payroll, attendance, leave, or policy resolution.

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

## Focused test map

Use this table to pick focused tests:

| Change area | First backend tests |
|---|---|
| Employee profile, family, documents, status, or reporting | `EmployeeListApi_Tests`, `EmployeePersonalInfoApi_Tests`, `FamilyInfoApi_Tests`, `EmployeeDocumentApi_Tests`, `EmployeeStatusApi_Tests` |
| Employee profile dashboard | `EmployeeDashboardProjection_Tests`, `EmployeeListApi_Tests.Should_Get_Employee_Dashboard_From_Stored_HR_Data`, and the attendance-only resolver regression in `HrPolicyAppService_Tests` |
| Employee onboarding and public submission | `EmployeeApplication_Tests` for the forward-only terminal state graph and token regeneration, `EmployeeApplicationAppService_Tests` for validation, unique invitation numbers, legacy uploads, atomic scope changes, and deferred effects, `EmployeeOnboardingAcademicScopeAssigner_Tests` for the BulkAssign gate, `EmployeeApplicationUploadPolicy_Tests` for strict-new and legacy-stored MIME behavior, `EmployeePersonalInfoAppService_Tests` for explicit role approval and employee-group audiences, `RolesSeederContributor_Tests` for stable role metadata, `EfCoreSampleAppServiceTests` for identity-role metadata round trips, and `EmployeeApplicationPublicApi_Tests` for multipart validation and upload limits |
| Payroll settings | `EarningComponentAppService_Tests` dependency lifecycle tests, `EarningComponentDependencyConcurrency_Tests` two-transaction graph races, `EarningComponentApi_Tests`, `DeductionComponentApi_Tests`, `PfRuleApi_Tests`, `PayElementApi_Tests`, `HrPolicyApi_Tests` |
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
