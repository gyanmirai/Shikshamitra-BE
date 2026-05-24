# HR Payroll Chain Functional Test Plan

## Purpose

This plan validates the production payroll chain end to end. Payroll is the final output, but it must be explained by the employee profile, HR policies, attendance, leave, payroll settings, preview snapshot, lock action, and salary slip.

The release target is not a claim of "no bugs". The target is a gated, repeatable test process where payroll cannot ship unless critical calculation, locking, permissions, audit, and UI/API consistency checks pass.

## Integrated Scope

Test these modules as one workflow:

- Employee HR and payroll profile
- Work calendar policy
- Attendance policy
- Payroll policy
- Policy assignment and effective-date resolution
- Attendance records
- Leave records
- Payroll preview
- Payroll review and lock
- Final salary slip
- Payroll-facing attendance import logs

Do not treat these areas as independent CRUD screens for release sign-off. CRUD tests are useful, but production readiness depends on the final salary output matching the source data and policy snapshot.

## Golden Payroll Scenarios

Use the DDVS seeded HR manual test data from `manual-hr-test-data.md`. For each scenario, record employee, payroll month, basis days, payable days, proration factor, full gross, prorated gross, deductions, net pay, policy ids/versions, and expected issue codes.

| Scenario | Seed employee | Required result |
|---|---|---|
| Full month present | Raj Baku | Payable days equal basis days; proration factor is `1`; no low-attendance issue. |
| Two half-days | Jignesh Jinjala | Payable days reduce by `1`; non-temp lines are prorated; temp lines are unchanged. |
| Approved paid leave | Nikhil Baku | Approved paid leave restores payable units according to leave policy. |
| Approved LOP | Jagruti Vadher | LOP reduces payable units and prorates non-temp lines. |
| Missing attendance | Keyur Sidhupura | Missing attendance either reduces payable days or raises the expected payroll issue code. |
| Loan/advance deductions | Vishal Makwana | Deductions appear in preview and final slip with the same amounts. |
| Employee policy override | Any dedicated override employee | Employee assignment wins over shift, role, department, employee-type, and default policies. |
| Denominator modes | Dedicated policy test employee | `working-days`, `total-month-days`, and `manual-days` produce the expected basis days. |

## Functional Coverage

### Employee Setup

- Active employees appear in payroll preview.
- Inactive, left, and retired employees are excluded from payroll preview and lock.
- Missing salary structure is visible as a blocking issue, not a silent zero salary.
- Employee salary structure edits affect preview before lock.
- Employee salary structure edits after lock do not mutate finalized slips.
- Employee-specific policy override wins over default policy resolution.

### Policy And Rules

- Default work calendar, attendance policy, and payroll policy resolve when no assignment exists.
- Assignment specificity order is employee, shift, role, department, employee-type, then default.
- Active overlapping assignments for the same target are rejected.
- Effective-dated assignments affect only payroll months inside the assignment range.
- Archived or inactive policies cannot be assigned.
- Locked payroll impact blocks risky policy changes.
- Policy versions remain available for historical payroll explanation.

### Attendance

- Full-present attendance gives full payable days.
- Half-day records count as `0.5` payable day.
- Full-day leave or absence reduces payable days unless paid leave policy restores units.
- Missing attendance reduces payable days or raises the expected issue code.
- Holiday and weekend handling follows attendance/payroll policy interpretation, not the calendar date alone.
- Manual attendance edits before lock change preview after force refresh.
- Manual attendance edits after lock do not mutate finalized salary slips.

### Leave

- Approved paid leave does not reduce salary when the policy says it is paid.
- Approved loss-of-pay leave reduces payable units.
- Pending, rejected, and cancelled leaves do not count as approved paid leave.
- Leave balance, overlap, past-date, notice-period, and approval-chain validations still pass.
- Reporting-chain approval is enforced; self-approval is rejected.

### Payroll Preview

- API accepts `MMMM-YYYY` payroll month values and rejects invalid formats.
- Preview includes basis days, payable days, proration factor, policy ids/versions, earnings, deductions, gross, and net pay.
- `working-days`, `total-month-days`, and `manual-days` denominator modes calculate expected basis days.
- Non-temporary payroll lines are prorated by the LOP factor.
- Temporary payroll lines are not prorated.
- Percentage components must resolve from the expected dependency before preview totals are calculated.
- Negative or impossible net pay appears as an issue or explicit business failure, not as silent successful payroll.

### Payroll Lock And Salary Slip

- Reviewed and eligible rows can be locked.
- Unreviewed or blocking-problem rows cannot be locked when review gating requires review.
- Re-locking the same month returns the expected already-locked behavior.
- Finalized slips match the locked preview snapshot.
- Finalized slips persist basis days, payable days, proration factor, policy ids/versions, earnings, deductions, gross, and net.
- Finalized slips cannot be regenerated in a way that changes payroll history.
- Attendance import log status becomes `payroll_locked` after finalized slips exist for that payroll month.

### Security And Audit

- Tenant A cannot see or affect Tenant B payroll, policies, employees, attendance, leave, or slips.
- Users without payroll permissions cannot preview, review, lock, or view slips.
- Users without attendance or leave permissions cannot alter payroll source data.
- Payroll lock persists audit/event data sufficient to explain who locked what, when, and for which month.
- Policy version snapshots are visible for payroll history review.

## Automated Test Gates

Run these before release:

```bash
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~PayrollChainFunctional"
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~HR"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier=Critical"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier!=Smoke"
```

Release is blocked if:

- Any critical HR payroll-chain test fails.
- Preview and salary slip totals differ without a documented, approved reason.
- Basis days, payable days, or proration factor cannot be explained from policy and attendance data.
- Tenant isolation or permission isolation fails.
- Payroll lock can be repeated or silently mutated.
- Rounding differs from the calculation core.

## Manual UI Gate

Use DDVS seeded login `admin-ddvs@ddvs.com` / `Temp@123`.

Validate these routes:

- `/hr/employee`
- `/hr/manual-attendance`
- `/hr/attendance-dashboard`
- `/hr/attendance-tracking`
- `/hr/leave-center`
- `/hr/leave-management-setting/leaveType`
- `/hr/leave-management-setting/leavePolicy`
- `/hr/payroll/setting/earning-component`
- `/hr/payroll/setting/deduction-component`
- `/hr/payroll/setting/pf`
- `/hr/payroll/setting/professional-tax`
- `/hr/payroll/run-payroll`
- `/hr/payroll/payslip-generation`

For each golden employee, compare UI totals against API response and the expected manual calculation record. Refreshing, navigating away, and returning must not change values. After lock, payslip values must match the locked preview.

## Acceptance Criteria

- Every golden scenario has documented expected basis days, payable days, gross, deduction, and net salary.
- Automated critical API/application/domain tests pass.
- Manual UI pass is recorded for preview, review, lock, and payslip generation.
- Locked payroll output is reproducible from persisted policy version, attendance snapshot, and salary component data.
- Defects are fixed and retested from a clean DB reset/reseed before production release.

## Business Sign-Off

If statutory payroll rules are not finalized, use the implemented rules as the technical source of truth and mark statutory validation as separate business/legal sign-off. Do not release statutory-sensitive payroll changes without that sign-off.
