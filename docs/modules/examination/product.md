---
module: examination
domain: product
content_type: Reference
status: canonical
updated_on: 2026-10-19
owner: backend-team
audience: humans-and-agents
---

# Understand Examination seating behavior

TL;DR: an examination contains one or more seating-plan groups, and each plan owns either one subject or a user-selected set of subjects that reuse the same rooms, rules, and seat map.

| Invariant | Behavior |
|---|---|
| Exam boundary | The dashboard returns one envelope per exam rather than one top-level row per subject session |
| Individual plan | Selecting exactly one exam subject creates `SingleSubject` scope |
| Shared plan | Selecting two or more exam subjects creates `SelectedSubjects` scope, even when subjects have different dates |
| Subject ownership | An exam subject can belong to only one plan; application checks and a unique database index enforce it |
| Readiness | Every selected subject needs a complete schedule; missing students or rooms block configuration |
| Capacity | Usable room capacity is authoritative; disabled and reserved seats are excluded, and markers outside the physical layout do not reduce capacity |
| Physical room layout | Saved `ColumnRows` defines the benches in each column; each bench contributes `BenchCapacity` seats. Number seats row by row, left to right, skipping missing benches |
| Preview safety | Preview is read-only and apply merges reviewed assignments without replacing existing seats |
| Concurrency | V2 writes echo an opaque entity version and stale writes fail |
| Draft lifecycle | Before publication, plans may be regrouped and their allocations explicitly cleared; the complete seating draft may be reset without deleting exam schedules |
| Publication coverage | Every scheduled exam subject must belong to exactly one valid, fully seated plan |
| Publication boundary | One `ExamSeatingPublication` record atomically publishes and locks every plan for the examination |
| Immutability | After examination publication, scope, room, seat, preview, reset, unlock, finalize, and publish mutations cannot reopen the arrangement |
| Retry safety | Repeated examination publish calls return the authoritative workspace without duplicate publication, audit, or notification effects |
| Dashboard truth | The dashboard exposes one exam summary with subject coverage, plan count, progress, and lifecycle; the workspace exposes plan-level capacity and issues |

Legacy all-subject, subject-group, and session-key plans remain readable through existing routes. A legacy per-plan `Published` status does not establish the new global immutable boundary unless migration proves complete, unique subject coverage. Partial or historically ambiguous legacy publication is reopened as a draft so the school can reset or regroup it and use the new single publish action.
The session projection remains in the workspace for older clients, while new clients use `Plans` and `AvailableSubjects`.
The default notification implementation records intent in logs but does not deliver email, SMS, or in-app messages.

Rooms without `ColumnRows` retain their existing row-wise seat numbering, including partial final rows and capacity-only legacy layouts. `RoomSeatingLayout` supplies physical geometry and capacity to automatic preview, preview apply, manual-seat validation, and seat-layout responses. Existing assignments are not renumbered or rewritten when loading a layout. A save targeting a seat outside the current physical room remains blocked before persisted assignments change.
