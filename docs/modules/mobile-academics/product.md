---
module: mobile-academics
domain: behavior
content_type: Reference
status: canonical
updated_on: 2026-10-12
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

Absent records remain empty or unavailable. A zero attendance denominator produces a null percentage. Unpublished or withheld results expose no marks or report card. Class comparisons include only published marks belonging to released class results.

## Availability boundaries

Exam resources reuse published LearningResource records classified as `SyllabusPortion`, `PaperPattern`, or `QuestionBank`; those records are subject-wide, not an inferred exam-specific curriculum. Chapter resources also include legacy AssignedSubjectDocument attachments, which inherit the chapter's publication and audience. Legacy filenames come from their stored blob references; unknown file sizes are omitted in mobile. Parent-only chapters and parent summaries require a guardian link. No persisted question-section or topic-level result model exists here, so those breakdowns remain unavailable. Report cards show real published totals and marks; they do not reproduce a school-specific branded template. Published attachment blobs must exist under the current tenant's file prefix.
