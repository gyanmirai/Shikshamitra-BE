---
module: student
domain: api
content_type: Reference
status: canonical
updated_on: 2026-08-18
owner: backend-team
audience: humans-and-agents
---

# Find Student Enrollment Admissions APIs

TL;DR: Admission Offering APIs use `api/student/enrollment/admissions/offerings` and separate read, validation, state, clone, delete, and bulk commands.

## Offering endpoints

| Method | Route | Permission |
|---|---|---|
| GET | `/api/student/enrollment/admissions/offerings` | `Students.Admissions.Offerings.View` |
| GET | `/api/student/enrollment/admissions/offerings/{id}` | `Students.Admissions.Offerings.View` |
| POST | `/api/student/enrollment/admissions/offerings/cycles/{admissionCycleId}` | `Students.Admissions.Offerings.Create` |
| PUT | `/api/student/enrollment/admissions/offerings/{id}` | `Students.Admissions.Offerings.Edit` |
| POST | `/api/student/enrollment/admissions/offerings/{id}/activate` | `Students.Admissions.Offerings.Activate` |
| POST | `/api/student/enrollment/admissions/offerings/{id}/deactivate` | `Students.Admissions.Offerings.Deactivate` |
| DELETE | `/api/student/enrollment/admissions/offerings/{id}` | `Students.Admissions.Offerings.Delete` |
| POST | `/api/student/enrollment/admissions/offerings/{id}/clone` | `Students.Admissions.Offerings.Clone` |
| POST | `/api/student/enrollment/admissions/offerings/validate` | `Students.Admissions.Offerings.Validate` |
| GET | `/api/student/enrollment/admissions/offerings/{id}/availability` | `Students.Admissions.Offerings.View` |
| POST | `/api/student/enrollment/admissions/offerings/cycles/{admissionCycleId}/bulk` | `Students.Admissions.Offerings.BulkCreate` |

The compatibility create route accepts `POST /api/student/enrollment/admissions/offerings?admissionCycleId=...`.

## Workflow endpoints

| Method | Route | Permission |
|---|---|---|
| GET/POST | `/api/student/enrollment/admissions/workflows` | Workflow `View` or `Create` |
| GET/PUT/DELETE | `/api/student/enrollment/admissions/workflows/{id}` | Workflow `View`, `Edit`, or `Delete` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/clone` | `Students.Admissions.Workflows.Clone` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/new-version` | `Students.Admissions.Workflows.Clone` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/validate` | `Students.Admissions.Workflows.Validate` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/publish` | `Students.Admissions.Workflows.Publish` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/archive` | `Students.Admissions.Workflows.Archive` |
| GET | `/api/student/enrollment/admissions/workflows/{id}/versions` | `Students.Admissions.Workflows.View` |
| GET | `/api/student/enrollment/admissions/workflows/{id}/usage` | `Students.Admissions.Workflows.ViewUsage` |
| GET | `/api/student/enrollment/admissions/workflows/stage-types` | `Students.Admissions.Workflows.View` |

## Runtime workflow endpoint

`POST /api/student/enrollment/admissions/applications/{id}/workflow/advance` requires `Students.Admissions.Applications.Advance`.
The body includes `expectedCurrentStageInstanceId`, a unique `idempotencyKey`, optional `concurrencyStamp`, and optional transition, skip, override, and reason fields.
Admin overrides additionally require `Students.Admissions.Applications.OverrideWorkflow`.
The response returns current workflow status, stage timestamps, immutable transition history, and `nextRequiredAction`. The next action is derived from the server stage-type registry and is `null` after the workflow completes.

## Application form runtime

| Route | Permission | Behavior |
|---|---|---|
| `GET /api/student/enrollment/admissions/applications/{id}/form` | `Students.Admissions.Applications.View` | Returns the pinned form definition/version, instance status, and saved values. |
| `PATCH /api/student/enrollment/admissions/applications/{id}/form-values` | `Students.Admissions.Applications.Edit` | Saves a draft with concurrency checking and rejects unknown field codes. |
| `POST /api/student/enrollment/admissions/applications/{id}/submit` | `Students.Admissions.Applications.Submit` | Validates visible fields and stores an immutable submission snapshot. |
| `POST /api/student/enrollment/admissions/applications/{id}/form/reopen` | `Students.Admissions.Applications.Reopen` | Reopens a submitted form for authorized correction and records the event. |
| `GET /api/student/enrollment/admissions/applications/{id}/form/history` | `Students.Admissions.Applications.View` | Returns form creation, draft-save, submission, and reopen history. |

Published form definitions are immutable versions. Existing applications retain their original `FormDefinitionId` and `FormVersion` when an offering is moved to a later version.

Choice fields support either static option arrays or an approved source descriptor such as `{ "source": "EducationBoards" }` or `{ "source": "EducationMediums", "parentField": "BOARD_FIELD" }`. Approved source metadata is exposed by `GET /api/student/enrollment/admissions/forms/metadata/data-sources`; tenant-scoped options are read through `GET /api/student/enrollment/admissions/forms/metadata/options?sourceKey=...&parentId=...`. Arbitrary URLs and executable data sources are not accepted, and submission validates selected values against the authoritative source.

## List conventions

The list supports server-side search, paging, sorting, academic hierarchy filters, active state, availability, and configuration status.
List rows return display names, effective dates, workflow version, application count, and action capability flags.
Detail responses add override dates, definition references, readiness errors, audit fields, and the concurrency stamp.

## Command conventions

Update bodies include `ConcurrencyStamp`.
Activate, deactivate, and delete receive `concurrencyStamp` as a query parameter.
Active state cannot be changed through ordinary update; callers must use the explicit state command.
Bulk creation previews every row and commits all rows in one unit of work only when every row is valid.

## Offering errors

| Code | Meaning |
|---|---|
| `Admissions.OfferingDuplicate` | The active academic identity already exists |
| `Admissions.OfferingAcademicHierarchyInvalid` | The selected hierarchy is inconsistent |
| `Admissions.Offering.HierarchyOutOfScope` | UserScope does not allow the hierarchy |
| `Admissions.OfferingDateRangeInvalid` | Override dates are missing, reversed, or outside the cycle |
| `Admissions.Offering.ConfigurationIncomplete` | Published runtime configuration is incomplete |
| `Admissions.Offering.IdentityLocked` | Runtime applications lock identity |
| `Admissions.Offering.HasApplications` | Delete is blocked by runtime history |
| `Admissions.Offering.ConcurrencyConflict` | The supplied stamp is stale |
| `Admissions.Offering.StateCommandRequired` | Active state was changed through update |
| `Admissions.Offering.CampusInvalid` | The selected Campus is missing or inactive |

## Admission Campuses

Admission Campuses are tenant-scoped offering configuration records. They are managed through
`/api/student/enrollment/admissions/campuses` and use the offering View/Manage permissions.
Campus codes are unique within a tenant. Only active campuses are returned by the selector and
can be assigned to a new or updated offering; an inactive or missing campus makes the offering
configuration incomplete.
