---
module: academic
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-30
owner: backend-team
audience: humans-and-agents
---

# Use the Academic setup APIs

TL;DR: four authenticated endpoints expose school-wide visit status and year-specific advisory guide state; they do not save operational configuration forms.
Contracts live in `src/ShikshaMitra.Application.Contracts/Academic/Setup/AcademicSetupDtos.cs` and `src/ShikshaMitra.Application.Contracts/Academic/Setup/IAcademicSetupAppService.cs`.
Operational workspaces, lookups, paged histories and report reads are documented in [UI read contracts](./references/ui-read-performance.md).

## Endpoints

### Academic Insights

`GET /api/academic/dashboard/insights` accepts required non-empty GUIDs `academicYearId`, `boardId`, and `educationMediumId`, plus optional `standardId`, `divisionId`, and `studentId`.
It returns `role`, `scopeLabel`, UTC `generatedAt`, `sections`, and an authorized `students` selector list for personal accounts.
Each section contains `key`, `status` (`ready`, `empty`, `unavailable`, or `error`), `summary`, numeric nullable `metrics`, named chart `series`, and bounded `items`.
Points provide `key`, `label`, numeric `value`, optional numeric `x`/`y`, and optional `status`.

Staff reads require the Academic Dashboard View action, an active current-tenant identity, a matching year/board/medium hierarchy, and server-resolved UserScope.
Every aggregate additionally requires its actual feature View action before querying that source.
An independently permitted sibling workflow can return a navigation-only `unavailable` section with no metrics or records; otherwise denied sections are omitted.
Technical source errors are logged and return an `error` section without discarding other available sections. Business, identity, authorization, context, validation, and cancellation failures propagate.
Role names only choose presentation order. Active class teacher assignments determine the class teacher presentation label.
Missing scope and foreign hierarchy selections fail closed; `Academic:Insights:InvalidContext` identifies an inconsistent selected hierarchy.

| Section | Source and contract |
|---|---|
| `classrooms` | Active class teacher mappings; `class-mapping` preview is limited to 120 divisions |
| `student360` | Scoped enrolled students; `attendance-score` matches credit-aware submitted attendance in the last 30 days with published/frozen, non-withheld exam results; 60 points maximum; student totals are independent of that preview |
| `calendar` | Selected-context calendar entries applying to authorized classes/divisions or the whole calendar; generated weekend holidays are excluded before counting and selecting the next five by date/ID. Actual events and other holidays on weekend dates remain eligible. |
| `curriculum` | Latest recorded chapter/division progress (creation time, then ID tie-break), joined through tenant-owned chapters and assigned subjects; `recorded-progress` is 0-100 percent, with no invented planned target; 30 chart subjects maximum |
| `lesson` | Lesson plan counts grouped by review state in authorized year/divisions, limited by the owning service's own/reporting-teacher resolution and exact active class teacher divisions; only server-resolved administrator scope bypasses that ownership filter |
| `timetable` | Slot counts grouped by day and sequence; `periods.x` uses .NET day numbering (Monday 1 through Saturday 6), `y` is sequence; this is not a conflict count |
| `tasks` | Parent aggregates first pass the owning homework and assignment access policies, including all-target division rules and established homework subject ownership; published schedules then group into `overdue`, `today`, and `upcoming` due-date buckets |
| `evaluation` | Received submissions from the same authorized parents grouped by recorded review/grade timestamp; `no-recorded-review` does not imply evaluation is required or pending |
| `exams` | Exam stage counts whose class and assigned divisions match the selected context |
| `reports` | The caller's latest 100 completed exports, restricted to permitted report families and exact stored dynamic year/board/medium; legacy jobs without verifiable context are excluded |
| `notifications` | Four existing read-only browser template categories; no fabricated template counts, save operation, or automatic sending |
| `audits` | Existing academic audit service with exact selected year/medium; total plus 10 recent items |
| `settings` | The exact nine setup setting existence checks, filtered by each setting's View permission; class-specific checks require a selected class |

The existing report-job and audit sources cannot establish a class/division authorization boundary. Those two sections return `unavailable` for narrower scopes.
Setting existence does not establish operational readiness.
No source read changes data, grants, seed state, or schema.

Accounts with exactly one persisted `Student` or `Parent` role follow the existing portal-only account definition and use verified portal association/mobile placement authorization, even when explicitly granted Dashboard View.
Role selection never authorizes student data by itself. Mixed staff/parent accounts retain the staff path and require Dashboard View; a missing staff grant never falls back to child data.
Their response contains only personal attendance/results, curriculum, learning tasks, exams, calendar, and seven-day timetable data already available through those services.
Multiple linked students without a selection return `scopeLabel: "Select a student"`, the authorized selector list, and no sections.
A selected student must belong to that persisted association and exactly match the requested context; no module grant is inferred from a personal association.
Personal preview limits and date windows are stated in section summaries.
The personal calendar reads the verified full month before excluding generated weekend holidays and selecting five upcoming entries; it does not re-filter an already truncated dashboard preview.

| Method and route | Input | Result |
|---|---|---|
| `GET /api/academics/setup/visit-status` | None | `hasVisitedConfiguration`, `canAccessSetup`, nullable `firstVisitedAt` |
| `POST /api/academics/setup/visit` | Optional `settingKey` in a JSON object | `firstVisit`, `hasVisitedConfiguration`, nullable `firstVisitedAt` |
| `GET /api/academics/setup/{academicYearId}` | Optional query `academicMediumId`, `standardId` | Guide state |
| `PUT /api/academics/setup/{academicYearId}/progress` | Progress JSON below | Updated guide state |

Visit-status GET is read-only.
For an active school account with no eligible configuration View action, it returns `canAccessSetup: false`, `hasVisitedConfiguration: false` and `firstVisitedAt: null` without disclosing stored marker state.
A false visit value is authoritative for gating only when `canAccessSetup` is true.
POST accepts a missing or null `settingKey`, or `welcome`, when any eligible configuration View is granted.
A setting key requires that exact setting's View action.
Only the first successful writer receives `firstVisit: true`.
The stored actor identifier is never returned.

## Progress and response

```json
{
  "concurrencyStamp": "<current academic-year revision>",
  "currentStageKey": "curriculum",
  "currentSettingKey": "subjects",
  "deferredSettingKeys": ["elective-groups"],
  "finishReview": false,
  "academicMediumId": "<selected medium GUID>",
  "standardId": null
}
```

Progress accepts the six exact stage keys in [product.md](./product.md).
`currentSettingKey` may be null for any valid stage, including a stage with no visible settings.
A supplied setting must belong to its stage and be viewable by the caller.
Deferred keys must be unique, known, viewable and limited to nine.
`finishReview: true` is valid only at `review`; the server supplies review time and actor.
The request's context fields have the same scope validation as the GET query.

| State field | Meaning |
|---|---|
| `academicYearId`, `concurrencyStamp` | Tenant-owned year and current revision |
| `currentStageKey`, nullable `currentSettingKey` | Shared guide cursor; a hidden current setting is returned as null |
| `deferredSettingKeys` | Only keys the caller may view |
| nullable `reviewedAt` | Advisory guide review timestamp |
| `settings` | Nine entries with `key`, `canView`, `canManage`, `configurationState` |

`canManage` requires both View and Add for that setting.
The four configuration-state values are defined in [product.md](./product.md).
Responses contain no configuration counts or record identifiers.
A partial-permission save preserves deferred keys hidden from that caller.

## Exact keys and permissions

Each permission suffix below has the exact prefix `Academics.`.
View grants entry and status access; Add governs the existing configuration save operation and `canManage`.
The guide does not grant Add or call those operational save APIs.
Module visibility is separate from action authorization.

| Setting key | Stage | Permission suffix before `.View` or `.Add` |
|---|---|---|
| `subjects` | `curriculum` | `CurriculumManagement.Settings.SubjectCreation` |
| `elective-groups` | `curriculum` | `CurriculumManagement.Settings.ElectiveGroupCreation` |
| `weekly-lectures` | `timetable` | `Timetable.Settings.WeeklyLectures` |
| `lecture-slots` | `timetable` | `Timetable.Settings.LectureSlots` |
| `homework-types` | `homework` | `HomeworkAndAssignment.Settings.HomeworkTypes` |
| `assignment-types` | `homework` | `HomeworkAndAssignment.Settings.AssignmentTypes` |
| `rubrics` | `homework` | `HomeworkAndAssignment.Settings.RubricConfig` |
| `grading-scales` | `examination` | `Examination.Settings.GradeScaling` |
| `exam-types` | `examination` | `Examination.Settings.ExamTypes` |

## Security and context

Every endpoint requires an authenticated, active Identity account whose tenant agrees with the current server tenant.
Host context and forged cross-tenant identities are rejected.
Year state and progress require at least one eligible setting View and scope in the requested tenant-owned academic year.
A selected medium must belong to a board in that year and be within the caller's effective hierarchy.
A selected class requires a medium, must belong to that medium and must be accessible through the caller's exact class or applicable wildcard scope.
Narrow class access does not expose sibling-class timetable state.
Medium-level configuration can be inherited by an authorized child scope.
See the [canonical security standard](../../common/security/permission-consistency.md).

## Conflicts and errors

| Status or code | Meaning |
|---|---|
| 401 | Unauthenticated request |
| 403 | Inactive/mismatched school identity, missing View or inaccessible scope |
| 404 | Year, medium or class is absent from the required tenant/context relationship |
| 409 | Stale year revision or exhausted optimistic visit retry |
| `ShikshaMitra:AcademicSetup:Invalid` | Unknown key, invalid stage/setting pairing, duplicate deferred keys, missing medium for class or review outside its stage |
| `ShikshaMitra:AcademicSetup:InvalidStoredState` | Malformed, unsupported or invalid persisted metadata |

Validation and business errors return 400.
Visit conflicts retry with fresh units of work, up to three attempts, preserving the winning marker.
Progress conflicts require refetching the current year state before deciding whether to retry.
No request restores permissions, changes scope or resets school data.
