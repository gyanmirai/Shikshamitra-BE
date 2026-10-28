---
module: academic
domain: read-contracts
content_type: Reference
status: active
updated_on: 2026-10-28
owner: backend-team
audience: humans-and-agents
---

# Read Academic workspaces and report pages

TL;DR: Academic screens use additive lookup, workspace, summary and page contracts to avoid loading full operational graphs for every UI request.
Existing APIs remain compatible; each read retains its existing action permission, tenant boundary and user-scope rules.
The [frontend client map](../../../../../Shikshamitra-FE/docs/academic/clients.md) owns screen wiring, cache behavior and browser lifecycle.

## Read contracts

Routes below include the `/api` prefix.
Controllers delegate authorization and scoped reads to their application services.

| Method and route | Input and result |
|---|---|
| `GET /api/class-management/class-teacher-assignments/workspace` | `organizationId`; returns assignment `items`, `summary` and `eligibleTeachers` together. |
| `GET /api/academic-mediums/{academicMediumId}/subjects/lookup` | Optional `searchTerm`, `includeArchived` (default false); lightweight subject options without operational usage enrichment. |
| `GET /api/academic/curriculum/chapters/lookup` | Required `standardId`, `subjectId`, optional `divisionId`; chapter ID, number and name options. |
| `GET /api/academic/curriculum/lesson-plans/workspace` | Existing lesson scope and paging plus `view`, `uiCriteria`, `searchTerm`, `searchColumns`, `filters`, `sorts`; returns `totalCount`, page `items`, associated `diaries` and `summary`. |
| `GET /api/academic/rubrics/templates/paged` | Existing rubric filters plus one-based `page`, `pageSize` (1 to 100), JSON `filters`/`sorts`, optional `timeZoneId`; template summaries, total and status/usage counts. |
| `GET /api/academic/rubrics/{id}/evaluations/submission/{submissionId}` | Required `studentId`; returns only the selected student's submission evaluation, or no content. |
| `POST /api/class-management/student-elective-assignments/selection-counts` | Body: `academicMediumId`, `classId`, `divisionIds`, `electiveGroupIds`; counts grouped by elective group, division and subject. |
| `GET /api/class-management/student-elective-assignments` | Existing context/search plus `includeSelectableSubjects` (default true); pass false to omit per-student option arrays and use the shared `subjectOptions` response. |
| `GET /api/academics/timetable/room-reservations/page` | `skipCount`, `maxResultCount`, `sorting`, optional created date, room, approval view, status group, search and JSON filters; page plus status counts. |
| `GET /api/academics/timetable/room-reservations/summary` | Existing reservation context; status counts for badges without loading reservation details. |
| `GET /api/academics/timetable/room-availability/audit-logs/page` | Paging/sorting, optional room, date range, action and search; selected audit rows with their complete snapshots and inline differences. |
| `GET /api/academics/student-academic-360` | Year, board, education medium, optional class/division, search, smart filter and paging; page items plus the existing all-selection summary. |
| `GET /api/academics/student-academic-360/{studentId}/tabs/{tab}` | Existing tab/context plus task/exam offsets, limits and filters; detail totals allow access to older records. |

Grade filter options remain documented with their owning [Examination API](../../examination/api.md).
The class-teacher, elective, lesson-plan, rubric and room DTO sources are under `src/ShikshaMitra.Application.Contracts/Academic/`.
Student list validation caps `maxResultCount` at 100 in `src/ShikshaMitra.Application.Contracts/Academic/StudentAcademic360/StudentAcademic360ListInputValidator.cs`.

## Counts, history and compatibility

| Surface | Contract to preserve |
|---|---|
| Lesson workspace | Exact views: `lessonList`, `teacherDiary`, `reviewQueue`; unknown views fail with `LessonPlan:InvalidWorkspaceView`. Counts and filter options describe the authorized cohort before table paging. Review queue spans authorized media. Full plans and diary responses belong only to selected page plan IDs. |
| Rubric list | Template summaries omit criterion graphs. Open details separately. Selection, evaluation and save permissions remain distinct from listing. |
| Student Academic 360 | Overview defaults to 10 recent tasks and 5 recent exams; attendance returns full-range totals and at most 30 recent rows. Detail paging and filters retain historical access. Legacy zero-limit detail behavior remains compatible for older clients. |
| Academic audit | Default time-ordered reads merge bounded event keys from relevant authorized sources before hydrating page context. Search, rendered values and alternate ordering can require broader narrow metadata. No history cutoff removes older events. |
| Room audit | Page selection precedes full snapshot loading; snapshots and inline differences remain complete for the selected events. |
| Legacy substitution year | GET infers the effective academic year without saving. A separate non-remote repair service provides bounded, authorized, locked and audited writes for maintenance. |

These changes bound graph hydration, rather than guaranteeing constant SQL work for every query.
Student name/culture ordering, rendered audit search, rubric date/culture matching and subject-allocation group order retain narrow metadata fallbacks where required for compatibility.
Lesson workspace summaries likewise read cohort keys before selecting full page records.
See `src/ShikshaMitra.Application/Academic/Audit/AcademicAuditTrailAppService*.cs`, `src/ShikshaMitra.Application/Academic/Curriculum/LessonPlanAppService.Workspace*.cs` and `src/ShikshaMitra.Application/Academic/StudentAcademic360/StudentAcademic360AppService.List.cs` before changing those semantics.

## Reports

`POST /api/app/academic-report/preview` selects page content for all 68 standard report types while retaining full filtered totals, ordering and summaries.
Preview page size defaults to 50 and rejects values over 500; export generation keeps its complete filtered dataset.
The focused builders are `src/ShikshaMitra.Application/Academic/Reports/AcademicReportGenerator.*Preview*.cs`.
Dynamic dataset queries and AI catalogue reuse have separate coverage below.

Export jobs retain the existing create/status routes under `/api/app/academic-report/exports`.
`GET /api/app/academic-report/exports/{jobId}/download` returns file bytes with content type and filename headers.
The frontend consumes a native Blob and can download a completed file without regenerating it.
Export history returns the latest 100 jobs for the current user and tenant.

## Focused validation map

Tests below are executable coverage, not a claim that a provider or deployment passed.
Paths are relative to the backend repository root.

| Concern | Test source |
|---|---|
| Class-teacher combined workspace and elective grouped reads | `test/ShikshaMitra.Application.Tests/Academic/ClassManagement/ClassTeacherWorkspaceRead_Tests.cs`, `test/ShikshaMitra.Application.Tests/Academic/ClassManagement/StudentElectiveReadPerformance_Tests.cs` |
| Subject lookup/bulk reads, allocation paging and chapter enrichment | `test/ShikshaMitra.Application.Tests/Academic/Curriculum/SubjectCreationReadPerformance_Tests.cs`, `test/ShikshaMitra.Application.Tests/Academic/Curriculum/SubjectAssignmentReadBatching_Tests.cs`, `test/ShikshaMitra.Application.Tests/Academic/Curriculum/SubjectAllocationPagingPerformance_Tests.cs`, `test/ShikshaMitra.Application.Tests/Academic/Curriculum/ChapterReadPerformance_Tests.cs` |
| All lesson workspace views and global counts | `test/ShikshaMitra.Application.Tests/Academic/Curriculum/LessonPlanWorkspacePerformance_Tests.cs` |
| Template paging and selected submission evaluation authorization | `test/ShikshaMitra.HttpApi.Tests/Academic/HomeworkAssignment/RubricReadPerformanceApi_Tests.cs` |
| Reservation pages, audit snapshots and recurrence parity | `test/ShikshaMitra.Application.Tests/Academic/Timetable/RoomAvailability/RoomReadPerformance_Tests.cs` |
| Read-only legacy year inference | `test/ShikshaMitra.Application.Tests/Academic/Timetable/Substitutions/SubstitutionYearReadOnly_Tests.cs` |
| Complete audit history, key ordering and scope | `test/ShikshaMitra.Application.Tests/Academic/Audit/AcademicAuditReadPerformance_Tests.cs`, `test/ShikshaMitra.Application.Tests/Academic/Audit/AcademicAuditKeyParity_Tests.cs` |
| Student list summaries, culture/search parity, bounded details and scope | `test/ShikshaMitra.Application.Tests/Academic/StudentAcademic360/StudentAcademic360Read_Tests.cs` |
| Standard preview/page/export parity and dynamic projections | `test/ShikshaMitra.Application.Tests/Academic/Reports/*Preview*.cs`, `test/ShikshaMitra.Application.Tests/Academic/Reports/AcademicDynamicReportProjection_Tests.cs`, `test/ShikshaMitra.Application.Tests/Academic/Reports/AcademicLearningTaskExportParity_Tests.cs`, `test/ShikshaMitra.Application.Tests/Academic/Reports/AcademicExamExportParity_Tests.cs` |
| Dashboard sections, timetable settings and calendar batching | `test/ShikshaMitra.Application.Tests/Academic/Dashboard/AcademicDashboardSectionQueries_Tests.cs`, `test/ShikshaMitra.Application.Tests/Academic/Timetable/TimetableSettingsReadPerformance_Tests.cs`, `test/ShikshaMitra.Application.Tests/Academic/Calendar/AcademicCalendarReadPerformance_Tests.cs` |
| PostgreSQL translation, HTTP commands and plans | `test/ShikshaMitra.HttpApi.Tests/Academic/Performance/AcademicReportPostgresSmoke_Tests.cs`, `test/ShikshaMitra.HttpApi.Tests/Academic/Performance/AcademicPostgresPerformance_Tests.cs` |

Run focused suites from `Shikshamitra-BE` with its pinned SDK:

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~LessonPlanWorkspacePerformance_Tests|FullyQualifiedName~StudentElectiveReadPerformance_Tests|FullyQualifiedName~StudentAcademic360Read_Tests|FullyQualifiedName~AcademicReportPerformance_Tests|FullyQualifiedName~AcademicExamExportParity_Tests|FullyQualifiedName~AcademicLearningTaskExportParity_Tests" --artifacts-path .artifacts/academic-read-tests
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~RubricReadPerformanceApi_Tests --artifacts-path .artifacts/academic-read-api-tests
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~AcademicReportPostgresSmoke_Tests|FullyQualifiedName~AcademicPostgresPerformance_Tests" --artifacts-path .artifacts/academic-read-postgres
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module academic
git diff --check
```

The PostgreSQL tests skip unless `ACADEMIC_PERF_TEST_POSTGRES` identifies a new empty loopback database named `academic_tests_*`; the factory rejects other targets.
Use the normal disposable test setup to supply credentials, never a school database.
`ACADEMIC_PERF_OUTPUT` optionally selects an artifact directory outside maintained docs.
Measurements use synthetic data and sequential warm requests; they do not establish production latency, concurrent load or deployment acceptance.
