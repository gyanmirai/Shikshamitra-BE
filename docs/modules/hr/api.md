---
module: hr
domain: api
content_type: Reference
status: canonical
updated_on: 2026-08-13
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
| Employee profile dashboard | `/api/employees/{id}/dashboard` | Own employee or `HR.Employee.EmployeeList.View` with HR user-scope enforcement |
| Personal info | `/api/employees/{id}/personal-info` | `HR.Employee.EmployeeList.*` |
| Family info | `/api/employees/{id}/family` | `HR.Employee.EmployeeList.*` |
| Additional info | `/api/employees/{id}/additional-info` | `HR.Employee.EmployeeList.*` |
| Work experience | `/api/employees/{id}/work-experience` | `HR.Employee.EmployeeList.*` |
| Employee payroll details | `/api/employees/{id}/payroll` | `HR.Employee.EmployeeList.*` |
| Reporting manager | `/api/employees/reporting-manager` | `HR.Employee.EmployeeList.*` |
| Employee status | `/api/employees/{id}/status` | `HR.Employee.EmployeeList.*` |
| Document settings | `/api/employees/document-config` | `HR.Employee.Setting.DocumentTypes.*` |
| Employee documents | `/api/employees/{id}/documents` | `HR.Employee.EmployeeList.*` |
| Employee onboarding | `/api/employee-applications*` | `HR.Employee.EmployeeOnboarding.*` |
| Public employee application | `/api/public/employee-applications/{token}`, `/api/public/employee-applications/{token}/submit` | Anonymous token |

Employee create and update flows must protect email, mobile, date of birth, date of joining, role, and academic scope consistency.
Identity role DTOs expose `roleAudience` with `Employee`, `Student`, or `Parent`, `employeeGroupAudience` with `Teaching`, `NonTeaching`, or `Both`, and the Boolean `employeeOnboardingAssignable` approval marker.
New or changed employee role assignments return `HR:Employee:010035` unless the role has the Employee audience, `HR:Employee:010036` unless it supports the selected employee group, and `HR:Employee:010038` unless it is explicitly approved for onboarding.
Admin and Super Admin roles remain unassignable to employees even if stale metadata marks them as approved.
An ordinary update may retain an unchanged legacy custom role when its Employee audience and employee-group compatibility remain valid, but changing to that role still requires explicit approval.
Employee creation contracts reject an absent employee group with `Employee group is required.`, and the domain guard uses `HR:Employee:010037` for callers that bypass contract validation.
The versioned role metadata seeder backfills built-in employee-group classifications and onboarding approvals, defaults missing custom employee-group classifications to `Both`, and defaults missing custom onboarding approval to false.
Role audience metadata is stored in identity-role extra properties and requires no database migration.
Status transitions must be explicit and auditable.
An unsubmitted onboarding application may only be rejected or withdrawn, link regeneration is allowed only at Invitation sent, active workflow transitions move forward, and Finalized, Rejected, and Withdrawn are terminal.
Attempts to advance or finalize an unsubmitted application return `HR:Employee:010410`.
`POST /api/employee-applications/{id}/finalize` accepts `academicYearId` and `academicScopes`, requires both for a teaching employee, rejects them for a non-teaching employee, and persists the employee, identity, policies, scopes, organization assignments, and application status in one unit of work.
Profile-picture storage, employee-created publication, and the welcome email run only after that finalization unit of work commits.
Invitation application numbers use the varying sequential-GUID tail and retry only application-number uniqueness collisions in a bounded new transaction.
Public submission uses multipart fields named `data`, `resume`, and `photo`, limits the complete form to 20MB, limits a resume to 10MB, limits a photo to 5MB, and checks each file length before copying it into application memory.
The upload boundary accepts only safe base filenames with matching extension and MIME allowlists, verifies PDF, Office, and image signatures after copying, rejects scriptable SVG or HTML payloads, and persists a server-selected canonical content type.
Previously stored uploads with a blank or `application/octet-stream` content type derive a canonical type from an allowed safe extension, but their bytes must still pass signature validation before download or finalization.
Public profile validation requires Family Caste, requires a family-member name when any family detail is supplied, requires organization, designation, and start date for a non-empty work row, and rejects dates after the current Asia/Kolkata institution date.
The employee profile dashboard returns stored attendance, approved-leave day overlays, leave balance, pending leave requests, and the resolved work-calendar holidays for the academic year that contains the requested date.
Stored attendance wins when an approved leave and an attendance record exist on the same date.
Approved-leave overlays do not create attendance records, and stored attendance alone drives the employee dashboard recorded-day count and percentage.
Dashboard calendar resolution loads only the applicable attendance policy and work calendar, so missing payroll or leave policy configuration does not discard valid attendance calendar data.
The endpoint returns explicit unavailable states when an academic year or leave policy is not configured and never synthesizes sample attendance.
The HR dashboard excludes approved-leave employees without stored attendance from the missing-attendance count and attendance-rate denominator.
Approved half-day leave removes one-half attendance unit but leaves the unrecorded remainder visible as missing attendance.
A stored attendance row suppresses the same-day leave overlay in HR dashboard totals and trend points.

## Attendance and leave API families

Attendance and leave APIs feed payroll, so locks and policy resolution matter:

| Family | Routes | Permission family |
|---|---|---|
| Attendance tracking | `/api/attendance-tracking/*` | `HR.AttendanceAndLeave.AttendanceTracking.*` |
| Manual attendance | `/api/hr/attendance/take-attendance/*` | `HR.AttendanceAndLeave.MarkAttendance.*` |
| Attendance register | `/api/hr/attendance/register*` | `HR.AttendanceAndLeave.AttendanceTracking.*` |
| Attendance import | `/api/attendance-import/*` | `HR.AttendanceAndLeave.ImportAttendance.*` |
| Automated attendance runs | `/api/hr/attendance/automated-runs*` | `HR.AttendanceAndLeave.ImportAttendance.*` |
| Leave policies | `/api/leave-policies`, `/api/hr-leave-types` | `HR.AttendanceAndLeave.Setting.LeavePolicy.*` |
| Leave management | `/api/leave-management*` | `HR.AttendanceAndLeave.LeaveManagement.*` |

Manual attendance reads require `HR.AttendanceAndLeave.MarkAttendance.View`, saves require `.Add`, and day lock or unlock requires `.Delete`.
Tenant and user-scope checks remain mandatory in addition to these endpoint permissions.
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
| Salary structure templates | `/api/payroll/salary-templates*` | `HR.PayrollManagement.Settings.SalaryTemplates.*` | Implemented |
| Gujarat professional-tax rules | `/api/payroll/professional-taxes` | `HR.PayrollManagement.Settings.ProfessionalTax.*` | Implemented |
| Pay elements | `/api/payroll/pay-elements` | Class-level authorization | Implemented |
| Payroll policies | `/api/hr/payroll/policies` | `HR.PayrollManagement.Settings.PayrollPolicies.*` | Implemented |
| Salary structure | `/api/payroll/employees/{employeeId}/salary-structure` | `HR.Employee.EmployeeList.*` | Implemented |
| Run payroll | `/api/payroll/run` | `HR.PayrollManagement.RunPayroll.*` | Implemented |
| Salary slips | `/api/payroll/salary-slips` | `HR.PayrollManagement.GenerateSalarySlip.*` | Implemented |
| Payroll attendance imports | `/api/payroll/attendance-imports*` | `HR.PayrollManagement.RunPayroll.View` | Implemented |
| Professional tax | Not documented as HTTP API | Not documented | Partial |

Payroll month parameters use `MMMM-YYYY`, such as `JANUARY-2025`.
Payroll lock is idempotent for an already locked month and returns the documented payroll lock error.
Finalized and Paid salary slips are immutable and are both included in month-lock and dependency-impact checks.
Saved employee salary-component rows satisfy salary-structure readiness even when optional employee payroll metadata has not been saved.

Salary structure templates are tenant-scoped, versioned smart fillers.
Published versions are immutable, and applying or reapplying a template writes ordinary employee salary-component rows without creating a persistent employee-template assignment.
The calculate endpoint accepts Gross or Take-home basis and returns earnings, employee deductions, employer contributions, statutory references, warnings, and a formula trace.
`GET /api/payroll/salary-templates/published` exposes only the current published versions to payroll-structure users, while the settings list also returns the latest draft and current published version.
`GET /api/payroll/salary-templates/component-catalog` returns active tenant earning and deduction components to Salary Template View users.
Creating a salary template produces a blank draft, and draft updates accept variable component rules keyed by component IDs while the server controls internal dependency codes.
Payroll preview recalculates employee PF from prorated eligible wages and Gujarat professional tax from actual monthly gross.
The selected effective professional-tax slab also controls whether tax is deducted in an employee's employment cessation month.
Employer PF is stored separately and never reduces employee net pay.
Percentage-based earning components with a custom-component base may reference only an active earning component in the current tenant and may not create a self-reference or dependency cycle.
An earning component cannot be deactivated or deleted while an active custom-component percentage component depends on it, and activation revalidates the full dependency chain.
Dependency validation returns `HR:Payroll:030035` for self-reference, `HR:Payroll:030036` for a cycle, `HR:Payroll:030037` for an invalid target, and `HR:Payroll:030038` for an active dependent.

## Policy API families

Policy APIs resolve work calendars, payroll policies, attendance policies, assignments, versions, and impact:

| Family | Routes |
|---|---|
| Work calendar policies | `/api/hr/work-calendar-policies*` |
| Attendance policies | `/api/hr/attendance/policies*` |
| Payroll policies | `/api/hr/payroll/policies*` |
| Leave policies | `/api/leave-policies*` |
| Policy assignments | `/api/hr/policy-assignments*` |
| Effective policies | `/api/hr/employees/{employeeId}/effective-policies?date=YYYY-MM-DD` |
| Holiday impact preview | `/api/hr/policy-holiday-impact-preview*` |
| Policy versions | `/api/hr/{policyType}/policies/{policyId}/versions*` |

Work-calendar effective dates accept years from 1900 through 2100, and a custom weekly-off pattern must leave at least one working day.
Work-calendar, attendance, payroll, and leave policy names are unique only among non-archived policies in the same tenant and academic year, so an archived name can be reused.
Leave policy uniqueness is case-insensitive and is backed by the normalized database key, while the other policy families preserve their existing case-sensitive name contract.
Duplicate active policy names return a stable business error that the frontend can present without exposing database details.
Work-calendar, attendance, and payroll list endpoints accept nullable `isArchived`, and the filter is applied before count and paging.
`GET /api/leave-policies` preserves the existing active-only result when `archivedOnly` is omitted and returns current or archived policy pages when it is explicitly `false` or `true`.
`POST /api/leave-policies/{id}/unarchive` restores an archived leave policy as a non-default draft after checking that its name is still available.
Normal create and update endpoints reject Archived status so callers cannot bypass the dedicated archive permission.
Restore-as-draft, create, clone, update, and unarchive workflows all enforce the same policy-name invariant before and during persistence.

Read [references/policy-architecture.md](./references/policy-architecture.md) before changing policy resolution.

## Calendar and reports API families

Calendar and reports APIs support HR operations and exports:

| Family | Routes | Notes |
|---|---|---|
| Event types | HR calendar event type routes | Used by calendar-backed HR event scenarios |
| Holiday types | HR calendar holiday type routes | Used by calendar-backed holiday scenarios |
| Events | HR calendar event routes | Verify active frontend navigation before treating HR menu access as shipped |
| Holidays | HR calendar holiday routes | Verify active frontend navigation before treating HR menu access as shipped |
| Reports | `/api/hr/reports/*` | Exports must match preview filters |

`GET /api/hr/reports/exports` accepts optional `requestedFrom` and exclusive `requestedTo` timestamps and applies them before the recent-results limit.
PostgreSQL export-history bounds are normalized to UTC before they are sent as `timestamptz` parameters, including unbounded sentinel values.
`DELETE /api/hr/reports/exports/{jobId}` soft-deletes only a failed export owned by the current tenant user and requires `HR.Reports.Delete`.
Every new HR export stores a trusted tenant and UserScope snapshot consisting of tenant-wide access or the resolved medium-stage IDs used by report generation.
Sharing requires `HR.Reports.View` for each recipient and rejects the action when the owner's or any recipient's current tenant UserScope does not cover the complete stored export scope.
Schedule creation and update validate recipient permission and scope, and schedule execution rechecks the owner's report-view permission plus every recipient's permission and scope before queuing the shared export.
Automatic schedule execution uses one transaction per due schedule, pauses permanent authorization or validation failures, and defers transient failures for retry without starving later schedules.
Scheduled email delivery is registered after export and schedule writes, runs only after the owning transaction commits, and remains a best-effort post-commit side effect.
Owner and shared-recipient export list, detail, regenerate, share, and download access recheck the current UserScope against the stored snapshot.
The worker captures and persists the owner's current scope before generating a legacy queued export that has no trusted snapshot.
Previously completed legacy exports without a trusted snapshot may be opened only by a current tenant-wide HR user.
PDF export uses a repeating table header, switches wide reports to landscape A3, limits output to 500 rows, displays a truncation notice, and records the number of rows actually written.
PDF failures store a stable user-facing message while retaining technical exceptions in server logs.

## Error and contract rules

Use business exceptions with stable HR error codes for domain failures.
Use validation errors for request-shape failures.
Use permission failures for unauthorized actions.
Do not use old generated exports as the source for response shape.

## Source inspection rule

Inspect controllers and DTOs when this page does not include enough request or response detail.
