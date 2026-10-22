---
module: mobile-academics
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-10-22
owner: backend-team
audience: humans-and-agents
---

# Mobile academic workflows

1. Mobile obtains context for its selected linked student, validates the returned student/year, then calls module routes with that context. Every server request independently rechecks access.
2. Homework uploads stage files under the current identity/student/task. Draft or final writes resolve only permitted attachment IDs, sanitize HTML, validate lifecycle/submission type and revision, and persist the response with its receipt. Open, unreviewed tasks allow a first submission even when overdue. Matching request-ID retries replay the successful response after access checks; other duplicate final POSTs are rejected. A draft cannot revert a final submission. PUT edits additionally require server time at or before the division deadline, preserve the first submission time, and append an immutable snapshot. Reviewed/closed work and overdue final submissions cannot be overwritten. Releasing a draft attachment requires saving without its ID before DELETE; final-version files are protected.
3. Guardian leave writes validate active type, year/range/session, existing requests, and published school-calendar applicability. The server computes duration and saves a pending request in its enrolled class. School decisions lock mobile edits; a guardian can cancel a pending request.
4. Attendance reads join submitted day headers to student entries; legacy entry dates do not replace header dates. A guardian absence reason is separate from the teacher's remarks and attendance status.
5. Results release only published/frozen header data and the selected student's non-withheld record. Subject endpoints expose published marks; PDF download repeats those checks. Historical placement follows the selected enrolled year.
6. Resource download revalidates student/year and published resource ownership before returning tenant-scoped bytes. Missing blobs fail; no example file is substituted.

Mobile disposes student-scoped request state on account/child changes. Details follow the same selected student context, and late async responses cannot replace a newer selection. See the [mobile binding guide](../../../../Gyanmirai-Mobile/docs/mobile-academics.md) for client ownership.

In person and No submission required tasks show a completion checkbox and optional file picker. An unchecked draft can be saved; final confirmation requires the checkbox and persists `acknowledged: true` as a submitted response. The same first-submit, deadline, retry, and final-history rules apply. Every actual final response has a View submission action, even after the deadline or teacher review; it displays saved text, completion acknowledgment, and evidence files without requiring edit permission.
