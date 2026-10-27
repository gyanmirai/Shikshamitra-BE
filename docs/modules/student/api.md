---
module: student
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-27
owner: backend-team
audience: humans-and-agents
---

# Find Student Enrollment Admissions APIs

TL;DR: Admission Offering APIs use `api/student/enrollment/admissions/offerings` and separate read, validation, state, clone, delete, and bulk commands.

## Mobile student profile

`GET /api/mobile/v1/students/{studentId}/profile?academicYearId={academicYearId}` is an authenticated, read-only portal API.
It requires an active self-student or guardian association in the current tenant; Student Records staff grants do not replace portal ownership.
An explicit academic year must belong to the student's available enrollments; omission uses the student's current year.
The response uses the normal `Result<MobileStudentProfileDto>` envelope with `studentId`, `academicYear`, `summary`, and `formSources`.
Each form source contains the retained definition/version, current values, masked field codes, and selected option labels, including repeated field paths.
The retained profile definition survives promotion, matching the web Details tab rather than filtering forms to the selected enrollment year.
Original submissions, change evidence, edit permissions, file bytes and field defaults are excluded.
Configured school masks and protected identity rules are applied before serialization; masked answers are null and identified by `formMaskedFieldCodes`.
Sibling option labels are limited to the caller's linked students; unavailable saved choices are represented without exposing unrelated identities.
No retained form returns an empty `formSources` array, not a fixed substitute schema.
The endpoint is implemented by `Application/Students/Portal/MobileStudentProfileAppService.cs` with shared Records reading/projection helpers; the existing staff endpoint and its permissions are unchanged.

## Application numbering settings

| Endpoint | Permission | Contract |
|---|---|---|
| `GET /api/student/enrollment/admissions/application-numbering` | `Students.Admissions.View` | Current school-wide rule, `expectedVersion`, `isConfigured`, `sequenceLocked`; unconfigured schools receive the legacy APP/calendar-year defaults. |
| `PUT /api/student/enrollment/admissions/application-numbering` | `Students.Admissions.Add` | `expectedVersion`, `prefix`, `separator`, `suffix`, `yearFormat`, `resetPolicy`, `digits`, `startingNumber`; returns the saved immutable revision. |
| `POST /api/student/enrollment/admissions/application-numbering/preview` | `Students.Admissions.View` | Same draft fields plus optional `academicYearId` (required for academic-year format/reset). Returns `nextNumber` and `nextSequence` from the allocation calculation without saving a rule, reserving a number or creating a counter. Stale `expectedVersion` returns `.Changed`. |

Year format: 0 none, 1 UTC calendar year, 2 offering-period academic year (`YYYY-YY`). Reset policy: 0 continuous, 1 calendar year, 2 academic year. Yearly resets require the matching year token. Prefix is 1–20 ASCII letters/digits/hyphens, suffix 0–20; separators are `/`, `-`, or empty; digits 1–12; starting number 1–999999999. Starting number/reset policy lock after first custom issuance. Stale saves return `Students.Admissions.Numbering.Changed`; invalid combinations use `.Invalid`; sequence changes after issuance use `.SequenceLocked`. Existing action permissions and registration permissions are unchanged.

Apply `AddAdmissionApplicationNumbering` before starting the updated API. The migration adds rule/counter tables only and does not rewrite applications or seed school settings.

Changing from the default format to a school prefix carries the existing sequence: `APP-2026-000050` → `DDVS-2026-0051` with four digits. Starting number is a minimum. Calendar reset adopts the matching legacy calendar counter; continuous adopts the highest legacy counter; academic reset adopts issued default APP numbers in that academic year, including soft-deleted applications. Existing custom scope counters and matching historic numbers also set a floor. Preview and issuance share this logic under the same tenant transaction lock; previews are current observations, not reservations. This continuation/preview change adds no schema migration.

## Student action permissions

The Student catalogue exposes only `View`, `Add` and supported `Delete` leaves across 36 features (106 action leaves).
The permission editor labels `Add` as **Add/Update**.
Approve, reject, publish, rollback, waive, finalize, activate, identifier unlock and attendance lock/unlock use the owning feature's `Add` permission.
Lifecycle commands including close, archive, deactivate, cancellation, withdrawal, seat release, payment reversal and document void use the owning feature's `Add` permission. Actual record removal retains `Delete`.
Overrides and Guardian Access have no Delete operation and expose only View and Add.
Permissions come from effective role/user grants; teacher assignments restrict student/class scope and never supply a Student action grant.
Tenant isolation, active identity, configured approval stages, separate approvers, reasons, concurrency and owning Finance/Identity permissions still apply.
Attendance lock/unlock requires MarkAttendance.Add and the same assigned-class scope as marking, with the existing database-administrator scope exception.
Onboarding settings, policy, identifier-series, offering and historical-preview reads use View.
Selected-year reads require academic scope; class-specific configuration is limited to readable classes, while inherited school/year defaults remain visible.
Configuration writes require the whole affected year or class; division-only scope cannot change shared class settings. School-default writes require tenant-wide scope.
Historical reconciliation and placement options use scoped application/division queries; applying a backfill still requires Add.

Existing canonical grants retain their identifiers.
Retired specialized positive grants do not automatically grant broader Add/Update access; administrators must review and explicitly assign canonical actions.
Retired explicit user denials conservatively deny their canonical replacement, including both Add and Delete for the old Transitions.Create denial.
Changing an override replaces that legacy denial while retaining any other affected canonical denial.
Deploy the matching frontend and backend together, review roles/users holding retired grants, and verify effective access before use.
No schema migration or grant reseed is required for this permission change.

## Parent-default contact and recipient contracts

| Contract | Behaviour |
| --- | --- |
| Inquiry / Direct Registration `familyContacts` | Optional `{otherParent?,otherGuardian?,mainContactRelationship}`. Each added person has `fullName`, `relationship`, optional `mobile` and `email`; the selected other guardian needs its own mobile or email. The other-parent relationship must differ from the captured parent. `parentRelationship` and confirmed sibling source IDs remain Father/Mother proof and are never replaced by the main-contact choice. |
| Records `family` snapshot/read/write | Adds `mainContactRelationship` (`father`, `mother`, `guardian`) and `guardianRelationship`. The existing Changes grants, school policy, reason and expected snapshot apply. Omitted sibling IDs preserve existing links. A contact preference is not portal authority. |
| Inquiry `mainSchoolContact` | Additive captured recipient display: `fullName`, `relationship`, `relationshipLabel`, `mobile`, `email`. Messaging uses this contact without a different-person fallback; original inquiry identity fields remain unchanged. |
| Credentials recipients | Adds `isMainContact`, optional `relationshipLabel`; `hasLinkedAccount` identifies the exact relationship and saved mailbox. An occupied adult link cannot be replaced by choosing another person. Generic-guardian email editing remains reviewed-only. |
| Send Credentials | Adult requests additionally require `confirmRecipientAccess: true`; the saved relationship, expected mailbox, case stamp, grants and scope remain authoritative. Student self requests retain their existing contract. |
| Activation `guardianAccessSelection` | Optional `{expectedEmail,confirmRecipientAccess}` permits an explicitly authorised saved guardian instead of an unlinked sibling parent handoff. It validates the current guardian mailbox, enabled access policy, actor authority, conflicts/collisions and case stamp. The case-only choice grants no link by itself and cannot replace an existing adult link. |
| Credentials workspace `guardianAccessEnabled` | Explicit Boolean from saved account policy (`optional`/`required` enabled; `not-needed` disabled), independent of required/readiness status. It does not establish family proof or account authority. |
| Credentials `parentSibling.guardianAccessSelected` | Reports an existing case-only guardian choice, including a legacy case with `enabled: false`. It is not confirmed sibling-family provenance. A changed mailbox needs explicit reconfirmation; linked-owner changes retain review. |

## Portal account owner profiles and case correction

`GET /api/account/my-profile` returns the identity profile with authorized linked student contexts under `extraProperties.studentContexts`.
It also returns `extraProperties.portalSession`: `{version:1,status:"ready"|"restricted",students:[{account,context,contextStatus}]}`.
`account` uses the existing `StudentPortalAccountDto` contract, including student/user IDs, student name, account type, relationship, email, confirmation and activity flags. `context` uses `PortalTimetableContextDto`.
`contextStatus` is `loaded`, `unavailable` (transient context-read failure), or `invalid` (invalid/ambiguous placement); failed contexts are null. Valid no-enrollment/no-published-timetable states remain loaded contexts with their existing `availability` values.
Restricted portal access returns no student records. Association-query failures are errors, never successful empty association lists. Context reads use an isolated read-only unit of work; authorization errors and cancellation are not converted to partial enrollment success.
The profile resolves associations once and batches context reads for all linked students. Legacy `studentContexts` is derived from the successful results. `/api/student/onboarding/portal/my-students` remains available for older consumers.
Deploy this additive backend contract before the updated mobile client. The updated client uses only this profile endpoint for login/shared student details and treats a missing/unsupported `portalSession` as a contract error. Authentication and academic feature endpoints remain separate.
Profile diagnostics record association/context durations, record counts and failure categories; they do not log bearer tokens or profile payloads. Compare staging latency distributions before claiming a transport or database stall is resolved.
Each context's `student` includes nullable `gender` (`0` Male, `1` Female; null when unrecorded), `mediumId`, `mediumName`, `stageId`, and `stageName`, alongside its existing identity, standard, division, and roll number fields.
Medium and stage come from the validated current class placement and remain null when no current academic year, active enrollment, or complete class assignment is available.
The same student fields are returned by `GET /api/mobile/v1/academics/timetable/context`.
Existing confirmed student/guardian association and tenant checks apply; revoked or unauthorized links are excluded from the profile.

The scoped Onboarding case-detail GET includes optional `portalAccountOwners` with account type, relationship, display name and saved email from the current Student contact/family profile. Student/self, guardian/father, guardian/mother and guardian/guardian are distinct recipients. List responses do not load these profile details. The existing case View grant, tenant isolation and case scope apply; provisioning still validates the current saved email and explicit ownership confirmation, and preserves identity/sibling checks. This additive response field has no new endpoint, write or migration.

`POST /api/student/onboarding/{id}/guardian-credential-email/enable` accepts `{reason,concurrencyStamp}` and returns the updated case detail. Controller and application service require Onboarding Configure (currently `Students.Onboarding.Add`), a current tenant/user, scoped case access, current stamp, an activated open case, a reviewed `ReadinessRulesSnapshot` and `guardianAccount=true`. The reason is required (3–400 characters). The command enables only `sendGuardianCredentialEmail` in this case's pinned snapshot, retains every other rule and school/Offering settings, and adds a creation-audited `GuardianCredentialEmailEnabled` event with previous/new values. An already-enabled current-stamp retry is a no-op. The command never provisions accounts, refreshes provisioning or enqueues emails. No schema migration is needed.

General Father/Mother/Guardian contact corrections continue through the existing Records snapshot/change APIs, including their section authority, stale snapshot check, approval policy and guardian-access review.
The explicitly approved Onboarding email-only command is the narrow exception described in [app-login email edits](#onboarding-app-login-email-edits); it does not replace those wider contact/account-repair workflows.
The selected saved email and ownership checks remain mandatory during portal provisioning.

## Decision history display

The existing application decision-history read also returns nullable `actorName`, resolved in one batch from recorded `actorId` values using the tenant-filtered Admissions people service. Missing/deleted actors retain their history with a null name. The existing Applications View grant and application academic/counsellor scope check run before history or people are read. This is an additive display field; no endpoint, workflow command, entity or migration changes are required.

## Academic list filters

`GET /api/students/records/directory` accepts up to 20 `columnFilters` bound with indexed keys, for example `columnFilters[0].column=rollNo&columnFilters[0].operator=between&columnFilters[0].min=2&columnFilters[0].max=7`. Criteria are ANDed with the existing academic year, population, scope selectors and search before counting, sorting and paging. Supported columns/operators are `name`, `grNumber`, `classDivision` with `contains`/`equals`/`startsWith`/`endsWith`; `rollNo` with `equals`/`greaterThan`/`lessThan` (string `value`) or inclusive `between` (`min`, `max`); and `status` with `equals`, values `6` Active, `7` Inactive, `8` Left. Text matches are case-insensitive; GR values remain identifiers. Class/division and roll filters use the same scoped selected-year enrollment projection and fallback as the returned rows. Identity filters cannot match masked identity. Unsupported fields/operators, malformed values, invalid ranges and excessive criteria fail with `Students:Records:InvalidDirectoryFilter`. Existing tenant/scope/grant checks remain in force. Actions is not a supported filter or sort column. No schema migration is required.

Inquiry list/detail responses expose nullable `standardId` and `standardName` from the inquiry's linked admission offering and tenant-filtered class. List reads resolve class names in one batch for the current page. Missing offerings leave both fields null; a missing class leaves its name null. This additive read projection does not change capture, application handoff, permissions or persistence and needs no migration.

Admissions Application, Inquiry (including CSV export), Offering, Pipeline/options, and Review queue list inputs accept `StandardIds` in addition to the existing optional `StandardId`. Student Onboarding list accepts `StandardIds` and `DivisionIds` alongside the scalar filters. Collections bind from repeated query keys, for example `standardIds={guid1}&standardIds={guid2}`. Nonempty collections match any member; scalar, Board, Medium, academic year and other supplied criteria intersect with that match. Empty collections do not restrict classes or divisions. Existing tenant and authorized academic scope applies before counting and paging; these filters do not broaden permissions. No persistence change is required.

## Active admission Pipeline and follow-ups

`GET /api/student/enrollment/admissions/applications/pipeline` requires AdmissionApplications.View and projects active inquiry-linked applications and started RTE/Management/Staff special registrations before Admission Decision within existing tenant and offering scope. Unlinked historical General applications and special rows without a started workflow remain excluded. Direct cases return nullable `inquiryId`, `sourceInquiryAvailable: false`, primary guardian contact data and zero Inquiry follow-ups; no synthetic Inquiry is created. Existing Inquiry-linked contact precedence is preserved. Inquiry follow-up commands still require an available source Inquiry.
It accepts academic hierarchy, MyCases, assigned counsellor, status, stage code, search, whitelisted sorting and bounded paging.
Each row includes `stageProgress` (runtime stage ID, name and status in display order), `nextFollowUpPurpose` (the earliest scheduled inquiry follow-up) and `overdueFollowUpCount` (only scheduled follow-ups before the server UTC clock). Progress is batched for the authorized result page from existing workflow instances, never inferred from a global stage list or from the number of completed steps. These additive read fields do not mutate stages, follow-ups or Finance data and require no database migration.
`GET /api/student/enrollment/admissions/applications/pipeline/options` supplies complete scoped active counsellor/stage choices, current user and the authoritative default queue without page/search truncation.
Administrators identified by the existing user scope and configured fallback managers default to All; other staff default to My.
The queue preference never changes academic access.
Terminal applications, failed/cancelled workflows and cases that have reached Admission Decision are excluded.
`GET /api/student/enrollment/admissions/applications?decisionWorkspaceOnly=true` applies the shared persisted phase rule before search, count, sort and paging and includes Withdrawn cases in the Applications archive; generic list consumers can omit this flag.
Application list/detail DTOs expose `hasReachedDecision`, `decisionEnteredAt`, `standardName`, `contactPhone` and `contactEmail` from existing authorized application, workflow, class and family/inquiry data.
Once a case has entered Admission Decision, correction back to Form or Documents does not restore Pipeline ownership.
An admission journey ended before Admission Decision remains discoverable in Applications with `hasReachedDecision=false` and no invented decision-stage entry.
Next-action guidance comes from the existing stage registry, due dates from published Form/Payment policies and follow-up reminders from existing scheduled inquiry records.
Missing source inquiries remain visible through an application-backed projection with `sourceInquiryAvailable=false`.

Application `GET /{applicationId}/follow-ups` requires Application View; schedule `POST /follow-ups` and complete/cancel `POST /follow-ups/{followUpId}/complete|cancel` additionally require exact Inquiry Manage.
These routes reuse inquiry management, policy, owner restrictions and concurrency stamps, and return `canManageFollowUps`.
Inquiry details/qualification remain locked after transfer, while follow-up commands require an active linked application.
Closed cases reject follow-up writes; an unavailable source inquiry returns a corrective business error.
No new workflow, messaging store or finalization path is created.

## Admission journey preparation and closure

| Contract | Authority and behavior |
|---|---|
| POST `/api/student/enrollment/admissions/applications/{id}/end-journey` | Existing AdmissionApplications.Decide (`Students.Admissions.Applications.Add`) and tenant/academic scope. Requires `{reason, concurrencyStamp}`; the trimmed reason is mandatory and limited to 512 characters. Returns the updated Application DTO with `canEndJourney=false`. |
| Application `canEndJourney` | Available before finalization for Draft, Submitted, InReview, Waitlisted, Approved, Offered and OfferAccepted applications without Student/Onboarding/enrollment links. Rejected, OfferDeclined, Withdrawn, finalized and enrolled applications cannot be closed again. |
| Future-stage preparation | Existing Form draft/verification/correction, Documents evidence/review, Assessment booking/attendance/results and fee-selection permissions remain authoritative. Capabilities permit Pending or Active stages only while the workflow is Active and the application is open; published configuration and independent review rules still apply. |
| Current-stage progression | Form submission (except the existing Inquiry entry handoff), Continue, completion and admission decisions retain their current-stage guards. Preparing evidence does not enter or complete a Pending stage. Fee selection confirms Admissions intent only and never collects payment. |

Ending a journey records Withdrawn with actor, reason, decision history and activity; cancels unfinished stages and unused assessment bookings; releases provisional seat reservations; and cancels scheduled inquiry follow-ups. Completed stage/evidence history and Finance records are retained. A matching pending Records return is closed through its owner service. The transaction validates the application stamp; a repeated successful withdrawal with the same reason returns the saved outcome without duplicating history, even when the retry carries the prior stamp.

## Intake setup lifecycle

Intake setup create/update/archive/restore and intake-profile writes are retired. Existing permission attributes remain, and authorized mutation attempts return `Admissions.StaffAssignmentRetired` without changing records. Legacy read contracts and stored profile references remain for compatibility.

## Intake counsellor options

Admissions no longer selects or assigns a counsellor or Onboarding case owner. Inquiry/application assignment and Onboarding owner commands reject with `Admissions.StaffAssignmentRetired`. Legacy list ownership filters are ignored; action, tenant and academic scope checks remain authoritative. Offering writes ignore intake profile references; readiness still validates workflow, forms, hierarchy and dates.

## Public inquiry link options

`GET /api/student/enrollment/admissions/public-inquiry-links/options?admissionCycleId={guid}` retains Offerings View and returns distinct published Inquiry forms from accessible active Inquiry/Both Offerings. The legacy assignment-profile field is always null. Create retains Offerings Add and requires the selected published form on an accessible active Offering in that period. It stores no assignment profile. Existing links retain their form, token hash, source, expiry and revocation behavior; their former routing profile no longer controls public capture. `Admissions.PublicInquiry.OfferingRequired` remains HTTP 400 with corrective form/Offering guidance and `PeriodName`. No new schema migration is required.

Activity and timeline DTOs return `actorName`; completed workflow and Onboarding steps return `completedByName`, and Onboarding events return `actorName`. New activities/events/transitions snapshot the authenticated server actor. Historical rows resolve retained creator IDs in the current tenant where possible; missing attribution is not replaced by a former case owner.

## Admission student field contracts

| Contract | Behavior |
|---|---|
| Form system-field metadata | `validationJson` describes shared field formats. `destination` distinguishes `studentProfile` from `capturedAdmissionDetails`. The predefined catalogue has 107 fields. |
| Form `metadata/options`, source `StudentSiblings` | Existing AdmissionForms.View authority and current tenant/staff scope. Optional `searchTerm` (at most 100 characters), `skipCount`, `maxResultCount` (default 20, maximum 21) supply a bounded option list. Repeated `selectedIds` GUID parameters hydrate up to 20 selected identities within scope; other data-source behavior remains unchanged. |
| Finalization preview and possible Student matches | `canApplyProfileFieldsDirectly` identifies pending Admission/Registration profiles. `canReviewProfileFields` accounts for Records View/Create authority. `capturedStudentFields` contains `{key,label,value}` rows with a safe photo summary; `profileReviewDescription` explains the handoff and separate Compliance review. |
| Finalization input | `applyStudentProfileFields` defaults to false for existing profiles. New profiles receive supported answers automatically. Protected existing profiles use the Records review service and its current section guards. |

Student captured summaries exclude Aadhaar, APAAR, PEN, UDISE, PAN and raw photo payloads. Their existing canonical storage and read permissions remain authoritative. The original admission evidence stays on the applicant.

## App Login settings

The existing Onboarding settings configuration remains schema version 1, with additive fields inside `readinessAccess`; no new endpoint, table or migration is required.

| Field | Contract |
|---|---|
| `studentAccountAccess`, `guardianAccountAccess` | Optional enum: `not-needed`, `optional`, `required`. When absent, the corresponding legacy account boolean resolves false/true to Not needed/Required. |
| `studentAccount`, `guardianAccount` | Retained required-account booleans. When an access enum is supplied alongside its boolean, it must equal whether that enum is `required`; contradictory payloads are rejected. |
| `credentialProvisioningMode` | Retained `manual`/`automatic`, default manual. The UI preserves existing automatic configuration until explicitly changed. |
| `sendStudentCredentialEmail`, `sendGuardianCredentialEmail` | Retained automatic-email flags; new UI writes enable them only in automatic mode for enabled account types. Manual Send Credentials retains its explicit saved-owner authority without changing school flags. |
| `credentialSendingTime` | Retained `activation`/`completion`; new manual UI saves use activation so staff controls the send moment. Existing case snapshots retain pinned timing. |

Pending cases adopt current school choices through the existing admission-derived snapshot logic. Activated/completed/cancelled snapshots are not rewritten. Optional preparation is allowed on completed active enrollments, but never on cancelled cases; optional email work does not block completion. Existing permission, tenant/scope, identity collision, saved-owner provenance, enrollment, identifiers and document checks remain authoritative. Automatic queue intents are flushed inside the same enclosing transaction so a following manual request reuses the queued intent rather than inserting a duplicate.

## Onboarding credential delivery

| Endpoint | Authority and contract |
|---|---|
| GET `/api/student/onboarding/{caseId}/credentials` | Onboarding.View plus current tenant/user and case scope; returns `caseId`, `concurrencyStamp` and legal saved recipients with `accountType`, `relationship`, `name`, `email`, `canSend`, `blockedReason` and optional delivery status. No credentials or tokens. |
| POST same credentials route | Onboarding.Edit plus current scope; `{accountType,relationship,expectedEmail,concurrencyStamp}` authorizes the selected saved owner, prepares or safely reuses the account and records a durable email intent in one transaction. New preparation additionally requires Identity.Users.Create; verified sibling Parent reuse also requires Identity.Users.Update. Returns the refreshed workspace. |
| POST `/api/student/onboarding/{caseId}/credentials/retry-parent-setup` | Onboarding.Edit plus current tenant/user, target stamp and scope for both this case and the original setup case. Accepts `{concurrencyStamp}`; retries the server-resolved original failed parent setup intent with its existing cooldown and saved-recipient checks. It never accepts an arbitrary source-case ID or creates a sibling email request. |
| POST `/api/student/onboarding/{caseId}/credentials/link-parent` | Onboarding.Edit and Identity.Users.Update plus current tenant, enrolled case scope and stamp. Accepts `{concurrencyStamp,siblingParentSelection?}` and revalidates confirmed family/current parent proof. Attaches an existing parent after enrollment, including completed optional-parent cases, without replaying placement, identifiers, fee binding or enrollment. Returns the credentials workspace; it never creates a parent identity or sibling email intent. |
| GET `/api/student/onboarding/{caseId}/credential-emails` | Onboarding.View and current academic scope; returns student/guardian rows including NotRequested/Blocked, mailbox, enabled flag and server canSend. No password/token fields. |
| POST same route | Onboarding.Edit and current scope; `{accountType, concurrencyStamp?}` uses the onboarding case stamp and current mandatory readiness. Returns durable status; already-pending requests are idempotent. |
| POST `/api/student/onboarding/{caseId}/documents/override` | Retained route rejects required-document override attempts; use canonical workflow evidence and verification. |

Email status DTO includes id, accountType, email, status, message, sentAt, attemptCount, emailEnabled and canSend.
For the explicit Father/Mother admission flow, the credentials workspace adds `parentSibling` with current eligible candidates, selection/link status, optional saved selection, review blockers and server `canLink`/`canRetrySetup` capabilities.
Candidate email and identity ID are drift guards, not permission to link by email alone.
Activation accepts optional `siblingParentSelection: {relationship,expectedEmail,expectedUserId}`; two eligible parents require an explicit choice, while one eligible parent can be selected deterministically.
Reuse requires Onboarding.Add and Identity.Users.Update, current scope, a confirmed family, a verified Father/Mother link for another child, matching current saved parent email and an active Parent-only identity.
An unconfirmed identity additionally requires the exact verified child's existing setup request; no new identity, password or sibling email is created.
Historical generic parent links are eligible only for the exact confirmed source child, matching current saved email and the staff-confirmed new Father/Mother relationship; the old relationship is never relabeled.
Parent access disabled by saved rules grants no link; failed reviewed-selection or contact drift leaves a reviewable access-pending state rather than switching identities.
New parent-only first accounts remain manual even under retained automatic school configuration; Student self-login and legacy guardian behavior are unchanged.
Reused account status is `Linked`, not a fabricated `Sent`; original setup Pending/Failed remains separately visible.
`expectedEmail` detects saved-profile drift and is not an arbitrary destination.
The new command preserves pinned account requirements, readiness and delivery timing; explicit manual requests can send when automatic email is Off without changing school/case flags.
Only an authorized saved-recipient manual intent receives this delivery authority; unmarked automatic requests and legacy commands retain their original email-enabled checks.
The command never invokes a case-only policy override, fabricates mailbox verification or resets an existing password.
Student recipients require explicit Applicant email capture matching the current profile, an applied Records correction that actually changed to the current email, or the explicit scoped Onboarding email-only capture marker matching the current Student profile.
Inherited parent/guardian email is never a Student recipient.
Completed cases allow eligible delivery retries without reopening or replacing their account links.
Completion-timed one-click intents may remain Pending until all non-email completion requirements pass; the dispatcher postpones only this typed timing condition before generating a password.
Exact already-pending owner intent retries are idempotent, while different owners, changed emails and stale new writes are rejected.
Manual resends/retries require the current saved readiness and sender permission, with a two-minute cooldown.
Automatic dispatch preserves the initial account creation grants; exact sender grants and academic scope are checked again before transport.
Email/mobile/username login aliases apply to exclusive Student/Parent identities with email and eligible child access.
An ambiguous mobile match fails closed; the owner verifies the mailbox and changes an initial password through the existing Account setup route.

## Admission-derived Onboarding operations

All routes below are under `/api/student/onboarding`, enforce tenant/academic scope, and retain existing Onboarding permission aliases.

`GET /filter-options` returns scalar academic IDs, names and parent links derived only from the scoped cases, with the optional year filter applied first. It does not materialize the recursively auto-included academic entity graph. View remains independent of the identifier-Unlock capability, which additionally requires configured database administrator membership.

The worklist GET applies all filters and counts in the scoped database query before paging.
`placementDivisionId` narrows current placement alongside the existing Board/Medium/Class/Division selectors.
`studentNameFilter` and `ownerNameFilter` accept `contains`, `equals`, `startsWith` or `endsWith` through their respective operator fields, case-insensitively.
`rollNumberFilter` accepts `equals`, `greaterThan` or `lessThan`; `between` requires ordered `rollNumberMin` and `rollNumberMax` bounds.
Sorting is a fixed whitelist of `StudentName`, `Placement`, `DivisionName`, `OwnerName`, `AssignedToUserName`, `RollNumber` and `Status`, followed by optional `asc` or `desc`.
Every sort has an identifier tie-breaker, and null Roll numbers remain last.
Unknown operators, incomplete/conflicting ranges, overlong text or unsupported sorting reject through the contract's FluentValidation rules rather than broadening the query.
Text filters are bounded to 300 characters and sort expressions to 100; validation failures identify the field and carry the existing `PolicyScopeInvalid` validation metadata.
This is a read-contract correction, not a new bulk assignment endpoint, permission, numbering policy or Finance operation.

| Route | Contract |
|---|---|
| GET `/settings/offerings?academicYearId=...` | View permission and academic scope; compatibility lookup for older clients. The current settings screen does not require an Offering selection. |
| POST `/{id}/requirements/review?concurrencyStamp=...` | Edit permission; compatibility refresh for eligible pending cases. Normal case reads and preparation adopt admission requirements automatically. |
| POST `/{id}/gr-number` | AllocateIdentifier; `{numberSeriesId, manualValue?, manualSequence?, reason?, concurrencyStamp?}`. First manual assignment needs no correction reason. |
| POST `/{id}/roll-number` | AllocateIdentifier; `{rollNumber, rollAssignmentMode?, reason?, concurrencyStamp?}`. Independent from GR assignment. |
| POST `/identifier-locks` | Add/Update (AllocateIdentifier and Repair aliases); `{academicYearId, divisionId, identifierType: gr|roll, isLocked, reason?}`. Both actions enforce the selected division scope; Unlock requires a reason. No Division placement lock. |
| GET `/{id}/fee-structure` and `/fee-structure/candidates` | View; existing Admissions selection/candidate DTOs backed by published Finance structure reads. |
| PUT `/{id}/fee-structure` | Edit; `{structureId, optionalHeadIds, concurrencyStamp?}` confirms admission intent only. No Finance mutation. |
| GET `/{id}/workflow-documents` | View; frozen applicable workflow requirements and canonical evidence. |
| POST `/{id}/workflow-documents/{stageId}/{code}/upload` or `/original` | Edit plus AdmissionDocuments.Upload or RecordOriginal respectively; reuses canonical Admissions evidence contracts. |
| POST `/{id}/workflow-documents/evidence/{evidenceId}/review` | Edit plus AdmissionDocuments.Review; canonical verification, with no required-document waiver bypass. |

Case DTOs retain `requirementsNeedReview=false` for compatible clients, frozen document availability, admission confirmation, fee confirmation, identifier lock and credential pending flags. Pending case reads adopt common checks and current saved account choices once per change and preserve document snapshots; activated and terminal cases are not reconfigured. Filter options expose trusted `canUnlockIdentifiers`; list supports `enteredToday` and task filters including fees/credentials. A successful activation persists enrollment before access preparation; access failure leaves Records-visible enrollment and a retriable credential state.

## Student Attendance API families

| API family | Contract and authority |
|---|---|
| Existing `/api/students/attendance` | GET `/students` and `/history` under Attendance_MarkAttendance.View; POST `/mark`, `/lock` and `/unlock` under Add. |
| Attendance workspace | GET `/api/students/attendance/context?academicYearId=...` returns scoped Directory hierarchy options, server `schoolToday`, current database `isAdmin`, exact active employee-linked `assignedClasses`, and `canManageLock`. GET `/workspace` accepts academicYearId, date, optional Board/Medium/Class/Division, search, whitelisted sorting and bounded paging; returns active Directory students with daily status and applicability reasons. Both require Attendance_MarkAttendance.View; Records permission is not required. |
| `/api/students/attendance/settings/leave-types` | GET paged list and active list under Attendance_SettingLeaveType.View; POST and PUT under Add; DELETE under Delete. These are the only attendance-settings commands. |
| `/api/students/attendance/leaves` | GET list and status counts under Attendance_LeaveAndAbsent.View. Optional `tableFilters` is a JSON array of column, operator, value, and optional min/max bounds for Student, Medium, Class, Division, Leave type, start/end date, total days, and status. Filters apply before count, sort, and paging; date bounds are UTC instants for the user's selected local calendar day. |
| `/api/students/attendance/register` and `/summary` | Register GET uses Attendance_Reports.View; summary GET uses Attendance_MarkAttendance.View. Both require academicYearId and bounded dates. Register permits optional Standard/Division and server paging; summary identifies one student. DTOs return nullable percentage, expected/recorded/missing counts, provisional and school-target metadata. |
| `/api/students/attendance/corrections` | GET under Attendance_LeaveAndAbsent.View; POST request, `/{id}/approve` or `/reject` under Attendance_LeaveAndAbsent.Add. Applying a locked correction additionally checks Attendance_MarkAttendance.Add. Commands require reasons and revisions and prohibit self-review. |

Daily saves require current `revision` when a day exists and `rosterRevision` from the roster response.
Daily roster reads check the exact active employee-linked ClassTeacher assignment before loading student identities, directory enrichment or statistics; tenant-wide UserScope alone does not authorize an unassigned class. History rows intersect the existing tenant/year/academic scope with active teacher assignments to the recorded day, so an assignment revocation also removes that history on the next request. Configured database administrators retain their existing data-scope exception and still require View. The workspace uses the same assignment authority.
`submit=false` saves partial drafts; `submit=true` checks the entire eligible roster.
The current school-date register uses the shared active Directory population. Visible students without applicable dated enrollment/placement remain disabled; historical membership and submitted snapshots remain preserved. Workspace count/search/paging precedes class enrichment; name/GR search and exact numeric roll/GR search respect identity masking.
Mark/Submit requires Add, current database identity, academic scope and an exact active ClassTeacher assignment through an active employee's UserId; configured database administrators retain the existing scope exception but still require the action permission. Claims alone and tenant-wide staff scope do not confer administrator or class-teacher authority. The roster exposes `canMark`, `canSubmit` and `canLock`. Lock/Unlock requires Add and the same assigned-class scope; Lock rejects incomplete, stale or credit-correction-required submitted rosters.
`operationId` identifies an actor/payload-specific durable retry, and `correctionReason` supplies observation conflict or correction evidence.
`isPastMarkingCutoff` informs the UI; `canMark` remains the server capability.
`needsCreditCorrection` identifies a submitted row whose approved-leave link or credit now differs from its recorded snapshot. Ordinary saves cannot refresh that row, and Submit checks omitted rows too. A correction request may retain the same Present/Absent mark when this flag is true; it still requires a reason and follows the school's approval rules. Approval rejects stale recorded credits or changed request-time leave facts.
Correction responses expose `isCreditCorrection` and nullable before/requested present and denominator credits, derived from saved request evidence and policy. The review preview stays pinned when source leave later changes; legacy evidence without recorded credits remains unknown.
The former policy draft, preview, publish and individual-schedule endpoints are removed. New attendance uses fixed default rules; submitted attendance keeps its recorded rule snapshot. Legacy policy and schedule rows remain stored for historical preservation but do not govern new attendance.
Business errors are centralized in `AttendanceOperationErrorCodes` and `AttendancePolicyErrorCodes`, with English attendance localization.
Stale roster/revision, incomplete submission, invalid status/detail, conflicting leave, tenant/scope violations and duplicate retry payloads are rejected atomically.

## Student Records API families

Records preserve each controller's response contract: Directory/Profile, Settings, Transitions, Guardian Access and certificate/ID metadata use the shared `Result<T>` envelope. Changes, Duplicates, Reports and photo-upload metadata return their DTO or paged DTO directly. PDF, photo and Excel downloads return file responses. Permission names use the central `Students.Records.*` catalogue. Profile requires `Students.Records.StudentList.View`; directory search and academic options accept Student List, Student I-Card or Certificates View. Scope and policy masking still apply.

| API family | Commands and permissions |
|---|---|
| `/api/students/records/settings` | GET effective/draft context and `/roles` under Settings.View; PUT `/draft` and POST `/preview` under Settings.Add; POST `/publish` under Settings.Add. Nullable `academicYearId` selects school defaults. |
| `/api/students/records/{studentId}/profile` and `/details` | Records.StudentList.View plus tenant/student and historical enrollment scope. Optional `academicYearId` contextualizes placement and visible journey evidence. Profile adds admission/onboarding milestones and actual credential readiness; Details returns structured availability/current section data and frozen submitted Registration sources, original/current typed values, captured/editable field codes and returning differences. Reads never create custom storage. |
| GET `/api/students/records/{studentId}/details/options` | Record-bound dynamic options query: `submissionId`, `fieldCode`, optional `parentId`, `searchTerm`, `skipCount`, `maxResultCount` (1–21), `selectedIds` (up to 20) and `selectedOnly`. The frozen source determines source kind and academic year. Available choices require Changes.View/Add and an editable field; selected-only labels require Records read and reject search/pagination. All reads enforce source masks, school hierarchy and target sibling Records eligibility/scope/Identity visibility. No Admission.Forms.View grant is required. |
| `/api/students/records/changes/students/{studentId}/snapshot/custom-details` | Existing Changes.View snapshot contract. A `custom-details` request submits `{sources:[{applicationId,formInstanceId,submissionId,values:{FIELD_CODE:typedValue}}]}` with the existing reason, client key and expected snapshot; Changes.Add and the school's Correction approval policy govern application. Unknown/mismatched sources, fields, types, changed hidden fields, stale snapshots and conflicting compatible values fail closed. |
| GET `/api/students/records/{studentId}/details/form-snapshot` | Records.StudentList.View, Changes.View and Changes.Add; full writable scoped profile required. Returns the existing unwrapped snapshot DTO with `section: registration-form`, hash and safe typed `{sources:[{applicationId,formInstanceId,submissionId,values}...]}` for the retained full Registration forms. |
| PUT `/api/students/records/{studentId}/details/form` | Same existing read/create permissions; body `{clientKey,reason,expectedSnapshot,payload}` using the full form snapshot. Immediately applies one atomic `registration-form` Correction audit, returning the unwrapped `StudentRecordChangeDto` with Applied status. This explicit inline profile command does not run school approval stages; generic section/custom correction requests retain their published approval policy. Immutable submitted answers are never rewritten. |
| Canonical Student document actions | The conventional `StudentDocumentAppService` list, required-status and download actions require Records.StudentList.View; upload requires StudentList.Add and delete requires StudentList.Delete, before scoped storage access. Ordinary uploads keep the existing RegistrationFormIssued/Registration lifecycle restriction. Admissions evidence and Onboarding workflow-document commands retain their separate source owners and action grants. |
| `/api/students/records/transitions` | GET paged cases, `/{id}`, `/options`, `/renewals`, `/renewals/{id}` under Transitions.View. Options accept `academicYearId`, optional `studentId` and `admissionOfferingId`; expose permitted placement, return policy, active Offerings with EntryMode, and resumable real applications. Selected Inquiry-capable Offering metadata additionally requires Inquiries.Create and source scope. |
| Transition requests | POST `/preview`, `/batches`, `/{id}/correct`, `/returns`, `/returns/preview`, `/renewals/{id}/confirm`, `/{id}/approve`, `/reject`, `/{id}/apply` and `/renewals/{id}/activate` require Transitions.Add. POST `/{id}/cancel` requires Transitions.Add. |
| Renewal portal handoff | GET `/renewals/{id}/portal-options` exposes required accounts, recorded email/guardian choices, existing links and `canProvision`. POST `/renewals/{id}/portal-accounts` uses the existing Onboarding account DTO; requires Transitions.Add, Onboarding.Add and Identity.Users.Create, plus Identity.Users.Update for verified existing guardian linkage. |
| Other Records surfaces | Directory, profile, changes, historical overrides, duplicate review, documents/IDs, guardian reviews, and reports use their source-owned controllers in `HttpApi/Controllers/Student/Records/`. Existing certificate/ID permission compatibility is preserved. |
| Certificates | Directory requests with `includeActiveStudentList=true` require Certificates.View and include enrolled Student List students without records onboarding; former students retain records eligibility. `academicMediumStageId` filters the academic stage. Directory options expose `stages` under Medium and `stageId` on Classes. Certificate preview, generation, history and downloads accept active scoped students while retaining identity masking and Leaving exit rules. GET `/api/students/records/certificates/batches` accepts `certificateType`, `generatedFrom`, `generatedTo` (offset-bearing timestamps spanning at most 32 days), `skipCount` and `maxResultCount` (1–100). It returns paged generation groups. GET `/batches/{batchId}?certificateType=...` returns the saved certificate snapshots for visible students. Both require Certificates.View. A multi-student generation uses one stable UUID followed by each student UUID as its idempotency key; older independent requests appear as individual groups. |
| Student I-Card | GET `/api/students/records/ids/templates/list` lists saved designs; POST `/templates` creates one, and GET/PUT/DELETE `/templates/{id}` views, edits, or deletes it. The existing GET/PUT `/templates` remains for older clients and addresses the oldest design. List and read require StudentICard.View, create and update require StudentICard.Add, and delete requires StudentICard.Delete. A student can belong to only one saved template in an academic year; creating or updating a template moves selected students from any other template in that year to the saved template. The same student may use a different template in another year. POST `/preview`, `/issue`, and `/export` accept an optional `templateId` to select a saved design; omitted ID uses the oldest design for compatibility. Preview validates up to 100 student IDs; issue creates numbered snapshot cards with an idempotency key; export returns an A4 bulk PDF for up to 100 IDs and requires StudentICard.Add. Deleting a design leaves issued card snapshots intact. Individual PDF requires View; reprint and void require Add. Directory accepts repeated `boardIds`, `mediumIds`, `standardIds` and `divisionIds` query parameters for the multi-select student step; scope and identity masking still apply. |

Batch requests contain one to 100 distinct students, source academic year, stable idempotency key, per-row outcome/reason/effective date, and preview student/enrollment concurrency stamps. Promote/Repeat/Return additionally require target year/class/division and roll number. Submission validates stamps and school batch behavior again. Reusing a key for a different request is rejected.

Registration evidence uses the frozen form version and final submitted snapshot before finalization, never Inquiry answers or current draft values. Original Aadhaar evidence is masked and raw profile-photo payloads are omitted. Current custom storage excludes mapped Student fields; nonmapped catalogue fields and custom questions retain typed values, including false, zero and repeated entries. Unclassified captured data requires Identity, Contact, Guardian, Medical and Bank policy visibility together, under current and source-year policies. Restricted source values, definitions, snapshots and change payload/history are withheld consistently. Correction DTOs add tenant-resolved `requestedByName` and decision `actorName`; missing former actors remain unknown.

Details also returns additive `formSources` for the complete retained Registration schema with safe canonical current values, `formEditableFieldCodes` and `formMaskedFieldCodes`. Legacy `registrationSources` and custom editable/captured codes remain compatible. Mapped fields use their owning section's current/source-year masks; unknown custom fields retain all five groups. Masked form answers are Restricted and uneditable; their original values are likewise redacted. Protected identity numbers, photo/file capabilities, read-only fields and mapped repeatable fields without matching canonical row storage cannot be changed through this command. Split guardian names retain submitted/saved parts while they match the current full name; a later independent full-name change is shown intact rather than guessed into name parts. The form save preserves unrepresented nullable flags, medical/disability details, sibling IDs, placement and protected identifiers; guardian changes retain the existing access-review handoff. A repeated identical client key returns its existing audit; changed payloads conflict, and stale snapshots or any invalid mixed change roll back the entire save.

Full form sources survive promotion and remain visible independently of the selected year context; historical Registration journey evidence retains its existing year filter. File/photo answers use safe read-only attachment markers, including repeated values, to exclude raw binary payloads from the form and its concurrency snapshot. Field masks also redact submitted/current differences and defaults. Identity Compliance read permission governs identity evidence, including PAN. Changed answers, newly visible questions and questions becoming conditionally required are validated per repeated row. Required MultiSelect/AddressGroup answers cannot be empty arrays or objects. Hiding a question retains its current answer and immutable original/history evidence.

Sources include `optionsByField` for selected dynamic labels and eligible editable choices, and `optionsByFieldPath` for repeated row-specific parent choices. Unavailable or restricted selected items receive inactive safe labels while their saved IDs remain unchanged. Dynamic school choices are constrained to the source academic year and current actor hierarchy; these finite catalogues retain the full existing dropdown list. Search/skip/max pagination applies to sibling students, which excludes masked identities before paging. File and photo-format captured fields remain read-only; student photo changes retain the existing Records photo owner. Custom audit payloads, reasons and approval capability are masked when any recorded submission is outside the current actor's readable source set.

Target-year options expose the Onboarding-owned `rollAssignmentMode` (`manual`, `automatic`, or `both`). A row sends `rollAssignmentMode` (`manual` or `automatic`); a school allowing both requires an explicit choice. Preview returns the selected mode, `proposedRollNumber`, and numbering scope. Automatic submission must send that accepted proposed number. A changed reservation raises `Students:Records:RollProposalChanged`; refresh preview without replacing the idempotency key after an unknown network outcome. Existing no-configuration manual clients remain compatible. Records freezes the accepted number and owner rule; later application validates that number rather than allocating another.

`POST /returns/preview` accepts the return input and returns one transition preview row for `ReuseProfile`; Fresh/Resume use their real Admissions handoff. Reuse supports a reviewed return within the same academic year by reopening the existing inactive enrollment. Completed and cancelled renewal cases remain in history; only one active renewal can exist for a student and year. A requester may cancel AwaitingRenewal only while its linked case is PendingChecklist, before target enrollment or new-year Finance dependencies; Provisioning cancellation is blocked.

Initial Onboarding exposes its school mode on the case DTO. `POST /api/student/onboarding/{id}/roll-preview` requires AllocateIdentifier and accepts `{divisionId?,rollNumber?,rollAssignmentMode?,reason?}`; it returns `{divisionId,schoolAssignmentMode,rollAssignmentMode,proposedRollNumber,concurrencyStamp}`. Optional `divisionId` previews a selected, unsaved division in the case's intended class, subject to tenant and placement access checks; omission retains the saved placement behavior. Preview resolves the selected division's published rule, locks and available numbers without saving placement, reserving a seat or identifier, or advancing the issued-number cursor. An unlocked roll from another division is not reused; a locked roll remains preserved and must be available in the selected division. The stamp is the saved case's concurrency stamp. Existing placement and identifier save commands remain authoritative and revalidate current availability; preview does not reserve its displayed number. The existing identifiers command retains GR inputs and positive `rollNumber`, adds optional `rollAssignmentMode`, and validates the accepted automatic proposal using the shared owner allocator.

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
The optional `columnFilters` query parameter is a JSON array of `{column,type,value,valueType}`
criteria (at most 20, 12,000 characters). `name` accepts string `contains`, `equals`, `startsWith`
and `endsWith`; `startDate`/`endDate` accept date-only `equals`, `after` and `before` using
`DD-MM-YYYY` or `YYYY-MM-DD`. `linkedOfferingCount`/`applicationCount` accept nonnegative integer
`equals`, `greaterThan`, `lessThan` and inclusive `between` with `{min,max}`. Criteria combine
with AND before counting and paging; counts use the same authorized Offering/Application scope
as the displayed usage values. Lifecycle/window filters retain their scalar parameters.
Invalid fields, operators, values or malformed JSON return `Students:Admissions:InvalidCycleFilter`.
Close, reopen, archive, and window revision require a reason of at most 500 characters. Cycle
description is optional and limited to 1000 characters.

## Offering endpoints

Create, clone and bulk writes retain the requested supported admission Track and EntryMode rather than normalizing them to General/Inquiry.
Routing on an Offering already used by applications remains frozen; an unused Offering retains the routing supplied on update.
Application form references are resolved from the selected workflow's configured Form stages; the caller cannot override an explicit workflow form.
Direct-only writes clear Inquiry form and Inquiry intake-profile references, while Inquiry-compatible offerings retain independent Inquiry form selection and workflow-default fallback.
Bulk `stageId` is optional and each item's actual stage is resolved from its Standard before tenant, hierarchy and UserScope validation.
Uniqueness includes admission Track, so General and RTE may coexist for the same cycle/class/academic scope, but two non-deleted offerings of the same identity and Track are rejected even when inactive.
Offering clone always uses the target cycle window (`useCycleDates=true`, null `openDate`/`closeDate` overrides), without copying or changing the source's custom dates.
Generic create/update/bulk date contracts retain validated custom-date compatibility.

Fresh special Registration and family matching accept only RTE, Management, or Staff offerings configured for DirectRegistration or retained Both entry.
RTE configuration still requires DirectRegistration, Form entry, no Inquiry stage and no Assessment stage.
General and Inquiry-first offerings reject these special-entry commands with `Admissions.DirectRegistration.EntryNotAllowed`.
New starts and family lookups require active/open/published-ready, current-tenant, accessible offerings.
Parent-only family matching accepts a saved Father/Mother email or mobile, while legacy mobile-only family linking remains compatible without gaining trusted sibling-account provenance.
Ordinary application start still requires Inquiry evidence; the server-internal Records return coordinator and existing applications retain their governed lifecycle.

| Method | Application route suffix | Permission | Contract |
| --- | --- | --- | --- |
| POST | `direct-registration` | `Students.Admissions.Applications.Add` | System-owned applicant/guardian identity, Offering ID, stable `requestId`, optional counsellor and explicit existing/separate family choice; creates a Registration-first application transactionally. |
| GET | `direct-registration/family-matches?offeringId=...&guardianMobile=...&guardianEmail=...` | `Students.Admissions.Applications.Add` | Matching scoped family identities, visible child names and saved parent candidates for an available special Registration offering. Mobile may be empty when email is supplied. |

These existing routes are under `/api/student/enrollment/admissions/applications` and enforce the same Start permission on both controller and application service.
The same-request/same-payload retry returns the existing scoped application without creating a second family/applicant; changed identity, offering, or initial counsellor conflicts.

### Parent-only sibling selection

Inquiry creation and Direct Registration accept additive `parentRelationship` (`father` or `mother`) and `existingParentApplicantId` alongside `existingFamilyId`.
An existing family in this new flow requires the source applicant and explicit parent relationship; a family ID or matching email alone never grants portal access.
Family match responses add `parents: [{sourceApplicantId,relationship,name,firstName,lastName,email,mobile}]`, `matchByEmail` and `matchByMobile`.
A legacy generic Parent contact has nullable `relationship`; staff must explicitly identify Father or Mother rather than the server guessing.
Legacy mobile-only responses retain the saved primary contact display name, but Guardian/Relative contacts are not promoted into parent candidates.
Inquiry `duplicate-check` additionally accepts up to two `parentContacts: [{relationship,email,mobile}]` for authored Father/Mother fields and returns the same parent candidates on each family match.
Cross-cycle suggestions require a matching saved parent contact and existing admission scope, not a name-only signal.
Selection rechecks the active family, source applicant visibility, current saved contact and tenant before recording server-owned confirmation on the new applicant.
The selected saved parent profile wins over typed child-form values; shared family contact rows are not overwritten.
Public Inquiry rejects trusted existing-parent selection.
Existing duplicate previews, cycle rules, request replay and finalization identity checks remain authoritative; name/date-of-birth similarity alone introduces no new hard identity block.
Direct Registration retries compare the recorded submitted email/mobile and selected relationship/source to reject changed payloads.
Pending authored parent profiles use a parameterized provider-backed JSON contact predicate intersected with current admission scope before projecting family IDs.
Current Student Father/Mother contacts and ordinary saved primary contacts also use targeted queries; there is no arbitrary pending-applicant ceiling, unrelated JSON-profile materialization, Student aggregate graph or per-child query.
These additive fields use existing audited extra-property storage and require no migration or new permission.

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

Offering list accepts optional `forSpecialRegistration=true`, default false. It filters RTE/Direct and Management-or-Staff/Direct-or-Both before counting, sorting and paging; cycle, type, academic scope, tenant, active/configuration/availability and search filters still apply. This prevents an inquiry-only first page from hiding eligible later rows. Availability filtering and returned status use the institution calendar date (`Asia/Kolkata`), including UTC/IST date boundaries. No permissions or stored routing change.
The read model projects academic identity/name/parent columns as scalars. It does not materialize Board/Medium/Stage/Standard entities and their recursive academic AutoIncludes; returned identities, readiness checks and sort fields are preserved.
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
The body includes `expectedCurrentStageInstanceId`, a unique `idempotencyKey`, required `concurrencyStamp`, and optional transition, skip, override, reason and `deferFeeCollection` fields.
`deferFeeCollection: true` is accepted only in the active Payment stage when its pinned completion policy permits deferral, with a nonblank reason of at most 512 characters. It cannot be combined with skip or override. Deferral uses configured routing, records actor/time/reason, and creates no Finance account, assignment, dues or receipt. Regular continuation enforces the pinned partial/full rule against Finance and requires a matching saved fee selection; the historical payment flag alone is insufficient.
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

The form list also accepts optional `columnFilters`: a JSON array of `{column,type,value,valueType}`
criteria, limited to 20 entries and 12,000 characters. `name` supports case-insensitive string
`contains`, `equals`, `startsWith` and `endsWith` (at most 150 characters). `version` and
`sectionCount` support nonnegative 32-bit integer `equals`, `greaterThan`, `lessThan` and inclusive
`between` with `{min,max}`. Criteria intersect the existing search, status, purpose and version
parameters before counting and paging. Section criteria and displayed section counts use the
same repository scope. Unsupported or malformed criteria return `Students:Admissions:InvalidFormFilter`.

## Admissions list column predicates

List column-filter additions preserve the existing authorization, tenant, academic and lifecycle
scope. Optional `columnFilters` is a bounded JSON array of `{column,type,value,valueType}`
criteria (at most 20 entries / 12,000 characters); supported criteria combine with AND before
result counts and pagination. Existing scalar scope/status inputs continue to intersect them.

| List | Supported column fields |
|---|---|
| Pipeline | `applicantName`, `applicationNumber`, `inquiryNumber`, `standardName`, `contactPhone`, `contactEmail` text predicates |
| Applications | `standardName` text; nullable `decisionEnteredAt` and `lastSavedAt` local-day predicates |
| Admission Stage/workflows | `name` text; `version`/`offeringCount` integer predicates; `lastModificationTime`/nullable `publishedAt` local-day predicates |
| Admission Offering | Displayed name/code fallback `name`; effective calendar `effectiveOpenDate`/`effectiveCloseDate`; `applicationCount` integer predicates; academic/configuration/availability retain their existing scalar parameters |
| Assessment applicants | `applicationNumber`, `className`, displayed `sessionName` text; displayed-attempt `attendance` and `result` enums; progress retains the existing `status` parameter |

Text operators are `contains`, `equals`, `startsWith` and `endsWith`; numeric operators are
`equals`, `greaterThan`, `lessThan` and inclusive `between` with `{min,max}`. Date operators
are `equals`, `before` and `after`. Application/Workflow timestamp criteria include
`startOffsetMinutes` and `endOffsetMinutes` using JavaScript timezone-offset sign, separately
validated at both selected local midnights for DST. They compare against UTC day boundaries;
missing timestamps remain excluded. Offering calendar dates retain date-only semantics.

Inquiry Class additionally accepts `standardName` and `standardNameFilterType` text predicates
through scoped Offerings/Standards. Workflow sorting adds `statusLabel` for displayed-label order
while preserving the legacy numeric `status` sort. Other supported sorts retain stable ID
tie-breakers. Public Inquiry Links remains a full-list endpoint with client filtering; this adds
no paging, mutation, permission or workflow change to that endpoint.

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
| `PUT /attempts/{id}/result-draft` | `RecordResults` | Save partial subject scores on a pending attempt without completing it |
| `POST /attempts/{id}/review` | `Review` | Approve evidence or return it for correction |
| `POST /attempts/{id}/retake` | `Book` | Permit another attempt within the configured limit |
| `POST /attempts/{id}/cancel` | `Book` | Cancel an unstarted attempt and release its booking |
| `POST /applications/{applicationId}/stages/{stageInstanceId}/waive` | `Waive` | Record an authorized waiver with a reason |

Workflow stage metadata advertises `assessment` in `SupportedConfiguration` and runtime stage DTOs expose `AssessmentEnabled`.
Scope choices also include optional `offeringCode` and `workflowName` for readable disambiguation of offerings and pinned workflow versions; these labels reuse already-loaded records and do not affect identity or authorization.
The configuration shape is `{ "assessment": { "version": 1, "enabled": true, "schedulingEnabled": false, "attendanceRequired": false, "scoringEnabled": true, "totalMarks": 100, "passingMarks": 40, "reviewRequired": false, "maxAttempts": 1 } }`.
Version 1 supports 1 to 10 attempts and positive total marks up to 10000, with passing marks between zero and the total.
Attendance can also be required in result-only mode.

Version 2 adds `scoringMode: "total" | "subjects"` and `classProfiles`.
Each profile contains `id`, `name`, `boardId`, `mediumId`, `standardIds`, `passRule` (`overall`, `everySubject`, `both`), `passingMarks` and `subjects`.
Each subject contains a stable `code`, nullable Academic `subjectId`, frozen `name`, `totalMarks` and nullable `passingMarks`; per-subject rules require every subject threshold.
Assessment configuration accepts up to 64,000 characters; other stage configuration retains its existing 8,000-character bound.
`GET /api/student/enrollment/admissions/workflows/assessment-catalog` requires Workflow View and returns tenant/user-scoped `classes` with Board, Medium and year context, and active Medium `subjects` with `assignedStandardIds` suggestions.
No Academic module permission or published class assignment is required to select an active Medium subject.
An Offering without one matching class profile fails readiness validation.

Subject result draft and final requests use `{ concurrencyStamp, notes?, subjectScores: [{ code, marks: number | null }] }`.
Drafts may omit subjects or leave null marks; final writes require all configured scores within their frozen maxima.
The server derives total and passed/failed; caller totals or outcomes cannot override subject rules.
The assessment and attempt DTOs expose `effectiveProfile`; attempts include `subjectScores`, and the summary includes `canSaveResultDraft` and `canSubmitResult`.
These flags retain the existing session, attendance, action and scope checks.

Session input requires offering/stage identifiers, name, ISO date-times, location, and capacity from 1 to 10000.
Session update/cancellation requires the session concurrency stamp.
Attempt creation and stage waiver require the application stamp returned in the assessment summary.
Other attempt mutations require that attempt's concurrency stamp.
Review uses `approved: false` plus notes to request correction, and waiver uses `reason`.
Attempt mutations return the refreshed assessment summary.

The applicant workbench accepts optional `admissionOfferingId` and `workflowStageDefinitionId`; other filters are `sessionId`, `search` (name/application number), and `queue` (`toBook`, `attendance`, `result`, `ready`, `attention`, `completed`). It accepts `skipCount`, `maxResultCount` (1-1000), and allow-listed `sorting` (`applicantName`, `applicationNumber`, `sessionStartsAt`, `enteredAt`, with optional `asc`/`desc`). The result contains `totalCount`, paged `items`, and `counts`; counts honor offering, workflow version, session, academic authorization and search before the queue/page filter. Each row contains application/stage identity and the same assessment summary as the individual read. Active stages without attempts are included; completed stage history remains readable. For active stages, a selected session roster requires the latest non-cancelled attempt to belong to that session, so a retake booked elsewhere is excluded from the old session's actionable roster. Inactive/completed stages retain historical non-cancelled bookings to the selected session; their actions are disabled.

The additive `status` filter accepts `pending`, `ongoing` or `complete`. Responses include `statusCounts` (all three statuses plus `all`), each item's `status` and actual `className`; scopes also expose `className`. Counts use the authorized academic/session/search context before status filtering and pagination. Unbooked/retake or future appointments are Pending; started and ended-but-unrecorded assessments are Ongoing. Complete requires the appointment end and a saved result (including zero marks); scored passed/failed results require numeric marks. Qualitative results and absence need no fabricated marks, and waiver completes immediately. Individual interview slot times override the session window. Status is independent of legacy `queue` routing readiness and does not advance a stage; existing Continue permissions, evidence and route gates still apply. No schema migration is required.


`ready` means completed evidence has an eligible route under the existing workflow rules, independently of the caller's advance permission. Failed/absent results without an explicit matching route remain `attention`. Independent result review is disabled in new saved Assessment policies; the frontend does not expose a Review queue or result-review action. Result-only attempts use `toBook` for the initial start action. Classification batches scoped candidates, latest non-cancelled attempts, grouped attempt counts, sessions and route facts without per-application queries. Complete history and action DTOs are expanded only for the requested page. Configured JSON routes still require candidate classification before queue pagination, so memory/CPU scale with the authorized Offering/stage/search population. This is a workbench query, not a tenant-wide reporting export; concurrent production load and very large scopes require separate staging acceptance.

### Shared school sessions and confirmed booking

The combined applicant query now permits omitted Offering/stage filters. It also accepts `academicYearId`, `boardId`, `mediumId`, `standardId`, and `standardIds` (CSV GUIDs, maximum 20, intersected with the other academic filters). Rows include Offering/stage identity, Offering name, workflow version and individual appointment times while preserving pinned policies and scoped history.

New session input adds `scopes: [{ admissionOfferingId, workflowStageDefinitionId }]` (1-20 distinct pairs), `sessionType` (`exam`, `individualInterview`, `groupInterview`), Institute `roomId`, Employee `interviewerId` for interviews, and individual `slotDurationMinutes` (5-180) / `breakMinutes` (0-60). Online assessments remain outside the contract. Exam/group capacity cannot exceed usable room seats; individual capacity counts sequential derived appointments. Legacy single-scope sessions remain readable/bookable through the existing API; editing one with an Institute room adopts its original scope and preserves booked attempts/history.

| Endpoint suffix under the Assessments API | Permission and contract |
| --- | --- |
| `GET session-resources` | ManageSessions; tenant room/building/floor/capacity plus initial 50 active scoped Employee choices |
| `GET interviewers?search=...` | ManageSessions; employee-first searchable staff choices, maximum 50 |
| `GET sessions/workbench` / `GET sessions/{id}` | View; combined server-paged/sorted sessions (page maximum 200), or direct scoped lookup |
| `GET sessions/{id}/slots` | Book; free individual appointment times without applicant-private data |
| `POST sessions/preview` | ManageSessions; `{ session, sessionId?, sessionConcurrencyStamp?, skipCount, maxResultCount, search? }`; paged candidates plus independent proposed bookings (up to 200), availability and fingerprint |
| `POST sessions/confirm` | ManageSessions plus Book when candidates are selected; `{ session, sessionId?, sessionConcurrencyStamp?, idempotencyKey, previewFingerprint, bookings }`; atomic creation/reservation/booking or additional booking of an existing session |

Confirmation rechecks eligibility, application versions, room/calendar/interviewer conflicts, shared seats and exact free interview slots. Durable receipts bind retries to actor and payload; later arrivals are never implicitly booked. Preview excludes conflicting appointments and proposes one application per pupil. Reservation dates/times use the existing India school-day timezone; cross-school-day sessions are rejected. Pending room approval holds bookings but blocks attendance/results. Direct generic room edits/cancellation of a live assessment reservation are rejected with guidance to Assessments; rescheduling replaces the reservation through the existing approval path. Session cancellation with `cancelPendingBookings: true` requires ManageSessions and Book and atomically cancels only unstarted pending attempts before releasing the room. Booked individual interviews cannot silently change slot layout. Partial-scope readers receive only authorized links and `canManage: false`; whole-session management requires every linked Offering. Missing attendance remains pending.

Available condition facts are `assessment.outcome`, `assessment.marks`, `assessment.attemptNumber`, `assessment.attendance`, and `assessment.reviewed`.
Current configured results become authoritative without a separate result-review step.
Negative outcome routes must contain a matching `Equals` or array-valued `In` condition on `assessment.outcome` and satisfy the complete route expression.
Unknown policy versions, stale stamps, unavailable capacity, missing evidence, and missing negative-outcome routes are rejected using `Admissions:Assessment:*` error codes.

## Inquiry management APIs

The existing inquiry capture-context response includes nullable `academicYearStartDate` (a calendar date) and `minimumAgeYears`. After the offering scope check, these are read from its cycle's Academic Year and its Standard's existing `AgeLowerLimit`; non-positive minima return null. Clients use them for advisory DOB age feedback in inquiry create/edit. This does not add inquiry validation, change workflow eligibility policies, require Institute Setup permissions, or introduce persisted settings/migrations.

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
| `POST` | `duplicate-check` | `Students.Admissions.Inquiries.Manage` | Find current-cycle duplicate signals and saved-parent email/mobile family matches visible in staff scope across admission cycles before creation |

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
Legacy `existingFamilyId` without parent-flow fields still requires an active, admission-scope-visible family and a matching primary guardian mobile.
The explicit Father/Mother sibling flow instead uses the server-resolved saved parent candidate described above; same email with a changed mobile requires staff confirmation.
Duplicate-check marks eligible results with `canLinkFamily`, but creation rechecks the family and selected source rather than trusting a preview flag.
Name-only duplicate signals remain within the requested cycle.

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
| POST same requirement route `/waive` | `Students.Admissions.Documents.Add` | Retained endpoint; unavailable because current stage policies disable waivers |
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
| GET base | Return effective pinned policy, form status, deadline, attempts, historical verification state, blockers, concurrency and capability flags; retired verification requirements are disabled |
| POST `sections/{sectionId}/verification` | Legacy compatibility route; effective policy disables new section verification |
| POST `correction` | Require a reason, append audit history, reopen the form, and invalidate earlier section verification |

Commands carry the application `concurrencyStamp`; route identifiers are excluded from bodies. Managed submit also enforces the deadline and maximum attempts through the existing application-form endpoint. Legacy manual-continuation settings may hold after submit for explicit continuation; retired section-verification flags do not block completion. Stored configuration and verification history remain unchanged. Missing or disabled `configurationJson.form` retains legacy Form behavior. Offering configuration rejects a Form-stage definition that differs from the Offering application form.

## Managed Payment stage APIs

### Application fee selection

Base: `/api/student/enrollment/admissions/fee-selections/applications/{applicationId}`.
The service returns server-controlled ownership, requiredness, locking and confirmation capabilities alongside an optional saved Finance-reference snapshot. `hasRecordedFeePayment` reports historical evidence, not progression readiness. `paymentCompletion` supplies `paymentRule`, `allowCollectionLater`, `canComplete`, `canDeferCollection`, `status` and `blocker`. Workflow configuration stores version 1 `paymentCompletion: { paymentRule: "partial" | "full", allowCollectionLater: boolean }`; absent settings default to partial/true. Unsupported versions or invalid values reject publication/advancement. `feeCollectionDeferral` exposes stage ID, reason, actor and UTC timestamp for Admissions and linked Onboarding.

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

Payment summary reads remain at `/api/student/enrollment/admissions/payments/applications/{applicationId}/stages/{stageId}` and include the same completion decision, with `feeStructureRequired`, `collectionPending`, `partiallyPaid`, `pendingVerification`, `financeCleared` or historical `collectionDeferred` status. A confirmed structure alone never means Paid. Partial evidence reads require existing Finance collection View permission; full/zero clearance consumes the existing scoped internal settlement decision. Legacy admission record/review/waive/reverse capabilities remain false; use the Finance collection adapter for actual collection. Existing payment permission identifiers remain intact.

Confirmation saves through the existing fee-selection PUT with structureId, optionalHeadIds and current application concurrencyStamp. Workflow advancement remains a separate permissioned/idempotent command. Actual collection uses the Finance-owned application adapter at `/api/fee-collections/admission-applications/{applicationId}`: read `summary`, explicitly `prepare` canonical dues, read `collect-form`, then POST a payment with its stable retry key. Collection View/Add and academic scope remain mandatory even when the Finance menu is hidden. Deferring collection invokes none of those monetary commands. See [Finance collection ownership and handoff](../finance/references/admission-fee-collection.md).

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
| GET `/sections/{section}?academicYearId={guid}` | Dashboard.View plus context section eligibility, destination action/visibility, tenant-owned year and current academic scope. `section` is records/admissions/attendance. Optional `boardId`, `mediumId`, `standardId`, `divisionId` constrain the Admin projection. Existing metrics/classStrength remain compatible. |
| GET `/filters?academicYearId={guid}` | Dashboard.View, database-resolved Admin mode, a permitted section and authorized tenant year. Returns scoped `{boards,mediums,standards,divisions}` option arrays with `{id,name,parentId}` from the existing academic hierarchy; Directory View is not required. Portal-only identities cannot read these staff options. |
| GET `/attendance-registers?academicYearId={guid}` | Dashboard.View, Admin mode and permitted Attendance View/visibility. Accepts the same academic filters plus `skipCount` and `maxResultCount` (1–100). Returns `{totalCount,items}` for the distinct missing/draft/submitted-needing-review registers. Items carry `{standardId,divisionId,className,divisionName,date,status,href}`; `status` is `missing`, `draft` or `review`. |

Admin section additions are `workGroups`, `onboardingTasks`, `attentionItems`, `attentionTotalCount`, `classStrengthTotalCount` and nullable `attendanceToday`.
Work/task rows reuse the metric contract `{id,label,value,tone,href,permission}`.
The setup preview is bounded to four cases and exposes only case/student identifiers, student/class/division names, the owning primary blocker, owner name, due time and owning-workspace link.
Class strength is bounded to 12 classes with complete division children; the total class count makes truncation explicit.
Unfilterable unassigned hierarchy rows have an empty link.
Attendance returns school date/time zone, `availability` (`available`, `outsideYear`, `noExpected`), `provisional`, expected/missing/draft/submitted/review register counts and a pending-register destination.
Review is a subset of submitted; outside-year counts are null, and zero eligible students or holidays do not create pending registers.
Other role modes retain their existing section projections.

Owning list additions keep dashboard counts and filtered worklists aligned before pagination:

| Owning list | Additive read input |
|---|---|
| Applications | `activeOnly`, `pendingDecisionOnly`, `divisionId`; pending decisions require an active workflow/current Approval stage and an undecided supported application state. |
| Onboarding | `dashboardQueue`: `unfinished`, `attention`, `ready`, `division`, `gr-number`, `roll-number`, `documents`, `credentials`; task queues reuse recorded requirements and the existing pending-task predicates. |
| Records changes | `approvableOnly` plus Board/Medium/Class/Division scalars; the shared predicate retains maker-checker, approval-stage role, prior-actor and current/snapshot masking checks. |
| Inquiries | `dashboardFollowUpsDue` and `divisionId`; due means at least one scheduled reminder at or before the current UTC time, with existing year/offering scope. |
| Student leave | `boardId`, intersecting the existing year, Medium, Class, Division and leave-period filters. |

Records approval groups require Records Add, and Admission decision groups require Applications Add in addition to the destination read/visibility grants.
Frontend route presets and cache invalidation are owned by the [Student frontend](../../../../Shikshamitra-FE/docs/student/clients.md).

These live GET reads and frontend refresh repeat reads without readiness adoption, writes or jobs.
Legacy Reporting-backed preview refresh retains its separate Dashboard.Add command authority.
Unknown section raises `Students:Dashboard:InvalidSection` (400); denied identity/section/year raises authorization failure (403).
Anonymous callers receive 401; ABP rejects an unknown tenant before this controller, normally with 404.
The bounded authorized setup preview contains names; staff aggregates do not return contacts, compliance identifiers, credential content or full student records.

## Division roll-number batches

The authenticated controller root is `/api/students/roll-number-batches`.

| Method and suffix | Contract |
|---|---|
| GET `context` | Academic year, Class and Division IDs; returns capabilities, unassigned count and latest 20 non-preview batch summaries. |
| POST `preview` | Scope plus operation `assign-missing` or `renumber`; optional `caseIds` bounds `assign-missing` to 1–100 distinct, current-tenant onboarding cases in that scope. Omission retains full-division behavior. The server still owns ordering and proposed numbers and returns the first 25 proposal rows. |
| GET `{id}` | Scoped immutable proposal; `skipCount` and `maxResultCount` page display only, maximum 100 rows. |
| POST `{id}/submit` | Current stamp, stable idempotency key and reason; previews expire after 15 minutes. Direct-policy batches apply atomically; governed batches await approval. |
| POST `{id}/decision` | Current stamp, approve/reject and review note; requires Records Changes Add authority and the current configured stage role. |
| POST `{id}/apply` | Approved batch, current stamp and original submission key; revalidates roster, rules, policy, locks and approvers before atomic application. |

Reads require an active database identity, tenant and academic scope, plus Onboarding View or Records Student List View as applicable to every included row.
Writes additionally require Onboarding Add for pending cases and Records Changes Add for active enrollments; renumbering uses the same authorized student/division scope.
Active-enrollment batch history also requires Records Changes View.
Records eligibility, provisioning policy, required placement reasons and Identity masking remain enforced.
Conflicting or expired proposals use `Students:RollNumbers:StalePreview`; blocked rows, invalid states/rules and missing action/approver authority have localized errors in `StudentRollNumberErrorCodes`.
Committed replay returns the saved batch without a second assignment.
Case-targeted batches persist their target IDs and revalidate them on submit, approval and apply; assigned students are excluded rather than renumbered.
The full division remains the reservation and concurrency basis, including students not on the selected page.
Preview and batch responses expose `caseIds`, `reasonRequired`, `approvalRequired`, approval-stage role names and the policy version ID so clients show the applicable frozen policy before submission.
Identifier Unlock additionally requires database-resolved configured administrator membership and the existing Onboarding Add/Repair authority; Lock retains existing action and division-scope checks.
`GET /filter-options` returns `canUnlockIdentifiers` only when both administrator and action requirements pass.
An unassigned Roll remains null in storage; a client may display it as zero, but assigned numbers must remain positive.

## Onboarding app-login email edits

| Endpoint | Contract |
|---|---|
| PUT `/api/student/onboarding/{id}/credentials/email` | Onboarding Add/Update, case scope and current case stamp; `accountType`, saved `relationship` (`self`, `father`, `mother`), `expectedEmail`, new `email`, server `emailEditToken` and `confirmSharedParent`. Unlinked profile edits save only the email; manual credentials remain a separate command. |
| POST `/api/student/portal/email/confirm` | Owner-delivered protected token; requires matching `__tenant` header/body `tenantId`, `userId`, `requestId` and `token`. Opening the email link does not perform this write. Request/token auditing is disabled. |

Existing-account edits additionally require `AbpIdentity.Users.Update`, all linked students in scope, no pending guardian access review or processing credential delivery, exact account role/profile/link integrity, duplicate checks and explicit shared-parent confirmation.
The response exposes recipient `canEditEmail`, `emailEditToken`, `hasLinkedAccount`, `linkedStudentCount`, `pendingEmail` and `emailChangeStatus` without passwords or verification tokens.
The existing identity retains its login email until owner verification; verification updates the email-as-username and linked profiles atomically, preserving password hashes and child links.
Existing sessions are revoked through the same token/cache pattern as controlled account-email changes; the owner signs in again with the new email and existing password.
Requests expire after 24 hours; manual replacement/resend has a two-minute cooldown and supersedes the old verification request.
The same scoped PUT accepts `cancelPendingChange: true` with the reviewed current email, case stamp and edit token to cancel pending verification. It does not require a new email or shared-change confirmation, changes no login/profile/password/link, and invalidates the previous owner-confirmation request.
Current case/profile/account/link stamps reject stale edits; tenant, staff authority and scope are rechecked before queued verification delivery and owner confirmation.
The existing persistent ABP background-job store delivers verification emails; it must be enabled operationally for those queued requests to send.
No new schema, Finance command, automatic credential preference or onboarding completion policy is introduced.

## Archived workflow restoration

| Endpoint | Contract |
|---|---|
| POST `/api/student/enrollment/admissions/workflows/{id}/reopen?concurrencyStamp=...` | Workflow Publish (existing `Students.Admissions.Workflows.Add` alias), authenticated and tenant-filtered; a current nonempty stamp is required. Restores the same archived ID/version as Published or Superseded and returns the refreshed workflow DTO. |

Re-open records governance action `Reopened = 10` without changing stages, routes, publication metadata or Offering activation.
A stale/missing stamp uses `Admissions.WorkflowConcurrencyConflict`; invalid lifecycle transitions use the existing governance-transition error.
See [product.md](./product.md#workflow-builder-publication) for historical-status restoration and family serialization.
