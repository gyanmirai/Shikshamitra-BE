# HR Multi-Policy Architecture

## Purpose

HR uses a shared calendar for dates, but payroll and attendance interpret those dates differently. A calendar holiday is neutral: it says what date is a holiday or event. Attendance policy decides whether attendance is required and how holiday work is handled. Payroll policy decides whether the date affects pay, payroll basis days, or paid/unpaid treatment.

## Model

| Concept | Owner | Responsibility |
|---|---|---|
| `WorkCalendarPolicy` | HR shared policy | Academic-year work calendar, weekly-off pattern, custom weekly-offs, holiday sync mode |
| `AttendancePolicy` | Attendance module | Punch thresholds, missing punch behavior, late/early rules, overtime, holiday work behavior, payroll integration |
| `PayrollPolicy` | Payroll module | Day calculation mode, Saturday/workday units, manual month days, payroll holiday overrides, locked month settings |
| `HrPolicyAssignment` | HR policy assignment | Effective-dated scoped assignment to attendance and payroll policies for employee, shift, role, department, or employee type |
| `HrPolicyVersion` | HR policy lifecycle | Immutable policy snapshots used for history, compare, restore-as-draft, and activation review |

## API Ownership

Shared work calendar APIs live under HR because both modules consume them:

- `GET/POST /api/hr/work-calendar-policies`
- `GET/PUT/DELETE /api/hr/work-calendar-policies/{id}`

Module-owned policy APIs live under the owning module:

- `GET/POST /api/hr/attendance/policies`
- `GET/PUT/DELETE /api/hr/attendance/policies/{id}`
- `POST /api/hr/attendance/policies/{id}/set-default`
- `GET/POST /api/hr/payroll/policies`
- `GET/PUT/DELETE /api/hr/payroll/policies/{id}`
- `POST /api/hr/payroll/policies/{id}/set-default`

Assignment and resolved-policy APIs live under HR:

- `GET/POST /api/hr/policy-assignments`
- `GET/PUT/DELETE /api/hr/policy-assignments/{id}`
- `GET /api/hr/policy-assignment-preview`
- `GET /api/hr/employees/{employeeId}/effective-policies?date=YYYY-MM-DD`
- `GET /api/hr/policy-holiday-impact-preview?academicYearId=...&payrollPolicyId=...&attendancePolicyId=...`

Policy lifecycle APIs expose immutable versions for each policy type:

- `GET /api/hr/{policyType}/policies/{policyId}/versions`
- `GET /api/hr/{policyType}/policies/{policyId}/versions/{versionId}`
- `GET /api/hr/{policyType}/policies/{policyId}/versions/compare?fromVersionId=...&toVersionId=...`
- `POST /api/hr/{policyType}/policies/{policyId}/versions/{versionId}/restore-draft`

`policyType` accepts `work_calendar`, `payroll`, or `attendance`.

## Resolution Rules

Resolution order is specificity-first:

1. Active employee assignment effective on the requested date.
2. Active shift assignment.
3. Active role assignment.
4. Active department assignment.
5. Active employee-type assignment.
6. Active tenant/academic-year default attendance policy and payroll policy.

Only active policies can be assigned. Active assignments for the same target type/key and academic year cannot overlap by date range. Within the same target type, higher priority wins; ties use the latest effective-from date. The preview APIs return the winning source and resolution steps for skipped candidates.

## Policy Lifecycle

Policy create, edit, clone, restore, set-default, and archive operations capture `HrPolicyVersion` snapshots. Restoring a version always creates a new draft policy; it does not mutate active policy history. Risky changes use impact preview and confirmation tokens before write operations when employees, assignments, payroll snapshots, or attendance evaluations may be affected. Locked payroll impact remains blocking.

## Holiday Sync

`WorkCalendarPolicy.HolidaySyncMode` supports:

- `linked_with_overrides`: follow calendar dates, allow payroll/attendance policy interpretation overrides.
- `snapshot`: copy calendar impact for controlled review before changes apply.
- `independent`: manage policy dates independently.

MVP defaults to `linked_with_overrides`.

## Payroll And Attendance Impact

Payroll owns paid/unpaid and basis-day behavior. Attendance owns work/non-work, comp-off, overtime payout, and punch expectations. This allows the same holiday to be:

- non-working in attendance and paid in payroll;
- required attendance with overtime treatment;
- unpaid or excluded for a part-time employee;
- overridden for one employee through policy assignment.

## Migration Note

Existing single attendance and payroll configuration rows are migrated once into default policies for each academic year. The old configuration APIs, services, seeders, EF mappings, and tests are removed after the migration. New payroll locks and attendance evaluations must resolve policy through `HrPolicyResolver`; historical payroll snapshot policy fields remain nullable only for rows that cannot be resolved during migration.
