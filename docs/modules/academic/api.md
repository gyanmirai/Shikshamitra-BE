---
module: academic
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-28
owner: backend-team
audience: humans-and-agents
---

# Use the Academic setup APIs

TL;DR: four authenticated endpoints expose school-wide visit status and year-specific advisory guide state; they do not save operational configuration forms.
Contracts live in `src/ShikshaMitra.Application.Contracts/Academic/Setup/AcademicSetupDtos.cs` and `src/ShikshaMitra.Application.Contracts/Academic/Setup/IAcademicSetupAppService.cs`.
Operational workspaces, lookups, paged histories and report reads are documented in [UI read contracts](./references/ui-read-performance.md).

## Endpoints

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
