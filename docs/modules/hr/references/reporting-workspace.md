---
module: hr
domain: references
content_type: Reference
status: canonical
updated_on: 2026-10-19
owner: backend-team
audience: humans-and-agents
---

# HR reporting workspace

The reporting workspace separates reusable templates, generation parameters, and captured results.
The frontend owns the library, generator, designer, and result views.
This document owns the server behavior and cutover procedure.

## Definitions and actors

Built-in templates are immutable examples.
A user with `HR.Reports.Build` can copy or create private templates.
A user with `HR.Reports.ManageTemplates` can create and publish school templates.
`HR.Reports.Delete` is additionally required to archive a template.
School edits create a draft while the last published definition remains available.
Updates require a matching concurrency stamp.
Archiving blocks new generation and preserves captured results.

Each action also requires `HR.Reports.View`, the exact owning dataset permission, and current tenant UserScope.
Leave datasets retain the canonical own/reporting hierarchy rules.
Performance datasets retain assignment access and confidential-outcome permission checks.
Sharing a template grants no source-data access.
Runs and downloads belong only to their creator.
Permissions, staff access, review-assignment access, and the captured scope are checked again before serving a result.
Background execution rebuilds the principal from the active user's persisted roles.
No grants are broadened or reseeded by this feature.

## Catalogue and row meanings

| Dataset | Row meaning | Layouts |
| --- | --- | --- |
| Staff | One current staff profile with joining and lifecycle dates | Detail, summary |
| Attendance | One eligible staff member on one date, including missing attendance | Detail, summary, monthly matrix |
| Leave requests | One request overlapping the selected dates, with approved units clipped to that period | Detail, summary |
| Leave balances | One current staff leave-policy rule, calculated at capture time | Detail, summary |
| Salary register | One staff member in one payroll month from saved evidence | Detail, summary, component register |
| Salary statement | One selected staff member in one payroll month, including unavailable months | Detail, summary |
| Performance | One accessible review assignment in a selected cycle | Detail, summary |
| Policies and audit | One policy assignment or scoped HR audit event | Detail, summary |

Twenty built-in templates cover the initial staff, attendance, leave, payroll, performance, policy, and audit use cases.

| Starter template | Meaning of one output row |
| --- | --- |
| Staff directory | One current staff profile |
| Department strength | One department with distinct staff count |
| Joining/leaving register | One staff member whose joining or leaving date overlaps the period |
| Staff in/out register | One eligible staff member on one date |
| Marked absences | One recorded absence on one eligible staff date |
| Attendance not recorded | One closed expected working staff date without attendance or approved leave |
| Missing punches | One closed integrated-attendance staff date with incomplete punches |
| Monthly attendance register | One staff member with daily cells and classification totals for one month |
| Leave requests | One request overlapping the period |
| Pending leave approvals | One overlapping request awaiting approval |
| Leave usage | One staff member with approved leave units overlapping the period |
| Current leave balances | One current staff leave-policy rule |
| School salary register | One staff member's saved evidence for one payroll month |
| Staff salary statement | One month for the selected staff member, including unavailable evidence |
| Earnings/deductions breakdown | One staff payroll month with saved component columns |
| Department payroll summary | One current department with totals from saved payroll evidence |
| Review completion | One accessible review assignment |
| Finalized outcomes | One accessible finalized review assignment |
| Policy assignments | One scoped policy assignment |
| HR audit activity | One scoped HR audit event |

Fields, operators, layouts, parameters, and aggregation capabilities come from the server catalogue.
Required generation parameters cannot be removed in the designer.
Filters support all/any groups with one nested level.
Grouping supports two levels and summaries support count, distinct count, sum, and average.
Charts support KPI, bar, line, and donut combinations validated against the catalogue.
No SQL, arbitrary joins, formulas, pivots, schedules, recurring delivery, AI design, or result sharing is exposed.

## Execution and invariants

Generation accepts a template ID, published revision, and declared parameter values.
The server resolves the definition and fixed filters.
Client-supplied definitions cannot override a generation request.
Relative dates resolve when queued in the tenant setting `ShikshaMitra.HR.Reporting.TimeZone`, default `Asia/Kolkata`.
The resolved dates, timezone, definition, scope, school identity, totals, and rows are captured together.
Date-only boundaries remain separate from UTC timestamp instants.
Monthly attendance and school salary registers require a complete month.
Salary statements resolve their selected range to complete payroll months so the captured period matches the saved evidence.

Runs move through Queued, Running, Completed, Failed, Cancelled, and Expired.
A background worker claims an attempt and executes inside a repeatable-read database transaction.
Only a successful materialization publishes Completed.
Optimistic concurrency prevents cancellation or another worker from being overwritten.
Retry creates a new run and preserves the failed attempt in history.
Interrupted Running work becomes retryable after 30 minutes when read again.

The complete matching population is filtered and aggregated before output pagination.
Readers page database-backed sources rather than using the legacy capped extract.
The explicit source limit is 500,000 rows and reporting periods are limited by dataset.
Exceeding a limit fails the run rather than truncating it.
Captured rows are tenant-owned JSONB chunks of 200 rows.
Result pagination and both file formats read only those chunks.
Changing source records cannot change a completed result.

Attendance uses the canonical employee eligibility, joining/leaving dates, work-calendar, capture-mode, and attendance-register readers.
Recorded status stays separate from derived day classification and exceptions.
Approved leave, weekly offs, holidays, future dates, and still-open days are excluded from missing-punch exceptions.
Manual-status attendance does not require integrated punches.
Half-day leave remains visibly distinct from full-day leave.

Leave usage uses saved approved units and canonical calendar calculations for partially overlapping approvals.
Balances use the canonical accrual, usage, pending, and carry-forward readers with batched reads.
Historical balance reconstruction is excluded.

Payroll reads issued salary evidence by default and includes saved draft snapshots only when explicitly requested.
Reporting never recalculates salaries.
Missing historical values stay null rather than zero.
Bank accounts are masked and decimal values remain typed through export.
Component registers preserve saved component IDs and labels.
Older evidence without component IDs remains explicitly unidentified and uses its exact saved labels.
Existing salary-document routes retain their narrow preview/export adapter and source permissions.

## Interfaces

All workspace interfaces use `/api/hr/reports`.

| Route | Purpose |
| --- | --- |
| `GET catalogue` | Accessible datasets and capabilities |
| `GET workspace-options?dataset=&search=` | Scoped parameter choices and staff search |
| `GET library?edit=` | Published templates or authorized draft definitions |
| `POST library`, `PUT library/{id}` | Create/save/publish a definition |
| `POST library/{id}/archive?concurrencyStamp=` | Archive with concurrency checks |
| `POST designer-preview` | Evaluate an unsaved definition; Build permission required |
| `POST runs`, `GET runs` | Queue a run or list private history |
| `GET runs/{id}`, `GET runs/{id}/rows` | Captured metadata and paginated rows |
| `POST runs/{id}/cancel`, `POST runs/{id}/retry` | Safe recovery actions |
| `POST runs/{id}/exports?format=` | Prepare Excel or PDF from captured rows |
| `GET runs/{id}/download?format=` | Download after current authorization checks |

History returns `items` and `hasMore` for the requested private-history window.
Current authorization can remove items from a window without hiding the link to older history.

Excel uses numeric/date cells, friendly headers, filters, frozen identifiers, and totals.
PDF uses captured school identity, reporting context, repeating headers, page numbers, and readable column sections.
PDF supports up to 10,000 output rows and 200,000 cells and fails explicitly above either limit.
The retention worker removes result rows and files after seven days.
Expired history remains identifiable and can lead to a fresh generation.

## Controlled cutover

Do not run cleanup against a live database as part of normal application startup.
Stop old reporting workers and pause report entry points before cutover.
Back up and restore the target database to a disposable environment first.
Apply the migration there, including removal of the old schedules table.
Export the exact report-owned file manifest with `scripts/hr/reporting-cutover-manifest.sql`.
Run `scripts/hr/reporting-cutover.sql` with an explicit expected database name and cutover confirmation.
The cleanup removes only old report templates, old export history, and queued old report jobs.
Delete only the manifest's referenced `hr-report-exports/` objects through the configured blob provider after the database cleanup commits.
Do not delete the whole blob container or infer file ownership from a broad directory search.
Preserve employees, attendance, leave, payroll snapshots, issued payslips, and all HR audit evidence.
Verify those source counts and official-file references before and after cleanup.
Repeat the validated procedure for release only after the backend and frontend PRs have explicit merge approval.

## Validation

Run `HrReportingEngine_Tests` for full-population filtering/aggregation, relative dates, matrices, component identity, state transitions, and typed Excel output.
Run `HrReportingWorkspaceApi_Tests` for stored definitions, source permissions, revocation, concurrency, and retired endpoints.
Run `HrReportingPostgres_Tests` with `HR_REPORTING_TEST_POSTGRES` pointing to an empty loopback `reporting_tests_*` database.
The PostgreSQL fixture refuses an existing schema and never resets a database.
Complete authenticated Playwright checks, final Codex in-app browser smoke testing, and representative Excel/PDF inspection before release.
