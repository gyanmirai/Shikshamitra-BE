---
module: student
domain: api
content_type: Reference
status: canonical
updated_on: 2026-09-11
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

## Assessment endpoints

The route prefix is `/api/student/enrollment/admissions/assessments`.
Responses use the standard `Result<T>` envelope.
Permission names below have prefix `Students.Admissions.Assessments.`.

| Method and suffix | Permission | Behavior |
|---|---|---|
| `GET /scopes` | `View` | Scoped offering/stage choices, including versions pinned to existing applications |
| `GET /sessions?admissionOfferingId=...&workflowStageDefinitionId=...` | `View` | Scheduled sessions for an offering and managed stage |
| `POST /sessions` | `ManageSessions` | Create a future session |
| `PUT /sessions/{id}` | `ManageSessions` | Update dates, location, name, or capacity before start |
| `POST /sessions/{id}/cancel` | `ManageSessions` | Cancel a session after its bookings are cancelled |
| `GET /applications/{applicationId}/stages/{stageInstanceId}` | `View` | Policy, attempt history, blockers, and permitted action flags |
| `POST /applications/{applicationId}/stages/{stageInstanceId}/attempts` | `Book` | Create an attempt, with a matching session when scheduling is enabled |
| `PUT /attempts/{id}/attendance` | `RecordAttendance` | Record `present` or `absent` |
| `PUT /attempts/{id}/result` | `RecordResults` | Record marks or an unscored passed/failed outcome |
| `POST /attempts/{id}/review` | `Review` | Approve evidence or return it for correction |
| `POST /attempts/{id}/retake` | `Book` | Permit another attempt within the configured limit |
| `POST /attempts/{id}/cancel` | `Book` | Cancel an unstarted attempt and release its booking |
| `POST /applications/{applicationId}/stages/{stageInstanceId}/waive` | `Waive` | Record an authorized waiver with a reason |

Workflow stage metadata advertises `assessment` in `SupportedConfiguration` and runtime stage DTOs expose `AssessmentEnabled`.
The configuration shape is `{ "assessment": { "version": 1, "enabled": true, "schedulingEnabled": false, "attendanceRequired": false, "scoringEnabled": true, "totalMarks": 100, "passingMarks": 40, "reviewRequired": false, "maxAttempts": 1 } }`.
Version 1 supports 1 to 10 attempts and positive total marks up to 10000, with passing marks between zero and the total.
Attendance can also be required in result-only mode.

Session input requires offering/stage identifiers, name, ISO date-times, location, and capacity from 1 to 10000.
Session update/cancellation requires the session concurrency stamp.
Attempt creation and stage waiver require the application stamp returned in the assessment summary.
Other attempt mutations require that attempt's concurrency stamp.
Review uses `approved: false` plus notes to request correction, and waiver uses `reason`.
Attempt mutations return the refreshed assessment summary.

Available condition facts are `assessment.outcome`, `assessment.marks`, `assessment.attemptNumber`, `assessment.attendance`, and `assessment.reviewed`.
Unreviewed evidence exposes a pending outcome and no authoritative marks.
Negative outcome routes must contain a matching `Equals` or array-valued `In` condition on `assessment.outcome` and satisfy the complete route expression.
Unknown policy versions, stale stamps, unavailable capacity, missing evidence, and missing negative-outcome routes are rejected using `Admissions:Assessment:*` error codes.

## Inquiry management APIs

The route prefix is `/api/student/enrollment/admissions/inquiries/{inquiryId}/management`.

| Method | Route suffix | Permission | Purpose |
| --- | --- | --- | --- |
| `GET` | none | `Students.Admissions.Inquiries.View` | Read safe policy, deadlines, blockers, capabilities, qualification, and follow-ups |
| `POST` | `contact` | `Students.Admissions.Inquiries.Manage` | Record a contact note and advance contact readiness |
| `POST` | `follow-ups` | `Students.Admissions.Inquiries.Manage` | Schedule a bounded future follow-up |
| `POST` | `follow-ups/{followUpId}/complete` | `Students.Admissions.Inquiries.Manage` | Complete an open follow-up with an optional note |
| `POST` | `follow-ups/{followUpId}/cancel` | `Students.Admissions.Inquiries.Manage` | Cancel an open follow-up with a reason |
| `POST` | `qualification` | `Students.Admissions.Inquiries.Manage` | Record a checklist-backed qualification outcome |
| `POST` | `reopen` | `Students.Admissions.Inquiries.Manage` | Reopen a non-converted qualification with a reason |

Counsellor choices use `GET /api/student/enrollment/admissions/inquiry-settings/counsellors`, require workflow view permission, and return only active users compatible with the requested offering scope.
Every management command requires the current management concurrency stamp.
Internal policy snapshot JSON and qualification-answer JSON are never returned directly.
Create Inquiry accepts `duplicateOverrideReason`; enabled policies can block duplicates or require this reason when staff explicitly create a separate family.

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

## Document verification APIs

Base: `/api/student/enrollment/admissions/document-verification`. Responses use the standard `Result<T>` envelope except authenticated file downloads.

| Method and relative route | Permission | Behavior |
|---|---|---|
| GET `applications/{applicationId}/stages/{stageInstanceId}` | `Students.Admissions.Documents.View` | Pinned requirements, applicability, chronological evidence/reviews, blockers, effective file limits, action flags, application concurrency stamp |
| POST `applications/{applicationId}/stages/{stageInstanceId}/requirements/{code}/upload` | `Students.Admissions.Documents.Upload` | Multipart `file`, `concurrencyStamp`, optional `notes`; appends a fresh revision |
| POST same requirement route `/original` | `Students.Admissions.Documents.RecordOriginal` | JSON `concurrencyStamp`, required observation `notes`; retains any latest upload and resets review |
| POST same requirement route `/waive` | `Students.Admissions.Documents.Waive` | JSON `concurrencyStamp`, required `reason`; requires policy permission |
| POST `evidence/{evidenceId}/review` | `Students.Admissions.Documents.Review` | JSON `concurrencyStamp`, `decision: accepted/rejected`, optional `reason` (required for rejection) |
| GET `evidence/{evidenceId}/download` | `Students.Admissions.Documents.Download` | Scoped private attachment, no-store response, audit activity; storage keys never exposed |
| GET `document-types` | `Students.Admissions.Workflows.View` | Tenant catalog mappings, allowed extensions and maximum file size |

All mutations return refreshed stage state and serialize against the application stamp. History is read-only after leaving the stage. Server authorization validates tenant, offering UserScope, application-stage ownership, frozen workflow version, current stage, policy, and evidence revision. Error family `Admissions:Documents:*` provides localized invalid-policy/state/evidence/review, stale-stamp, and completion-gate messages.

Linked Student evidence uses GET `/api/student/records/students/{studentId}/admission-evidence` with both `Students.Records.StudentList.View` and `Students.Admissions.Documents.View`. Its `/{evidenceId}/download` additionally requires `Students.Admissions.Documents.Download` and verifies the finalized source application and Student link.

### Student record profile

GET `/api/students/records/{studentId:guid}/profile` requires `Students.Records.StudentList.View` on both the controller and application service.
It returns `Result<StudentRecordProfileDto>` with matching canonical GUID `id` and `studentId`, current names, numeric `grNo` and alphanumeric `grNumber`, roll number, standard/division/academic-medium IDs and names, lifecycle `status`, date of birth, and gender.
Optional identity and placement values remain null when absent.
The read projects scalar fields from the current Student record; it does not return the aggregate, an inquiry snapshot, financial data, or photo bytes.
Tenant filtering and the shared Student direct-record guard apply before projection; explicit academic-year, board, medium, stage, standard, or division grants determine access, and ancestor IDs from a narrower assignment do not grant wider access.
The same direct-record guard protects linked admission-evidence reads and downloads.
This endpoint does not add a Student write or change lifecycle behavior, and it requires no schema migration.

### Onboarding documents

GET/POST `/api/student/onboarding/{caseId}/documents` requires `Students.Onboarding.View`; POST also requires `Students.Onboarding.Edit`. Uploads validate the scoped active case, permitted Student lifecycle, tenant document type, file evidence, and concurrency, then refresh readiness. Replacing an existing file preserves its old private blob until the transaction commits successfully.

## Eligibility APIs

Base: `/api/student/enrollment/admissions/eligibility`. Responses use `Result<T>`. All applicant endpoints require `Students.Admissions.Eligibility.View`; mutations additionally require the action permission below and the current application `concurrencyStamp`.

| Method and relative route | Additional permission | Behavior |
|---|---|---|
| GET `applications/{applicationId}/stages/{stageId}` | None | Pinned policy, latest evaluation, freshness, blockers, concurrency and action flags |
| POST same stage route `/evaluate` | `Students.Admissions.Eligibility.Evaluate` | Check declared saved facts and append an immutable evaluation |
| GET same stage route `/history` | None | Evaluations, criterion results, exception requests and decisions |
| POST same stage route `/evaluations/{evaluationId}/exceptions` | `Students.Admissions.Eligibility.RequestException` | Required reason, fresh latest ineligible result, policy permission; append request |
| POST same stage route `/exceptions/{exceptionId}/review` | `Students.Admissions.Eligibility.ReviewException` | Required reason and `decision: approved/rejected`; enforce configured role and reviewer separation |
| GET `reviewer-roles` | `Students.Admissions.Workflows.View` only | School roles as `{id,name}` choices |

The summary reports `notChecked`, `eligible`, `ineligible`, `needsInformation`, or `exceptionApproved`, plus a separate `isStale`. Each criterion returns its expected value, actual value, source, outcome and message. Saved evaluation outcomes remain `eligible/ineligible/needsInformation`; exception approval is an additional audited decision. GET does not create an evaluation. Mutations return refreshed summary state. Historical completed stages can be rechecked before finalization; finalized or closed admissions cannot mutate.

Policy fields are `version`, `enabled`, `allowExceptions`, nullable `exceptionApprovalRole`, `requireIndependentReviewer`, and 1-30 `criteria`. Each criterion has a unique `code`, `name`, `kind`, optional `failureMessage`, and kind-specific properties: age uses `minAgeMonths/maxAgeMonths/cutoffDate`; answers use `formDefinitionId/conditionJson`; assessment/documents use `sourceStageCode`, with optional assessment `minimumPercentage/requiredOutcome`. A waived assessment cannot also require a score. The existing complete stage JSON limit remains 8000 characters.

Authoritative transition facts are `eligibility.status`, `eligibility.failedCount`, and `eligibility.unknownCount`. A stale result exposes status `stale`; an unrecorded result exposes `notChecked`. Negative routes must actually depend on the matching status, not merely contain an unused OR branch. Error family `Admissions:Eligibility:*` covers invalid configuration/state, stale application stamps, missing/stale evaluations, unauthorized exceptions, and missing negative-result routes.

Offering configuration errors include `EligibilityFormMismatch` when an enabled criterion references a different application or inquiry form version from the Offering. Create/update/activate use the localized `Admissions:Eligibility:OfferingFormMismatch` business error, and detail/list/availability report incomplete configuration. Missing form selections retain the established draft-save and activation rules.

## Review APIs

Base: `/api/student/enrollment/admissions/reviews`.
Responses use the standard `Result<T>` envelope, and mutations require the application `concurrencyStamp`.
Applicant endpoints enforce `Students.Admissions.Reviews.View` on both HTTP and application-service boundaries, plus the additional permission below.

| Method and relative route | Additional permission | Behavior |
|---|---|---|
| GET `applications/{applicationId}/stages/{stageId}` | None | Pinned policy, current evidence, assignment, deadline, safe record, blockers and action flags |
| GET same stage route `/history` | None | Immutable recommendation, checklist, assignment and actual-decision history |
| POST same stage route `/draft` | `Students.Admissions.Reviews.Edit` | Save current checklist, notes, optional recommendation and reason |
| POST same stage route `/submit` | `Students.Admissions.Reviews.Submit` | Submit a complete checklist and reasoned recommendation |
| POST same stage route `/assignment` | `Students.Admissions.Reviews.Assign` | Reasoned reviewer/deadline change; validate tenant, configured role and offering scope |
| POST same stage route `/request-correction` | `Students.Admissions.Reviews.Decide`, `Students.Admissions.Applications.Decide`, `Students.Admissions.Applications.Reopen` | Record the actual correction decision and reopen the application form |
| GET `queue` | None | Scoped paged active Review queue with `assignedToMe` and `overdueOnly` filters |
| GET `reviewer-roles` | `Students.Admissions.Workflows.View` only | Tenant roles as `{id,name}` choices |
| GET `reviewers?filter=...&role=...` | `Students.Admissions.Workflows.View` or `Students.Admissions.Reviews.Assign` | Bounded active tenant user choices with applicable role and scope checks |

Save/submit payloads contain `notes`, `recommendation`, `reason`, and `checklist: [{code,checked,notes}]`; labels and required flags come exclusively from the pinned policy.
Recommendation values are `approve`, `waitlist`, `reject`, and `requestCorrection`.
All notes and reasons are limited to 1024 characters; submit requires a nonblank reason and every required confirmation.
Assignment contains nullable `assignedReviewerId` and `dueAt`, plus a required reason; an explicit past deadline is valid and appears overdue.
The summary reports `notStarted`, `draft`, `submitted`, `approved`, `waitlisted`, `rejected`, `correctionRequested`, or `stale`, with separate freshness and action flags.

Existing application Approve/Reject/Waitlist commands retain their routes and permissions and additionally enforce managed Review approval rules and `Students.Admissions.Reviews.Decide`.
The main application detail adds nullable `reviewDecision` with `managedReviewEnabled`, `isSatisfied`, decision capabilities and blockers; absent values preserve the legacy client contract.
No new competing approval endpoint or automatic decision is introduced.
Transition facts are `review.status` and `review.decision`; the server overwrites reserved Review facts when managed Review exists.
Error family `Admissions:Review:*` provides localized policy, scope/state, stale-stamp, evidence, assignment, recommendation and route guidance.
Offering validation reports `ReviewAssigneeUnavailable` when the configured default reviewer is inactive, outside the configured role or outside the exact hierarchy; save and activation use `Admissions:Review:ReviewerNotAllowed`.

## Managed Form stage APIs

Base: `/api/student/enrollment/admissions/applications/{applicationId}/form-stages/{stageId}`. Read requires `Students.Admissions.Applications.View`; verification requires `Edit`; correction requires `Reopen`. The application service repeats every permission check and validates tenant, Offering UserScope, pinned workflow, stage identity, and frozen form identity.

| Method and relative route | Behavior |
|---|---|
| GET base | Return pinned policy, form status, deadline, attempts, verification state, blockers, concurrency and capability flags |
| POST `sections/{sectionId}/verification` | Verify or revoke one configured section; revocation requires a reason |
| POST `correction` | Require a reason, append audit history, reopen the form, and invalidate earlier section verification |

Commands carry the application `concurrencyStamp`; route identifiers are excluded from bodies. Managed submit also enforces the deadline and maximum attempts through the existing application-form endpoint. A managed stage may hold after submit for verification or explicit continuation. Missing or disabled `configurationJson.form` retains legacy Form behavior. Offering configuration rejects a Form-stage definition that differs from the Offering application form.

## Managed Payment stage APIs

Payment routes use `/api/student/enrollment/admissions/payments`. Stage reads are `GET /applications/{applicationId}/stages/{stageId}`; staff collection posts to `/transactions`; verification posts to `/transactions/{transactionId}/review`; full waivers post to the stage `/waive` route; reversals post to `/transactions/{transactionId}/reverse`.

The distinct permissions are `Students.Admissions.Payments.View`, `Record`, `Verify`, `Waive`, and `Reverse`. Every command uses optimistic concurrency. Record, review, waiver, and reversal requests also use idempotency keys. Responses expose required, settled, pending, and outstanding amounts, due state, blockers, allowed actions, and safe transaction evidence; they do not expose policy snapshots or internal retry keys.

## Managed Offer stage APIs

Base: `/api/student/enrollment/admissions/offers`. All routes use the standard `Result<T>` envelope, repeat authorization at the controller and application-service boundaries, and verify tenant, Offering UserScope, pinned workflow, active stage, application concurrency, and idempotency.

| Method and relative route | Permission | Behavior |
|---|---|---|
| GET `applications/{applicationId}/stages/{stageId}` | `Students.Admissions.Offers.View` | Return pinned content, status, expiry, counters, blockers, capabilities and safe history |
| POST same stage route `/issue` | `Issue` | Issue the first version with configured or permitted expiry |
| POST same stage route `/respond` | `Respond` | Record acceptance confirmations or a reasoned decline |
| POST same stage route `/extend` | `Extend` | Extend an active offer within policy limits |
| POST same stage route `/withdraw` | `Withdraw` | Withdraw an active offer with a permanent reason |
| POST same stage route `/reissue` | `Reissue` | Issue a new bounded version after decline, expiry, or withdrawal |

Every command body includes `concurrencyStamp` and `idempotencyKey`; route identifiers stay in the URL. Workflow facts are `offer.status`, `offer.versionNumber`, `offer.extensionCount`, and `offer.isExpired`. Non-acceptance outcomes can continue only through a transition whose evaluated condition explicitly matches `offer.status`.

## Managed Seat stage APIs

Base: `/api/student/enrollment/admissions/seats/applications/{applicationId}/stages/{stageId}`. Reads and mutations verify tenant, Offering UserScope, the application-stage relationship, the pinned managed policy, and action-specific permission.

| Method and relative route | Permission | Behavior |
|---|---|---|
| GET base | `Students.Admissions.Seats.View` | Return capacity, allocation status, expiry, waitlist position, blockers, capabilities and safe event history |
| POST `/reserve` | `Reserve` | Reserve within capacity or join the configured waitlist; capacity override requires `OverrideCapacity` |
| POST `/confirm` | `Confirm` | Convert an active reservation into a confirmed seat |
| POST `/renew` | `Renew` | Extend an active hold within policy bounds |
| POST `/release` | `Release` | Release an allocation; a confirmed allocation remains releasable after stage completion until finalization |
| POST `/waitlist` | `Waitlist` | Add the applicant to the offering FIFO waitlist |
| PUT `/capacity` | `ManageCapacity` | Change offering capacity; going below occupancy requires `OverrideCapacity` and a reason |

Commands carry the application stamp, allocation stamp when present, and an idempotency key. Capacity changes also carry the pool stamp. Workflow facts are `seat.status`, `seat.remainingCapacity`, `seat.isExpired`, and `seat.waitlistPosition`.
