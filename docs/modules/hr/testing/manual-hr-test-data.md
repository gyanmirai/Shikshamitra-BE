# HR Manual Test Data

This data pack is seeded only when `SeedHrManualTestData=true` for the `DDVS` tenant. It is designed for repeatable HR manual testing after deleting and reseeding the local development database.

## Login Defaults

All HR manual test users created by the seeder use:

- Password: `Temp@123`
- Tenant: `DDVS`

The existing deterministic accounts from `SeedTestLogins=true` are still available, including `admin-ddvs@ddvs.com`, `admin-ops@ddvs.com`, `principal-gseb@ddvs.com`, `principal-cbse@ddvs.com`, `teacher-gseb@ddvs.com`, `clerk-office@ddvs.com`, and `finance-officer@ddvs.com`.

## Tester-Owned Employees

| Employee | Login | Identity Role | Employee Designation | Payroll Scenario | Attendance Scenario | Leave Scenario |
|---|---|---|---|---|---|---|
| Yagnik Vadher 1 | `yagnik.vadher1@ddvs.com` | Principal | Principal | Ready to calculate | Full month present | Approver / HR owner |
| Yagnik Vadher 2 | `yagnik.vadher2@ddvs.com` | Vice Principal | Vice Principal | Ready to calculate | Full month present | Reporting-chain approver |
| Prerna Atre | `prerna.atre@ddvs.com` | HR Manager | HR Manager | Ready to calculate | Full month present | Leave and employee-update owner |
| Parth Ghusar | `parth.ghusar@ddvs.com` | Finance Officer | Finance Officer | Ready to calculate | Full month present | Payroll reviewer |
| Raj Baku | `raj.baku@ddvs.com` | Teacher | Senior Teacher - Mathematics | Ready to calculate | Clean attendance | Baseline teacher row |
| Jignesh Jinjala | `jignesh.jinjala@ddvs.com` | Teacher | Teacher - Science | Ready to calculate | Two half days | Pending casual leave |
| Nikhil Baku | `nikhil.baku@ddvs.com` | Teacher | Teacher - English | Ready to calculate | Approved leave days | Approved sick leave |
| Keyur Sidhupura | `keyur.sidhupura@ddvs.com` | Teacher | Teacher - Computer Science | Ready to calculate with low-attendance issue | Low/missing attendance | Rejected casual leave |
| Mayur Solanki | `mayur.solanki@ddvs.com` | Administrative Manager | Administrative Manager | Ready to calculate | Full month present | Reporting manager scenario |
| Jagruti Vadher | `jagruti.vadher@ddvs.com` | Office Assistant | Office Assistant | Ready to calculate | One LOP/full-day leave | Approved loss-of-pay leave |
| Vishal Makwana | `vishal.makwana@ddvs.com` | IT Administrator | IT Administrator | Ready to calculate | Full month present | Cancelled self-leave |

## Reporting Chain

- Yagnik Vadher 1 is the top-level Principal.
- Yagnik Vadher 2 reports to Yagnik Vadher 1.
- Prerna Atre and Parth Ghusar report to Yagnik Vadher 2.
- Raj Baku reports to Yagnik Vadher 2.
- Jignesh Jinjala, Nikhil Baku, and Keyur Sidhupura report to Raj Baku.
- Mayur Solanki reports to Prerna Atre.
- Jagruti Vadher and Vishal Makwana report to Mayur Solanki.

The always-on HR employee seeder also creates a hierarchy for Employee List testing:

- Hiren Vadher is the top-level Principal and is linked to `principal-gseb@ddvs.com`.
- Jignesh Jinjala reports to Hiren Vadher.
- Akshay Patel and Parth Ghusar report to Jignesh Jinjala.
- Teaching employees report under Akshay Patel or Parth Ghusar.
- Non-teaching employees report under Jignesh Jinjala, Vishal Makwana, or Mayur Solanki.

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

The base HR seeder also creates payroll settings used by payroll previews and settings screens:

- Standard Provident Fund rule (`STD-PF`) with 12% employee and 12% employer contribution
- Professional Tax slabs for male and female employees
- Default day calculation setting using working days

## Payroll Starting State

Payroll is intentionally empty after seeding. The seeder does not create salary slips, finalized slips, payroll run snapshots, locked rows, or reviewed rows for the manual-test employees.

To test payroll manually:

- Choose a completed historical month from the seeded attendance window.
- Open the run-payroll preview to calculate rows from attendance and salary components.
- Mark rows reviewed.
- Run/lock payroll to create finalized salary slips.

Previous-month net-pay comparison appears only after you have manually run and locked a prior month.

## Attendance Data

The seeder creates weekday attendance for the last 10 fully completed calendar months. It excludes the current month and never seeds future attendance dates. For example, if the seeder runs on May 16, 2026, payroll-test attendance is created for July 2025 through April 2026.

| Employee | Attendance Pattern |
|---|---|
| Raj Baku | All weekdays present |
| Jignesh Jinjala | Two half days |
| Nikhil Baku | Two full-day leave records |
| Keyur Sidhupura | Only first three weekdays present; remaining weekdays omitted |
| Jagruti Vadher | One full-day leave / LOP-style day |
| Others | All weekdays present |

All attendance rows include `EducationMediumStageId` so payroll scope filtering includes these employees.

The base HR seed also creates current-month attendance and a few approved leaves for general seeded HR employees. Those rows are for attendance and leave workflow testing only. The tester-owned payroll employees above remain historical-only for payroll testing.

## Leave Data

The seeder uses existing leave types and policies from the base HR seed:

- Casual Leave
- Sick Leave
- Earned Leave
- Loss of Pay
- Maternity Leave
- Paternity Leave

Leave requests are seeded in the most recent completed month so they line up with the historical attendance/payroll test window.

| Employee | Status | Type |
|---|---|---|
| Jignesh Jinjala | Pending | Casual Leave |
| Nikhil Baku | Approved | Sick Leave |
| Keyur Sidhupura | Rejected | Casual Leave |
| Vishal Makwana | Cancelled | Casual Leave |
| Jagruti Vadher | Approved | Loss of Pay |

## Broader Demo Data

The main development seeder also creates supporting data for non-payroll manual testing:

- Institute setup: room types such as Classroom, Lab, Library, Staff Room, and Exam Hall; demo buildings with floors; and rooms with capacity, benches, and feature notes.
- Student data: admitted students for every division in the current academic year, with contact, additional, medical details, and fun-name middle names for parent-style testing. The default is four students per division and can be changed with `StudentSeeder:StudentsPerDivision` or capped with `StudentSeeder:MaxStudents`.
- Academic setup: subjects per education medium, exam types, homework types, assignment types, and homework assignment types for assignment/homework flows.

This supporting data is seeded before the student data so exams, room allocation, timetable, homework, and assignment flows can use the same academic setup.

