---
module: academic
domain: product
content_type: Reference
status: canonical
updated_on: 2026-10-28
owner: backend-team
audience: humans-and-agents
---

# Understand Academic guide state

TL;DR: automatic presentation depends on whether any authorized account in the school has visited Academic configuration; guide progress belongs to the selected academic year and remains advisory.

## State boundaries

| State | Scope | Meaning |
|---|---|---|
| First configuration visit | Entire school | The first authorized guide or configuration entry was recorded |
| Guide cursor and deferred settings | School academic year | A shared guide position and settings left for later |
| Guide review | School academic year | Someone deliberately finished the guide's review stage |
| Configuration state | Selected year, medium and applicable class | A matching record exists under the caller's permissions and scope |

The first visit stores server-selected actor and time once.
It is independent of user, browser, academic year, guide review and configuration completeness.
General Academic module entry may read visit status, but only an actual guide or configuration entry records a visit.
Changing year, clearing settings or deferring every setting does not remove the school-wide marker.
Schools with existing settings receive no inferred historical visit.

## Guide scope

The stages are `welcome`, `curriculum`, `timetable`, `homework`, `examination` and `review`.
The guide covers exactly nine settings: subjects, elective groups, weekly lectures, lecture slots, homework types, assignment types, reusable rubrics, grading scales and exam types.
Calendar Settings and LMS are outside this guide's current contract.
The exact setting keys and stage ownership are listed in [api.md](./api.md).

The guide stores cursor, deferred setting keys and review metadata.
Existing configuration endpoints save real form values and enforce their own domain validation.
An unsubmitted configuration form has no persisted guide draft.
Saving guide progress never creates or changes subjects, timetables, types, rubrics or exam settings.

## Advisory status

| State | Meaning |
|---|---|
| `configured` | At least one qualifying record exists in the selected configuration context |
| `empty` | No qualifying record exists in that context |
| `context-required` | An authorized setting still needs a medium or class selection |
| `unavailable` | The caller lacks that setting's View action |

`configured` is an existence check, not a readiness certificate.
For example, a reusable rubric draft can exist without being published.
The guide does not revalidate a record's full domain lifecycle or certify that every class is configured.
`reviewedAt` records a guide review, not operational completion.
Finishing review does not require every setting to be configured.
Changing cursor or deferred settings clears a prior review; an unchanged save preserves it.
Operational edits outside the guide do not invalidate this advisory review automatically.

## Data integrity

Authorization, active tenant identity and hierarchical scope are enforced on the server.
Hidden frontend controls do not authorize a request.
Unauthorized setting states expose no record counts or details.
Invalid stored metadata returns an error rather than silently resetting the first visit or progress.
See [api.md](./api.md) for access rules and [architecture.md](./architecture.md) for concurrency and persistence.
