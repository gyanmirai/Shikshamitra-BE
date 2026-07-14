---
module: hr
domain: api
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Use HR backend APIs

TL;DR: this is the canonical backend HR API map.
Use it for endpoint families, permissions, request conventions, errors, and known partial surfaces.
Use current controllers and application contracts when route behavior is unclear.
Do not restore raw generated exports under `docs/`.

## Shared request rules

All tenant-scoped HR APIs require authentication and tenant context:

```http
Authorization: Bearer your_access_token_here
__tenant: your_tenant_id_here
```

Mutating endpoints must enforce backend authorization.
Hidden frontend buttons are not a security boundary.

## Employee API families

Employee APIs manage staff identity, profile data, documents, status, reporting, and employee payroll details:

| Family | Routes | Permission family |
|---|---|---|
| Employee list and profile | `/api/employees`, `/api/employees/{id}` | `HR.Employee.EmployeeList.*` |
| Personal info | `/api/employees/{id}/personal-info` | `HR.Employee.EmployeeList.*` |
| Family info | `/api/employees/{id}/family` | `HR.Employee.EmployeeList.*` |
| Additional info | `/api/employees/{id}/additional-info` | `HR.Employee.EmployeeList.*` |
| Work experience | `/api/employees/{id}/work-experience` | `HR.Employee.EmployeeList.*` |
| Employee payroll details | `/api/employees/{id}/payroll` | `HR.Employee.EmployeeList.*` |
| Reporting manager | `/api/employees/reporting-manager` | `HR.Employee.EmployeeList.*` |
| Employee status | `/api/employees/{id}/status` | `HR.Employee.EmployeeList.*` |
| Document settings | `/api/employees/document-config` | `HR.Employee.Setting.DocumentTypes.*` |
| Employee documents | `/api/employees/{id}/documents` | `HR.Employee.EmployeeList.*` |

Employee create and update flows must protect email, mobile, date of birth, date of joining, role, and academic scope consistency.
Status transitions must be explicit and auditable.

## Attendance and leave API families

Attendance and leave APIs feed payroll, so locks and policy resolution matter:

| Family | Routes | Permission family |
|---|---|---|
| Attendance tracking | `/api/attendance-tracking/*` | `HR.AttendanceAndLeave.AttendanceTracking.*` |
| Manual attendance | `/api/hr/attendance/take-attendance/*` | `HR.AttendanceAndLeave.TakeAttendance.*` |
| Attendance register | `/api/hr/attendance/register*` | `HR.AttendanceAndLeave.AttendanceTracking.*` |
| Attendance import | `/api/attendance-import/*` | `HR.AttendanceAndLeave.ImportAttendance.*` |
| Automated attendance runs | `/api/hr/attendance/automated-runs*` | `HR.AttendanceAndLeave.ImportAttendance.*` |
| Leave policies | `/api/leave-policies`, `/api/hr-leave-types` | `HR.AttendanceAndLeave.Setting.LeavePolicy.*` |
| Leave management | `/api/leave-management*` | `HR.AttendanceAndLeave.LeaveManagement.*` |
| Attendance evaluations | `/api/hr/attendance/evaluations*` | `HR.AttendanceAndLeave.Evaluations.*` |

The target leave model uses paid and unpaid rule buckets inside leave policies.
Do not reintroduce removed leave-type behavior without product approval.

## Payroll API families

Payroll APIs manage setup, preview, review, lock, and salary slips:

| Family | Routes | Permission family | Status |
|---|---|---|---|
| Earning components | `/api/payroll/earning-components` | `HR.PayrollManagement.Settings.EarningComponents.*` | Implemented |
| Deduction components | `/api/payroll/deduction-components` | `HR.PayrollManagement.Settings.DeductionComponents.*` | Implemented |
| Provident fund rules | `/api/payroll/pf-rules` | `HR.PayrollManagement.Settings.PfRule.*` | Implemented |
| Pay elements | `/api/payroll/pay-elements` | Class-level authorization | Implemented |
| Payroll policies | `/api/hr/payroll/policies` | `HR.PayrollManagement.Settings.PayrollPolicies.*` | Implemented |
| Salary structure | `/api/payroll/employees/{employeeId}/salary-structure` | `HR.Employee.EmployeeList.*` | Implemented |
| Run payroll | `/api/payroll/run` | `HR.PayrollManagement.RunPayroll.*` | Implemented |
| Salary slips | `/api/payroll/salary-slips` | `HR.PayrollManagement.GenerateSalarySlip.*` | Implemented |
| Payroll attendance imports | `/api/payroll/attendance-imports*` | `HR.PayrollManagement.RunPayroll.View` | Implemented |
| Professional tax | Not documented as HTTP API | Not documented | Partial |

Payroll month parameters use `MMMM-YYYY`, such as `JANUARY-2025`.
Payroll lock is idempotent for an already locked month and returns the documented payroll lock error.

## Policy API families

Policy APIs resolve work calendars, payroll policies, attendance policies, assignments, versions, and impact:

| Family | Routes |
|---|---|
| Work calendar policies | `/api/hr/work-calendar-policies*` |
| Attendance policies | `/api/hr/attendance/policies*` |
| Payroll policies | `/api/hr/payroll/policies*` |
| Policy assignments | `/api/hr/policy-assignments*` |
| Effective policies | `/api/hr/employees/{employeeId}/effective-policies?date=YYYY-MM-DD` |
| Holiday impact preview | `/api/hr/policy-holiday-impact-preview*` |
| Policy versions | `/api/hr/{policyType}/policies/{policyId}/versions*` |

Read [references/policy-architecture.md](./references/policy-architecture.md) before changing policy resolution.

## Calendar and reports API families

Calendar and reports APIs support HR operations and exports:

| Family | Routes | Notes |
|---|---|---|
| Event types | HR calendar event type routes | Used by calendar-backed HR event scenarios |
| Holiday types | HR calendar holiday type routes | Used by calendar-backed holiday scenarios |
| Events | HR calendar event routes | Verify active frontend navigation before treating HR menu access as shipped |
| Holidays | HR calendar holiday routes | Verify active frontend navigation before treating HR menu access as shipped |
| Reports | HR report source, options, template, preview, export, and history routes | Exports must match preview filters |

## Error and contract rules

Use business exceptions with stable HR error codes for domain failures.
Use validation errors for request-shape failures.
Use permission failures for unauthorized actions.
Do not use old generated exports as the source for response shape.

## Source inspection rule

Inspect controllers and DTOs when this page does not include enough request or response detail.
