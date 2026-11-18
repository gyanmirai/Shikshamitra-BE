---
module: academic
domain: api
content_type: Reference
status: canonical
updated_on: 2026-11-19
owner: backend-team
audience: humans-and-agents
---

# Use the Academic setup APIs

TL;DR: four authenticated endpoints expose school-wide visit status and year-specific advisory guide state; they do not save operational configuration forms.
Contracts live in `src/ShikshaMitra.Application.Contracts/Academic/Setup/AcademicSetupDtos.cs` and `src/ShikshaMitra.Application.Contracts/Academic/Setup/IAcademicSetupAppService.cs`.
Operational workspaces, lookups, paged histories and report reads are documented in [UI read contracts](./references/ui-read-performance.md).

## Endpoints

### Staff notification configuration

Routes use the base path `/api/academic/notifications` and an authenticated, active school account. The action suffixes below refer to `Academics.Notification`.

| Endpoint | Action | Result |
|---|---|---|
| `GET /catalogue` | View | Event schemas, audience options, source availability and channel configuration flags |
| `GET /rules` | View | Paged event rules at a school, year, board, medium, class or division scope, with the effective rule and inheritance marker |
| `PUT /rules/{eventKey}` | Add | Save an explicitly scoped rule and return its revision and concurrency stamp |
| `POST /rules/{eventKey}/preview` | Add + source access | Preview a proposed `rule` for the given `sourceType` and `sourceId` |
| `POST /manual/preview` | View + Send + source access | Review a saved source audience, chosen channels and published templates |
| `POST /manual/send` | Send + source access | Confirm a `reviewToken` with an `idempotencyKey`. Returns 202 with `operationId` and `status` |
| `POST /operations/{id}/approve` and `/{id}/reject` | Add + assigned approver + source access | Decide an Academic operation in `PendingApproval` |
| `GET /operations/{id}` | View + source read access | Read the status, timestamps, delivery counts and intent outcomes for an operation within the caller's scope |
| `GET /history` | View + source read access | Page delivery attempts, work awaiting an attempt, and exclusions for the authorized source |
| `POST /dispatches/{id}/retry` | Add + source read access | Queue a verified retry of this channel's definitively rejected attempt |
| `POST /operations/{id}/cancel` | Add + source read access | Stop unclaimed work and return cancelled intent and dispatch counts plus remaining in-flight work |
| `GET /templates` | View | Paged template headers for the selected `eventKey` and optional `academicMediumId` |
| `GET /templates/{id}` and `/{id}/history` | View | Current template and revision history |
| `GET /templates/{id}/preview/{revisionId}` | View | Render with synthetic values for review |
| `POST /templates` and `PUT /templates/{id}/draft` | Add | Create or edit a draft |
| `POST /templates/initialize-defaults` | Add + school-wide scope | Create missing English variant drafts for one `eventKey` |
| `GET /templates/options` | View + template scope | Page published, non-retired variant names, channels and languages for an event without reading message content |
| `POST /templates/{id}/test-send` | Add + template scope | Queue a synthetic test for a single verified account |
| `POST /templates/{id}/publish` and `/{id}/restore-default` | Add | Publish an immutable revision or create a fresh draft from the system default |
| `DELETE /templates/{id}/draft` | Delete | Delete the selected unused draft |

Scope IDs form a contiguous tenant-owned hierarchy. A rule applies to the selected scope and its descendants. The nearest rule takes precedence, including a disabled override. Page items without a rule return a null `effectiveRule`. New overrides omit the concurrency stamp. Updates use the stamp of the exact rule being edited. A stale rule or draft write returns 409.

Rules default to the primary guardian for family events. Other saved guardian relationships and the student account can be selected when the event supports them. Enabling a rule requires an available source and valid published template coverage for its audiences, channels and fallback language. Channel flags report configuration status. WhatsApp also requires approval for the selected template revision before dispatch.

Rule preview uses the saved source audience within the proposed rule's scope. It reports eligible recipients after contact, preference, consent and timing checks, estimated ready deliveries per channel, and grouped exclusions. Ready delivery counts depend on published template coverage, channel setup, and current device or provider approval. Selected drafts can render fictional samples. The preview response omits recipient identities and destinations, and the request creates no rules, campaigns, or dispatches.

Manual preview accepts `eventKey`, `sourceType`, `sourceId`, `selectedRecipients`, `channels`, and optional `templateId`. Recipient references must come from the authorized source roster. An empty selection uses the source roster, subject to scope checks for every included segment. The in-app channel is required. The optional template selects a compatible published recipient, channel, and language variant. An effective rule supplies family preferences where present. Otherwise, the family audience defaults to the primary guardian.

The response reports recipient counts, exclusions, sample messages with fictional values, and whether the audience or a school approval rule requires approval. When at least one delivery is ready, it returns an opaque `reviewToken` and `expiresAt` up to ten minutes away, capped by source expiry. The server stores a digest bound to the actor, source revision, scoped audience, templates, and delivery evidence. Preview writes only the review receipt and makes no provider calls.

Confirmation opens the protected request attached to the token and checks the live source and reviewed delivery details. A material change, expired token, or reused token with a different key returns 409. Repeating the same token and key returns the existing operation. A `Queued` operation is awaiting delivery. The worker checks the reviewed delivery fingerprint again before creating dispatches.

Approval-required operations write no sendable intents until approval. Configure an active independent staff account at `Notifications:Academic:Tenants:<tenantIdN>:ApproverUserId`, where `tenantIdN` is the tenant GUID without hyphens. The assigned account needs Academic Notification Add and access to the source. Confirmation returns `Communication:Notification:ApprovalSetupRequired` if that account is unavailable. Approval rechecks the same preview. Rejection ends the pending operation as `Cancelled`.

Rule changes, template publication, test sends, confirmation, approval, rejection, retry and cancellation write an action audit in the same transaction as the change. Audit data is limited to the actor, tenant, action, relevant identifiers, revisions, status and counts. Replays of completed actions reuse the original audit history.

Template test-send accepts only `revisionId` and `concurrencyStamp`. It renders sample values, queues one delivery, expires after ten minutes and returns 202 with `isSynthetic: true`. In-app, email and web push use the authenticated account by default. To designate a test account, set `Notifications:Academic:Tenants:<tenantIdN>:TemplateTest:<channel>:UserId`. WhatsApp requires a designated account with a verified number, current consent and a published revision with an approved provider binding. Email uses the account's verified address and the existing provider's consent policy. Web push uses an active subscription for that account. The test-send limit is five queued test messages per school and channel in 15 minutes.

History takes `eventKey`, `sourceType`, `sourceId`, optional `operationId` and `state`, and standard `skipCount` and `maxResultCount` pagination. Staff see rows for their authorized source scopes. Whole-operation status and exclusions recorded before dispatch require access to every scope represented by that operation.

History keeps provider acceptance, delivery, read, failure, skips and unknown acceptance distinct. It returns recipient references, timestamps, and recognized reason codes. Message bodies, destinations and raw provider references or errors stay private.

A retry uses the original recipient and template. The saved attempt must show a recognized transient rejection with no provider acceptance. The service rechecks eligibility, consent, expiry and the provider route. It honors any saved retry window and allows one attempt beyond the automatic limit. A repeat request returns the existing queued retry. Ambiguous outcomes and changed delivery conditions return 409 with `Communication:Notification:RetryRequiresReview`.

Cancellation applies to unclaimed pending dispatches, unleased intents awaiting planning, and operations awaiting approval. It preserves earlier attempts and reports any claimed work still in flight. Both commands record the actor, operation and reason in the audit log.

Initializing defaults preserves previous edits and deletions and creates drafts without activating a rule.

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

## Timetable conflict context

Teacher and room validation errors retain their existing codes and serialized `data.ConflictDetails` array. Each detail also includes nullable `ConflictingAcademicYearName`, `ConflictingEducationBoardName`, and `ConflictingEducationMediumName`, resolved from the competing class's server-owned hierarchy. The contextual `data.message` includes the same labels. Older records with missing hierarchy labels retain the existing class/division fallback. Conflict checks continue across boards and media within the current tenant.

## Room calendar requests

For dated room operations under `api/academics/timetable`, `room-availability` and `room-availability/schedule` accept an optional `academicMediumId` query parameter.
Reservation create/update, conflict-check and room-suggestion request bodies accept the same optional field.
Physical room occupation, maintenance, tenant isolation and reservation access checks apply independently of the calendar medium.
See [room calendar context](product.md#room-reservation-calendar-context) for calendar selection and saved-booking rules.

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

## Student submission attachments

`GET /api/homework/submissions` and `GET /api/assignments/submissions` return `submissionAttachments` on each student row.
Each item contains `id`, `name`, `url`, `contentType`, and `size`; legacy single-file uploads may have null metadata.
The list maps every persisted mobile attachment, falls back to the legacy submitted file, and is empty for drafts or absent files.
URLs target the existing authenticated `GET /api/files/{fileName}` endpoint, which resolves storage within the current tenant.
Stored mobile blob paths must match the submission tenant before they are converted to file identifiers.
No new evaluation endpoint or migration is required.

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

## Locked elective corrections

Both commands require `ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.Add` and current persisted membership of the tenant's protected `admin` role. Configured data-scope rules, including the separately configured administrator scope exception, remain enforced. The elective roster response supplies `canCorrectLockedAssignments`; client role claims do not authorize a correction.

| Endpoint | Contract |
|---|---|
| `POST /api/class-management/student-elective-assignments/corrections/preview` | `StudentElectiveCorrectionInput`: `academicMediumId`, `classId`, `divisionId`, `electiveGroupId`, `studentId`, `selectedSubjectId`, optional `preferredSubjectId` and `reason` (maximum 1,000 characters). Null preference retains the current preference. Returns old/new labels, all detected impacts, `reviewToken` and `expiresAt`. No mutation. |
| `POST /api/class-management/student-elective-assignments/corrections/confirm` | The identical input plus `reviewToken`. Revalidates admin membership, context, choices and related state in a serializable transaction; saves the locked selection and correction audit together. Returns 204. |

The protected token expires after ten minutes and binds the user, tenant, input and reviewed state. Invalid or expired tokens and changed impacts return `ClassManagement:ElectiveCorrectionReviewChanged`; refresh and review again. Invalid context/choices return `ClassManagement:InvalidElectiveCorrection`; unlocked rows return `ClassManagement:CorrectionRequiresLockedSelection`; unchanged choices return `ClassManagement:ElectiveCorrectionNoChanges`. These business errors return 400; authorization failure returns 403. Existing academic records are preserved as described in [product rules](product.md#data-integrity).
