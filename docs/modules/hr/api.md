---
module: hr
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-05
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
| ESI statutory scheme | `/api/payroll/statutory-schemes/esi*` | `HR.PayrollManagement.Settings.StatutorySchemes.*` | Implemented |
| Pay elements | `/api/payroll/pay-elements` | Class-level authorization | Implemented |
| Payroll policies | `/api/hr/payroll/policies` | `HR.PayrollManagement.Settings.PayrollPolicies.*` | Implemented |
| One-time salary components | `/api/payroll/employees/{employeeId}/salary-structure` | `HR.Employee.EmployeeList.*` | Implemented |
| Compensation revisions | `/api/payroll/employees/{employeeId}/compensation` | `HR.PayrollManagement.CompensationRevisions.View`, `Manage`, or `ViewAudit` | Implemented |
| Run payroll | `/api/payroll/run` | `HR.PayrollManagement.RunPayroll.*` | Generation-bound review and lock; rollout pending |
| Durable payroll previews | `/api/payroll/run/previews*` | `HR.PayrollManagement.RunPayroll.View` | Additive migration and integration verification pending |
| Salary slips | `/api/payroll/salary-slips` | `HR.PayrollManagement.GenerateSalarySlip.*` | Implemented |
| Payroll attendance imports | `/api/payroll/attendance-imports*` | `HR.PayrollManagement.RunPayroll.View` | Implemented |
| Professional tax | Not documented as HTTP API | Not documented | Partial |

Payroll month parameters use `MMMM-YYYY`, such as `JANUARY-2025`.
Payroll lock is idempotent for an already locked month and returns the documented payroll lock error.
Finalized and Paid salary slips are immutable and are both included in month-lock and dependency-impact checks.
The salary-slip response includes the stored ESI rule, wages, coverage decision, insurance number, contribution period, and effective compensation revision evidence.
These fields describe the finalized snapshot rather than current enrollment or compensation settings.
Saved employee salary-component rows satisfy salary-structure readiness even when optional employee payroll metadata has not been saved.

### Durable preview contract

The synchronous preview and readiness HTTP actions are replaced in the coordinated release.
Do not deploy this contract until the additive migration, PostgreSQL concurrency tests, and paired frontend acceptance pass.

| Operation | Contract |
|---|---|
| `POST /api/payroll/run/previews` | Accepts `month`, optional `academicYearId`, and `refresh`; returns HTTP 202 and the authorized run status |
| `GET /api/payroll/run/previews/{id}` | Returns status, completed employee count, timestamps, freshness, and safe failure information |
| `GET /api/payroll/run/previews/{id}/employees` | Accepts zero-based `page`, `pageSize` from 1 to 100, and trimmed `search`; returns one page plus unfiltered authorized-population readiness |
| Review and lock | Require `previewRunId` and explicit selected `employeeIds`; stale inputs return HTTP 409 with `PAYROLL_PREVIEW_STALE` |

Run reuse requires matching tenant, month, academic year, and server-resolved employee scope.
The worker rechecks the requesting user's active account, permission, and scope.
Generation employee results and the complete summary publish in one transaction.
Input revisions currently use a conservative tenant-wide fence, so an input change can invalidate additional unlocked employees.
Attendance, HR policy/calendar, compensation, employment, enrollment, and manual adjustment writes advance that fence transactionally.
Calculator-generated adjustments are outputs and do not invalidate their own calculation.
Review markers identify the exact generation, and locking retains the revision fence and owner-checked month lease until commit.
Finalized employees are excluded before recalculation.

Salary structure templates are tenant-scoped, versioned calculation definitions.
Exactly one Basic earning is required, while an explicitly designated balancing earning is optional.
Underlying earnings must allocate gross before rounding; `HR:Payroll:SalaryTemplateAllocationMismatch` includes gross, allocated earnings, difference, and a readable explanation.
Salary generation and payroll share `GrossEarningAllocation`, evaluating earning dependencies at decimal precision and distributing fractional paise by largest remainder, with ties ordered by ordinary earnings before Basic and then component ID.
Fixed monthly inputs are normalized to two decimals, attendance is applied once, and deductions use finalized earnings.
Basic at 100 percent with PF does not require Special Allowance, and ESI remains system-managed.
Active versions are immutable and carry a default salary basis, derived protection target, PF applicability, and PF rule.
The calculate endpoint accepts Monthly salary (GROSS) or Monthly cost to school (EMPLOYER_COST), an effective month, and optional employee context.
It returns earnings, employee deductions, employer contributions, ESI coverage and contribution details, statutory references, target variance, warnings, and a formula trace.
`GET /api/payroll/salary-templates/active` exposes only valid active templates to Compensation Manage users.
The settings list returns Active, Needs setup, and Archived templates to Salary Template View users.
`GET /api/payroll/salary-templates/component-catalog` returns active tenant earning and deduction components to Salary Template View users.
`POST /api/payroll/salary-templates` creates a complete active template, and `PUT /api/payroll/salary-templates/{id}` atomically creates the next active immutable version.
`POST /api/payroll/salary-templates/preview` accepts current rules, salary basis, monthly amount, effective month, PF selection, and optional template/version/concurrency context under Salary Templates View permission.
It is employee-independent and does not persist templates, versions, employee structures, or payroll records.
It returns `isSample`, `calculation`, `previewToken`, and `expiresAt`.
Each create/update carries `previewToken`, `sampleMonthlyAmount`, `sampleEffectiveMonth`, an idempotency key, expected active version, and concurrency stamp.
The protected preview expires after 30 minutes and binds tenant, user, normalized inputs, version context, and calculation results.
Save recalculates using current configuration and rejects a missing, changed, mismatched, or expired preview with `SalaryTemplatePreviewRequired`, `SalaryTemplatePreviewChanged`, or `SalaryTemplatePreviewExpired` under the `HR:Payroll:` prefix.
Completed idempotent retries retain their result and require a proof bound to the same identity and inputs, without requiring an unexpired preview.
Name and description changes do not invalidate the calculation proof.
A successful sample establishes validity only for its amount and effective month; employee salary generation still validates actual compensation and eligibility.
The first valid template becomes the tenant default, and archiving the default requires a replacement.
Payroll preview recalculates employee PF from prorated eligible wages and Gujarat professional tax from actual monthly gross.
Salary previews and payroll resolve the same effective-dated ESIC configuration for the selected month.
GET /api/payroll/statutory-schemes/esi/configuration returns the persisted default-on/current configuration, audit history, server current month, and school-wide scope.
Unknown school registration facts remain empty and do not block salary calculations.
PUT /api/payroll/statutory-schemes/esi/configuration saves enabled status, registration facts, and earning-treatment changes with a concurrency stamp and idempotency key.
Saving requires both Edit and Activate permissions plus HR-wide scope.
There are no draft-preview, schedule, or cancellation commands in this configuration flow.
The server applies changes from the current Asia/Kolkata payroll month.
Save shares the payroll input-revision transaction fence with payroll locking.
Identical retries are idempotent; stale edits and changed retry payloads are rejected.
Current/future unlocked snapshots and reviews are invalidated; earlier, locked, and finalized snapshots retain their configuration and figures.
Off mode omits ESI deductions and employer contributions while retaining employee enrollment facts.
ESI uses effective-dated component wage classifications, the employee coverage record, the standard or disability ceiling, contribution-period continuation, and audited overrides.
The wage-mapping catalog includes active master earnings and employee-specific earnings applicable to the selected month so a custom salary line cannot become an unresolvable payroll blocker.
`GET` and `POST /api/payroll/statutory-schemes/esi/employees/{employeeId}/overrides` expose immutable exception history and add permission-controlled exceptions.
ESIC exceptions require EffectiveFrom on the first day and EffectiveTo on the last day of their selected months, inclusive.
Partial-month and reversed ranges fail validation.
When exceptions overlap, the newest applicable coverage or amount exception is authoritative.
Unclassified wages and missing required insurance numbers block payroll lock.
HR supplies registration facts and supported exceptions; eligibility and contribution-period continuation are derived automatically.
Locked salary slips retain the ESI rule version, statutory wages, coverage decision, insurance number, contribution period, employee deduction, and employer contribution.
PwD employer-share relief requires the PwD wage-ceiling category, an effective start date, and a disability evidence reference.
Valid relief moves the employer ESI share to the government contribution ledger and does not reduce the employee deduction.
Invalid PwD relief data is rejected before enrollment is saved.
Recurring employee compensation is exposed as effective-dated immutable Effective and Superseded revisions.
`PUT /api/payroll/employees/{employeeId}/compensation` validates and recalculates the complete salary before saving it directly as an effective revision.
Saving again for the same effective month supersedes the prior effective revision, while future-effective revisions remain scheduled until their month.
The save contract includes template-version provenance, a salary basis of GROSS or EMPLOYER_COST, PF selection, permitted overrides, customization reason, idempotency key, and expected month revision.
Employee payroll and salary-structure update contracts also accept only `GROSS` and `EMPLOYER_COST` when a quote basis is supplied.
`TAKE_HOME` is rejected by contract and domain validation.
Template provenance remains all-or-nothing, and bank or one-time-component updates do not alter saved compensation revisions.
The salary-structure component lists may be empty because recurring earnings belong to the effective compensation revision.
Basic, DA, and ordinary rows may be customized with a reason.
Balancing, PF, professional-tax, and ESI rows cannot be overridden.
Employee enrollment and override endpoints enforce the employee scope in addition to the route permission.
Impact preview returns only employees in the caller's HR scope, while tenant-wide statutory writes and activation require HR-wide scope.
`POST /api/payroll/statutory-schemes/esi/impact-preview` accepts `monthYear`, trimmed employee name or code search, coverage, readiness, enrollment statuses, zero-based page, page size up to 100, one documented sort field, and `asc` or `desc` direction.
Its summary and `canActivate` always cover the complete scoped employee population, while `totalCount` and `items` reflect the active filters and requested page.
Preview issues contain a stable code plus server-authored severity, title, message, resolution target, and affected employee count where applicable.
Coverage decisions contain both the stable decision code and a server-authored display label.
Preview responses include `generatedAtUtc` and a deterministic SHA-256 `previewFingerprint` over activation-relevant rule, establishment, classification, employee, enrollment, override, and calculated result inputs.
`PUT /api/payroll/statutory-schemes/esi/configuration` saves directly with a concurrency stamp and idempotency key; it does not require a client impact-preview fingerprint.
The former establishment activation and standalone classification mutation routes have been removed.
Compensation audit identities, timestamps, legacy workflow details, and calculation snapshots require `HR.PayrollManagement.CompensationRevisions.ViewAudit`.
Payroll resolves the latest effective revision for the month and keeps one-time salary components separate.
Locked payroll at or after the effective month prevents compensation revision changes.
Payroll records quote provenance and blocks locking when the normalized protected amount differs from the saved target.
Resolution requires an explicit compensation change; ESIC configuration never silently reduces established wages.
This follows the wage-protection principle in [Social Security Code section 124](https://labour.gov.in/sites/default/files/ss_code_as_passed_by_lok_sabha.pdf).
The calculation retains the Gujarat profile, employee and employer rates of 0.75% and 3.25%, and upward rupee rounding under [Central Rules 2026 rule 19](https://www.labour.gov.in/static/uploads/2026/05/49aa9b62c2125499c37399b90e969d67.pdf).
Monthly-rated average daily wages use contractual monthly statutory wages divided by 26; the employee share is exempt up to Rs 176 per day, while the employer share remains payable, as described in the [ESIC official report](https://www.esic.gov.in/attachments/publicationfile/b830b6c5a968aae81fdbc3a09e9063fb.pdf).
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

## Gross Salary calculation base

Earning and deduction component create/update requests accept `calculationBase: "GROSS"` for percentage calculations, with the selected component ID omitted.
Use `calculationBase: "COMPONENT"` with a selected component ID for ordinary component references.
Responses return this discriminator so the editor can reopen system-based formulas without inventing a component ID.
Salary-template calculation lines include `formulaValue` and `basisComponentId`, preserved in compensation calculation JSON and consumed during payroll.
Run payroll returns regular `fullGrossSalary` and attendance-adjusted `grossSalary`, plus `additionalEarnings` and `totalEarnings`.
Payslips return `fullGrossSalary`, `grossSalary`, `additionalEarnings`, and the existing `totalEarning`, deduction, and net totals.
Gross is a summary and is never added again to its earning breakdown.

New salary templates default to EMPLOYER_COST.
New setup contracts derive protection from the salary basis and do not accept an independent protection choice.
Salary previews include the applicable ESIC configuration ID, including when calculation is off.
Employee-facing payslips omit employer contributions; school cost reports and stored audit evidence retain them.

## Staff presentation and attendance file headers

HR output headings, export labels, generated attendance templates, onboarding email copy, and permission display labels use Staff terminology.
Existing user data, stored documents, historical audit records, official statutory scheme names, routes, permission keys, enums, database names, and API property keys remain unchanged.
New attendance templates use Staff Code and Staff Name.
CSV and XLSX imports accept either Staff Code or Employee Code and either Staff Name or Employee Name, case-insensitively with the existing whitespace normalization.
Different fields may use mixed aliases.
Both aliases for one field, or repeated copies of one field, are rejected with `HR:Leave:020217`.
Normalization retains the internal `EmployeeCode` and `EmployeeName` row fields and the existing attendance calculations.
A sole-default attendance archive or clear-default rejection includes `reason: sole-default` in business-error data, allowing concise UI guidance without reinterpreting every use of `DefaultAttendancePolicyRequired`.
Removal of the frontend policy lookup page does not remove authenticated effective-policy APIs, impact previews, permissions, grants, or payroll safeguards.
