# HR Manual Test Plan

This checklist is scoped to the HR module only: Employee, Attendance, Leave Management, Payroll Settings, Run Payroll, and Payslips.

Use the seeded data described in [manual-hr-test-data.md](./manual-hr-test-data.md). For reset instructions, use [manual-hr-reset-and-reseed.md](./manual-hr-reset-and-reseed.md).

## Phase 1: Environment And Seed Verification

1. Reset and reseed the development database with:
   - `SeedTestLogins=true`
   - `SeedHrManualTestData=true`
2. Start backend and frontend against the same local database.
3. Log in as `admin1@gmail.com` / `Temp@123`.
4. Open HR Employee List and verify these employees appear:
   - Yagnik Vadher 1
   - Yagnik Vadher 2
   - Prerna Atre
   - Parth Ghusar
   - Raj Baku
   - Jignesh Jinjala
   - Nikhil Baku
   - Keyur Sidhupura
   - Mayur Solanki
   - Jagruti Vadher
   - Vishal Makwana

Expected result: all tester-owned employees are active, searchable, and have the expected designation and role.

## Phase 2: Employee Module

Test route: `/hr/employee`

Checklist:

- Search by first name, last name, role, and designation.
- Open each tester-owned employee profile.
- Verify personal information, contact information, department, designation, shift, and active status.
- Verify reporting manager chain:
  - Yagnik Vadher 2 reports to Yagnik Vadher 1.
  - Raj Baku reports to Yagnik Vadher 2.
  - Teachers report to Raj Baku.
  - Jagruti Vadher and Vishal Makwana report to Mayur Solanki.
- Open payroll section and verify bank details, PF/UAN/punch IDs, earnings, and deductions.
- Edit a non-critical field for one employee and confirm it persists after refresh.
- Change one non-payroll test employee status to inactive, verify it disappears from payroll preview, then revert before continuing payroll testing.

## Phase 3: Attendance

Test routes:

- `/hr/manual-attendance`
- `/hr/attendance-dashboard`
- `/hr/attendance-tracking` if enabled in navigation

Checklist:

- Select the current seed month.
- Verify Raj Baku has clean present attendance.
- Verify Jignesh Jinjala has two half-day records.
- Verify Nikhil Baku has full-day leave records matching approved leave.
- Verify Keyur Sidhupura has low/missing attendance.
- Verify Jagruti Vadher has one full-day leave / LOP-style day.
- Edit one attendance row for a reviewed-but-unlocked employee, then return to Run Payroll and force refresh if the UI exposes it.

Expected result: attendance changes affect payable days and issue codes in payroll preview.

## Phase 4: Leave Management

Test routes:

- `/hr/leave-center`
- `/hr/leave-management-setting/leaveType`
- `/hr/leave-management-setting/leavePolicy`

Checklist:

- Verify leave types and policies exist for the active academic year.
- Open Leave Center and filter by current month.
- Verify seeded leave states:
  - Jignesh Jinjala: pending casual leave.
  - Nikhil Baku: approved sick leave.
  - Keyur Sidhupura: rejected casual leave with remarks.
  - Vishal Makwana: cancelled casual leave.
  - Jagruti Vadher: approved loss-of-pay leave.
- Log in as a teacher and create a self-leave request.
- Log in as an approver/admin and approve or reject that request.
- Confirm leave balance updates after approval.

Expected result: leave list, calendar/balance views, approvals, rejection remarks, and cancellation states behave consistently.

## Phase 5: Payroll Settings

Test routes:

- `/hr/payroll/setting/earning-component`
- `/hr/payroll/setting/deduction-component`
- `/hr/payroll/setting/payroll-configuration`
- `/hr/payroll/structure`

Checklist:

- Verify earning and deduction master settings load.
- Confirm employee payroll components use `Basic` as the basic salary line.
- Verify day calculation configuration loads and can be saved.
- Confirm saved payroll settings do not break current-month preview.

Expected result: settings pages load, update, and still support payroll preview generation.

## Phase 6: Run Payroll

Test route: `/hr/payroll/run-payroll`

Checklist:

- Select the current seed month.
- Verify row states:
  - Pending: Prerna Atre, Jignesh Jinjala, Keyur Sidhupura, Mayur Solanki, Vishal Makwana.
  - Reviewed: Raj Baku, Nikhil Baku, Jagruti Vadher.
  - Locked: Yagnik Vadher 1, Yagnik Vadher 2, Parth Ghusar.
- Verify payroll issue behavior:
  - Raj Baku should be a clean baseline row.
  - Keyur Sidhupura should show low/missing attendance impact.
  - Nikhil Baku and Jagruti Vadher should show attendance/leave impact.
  - Vishal Makwana should show loan/advance deductions.
- Try locking an unreviewed row and confirm it is blocked.
- Mark a pending row reviewed, then lock it.
- Confirm locked rows cannot be unreviewed.

Expected result: payroll review gating, locking, issue display, payable days, gross pay, deductions, net pay, and previous-month comparison all work.

## Phase 7: Payslips

Test route: `/hr/payroll/payslip-generation`

Checklist:

- Verify finalized payslips exist for:
  - Yagnik Vadher 1
  - Yagnik Vadher 2
  - Parth Ghusar
- Open payslip detail/preview.
- Verify earnings, deductions, net salary, payable days, and month.
- Print/download/email actions can be tested if enabled in the UI.
- After locking another reviewed row from Run Payroll, return to Payslips and confirm the new finalized slip appears.

## Phase 8: Retest Cycle

When manual testing is complete:

1. Record any defects with employee name, route, seed month, and expected vs actual behavior.
2. Stop backend/frontend.
3. Drop and recreate the HR test database.
4. Rerun DbMigrator with the same flags.
5. Repeat the relevant phase from this checklist.

