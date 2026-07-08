---
module: hr
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-07-08
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
| Payroll settings | `EarningComponentApi_Tests`, `DeductionComponentApi_Tests`, `PfRuleApi_Tests`, `PayElementApi_Tests`, `HrPolicyApi_Tests` |
| Payroll preview, review, lock, or slips | `RunPayrollApi_Tests`, `SalarySlipApi_Tests`, `AttendanceImportLogsApi_Tests` |
| Attendance and imports | `AttendanceTrackingApi_Tests`, `AttendanceImportLogsApi_Tests`, `AttendancePolicyEvaluationApi_Tests` |
| Leave policies and requests | `LeavePolicyApi_Tests`, `LeaveManagementApi_Tests` |
| Calendar events and holidays | `EventApi_Tests`, `HolidayApi_Tests`, `EventTypeApi_Tests`, `HolidayTypeApi_Tests`, `CalendarViewApi_Tests` |
| Reports | `EmployeeReportApi_Tests` |

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
