---
module: student
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-09
owner: backend-team
audience: humans-and-agents
---

# Find Student Enrollment Admissions APIs

TL;DR: Admission Offering APIs use `api/student/enrollment/admissions/offerings` and separate read, validation, state, clone, delete, and bulk commands.

## Academic list filters

Admissions Application, Inquiry (including CSV export), Offering, Pipeline/options, and Review queue list inputs accept `StandardIds` in addition to the existing optional `StandardId`. Student Onboarding list accepts `StandardIds` and `DivisionIds` alongside the scalar filters. Collections bind from repeated query keys, for example `standardIds={guid1}&standardIds={guid2}`. Nonempty collections match any member; scalar, Board, Medium, academic year and other supplied criteria intersect with that match. Empty collections do not restrict classes or divisions. Existing tenant and authorized academic scope applies before counting and paging; these filters do not broaden permissions. No persistence change is required.

## Active admission Pipeline and follow-ups

`GET /api/student/enrollment/admissions/applications/pipeline` requires AdmissionApplications.View and projects active inquiry-linked applications within existing tenant and offering scope.
It accepts academic hierarchy, MyCases, assigned counsellor, status, stage code, search, whitelisted sorting and bounded paging.
`GET /api/student/enrollment/admissions/applications/pipeline/options` supplies complete scoped active counsellor/stage choices, current user and the authoritative default queue without page/search truncation.
Administrators identified by the existing user scope and configured fallback managers default to All; other staff default to My.
The queue preference never changes academic access.
Terminal applications and failed/cancelled workflows are excluded; a completed workflow awaiting finalization stays active.
Next-action guidance comes from the existing stage registry, due dates from published Form/Payment policies and follow-up reminders from existing scheduled inquiry records.
Missing source inquiries remain visible through an application-backed projection with `sourceInquiryAvailable=false`.

Application `GET /{applicationId}/follow-ups` requires Application View; schedule `POST /follow-ups` and complete/cancel `POST /follow-ups/{followUpId}/complete|cancel` additionally require exact Inquiry Manage.
These routes reuse inquiry management, policy, owner restrictions and concurrency stamps, and return `canManageFollowUps`.
Inquiry details/qualification remain locked after transfer, while follow-up commands require an active linked application.
Closed cases reject follow-up writes; an unavailable source inquiry returns a corrective business error.
No new workflow, messaging store or finalization path is created.

## Public inquiry link options

Public-link creation options are exposed through `GET /api/student/enrollment/admissions/public-inquiry-links/options?admissionCycleId={guid}` with Admission Offerings View authority. The response envelope contains distinct `{counsellorAssignmentProfileId, inquiryFormDefinitionId}` pairs from active Inquiry/Both Offerings in the published period and the current user's academic scope. Archived setups and unpublished/non-Inquiry forms are excluded. An unknown or unpublished period returns an empty list. Create keeps its existing payload and Add authority; `Admissions.PublicInquiry.OfferingRequired` remains HTTP 400 and includes corrective text plus `PeriodName`, `SetupName`, and `Reason` for absent Offerings or unmatched profile/form linkage.

## Admission student field contracts

| Contract | Behavior |
|---|---|
| Form system-field metadata | `validationJson` describes shared field formats. `destination` distinguishes `studentProfile` from `capturedAdmissionDetails`. The predefined catalogue has 107 fields. |
| Form `metadata/options`, source `StudentSiblings` | Existing AdmissionForms.View authority and current tenant/staff scope. Optional `searchTerm` (at most 100 characters), `skipCount`, `maxResultCount` (default 20, maximum 21) supply a bounded option list. Repeated `selectedIds` GUID parameters hydrate up to 20 selected identities within scope; other data-source behavior remains unchanged. |
| Finalization preview and possible Student matches | `canApplyProfileFieldsDirectly` identifies pending Admission/Registration profiles. `canReviewProfileFields` accounts for Records View/Create authority. `capturedStudentFields` contains `{key,label,value}` rows with a safe photo summary; `profileReviewDescription` explains the handoff and separate Compliance review. |
| Finalization input | `applyStudentProfileFields` defaults to false for existing profiles. New profiles receive supported answers automatically. Protected existing profiles use the Records review service and its current section guards. |

Student captured summaries exclude Aadhaar, APAAR, PEN, UDISE, PAN and raw photo payloads. Their existing canonical storage and read permissions remain authoritative. The original admission evidence stays on the applicant.

## Onboarding credential delivery

| Endpoint | Authority and contract |
|---|---|
| GET `/api/student/onboarding/{caseId}/credential-emails` | Onboarding.View and current academic scope; returns student/guardian rows including NotRequested/Blocked, mailbox, enabled flag and server canSend. No password/token fields. |
| POST same route | Onboarding.Edit and current scope; `{accountType, concurrencyStamp?}` uses the onboarding case stamp and current mandatory readiness. Returns durable status; already-pending requests are idempotent. |
| POST `/api/student/onboarding/{caseId}/documents/override` | Retained route rejects required-document override attempts; use canonical workflow evidence and verification. |

Email status DTO includes id, accountType, email, status, message, sentAt, attemptCount, emailEnabled and canSend.
Manual resends/retries require the current saved readiness and sender permission, with a two-minute cooldown.
Automatic dispatch preserves the initial account creation grants; exact sender grants and academic scope are checked again before transport.
Email/mobile/username login aliases apply to exclusive Student/Parent identities with email and eligible child access.
An ambiguous mobile match fails closed; the owner verifies the mailbox and changes an initial password through the existing Account setup route.

## Offering-linked Onboarding operations

All routes below are under `/api/student/onboarding`, enforce tenant/academic scope, and retain existing Onboarding permission aliases.

| Route | Contract |
|---|---|
| GET `/settings/offerings?academicYearId=...` | Configure permission; scoped Offering names, academic context and published workflow references for readiness settings. |
| POST `/{id}/requirements/review?concurrencyStamp=...` | Edit permission; explicitly adopts current effective Offering requirements for an eligible unactivated legacy case. |
| POST `/{id}/gr-number` | AllocateIdentifier; `{numberSeriesId, manualValue?, manualSequence?, reason?, concurrencyStamp?}`. First manual assignment needs no correction reason. |
| POST `/{id}/roll-number` | AllocateIdentifier; `{rollNumber, rollAssignmentMode?, reason?, concurrencyStamp?}`. Independent from GR assignment. |
| POST `/identifier-locks` | AllocateIdentifier; `{academicYearId, divisionId, identifierType: gr|roll, isLocked, reason?}`. Unlock also requires Repair, trusted administrator scope and a reason. No Division placement lock. |
| GET `/{id}/fee-structure` and `/fee-structure/candidates` | View; existing Admissions selection/candidate DTOs backed by published Finance structure reads. |
| PUT `/{id}/fee-structure` | Edit; `{structureId, optionalHeadIds, concurrencyStamp?}` confirms admission intent only. No Finance mutation. |
| GET `/{id}/workflow-documents` | View; frozen applicable workflow requirements and canonical evidence. |
| POST `/{id}/workflow-documents/{stageId}/{code}/upload` or `/original` | Edit plus AdmissionDocuments.Upload or RecordOriginal respectively; reuses canonical Admissions evidence contracts. |
| POST `/{id}/workflow-documents/evidence/{evidenceId}/review` | Edit plus AdmissionDocuments.Review; canonical verification, with no required-document waiver bypass. |

Case DTOs expose requirements review, frozen document availability, admission confirmation, fee confirmation, identifier lock and credential pending flags. Filter options expose trusted `canUnlockIdentifiers`; list supports `enteredToday` and task filters including fees/credentials. A successful activation persists enrollment before access preparation; access failure leaves Records-visible enrollment and a retriable credential state.

## Student Attendance API families

| API family | Contract and authority |
|---|---|
| Existing `/api/students/attendance` | GET `/students` and `/history` under Attendance_MarkAttendance.View; POST `/mark` under Add; POST `/lock` or `/unlock` under Delete. |
| `/api/students/attendance/settings` | GET nullable academicYearId under Attendance_SettingLeaveType.View; PUT `/draft`, POST `/preview` and `/publish` under Attendance_SettingLeaveType.Add. Commands use concurrency stamps and publish dates. |
| `/api/students/attendance/schedules` | GET enrollmentId under Attendance_MarkAttendance.View; POST create/edit and DELETE id with concurrencyStamp under Attendance_SettingLeaveType.Add, plus enrollment/hierarchy access checks. |
| `/api/students/attendance/register` and `/summary` | Register GET uses Attendance_Reports.View; summary GET uses Attendance_MarkAttendance.View. Both require academicYearId and bounded dates. Register permits optional Standard/Division and server paging; summary identifies one student. DTOs return nullable percentage, expected/recorded/missing counts, provisional and school-target metadata. |
| `/api/students/attendance/enrollments` | GET paged scoped membership under Attendance_SettingLeaveType.View; POST `/{enrollmentId}/start-date` under Attendance_SettingLeaveType.Add with date and revision. A confirmed start cannot silently be rewritten. |
| `/api/students/attendance/corrections` | GET under Attendance_LeaveAndAbsent.View; POST request, `/{id}/approve` or `/reject` under Attendance_LeaveAndAbsent.Add. Locked review additionally checks Attendance_MarkAttendance.Delete. Commands require reasons and revisions and prohibit self-review. |

Daily saves require current `revision` when a day exists and `rosterRevision` from the roster response.
`submit=false` saves partial drafts; `submit=true` checks the entire eligible roster.
`operationId` identifies an actor/payload-specific durable retry, and `correctionReason` supplies observation conflict or correction evidence.
`isPastMarkingCutoff` informs the UI; `canMark` remains the server capability.
`needsCreditCorrection` identifies a submitted row whose approved-leave link or credit now differs from its recorded snapshot. Ordinary saves cannot refresh that row, and Submit checks omitted rows too. A correction request may retain the same Present/Absent mark when this flag is true; it still requires a reason and follows the school's approval rules. Approval rejects stale recorded credits or changed request-time leave facts.
Correction responses expose `isCreditCorrection` and nullable before/requested present and denominator credits, derived from saved request evidence and policy. The review preview stays pinned when source leave later changes; legacy evidence without recorded credits remains unknown.
Business errors are centralized in `AttendanceOperationErrorCodes` and `AttendancePolicyErrorCodes`, with English attendance localization.
Stale roster/revision, incomplete submission, invalid status/detail, conflicting leave, tenant/scope violations and duplicate retry payloads are rejected atomically.

## Student Records API families

Records preserve each controller's response contract: Directory/Profile, Settings, Transitions, Guardian Access and certificate/ID metadata use the shared `Result<T>` envelope. Changes, Duplicates, Reports and photo-upload metadata return their DTO or paged DTO directly. PDF, photo and Excel downloads return file responses. Permission names use the central `Students.Records.*` catalogue; Directory/Profile reads retain `Students.Records.StudentList.View` for compatibility.

| API family | Commands and permissions |
|---|---|
| `/api/students/records/settings` | GET effective/draft context and `/roles` under Settings.View; PUT `/draft` and POST `/preview` under Settings.Add; POST `/publish` under Settings.Publish. Nullable `academicYearId` selects school defaults. |
| `/api/students/records/transitions` | GET paged cases, `/{id}`, `/options`, `/renewals`, `/renewals/{id}` under Transitions.View. Options accept `academicYearId`, optional `studentId` and `admissionOfferingId`; expose permitted placement, return policy, active Offerings with EntryMode, and resumable real applications. Selected Inquiry-capable Offering metadata additionally requires Inquiries.Create and source scope. |
| Transition requests | POST `/preview`, `/batches`, `/{id}/cancel`, `/{id}/correct`, `/returns`, `/returns/preview`, `/renewals/{id}/confirm` under Transitions.Create. POST `/{id}/approve` and `/reject` under Transitions.Approve. POST `/{id}/apply` and `/renewals/{id}/activate` under Transitions.Apply. |
| Renewal portal handoff | GET `/renewals/{id}/portal-options` exposes required accounts, recorded email/guardian choices, existing links and `canProvision`. POST `/renewals/{id}/portal-accounts` uses the existing Onboarding account DTO; requires Transitions.Create, Onboarding.Edit and Identity.Users.Create, plus Identity.Users.Update for verified existing guardian linkage. |
| Other Records surfaces | Directory, profile, changes, historical overrides, duplicate review, documents/IDs, guardian reviews, and reports use their source-owned controllers in `HttpApi/Controllers/Student/Records/`. Existing certificate/ID permission compatibility is preserved. |

Batch requests contain one to 100 distinct students, source academic year, stable idempotency key, per-row outcome/reason/effective date, and preview student/enrollment concurrency stamps. Promote/Repeat/Return additionally require target year/class/division and roll number. Submission validates stamps and school batch behavior again. Reusing a key for a different request is rejected.

Target-year options expose the Onboarding-owned `rollAssignmentMode` (`manual`, `automatic`, or `both`). A row sends `rollAssignmentMode` (`manual` or `automatic`); a school allowing both requires an explicit choice. Preview returns the selected mode, `proposedRollNumber`, and numbering scope. Automatic submission must send that accepted proposed number. A changed reservation raises `Students:Records:RollProposalChanged`; refresh preview without replacing the idempotency key after an unknown network outcome. Existing no-configuration manual clients remain compatible. Records freezes the accepted number and owner rule; later application validates that number rather than allocating another.

`POST /returns/preview` accepts the return input and returns one transition preview row for `ReuseProfile`; Fresh/Resume use their real Admissions handoff. Reuse supports a reviewed return within the same academic year by reopening the existing inactive enrollment. Completed and cancelled renewal cases remain in history; only one active renewal can exist for a student and year. A requester may cancel AwaitingRenewal only while its linked case is PendingChecklist, before target enrollment or new-year Finance dependencies; Provisioning cancellation is blocked.

Initial Onboarding exposes its school mode on the case DTO. `POST /api/student/onboarding/{id}/roll-preview` requires AllocateIdentifier and accepts `{rollNumber?,rollAssignmentMode?,reason?}`; it returns `{schoolAssignmentMode,rollAssignmentMode,proposedRollNumber,concurrencyStamp}`. The existing identifiers command retains GR inputs and positive `rollNumber`, adds optional `rollAssignmentMode`, and validates the accepted automatic proposal using the shared owner allocator.

Decision and renewal commands require `concurrencyStamp`. Correction first supports `previewOnly`; submission uses a reason and idempotency key and returns the new `transitionId`. Case DTO capabilities are derived from state, actual actor/role, permissions, and correction impact; they do not replace server authorization.

Fresh return accepts optional guardian-only `registration` when `freshApplicationRequiresRegistration` is true, optional numeric `entryPath` (Inquiry=0, DirectRegistration=1), and `inquiry` containing explicit source, sourceDetail, preferredLanguageCode and configured formValues. Both requires an explicit entryPath; mismatches are rejected. Applicant/guardian bound fields are read-only and server-filled; metadata identifies their field codes even when policy masks hide values. Fresh/Resume enforce the owning Admissions permissions and return a nullable transition/application/inquiry link with Submitted or AwaitingAdmission status. Links are exposed only with the corresponding Admissions View permission. Onboarding completion marks the return Applied; submission and Admission handoff do not activate the student.

Renewal portal creation is available only after a real target-year enrollment reaches Provisioning. The source checks saved contact/guardian email, required account type, explicit verified relationship, current database actor roles/grants, tenant and academic scope, concurrency, and exclusive Student/Parent identity roles. A verified existing guardian requires explicit reuse confirmation; privileged identities are never transferred by email alone. New identities have no generated password and remain unconfirmed until the existing Account owner setup flow completes. Linking is the local handoff acknowledged by renewal, consistent with initial Onboarding.

Common blocking errors use `Students:Records:*`: `ConfigurationChanged`, `ConcurrencyConflict`, `StaleSnapshot`, `BatchHeld`, `TargetEnrollmentExists`, `AuthorityChanged`, `RenewalChecklistRequired`, `RenewalProvisioningRequired`, `FinancialDependency`, and `DownstreamDependency`. Required result/Finance/Library/Transport evidence has distinct readiness codes. Refresh a stale preview or resolve the owning module issue before an explicit retry.

`FinancialPlacementImpactRequired` blocks a same-year class change or same-year return into another class when Finance already has an assignment. Records does not provide a Finance impact confirmation/reassignment contract; a paid installment is not evidence that old-class fees apply to the proposed class. Same-class division/roll edits and returns remain independent of this class-impact blocker.

## Reserved Student identity contract

The reserved GUID is backend-owned and is not accepted by inquiry, applicant-update, form or finalization inputs.
Existing public DTO shapes are unchanged; `StudentId` continues to mean an actual Student, not a reservation.
Finalization may return `Admissions:ReservedStudentIdConflict` if the reserved identity is occupied, including a concurrent finalization; refresh and review the actual linked Student before retrying, and escalate a persistent conflict.
The error does not reveal other-tenant or deleted Student details.
Existing finalization permissions, scope checks, idempotency and onboarding boundaries remain unchanged.

## Permission catalog

Student submodules normally expose `View`, `Add`, and `Delete`. Workflow Builder retains compatibility
`Approve`, `ManageGovernance`, and `Rollback` grants; current direct publication still requires
Workflow Add and does not depend on those retired builder controls.
Module visibility permissions under `ShikshaMitra.Module.Students.*` remain separate and only control whether the corresponding module or submodule is visible.
Student Dashboard live reads require `Students.Dashboard.View`; the legacy Reporting snapshot refresh command requires `Students.Dashboard.Add`.

## Admission Cycle endpoints

Base route: `/api/student/enrollment/admissions/cycles`.

| Method | Route | Permission | Purpose |
|---|---|---|---|
| GET | `/` | `Students.Admissions.View` | Server-paged search and Academic Year, lifecycle, or window filtering |
| GET | `/{id}` | `Students.Admissions.View` | Details with lifecycle, window, schedules, and usage counts |
| GET | `/{id}/impact` | `Students.Admissions.View` | Offering and Application dependency counts |
| GET | `/{id}/history` | `Students.Admissions.View` | Immutable lifecycle/activity history |
| POST | `/` | `Students.Admissions.Add` | Create Draft |
| PUT | `/{id}` | `Students.Admissions.Add` | Edit Draft using concurrency stamp |
| POST | `/{id}/validate` | `Students.Admissions.Add` | Return publish errors and warnings |
| POST | `/{id}/publish` | `Students.Admissions.Add` | Publish a ready Draft |
| POST | `/{id}/reopen` | `Students.Admissions.Add` | Reopen Closed cycle with reason and optional new end date |
| PUT | `/{id}/window` | `Students.Admissions.Add` | Revise Published/Closed dates with reason |
| PUT | `/{id}/schedule` | `Students.Admissions.Add` | Set future publish and/or close action |
| POST | `/clone` | `Students.Admissions.Add` | Create independent Draft copy |
| POST | `/{id}/close` | `Students.Admissions.Delete` | Stop new applications with reason and dependency acknowledgement |
| POST | `/{id}/archive` | `Students.Admissions.Delete` | Archive a Closed cycle with reason |
| DELETE | `/{id}` | `Students.Admissions.Delete` | Permanently remove only an unused Draft |

List results include `status`, computed `windowStatus`, `isAcceptingApplications`, Academic Year
name, schedule timestamps, and usage counts. All action DTOs carry the current concurrency stamp.
Close, reopen, archive, and window revision require a reason of at most 500 characters. Cycle
description is optional and limited to 1000 characters.

## Offering endpoints

Create, clone and bulk writes normalize new offerings to General/Inquiry. Application form references are resolved from the selected workflow's configured Form stages; the caller cannot override an explicit workflow form. Inquiry form remains selectable. Bulk `stageId` is optional and each item's actual stage is resolved from its Standard before tenant, hierarchy and UserScope validation. Read DTOs retain historical enums and references for compatibility.

Fresh Admissions direct-registration and direct-registration family-match endpoints reject with the existing direct-entry error. Ordinary application start requires Inquiry evidence. The server-internal Records return coordinator and existing applications retain their historical lifecycle.

| Method | Route | Permission |
|---|---|---|
| GET | `/api/student/enrollment/admissions/offerings` | `Students.Admissions.Offerings.View` |
| GET | `/api/student/enrollment/admissions/offerings/{id}` | `Students.Admissions.Offerings.View` |
| POST | `/api/student/enrollment/admissions/offerings/cycles/{admissionCycleId}` | `Students.Admissions.Offerings.Add` |
| PUT | `/api/student/enrollment/admissions/offerings/{id}` | `Students.Admissions.Offerings.Add` |
| POST | `/api/student/enrollment/admissions/offerings/{id}/activate` | `Students.Admissions.Offerings.Add` |
| POST | `/api/student/enrollment/admissions/offerings/{id}/deactivate` | `Students.Admissions.Offerings.Delete` |
| DELETE | `/api/student/enrollment/admissions/offerings/{id}` | `Students.Admissions.Offerings.Delete` |
| POST | `/api/student/enrollment/admissions/offerings/{id}/clone` | `Students.Admissions.Offerings.Add` |
| POST | `/api/student/enrollment/admissions/offerings/validate` | `Students.Admissions.Offerings.Add` |
| GET | `/api/student/enrollment/admissions/offerings/{id}/availability` | `Students.Admissions.Offerings.View` |
| POST | `/api/student/enrollment/admissions/offerings/cycles/{admissionCycleId}/bulk` | `Students.Admissions.Offerings.Add` |
| GET | `/api/student/enrollment/admissions/offerings/{offeringId}/class-capacity` | `Students.Admissions.Offerings.View` |
| GET | `/api/student/enrollment/admissions/offerings/{offeringId}/class-capacity/rooms` | `Students.Admissions.Offerings.View` |
| PUT | `/api/student/enrollment/admissions/offerings/{offeringId}/class-capacity` | `Students.Admissions.Offerings.Add` |

The compatibility create route accepts `POST /api/student/enrollment/admissions/offerings?admissionCycleId=...`.
Class capacity is shared by all tracks for the same academic year and Standard. PUT accepts distinct `roomIds`; it assigns existing positive-capacity rooms to that class, not a manually entered seat number. The GET summary returns `capacity`, `occupiedCount`, `remainingSeats`, `isFull`, `isOverCapacity`, and a non-blocking warning. With no assigned rooms, capacity is unknown rather than zero. The room-choice response flags rooms allocated to a different class.

## Workflow endpoints

| Method | Route | Permission |
|---|---|---|
| GET/POST | `/api/student/enrollment/admissions/workflows` | Workflow `View` or `Create` |
| GET/PUT/DELETE | `/api/student/enrollment/admissions/workflows/{id}` | Workflow `View`, `Edit`, or `Delete` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/clone` | `Students.Admissions.Workflows.Add` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/new-version` | `Students.Admissions.Workflows.Add` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/validate` | `Students.Admissions.Workflows.Add` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/publish` | `Students.Admissions.Workflows.Add` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/governance/publish` | `Students.Admissions.Workflows.Add` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/submit-for-approval` | `Students.Admissions.Workflows.Add` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/approve` | `Students.Admissions.Workflows.Approve` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/reject` | `Students.Admissions.Workflows.Approve` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/rollback-draft` | `Students.Admissions.Workflows.Rollback` |
| POST | `/api/student/enrollment/admissions/workflows/{id}/archive` | `Students.Admissions.Workflows.Delete` |
| GET | `/api/student/enrollment/admissions/workflows/{id}/versions` | `Students.Admissions.Workflows.View` |
| GET | `/api/student/enrollment/admissions/workflows/{id}/usage` | `Students.Admissions.Workflows.View` |
| GET | `/api/student/enrollment/admissions/workflows/{id}/governance/history` | `Students.Admissions.Workflows.View` |
| GET/PUT | `/api/student/enrollment/admissions/workflows/governance/settings` | Workflow `View` or `ManageGovernance` |
| GET | `/api/student/enrollment/admissions/workflows/governance/approvers` | `Students.Admissions.Workflows.View` |
| GET | `/api/student/enrollment/admissions/workflows/governance/notifications` | `Students.Admissions.Workflows.View` |
| POST | `/api/student/enrollment/admissions/workflows/governance/notifications/{notificationId}/read` | `Students.Admissions.Workflows.View` |
| GET | `/api/student/enrollment/admissions/workflows/stage-types` | `Students.Admissions.Workflows.View` |

Both workflow publish endpoints execute immediate publication with the existing Workflow Add,
tenant ownership, graph validation and concurrency checks. Stored maker-checker settings do not
block publication. The compatibility governance payload's effective time no longer schedules work;
old schedule rows and audit/notification read endpoints remain available without automatic
publication or new workflow-specific notification delivery. Compatibility approval/settings and
rollback endpoints keep their original permission enforcement and historical data contracts.
Rollback still creates a separate Draft and never rewrites an Offering or Application reference.

## Runtime workflow endpoint

`POST /api/student/enrollment/admissions/applications/{id}/workflow/advance` requires `Students.Admissions.Applications.Add`.
The body includes `expectedCurrentStageInstanceId`, a unique `idempotencyKey`, optional `concurrencyStamp`, and optional transition, skip, override, and reason fields.
Admin overrides additionally require `Students.Admissions.Applications.Add`.
The response returns current workflow status, stage timestamps, immutable transition history, and `nextRequiredAction`. The next action is derived from the server stage-type registry and is `null` after the workflow completes.

## Application form runtime

| Route | Permission | Behavior |
|---|---|---|
| `GET /api/student/enrollment/admissions/applications/{id}/form` | `Students.Admissions.Applications.View` | Returns the pinned form definition/version, instance status, and saved values. |
| `PATCH /api/student/enrollment/admissions/applications/{id}/form-values` | `Students.Admissions.Applications.Add` | Saves a draft with concurrency checking and rejects unknown field codes. |
| `POST /api/student/enrollment/admissions/applications/{id}/submit` | `Students.Admissions.Applications.Add` | Validates visible fields and stores an immutable submission snapshot. |
| `POST /api/student/enrollment/admissions/applications/{id}/form/reopen` | `Students.Admissions.Applications.Add` | Reopens a submitted form for authorized correction and records the event. |
| `GET /api/student/enrollment/admissions/applications/{id}/form/history` | `Students.Admissions.Applications.View` | Returns form creation, draft-save, submission, and reopen history. |

Published form definitions are immutable versions. Existing applications retain their original `FormDefinitionId` and `FormVersion` when an offering is moved to a later version.

Inquiry and Registration form definitions publish directly from Draft after validation, with the existing form publish permission and concurrency checks. Form create/update do not create an approval state, and the form API no longer exposes submit-for-review, approve, reject, or admin-override review commands. Historical form approval metadata and audit entries remain readable in storage for compatibility but do not gate publication; application decision approvals are separate and unchanged; workflow publication follows the direct command described above.

Choice fields support either static option arrays or an approved source descriptor such as `{ "source": "EducationBoards" }` or `{ "source": "EducationMediums", "parentField": "BOARD_FIELD" }`. Approved source metadata is exposed by `GET /api/student/enrollment/admissions/forms/metadata/data-sources`; tenant-scoped options are read through `GET /api/student/enrollment/admissions/forms/metadata/options?sourceKey=...&parentId=...`. Arbitrary URLs and executable data sources are not accepted, and submission validates selected values against the authoritative source.

Form list, detail, create, and update contracts expose `purpose`; new forms support `Inquiry` or `Registration`, and list requests may filter by purpose. The additive form-purpose migration may have classified earlier test definitions as `Correction` or `Consent`; those values are retained for reading existing records, but new drafts, clones, and versions cannot use them.

## Assessment endpoints

The route prefix is `/api/student/enrollment/admissions/assessments`.
Responses use the standard `Result<T>` envelope.
Permission names below use only `Students.Admissions.Assessments.View`, `.Add`, and `.Delete`.

| Method and suffix | Permission | Behavior |
|---|---|---|
| `GET /scopes` | `View` | Scoped offering/stage choices, including versions pinned to existing applications |
| `GET /applicants` | `View` | Paged assessment work queues and session rosters with scoped counts, evidence, blockers and permitted actions |
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
Scope choices also include optional `offeringCode` and `workflowName` for readable disambiguation of offerings and pinned workflow versions; these labels reuse already-loaded records and do not affect identity or authorization.
The configuration shape is `{ "assessment": { "version": 1, "enabled": true, "schedulingEnabled": false, "attendanceRequired": false, "scoringEnabled": true, "totalMarks": 100, "passingMarks": 40, "reviewRequired": false, "maxAttempts": 1 } }`.
Version 1 supports 1 to 10 attempts and positive total marks up to 10000, with passing marks between zero and the total.
Attendance can also be required in result-only mode.

Session input requires offering/stage identifiers, name, ISO date-times, location, and capacity from 1 to 10000.
Session update/cancellation requires the session concurrency stamp.
Attempt creation and stage waiver require the application stamp returned in the assessment summary.
Other attempt mutations require that attempt's concurrency stamp.
Review uses `approved: false` plus notes to request correction, and waiver uses `reason`.
Attempt mutations return the refreshed assessment summary.

The applicant workbench requires `admissionOfferingId` and `workflowStageDefinitionId`; optional filters are `sessionId`, `search` (name/application number), and `queue` (`toBook`, `attendance`, `result`, `ready`, `attention`, `completed`). It accepts `skipCount`, `maxResultCount` (1-1000), and allow-listed `sorting` (`applicantName`, `applicationNumber`, `sessionStartsAt`, `enteredAt`, with optional `asc`/`desc`). The result contains `totalCount`, paged `items`, and `counts`; counts honor offering, workflow version, session, academic authorization and search before the queue/page filter. Each row contains application/stage identity and the same assessment summary as the individual read. Active stages without attempts are included; completed stage history remains readable. For active stages, a selected session roster requires the latest non-cancelled attempt to belong to that session, so a retake booked elsewhere is excluded from the old session's actionable roster. Inactive/completed stages retain historical non-cancelled bookings to the selected session; their actions are disabled.

`ready` means completed evidence has an eligible route under the existing workflow rules, independently of the caller's advance permission. Failed/absent results without an explicit matching route remain `attention`. Independent result review is disabled in new saved Assessment policies; the frontend does not expose a Review queue or result-review action. Result-only attempts use `toBook` for the initial start action. Classification batches scoped candidates, latest non-cancelled attempts, grouped attempt counts, sessions and route facts without per-application queries. Complete history and action DTOs are expanded only for the requested page. Configured JSON routes still require candidate classification before queue pagination, so memory/CPU scale with the selected offering/stage/search population. This is a workbench query, not a tenant-wide reporting export; concurrent production load and very large scopes require separate staging acceptance.

Available condition facts are `assessment.outcome`, `assessment.marks`, `assessment.attemptNumber`, `assessment.attendance`, and `assessment.reviewed`.
Current configured results become authoritative without a separate result-review step.
Negative outcome routes must contain a matching `Equals` or array-valued `In` condition on `assessment.outcome` and satisfy the complete route expression.
Unknown policy versions, stale stamps, unavailable capacity, missing evidence, and missing negative-outcome routes are rejected using `Admissions:Assessment:*` error codes.

## Inquiry management APIs

The inquiry collection route prefix is `/api/student/enrollment/admissions/inquiries`.

| Method | Route | Permission | Purpose |
| --- | --- | --- | --- |
| `GET` | none | `Students.Admissions.Inquiries.View` | Page and server-sort the CRM queue; filters include cycle, offering, status, source, `assignedCounsellorId`, `isAssigned`, `workQueue`, and search |
| `GET` | `export` | `Students.Admissions.Inquiries.View` | Export the complete filtered/sorted queue as injection-safe UTF-8 CSV, capped at 50,000 rows |
| `GET` | `{inquiryId}` | `Students.Admissions.Inquiries.View` | Read one scoped inquiry, including language and concurrency stamp |
| `GET` | `{inquiryId}/timeline` | `Students.Admissions.Inquiries.View` | Read up to 200 most recent inquiry audit and communication activities, including optional `metadataJson` for distinguishing form edits from status-only changes |
| `PUT` | `{inquiryId}` | `Students.Admissions.Inquiries.Manage` | Update pre-application identity, contact, source, and preferred language using the concurrency stamp |
| `GET` | `counsellors` | `Students.Admissions.Inquiries.Assign` | Find up to 50 active counsellors; an optional `admissionOfferingId` limits results to staff with access to that offering |
| `POST` | `{inquiryId}/assign` | `Students.Admissions.Inquiries.Assign` | Assign manually or through the configured candidate pool after rechecking active status and offering scope |
| `POST` | `bulk/assign` | `Students.Admissions.Inquiries.Assign` | Atomically apply one validated assignment to up to 100 scoped inquiries |
| `POST` | `{inquiryId}/communications` | `Students.Admissions.Inquiries.Manage` plus Communication permissions | Create a family-facing Communication campaign linked to the inquiry; `Communication.Campaigns.Add` is always required and `Communication.Campaigns.Send` is additionally required for immediate delivery |
| `POST` | `duplicate-check` | `Students.Admissions.Inquiries.Manage` | Find current-cycle duplicate signals and same-mobile families visible in staff scope across admission cycles before creation |

`isAssigned=false` is the authoritative Unassigned queue. Combining `isAssigned=true` with `assignedCounsellorId` produces a specific counsellor queue without loading and filtering all inquiries in the browser.

`workQueue` is server-filtered before counting and paging: `1` Overdue, `2` Needs first contact, `3` Needs qualification, and `4` Open follow-ups. List rows include cycle/offering labels, assigned-counsellor name, management state, optional SLA deadlines, qualification outcome, next scheduled follow-up, open-follow-up count, and overdue flags so clients do not issue one management request per row.

Inquiry communications accept WhatsApp, SMS, and Email only. Email requires both an inquiry email address and a subject. The command creates an explicit one-recipient Communication campaign with Student source metadata, preferred language, and the cycle's academic year. Provider availability, consent, approval, delivery attempts, retries, and usage remain owned by the Communication module. A failed immediate publish returns the preserved draft plus `deliveryErrorCode`; the CRM does not hide the failed handoff or create parallel delivery records. Scheduling is intentionally absent until the Communication publisher supports scheduled execution end to end.

The route prefix is `/api/student/enrollment/admissions/inquiries/{inquiryId}/management`.

| Method | Route suffix | Permission | Purpose |
| --- | --- | --- | --- |
| `GET` | none | `Students.Admissions.Inquiries.View` | Read safe policy, deadlines, blockers, capabilities, qualification, follow-ups, notes and offset-safe `nextReminderAt` |
| `POST` | `contact` | `Students.Admissions.Inquiries.Manage` | Record a structured contact channel, direction, outcome, and note; advance contact readiness |
| `POST` | `follow-up-notes` | `Students.Admissions.Inquiries.Manage` | Atomically record a note and inquiry status, clear previous open reminders and optionally create the replacement reminder |
| `POST` | `follow-ups` | `Students.Admissions.Inquiries.Manage` | Schedule a bounded future follow-up |
| `POST` | `follow-ups/{followUpId}/complete` | `Students.Admissions.Inquiries.Manage` | Complete an open follow-up with an optional note |
| `POST` | `follow-ups/{followUpId}/cancel` | `Students.Admissions.Inquiries.Manage` | Cancel an open follow-up with a reason |
| `POST` | `qualification` | `Students.Admissions.Inquiries.Manage` | Record a checklist-backed qualification outcome and standard closure code when closed |
| `POST` | `reopen` | `Students.Admissions.Inquiries.Manage` | Reopen a non-converted qualification with a reason |

Workflow setup continues to use `GET /api/student/enrollment/admissions/inquiry-settings/counsellors` with workflow view permission. Runtime assignment uses the inquiry collection `counsellors` route with the dedicated Inquiry Assign permission.
Every management command requires the current management concurrency stamp.
`follow-up-notes` additionally requires the inquiry's current `inquiryConcurrencyStamp`, a trimmed nonblank `note` of at most 500 characters and `status` exactly `0` (Pending), `2` (Interested) or `6` (Not interested).
Pending and Interested require an offset-bearing `reminderAt` strictly in the future and within one year, validated against the UTC clock instant.
Not interested ignores any supplied reminder and clears open reminders without creating another.
Scope, actor restrictions, both stamps, locked/duplicate state and counsellor eligibility are checked before mutation.
Replacement cancels prior Scheduled reminders with an audited superseded/closure reason and preserves their history; an existing maximum-open limit does not block this replacement, while the general schedule endpoint retains its limit.
The status, note and reminder changes commit in one transactional unit of work; first Interested retains the existing qualification-email intent guard.
The response contains `notes: [{id,occurredAt,note,status,reminderAt,authorName}]`; older notes without a recorded status expose null, and automated reminder cancellations do not create staff-note cards.
`nextReminderAt` uses the original offset snapshot only when activity metadata identifies the exact active task, then falls back to UTC normalization for older tasks without that evidence.
New note metadata includes `followUpId`; `ScheduledAt` binds through a field-level timezone-aware converter, with no global clock change or rewrite of existing persisted tasks.
Internal policy snapshot JSON and qualification-answer JSON are never returned directly.
Create Inquiry accepts `duplicateOverrideReason`; enabled policies can block duplicates or require this reason when staff explicitly create a separate family.
`existingFamilyId` is accepted only for an active, admission-scope-visible family whose primary guardian mobile
matches the normalized mobile in the request. A duplicate-check result based only on name or email is not
authorization to link that family. Duplicate-check marks each result with `canLinkFamily` for the supplied
guardian mobile; Create Inquiry rechecks the rule rather than trusting that preview flag.
Cross-cycle matches are limited to the supplied guardian mobile and an applicant visible through a
scoped Inquiry or Application; name/email-only duplicate signals remain within the requested cycle.

## Applicant 360 APIs

The Applicant 360 route prefix is `/api/student/enrollment/admissions/applicants`.

| Method | Route | Permission | Purpose |
| --- | --- | --- | --- |
| `GET` | `{applicantId}` | `Students.Admissions.Applicants.View` | Read the applicant, family, primary guardian, applications, ordered workflow stages, recent activity, and staff notes as one workspace response |
| `PUT` | `{applicantId}/profile` | `Students.Admissions.Applicants.Add` | Correct the applicant identity before enrollment or finalization locks the Admissions copy |
| `GET` | `{applicantId}/activities` | `Students.Admissions.Applicants.View` | Read the 200 most recent applicant activities, newest first |
| `GET` | `{applicantId}/notes` | `Students.Admissions.Applicants.View` | Read internal staff notes, newest first |
| `POST` | `{applicantId}/notes` | `Students.Admissions.Applicants.Add` | Add an internal staff note and append the related activity |
| `PUT` | `{applicantId}/notes/{noteId}` | `Students.Admissions.Applicants.Add` | Update a note that belongs to the same applicant and append the related activity |

Application rows include creation, last-save, and submission times. Each workflow stage includes `EnteredAt` and `CompletedAt`, allowing the client to explain the current admission position without reconstructing timestamps from activity text. The activity cap is applied in the database query so the Applicant 360 response remains bounded for long-running schools.

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
| POST `applications/{applicationId}/stages/{stageInstanceId}/requirements/{code}/upload` | `Students.Admissions.Documents.Add` | Multipart `file`, `concurrencyStamp`, optional `notes`; appends a fresh revision |
| POST same requirement route `/original` | `Students.Admissions.Documents.Add` | Retained endpoint; unavailable for current upload-only stage policies |
| POST same requirement route `/waive` | `Students.Admissions.Documents.Delete` | Retained endpoint; unavailable because current stage policies disable waivers |
| POST `evidence/{evidenceId}/review` | `Students.Admissions.Documents.Add` | JSON `concurrencyStamp`, `decision: accepted/rejected`, optional `reason` (required for rejection) |
| GET `evidence/{evidenceId}/download` | `Students.Admissions.Documents.View` | Scoped private attachment, no-store response, audit activity; storage keys never exposed |
| GET `document-types` | `Students.Admissions.Workflows.View` | Tenant catalog mappings, allowed extensions and maximum file size |
| GET `classes` | `Students.Admissions.Workflows.View` | Tenant class IDs, names, and academic-year/board/medium/stage context for requirement applicability |

All mutations return refreshed stage state and serialize against the application stamp. History is read-only after leaving the stage. Server authorization validates tenant, offering UserScope, application-stage ownership, frozen workflow version, current stage, policy, and evidence revision. Error family `Admissions:Documents:*` provides localized invalid-policy/state/evidence/review, stale-stamp, and completion-gate messages.
The current stage policy clears collectionFormStageCode and disables early Form collection, original checks and waivers. Uploads require the active Documents stage; accepted/rejected evidence review remains available. Student document category mapping is retained.

Linked Student evidence uses GET `/api/student/records/students/{studentId}/admission-evidence` with both `Students.Records.StudentList.View` and `Students.Admissions.Documents.View`. Its `/{evidenceId}/download` uses the same Admissions document `View` permission and verifies the finalized source application and Student link.

### Student record profile

GET `/api/students/records/{studentId:guid}/profile` requires `Students.Records.StudentList.View` on both the controller and application service.
It returns `Result<StudentRecordProfileDto>` with matching canonical GUID `id` and `studentId`, current names, numeric `grNo` and alphanumeric `grNumber`, roll number, standard/division/academic-medium IDs and names, lifecycle `status`, date of birth, and gender.
Optional identity and placement values remain null when absent.
The read projects scalar fields from the current Student record; it does not return the aggregate, an inquiry snapshot, financial data, or photo bytes.
Tenant filtering and the shared Student direct-record guard apply before projection; explicit academic-year, board, medium, stage, standard, or division grants determine access, and ancestor IDs from a narrower assignment do not grant wider access.
The same direct-record guard protects linked admission-evidence reads and downloads.
This endpoint does not add a Student write or change lifecycle behavior, and it requires no schema migration.

### Onboarding documents

GET/POST `/api/student/onboarding/{caseId}/documents` requires `Students.Onboarding.View`; POST also requires `Students.Onboarding.Add`. Uploads validate the scoped active case, permitted Student lifecycle, tenant document type, file evidence, and concurrency, then refresh readiness. Replacing an existing file preserves its old private blob until the transaction commits successfully.

### Onboarding configuration

GET `/api/student/onboarding/settings/configurations` requires `Students.Onboarding.View` and returns school configuration plus the requested academic-year override. PUT requires `Students.Onboarding.Add`, scope access and a concurrency stamp, and validates rule hierarchy, overlap, dates and numbering bounds. DELETE `/api/student/onboarding/settings/configurations/{id}` requires configuration permission, scope access and concurrency. Runtime resolves this configuration for readiness, account requirements and numbering; case commands enforce capacity, uniqueness and activation.

GET `/api/student/onboarding/settings/campuses` requires onboarding configuration permission and returns active tenant campuses for the optional campus selector in readiness, GR, Roll, and Division rules. Saved campus-specific coverage is validated against active campuses. Omitting campus coverage preserves the existing all-campus behavior; this endpoint does not modify Academic Setup or Finance.

PUT `/api/student/onboarding/{id}/admission-route` accepts the four built-in routes or a school-defined route present in the case's effective onboarding configuration. The server canonicalizes the saved route name, checks the case concurrency stamp, resets approval review, and recalculates readiness. Unconfigured route names are rejected.

GET `/api/student/onboarding/{id}/portal-accounts` reads scoped account requirements and links. POST requires onboarding management plus `AbpIdentity.Users.Create`, saved contact-email equality and explicit contact verification. Linking an existing guardian additionally requires `AbpIdentity.Users.Update` and an already linked, active, confirmed Parent-only identity. New identities are passwordless; staff cannot reuse privileged identities or supply a password. GET `/api/student/onboarding/portal/my-students` requires an active, confirmed identity and returns only that identity's tenant-local student associations.

`StudentPortalAccessService` supplies the same association policy to the mobile timetable context/schedule endpoints. It accepts active persisted portal links and the existing `Student.UserId` self association when that user/student pair has no explicit self-link history. A soft-deleted explicit self link suppresses the older association, so revocation cannot restore access through fallback. Names, email matches, client roles and staff wildcard scopes never establish a student association. Reads do not create links, reseed identities or change grants.

Mobile timetable context uses active current-year `StudentEnrollment` placement first. For legacy Admission/Enrolled students with no enrollment history in that year, it uses the student's saved current-year class/division, matching the Academic 360 placement source. Inactive or soft-deleted enrollment records prevent that fallback. Class/year/medium/division consistency and timetable publication checks still apply. Schedule responses contain timetable data only, not the rest of the Academic 360 overview.

For newly created demo students, `StudentSeederContributor` creates the login with `IdentityUser.Id = Student.Id` and saves the same value in `Student.UserId`. The existing-tenant guard skips student seeding when records exist; reruns do not rekey identities or replace passwords/roles. Existing logins with different IDs remain resolved through their persisted student association.

POST `/api/student/onboarding/{id}/service-consent/confirm` requires `Students.Onboarding.Add` and scoped access to an enrolled, unfinished case with a required `service-consent` handoff. The body supplies the case `concurrencyStamp` and a 3-200 character saved-consent `evidenceReference`. The server audits the staff confirmation, acknowledges only that handoff, and rechecks the independent Finance and portal-account completion gates. Repeating an already acknowledged confirmation is idempotent; it does not collect consent or write money.

The existing Account reset-password contract validates the protected owner token. First password setup confirms email only for a passwordless linked Student-only or Parent-only identity. Invalid/replayed tokens do not confirm email. No automatic message delivery is performed by onboarding.

Case list/detail responses include configured `intendedBoardName` and `intendedMediumName`, plus independent `studentAccountRequired`, `guardianAccountRequired`, `studentAccountStatus` and `guardianAccountStatus`. Before enrollment, requirements resolve from effective saved settings; after enrollment, including completed cases, they come from persisted required handoffs. The optional pre-activation finance/accounts checklist item is not evidence that post-activation fee or portal provisioning is optional. Account handoff acknowledgement means linking is complete, not that the owner has verified email or set a password.

## Eligibility APIs

Eligibility, Review, and Offer are retired Admission Stage types. New drafts cannot select them, publication rejects them, application workspaces do not mount their working panels, and their direct admission operations reject unsupported stages. Existing enum numbers and stored audit records remain intact. Inquiry qualification, Student Records reviews, class capacity, and Finance ownership are separate contracts.

## Review APIs

Eligibility, Review, and Offer are retired Admission Stage types. New drafts cannot select them, publication rejects them, application workspaces do not mount their working panels, and their direct admission operations reject unsupported stages. Existing enum numbers and stored audit records remain intact. Inquiry qualification, Student Records reviews, class capacity, and Finance ownership are separate contracts.

## Managed Form stage APIs

Base: `/api/student/enrollment/admissions/applications/{applicationId}/form-stages/{stageId}`. Read requires `Students.Admissions.Applications.View`; verification requires `Edit`; correction requires `Reopen`. The application service repeats every permission check and validates tenant, Offering UserScope, pinned workflow, stage identity, and frozen form identity.

| Method and relative route | Behavior |
|---|---|
| GET base | Return pinned policy, form status, deadline, attempts, verification state, blockers, concurrency and capability flags |
| POST `sections/{sectionId}/verification` | Verify or revoke one configured section; revocation requires a reason |
| POST `correction` | Require a reason, append audit history, reopen the form, and invalidate earlier section verification |

Commands carry the application `concurrencyStamp`; route identifiers are excluded from bodies. Managed submit also enforces the deadline and maximum attempts through the existing application-form endpoint. A managed stage may hold after submit for verification or explicit continuation. Missing or disabled `configurationJson.form` retains legacy Form behavior. Offering configuration rejects a Form-stage definition that differs from the Offering application form.

## Managed Payment stage APIs

### Application fee selection

Base: `/api/student/enrollment/admissions/fee-selections/applications/{applicationId}`.
The service returns server-controlled ownership, requiredness, locking and confirmation capabilities alongside an optional saved Finance-reference snapshot.

| Method and relative route | Contract |
|---|---|
| GET base | Read the application's saved selection and current capabilities |
| GET `/candidates` | Read eligible published Finance structures for the Offering class and admission-cycle academic year |
| PUT base | Save `structureId`, `optionalHeadIds` and application `concurrencyStamp`; mandatory heads are included by the server |
| POST `/confirm-onboarding` | Explicitly confirm the saved choice with application `concurrencyStamp` |

Reads accept the existing Applicant View, Application View or linked Onboarding View capability, with record scope enforced separately.
Saving requires `Students.Admissions.Applications.Add`; confirmation requires the existing Student Onboarding edit capability.
These endpoints preserve the standard `Result<T>` envelope.
They do not create Finance assignments, dues, allocations or receipts.

Payment summary reads remain at `/api/student/enrollment/admissions/payments/applications/{applicationId}/stages/{stageId}` and expose feeStructureRequired/feeStructureConfirmed, blockers and completion readiness. Record/review/waive/reverse capabilities are false; the supported Payment stage has no admission monetary collection policy and rejects those commands. Existing payment permission identifiers remain intact.

Confirmation saves through the existing fee-selection PUT with structureId, optionalHeadIds and current application concurrencyStamp. Workflow advancement remains a separate permissioned/idempotent command. Finance assignment and payment APIs are unchanged.

## Managed Offer stage APIs

Eligibility, Review, and Offer are retired Admission Stage types. New drafts cannot select them, publication rejects them, application workspaces do not mount their working panels, and their direct admission operations reject unsupported stages. Existing enum numbers and stored audit records remain intact. Inquiry qualification, Student Records reviews, class capacity, and Finance ownership are separate contracts.

Application counsellor list/assignment endpoints remain under `/api/student/enrollment/admissions/applications`, with current tenant, Offering hierarchy and concurrency enforcement. Retired Offer operations do not change application ownership.

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
## Student dashboard live reads

Base route: `/api/students/dashboard`. DTOs and interface: `src/ShikshaMitra.Application.Contracts/Students/Dashboard/StudentModuleDashboardDtos.cs`.

| Method and path | Contract and authority |
|---|---|
| GET `/context` | Dashboard.View; active tenant database identity. Returns `{mode,displayName,scopeLabel,sections,links,myStudents}`. Links include their exact action/visibility grants. Sections appear only with destination access and academic scope. Portal-only context returns verified linked student names. |
| GET `/sections/{section}?academicYearId={guid}` | Dashboard.View plus context section eligibility, destination action/visibility, tenant-owned year and current academic scope. `section` is records/admissions/attendance. Returns `{key,academicYearId,generatedAt,description,metrics,classStrength}`. Metric rows carry id/label/value/tone/href/permission; strength is bounded to 12. |

These live GET reads and frontend refresh require View authority and repeat reads without writes or jobs. Legacy Reporting-backed preview refresh retains its separate Dashboard.Add command authority. Unknown section raises `Students:Dashboard:InvalidSection` (400); denied identity/section/year raises authorization failure (403). Anonymous callers receive 401. ABP rejects an unknown tenant before this controller, normally with 404. No student contacts, compliance identifiers, credentials or raw student records are returned in staff aggregates.
