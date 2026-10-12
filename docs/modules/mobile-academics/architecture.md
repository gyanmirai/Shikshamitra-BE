---
module: mobile-academics
domain: implementation
content_type: Reference
status: canonical
updated_on: 2026-10-12
owner: backend-team
audience: humans-and-agents
---

# Mobile academic implementation

Thin authenticated controllers call dedicated application services. `MobileAcademicContextAppService` resolves tenant, persisted portal access, academic year, enrollment, hierarchy, and eligible subjects. It reuses `StudentPortalAccessService`; staff UserScope grants do not substitute for a student link.

Services read existing homework/assignment, attendance, curriculum, examination, result, and academic calendar aggregates. Mutations use existing submission/leave managers and domain invariants under ABP units of work. Reads never infer identity or tenant from a mobile payload. EF queries restrict tenant and student/class before any in-memory date conversion, sorting, or paging.

Mobile leave writes acquire a PostgreSQL advisory transaction lock keyed by tenant and student before checking overlapping requests. The lock lasts until commit and prevents simultaneous mobile requests on different hosts from passing the same overlap check. SQLite tests do not exercise this PostgreSQL mechanism; it needs a concurrent PostgreSQL integration check before release.

`MobileAttachmentStore` writes opaque file blobs and identity/student/task-scoped staging metadata. Download routes reauthorize the owning task, chapter, exam, or leave. External resource URLs are never fetched by the server. Tenant file prefixes and traversal checks protect published downloads.

## Migration

`src/ShikshaMitra.EntityFrameworkCore/Migrations/20261012061916_MobileAcademicResponses.cs` adds five nullable columns: submission/leave attachment metadata, guardian absence reason, and two half-day time columns. Existing rows need no synthetic backfill. Submission and attendance-entry modification timestamps are concurrency tokens. Leave already uses aggregate concurrency stamps.

The migration preserves unrelated academic-term, student-settings, and roll-number tables from the baseline snapshot. Existing model drift must be investigated separately before future scaffolding.

`20261012113000_RepairMobileLeaveHalfDayTimes` repairs databases that recorded the earlier three-column version of `MobileAcademicResponses` before the half-day fields were added. It adds either missing time column with PostgreSQL `ADD COLUMN IF NOT EXISTS`; it also succeeds when both columns already exist. Its rollback preserves these columns because the preceding migration's target model includes them. Do not delete migration history or rerun the initial migration to repair this state.

The repair was applied to the local development database without seeding or changing existing leave records. Remote migration and API deployment remain release steps.
