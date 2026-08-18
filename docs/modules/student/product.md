---
module: student
domain: product
content_type: Reference
status: canonical
updated_on: 2026-08-14
owner: backend-team
audience: humans-and-agents
---

# Understand Student Enrollment Admissions behavior

TL;DR: an Admission Offering is the tenant-scoped, auditable configuration that connects one cycle and academic hierarchy to published workflow and form versions.

## Offering identity

The active identity is Admission Cycle, optional Campus, Board, Medium, Stage, and Standard.
Division is not part of Admission Offering identity.
The database prevents duplicate non-deleted identities with separate filtered indexes for Campus and no-Campus records.
Campus selection is currently rejected because this installation has no authoritative Admission Campus source.

## Admission window

`UseCycleDates` expresses whether an offering inherits its cycle window.
Inherited dates are persisted as null overrides.
Explicit dates must be ordered and fall inside the Admission Cycle.
API payload dates use `DateTimeOffset` while cycle inheritance remains date-based.

## Readiness and availability

Readiness is computed from the cycle, academic hierarchy, dates, published workflow, published inquiry form, and published application form.
It is never stored as an editable flag.

| Availability | Meaning |
|---|---|
| `ConfigurationIncomplete` | Required configuration is missing or no longer published |
| `NotOpenYet` | The cycle or effective window has not opened |
| `Open` | Inquiries and applications may start |
| `Closed` | The cycle or effective window is closed |
| `Inactive` | An administrator deactivated the offering |

## Runtime integrity

Starting an application freezes its workflow definition and application form definition references.
Changing future offering defaults does not rewrite existing applications.
Once an application references an offering, its cycle and academic identity cannot change.
An offering with applications cannot be deleted and must be deactivated instead.
Published workflow and form versions are immutable; changes require cloning a new version.

## Security and audit

Backend authorization is required for every offering operation.
ABP tenant filters and academic UserScope restrict reads and hierarchy writes.
Concurrency stamps protect update, activate, deactivate, and delete commands.
Soft deletion preserves historical references and uniqueness semantics.
