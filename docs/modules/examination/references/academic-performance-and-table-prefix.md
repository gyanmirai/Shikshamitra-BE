---
module: examination
domain: persistence
content_type: Reference
status: active
updated_on: 2026-09-16
owner: backend-team
audience: humans-and-agents
---

# Academic query performance and table naming

TL;DR: Academic and shared academic setup tables use `academic_`; the rename migration preserves data in place and requires coordinated deployment of the database and backend.
The query changes reduce repeated database reads and entity materialization while keeping existing API shapes and business calculations.
This reference also covers the shared persistence used by [Institute Setup](../../institute-setup/architecture.md).

## Table ownership

`ShikshaMitraConsts.AcademicDbTablePrefix` is used by the Academic and TenantSettings EF configurations.
All 102 mapped tables in those areas are included, including child, link, audit, and owned tables.
Shared academic years, boards, mediums, stages, standards/classes, divisions, shifts, buildings, floors, room types, rooms, and their setup permission tables are included.
Examples: `AppSubjects` becomes `academic_Subjects`, `AppAcademicYears` becomes `academic_Years`, and `AppAllocatedTeachers` becomes `academic_AllocatedTeachers`.
The prefix is not repeated when the original table name already starts with `Academic`: `AppAcademicCalendars` ends as `academic_Calendars`, not `academic_AcademicCalendars`.
Existing suffixes retain their spelling and case; PostgreSQL SQL consumers must quote these identifiers.

Student, admissions, HR, finance, authentication, and platform tables keep their own names even when they reference academic setup records.
Their foreign keys continue referencing the same database objects.
For example, `AppStudents` and `AppEmployees` remain unchanged.
The initial inventory is maintained in [PrefixAcademicTables](../../../../src/ShikshaMitra.EntityFrameworkCore/Migrations/20260916050346_PrefixAcademicTables.cs), with the 12-name correction in [NormalizeAcademicTableNames](../../../../src/ShikshaMitra.EntityFrameworkCore/Migrations/20260916080050_NormalizeAcademicTableNames.cs).

## Query implementation

| Surface | Current optimization | Behavior protected by tests |
|---|---|---|
| Subject/elective usage | Group counts for the requested IDs; traverse tenant-filtered timetable, weekly-slot, and exam parents | Separate usage categories, class counts, soft deletion and tenant ownership |
| Homework/assignment types and rubric lists | Group usage and batch criteria/score lookups | Archive rules, page contents and score calculation |
| Lesson-plan list | Load relevant leave/calendar records once per page | Local dates, division matching and leave precedence |
| Lesson-plan names | Project teacher, class, division, subject, chapter, reviewer, diary and period display fields | Avoid the setup hierarchy's auto-include graph on a small page |
| Subject assignments | Reuse usage/schedule reads across groups; project KPI/workload fields | Group deduplication, search, page contents and existing truncation |
| Student Academic 360 | Filter/page visible exams in SQL; aggregate attendance before loading the last 30 entries; project eligible learning tasks | Historical filters, attendance totals, display dates, tenant isolation and UserScope |
| Student Academic 360 and timetable lookups | Load the requested student's profile details and scalar setup names; count classmates and project elective selections | Profile fields, class/division membership, elective completeness and existing access checks |
| Timetable validation | Project candidate assignments once for submitted periods | Overlap boundaries, teacher-before-room checks, input slot order and conflict fields |
| Room availability | Reuse a request-local calendar/reservation/maintenance/timetable snapshot | Existing conflicts, recommendation scoring and paging |
| Academic/calendar dashboards | SQL counts/status groups and scalar projections | Empty data, row counts, tenant isolation and existing CLR rounding |
| Calendar audit | Group audit statistics and reuse actor lookup within a bulk operation | Separate audit rows and user/tenant identity |
| Bulk exam marks | Preload student/elective applicability; validate all rows before writing a batch | Attendance, range errors, retry/update behavior and marks workflow |
| Marks and seating lists | Project candidates and hydrate page data; count/page seating students in SQL | Search, null roll numbers, eligibility fallback and global publication |
| Question bank/papers/results | Scoped aggregates, distinct usage IDs and reuse applicability data | User scope, question usage and result rules |
| Static report exports | Read all 68 report types in bounded batches; use disk-backed sorting and row spooling; write XLSX/CSV sequentially | Columns, values, filtering, ranks, rounding, ordering and cancellation across batches |
| Dynamic reports and catalog | Project scalar rows and enforce the existing 5,000-row boundary before graph hydration; retain the first 1,000 chapter tokens using the existing CLR comparer | Scope, child expansion, empty rows, where/sort behavior and token ordering |
| Export job lifecycle | Renew a worker lease; use fresh units of work for cancellation/state and per-run blob ownership | Retry, abandoned-run recovery, cancellation, shutdown and cleanup without deleting completed output |
| Report preview and history | Select required lookup columns/families, group chapter progress, limit export history before loading | Names, date extrema, CLR rounding and latest-100 export history |
| Scheduled publication and exam completion | Scan 100 IDs at a time; revalidate and commit each aggregate in a fresh tenant-local unit of work | Existing scheduling thresholds, cancellation/resume, per-item failure and retry |

No shared `DbContext` is accessed concurrently.
Writes retain ABP repositories, auditing, domain behavior, and unit-of-work boundaries.
Room snapshots are request-local; no cross-request cache invalidation behavior is introduced.
Static PDF exports spool source rows to disk and keep the existing layout. The pinned QuestPDF 2024 library still buffers document layout/native output, so PDF rendering is not constant-memory. Large PDF capacity must be measured separately before changing library versions or layout.
The database blob provider also retains its existing storage behavior; streaming generation does not establish a constant-memory bound for blob persistence.
Temporary report files contain school data. They use owner-only Unix permissions, and normal completion, cancellation, failure and partial enumeration clean them up. Abrupt process termination can leave external-sort directories; include the host's temporary directory in its existing secure cleanup policy.
Conflict candidates now have deterministic ID ordering; the old query did not specify child ordering, so which subject appears first among otherwise simultaneous conflicts can differ.
Question-paper dashboard queries now apply the existing UserScope service before aggregation.
Existing Principal/Teacher academic dashboard scope policies still need a separate security review; this change does not establish that every dashboard has complete hierarchical scope enforcement.

## Deploy and roll back

Rename migration: `20260916050346_PrefixAcademicTables`.
Following index migration: `20260916065258_IndexAcademicLessonPlanPaging`.
Name-normalization migration: `20260916080050_NormalizeAcademicTableNames`.
Previous migration: `20260915122353_canonicalFeeAssignment`.
The migration renames tables, indexes, primary keys, and existing foreign-key constraints; it does not copy, drop, or recreate academic data.
Some historical hand-written migrations omitted model-declared foreign keys, so constraint renames check whether those foreign keys exist and preserve that existing state.
This migration does not repair pre-existing schema drift or add missing constraints.
The following migration adds one non-unique partial index on active lesson plans: `(TenantId, AcademicYearId, CreationTime DESC) WHERE NOT IsDeleted`. The measured year-filtered list query uses it to return its page without sorting the full year's rows. It is a separate reversible operation; rollback removes this index before reversing the table names. Index creation also belongs in the coordinated maintenance window.
Primary-key, table, and index renames remain strict.
Historical migrations and the pre-baseline legacy schema bridge retain their original table names deliberately.
The normalization migration renames the 12 prefixed tables whose suffix still started with `Academic`, together with their generated indexes and existing constraints.
It supports databases that already applied the first prefix migration and performs no data copy, table rebuild, or constraint recreation.

1. Restore a recent backup to an isolated staging database and run upgrade plus application smoke checks there.
2. Inventory external SQL reports, integrations, scheduled jobs, and database permissions that refer to the old names; update their SQL consumers with this release.
3. Take and verify the deployment backup, drain API requests, and stop every API/background-worker instance using the old mappings.
4. Apply the reviewed migration script and deploy the matching backend build together.
5. Smoke-test setup lookups, subjects, calendar, timetable conflicts, marks save/read/publish, seating, homework, and reports before resuming traffic.
6. If rolling back the application, stop traffic/workers first, apply the migration's `Down` script to the previous migration, then restore the previous backend build.

Old binaries cannot run against the renamed tables, so a rolling deployment with mixed old/new mappings is unsupported.
Table renames acquire database locks; staging must establish an acceptable maintenance window for the actual workload.
Local validation does not measure production latency or prove external SQL compatibility.

The normal deployment workflow currently invokes DbMigrator before replacing the running backend.
DbMigrator therefore rejects this rename on an existing database unless the one-time maintenance setting is supplied.
It checks migration history before executing schema changes; fresh databases and databases already containing both rename migrations do not require the setting.
Databases that contain the first prefix migration but not the normalization migration still require the coordinated maintenance setting.
During the coordinated release, after verifying the backup and stopping every old API/worker instance, set `ShikshaMitra__Deployment__AcademicRenameMaintenanceConfirmed=true` for that migrator invocation only.
Leave this setting out of the normal online workflow.
The setting records an operator prerequisite; it does not itself stop Azure instances, verify a backup, or protect direct `dotnet ef`/SQL execution.
The guard and its recognized/legacy-history cases have dedicated regression tests.

Generate scripts from `src/ShikshaMitra.EntityFrameworkCore` with the repository-compatible EF 9 tool and a safely supplied connection configuration:

```powershell
dotnet ef migrations has-pending-model-changes --configuration Release
dotnet ef migrations script 20260915122353_canonicalFeeAssignment 20260916080050_NormalizeAcademicTableNames --idempotent --configuration Release --output academic-complete-up.sql
dotnet ef migrations script 20260916080050_NormalizeAcademicTableNames 20260915122353_canonicalFeeAssignment --configuration Release --output academic-complete-down.sql
```

Keep generated scripts and test artifacts outside source commits unless release policy requires them.
Run the model-consistency command in a separate process: ABP runtime/test startup enables legacy Npgsql timestamps while the design-time factory disables them.
Do not change that existing timestamp policy as part of a table rename.

## Validation

Run from the backend repository root:

```powershell
dotnet test test/ShikshaMitra.Application.Tests -c Release --filter 'FullyQualifiedName~ShikshaMitra.Academic|FullyQualifiedName~ShikshaMitra.TenantSettings|FullyQualifiedName~PayrollApprovedLeaveResolver'
dotnet test test/ShikshaMitra.Domain.Tests -c Release --filter 'FullyQualifiedName~ShikshaMitra.Academic'
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests -c Release --filter 'FullyQualifiedName~AcademicTablePrefix|FullyQualifiedName~AcademicMigrationMaintenance|FullyQualifiedName~LegacyMigrationBaselineBridge|FullyQualifiedName~TimetableConflictQuery|FullyQualifiedName~AssignmentV2ModelMapping'
dotnet test test/ShikshaMitra.HttpApi.Tests -c Release --filter '(FullyQualifiedName~ShikshaMitra.Academic|FullyQualifiedName~ShikshaMitra.TenantSettings)&FullyQualifiedName!~Academic.Performance'
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -AllModules
git diff --check
```

`AcademicTablePrefix_Tests` includes an opt-in real PostgreSQL test.
Set `ACADEMIC_SCHEMA_TEST_POSTGRES` to an **empty disposable** loopback database whose name starts with `academic_tests_`.
The test refuses a populated database, creates the previous model, seeds tenant and owned-row examples, and verifies upgrade/rollback preserve storage identities, foreign-key identities, validation state, and data.
It also creates and drops the following lesson-plan index, verifies its column order and soft-delete predicate, and checks that rollback restores the original storage inventory.
It also covers a historical schema with an omitted optional foreign key.
Use a new empty database for each run; the test deliberately does not delete an existing database.
Separately replay the full migration history on a disposable database, since historical migrations can differ from the model snapshot.

`AcademicReportPostgresSmoke_Tests` exercises every static export and dynamic dataset through the real PostgreSQL provider with small synthetic fixtures. It uses `ACADEMIC_PERF_TEST_POSTGRES` and the same empty loopback database guard as the HTTP measurement harness below. Run each opt-in harness separately with a different new database; neither resets an existing database.

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests -c Release --filter 'FullyQualifiedName~AcademicReportPostgresSmoke_Tests'
```

At starting commit `53be9219`, the academic application suite had 206 passing tests and 10 failing tests in exam schedule/status, question-bank update, and question-paper scenarios due to existing UserScope fixture behavior.
The HTTP baseline also had five lecture-slot fixtures missing the class-shift prerequisite and one role fixture missing `employeeOnboardingAssignable`.
Those failures were reproduced in a clean detached checkout before evaluating this change. The fixtures now supply the existing prerequisites; production authorization remains enabled.

Validation on 2026-09-16:

| Check | Result |
|---|---|
| Academic, setup, and payroll calendar application tests | 431 passed, zero failures, using isolated Release outputs |
| Academic domain tests | 32 passed |
| Focused EF/model/PostgreSQL tests | Includes the opt-in real PostgreSQL prefix/index/normalization upgrade and rollback test plus maintenance-guard coverage |
| Legacy migration bridge tests | 5 passed |
| Academic/setup HTTP tests | 49 passed, zero failures, using isolated Release outputs |
| PostgreSQL report provider smoke test | All 68 static exports and all 12 populated dynamic datasets passed; schedule expansion also has a SQLite tenant/filter/limit regression |
| PostgreSQL HTTP performance harness | Passed with 2,000 students, 320,000 marks and five measured endpoints; repeated exam completion was idempotent |
| EF design-time model consistency | No pending model changes |
| PostgreSQL migration chain and generated scripts | Full historical chain; prefix, index and name-normalization upgrade/rollback preserve table and index storage identities |
| Actual DbMigrator maintenance guard | Rejected unacknowledged upgrade without changing history; acknowledged isolated upgrade succeeded |
| Documentation and whitespace | All-module docs validator and `git diff --check` passed |

The isolated PostgreSQL 18 instance used only synthetic data and was stopped after verification.
Production and staging databases were not accessed.
These checks establish correctness and bounded-query properties for the covered scenarios, not a production latency percentage.
The existing solution emits compiler warnings; the isolated Release build completed with zero errors. Validation is focused on Academic, its setup dependencies and the affected payroll calendar behavior, not an assertion that every unrelated module's suite passes.

## Further performance work

Use representative tenant data to record endpoint p50/p95, SQL command counts, rows read, allocations, and PostgreSQL query plans before selecting the next change.
Representative deployment measurements remain necessary after the bounded report readers, background-worker batches, Student Academic 360 reads, subject-assignment reads, and exam-list projections implemented here.
Add indexes only where measured query plans justify their write/storage cost.
Cross-request caches need tenant/user-scope keys and explicit invalidation coverage.
Runtime/ABP/EF upgrades and broad service decomposition should be validated as separate changes.

### Repeatable PostgreSQL HTTP measurements

`AcademicPostgresPerformance_Tests` boots the actual HTTP host with test authentication against a new empty loopback database named `academic_tests_*`.
It creates 2,000 synthetic students, 40 subjects, 12,000 lesson plans, 40 exams, and 320,000 marks, in addition to the existing setup fixtures.
Three warmups and 20 sequential requests per endpoint record p50/p95, SQL reader count, rows consumed, process-wide allocations and response bytes.
It also captures `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` for the last request's SELECT commands.
Rows consumed are reader observations, not an estimate of all internal database work; the query plans provide that additional context.
Authentication, auditing, network latency and concurrency differ from a real deployment, so these results must not be presented as production latency.

```powershell
$env:ACADEMIC_PERF_TEST_POSTGRES = 'Host=127.0.0.1;Port=55436;Database=academic_tests_new_run;Username=academic_test'
dotnet test test/ShikshaMitra.HttpApi.Tests -c Release --filter 'FullyQualifiedName~AcademicPostgresPerformance_Tests'
```

The harness refuses to reset an existing schema; create a new disposable database for each run.
Output is written to the ignored `.artifacts/academic-performance/http-postgres` directory, or `ACADEMIC_PERF_OUTPUT`.
An optional `ACADEMIC_PERF_COMPARE_INDEXES=true` run creates four explicitly named `bench_probe_*` indexes only inside that disposable database and measures the same endpoints again.
Those probes are experiments, not migrations or recommendations to create every candidate index.
The 2026-09-16 experiment selected only the lesson-plan index. Its page query fell from 4.838 ms / 388 shared buffer hits to 0.048 ms / 48 shared buffer hits. The three marks probes were not promoted: this workload did not show sufficient overall endpoint benefit to justify their storage and write cost.
Do not supply a staging/production connection string to this harness.

The successful five-endpoint run used 2,000 students and 320,000 marks:

| Endpoint | Median before candidate indexes | p95 before candidate indexes | SQL readers | Rows consumed |
|---|---:|---:|---:|---:|
| Subject list | 73.01 ms | 80.34 ms | 12 | 94 |
| Lesson-plan page | 23.11 ms | 26.28 ms | 10 | 94 |
| Exam list | 20.47 ms | 23.16 ms | 7 | 242 |
| Marks page | 11.77 ms | 13.27 ms | 8 | 2,057 |
| Student Academic 360 | 39.24 ms | 43.17 ms | 21 | 331 |

Earlier iterations of the local candidate read 16,293 rows for the lesson-plan page (1,688 ms median), read 7,457 rows for marks (579 ms median), and timed out on exam lists and Student Academic 360. Scalar lookup changes removed that avoidable graph loading. These are iterative synthetic comparisons, not a clean production before/after benchmark. The successful run also executed exam auto-completion twice against PostgreSQL, verifying provider translation and no repeated completion.

The query strategy follows Microsoft's guidance on [projection, result limits, and avoiding repeated queries](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).
Migration edits follow the requirement to [review generated operations for data preservation](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing).
