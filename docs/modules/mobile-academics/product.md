---
module: mobile-academics
domain: behavior
content_type: Reference
status: canonical
updated_on: 2026-10-30
owner: backend-team
audience: humans-and-agents
---

# Academic data available to mobile accounts

| Module | Behavior |
|---|---|
| Dashboard | Pending homework, attendance percentage, upcoming exams, and current-month events |
| Homework & Assignment | Published class tasks; durable drafts, submission and permitted resubmission; attachments; released feedback and rubrics |
| Attendance | Submitted registers, policy credits, month calendar, guardian absence reasons, and guardian leave create/edit/cancel |
| Syllabus | Published assigned/elective subjects, public chapters, division coverage, and published resources |
| Exam | Published/completed exams, subject schedules, preparation notes, own published seat, and public subject resources |
| Result | Enrolled years, pending/released/withheld states, published marks, aggregate comparisons, and generated report-card PDF |
| Holidays & Events | Published active school/class/division notices in the enrolled medium and academic year |

The authenticated identity must be active and confirmed, with a persisted self or guardian link to the requested student. Revoked links deny reads and writes. Server placement resolves the selected enrollment, including historical classes for results; a legacy placement is used only when no enrollment history exists.

Students can submit their own work. Leave changes and absence reasons require a guardian link. Guardians cannot approve their own leave. Only pending leave can be edited or cancelled; overlapping active requests and unconfigured school days are rejected. Half-day times and school-day duration are persisted. School dates use Asia/Kolkata.

Homework and assignments share the mobile submission workflow. The teacher's requirement controls final validation: online text needs an answer, file upload needs a file, and In person / No submission required need a checked completion acknowledgment with optional evidence files. Confirming an acknowledgment creates a normal submitted response. Answers support sanitized formatting up to 100,000 decoded UTF-16 units; responses allow five files up to 5 MiB each. Drafts can be empty or unchecked but are only available before final submission. Open, unreviewed work allows one first submission even after its division deadline; late submissions are recorded as late. After the first submission, only editing is allowed, and only at or before the deadline. Submitted work becomes read-only after the deadline, with its answer, acknowledgment, and files still available to view. Retry receipts and revision checks prevent duplicate or stale mobile writes; confirmed replacements preserve final history and its files.

Absent records remain empty or unavailable. A zero attendance denominator produces a null percentage. Unpublished or withheld results expose no marks or report card. Class comparisons include only published marks belonging to released class results.

## Availability boundaries

Timetable elective preferences are once per student, academic year and required elective group, whether submitted by the student or a linked guardian. Each group becomes read-only immediately. Existing unlocked school assignments are also read-only and await school approval. The original mobile preference remains in immutable history if staff changes or removes the assignment; a removed assignment requires school intervention rather than another mobile submission.

Approval uses the existing school `IsLocked` workflow for a class/division/group. Every required group in the published weekly timetable must be valid and locked before any mobile schedule periods are returned. Students receive selection/approval/setup guidance instead of a missing-elective 400. The school-approved subject controls the schedule and may differ from the original preference. There is no new rejection, reopening, individual approval or historical timetable workflow.

Syllabus chapters require `PublishStatus = Published` and a student-facing audience (`Students` or `All`); guardians can also read `Parents` chapters. `Teacher` visibility excludes both mobile audiences. Draft, in-review, and archived chapters do not contribute to mobile dashboard counts or chapter lists, and their detail endpoints return not found. A chapter appearing in the staff syllabus table does not establish mobile publication. Teaching progress of zero does not hide a published chapter; it appears as `notStarted`.

Exam resources reuse published LearningResource records classified as `SyllabusPortion`, `PaperPattern`, or `QuestionBank`; those records are subject-wide, not an inferred exam-specific curriculum. Chapter resources also include legacy AssignedSubjectDocument attachments, which inherit the chapter's publication and audience. Legacy filenames come from their stored blob references; unknown file sizes are omitted in mobile. Parent-only chapters and parent summaries require a guardian link. No persisted question-section or topic-level result model exists here, so those breakdowns remain unavailable. Report cards show real published totals and marks; they do not reproduce a school-specific branded template. Published attachment blobs must exist under the current tenant's file prefix.
