---
module: hr
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-25
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

## Employee picker lookup

`GET /api/employees/options` accepts one-based `page` (1 to 1,000,000), `pageSize` (1 to 50, default 25), optional `search`, and optional `status`.
The response contains `items` with `id`, `fullName`, and numeric `status`, plus `hasMore`.
Name, employee-code, email, and phone searches use the directory's existing search semantics.
The service applies the same authorization and visibility filters before paging, orders by first name, last name, and ID, and reads at most one extra row to determine `hasMore`.
The lookup does not calculate a total count or enrich organization, role, or shift details.
Payroll Structure filters active staff on the server; Payslips leaves status unfiltered for historical staff.
Deploy this backend endpoint before the frontend picker changes.

## Employee API families

`GET /api/employees/reporting-manager/options?search=...` requires Staff View and returns existing visible active employees plus active, unlocked, current-tenant administrator logins without an employee profile. Administrator eligibility uses current database membership in the configured administrator roles. Linked employee profiles retain the directory's active/status and joining-date rules. No role, scope, or permission is granted by this lookup.

`PUT /api/employees/{employeeId}/reporting-manager` still requires a nonempty `reportingManagerId` and Staff Edit with employee scope. `reportingManagerKind` is `Employee` (default for existing clients) or `TenantAdministrator`. Employee IDs persist in `ReportingManagerId`; administrator login IDs persist in the separate `ReportingManagerUserId` identity foreign key. The server rechecks administrator eligibility at save, rejects self-reference, and clears the other reference on a swap. GET returns the saved manager's `id` and `kind`; existing inactive assignments remain readable. Staff-only reporting hierarchy, scope inheritance, leave and review access rules are unchanged.

Apply `AddAdministratorReportingManager` before starting the updated backend, then deploy the frontend selector. The additive nullable column, restricted identity foreign key and exclusive-reference constraint preserve existing employee-manager relationships. No bootstrap backfill, synthetic employee profile, or tenant reseed is needed.

Employee APIs manage staff identity, profile data, documents, status, reporting, and employee payroll details:

| Family | Routes | Permission family |
|---|---|---|
| Employee list and profile | `/api/employees`, `/api/employees/{id}` | `HR.Employee.EmployeeList.*` |
| Employee picker options | `GET /api/employees/options` | `HR.Employee.EmployeeList.View`, same employee visibility and tenant filters as the directory |
| Staff role options | `GET /api/employees/assignable-roles` | `HR.Employee.EmployeeList.Add` OR `HR.Employee.EmployeeList.Edit` |
| Employee profile dashboard | `/api/employees/{id}/dashboard` | Own employee or `HR.Employee.EmployeeList.View` with HR user-scope enforcement |
| Personal info | `/api/employees/{id}/personal-info` | `HR.Employee.EmployeeList.*` |
| Controlled account email | GET/PUT `/api/employees/{id}/account-email` | `HR.Employee.EmployeeList.Edit` AND `AbpIdentity.Users.Update`, employee scope |
| Family info | GET/PUT `/api/employees/{id}/family-info` | `HR.Employee.EmployeeList.View` / `.Edit`, employee scope |
| Academic permission | GET/PUT `/api/employees/{id}/academic-permission` | Staff View/Edit and employee access; bounded creation delegation described below |
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

Staff Academic Permission reads use `academicYearId` and return the existing scope hierarchy shape.
PUT accepts `BulkScopeAssignmentDto`, with exactly the employee's linked user ID.
Existing-account scope management requires `ShikshaMitra.UserScope.BulkAssign`.

- Without that global grant, Staff Add/Edit may complete only the caller's inactive, never-transitioned staff draft and caller-created identity, with no other employee sharing that identity.
- Every submitted branch must fit within the caller's live academic scope. Ancestor IDs from a narrower assignment never authorize its parent subtree.
- Existing direct assignments must have this workflow's description and creator. Manual assignments remain protected.
- The target year's replacement, organization synchronization and cache invalidation share the existing transactional writer.

Global User Scope APIs retain their own View/BulkAssign checks. No role grants or scope rows are reseeded.

Before academic assignment, profile reads and edits may access the caller's own newly created staff draft.
The persisted `CreatorId` must match, status must be Inactive with no status-change history, and the employee must have no organization assignments, including deleted assignments.
The caller still needs the endpoint's action permission, current Staff Add, and at least one valid accessible HR stage. Missing or revoked scope denies access.
Organization assignment or any status transition ends this draft exception; normal employee scope applies. Payroll and other module-specific scope checks do not use it.
Family reads and writes enforce employee access in the application service as well as the controller.
The staff role lookup returns `items` containing only `id`, `name`, `roleAudience`, `employeeGroupAudience`, and `employeeOnboardingAssignable` for explicitly approved Employee-audience roles in the current tenant.
Reserved Admin, Super Admin, Student, and Parent names are excluded; Identity role-management APIs retain their own permissions.
Creating personal information with `policyOverride` requires `HR.Policies.Assign`; the onboarding default-policy confirmation marker also requires `HR.Policies.PreviewImpact`.
The common creation workflow checks these existing policy actions before creating an identity or staff record, and returns HTTP 403 with `errorCode: HR:Employee:010043` and `data.missingPermissions` when access is missing.
Creating personal information without an override keeps its existing Staff Add permission contract.
Policy list reads retain `HR.Policies.View` for attendance/payroll and `HR.AttendanceAndLeave.Setting.LeavePolicy.View` for leave; Staff Add/Edit does not silently grant policy administration access.
Generic personal-information updates still reject email changes with HR:Employee:010034.
The controlled account-email GET returns userId, employeeEmail, accountEmail, userName and a concurrencyToken covering both records.
PUT requires email, a 5-512 character reason, confirmEmployeeVerified=true, that concurrencyToken and a non-empty idempotencyKey.
It validates the unique same-tenant employee-account link and rejects duplicate employee or Identity addresses and duplicate email-based usernames.
An email-based username follows the new email; a distinct username remains unchanged.
Identity validation, security-stamp rotation, persisted OAuth credential revocation, employee persistence and the HR audit event share one transaction and the delivery fence.
Request replay acknowledges its original write without restoring an old address after a later change.
HR:Employee:010039 means a stale edit or mismatched request replay; 010040 means an invalid account link; 010041 means the address or sign-in name is in use; 010042 means Identity rejected the change.
Current host validation and self-contained bearer-token expiry remain unchanged.
No new database table or schema migration is required.
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

### Application finalization salary contract

`POST /api/employee-applications/{id}/finalize` requires `salary` using the existing `SaveEmployeeCompensationDto` contract.
The optional `statutoryProfile` is forwarded to the canonical compensation save as its statutory profile override.
Existing compensation Manage, statutory enrollment, employee scope, template, PF/ESIC, validation, and effective-month rules still apply.
The saved salary must cover the employee's required activation month.
The application ID supplies the stable initial salary idempotency key; concurrent submissions and response-loss retries return the same finalized employee.
The response includes `activationReadiness`, including the persisted `compensationRevisionId` and remaining blocker codes.
Employee status remains Inactive; activation remains a separate authorized status transition and login does not change employee status.
Deploy this backend contract before the corresponding frontend onboarding change.

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
Basic at 100 percent with PF does not require Special Allowance; ESIC is calculated from the employee assignment and effective policy when managed in ShikshaMitra.
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
Unclassified wages block statutory-mode payroll lock.
Missing registration numbers produce follow-up warnings without suppressing eligible employee or employer contributions.
Disabled Apply flags suppress the corresponding contributions.
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
Configuration save and draft-preview validation reject school employee/employer rate overrides, including documented overrides.
Contribution rates come from the effective statutory policy; component classification continues through the existing effective-dated configuration command.
Compensation audit identities, timestamps, legacy workflow details, and calculation snapshots require `HR.PayrollManagement.CompensationRevisions.ViewAudit`.
Payroll resolves the latest effective revision for the month and keeps one-time salary components separate.
Locked payroll at or after the effective month prevents compensation revision changes.
Payroll records quote provenance and blocks locking when the normalized protected amount differs from the saved target.
Gross-protected salaries retain agreed gross.
Employer-cost assignments reuse their immutable source template and saved overrides to rebalance recurring earnings when the selected ESIC policy version changes, without rewriting salary revisions or calculation snapshots.
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

The report workspace uses the capability catalogue, versioned library, designer preview, private runs, captured rows, and captured-result exports under this route family.
See [HR reporting workspace](./references/reporting-workspace.md#interfaces) for the current endpoint contract and source authorization rules.
The narrow salary-document adapter retains preview/export routes for existing payroll screens and requires the salary-slip source permission.
Schedules, delivery, AI planning, and result sharing are removed.

## Operational HR dashboard

`GET /api/hr/dashboard/overview` requires `HR.Dashboard.View` and returns generation time, current IST `asOfDate`, capabilities, explicit section states, typed workforce, leave, onboarding and appraisal summaries, seven-day attendance counts, and saved payroll progress.
`GET /api/hr/dashboard/staff` additionally requires `HR.AttendanceAndLeave.AttendanceTracking.View` and returns matching staff with bounded server pagination.
Both reads enforce the current tenant and persisted UserScope before serialization.
Attendance aggregates require `HR.AttendanceAndLeave.Dashboard.View`, employee counts and profile actions require `HR.Employee.EmployeeList.View`, leave counts require `HR.LeaveManagement.LeaveCenter.View` with the existing own/reporting hierarchy boundary, and payroll counts require `HR.PayrollManagement.Dashboard.View`.
Mark attendance additionally requires its existing View and Add grants, and opening Run Payroll requires its View grant.

Overview accepts ISO `date` for attendance only.
Staff reads additionally accept optional `isTeaching`, `educationMediumStageId`, `department`, and `shiftId`.
Date defaults to today in IST and rejects future dates.
Staff reads additionally accept name/code `search`, `status`, `sorting` of `name`, `name desc`, `status`, or `status desc`, nonnegative `skipCount`, and `maxResultCount` from 1 to 100.
Name ordering uses employee ID as the final stable tie-breaker.
Staff search and status selection narrow only the separate staff response.
Overview attendance covers the whole authorized daily population without staff filters.
`Review` filters secondary review flags without inventing another primary attendance status.

Workforce counts eligible current staff with teaching/non-teaching totals and joins from the current IST month.
Leave counts current pending requests using the existing approval hierarchy inside employee scope.
Onboarding requires `HR.Employee.EmployeeOnboarding.View` and HR-wide scope because pending applications have no reliable school/stage ownership.
Restricted users receive Unavailable, a safe explanation, and `canOpenOnboarding=false` rather than an unscoped count or shortcut.
Onboarding counts ReadyForOnboarding separately from Applied, Screening, Interviewing, and Offered applications; invitations and completed/rejected applications are excluded.
Appraisals require `HR.PerformanceManagement.View` or its existing `HR.ProfessionalManagement.View` alias.
The count includes distinct unfinished assignments within employee scope in Published, InProgress, or Calibration cycles.
An assignment is overdue once if any unfinished stage deadline has passed, and the next deadline is the earliest remaining deadline on or after the current IST date.
Payroll uses saved snapshots for the current IST month, regardless of the attendance date.
Each module failure produces a Failed state with a null payload without blocking independent sections.
Review lists, cycle review lists, details, employee history, evidence, observations, feedback, audit drill-down and review PDF reads enforce canonical employee UserScope.
Missing scope is denied, and an out-of-scope assignment cannot be opened by supplying its identifier.
Scoped audit reads include only authorized review events, while HR-wide audit access retains tenant-wide events.
Final scores, ratings, and outcomes require the existing confidential-outcome grant or its legacy alias in both list and detail responses.

| Primary status | Operational definition |
|---|---|
| Present | Recorded present, including a working exception on a non-working day |
| Absent | Recorded full-day absence without covering approved full-day leave |
| OnLeave | Approved full-day leave without contradictory present or half-day attendance |
| HalfDay | Recorded half-day attendance, with approved half-day leave shown separately |
| NotMarked | Required working attendance remains unrecorded or unresolved |
| NotScheduled | Configured holiday or weekly off without a recorded working exception |
| NeedsSetup | Missing or ambiguous calendar or policy prevents classification of an unrecorded day |

Counts, staff rows, filters, and trends share one classification and eligible employee population.
Approved full-day leave converts a recorded absence to OnLeave, while recorded presence or half-day attendance is preserved and flagged if it conflicts with full-day leave.
Incomplete imported or integrated punches never establish absence, and unmarked attendance never becomes confirmed absence.
Joining and effective employment-status dates constrain each daily population.
Effective attendance assignment precedence, group membership, work-calendar holidays, and configured weekly offs determine scheduling through batch reads.
Unavailable and failed sections use explicit states and nullable payloads rather than zero counts.
The read model excludes leave reasons, salary, banking information, and documents.
Reads do not create payroll previews or alter attendance, leave, or audit records.

The combined `/api/hr/dashboard/summary` route is retired, and Main preview reads and refresh requests are rejected.
Scheduled refreshes ignore existing Main snapshots and create only Attendance and Payroll previews.
Existing snapshot history remains stored, and separate attendance and payroll preview workflows remain supported.

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

## Payslip documents and distribution

All endpoints below require authentication, the exact payslip action permission and employee scope where relevant.
School settings writes additionally require HR-wide school scope.
There is no public document URL or arbitrary email-recipient override.

| Endpoint | Permission | Contract |
| --- | --- | --- |
| GET /api/payroll/payslip-documents?employeeId=&monthYear= | GenerateSalarySlip.View | Revision metadata, current marker, checksum, replacement reason/relation, recorded employee email and separate payment state. |
| GET /api/payroll/payslip-documents/{id}/pdf | GenerateSalarySlip.View | Exact immutable PDF bytes for the requested revision, including a replaced revision. |
| POST /api/payroll/payslip-documents/payment | GenerateSalarySlip.Add | DocumentId, IdempotencyKey, PaidAt and Reference; current finalized revision only. |
| POST /api/payroll/payslip-documents/replace-facts | GenerateSalarySlip.Add | DocumentId, IdempotencyKey and Reason; preserves payroll amounts and returns replacement metadata. |
| GET /api/payroll/payslip-deliveries?salarySlipId= | GenerateSalarySlip.View | Delivery history across all issued revisions, with recipient, actor, times, status and safe error category. |
| POST /api/payroll/payslip-deliveries | GenerateSalarySlip.Add | DocumentId and IdempotencyKey; queues the current official document for its employee. |
| GET /api/payroll/payslip-settings | GenerateSalarySlip.View | SchoolName, SchoolAddress, AutomaticEmail, ConcurrencyStamp and last IdempotencyKey. |
| POST /api/payroll/payslip-settings/preview | GenerateSalarySlip.View | Accepts validated `SalaryDocumentPresentation` and returns a no-store A4 PDF using the issuance renderer. Reads current tenant school identity/logo; ignores client logo references. Fixed, explicitly synthetic employee/payroll facts only; no employee lookup, settings persistence, document issuance or delivery. |
| PUT /api/payroll/payslip-settings | GenerateSalarySlip.Add | Presentation, AutomaticEmail, ConcurrencyStamp and IdempotencyKey; optimistic concurrency and idempotent retries under the payroll fence. Incoming school details are ignored. |

These short permission suffixes refer to the existing HR payroll payslip permission catalogue, not new grants.
Background delivery from payroll locking rechecks the recorded RunPayroll.Lock action, so an approver does not need RunPayroll.Add to distribute an already reviewed payroll.
Manual Send/Resend and direct payslip issuance retain GenerateSalarySlip.Add; delivery still revalidates the actor, tenant, employee scope, current revision and recipient.
A retried Send request returns the same attempt; deliberate resend uses a new key.
Send and history responses include the persisted IdempotencyKey for request correlation.
The key does not grant access; existing action, tenant, employee-scope and current-document checks remain authoritative.
Delivery statuses are QUEUED, PROCESSING, PROVIDER_ACCEPTED, FAILED, UNCERTAIN and SUPERSEDED.
Delivery responses include RequestedByDisplayName resolved from the requesting account within the current school tenant, with full name preferred over username.
RequestedBy remains the persisted audit identifier; the display name is current account metadata, not a frozen historical name.
Missing, deleted or foreign-tenant accounts yield a null display name; the client cannot supply or override it, and history resolution batches the required identities after the existing permission and employee-scope checks.
UNCERTAIN requires checking receipt before deliberate resend because SMTP cannot guarantee exactly-once delivery.
Reasoned monetary corrections use the following typed endpoints.
Every operation also checks employee scope and tenant ownership.

| Endpoint | Permissions | Contract |
| --- | --- | --- |
| GET /api/payroll/payslip-corrections?documentId= | GenerateSalarySlip.View and RunPayroll.View | Correction history with source, reason, status, attendance, original totals, candidate and concurrency stamp. |
| POST /api/payroll/payslip-corrections | Read permissions, GenerateSalarySlip.Add and RunPayroll.Adjustments | DocumentId, IdempotencyKey and Reason; one active correction per salary slip. |
| POST /api/payroll/payslip-corrections/{id}/calculate | Same edit permissions | ConcurrencyStamp and source-month Attendance; calculates without changing issued payroll. |
| POST /api/payroll/payslip-corrections/{id}/review | Edit permissions and RunPayroll.Add | ConcurrencyStamp; revalidates current inputs, statutory evidence and blocking issues. |
| POST /api/payroll/payslip-corrections/{id}/cancel | Edit permissions | ConcurrencyStamp; retains the cancelled evidence and supports repeated cancellation. |
| POST /api/payroll/payslip-corrections/{id}/apply | Edit permissions and RunPayroll.Lock | ConcurrencyStamp; rechecks the calculation, replaces unpaid payroll and issues the next revision atomically. |
| POST /api/payroll/payslip-corrections/{id}/settle | Edit permissions and RunPayroll.Lock | ConcurrencyStamp, TargetMonth (yyyy-MM) and PreviewRunId; commits a reviewed paid-source difference to a calculated later unlocked payroll and invalidates that target for regeneration. |

Correction attendance uses ISO calendar dates, statuses Present (0), HalfDayLeave (1) and FullDayLeave (2), and optional offset-aware TimeIn/TimeOut.
Duplicate dates, dates outside the source month and undefined statuses are rejected.
Supplied punches use attendance's offset-zero clock contract and must fall on the attendance date when read as UTC.
TimeOut must be after TimeIn when both are supplied, except the supported FullDayLeave midnight pair.
Status-only records and individual missing punches remain supported; invalid supplied timestamps return HR:Attendance:020313 with the attendanceDate and field.
Calculation, review, reissue and paid settlement share this invariant through the correction calculation pipeline.
Apply accepts unpaid finalized payroll only.
Settle accepts paid source payroll only and retains its issued artifact and monetary snapshot.
The target preview must be successful, current, contain the employee and belong to the selected later month.
The employee must still belong to that payroll, and the target employee-month must not be locked, finalized or paid.
The target must be recalculated and reviewed before locking after settlement.
Repeated settlement of the same correction in the same month returns its existing outcome; retargeting an applied correction is rejected.
Signed component differences retain source document, correction and source-period evidence in the calculated target components.
Payment and document-detail replacement return HR:Payroll:030048 while a correction is pending.

## Staff payroll profile details

`PATCH /api/employees/{employeeId}/payroll/profile-details` accepts the typed `EmployeePayrollProfileDetailsPatch` and returns 204.
It requires both `HR.Employee.EmployeeList.Edit` and `HR.PayrollManagement.PayrollStructure.Add`, plus employee modify scope within the current tenant.
The application service repeats the employee-scope check for direct service callers.
At least one non-null group is required: `bankDetails`, `statutoryIdentifiers`, or `attendanceIntegration`.
Omitted groups remain unchanged; supplied groups replace their fields after validation in a single unit of work.
Bank fields use the existing bank validator; identifiers and attendance fields use the dedicated patch validator.
Null or empty optional identifiers clear those fields, and working hours accept null or a value from 0 through 744.
Unknown top-level properties are rejected rather than silently ignored.
This endpoint cannot change compensation revisions, salary components, salary-template metadata, PF calculation rules, or policy assignments.
Stored temporary components and historical finalized payroll remain unchanged.
The write refreshes the employee cache and invalidates unlocked payroll reviews using the existing payroll-details behavior.
Salary and policy changes retain their separate APIs and permission requirements.

Contract and security coverage is in `test/ShikshaMitra.HttpApi.Tests/HR/Employees/EmployeePayrollApi_Tests.cs`, in the ProfileDetailsPatch tests.

Compensation revision reads expose `estimatedStatutoryDeductions` from the saved calculation snapshot for display alongside persisted recurring component lines.
These PF, professional-tax, and ESI estimates do not include employer contributions and are never copied into manual salary components.

## PF enrollment and ESIC calculation settings

Gross and CTC retain their existing single monthly amount input and calculation contracts.
Salary calculation and compensation save accept `pfEnrollment` with `membership` (`UNKNOWN`, `EXISTING_MEMBER`, or `NOT_MEMBER`) and an optional `consentReference`.
Both requests require an explicit Boolean `pfApplicable`; omitting it returns a validation error rather than silently opting the staff out.
Responses expose `pfEligibility` with status, full-month `grossSalary`, Basic plus Dearness Allowance `wages` for contributions, the enrollment ceiling, enrollment requirement, save readiness, and issue code.
HR records the declaration through the existing scoped compensation permissions; there is no employee self-service or new approval workflow.
Existing membership continues above the ceiling; unknown membership or school coverage requires review, and voluntary enrollment above the ceiling requires consent evidence.
The salary-setup policy defaults Apply PF on up to and including INR 25,000 full-month Gross and requires an explicit choice above that amount.
An authorized administrator can disable PF below the threshold or for an existing member with a recorded reason; underlying eligibility and membership evidence remain intact.
CTC inputs use the calculated Gross rather than the quoted employer cost, and attendance reductions do not change the enrollment decision.
This school enrollment policy does not change the statutory contribution wage basis, percentages, caps, or existing-member continuity.
Salary calculation and payroll readiness use the same gross-based decision.
Previously saved compensation decisions and locked payroll snapshots retain their original values; older decisions without `grossSalary` remain readable without backfilling history.
An assigned PF rule requires a positive full-month PF wage base; there is no fallback to Gross.
Employee and employer PF amounts round independently to the nearest whole rupee, with midpoint rounding away from zero.

`GET /api/payroll/pf-rules/coverage` requires `HR.PayrollManagement.Settings.PfRule.View` and returns immutable configuration history and `hasSchoolWideScope`.
`PUT` requires `HR.PayrollManagement.Settings.PfRule.Add` and school-wide UserScope.
Its payload contains `effectiveMonth` (first day of the current or a future month), `isCovered`, `evidenceReference`, `expectedConfigurationId`, and `idempotencyKey`.
Identical retries reuse the saved outcome; stale version IDs and changed retry payloads return 409.

ESIC configuration save accepts `effectiveMonth` (`yyyy-MM`) and `calculationSettings` in addition to the existing configuration fields.
`eligibilityBasis` and `calculationBasis` independently select `CLASSIFIED_EARNINGS`, `GROSS`, or `COMPONENT`.
`eligibilityComponentId` and `calculationComponentId` identify their respective active tenant recurring earning components.
The component must be present in the employee salary structure; selecting it uses that component alone, without other earnings or a Gross fallback.
Eligibility uses full-month values; contribution calculation uses the finalized payable component once, without further proration.
A Basic component can use the existing 60 percent of Gross formula.
The statutory wage reference remains separately visible through reconciliation warnings and audit evidence.
Rates, contribution-period continuity, exemptions, and upward rounding continue to apply.
New writes reject the retired `wageBasis` and shared `basicComponentId` contract.
Stored legacy settings and historical snapshots remain readable without rewriting issued evidence.
Optional `employeePercentage` and `employerPercentage` overrides require `overrideReason` and `evidenceReference`; omitted rates resolve from the statutory rule.
Saving configuration requires the exact Edit, Activate, and `HR.PayrollManagement.Settings.StatutorySchemes.ConfigureCalculation` capabilities plus school-wide UserScope.
New built-in administrator roles receive ConfigureCalculation during creation; existing roles are never granted it by reseeding.
Impact preview accepts an optional `configurationDraft`, requires ConfigureCalculation for that draft, and rejects a draft whose effective month differs from `monthYear` (`MM-yyyy`).
Past-month configuration writes are rejected; saving versions invalidates unlocked affected payroll only.
Payroll items expose `esiCalculationDetailsJson` and `pfEligibilityDetailsJson` containing the resolved calculation evidence.
Locked and finalized snapshots retain their recorded settings and amounts.
`ESI_RATE_OVERRIDE`, registration suppression, reconciliation differences, and explained enrollment-related CTC variance are visible warnings.
Missing required salary rules, invalid selected components, unknown coverage, stale previews, unauthorized actions, and unexplained target variance remain blockers.

## Canonical employee statutory profile and salary documents

| Endpoint | Permission and behavior |
| --- | --- |
| GET /api/employees/{employeeId}/payroll/statutory-profile | Employee UserScope and the applicable CompensationRevisions.View or StatutorySchemes.View; identifiers are projected only for authorized schemes. |
| PUT /api/employees/{employeeId}/payroll/statutory-profile | CompensationRevisions.Manage for PF and StatutorySchemes.ManageEnrollment for ESIC, with employee scope, concurrency stamp and a GUID idempotency key. |
| GET /api/payroll/statutory-profile/readiness | RunPayroll.View and scoped employees; bounded page size of 100, flags and number-presence only. |
| POST /api/payroll/payslip-settings/logo | GenerateSalarySlip.Add and HR-wide UserScope; tenant-owned PNG/JPEG, 2 MiB maximum, validated dimensions and decoded pixels. |
| POST /api/hr/reports/preview | Existing HR Reports View; payroll_statement requires one employee and an inclusive month range, payroll_register requires one month. |
| POST /api/hr/reports/exports | Existing salary-source View access; queued PDF/Excel exports with ownership, current scope, limits and expiry. |

The profile payload contains Apply PF, PF number, 12-digit UAN, PF disabled reason, Apply ESIC, insurance number and ESIC disabled reason.
The two schemes are independent; blank, whitespace and the exact placeholder 0 count as a missing number.
PF, UAN and ESIC identifiers may be supplied later and do not block salary calculation, saving, activation, payroll locking or payslip issuance.
School PF references such as 7 and 244 are accepted.
An intentional disabled scheme requires a reason; incomplete registration can be saved.
Existing onboarding finalization persists supplied statutory details within its transaction.
Legacy bank/profile and enrollment APIs delegate or project these identifiers instead of maintaining another writable owner.
The effective month defaults to the current Indian month and moves past locked or issued months.
Past-month writes are rejected; stale or conflicting retry writes return 409.
Changed profiles invalidate affected unlocked previews and reviews, preserve issued and paid history, and never generate catch-up deductions or re-solve saved Gross.
Payroll snapshots retain reference and applied amounts, number presence, flags, rule versions and warning reasons.
Review records the current warning codes and profile version before locking.

Payroll document settings own Compact/Detailed layouts, optional staff/bank/attendance/earnings/zero-deduction fields and statement/register column presets.
SchoolName and SchoolAddress are read-only projections of the Institute Setup tenant profile.
New payslips, previews, payroll statement/register branding and salary-document table exports resolve the current institute name and postal address on the server.
The school-level School Identity logo replaces the former payroll-specific logo override.
Settings edits retain existing preferences and issued documents; no data migration or historical PDF rewrite occurs.
Identity, period and totals always remain; every nonzero deduction always appears.
Issuance freezes the presentation version, school branding and logo checksum with the stored PDF.
Statements and registers read saved evidence, use issued payroll for official output and label draft-inclusive output Preview.
Missing months and unreconstructable historical leave fields are unavailable rather than fabricated zero values.
New evidence captures CL, other paid leave and unpaid leave separately from employment-boundary non-payable days.

The ESIC coverage endpoint treats insurance numbers as read-only profile projections.
Identifier writes use the statutory-profile endpoint with caller-supplied concurrency and idempotency keys.

## Full-month salary summary

GET /api/payroll/employees/{employeeId}/salary-summary?month=YYYY-MM requires CompensationRevisions.View or Manage and employee UserScope in the current tenant.
The read model uses saved recurring compensation with full-month payable units and effective statutory rules through the existing payroll calculation services.
It excludes attendance and one-time adjustments, and does not create payroll snapshots or reviews.
The response includes revision identity, salary effective month, earnings, employee deductions, school contributions, and PF/ESIC/PT decisions with reasons and registration completeness.
Incomplete statutory setup returns IsComplete=false and unavailable deduction, take-home and school-cost totals rather than valid-looking zeros.
Registration identifiers are not returned by this endpoint.
Salary-template calculation responses include the same StatutorySummary presentation contract.
Refreshing current unlocked payroll applies eligible contributions before registration is complete; saved gross and locked or issued history remain unchanged.

## HR table export interface

See [complete HR table exports](./references/hr-table-exports.md) for the allowlisted background capture API, private authenticated downloads, source permissions and explicit limits.

## Named ESIC policies

| Endpoint | Contract and authorization |
|---|---|
| `GET /api/payroll/statutory-schemes/esi/policies` | Tenant policy/version catalogue; Statutory Schemes View |
| `GET /api/payroll/statutory-schemes/esi/policies/options` | Selection catalogue; statutory View, template View/Add, or compensation View/Manage |
| `POST /api/payroll/statutory-schemes/esi/policies` | Name, code and initial version; existing Edit, Activate, ConfigureCalculation and HR-wide scope |
| `POST /api/payroll/statutory-schemes/esi/policies/{id}/versions` | New immutable version with expected concurrency stamp and GUID idempotency key; same write permissions |
| `POST /api/payroll/statutory-schemes/esi/policies/{id}/archive` | Expected concurrency stamp; same write permissions; default policy cannot be archived |

Version inputs include employee/employer percentages, wage ceilings, daily exemption, establishment threshold, caps, rounding, calculation settings, source and verification date.
Versions begin on a month boundary; each later version supersedes its predecessor without editing historical versions.
Reject duplicate/backdated version starts, stale writes and changed-body retries.
The response includes `appliesUntil`; expired versions and gaps never fall back to an older version.
Template read/save contracts carry `defaultEsiManagementMode`, `defaultEsiPolicyId` and `defaultEsiExternalReason`.
Preview/calculate and employee compensation contracts carry `esiManagementMode`, `esiPolicyId` and `esiExternalReason`.
Employee compensation saves require `expectedEsiPolicyVersionId` from the preview's `employeeStateInsurance.ruleId` when ESIC is calculated in the platform.
The effective version is checked under the payroll input transaction lock before any salary or statutory-profile changes.
A changed version returns HTTP 409 with `HR:Payroll:CompensationPreviewStale`; refresh the preview before retrying.
Idempotent retries compare the original saved version rather than recalculating the saved result.
`IN_PLATFORM` requires a tenant policy; `EXTERNAL` requires a reason and no policy ID.
External management suppresses both ESIC shares and payslip presentation, while retaining enrollment facts.
Policy selection does not confer eligibility; the existing wage classifications, enrollment, contribution continuity and rounding engine still apply.
Missing effective versions return `HR:Payroll:EsiPolicyNotEffective`; missing/invalid assignment returns `HR:Payroll:EsiPolicySelectionInvalid`.
New assignments reject archived policies; existing employee assignments and historical references continue resolving them.
Preview proof and compensation idempotency include assignment values and resolved calculation evidence.
Reusing a template save key with a changed ESIC assignment or submitting a stale template write returns HTTP 409.

### Attendance register query composition

The grouped register (`GET /api/hr/attendance/register/grouped`) accepts indexed `educationMediumStageIds[0]`, `educationMediumStageIds[1]` query parameters. These identify academic organization assignments, not student/class divisions. Empty selection retains the existing all-authorized behavior. Selected IDs use OR; staff ID, group, department substring, date, attendance status, payroll state, exceptions and search combine using AND. The legacy single `educationMediumStageId` remains supported and intersects a supplied list. Every requested stage is checked against the actor's current scope before reading; year-specific assignment lookup still applies. Filtering occurs before day projection, summary and pagination.

Grouped `sorting` accepts one `employeeName`, `present`, `employeeGroup` or `department` field with `asc`/`desc`. Unknown fields fall back to the existing exception-priority order. Employee ID provides stable ties. HR register Excel/PDF exports deserialize the same criteria and call the same grouped reader for every export batch; no loaded-page filter or separate unscoped query is used. This additive contract needs no schema migration.

### Reporting table queries

`POST /api/hr/reports/runs/query` accepts `skip`, `take` and `criteria` for private generated-report history.
`POST /api/hr/reports/runs/{id}/rows/query` accepts `page`, `pageSize` and `criteria` for an immutable captured result.
Criteria contain `search`, `filters` (`column`, `type`, `value`) and `sorts` (`column`, `direction`).
Only columns from server-owned history or captured-result metadata are accepted.
Search is limited to 256 characters, criteria to 32 KB, filters to 30 and distinct sorts to 60.
Filter alternatives within one column use OR, and different columns use AND.
Numbers and money support equals, greaterThan, lessThan and inclusive between; dates support equals and inclusive after/before; booleans support equals.
Date-time comparisons use school time and minute precision, while ordering preserves full timestamp precision and stable capture-order ties.
History status filtering includes computed Expired, and access checks run before visible pagination.
Existing GET history/row reads remain available without table criteria.
Reporting table exports accept the same criteria object in their query and preserve visible ordered columns.
Filtered captured exports omit original report totals rather than attach totals from the unfiltered population.
No grants, ownership checks, tenant filters, captured data or retention rules change.

## Operational table queries

POST `/api/hr/table-query` accepts `{ table, query, criteria: { search, filters, sorts }, offset, size }`. Offset is zero-based, size is 1–500, query/criteria are bounded, and source/field/operator/type allowlists are server-owned. This read-only contract preserves each owning reader's response envelope and existing permission, tenant and staff scope. It cannot select arbitrary entities, joins, SQL expressions or hidden staff banking columns. Salary criteria are limited to the explicitly selected salary source columns. Existing authentication-only document-configuration reads retain their existing authorization; no grants are widened here.

The operational endpoint always captures its bounded authorized population, including empty criteria, before slicing the response. Offsets need not align to a source page: `offset=1,size=5` returns rows 1–5, and the response size may exceed a reader's native 100-row page. This uses the same snapshot, cap and incomplete-population checks as active criteria; it never rounds an offset down to the beginning of a source page. Legacy aligned export reads retain their existing streaming path when no criteria are selected.

Operational criteria and complete table exports share `HrTableExportSources` and its projection. Filters/count/order precede response pagination. Equal-column alternatives are OR; different columns are AND. Numeric/date/clock/enum handling is typed on the server. Unbounded amounts sort above finite values and do not match finite numeric criteria. Salary displayed-page totals and full-export available-value totals use their respective matching populations. See [implementation limits](references/hr-table-exports.md#operational-query-population-capture).

The table count reflects matching rows. Other owning-reader business summary metadata retains its canonical domain scope (for example the selected payroll/calculation month); table header criteria do not recalculate payroll or statutory business totals. Salary totals explicitly labeled for displayed rows are recomputed for the displayed matching page.
