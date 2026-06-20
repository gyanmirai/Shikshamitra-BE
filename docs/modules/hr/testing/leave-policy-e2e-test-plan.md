# Leave Policy E2E Test Plan

## Coverage Summary

- Backend domain coverage verifies yearly entitlement, monthly proration by eligible months, effective-date limits, employee joining month, and unlimited unpaid accrual semantics.
- Backend application/API coverage verifies Paid and Unpaid policy create/read/list behavior, carry-forward and encashment round-trip, required `paidType`, limited/unlimited balance validation, half-day units, approval-time balance validation, and payroll preview/lock safeguards.
- Frontend coverage verifies Leave Center balance math uses `accruedDays` when present, keeps unlimited unpaid separate, displays finite negative balances, and submits Paid/Unpaid requests without legacy leave-type fields.
- Playwright coverage verifies policy create/edit payloads, Leave Center balance/request flows, and payroll Paid/Unpaid impact preview/review/lock/slip contract paths.

## Automated Commands

Run from `C:\Repo\ShikshaMitra\Shikshamitra-BE`:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~LeaveAccrualCalculatorTests"
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~LeavePolicyAppService_Tests|FullyQualifiedName~LeaveManagementAppService_Tests|FullyQualifiedName~LeaveWorkCalendarUnitService_Tests|FullyQualifiedName~PayrollApprovedLeaveResolver_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~LeavePolicyApi_Tests|FullyQualifiedName~LeaveManagementApi_Tests|FullyQualifiedName~RunPayrollApi_Tests"
```

Expected focused results:

- Domain, application, and HTTP API focused suites pass.

Run from `C:\Repo\ShikshaMitra\Shikshamitra-FE`:

```powershell
npm run ts
npm test -- src/screens/hr/leave-request src/screens/hr/policies
$env:E2E_HR_FULL_CYCLE='1'
$env:E2E_BASE_URL='http://localhost:3000'
$env:E2E_API_URL='https://localhost:44332'
$env:E2E_TENANT='DDVS'
npm run test:e2e -- e2e/hr/hr-leave-policy-leave-center.spec.ts e2e/hr/hr-leave-policy-payroll-impact.spec.ts --project=chromium
```

Expected focused results:

- TypeScript: no errors.
- Vitest: 20 files and 135 tests passed.
- Playwright: setup plus 3 chromium tests passed, 4 total.

## Local DDVS Smoke Checklist

Use generated names with an `LPPR-YYYYMMDD-HHMM` prefix and mutate only the local DDVS tenant.

1. Sign in locally as the seeded HR user.
2. Create or reuse a generated employee linked to the current academic year.
3. Create a generated leave policy with one Paid rule and one Unpaid rule; set it active/default.
4. Open Leave Center and verify the balance tab shows accrued finite Paid balance and unlimited Unpaid balance.
5. Submit one Paid leave request and one Unpaid leave request; confirm request payloads contain `paidType` and no legacy leave-type fields.
6. Approve both requests from approvals.
7. Reopen Leave Center and verify balance/history reflect approved Paid and Unpaid leave.
8. Open payroll preview for the relevant month and verify Paid leave is non-deducting and Unpaid leave affects loss-of-pay preview.
9. Do not lock payroll during smoke; preview is sufficient for repeatability.

## Notes

- `entitledDays` remains the policy entitlement. `accruedDays` is additive and should be used by clients as `accruedDays ?? entitledDays`.
- Unlimited unpaid balances must use `remainingDays = null`.
- Negative balance is finite, not unlimited; balances may go below zero only when the active rule allows negative balance.
- Carry-forward and encashment are covered as policy save/read/audit risk fields. This pass does not introduce year-end carry-forward jobs or encashment payroll posting.
