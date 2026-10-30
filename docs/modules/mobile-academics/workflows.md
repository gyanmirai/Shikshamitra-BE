---
module: mobile-academics
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-10-30
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

## Elective preference and school approval

1. The mobile timetable checks all required elective groups in the published weekly timetable. Missing choices, unlocked assignments and invalid configuration return guided availability states without periods.
2. A student or linked guardian reads the preference endpoint. Only `canSubmit` groups show choices. Confirmation submits one group and one `clientRequestId`; an uncertain retry retains that ID.
3. The server rechecks tenant, active portal ownership, current enrollment and offered subject, then atomically records immutable submission history and an unlocked school assignment. Another linked account cannot submit a replacement. Existing school assignments are never overwritten by this API.
4. The school reads/saves through its existing assignment API. Its lock action still requires all students in the class/division/group to have assignments and locks that group in bulk. This is the approval signal; there is no new mobile approval endpoint.
5. Check approval or refresh reads current state. A changed final assignment is displayed separately from the original preference. Removing an assignment after submission requires school intervention and does not reopen student choice.
6. Once all required groups are locked, mobile fetches a fresh timetable using the school's assignments. Opening or resuming screens and returning from preferences refresh status; no background polling is required.

In person and No submission required tasks show a completion checkbox and optional file picker. An unchecked draft can be saved; final confirmation requires the checkbox and persists `acknowledged: true` as a submitted response. The same first-submit, deadline, retry, and final-history rules apply. Every actual final response has a View submission action, even after the deadline or teacher review; it displays saved text, completion acknowledgment, and evidence files without requiring edit permission.
