# HR Production Golden Path

Use this flow on a fresh seeded database to verify the production HR path end to end.

## Seeded Policy Precedence Scenario

The HR seeder should provide default policies for each academic year and demo scoped assignments for current and past payroll testing:

1. Direct employee assignment wins over shift.
2. Shift assignment wins over role.
3. Role assignment wins over department.
4. Department assignment wins over employee type.
5. Employee type assignment wins over academic-year defaults.
6. Academic-year default payroll and attendance policies are the final fallback.

Verify the scenario from `/hr/policies?tab=effective` by changing employee, role, shift, department, employee type, academic year, and date.

## Critical Workflow

1. Open `/hr/policies`.
2. Create or edit a work calendar, payroll policy, and attendance policy.
3. Create scoped policy assignments for employee type, department, role, shift, and one direct employee override.
4. Preview the effective policy for each scenario above.
5. Open `/hr/policies?tab=evaluations`.
6. Preview a past seeded payroll month.
7. Create the monthly attendance evaluation batch.
8. Approve and publish each policy-specific evaluation run.
9. Open `/hr/payroll/run-payroll`.
10. Preview the same month and confirm per-employee warnings match their own attendance policy.
11. Mark valid rows reviewed.
12. Lock payroll with `HR.PayrollManagement.RunPayroll.Lock`.
13. Open audit drawers for policy, assignment, evaluation, and payroll lock.

## Expected Audit Visibility

- Policy, policy version, and scoped assignment audit uses `HR.Policies.ViewAudit`.
- Attendance evaluation audit uses `HR.AttendanceAndLeave.Evaluations.ViewAudit`.
- Payroll lock audit uses `HR.PayrollManagement.RunPayroll.ViewAudit`.

## Production Route Rule

Runtime HR screens must not show frontend hardcoded sample rows as production data. If a screen has no backend contract yet, it should be hidden from navigation or rendered as a disabled/unavailable state.
