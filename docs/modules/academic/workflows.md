---
module: academic
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-10-28
owner: backend-team
audience: humans-and-agents
---

# Trace Academic guide workflows

TL;DR: read visit status to decide whether to present the guide, record only an actual guide/configuration entry, and save advisory year progress separately from operational configuration.
Screen and route behavior belongs to the [frontend workflows](../../../../Shikshamitra-FE/docs/academic/workflows.md).

## First school entry

1. An authenticated school account reads visit status.
2. The server validates active identity and determines whether any of the nine setting Views is granted.
3. A general Academic module visit performs no metadata write.
4. On actual guide or configuration entry, the caller posts `welcome`, null or the exact setting key.
5. The server checks the corresponding View and records actor/time only if no school marker exists.
6. The first successful writer receives `firstVisit: true`; subsequent visitors receive false and the preserved timestamp.

Two clients may both have read an unvisited snapshot.
The POST result resolves that race; the read result alone is not a reservation.
Automatic presentation must not treat a losing POST as a new first visit.
An existing marker applies across accounts, browsers and academic years.

## Resume and configure a year

1. Select a tenant-owned academic year and optionally a medium and class.
2. Read year state with those context IDs.
3. The server validates the hierarchy and caller scope, returns shared progress and queries only permitted settings.
4. Save cursor/deferred changes with the returned AcademicYear stamp.
5. Submit real configuration changes through their existing endpoint family.
6. Refetch guide state in the same context to reflect qualifying records.

Saving guide metadata alone cannot persist a partially filled form or change an `empty` setting to `configured`.
Weekly lectures and lecture slots require a class selection; the other guide statuses require a medium.
Changing context changes the queried configuration state without changing the school visit marker.

## Defer and review

An authorized caller may leave known viewable settings deferred.
Hidden deferred keys remain stored when a partial-permission caller saves their visible subset.
A valid stage may have a null current setting.
Only `finishReview: true` at the `review` stage records server review actor/time.
Changing cursor or deferred membership clears that evidence.
Review does not certify domain readiness and does not require all nine settings to be populated.

## Recover from conflicts or invalid state

| Condition | Recovery |
|---|---|
| First-visit compare-and-swap collision | Server retries with a fresh unit of work and returns the immutable winner |
| Visit retry budget exhausted | Refetch visit status before deciding whether an entry request still needs retrying |
| Stale AcademicYear stamp | Refetch current progress/context, preserve newer changes and resubmit deliberately |
| No configuration View | Do not record a visit or request year progress |
| Wrong year/medium/class relationship or missing scope | Correct the selected context or have an administrator review access |
| Invalid stored metadata | Surface the error for administrator investigation; do not clear the marker or reset configuration |

No recovery path reseeds identities, restores grants or resets school records.
