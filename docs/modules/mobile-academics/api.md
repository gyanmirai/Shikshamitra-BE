---
module: mobile-academics
domain: contracts
content_type: Reference
status: canonical
updated_on: 2026-10-12
owner: backend-team
audience: humans-and-agents
---

# Mobile academic API contracts

All 41 routes require bearer authentication and a current tenant. Reads and multipart uploads take `studentId` and `academicYearId` in the query; JSON writes take them in the body. Context can omit the year. The backend validates enrollment and persisted student access independently of client selections or role claims.

JSON success uses the existing `Result<T>` envelope (`success`, `data`). ABP supplies HTTP validation, authorization, business, and not-found errors; clients must treat failed HTTP responses as failures. Downloads return binary content with the filename and MIME type. Deletes return 204.

Paged endpoints return `studentId`, `academicYearId`, `items`, `totalCount`, `pageNumber`, and `pageSize`. Pages start at 1; size defaults to 20 and is limited to 100. Supported filtering is implemented in each service, not every list: homework has search/status/tab/subject/date; chapters have search/status; exams and results have search/status; leave has status. Calendar takes numeric `year` and `month`. Dates use ISO `yyyy-MM-dd`; half-day times use `HH:mm:ss`.

`GET /api/mobile/v1/academics/syllabus/dashboard` returns the saved subject-master colour as `data.subjects[].color` (for example, `"#008967"`). It uses the same subject ID as the name and progress fields. The value is nullable when the subject master is unavailable; the API does not derive colours from subject names.

## Academics prefix: `/api/mobile/v1/academics`

| Method | Suffix |
|---|---|
| GET | `/context` |
| GET | `/dashboard` |
| GET | `/holidays-events` |
| GET | `/holidays-events/{id}` |
| GET | `/homework` |
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

Homework accepts `textAnswer` (maximum 5000 characters) and `attachmentIds`. Leave accepts `leaveTypeId`, `startDate`, `endDate`, `isHalfDay`, optional paired `halfDayStartTime`/`halfDayEndTime`, `description`, and `attachmentIds`. Absence reason accepts `reason` (maximum 500 characters). Cancel accepts student/year context only. Multipart uploads use field `file`, allow five distinct attachments per response, and enforce a 5 MB per-file limit plus extension, MIME, and signature validation. Staged uploads belong to the uploading identity, student, and task; retained attachments may be used by another authorized guardian/self account after they are saved.
