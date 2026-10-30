---
module: mobile-academics
domain: validation
content_type: Reference
status: canonical
updated_on: 2026-10-31
owner: backend-team
audience: humans-and-agents
---

# Validate mobile Academics

## Dashboard class teacher

`MobileAcademicAppService_ClassTeacher_Tests` exercises student and guardian
dashboard reads, assigned-year/yearless mappings, canonical serialized keys,
duplicate identities, conflicts, inactive/non-teaching/deleted/missing employees,
tenant/class/division/medium/year mismatches and revoked portal links. Query
capture checks one narrow teacher projection without HR profile hydration;
repository authorization reads are separate from that projection.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~Dashboard_class_teacher_|FullyQualifiedName~Student_and_guardian_read_published_academics_without_staff_grants"
```

Run the dashboard/header/count Flutter regressions in the linked mobile guide.
Local application tests do not replace an authenticated staging/device check.

## Homework counts

`MobileAcademicAppService_HomeworkCounts_Tests` covers first submissions and edits
for students/guardians, drafts, legacy submission rows, review/excusal, publication,
closed and unsupported work, late first submissions, legacy subject identifiers,
tenant/link/year boundaries and reads after a committed write in a fresh unit of
work. SQL capture verifies scalar counts without task/submission content hydration.
Keep the existing homework visibility and date-range regressions alongside it.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Homework_counts_|FullyQualifiedName~Homework_compatibility_keeps_placement_and_publication_boundaries|FullyQualifiedName~HomeworkDates"
```

Focused local source builds may use the bounded historical-migration-designer
compilation described below on constrained machines. That is not a full solution
build or authenticated staging certification. Counts require no migration.

## Elective preferences

Run the preference application and timetable approval-policy regressions together. The timetable theory verifies that a Wednesday elective gates a Monday request, that unlocked assignments never expose periods, and that profile context remains independent. Existing no-elective, unpublished, date and calendar behavior must remain covered.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~PortalElectivePreferenceAppService_Tests|FullyQualifiedName~PortalTimetableAppService_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~PortalElectivePreferenceApi_Tests|FullyQualifiedName~PortalTimetableApi_Tests"
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module mobile-academics
git diff --check
```

Relational acceptance must exercise the actual receipt migration and two concurrent student/guardian requests on separate PostgreSQL connections. Verify rollback after a failed legacy assignment insertion, immutable original preference after staff save, visibility through the unchanged school read, and timetable availability after the existing group lock. SQLite/application tests alone cannot prove advisory-lock or unique-index race behavior. Use the disposable loopback database convention below; do not run migrations or reset a school database. Deployment and authenticated staging smoke remain release checks.

On 2026-10-30, all 46 focused application cases passed: the complete existing timetable/profile test class and ten preference cases, including direct access/year validation, independent groups, legacy read-only assignments, soft-delete conflicts, immutable history, whole-week approval gating and profile-context compatibility. All ten focused HTTP cases also passed. The PostgreSQL 18.6 workflow in `MobileAcademicPostgresApi_Tests` applies the actual `AddStudentElectivePreferenceSubmissions` migration SQL in a fresh loopback database, checks both unique indexes, forces a legacy-insert failure to prove rollback, races separate student/parent connections, verifies retry row versions remain unchanged, and exercises the unchanged staff read/save/lock followed by an approved timetable using the final assigned subject.

Application, EF Core, HTTP API, Host and HTTP test projects compiled with zero errors using bounded selective local builds. Focused validation retained the actual migration bodies, new designer and current model, while excluding historical migration designers to fit local memory; the normal full solution build and full repository test suites were not run. Existing warnings remain. No school/development database migration or staging deployment was performed. See the [mobile guide](../../../../Gyanmirai-Mobile/docs/mobile-academics.md) for Flutter validation and refresh behavior.

## Earlier academic checks

The leave-list regression `Leave_list_accepts_UTC_year_dates_and_preserves_school_date_boundaries`
in `MobileAcademicAppService_Tests.cs` covers academic-year dates with UTC and
unspecified kinds for both guardian and student reads. PostgreSQL materializes
year dates as UTC timestamps; the leave list must treat their date components as
school calendar dates before applying the India offset. Otherwise even an empty
list throws `ArgumentException` before reading leave records. The cases also
cover inclusive first/last school dates, UTC leave timestamps on either side of
the year, status filtering, pagination, and guardian-only edit flags. This fix
requires a backend deployment, with no schema migration or mobile API change.

On 2026-10-23, the regression reproduced two UTC-date failures before the fix
while the two unspecified-date cases passed. After the fix, all four cases and
the three existing attendance/guardian-leave cases passed (seven total). The
application build succeeded with existing warnings, using the bounded local
migration-designer target described below; the full backend suite was not run.
Read-only staging checks returned 500 for leave requests and 200 for overview,
calendar, and leave types. Deployed verification of the fix remains outstanding.

Submission regression cases are in `MobileHomeworkSubmission_Tests.cs` (the partial `MobileAcademicAppService_Tests` class), `MobileAnswerSanitizer_Tests`, and `MobileSubmissionRules_Tests`. They cover HTML/Unicode limits, stable receipt replay, stale revisions, draft/final separation, immutable files, teacher review, revoked-link replay, homework/assignment requirements, and deadline boundaries. The PostgreSQL HTTP fixture additionally applies `MobileHomeworkSubmissionReliability` and exercises repeated multipart uploads, simultaneous first-submit retries, competing replacements, and a response UPDATE paused while teacher review commits on another connection.

Completion acknowledgment checks cover homework and assignments in both In person / No submission required modes: empty unchecked drafts, required final acknowledgment, optional evidence, persisted response reads, immutable snapshots, safe retries, a first submission after the deadline, and overdue edit rejection. The PostgreSQL fixture also executes `MobileHomeworkCompletionAcknowledgment` against the predecessor schema. On 2026-10-22, ten focused application cases, five domain cases, and three HTTP cases passed. Mobile checks passed 76 focused Cubit/API/widget cases, including View submission navigation for all four modes, actual saved dates, formatted answers, optional files, and read-only responses. The wider mobile run was stopped because of local resource contention; these focused files completed with serial execution.

The overdue-policy regression covers homework and assignments in both text and file modes: allow a first submission after the deadline (including draft restoration and uploads), retain safe receipt replay, reject another first submission, reject drafts/edits after finalization, and record the response as late. Domain cases check the exact due-time boundary for edits. Mobile Cubit and detail-screen cases follow server permissions for late first submissions, permitted edits, and read-only overdue final submissions.

On 2026-10-22, all six focused application cases, three domain cases, and 29 mobile Cubit/widget cases passed for the overdue policy. Flutter analysis reported no issues and module documentation validation passed. Backend validation used the same bounded migration-designer build described below; no additional migration is needed for this policy change.

The authenticated development mobile session was also checked against the running local backend for the earlier overdue-policy fix. Its older process still returned `canSubmit: false` and `DeadlinePassed` for unsubmitted overdue work. After restarting that service with the updated build, fresh requests from the same signed-in mobile session returned `canSubmit: true` with no lock reason for overdue online-text and file-upload tasks. In-person and no-submission-required tasks were still unavailable at that point; the subsequent acknowledgment extension now permits their completion responses. No student response was submitted during that development-session check.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~MobileAcademicAppService_Tests|FullyQualifiedName~MobileAnswerSanitizer_Tests" -m:1 -p:UseSharedCompilation=false
dotnet test test/ShikshaMitra.Domain.Tests --filter FullyQualifiedName~MobileSubmissionRules_Tests -m:1 -p:UseSharedCompilation=false
```

The EF project includes a large historical migration source set. Keep local build concurrency bounded on memory-constrained machines. PostgreSQL validation must use the fresh disposable database convention described below; never point it at a school database.

Submission validation on 2026-10-21 passed 42 focused application cases, three domain cases, and three HTTP cases (the PostgreSQL workflow and two conflict-mapping cases). The PostgreSQL workflow applied the new migration and verified upload replay, simultaneous first submissions, competing edits, first-submission timestamp preservation, immutable attachment protection, and a concurrent teacher review. Flutter validation passed 114 homework/API tests, subsequent focused checks passed after the final error-mapping change, and `flutter analyze` reported no issues. Both submission themes were rendered and inspected.

For local submission validation, an ignored build target excluded historical migration designer files except the exercised migrations; migration bodies and the current model snapshot remained included. The normal full migration-designer compilation and full repository test suites were not validated. The initial 2026-10-21 test run did not apply a submission migration to the application database. The local development database now has both submission migrations, including the two acknowledgment columns, applied without seeding. Remote deployment remains outstanding. Repository-wide documentation validation still reports two pre-existing missing student test-result links.

`test/ShikshaMitra.Application.Tests/Academic/Mobile/MobileAcademicAppService_Tests.cs` covers student/guardian reads without staff grants, saved subject colours in the Syllabus dashboard, chapter publication and audience across dashboard/list/details, revoked/unlinked denial, draft/final persistence and locks, unpublished/withheld marks, generated PDF bytes, legacy attendance dates, guardian-only reasons, leave school-day duration and half-day times, school decision locks, and foreign attachment references.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~MobileAcademicAppService_Tests
dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module mobile-academics
git diff --check
```

All 20 focused cases passed on 2026-10-16 using the normal test project with `--artifacts-path .artifacts/syllabus-publication -m:1 -p:UseSharedCompilation=false`. The publication cases cover Draft, In Review, Archived, Published, Teacher/Students/Parents/All audiences, hidden chapter details, and visible zero-progress chapters. The full application suite was not run. Repository-wide build warnings remain. SQLite application tests validate behavior; the additional HTTP test below uses real PostgreSQL.

`test/ShikshaMitra.HttpApi.Tests/Academic/Mobile/MobileAcademicPostgresApi_Tests.cs` passes on PostgreSQL 18.6. It creates a fresh loopback `mobile_academic_tests_*` database and refuses existing databases. It recreates the predecessor columns and executes the actual `MobileAcademicResponses` migration operations. It then verifies `RepairMobileLeaveHalfDayTimes` against both complete and missing half-day columns, repeated application, and recorded migration history. It tests all seven root routes plus the attendance calendar, persisted-link denial, two simultaneous leave requests reaching transaction locks on different connections, one surviving record, half-day times, cancellation, multipart upload, exact downloaded bytes, deletion, and a generated PDF report. Authentication headers come from the HTTP test harness; this is local application integration evidence rather than deployed OIDC evidence.

Set `MOBILE_ACADEMIC_TEST_POSTGRES` to a connection string for a fresh disposable loopback database before running:

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~MobileAcademicPostgresApi_Tests --artifacts-path .artifacts/mobile-academics-http -m:1 -p:UseSharedCompilation=false
```

The automated test does not migrate or query a school database. Separately, the local development database was inspected after the reported `42703` error: migration history included `MobileAcademicResponses`, but both half-day columns were absent. A schema backup was saved in ignored local artifacts, the repair migration was applied transactionally without seeding, and both existing leave records were retained. Authenticated requests to the running host returned HTTP 200 for attendance calendar (October 2026, 31 days), overview, and leave requests. No host restart was needed. Remote database migration and API deployment remain release steps.

The [mobile guide](../../../../Gyanmirai-Mobile/docs/mobile-academics.md) owns Flutter analysis, regression commands, and browser evidence. Run an authenticated integration smoke after deploying the migration and backend, including child switches and authorized binary downloads.
