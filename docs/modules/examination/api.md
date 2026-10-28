---
module: examination
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-28
owner: backend-team
audience: humans-and-agents
---

# Find Examination seating APIs

TL;DR: V2 exposes an exam plan-group workspace, idempotent one-or-many subject plan creation, and reviewed-preview apply while retaining existing lifecycle and compatibility routes.

| Method and route | Permission | Purpose |
|---|---|---|
| `GET api/academics/exams/{examId}/seating-plan/v2/workspace` | View | Return available exam subjects, plan groups, counts, rooms, capacity, issues, selected plan, version, and the legacy session projection |
| `POST api/academics/exams/{examId}/seating-plan/v2/plans` | Add | Create or return one individual/shared plan for the exact selected exam-subject IDs |
| `POST api/academics/exams/{examId}/seating-plan/v2/session` | Add | Compatibility route for older session-first clients |
| `POST api/academics/exams/{examId}/seating-plan/v2/validate-examination` | View | Validate subject coverage and every plan before the single publish action |
| `POST api/academics/exams/{examId}/seating-plan/v2/reset-draft` | Delete | Audit and remove every draft plan, subject link, and seat assignment |
| `POST api/academics/exams/{examId}/seating-plan/v2/publish` | Publish | Atomically finalize, publish, audit, and lock the complete examination seating |
| `POST api/academics/exams/{examId}/seating-plan/scopes` | Add | Create or edit a subject group; changing a populated group requires explicit allocation reset intent |
| `DELETE api/academics/exams/{examId}/seating-plan/scopes/{planId}` | Delete | Delete an empty draft plan |
| `PUT api/academics/exams/{examId}/seating-plan` | Add | Save selected rooms and allocation rules with version checking |
| `POST api/academics/exams/{examId}/seating-plan/auto-allocate` | Auto Allocate | Generate a read-only proposal |
| `POST api/academics/exams/{examId}/seating-plan/v2/apply-preview` | Auto Allocate | Merge the reviewed proposal idempotently |
| `POST api/academics/exams/{examId}/seating-plan/validate` | View | Return authoritative blockers and warnings |
| `POST api/academics/exams/{examId}/seating-plan/finalize` | Finalize | Compatibility plan-level validation/lock; it does not publish the examination |
| `POST api/academics/exams/{examId}/seating-plan/publish` | Publish | Compatibility route that delegates to complete examination publication |
| `GET api/academics/exams/seat-allocation` | View | Return one exam envelope with subject names, subject coverage, plan count, progress, lifecycle, and compatibility session summaries |

V2 clients send `Version` on draft and apply writes.
A mismatch returns code `SEATING_PLAN_CONCURRENCY_CONFLICT` and requires a refresh.
User scope and tenant filters still apply in addition to endpoint permissions.
Creating or editing a plan fails when any selected subject already belongs to another plan or has an incomplete schedule. Mutations after publication return code `EXAMINATION_SEATING_PUBLISHED`; incomplete global publication returns `EXAMINATION_SEATING_NOT_READY` with blocker details.

## Grade filter options

`GET api/academics/exams/marks/grade-options` requires `Academics.Examination_RecordMarks.View` and the existing exam user-scope check. It returns a string array for grade filters without loading mark feedback or student profiles.

| Query | Rule |
|---|---|
| `ExamId` | Required exam ID |
| `SubjectId` | Optional; omitted includes all exam subjects |
| `DivisionId` | Optional; omitted includes permitted exam divisions |
| `StandardId` | Optional consistency filter; a different standard returns an empty array |

Only enrolled, approved students and their applicable locked elective selections contribute marks. Subject grading scales use their configured total and inclusive ranges, matching Grade Entry; an unmatched custom range contributes no label. Nonempty labels are trimmed and ordinal-distinct, with their case preserved. There is no result cap. Missing subjects, divisions, eligible marks, or labels return an empty array.
