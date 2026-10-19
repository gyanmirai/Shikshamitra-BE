---
module: hr
domain: references
content_type: Reference
status: canonical
updated_on: 2026-10-19
owner: backend-team
audience: humans-and-agents
---

# Complete HR table exports

HR table exports capture the complete authorized population matching a supported table query.
They reuse the owning domain readers and the reporting capture, storage, document writer and seven-day retention infrastructure.
The frontend keeps Minimals and attendance register presentation contracts in its DESIGN.md.

## Actors and preconditions

An authenticated tenant user needs the exact owning screen View permission, actor access and current UserScope.
No separate Reports Export grant is required.
Reports View remains required for generated report history and captured report rows.
Build remains required to capture an unsaved designer definition.
Private exports and generated results belong to their creator.
Template sharing never grants data access.
No grants are seeded or broadened by this change.

## Interfaces

| Method and route | Behavior |
|---|---|
| POST /api/hr/table-exports | Validate allowlisted table, typed query, ordered data columns and excel/pdf format; enqueue private capture |
| GET /api/hr/table-exports/{id} | Return status, friendly name, row count, failure and expiry after current-access checks |
| GET /api/hr/table-exports/{id}/download | Return authenticated completed output after owner, tenant and current-source/scope checks |
| POST /api/hr/table-exports/{id}/cancel | Cancel queued or running work safely |
| POST /api/hr/reports/designer-export | Build-protected capture of an unsaved definition and parameters without saving a template |
| GET /api/employees/{employeeId}/documents/checklist | Page configured staff document types, including missing uploads, with canonical employee access |

Query values deserialize into the owning domain DTO and invalid values return actionable validation errors.
The table catalogue in HrTableExportSources is an explicit allowlist of fields and readers.
Clients cannot select a database table, join, service or sensitive field.
Client page limits are replaced by bounded server batches.
Unsupported source sorting is not executed as arbitrary dynamic input.
Action controls are excluded and sensitive values retain the owning reader masking.

## Capture and state transitions

A new export starts Queued, claims an attempt and transitions to Running.
The job rebuilds the current database principal, rechecks access and reads a consistent repeatable-read database snapshot.
Every matching row is processed before publishing Completed metadata and file bytes atomically.
Filters and counts run before pagination in the owning source.
Generated report exports read materialized captured rows and never rerun source calculations.
Cancellation and failed attempts cannot publish an incomplete file.
Retry creates a new export using the current screen query.
Rows and files expire after seven days through the report retention worker.
Expired history remains identifiable in the report workspace.

## Invariants and limits

The population limit is 500,000 rows.
An incomplete source page fails explicitly.
PDF is limited to 10,000 rows and 200,000 cells; use Excel or narrower filters when exceeded.
No limit silently truncates output.
Excel preserves numeric and date types, monetary precision, readable labels, filters, frozen identifiers and supported totals.
PDF repeats school identity, period, filters, capture time, headers and page numbers.
Wide PDF registers repeat identifiers across readable column sections.
Unavailable historical values remain unavailable.
Reporting never recalculates payroll.

## Source and scope details

Staff directory reads use current database UserScope rather than role-name claims.
The existing own-record exception is preserved.
Attendance import details filter scoped staff before counting and paging; unresolved staff codes are visible only with HR-wide scope.
Performance report readers use the existing review scope service for pending actions, outcomes, training, SQAA dashboards and audit packs.
Policy impact returns the complete affected collection rather than a 25-person sample.
Policy versions and assignments apply search and filter conditions before counting and paging.
The document checklist represents one configured document type for the selected employee, including absent uploads.
Current record access is rechecked before serving captured employee identifiers or salary staff codes.

## Validation and release

Run HrTableExport_Tests, HrTableExportApi_Tests, HrReportingEngine_Tests and HrReportingWorkspaceApi_Tests.
Run employee list/options/document, attendance-import, policy and professional-management regression cases for changed readers.
Run HrReportingPostgres_Tests against a fresh disposable loopback reporting_tests_* database.
The PostgreSQL acceptance population exceeds 25,000 records and checks immutable downloads, date cells, capture retention and saved payroll evidence.
Run HR docs validation and canonical executable security checks.
Finish with frontend signed-in browser review and inspection of freshly generated Excel/PDF files.
Deliver backend before the dependent frontend and obtain explicit approval before merging.
Preserve deployment files, attendance, employee records, payroll evidence and official payslips.
