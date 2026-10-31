---
module: mobile-academics
domain: contracts
content_type: Reference
status: canonical
updated_on: 2026-10-31
owner: backend-team
audience: humans-and-agents
---

# Mobile academic API contracts

All module routes require bearer authentication and a current tenant. Reads and multipart uploads take `studentId` and `academicYearId` in the query; JSON writes take them in the body. Context can omit the year. The backend validates enrollment and persisted student access independently of client selections or role claims.

JSON success uses the existing `Result<T>` envelope (`success`, `data`). ABP supplies HTTP validation, authorization, business, and not-found errors; clients must treat failed HTTP responses as failures. Downloads return binary content with the filename and MIME type. Deletes return 204.

## Dashboard class teacher

`GET /api/mobile/v1/academics/dashboard?studentId={id}&academicYearId={id}`
includes nullable `classTeacher: { teacherId, teacherName }` alongside the existing
dashboard fields. `teacherId` is the employee identifier used by the web class
teacher workspace; `teacherName` follows `Employee.EmployeeName` (first/last name).
No employee contact, photo, payroll or other HR profile fields are returned.

The dashboard resolves persisted self/guardian access and enrollment before the
reader matches the current tenant, standard, division and medium. Only Active
assignments for the resolved year or a null year, joined to active teaching
employees in the same tenant, qualify. Yearless mappings remain supported because
the validated class hierarchy establishes their year. Soft-deleted records remain
excluded. Identical teacher mappings are deduplicated; missing/ineligible teachers
or conflicting distinct teachers return null. Conflicts produce a server warning
with scope identifiers; database errors propagate normally. The reader projects
only employee IDs and name components and does not hydrate HR profiles.

This is an additive response change with no new endpoint, staff permission or
migration. Deploy it before the mobile header update. See the
[mobile binding guide](../../../../Gyanmirai-Mobile/docs/mobile-academics.md#dashboard-class-teacher)
for the shared header/body state.

## Module reads

Paged endpoints return `studentId`, `academicYearId`, `items`, `totalCount`, `pageNumber`, and `pageSize`. Pages start at 1; size defaults to 20 and is limited to 100. Supported filtering is implemented in each service, not every list: homework has search/status/tab/subject/date; chapters have search/status; exams and results have search/status; leave has status. Calendar takes numeric `year` and `month`. Dates use ISO `yyyy-MM-dd`; half-day times use `HH:mm:ss`.

`GET /api/mobile/v1/academics/homework/subjects` returns `data` as an array of `{ id, name, code }`, sorted by name, for the student's published class subjects and locked elective selections, including subjects without tasks.

`GET /api/mobile/v1/academics/homework/counts?studentId={id}&academicYearId={id}`
returns `{ studentId, academicYearId, pendingHomework, calculatedAt }` inside the
existing `Result<T>` envelope. The count combines homework and assignments eligible
for a first submission, without date, search, subject or paging filters. It includes
drafts, legacy pre-created unsubmitted rows and overdue first submissions still
allowed by school policy. It excludes submitted responses even while editable,
reviewed/excused responses, closed work and unsupported submission types.

The count reader shares the list's tenant, year-specific placement, published
division and assigned-subject predicates, and the submission command's first-write
eligibility expression. It executes database aggregates rather than hydrating task
bodies, attachments, teachers or submission content. `calculatedAt` is UTC; the
server captures the publication-check clock once per read. All calls validate the
persisted self/guardian association and academic year before counting.

Dashboard `pendingHomework` uses the same definition and reader. The legacy
`tab=pending` list retains its existing inclusion of editable submitted responses;
its membership is distinct from the first-submission badge. No permission seeding,
new permission or schema migration is required. Deploy the endpoint before the
mobile count consumer.
Homework and assignment reads include older records with a null academic-year field only within the student's validated year-specific class and division; explicit mismatching years remain excluded.

Homework list `date` filtering (and `tab=today`) includes every school calendar date from `assignedDate` through `dueDate`, inclusive, using Asia/Kolkata. The publication and due days both match regardless of their time of day. Without a date/tab filter, the list remains unrestricted by this date range. Existing publication, placement, subject, and status filters still apply.
Legacy class-subject assignment IDs are resolved to canonical subject IDs in task responses, names, codes, and subject filtering.
Division publication and publish-time checks continue to apply to task reads.

`GET /api/mobile/v1/academics/syllabus/dashboard` returns the saved subject-master colour as `data.subjects[].color` (for example, `"#008967"`). It uses the same subject ID as the name and progress fields. The value is nullable when the subject master is unavailable; the API does not derive colours from subject names.

The syllabus dashboard includes overall counts in `data.totalChapters` and `data.chaptersCovered`, and per-subject counts in `data.subjects[]`: `totalChapters`, `completedChapters`, `inProgressChapters`, and `upcomingChapters`.

The subject chapters endpoint additionally returns `data.summary` with `totalChapters`, `completedChapters`, `inProgressChapters`, `upcomingChapters`, and `coveragePercentage`. This summary covers all published chapters visible to the current student/guardian for the subject, before search, status filtering, or pagination. `totalCount` remains the count after filtering, before pagination. Completion means division progress is 100; in progress means 1-99; upcoming means 0. Coverage is the rounded mean chapter progress, or 0 for no visible chapters. Draft, in-review, archived, and audience-inaccessible chapters do not contribute to these counts.

## Academics prefix: `/api/mobile/v1/academics`

| Method | Suffix |
|---|---|
| GET | `/context` |
| GET | `/dashboard` |
| GET | `/holidays-events` |
| GET | `/holidays-events/{id}` |
| GET | `/homework` |
| GET | `/homework/subjects` |
| GET | `/homework/{id}` |
| PUT | `/homework/{id}/submission/draft` |
| POST | `/homework/{id}/submission` |
| PUT | `/homework/{id}/submission` |
| POST | `/homework/{id}/submission/attachments` |
| DELETE | `/homework/{id}/submission/attachments/{attachmentId}` |
| GET | `/homework/{id}/attachments/{attachmentId}/download` |
| GET | `/attendance/overview` |
| GET | `/attendance/calendar` |
| PUT | `/attendance/days/{date}/absence-reason` |
| GET | `/attendance/leave-types` |
| GET | `/attendance/leave-requests` |
| GET | `/attendance/leave-requests/{id}` |
| POST | `/attendance/leave-requests` |
| PUT | `/attendance/leave-requests/{id}` |
| PATCH | `/attendance/leave-requests/{id}/status` (cancel only) |
| POST | `/attendance/leave-requests/attachments` |
| DELETE | `/attendance/leave-requests/attachments/{attachmentId}` |
| GET | `/attendance/leave-requests/{id}/attachments/{attachmentId}/download` |
| GET | `/syllabus/dashboard` |
| GET | `/syllabus/subjects/{subjectId}/chapters` |
| GET | `/syllabus/chapters/{id}` |
| GET | `/syllabus/chapters/{chapterId}/resources/{resourceId}/download` |

## Exam prefix: `/api/mobile/v1/exams`

`examDate` is a school calendar date. `startTime` and `endTime` encode Asia/Kolkata
wall-clock components, including when the ISO wrapper ends in `+00:00`.
`2026-10-30T14:10:00+00:00` therefore means **2:10 PM school time**; clients must
not convert it from UTC into the device timezone. Combine the clock components
with `examDate`, not with the date portion of the time field.

List, detail and dashboard projections share the web's effective schedule rule:
Published maps to `upcoming`; the interval from the first paper's start through
the final paper's end (inclusive) maps to `ongoing`; a later time maps to
`completed`. Gaps between papers remain ongoing. Lifecycle rules still apply;
`resultStatus` independently describes result publication. Missing start/end
clocks retain the web defaults of start/end of the school day. Exams without a
dated assigned paper are not listed. Status uses the complete exam window;
returned papers remain restricted to the student's assigned subjects. Routes and
response fields are unchanged; no migration or stored-time conversion is needed.

| Method | Suffix |
|---|---|
| GET | root |
| GET | `/{id}` |
| GET | `/{id}/subjects/{scheduleId}/preparation` |
| GET | `/{id}/subjects/{scheduleId}/seating-plan` |
| GET | `/{id}/subjects/{scheduleId}/syllabus-portion` |
| GET | `/{id}/subjects/{scheduleId}/paper-pattern` |
| GET | `/{id}/subjects/{scheduleId}/question-banks` |
| GET | `/{id}/subjects/{scheduleId}/resources/{resourceId}/download` |

## Result prefix: `/api/mobile/v1/results`

| Method | Suffix |
|---|---|
| GET | `/academic-years` |
| GET | root |
| GET | `/{id}` |
| GET | `/{id}/subjects/{subjectId}` |
| GET | `/{id}/report-card/download` |

## Write contracts

### Timetable and one-time elective preferences

Prefix: `/api/mobile/v1/academics/timetable`. The existing `context` and `schedule` reads retain their routes and response envelope.

| Method | Suffix | Input |
|---|---|---|
| GET | `/elective-preferences` | Query `studentId`, `academicYearId` |
| POST | `/elective-preferences` | JSON `studentId`, `academicYearId`, `electiveGroupId`, `preferredSubjectId`, `clientRequestId`; all UUIDs |

GET and POST return `Result<PortalElectivePreferencesDto>` with `studentId`, `academicYearId`, `academicYearName`, `availability`, nullable `message`, `canViewTimetable`, and `groups`. A group includes `electiveGroupId`, `name`, `options` (`subjectId`, `name`), nullable original preference ID/name, nullable school-assigned subject ID/name, nullable `submittedAt`, `status`, and server-controlled `canSubmit`.

| Group status | Meaning |
|---|---|
| `selectionRequired` | No assignment or immutable submission exists; the student or linked guardian may choose one offered subject |
| `pendingApproval` | An unlocked assignment exists; read-only, including existing school assignments |
| `approved` | The valid school assignment is locked; read-only |
| `schoolActionRequired` | Configuration is invalid or an assignment was removed after submission; resubmission is unavailable |

Required groups come from the entire published class/division timetable, independent of the requested date. Options must be active group members published for that class and usable in the group's scheduled occurrences. Only the currently enrolled academic year is supported, as with the existing timetable API.

POST creates the immutable receipt and unlocked legacy assignment in one transaction. It never overwrites an existing school assignment. An identical `clientRequestId` and payload replays current state after authorization; a different retry or second submission returns HTTP 409. The uniqueness boundary is tenant/student/year/group, shared by the student and every linked guardian. Revoked links cannot replay an earlier operation.

Mobile `context` and `schedule` return normal HTTP 200 action states: `electiveSetupRequired`, `electivePreferenceRequired`, or `electiveApprovalPending`, in that priority order. Missing choices take priority over pending approval. `message` provides guidance; clients localize by `availability`. These states contain no timetable periods. Once all groups are approved, the schedule uses the school's assigned subject, which can differ from the immutable original preference. Invalid enrollment, access denial and malformed requests retain their errors. Profile/shared enrollment context is unchanged.

The staff contract remains unchanged: `GET` and `PUT /api/class-management/student-elective-assignments` read/save school assignments; `POST /api/class-management/student-elective-assignments/lock` locks an entire class/division/group after all students have assignments. Mobile creates no staff grants and offers no approve, unlock, delete, or edit endpoint. Existing locked assignments remain approved without inventing an original student submission.

### Homework and attendance inputs

Homework accepts legacy `textAnswer` or sanitized `textAnswerHtml` (100,000 UTF-16 text units), plus `attachmentIds`. See the submission contract below. Leave accepts `leaveTypeId`, `startDate`, `endDate`, `isHalfDay`, optional paired `halfDayStartTime`/`halfDayEndTime`, `description`, and `attachmentIds`. Absence reason accepts `reason` (maximum 500 characters). Cancel accepts student/year context only. Multipart uploads use field `file`, allow five distinct attachments per response, and enforce a 5 MiB per-file limit plus extension, MIME, and signature validation. Staged uploads belong to the uploading identity, student, and task; retained attachments may be used by another authorized guardian/self account after they are saved.

## Homework submission contract

The homework routes serve both homework and assignments. `type` identifies the task family. Detail and write responses remain `Result<MobileHomeworkDto>`; preview and confirmation are local client steps.

| Field | Contract |
|---|---|
| `textAnswerHtml` | Optional formatted answer; mutually exclusive with legacy `textAnswer`. Server strips active content and permits paragraphs, breaks, bold, italic, underline, lists, and code. |
| `attachmentIds` | Complete replacement list of up to five distinct staged or retained attachment IDs. |
| `acknowledged` | Completion checkbox; required to be `true` for final `inPerson` and `noSubmissionRequired` responses. Files and answer text are optional for these modes. Defaults to `false` for older clients. |
| `expectedRevision` | Opaque revision from detail or the previous successful write; `"0"` before a submission row exists. |
| `clientRequestId` | UUID retained for retries of the same draft, first submission, or replacement operation. |

All three write routes use these fields plus student/year context. HTML, an affirmative acknowledgment, revision, or request-ID usage requires both `expectedRevision` and `clientRequestId`. Legacy plain-text clients can omit both. HTML is limited to 1 MiB of UTF-8 before parsing and 100,000 decoded UTF-16 text units after sanitization, excluding one structural terminal newline. Plain-text legacy answers retain their 100,000-unit limit.

Responses add `textAnswerHtml`, `acknowledged`, `revision`, `savedAt`, `updatedAt`, `canSaveDraft`, and `submissionReasonCode`. `textAnswer` remains a plain-text projection for existing assessment clients. `submittedDate` preserves the first final submission time; replacement writes update `updatedAt`. Every submission-row update, including review, changes the concurrency stamp. Submitted answers, acknowledgment, and submission attachments remain readable through detail when editing is unavailable; viewing is independent of `canSubmit` and `canEdit`.

| Action | Required server state |
|---|---|
| Save draft | Any of the four supported modes, no final submission, open and unreviewed; available after the due time as well. Empty/unchecked drafts are accepted. |
| First submission | `canSubmit`, including overdue work with no prior final submission; text is required for `online`, a file for `fileUpload`, or `acknowledged: true` for `inPerson` / `noSubmissionRequired`. Supplementary content is accepted. |
| Replace submission | `canEdit`; only at or before the division due time while open and unreviewed. Once submitted, another first-submission POST is rejected. Previous final snapshots and file references are preserved. |
| Upload/remove staged file | `canSubmit` or `canEdit`, including optional evidence for acknowledgment modes. |

Multipart homework uploads accept optional form field `clientUploadId` (UUID). Retain it for retrying the same name, MIME, and bytes. Upload responses keep `{ id, name, url, contentType, size, fileName }`. Supported document extensions remain JPG/JPEG, PNG, PDF, DOC/DOCX, and XLS/XLSX. Files remain separate from the rich-text answer.

Save a draft without a removed file before deleting it. A final snapshot's files cannot be physically deleted, including after replacement. Deletion retains an internal ownership receipt so repeated authorized deletion returns 204. A linked guardian can release and delete a retained draft attachment after saving the replacement draft.

Successful write/upload receipts are persisted atomically with their changes and scoped to tenant, identity, student, academic year, task, and operation. Matching retries return the original response, including after the deadline; current student access and task visibility are checked before replay. Changing a receipt's payload or using a stale revision returns HTTP 409 (`MobileAcademics:IdempotencyConflict` / `MobileAcademics:SubmissionConflict`). A PostgreSQL transaction lock serializes mobile writes for a student/task; EF concurrency also rejects changes racing with teacher review.

Malformed metadata, ambiguous answers, invalid attachment lists, oversized content, missing required content, draft-after-submission attempts, and final-file deletion return coded HTTP 400 errors. An unchecked completion response returns `MobileAcademics:AcknowledgmentRequired`. Closed/reviewed work is locked. Submitted work past its due time returns a deadline reason and cannot be edited; overdue work without a final submission remains eligible for its first submission. Authentication and persisted student-link checks remain mandatory.

Apply migration `20261021110849_MobileHomeworkSubmissionReliability` before deploying this backend or the updated mobile client. It adds HTML/timestamps/concurrency stamps, backfills existing stamps, and creates receipt and final-snapshot tables. No seeding or permission changes are required.
Apply `20261022072327_MobileHomeworkCompletionAcknowledgment` next; it adds a non-null `Acknowledged` flag with default `false` to submissions and final snapshots, preserving existing responses.
