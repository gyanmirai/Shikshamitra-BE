---
module: academic
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-11-19
owner: backend-team
audience: humans-and-agents
---

# Validate Academic setup behavior

TL;DR: run the guide's real API/service boundary suite and isolated Host build, then verify frontend workflows separately; PostgreSQL parallel-visitor acceptance remains unverified.

## Focused commands

Academic Insights tests are in `test/ShikshaMitra.Application.Tests/Academic/Dashboard/AcademicInsights*_Tests.cs`.
The contract suite covers required context, exact scope wildcard boundaries, denied-feature query suppression, revocation, personal-account entry selection, sibling-only navigation, technical error isolation, owner filters, latest progress, and assignment all-target scope.
The disposable SQLite query suite checks direct application identity denial, invalid hierarchy/missing scope, cross-tenant/year/division lesson isolation, and execution of empty aggregate/scatter queries.
Calendar query cases cover exclusion of generated weekends before the five-entry limit, retaining real Sunday events and other holidays, date order, past/foreign-division exclusion, and a weekend-only empty result.
Personal calendar projection cases verify the same limit and filtering against the full verified monthly source, including unordered input and past entries.
These checks do not establish PostgreSQL performance or deployed/browser acceptance.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AcademicInsights --artifacts-path .artifacts/academic-insights
dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj --artifacts-path .artifacts/academic-insights
```

Run from `Shikshamitra-BE` with the .NET 9 SDK selected by `global.json`:

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AcademicSetupGuideApi_Tests --artifacts-path .artifacts/academic-setup-guide
dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj --artifacts-path .artifacts/academic-setup-guide
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module academic
git diff --check
```

Keep generated logs and test output under the ignored artifacts directory.
No runtime database, migration or seeder run is needed for this feature's schema.
The API fixture uses disposable synthetic SQLite data.

## Executable coverage

Tests live in `test/ShikshaMitra.HttpApi.Tests/Academic/Setup/AcademicSetupGuideApi_Tests*.cs`.

| Concern | Evidence |
|---|---|
| First school visit | Repeated GETs do not write; first POST wins across accounts; another year does not reset the marker |
| Action authorization | Exact setting View required; Dashboard/module visibility does not substitute for it |
| Identity and tenancy | Anonymous, host, inactive and forged cross-tenant accounts rejected |
| Context and scope | Foreign years/media, wrong year-medium relationship, missing scope and sibling classes rejected |
| Real configuration | All nine record families affect their selected context; archived subjects do not count |
| Progress | Shared year state, stale-stamp rejection, server review timestamp and review clearing |
| Data preservation | Sibling ExtraProperties and hidden deferred keys survive permitted writes |
| Invalid stored state | Malformed marker and unsupported/incomplete progress JSON fail without resetting data |
| Direct application boundary | Identity enforcement holds without an HTTP controller |
| Visit retry | Synthetic repository collision exercises the actual service, fresh UOW and preserved winning actor/time |

The focused suite passed 15 tests on 2026-10-28.
The final isolated Host build passed with zero errors; earlier full compilations exposed existing repository warnings.
These results do not establish deployed acceptance.

## Homework school-date and calendar checks

Run `dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~HomeworkCalendarDate_Tests|FullyQualifiedName~LearningTaskCalendarReadPerformance_Tests|FullyQualifiedName~LearningTaskScheduleTimestampTests" --artifacts-path .artifacts/homework-calendar-tests -p:RunAnalyzers=false`.

Coverage includes IST midnight versus the previous UTC date, valid and blocked selected dates, calendar changes after draft creation, inactive calendars, other media/divisions, UTC storage round trips and one batched marker query across division schedules. These SQLite tests do not establish deployed PostgreSQL acceptance; the [frontend testing guide](../../../../Shikshamitra-FE/docs/academic/testing.md) owns browser evidence.

## Elective late admissions

`StudentElectiveCorrectionAppService_Tests` covers forged admin claims, foreign context/tenant rejection, missing review, new marks after preview, published marks/frozen results/seating preservation, immutable original preference, retained lock, audit actor and replay rejection. `PortalElectivePreferenceAppService_Tests.Corrected_preference_is_visible_while_original_submission_remains_immutable` covers portal readback.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~StudentElectiveCorrectionAppService_Tests|FullyQualifiedName~StudentElectiveAssignmentAppService_Tests|FullyQualifiedName~StudentElectiveReadPerformance_Tests|FullyQualifiedName~PortalElectivePreferenceAppService_Tests" --artifacts-path .artifacts/elective-correction-tests -p:RunAnalyzers=false -p:UseSharedCompilation=false -m:1
```

Migration `AdminLockedElectiveCorrections` adds the preference override and concurrency stamp plus the correction audit table. Apply it before deploying the new API. Local SQLite tests validate behavior; PostgreSQL concurrency under simultaneous writers requires environment validation.

Elective late-admission regression: `StudentElectiveAssignmentAppService_Tests.New_Admission_Should_Remain_Editable_Without_Unlocking_Existing_Students` covers initial locking, admission, search-independent lock status, incomplete-roster rejection, saving and locking new selections, and rejection of changes/deletions to locked selections. Run it with the shared-option/count regressions:

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~StudentElectiveAssignmentAppService_Tests|FullyQualifiedName~StudentElectiveReadPerformance_Tests" --artifacts-path .artifacts/elective-lock-tests -p:RunAnalyzers=false
```

## Homework and assignment student rosters

`FeedbackStudentRoster_Tests` exercises both feedback services against disposable SQLite data.
It reads a task before and after an active student with a pending legacy approval flag is added, checks the alphanumeric GR number and missing-submission summary, and excludes inactive, departed, pending-admission, deleted, other-tenant, other-medium, other-class and other-division students.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter FullyQualifiedName~FeedbackStudentRoster_Tests -p:RunAnalyzers=false -p:UseSharedCompilation=false -m:1
```

## Class timetable lookup reads

`TimetableClassReadPerformance_Tests` checks the real application/SQLite response before and after unrelated subject, elective-component and room catalogues grow.
It compares the complete serialized DTO, literal slot/summary behavior, database-reader rows and tracked lookup entities.
The configured and saved-only variants explicitly restore their test cultures and retain colon/dot time formatting.
Coverage includes archived/inactive labels, draft saved allocations, complete published summaries, selected-but-unsaved subjects without an assignment, empty configured placeholders, missing-reference fallbacks and existing negative remaining-slot totals.
The companion elective cases protect newly selected subjects from inheriting another subject's saved teacher or room.
These checks do not establish PostgreSQL query plans, production latency or deployed acceptance.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~TimetableClassReadPerformance_Tests|FullyQualifiedName~TimetableElectiveAssignments_Tests|FullyQualifiedName~TimetableSettingsReadPerformance_Tests" --artifacts-path .artifacts/timetable-class-read-tests -p:RunAnalyzers=false -p:UseSharedCompilation=false -m:1
```

## Timetable conflict lifecycle

`TimetableConflictQuery_Tests` covers draft and `NotCreated` exclusion, published overlap detection, parent soft deletion, current-class exclusion, adjacent periods and tenant isolation for teacher and room reservations. `TimetableGhostConflict_Tests` exercises validation, saving and direct teacher assignment against draft versus published competing timetables, including saved-assignment readback. Existing elective and teacher reassignment suites protect real conflicts and same-period duplicate assignments.

The cross-board/medium cases create a published GSEB/Gujarati competing timetable and verify that teacher and room errors identify its year, board and medium. These cases must continue to reject the overlap, even when the caller is editing another academic context.

```powershell
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests --filter FullyQualifiedName~TimetableConflictQuery_Tests -p:RunAnalyzers=false -p:UseSharedCompilation=false -m:1
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~TimetableGhostConflict_Tests|FullyQualifiedName~TimetableElectiveAssignments_Tests|FullyQualifiedName~SubjectTeacherReassignmentSafety_Tests" -p:RunAnalyzers=false -p:UseSharedCompilation=false -m:1
```

## Concurrency and provider limits

The normal API fixture shares one in-memory SQLite connection.
Simultaneous HTTP transactions fail in provider transaction creation before the visit service, so that fixture does not establish parallel PostgreSQL behavior.
The suite covers two clients with pre-visit snapshots and injects an optimistic repository collision into the real visit service to verify its fresh-UOW retry contract.
Run a disposable PostgreSQL acceptance check before claiming provider-level parallel-visitor evidence.
Do not repurpose a deployed school database for that test.

## Staff notification configuration checks

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~AcademicNotificationStaffApi_Tests|FullyQualifiedName~AcademicNotificationRulePreviewApi_Tests|FullyQualifiedName~AcademicNotificationManualApi_Tests|FullyQualifiedName~LearningTaskNotificationEligibility_Tests"
```

The authenticated API fixture covers catalogue response fields, template initialization and synthetic preview, action permissions, published revision immutability, stale writes, disabled overrides, hierarchy validation and unsupported rule options.
The Homework feedback fixture covers the first Reviewed status, repeated feedback edits, draft and submitted rubric evaluations, and rubric approval. It checks the exact student and division and verifies that the source variables exclude marks and feedback text.

Assignment publication coverage exercises the staff status route and scheduled publisher with staggered divisions. It verifies that the first release targets its own learners and class teacher, that the later release targets its own division, and that repeated scheduler runs create no duplicate intents.

The Homework Due and Assignment Due fixtures enroll an eligible learner after the first reminder settles. They verify that the scheduler queues just the new learner under the original operation, a repeat scan adds no records, and an authorized staff cancellation stops future additions even after delivery work has settled.

Feedback release coverage exercises plain feedback and rubric approval for both Homework and Assignment. It checks draft and submit-for-approval steps, repeated edits, recipient isolation, and omission of grades and feedback text from notification data. Assignment checks also cover notification eligibility after closing, the exact history permission, and allowed and denied division viewers.

Revision request tests exercise the feedback routes for both task types starting from a submission whose status was set without feedback. They verify one notice per submitted attempt, a second request after resubmission, suppression after resubmission or final review, and omission of private notes and learner work. Assignment history also checks the exact permission and submission division.

Overdue coverage runs the scheduler over real Homework and Assignment schedules. It verifies fixed deadline timing, publication and division boundaries, duplicate-scan protection, a single assigned-teacher summary with a scoped count, and a fresh roster after late submission or excuse. A family template can receive only its own learner's missing count. An overdue intent expires 24 hours after the saved deadline.

Submission digest coverage exercises both task types at a saved daily cutoff. It checks inclusion at the start, exclusion at the cutoff, draft and elective exclusions, one teacher operation per window, and rechecking enrollment before preparation. The digest payload contains a count and omits learner work.

The Homework publication fixture verifies zero draft intents, the staff publication route, the configured family and teacher scopes, sibling exclusion, repeat publication, and reassignment before dispatch. Persisted Homework reminder tests cover published deadlines, current enrollment, locked elective choices, completed submissions, tenant boundaries and Learning Tasks View. Scheduler checks cover bounded pages of current and legacy schedules, deadline changes, exact division scope, duplicate scans, and removing a learner who submits after capture. Assignment reminder coverage includes future publication dates, assignment-specific submissions, exact manual preview access, and a real queued intent that rechecks the roster. The source fixture exercises sibling-division exclusion, primary and optional family selection, source authorization and the ready-delivery count. It verifies that preview neither stores work nor returns actual recipient identities, contacts, or source text.
Manual preview tests check independent View and Send grants, source-bound selections, the persisted expiry and digest, contact and source changes, and the approval threshold. The test verifies that a review creates no campaign or queued intent.

Confirmation tests exercise Send without Add, exact replay, stale and expired reviews, approval and rejection, self-approval denial and the worker's delivery check after confirmation. Run the PostgreSQL test `ReviewedAcademicNotificationMigrationPostgres_Tests` with the local notification test database to verify that migration preserves existing automatic work and allows reviewed manual work without an automatic rule.

History tests cover independent View and source authorization, per-scope attempts, cross-scope aggregate denial, page and status filters, and accepted-versus-delivered timestamps. The fixture includes sensitive stored values to verify they stay out of the response.

Delivery action tests cover Add and source access, retry scheduling after a definitive transient rejection, unchanged successful channels, rejection of unknown acceptance, cancellation before planning or claim, and stored audit metadata.

Timetable notification tests exercise draft, publish, reordered re-save, a teacher change, an elective option removal and withdrawal through the save API. The workflow checks exact teacher and learner targets, safe former-teacher content, enrollment revocation, superseded publications and the history permission. A separate application test exercises a published teacher change from Curriculum allocation.

The hosted timetable workflow also verifies the automatic day reminder, including school-local timing, elective-aware recipients, per-recipient period fields, duplicate scans and suppression after a scoped closure. `NotificationReminderTimingPolicy_Tests` covers the reminder cutoff, weekends, closure days and zero or multiple preceding school days.

`RoomReservationNotificationDelivery_Tests` uses the actual reservation routes and a calendar closure. It checks requester decisions, rejection of self-review routing, configured reviewer changes, material versus cosmetic edits, removal-safe teacher content, cancellation, series deduplication and the history permission.

`ResultNotificationPrivacy_Tests` exercises the actual first-publication endpoint. It covers generated and repeated publication, withheld and out-of-division results, enrollment revocation, generic variables and staff history access.

`ExamNotificationEvents_Tests` exercises the real exam publish action and the due scheduler. It covers repetition, class and division staff assignments, separate locked-elective sittings, school-local timing, calendar closure suppression and Schedule Exam View for history.

`SeatingNotificationEvents_Tests` uses the existing exam and seating publication actions in both orders. It verifies repeat publication, one-learner seat projection, saved timing, student-only template selection, email exclusion and unavailable exam-staff delivery. The hosted exam tests verify the seating history permission. Run `SeatingNotificationPostgresProbe_Tests` against a fresh disposable PostgreSQL fixture to verify concurrent exam and seating publication queues one operation.

`CurriculumNotificationEvents_Tests` covers class teacher confirmation, repeated saves, silent drafts, replacement, removal, generic outgoing content and suppression of older revisions. `ClassTeacherNotificationDelivery_Tests` verifies the canonical save API with an assignment created before notice metadata existed and checks staff history permissions. `ClassTeacherNotificationPostgres_Tests` runs the workflow with concurrent confirmations against a disposable PostgreSQL database.

`SubjectAllocationNotificationEvents_Tests` covers publication, teacher changes, repeated saves, locked elective withdrawal recipients and suppression after restoration. `SubjectAllocationNotificationDelivery_Tests` verifies publication and archival through the hosted Curriculum API alongside source history access. `SubjectAllocationNotificationPostgres_Tests` runs the same saved workflow on a fresh disposable PostgreSQL database.

`ElectiveSelectionNotificationEvents_Tests` checks that a repeated lock creates one notice for the learner whose selection was newly confirmed. `ElectiveCorrectionNotificationDelivery_Tests` exercises the lock and correction endpoints, exact learner and staff references, history permissions and invalidation of earlier pending notices. `ElectiveCorrectionNotificationPostgres_Tests` includes concurrent repeat locks in a clean PostgreSQL database. `StudentElectiveCorrectionAppService_Tests` also covers invalid or stale review, replay, and preservation of existing academic records and the preference receipt.

`LessonPlanSubmissionNotificationEvents_Tests` covers explicit submission, creation as Submitted and grouped bulk submission with an eligible configured reviewer. The hosted lesson plan workflow also checks that reviewing one submitted plan leaves the remaining plan eligible for the grouped notice, and that a changed reviewer configuration does not reroute it.

`LessonPlanNotificationEvents_Tests` covers the three review outcomes, unchanged repeats and the exact plan owner. `LessonPlanReviewNotificationDelivery_Tests` and `LessonPlanReviewNotificationPostgres_Tests` exercise scoped review and history through the hosted API, private review remarks and suppression after a revision.

`TeacherDiaryFollowUpNotification_Tests` verifies that submission makes the diary eligible for an explicit follow-up, creates no intent and keeps the diary's private content out of templates. `TeacherDiaryFollowUpNotificationDelivery_Tests` and `TeacherDiaryFollowUpNotificationPostgres_Tests` exercise the hosted staff roster, permission and recipient checks, template preview and confirmation.

`AcademicProgressDigest_Tests` covers the school cutoff, duplicate suppression, and silence when dates are missing or removed. `AcademicProgressMilestoneDigest_Tests` covers the first recorded threshold crossing, later recrossings, the scoped intent and private audit exclusions. `AcademicProgressDigestDelivery_Tests` and `AcademicProgressDigestPostgres_Tests` exercise both digests with the scoped roster, published chapter counts, source changes and protected operation history.

`AcademicCalendarNotificationEvents_Tests` verifies that a saved draft stays silent, repeated publication queues one notice for the saved scope, and archiving suppresses it. `AcademicCalendarPublicationDelivery_Tests` and `AcademicCalendarPublicationPostgres_Tests` exercise the HTTP publication and archive actions, exact staff and enrolled learner references, the primary guardian default and protected history.

`SyllabusNotificationEvents_Tests` covers publication, suppressed repeats and progress edits, assigned staff and material revisions. `SyllabusNotificationDelivery_Tests` and `SyllabusNotificationPostgres_Tests` exercise the hosted chapter create and update workflow, exact locked elective eligibility, parent-only delivery limits, history authorization and suppression after returning to draft.

`DivisionSubjectNotificationEvents_Tests` covers replacement and removal through the application service. `SubjectAllocationNotificationDelivery_Tests` also exercises the hosted division-teacher API, including unchanged teacher sets and HTTP validation of a null list, a sibling division, generic outgoing content, superseded pending work and history permissions. `DivisionSubjectNotificationPostgres_Tests` covers concurrent replacements on a fresh disposable PostgreSQL database.

`MarksNotificationEvents_Tests` exercises draft saves, submission, correction, resubmission and verification through Record Marks. `MarksNotificationDelivery_Tests` verifies the persisted reviewer and teacher audiences, source permission for history and deactivated account suppression. `MarksNotificationPostgres_Tests` runs the hosted workflow against a disposable PostgreSQL database and checks that saved audit revisions still match when read in a new unit of work.

Substitution notification tests cover silent and repeated assignments, exact staff targeting, queued and provider-accepted status, shared scheduled and explicit reminder identity, and suppression after acknowledgement. The hosted workflow also exercises material rescheduling, no-op edits, recipient-specific removal content, cancellation for exactly the previously informed staff, and the exclusion of unconfirmed acceptance.

Reviewer escalation cases verify backward-compatible rule decoding, required teacher and reviewer selectors, a positive delay, no early or duplicate reviewer intent, separate role targeting, and suppression after the substitute responds.

Staff API tests check stored audit fields for rule saves, template publication and manual approval decisions. They check audit counts after stale or repeated requests and verify that the serialized fields contain only approved metadata.

Template test-send tests use synthetic draft content and the authenticated account. They verify the local inbox item, exact Add permission, rejected destinations and stale revisions, the per-channel limit and required WhatsApp test-account setup.

## Website push checks

Run the backend delivery, subscription protection and authenticated registration tests from the backend repository.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~WebPushNotificationChannel_Tests|FullyQualifiedName~WebPushSubscriptionProtection_Tests|FullyQualifiedName~NotificationDispatchPlanner_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~NotificationSelfApi_Tests_Push"
```

The provider fixtures cover signed encrypted requests, delivery-time DNS checks, rejection, throttling, registration expiry and account changes.
The key-ring test reloads the same directory after rotating the Data Protection key and reads the original browser subscription from a fresh service provider.
Production acceptance requires replica and restart verification with the deployed key repository, plus an actual notification on supported desktop and mobile browsers.

## Room calendar regression

`RoomCalendarContext_Tests.cs` covers the 31 October 2026 ghost-vacation regression across direct checks, room lists, timeline periods, recurring sessions and saved bookings, including selected-medium isolation, live day deletion and adjacent-date boundaries.
Run it with the surrounding room regressions:

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~RoomAvailabilityAppService_Tests" -p:RunAnalyzers=false
```

## Security gate and known baseline

Follow the [security standard](../../common/security/permission-consistency.md) and the reusable workflow in `.github/workflows/security-regression.yml` before merging.
A focused local domain subset covering `UserScopeResolution_Tests`, `SecurityPermissionSeeder_Tests`, `SecurityReleaseWorkflow_Tests`, `RolePermissionSeederContributor_Tests` and `RolesSeederContributor_Tests` yielded 54 passes and one existing failure on 2026-10-28.
`SecurityReleaseWorkflow_Tests.Deployment_Requires_Security_Gates_Before_Migration_And_Seeding` expects a reusable security dependency that the current deployment workflow lacks.
That workflow and test were unchanged by the guide implementation.
The complete security workflow, PostgreSQL gates and deployed browser acceptance were not established by the backend guide tests.
Frontend checks and browser evidence belong to the [frontend testing guide](../../../../Shikshamitra-FE/docs/academic/testing.md).
