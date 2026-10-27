---
module: hr
domain: hr-policy-architecture
content_type: Reference
status: canonical
updated_on: 2026-10-27
owner: backend-team
audience: humans-and-agents
---

# Understand HR policy architecture

TL;DR: use this page for the shared work calendar, attendance policy, payroll policy, and assignment resolution model.

## Purpose

HR uses a shared calendar for dates, but payroll and attendance interpret those dates differently. A calendar holiday is neutral: it says what date is a holiday or event. Attendance policy decides whether attendance is required and how holiday work is handled. Payroll policy decides whether the date affects pay, payroll basis days, or paid/unpaid treatment.

## Model

| Concept | Owner | Responsibility |
|---|---|---|
| `WorkCalendarPolicy` | HR shared policy | Academic-year work calendar, weekly-off pattern, custom weekly-offs, holiday sync mode |
| `AttendancePolicy` | Attendance module | Punch thresholds, missing punch behavior, late/early rules, overtime, holiday work behavior, payroll integration |
| `PayrollPolicy` | Payroll module | Day calculation mode, Saturday/workday units, manual month days, payroll holiday overrides, locked month settings |
| `HrPolicyAssignment` | HR policy assignment | Effective-dated scoped assignment to attendance and payroll policies for an employee or dynamic group |
| `HrPolicyVersion` | HR policy lifecycle | Immutable policy snapshots used for history, compare, restore-as-draft, and activation review |

## API ownership

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

## Resolution rules

Resolution order is specificity-first:

1. Active employee assignment effective on the requested date.
2. Active group assignment effective on the requested date.
3. Active tenant/academic-year default attendance policy and payroll policy.

Groups are the v1 abstraction for dynamic role, department, employment-type, and mixed targeting. Only active policies can be assigned. Active assignments for the same target type/key and academic year cannot overlap by date range. Employee assignments always beat group assignments. Within the same specificity, higher priority wins; ties use the latest effective-from date. The preview APIs return the winning source and resolution steps for skipped candidates.

## Policy lifecycle

### New production tenant initialization

`HostTenantAppService.CreateAsync` sets the internal `HrStarterPoliciesEnabled` tenant extra property only for newly created production schools. It still avoids `IDataSeeder` and demo school data. `MultiTenancyAppService.CreateAsync` also opts in only its newly created tenant after the legacy base creation/seed pipeline returns, then initializes any empty academic years that pipeline just created. It preserves any seeded/customized policies. Generic demo seeders, tenant updates and deployment runs never opt existing schools into this initializer.

`AcademicYearHrStarterPolicyHandler` handles the local `EntityCreatedEventData<AcademicYear>` event before the creation unit of work commits. `HrStarterPolicyInitializer` checks the opt-in and academic-year `HrStarterPoliciesInitialized` marker, switches to the year owner's tenant context, and uses explicit tenant/year predicates while preserving ABP filters. It ensures one tenant-wide shift when none exists, creates the calendar, then default payroll, attendance and leave policies (including paid/unpaid rules), and finally marks the year initialized. All references point to the same tenant/year calendar. Domain constructors enforce policy validity. The tenant-level `HrStarterShiftInitialized` marker preserves shift edits/deletions across academic years; shift IDs derive from the tenant alone.

The year, policy set, rules and marker commit together. A failure propagates and rolls back creation; callers may retry the normal setup request. IDs derive deterministically from tenant ID, academic-year ID and record kind, so duplicate delivery cannot insert a second starter set. Concurrent stale deliveries may encounter a database primary-key/concurrency conflict and must retry the transaction. Completed years and existing policy setups are skipped; the initializer never rewrites customized defaults, promotes drafts, repairs deleted policies or runs as a migration/backfill contributor. There is no automatic rollover on year updates; each newly created academic year in an opted-in school receives its own initial set. Changing an existing year's dates follows the existing policy-edit workflow.

Starter settings and review obligations are documented in [HR product behavior](../product.md#new-school-starter-policies). No FE deployment or schema migration is required: flags use existing tenant/year extra-property storage. Deploy the backend before creating new production tenants. Existing tenants remain unchanged and would require a separately reviewed explicit backfill feature.

Policy create, edit, clone, restore, set-default, and archive operations capture `HrPolicyVersion` snapshots. Restoring a version always creates a new draft policy; it does not mutate active policy history. Risky changes use impact preview and confirmation tokens before write operations when employees, assignments, payroll snapshots, or attendance evaluations may be affected. Locked payroll impact remains blocking.

### Leave impact and version history

`POST /api/leave-policies/impact/preview` assesses an exact create/update/default/archive proposal or an information-only `inspect` request, in the authenticated tenant and selected academic year/date window. Effective staff uses the operational staff > group > default resolver and real memberships. Assignment records, pinned requests, settlement items and linked payroll periods are separate measures. Each has an explicit scope and nullable availability state; failed/incomplete input is never zero. Payroll exposure uses employee/request-date overlap and settlement employee/month provenance, not a nonexistent leave-policy ID on payroll snapshots. Exposure alone is not a salary change or a blanket prohibition on changing an unrelated configuration. Balance deltas and salary differences remain explicitly uncalculated; public year-start balance resolution and policy-date accrual semantics are unchanged.

`POST /api/leave-policies/impact/details` recomputes the same fingerprint before serving bounded pages. Employee/request/payroll details retain their owning read grants and existing staff scope. Accessible totals and redacted counts are distinguished from the complete tenant safety assessment. Confirmation binds the actor, tenant, proposal, dates, source versions/configurations, assignment/membership dependencies and reference identities; mutation revalidation occurs after acquiring the tenant payroll fence and involved policy rows. Draft maintenance and new-draft restore add no operational gate. Active edits, activation and default replacement require server review; the initial unreferenced default can still be created during setup.

Leave versions reuse `HR_PolicyVersions`. Existing policies start at counter 1; their first changed lifecycle write captures the actual pre-change configuration as an explicitly labeled baseline, then captures the incremented version. GET never creates history. No-op writes do not invent events. Default promotion and demotion capture both policies within one unit of work with a shared audit correlation. The additive `AddLeavePolicyVersion` migration initializes the counter and concurrency mapping; it does not reconstruct historical events.

Leave history/compare uses existing Leave View, and restore-as-new-draft uses existing Leave Add. Restore copies operational snapshot rules into fresh identities, Draft and nondefault, retaining source policy/version provenance. It never rebinds assignments, requests, carry-in, settlements or payroll outcomes. Referenced historical rules, including source/target settlement rules and audited deleted request references, cannot be repurposed by later edits. Existing archive/unarchive and mutation grants remain separate and unchanged. Deploy the additive backend contract/schema before the compatible frontend; coordinated rollout is required before enforcing reviewed active writes for existing clients.

## Holiday sync

`WorkCalendarPolicy.HolidaySyncMode` supports:

- `linked_with_overrides`: follow calendar dates, allow payroll/attendance policy interpretation overrides.
- `snapshot`: copy calendar impact for controlled review before changes apply.
- `independent`: manage policy dates independently.

MVP defaults to `linked_with_overrides`.

## Payroll and attendance impact

Payroll owns paid/unpaid and basis-day behavior. Attendance owns work/non-work, comp-off, overtime payout, and punch expectations. This allows the same holiday to be:

- non-working in attendance and paid in payroll;
- required attendance with overtime treatment;
- unpaid or excluded for a part-time employee;
- overridden for one employee or dynamic employee group through policy assignment.

## Migration note

Existing single attendance and payroll configuration rows are migrated once into default policies for each academic year. The old configuration APIs, services, seeders, EF mappings, and tests are removed after the migration. New payroll locks and attendance evaluations must resolve policy through `HrPolicyResolver`; historical payroll snapshot policy fields remain nullable only for rows that cannot be resolved during migration.
