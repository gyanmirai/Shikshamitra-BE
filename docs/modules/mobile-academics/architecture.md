---
module: mobile-academics
domain: implementation
content_type: Reference
status: canonical
updated_on: 2026-10-22
owner: backend-team
audience: humans-and-agents
---

# Mobile academic implementation

Thin authenticated controllers call dedicated application services. `MobileAcademicContextAppService` resolves tenant, persisted portal access, academic year, enrollment, hierarchy, and eligible subjects. It reuses `StudentPortalAccessService`; staff UserScope grants do not substitute for a student link.

Services read existing homework/assignment, attendance, curriculum, examination, result, and academic calendar aggregates. Mutations use existing submission/leave managers and domain invariants under ABP units of work. Reads never infer identity or tenant from a mobile payload. EF queries restrict tenant and student/class before any in-memory date conversion, sorting, or paging.

Mobile leave writes acquire a PostgreSQL advisory transaction lock keyed by tenant and student before checking overlapping requests. The lock lasts until commit and prevents simultaneous mobile requests on different hosts from passing the same overlap check. SQLite tests do not exercise this PostgreSQL mechanism; it needs a concurrent PostgreSQL integration check before release.

`MobileAttachmentStore` writes opaque file blobs and identity/student/task-scoped staging metadata. Download routes reauthorize the owning task, chapter, exam, or leave. External resource URLs are never fetched by the server. Tenant file prefixes and traversal checks protect published downloads.

`MobileHomeworkSubmissionService` owns draft/final/upload/delete orchestration, leaving reads in `MobileHomeworkAppService`. It acquires `IMobileSubmissionLock` for tenant/student/task before loading current state or replaying a receipt. `MobileAnswerSanitizer` uses an HTML parser and allowlisted sanitization, maintaining a plain-text projection for existing assessment clients. `StudentSubmissionEntity` implements `IHasConcurrencyStamp`, so review and legacy repository updates participate in EF concurrency checks.

`MobileSubmissionOperation` stores operation identity, payload hash, and successful response in the same transaction as the write. `MobileSubmissionSnapshot` is append-only final-response history and protects referenced attachment blobs after replacement. Both use tenant filters. Staging metadata doubles as an ownership receipt for safe deletion retries; draft replacement grants a linked actor access to release the files it just removed.

## Migration

`src/ShikshaMitra.EntityFrameworkCore/Migrations/20261012061916_MobileAcademicResponses.cs` adds five nullable columns: submission/leave attachment metadata, guardian absence reason, and two half-day time columns. Existing rows need no synthetic backfill. Submission and attendance-entry modification timestamps are concurrency tokens. Leave already uses aggregate concurrency stamps.

The migration preserves unrelated academic-term, student-settings, and roll-number tables from the baseline snapshot. Existing model drift must be investigated separately before future scaffolding.

`20261012113000_RepairMobileLeaveHalfDayTimes` repairs databases that recorded the earlier three-column version of `MobileAcademicResponses` before the half-day fields were added. It adds either missing time column with PostgreSQL `ADD COLUMN IF NOT EXISTS`; it also succeeds when both columns already exist. Its rollback preserves these columns because the preceding migration's target model includes them. Do not delete migration history or rerun the initial migration to repair this state.

The repair was applied to the local development database without seeding or changing existing leave records. Remote migration and API deployment remain release steps.

`20261021110849_MobileHomeworkSubmissionReliability` adds sanitized HTML, response timestamps, and concurrency stamps to submissions, backfills existing stamps, and creates receipt/snapshot tables. Deploy this migration before the updated API and mobile client. It preserves legacy content and existing action grants.

`20261022072327_MobileHomeworkCompletionAcknowledgment` adds `Acknowledged` (default `false`) to both student submissions and immutable final snapshots. The domain validates explicit completion for In person / No submission required tasks; the read service exposes the flag and permits their first submission and optional uploads. Existing receipt hashes remain unchanged for omitted/false acknowledgment fields, so pre-upgrade successful retries remain valid.
