# HR Manual Test Data

This data pack is seeded only when `SeedHrManualTestData=true` for the `DDVS` tenant. It is designed for repeatable HR manual testing after deleting and reseeding the local development database.

## Login Defaults

All HR manual test users created by the seeder use:

- Password: `Temp@123`
- Tenant: `DDVS`

The existing deterministic accounts from `SeedTestLogins=true` are still available, including `admin1@gmail.com`, `admin2@ddvs.com`, `principle1@ddvs.com`, `teacher1@ddvs.com`, `clerk1@ddvs.com`, and `finance1@ddvs.com`.

## Tester-Owned Employees

| Employee | Login | Identity Role | Employee Designation | Payroll Scenario | Attendance Scenario | Leave Scenario |
|---|---|---|---|---|---|---|
| Yagnik Vadher 1 | `yagnik.vadher1@ddvs.com` | Principal | Principal | Locked/finalized payslip | Full month present | Approver / HR owner |
| Yagnik Vadher 2 | `yagnik.vadher2@ddvs.com` | Vice Principal | Vice Principal | Locked/finalized payslip | Full month present | Reporting-chain approver |
| Prerna Atre | `prerna.atre@ddvs.com` | HR Manager | HR Manager | Pending payroll row | Full month present | Leave and employee-update owner |
| Parth Ghusar | `parth.ghusar@ddvs.com` | Finance Officer | Finance Officer | Locked/finalized payslip | Full month present | Payroll reviewer |
| Raj Baku | `raj.baku@ddvs.com` | Teacher | Senior Teacher - Mathematics | Reviewed, not locked | Clean attendance | Baseline teacher row |
| Jignesh Jinjala | `jignesh.jinjala@ddvs.com` | Teacher | Teacher - Science | Pending payroll row | Two half days | Pending casual leave |
| Nikhil Baku | `nikhil.baku@ddvs.com` | Teacher | Teacher - English | Reviewed, not locked | Approved leave days | Approved sick leave |
| Keyur Sidhupura | `keyur.sidhupura@ddvs.com` | Teacher | Teacher - Computer Science | Pending payroll row with issue | Low/missing attendance | Rejected casual leave |
| Mayur Solanki | `mayur.solanki@ddvs.com` | Administrative Manager | Administrative Manager | Pending payroll row | Full month present | Reporting manager scenario |
| Jagruti Vadher | `jagruti.vadher@ddvs.com` | Office Assistant | Office Assistant | Reviewed, not locked | One LOP/full-day leave | Approved loss-of-pay leave |
| Vishal Makwana | `vishal.makwana@ddvs.com` | IT Administrator | IT Administrator | Pending payroll row | Full month present | Cancelled self-leave |

## Reporting Chain

- Yagnik Vadher 1 is the top-level Principal.
- Yagnik Vadher 2 reports to Yagnik Vadher 1.
- Prerna Atre and Parth Ghusar report to Yagnik Vadher 2.
- Raj Baku reports to Yagnik Vadher 2.
- Jignesh Jinjala, Nikhil Baku, and Keyur Sidhupura report to Raj Baku.
- Mayur Solanki reports to Prerna Atre.
- Jagruti Vadher and Vishal Makwana report to Mayur Solanki.

## Payroll Structure

Each active tester employee receives bank/payroll details and employee-specific salary components. Earning components are seeded with a `Basic` component name so payroll computation can identify basic salary correctly.

Typical gross salary bands:

| Role Type | Gross Monthly Range |
|---|---:|
| Principal / Vice Principal | Rs. 105,000 - Rs. 120,000 |
| Senior Teacher | Rs. 82,000 |
| Teachers | Rs. 54,000 - Rs. 60,000 |
| HR/Admin/Finance/IT | Rs. 64,000 - Rs. 78,000 |
| Office Assistant | Rs. 30,000 |

Seeded earnings:

- Basic
- House Rent Allowance (HRA)
- Dearness Allowance (DA)
- Transport Allowance
- Medical Allowance
- Special Allowance
- Performance Bonus for Raj Baku

Seeded deductions:

- Provident Fund (PF)
- Professional Tax
- TDS for higher salary employees
- ESI-like deduction for Jagruti Vadher
- Loan and advance deductions for Vishal Makwana

## Payroll Row States

For the current seed month, based on seed-run date:

| State | Employees |
|---|---|
| Pending review | Prerna Atre, Jignesh Jinjala, Keyur Sidhupura, Mayur Solanki, Vishal Makwana |
| Reviewed, not locked | Raj Baku, Nikhil Baku, Jagruti Vadher |
| Locked/finalized | Yagnik Vadher 1, Yagnik Vadher 2, Parth Ghusar |

Previous-month finalized payslips are also seeded for all tester-owned employees so the run-payroll screen can show previous-month net pay comparison.

## Attendance Data

The seeder creates weekday attendance for the full current calendar month, including future weekdays in the current month. This is intentional so the current-month payroll preview has a stable full-month basis during manual testing.

| Employee | Attendance Pattern |
|---|---|
| Raj Baku | All weekdays present |
| Jignesh Jinjala | Two half days |
| Nikhil Baku | Two full-day leave records |
| Keyur Sidhupura | Only first three weekdays present; remaining weekdays omitted |
| Jagruti Vadher | One full-day leave / LOP-style day |
| Others | All weekdays present |

All attendance rows include `EducationMediumStageId` so payroll scope filtering includes these employees.

## Leave Data

The seeder uses existing leave types and policies from the base HR seed:

- Casual Leave
- Sick Leave
- Earned Leave
- Loss of Pay
- Maternity Leave
- Paternity Leave

Current-month leave requests:

| Employee | Status | Type |
|---|---|---|
| Jignesh Jinjala | Pending | Casual Leave |
| Nikhil Baku | Approved | Sick Leave |
| Keyur Sidhupura | Rejected | Casual Leave |
| Vishal Makwana | Cancelled | Casual Leave |
| Jagruti Vadher | Approved | Loss of Pay |

