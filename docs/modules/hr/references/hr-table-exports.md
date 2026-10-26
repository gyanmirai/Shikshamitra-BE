---
module: hr
domain: references
content_type: Reference
status: canonical
updated_on: 2026-10-26
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

## Operational-query population capture

The read-only operational endpoint and table export share the owning authorized readers, server field catalog and projection. Legacy endpoint inputs remain compatible. Criteria are applied to a complete bounded server capture, not a loaded browser page and not SQL predicate pushdown. A transactional snapshot covers the capture. Reader ordering has stable ID ties; raw numeric enum/editing envelopes are preserved. ESI calculates one authorized model per capture and slices that model for source batches. Its existing view/configuration-draft permissions still apply.

The configured cap is 500,000 source rows, captured in batches of 100 (at most 5,000 reader pages). An over-cap population, changing total or incomplete page sequence returns an error with no partial result. Header filters cannot reduce the population before capture; narrow the existing period/staff/organization/domain-search scope first. Every new query request recaptures its population. This design has linear allocation/capture cost and sorting cost, and underlying reader/database cost varies by dataset. It is intended for scoped HR workspaces, not an unmeasured guarantee of interactive latency at the cap.

Synthetic owning-reader measurements on this Windows host: 10,000 rows / 100 pages / 288 ms / about 50.5 MB allocated; 25,000 rows / 250 pages / 544 ms / about 120.4 MB allocated. Both return ten response rows and reuse the same captured population for export within the operation. These measurements exclude production database latency and are allocation totals, not peak resident memory. They do not establish performance at 500,000 rows. Changed-population and over-cap tests explicitly verify failure rather than partial answers. No migration, permission grant, hierarchy expansion or production data mutation is required.

Column criteria retain their displayed meaning: staff names include the identifiers/context shown in their cell, onboarding candidate contact uses the visible email/mobile/application fallback, calendar ranges and custom weekdays use the frontend labels, and performance sections include SQAAF domains. Composite identity/range predicates preserve primary name/start-date ordering. Imported Attendance labels prioritize statusCode and normalize legacy text exactly as the editor does; aliases cannot make a contradictory raw status match. Enum choices accept existing native zero/false codes as well as visible labels except where a status is deliberately derived. The I-card selection source reuses the authorized staff reader and exposes only name/designation/readiness criteria; selection and document-export restrictions remain in the frontend workflow.

The latest capture fixture includes a unique visible email per onboarding row and its composite query value. Its allocation totals exceed the earlier name-only fixture (about 37 MB / 99 MB); that comparison includes a payload change and is not an isolated query-engine benchmark. The endpoint still captures every scoped row before global criteria, so source-reader/database latency and large DTOs can dominate. The cap remains a safety limit rather than an interactive-latency guarantee.
