---
module: mobile-academics
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-10-12
owner: backend-team
audience: humans-and-agents
---

# Mobile academic workflows

1. Mobile obtains context for its selected linked student, validates the returned student/year, then calls module routes with that context. Every server request independently rechecks access.
2. Homework uploads stage files under the current identity/student/task. Draft or final writes resolve only permitted attachment IDs, validate lifecycle/deadline/submission type, and persist the response. Reviewed/closed work cannot be overwritten. A duplicate final POST is rejected; permitted replacement uses PUT.
3. Guardian leave writes validate active type, year/range/session, existing requests, and published school-calendar applicability. The server computes duration and saves a pending request in its enrolled class. School decisions lock mobile edits; a guardian can cancel a pending request.
4. Attendance reads join submitted day headers to student entries; legacy entry dates do not replace header dates. A guardian absence reason is separate from the teacher's remarks and attendance status.
5. Results release only published/frozen header data and the selected student's non-withheld record. Subject endpoints expose published marks; PDF download repeats those checks. Historical placement follows the selected enrolled year.
6. Resource download revalidates student/year and published resource ownership before returning tenant-scoped bytes. Missing blobs fail; no example file is substituted.

Mobile disposes student-scoped request state on account/child changes. Details follow the same selected student context, and late async responses cannot replace a newer selection. See the [mobile binding guide](../../../../Gyanmirai-Mobile/docs/mobile-academics.md) for client ownership.
