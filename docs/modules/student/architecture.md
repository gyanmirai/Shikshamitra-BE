---
module: student
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-10-13
owner: backend-team
audience: humans-and-agents
---

# Locate Student Enrollment Admissions code

## Attendance workspace reads and authority

`Application/Students/Records/StudentDirectoryQueryService.cs` owns shared scoped Directory inclusion, hierarchy, paging and identity masking; Records keeps its public permissions while Attendance calls this internal read service under Attendance View. `Application/Students/Attendance/AttendanceWorkspaceQueryService.cs` builds context and paged daily rows, enriches only classes present on the page, and batches historical observation fallback for disabled rows. Existing dated roster, report calculations and command APIs retain attendance ownership.

`AttendanceWorkspaceAccessService.cs` checks current database identity/admin roles and exact active ClassTeacher → active Employee.UserId mapping; manual academic scope does not create teaching authority. Mark/Submit checks this authority inside its Serializable command unit of work, alongside current roster/scope checks. No new database entities, grants, seed repair or migration are introduced.

TL;DR: Admissions remains inside the Student modular-monolith boundary and separates aggregate invariants, authoritative validation, reads, commands, and runtime availability.

## Staff scope gateways

`StudentScopeAccessService` owns strict Student placement, division/attendance tuple checks, bulk existence checks, ancestor navigation and settings authority. `StudentRecordScope` matches exact division or explicit ancestor wildcard grants; the generic Academic ancestor-visibility matcher is not a Student record authorization check.

`AdmissionApplicationScopeAccessService` and `AdmissionOfferingScopeQuery` authorize class-level offerings. Offering reads and source commands use that same query. `AdmissionInquiryScopeAccessService` handles offering-linked and unallocated inquiries. `AdmissionApplicantScopeAccessService` guards applicant identity, shared profile changes and activity references; application services and controllers retain exact action permissions.

## Onboarding email and access ownership

| Concern | Source |
|---|---|
| Manual one-click credentials | `StudentOnboardingAppService.Credentials.cs` owns the scoped workspace and transactional send; `StudentCredentialsRecipientReader.cs` reads legal current owners and Student email provenance; `StudentCredentialsDtos.cs` supplies bounded DTO validation. The existing portal preparation core and credential outbox remain owners of linking and transport. |
| Automatic admission requirements | `Application/Students/StudentEnrollments/Onboarding/OnboardingRequirementSnapshotService.cs`, `Domain/Students/StudentEnrollments/Onboarding/Services/AutomaticOnboardingReadinessRules.cs`; finalization determines document requirements before Student profile writes and reuses the prepared JSON at case creation. Pending case extra properties capture common readiness, current account choices and published workflow document policy automatically. Actual snapshot changes receive an adoption audit event; unchanged activation previews do not rewrite step evidence. Activated and terminal snapshots remain fixed. Required document overrides are rejected. |
| Canonical documents | `Application/Students/StudentEnrollments/Onboarding/OnboardingWorkflowDocumentsAppService.cs` delegates finalized-application upload/original/review to existing Admissions owners and permissions. |
| Fee intent | `StudentOnboardingAppService.Fees.cs` owns selection and confirmation of an existing published fee structure. Fee assignment, dues, collection and refunds remain owned by the existing Finance services. |
| Identifier group policy | `StudentOnboardingAppService.IdentifierLocks.cs`, `StudentOnboardingAppService.LockedGroupAssignments.cs`; Division extra properties persist independent year-specific GR/Roll policies, including empty groups. Student/domain and Records correction paths enforce locks. |
| Readiness and automatic orchestration | `Application/Students/StudentEnrollments/Onboarding/StudentOnboardingCredentialEligibilityResolver.cs`, `StudentOnboardingAppService.CredentialEmails.cs`; uses current Enrollment, Student, Admissions document evidence and existing completion truth. |
| Durable email | `Domain/Students/Portal/Entities/StudentCredentialEmailRequest.cs`, `Application/Students/StudentEnrollments/Onboarding/StudentCredentialEmailAppService.cs` and `StudentCredentialEmailDispatcher.cs`; durable preparation commits before SMTP, frozen link/mailbox and optimistic claim prevent duplicate intent. |
| Persistence/worker | `EntityFrameworkCore/Students/Portal/StudentCredentialEmailRequestConfiguration.cs`, `HttpApi.Host/BackgroundWorkers/StudentCredentialEmailWorker.cs`; worker changes to each intent's tenant and delegates checks. |
| Lifecycle and login | `Application/Students/Portal/StudentPortalLifecycleService.cs`, `StudentPortalLoginService.cs`, `StudentPortalAccessService.cs`, `HttpApi.Host/Controllers/StudentPortalTokenController.cs`; Records terminal application and correction own link revocation/restoration. |

The source paths above use the corresponding backend layer's source folder.
HR employee email and the queue-only generic communications gateway are unchanged.

Offering activation commits the enrollment in a serializable transaction before account preparation. A separate preparation transaction may fail without rolling back enrollment; a fresh repair transaction records access pending for retry. Student Directory, Records access and dashboard eligibility recognize the persisted activated enrollment. Historical renewal Finance rules are separate.

## Student Attendance ownership

| Layer | Owner |
|---|---|
| Shared rules | `src/ShikshaMitra.Domain.Shared/Students/Attendance/`: typed policy, centralized operation errors and pure credit/completeness calculation. |
| Domain | `src/ShikshaMitra.Domain/Students/Attendance/`: day/entry, correction and durable operation journal. New applicability consumes existing Academic Calendar and fixed attendance defaults. Legacy policy and schedule entities remain mapped only to preserve stored data. |
| Enrollment | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Aggregates/StudentEnrollment.Attendance.cs`: confirmed start and dated membership snapshots. Existing Records placement/transition/renewal commands supply their effective dates. |
| Application | `src/ShikshaMitra.Application/Students/Attendance/`: focused roster/register/report reads, daily and correction commands, enrollment setup and leave-type management. Thin entry services retain action permissions and academic-scope checks. |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Students/Attendance/`: policy/version/schedule, correction and operation mappings plus enrollment/date uniqueness. Migration `20261001043143_AddUniversalStudentAttendance` preserves legacy identity and leaves unknown membership dates unknown. |
| Integration | `StudentListAppService` and `DashboardStudentAttendanceSliceProvider` use canonical credit and provisional metrics. Student Leave approval remains explicit and never writes fabricated daily presence. |

Daily/correction/leave-approval transactions use serializable units of work and map database concurrency conflicts to a reviewable business response.
Daily entry persistence reads existing rows once per day and batches inserts/updates.
Submitted snapshots pin historical policy and eligibility. Removed policy and schedule configuration no longer affects new attendance.
Tenant filters remain enabled; historical paths are independently scoped rather than authorized through a student's current placement.

## Student Records ownership

| Layer | Source and responsibility |
|---|---|
| Shared policy | `Domain.Shared/Students/StudentRecords/StudentRecordsPolicy.cs`: typed action/stage/condition, provisioning, masks, guardian access, document, and transition choices. Central permission partials remain the only permission catalogue. |
| Published configuration | `Domain/Students/StudentRecords/`: draft configuration, immutable policy versions, structural validator, and effective resolver. Year-published policy precedes school-published policy; otherwise runtime uses the documented starter. |
| API contract | `Application.Contracts/Students/Records/`: requests, paged results, state/capability DTOs; settings contracts remain under `Students/StudentRecords/`. |
| Profile evidence and current custom data | `StudentRecordRegistrationReader` batches frozen Registration definitions/submissions for scoped activated journeys; `StudentRecordDetailsAppService` composes existing canonical section reads and visibility. `StudentRecordCustomDetailsService` resolves real form ancestry plus field code/type, validates definitions/rules server-side and joins the existing change pipeline. Returning cases join their committed enrollment by StudentEnrollmentId, preserving the enrollment's original provenance. |
| Inline retained Registration form | `StudentRecordFormProjection` projects the full frozen schema with owning masks and field capabilities; `StudentRecordFormMapping` patches changed canonical bindings while preserving fields outside the form. `StudentRecordFormAppService` joins typed custom validation and canonical application into one serializable Applied `StudentRecordChange` audit. No new entity or migration; generic approval commands are unchanged. |
| Access and governance | `Application/Students/Records/StudentRecordsActorService`, access service, enrollment scope, write guard, change application, and placement services preserve existing write APIs while governing enrolled/former records. Records checks the current tenant's active database identity even when a previously issued login remains valid; settings uses the same guard. |
| Transitions | Focused Readiness, Options, Authority, Application, Correction, RenewalReadiness, RenewalCoordinator, ReturnCoordinator, AdmissionReturnService, and thin partial TransitionAppService separate source-owner evidence, authorization, lifecycle, and mapping. Internal coordinators disable remote exposure. |
| History | Transition/batch/event/renewal/return-handoff entities retain policy and source snapshots. Target year/class/division projections are parsed once from immutable input for database scope filtering and paging. |
| Owner boundary | Initial enrollment stays in Onboarding. Academic/Library/Transport source evidence is read only. `StudentTransitionFinanceClearanceReader` delegates to Finance's canonical collection ledger; renewal delegates to Onboarding's Finance readiness reader. `StudentRenewalPortalCoordinator` owns the account handoff for real annual renewals without fabricating an initial case. Returns call real Admissions commands. |
| Persistence | `EntityFrameworkCore/Students/Records/` defines tenant-scoped unique request keys, policy version numbering, one active student/year renewal, correction uniqueness, indexes, restrictive relationships, and immutable snapshot payloads. Completed and cancelled renewal reviews remain separate history rows. |
| Custom current values | Tenant/student-owned `StudentRecordCustomProfile` retains baseline submission, captured schema/source references and current typed JSON, with ABP audit/concurrency. `20261012120000_AddStudentRecordCustomProfiles` adds storage only; existing students derive a read-only baseline until an audited write. Rollback rejects dropping persisted corrections. |

Transition application uses a serializable unit of work for each student and revalidates live approvals. The background scheduler invokes `StudentRecordTransitionApplication.ApplyAsRecordedActorAsync(id)` within the tenant; it processes Approved/Scheduled cases, while Exceptions require manual retry. A school may choose immediate roster removal on approved exits, even when the historical effective date is later.

Scheduled change selection and change application share ABP `IClock`; the transition worker also uses that clock and the institution calendar. Workers poll every minute. Report generation checks the current Add permission, captured source authority and expiry again after workbook generation, before blob publication. Download requires current Reports.View and source View, so removing Add alone does not invalidate an already completed report.

Both change and transition application classify raw or wrapped database SQLSTATE `40001` through the existing provider-neutral Onboarding conflict mapper. After rollback they persist `Students:Records:ConcurrencyConflict` in Change Conflict or Transition Exception. These states leave the automatic worker queue; transition recovery is an explicit staff retry, while a change uses a reviewed replacement request. Unrelated exceptions still propagate.

Transition exception recording reloads the row after rollback and only marks Approved or Scheduled cases. If another worker has already committed Applied or AwaitingRenewal, the losing worker preserves that result and its history. Repeated automatic ticks select neither completed state.

Renewal activation uses the same classifier after its serializable transaction is disposed. A concurrent activation conflict returns `Students:Records:ConcurrencyConflict` for an explicit refresh; it does not retry the command automatically or mark the winning renewal as failed. Unrelated failures retain their existing handling.

`StudentRecordsEnrollmentScopeService.EnsureTargetPlacementAsync` validates the target tenant, academic year and complete class/division hierarchy using the existing precise division-scope query. Placement request/application and transition readiness share this check. Parent board/medium IDs populated from a narrow class assignment do not authorize sibling classes; explicit wildcard or exact division grants remain authoritative. The shared generic scope implementation is unchanged.

Renewal activation creates an enrollment before Finance assignment, matching existing Onboarding ordering. Renewal then remains Provisioning until confirmed Finance/portal evidence exists. Governed permitted placement edits synchronize the active enrollment and renewal target. No records service creates arbitrary admission FKs or performs a fee reversal.

`StudentOnboardingRollRuleReader` resolves the target academic path without fabricating an Admission or Onboarding case. A submitted annual transition freezes that owner rule in its extra properties. `StudentOnboardingRollNumberAllocator` serves both initial Onboarding and Records annual/return commands. Target roll validation observes school manual/automatic/both mode, starting number, class or division scope, and reason requirements, including outstanding onboarding/renewal/transition reservations. Both mode requires an explicit staff choice. Previewed automatic proposals are checked again during serializable submission; a changed proposal or competing serializable reservation requires a refreshed preview. Existing GR numbers stay permanent. Records promotion/return uses only the Records approval chain; initial Onboarding retains its own approval rules.

Reviewed same-year ReuseProfile returns reactivate the existing inactive enrollment and preserve owner history. Cancelling an unactivated PendingChecklist renewal retains both case and transition audit. Provisioning permits school-authorized division/roll edits, but has no automated target-class recovery. Class changes with an immutable Finance assignment are blocked in ordinary Placement and same-year Return until a Finance-owned impact workflow exists; completion does not bypass this dependency. Migration `20261001021100_AddStudentRecordRenewalHistory` changes only the renewal index predicate and refuses a downgrade that cannot retain repeated reviews. Its target model derives from the previous generated Records migration and changes only this predicate, keeping separately developing Attendance models outside this migration.

`StudentRecordAdmissionReturnService` orchestrates the frozen Records Status authorization through existing Admissions and Onboarding owners. Handoffs persist typed entry/application/inquiry/transition references, intake configuration/contact and source provenance; `ReturnHandoffId` remains in transition extra properties. Migration `20261001060643_AddStudentRecordAdmissionReturn` changes only the return-handoff table and transition source nullability. Its target model retains the preceding Attendance migration and changes only those two Records entities. Unique linked application/inquiry indexes apply only where TransitionId exists, preserving legacy duplicate handoffs. Legacy rows are not backfilled with invented approval authority. A used governed return or source-less historical return blocks downgrade; use a forward fix.

`StudentRecordAdmissionReturnClosureService` is an internal, non-remote coordinator called by the existing Admissions Decide and Onboarding Cancel owners. It joins their unit of work, validates tenant and exact handoff/transition/case identity, rejects activation, and appends one closure event. Replays cannot append duplicate closure events. Fresh Inquiry and DirectRegistration applicants reserve a unique unused future Student ID per journey; `ExistingStudentId` remains the permanent profile link, including after repeated rejected/cancelled returns. No new schema is required for closure.

Onboarding activation maps optimistic database concurrency and wrapped SQLSTATE `40001` to its existing `Students.Onboarding.ConcurrencyConflict` response after disposing the serializable transaction. These conflicts bypass activation failure-state repair, preserving the committed competing activation/cancellation. Unrelated activation failures retain the existing repair flow. Cancellation remains in its owner transaction, including linked Records closure; an optimistic conflict rolls the entire command back.

## Reserved Student identity and rollout

`AdmissionApplicant.ReservedStudentId` is supplied by `AdmissionInquiryAppService` using the server GUID generator.
`AdmissionIdentityConfiguration` requires the GUID, prohibits ordinary EF changes after save, and adds `UX_AdmissionApplicants_ReservedStudentId` plus a nonempty check constraint.
There is no Student foreign key and no database-generated default.
`AdmissionApplicationDecisionService` uses this ID only on its new-Student branch; existing-Student selection and actual application links keep their current meanings.
The Domain `IAdmissionFinalizationConflictClassifier` contract keeps provider-specific exception handling inside EF's `AdmissionFinalizationConflictClassifier`.
Only Student-primary-key collisions, relevant onboarding unique constraints and optimistic concurrency errors are translated; tenant filters remain enabled and unrelated failures propagate.

Migration `src/ShikshaMitra.EntityFrameworkCore/Migrations/20260924103013_AddAdmissionApplicantReservedStudentId.cs` adds the column nullable, backfills every applicant (including deleted and already-linked records), then enforces required/nonempty/global uniqueness.
Existing Student IDs, actual applicant/application links and historical data are not rewritten; backfilled reservations on already-linked applicants remain unused.
Review generated SQL and rehearse against an isolated PostgreSQL database before rollout.
Deployment requires admission writes to stop while the migration and matching backend binaries are installed: older binaries cannot insert applicants into the new required column without a default.
Do not roll back to old binaries while writes continue; use a coordinated recovery window or a separately designed expand/contract release if zero-downtime deployment is required.
Do not drop or regenerate reservations once they are in use, and never delete Students as a rollback shortcut.

## Offering implementation map

| Concern | Source |
|---|---|
| Aggregate and effective dates | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionOffering.cs` |
| Deterministic availability | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionOfferingAvailability.cs` |
| DTOs and validators | `src/ShikshaMitra.Application.Contracts/Students/Enrollments/Admissions/AdmissionOfferingDtos.cs` and `AdmissionOfferingDtoValidators.cs` |
| Hierarchy and readiness validation | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingConfigurationValidator.cs` |
| Scoped projections | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingReadService.cs` |
| Commands and concurrency | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingCommandService.cs` |
| Permission facade | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingAppService.cs` |
| Inquiry and application guard | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionOfferingRuntimeService.cs` |
| EF mapping and indexes | `src/ShikshaMitra.EntityFrameworkCore/Students/StudentEnrollments/Admissions/AdmissionOfferingConfiguration.cs` |
| HTTP routes | `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/Admissions/AdmissionOfferingsController.cs` |

## Boundary rules

The domain aggregate owns normalized code, date inheritance, identity locking, and active state transitions.
The application validator owns repository-backed hierarchy, UserScope, published reference, cycle-window, and duplicate checks.
The read service applies tenant filters, academic UserScope, display projections, readiness, availability, semantic filters, and paging.
Inquiry and application services call the shared runtime service instead of duplicating availability logic.
The frontend owns interaction state only and cannot replace backend authorization or hierarchy validation.

## Workflow implementation map

| Concern | Source |
|---|---|
| Definition, stage, and transition aggregates | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowDefinition.cs`, `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowStageDefinition.cs`, and `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowTransitionDefinition.cs` |
| Graph validation | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowGraphValidator.cs` |
| Controlled condition fields and evaluation | `src/ShikshaMitra.Domain.Shared/Students/StudentEnrollments/Admissions/AdmissionWorkflowConditionFieldRegistry.cs` and `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowConditionEvaluator.cs` |
| Deterministic transition selection | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowTransitionSelector.cs` |
| Runtime handler registry | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionWorkflowStageHandlers.cs` |
| Runtime instances and history | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/AdmissionApplicationRuntimeEntities.cs` |
| Configuration commands and queries | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionWorkflowAppService.cs` |
| Runtime transition command | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionApplicationAppService.cs` |
| HTTP routes | `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/Admissions/AdmissionWorkflowsController.cs` and `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/Admissions/AdmissionApplicationsController.cs` |

Form runtime persistence is separated from configuration: `AdmissionFormInstance` pins a published definition/version, `AdmissionFormFieldValue` stores mutable values, `AdmissionFormSubmission` stores immutable submitted snapshots, and `AdmissionFormChangeHistory` records lifecycle changes. Form definition changes never mutate an existing instance.

Draft graph updates synchronize existing child rows by identifier, insert new rows, and delete removed transitions before removed stages. This preserves required EF relationships and prevents conceptual-null failures while a draft is edited.

## Assessment implementation map

| Concern | Source |
|---|---|
| Opt-in versioned policy and error codes | `src/ShikshaMitra.Domain.Shared/Students/StudentEnrollments/Admissions/AdmissionAssessmentPolicy.cs` |
| Shared sessions and applicant evidence | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/Aggregates/AdmissionAssessmentSession.cs` and `AdmissionAssessmentAttempt.cs` in the same folder |
| Completion facts and negative-outcome route checks | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/Services/AdmissionAssessmentEvidence.cs` |
| API contracts | `src/ShikshaMitra.Application.Contracts/Students/Enrollments/Admissions/AdmissionAssessmentDtos.cs` and `IAdmissionAssessmentAppService.cs` in the same folder |
| Scoped access, reads, commands, sessions, and runtime integration | `src/ShikshaMitra.Application/Students/StudentEnrollments/Admissions/AdmissionAssessment*Service.cs` |
| Permissions and explicit HTTP routes | `src/ShikshaMitra.Domain.Shared/Constants/Permissions/SubmodulePermissions/Students/ShikshaMitraSubmodulePermissions.Students.Assessments.cs` and `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/Admissions/AdmissionAssessmentsController.cs` |
| EF tables and indexes | `src/ShikshaMitra.EntityFrameworkCore/Students/StudentEnrollments/Admissions/AdmissionAssessmentConfiguration.cs` |
| Additive migration | `src/ShikshaMitra.EntityFrameworkCore/Migrations/20260910070250_AddAdmissionAssessments.cs` |

Assessment sessions and attempts are tenant-owned audited aggregates with optimistic concurrency stamps.
Attempt numbers are unique within a stage instance and preserve cancelled attempts as history.
Foreign keys to offerings, workflow stages, runtime stages, applications, and sessions use restrictive deletion.
The managed runtime checks application-stage ownership and academic scope, including authorized skip/override paths.

## Payment implementation map

| Concern | Owner |
| --- | --- |
| Versioned policy and validation | `AdmissionPaymentStagePolicy.cs` in Domain.Shared Admissions |
| Financial transaction, immutable evidence, and state rules | `Domain/Finance/Admissions/AdmissionPaymentTransaction.cs` |
| Scope-safe commands, reads, and completion facts | `Application/Students/StudentEnrollments/Admissions/AdmissionPayment*Service.cs` |
| Explicit permissions and HTTP routes | `ShikshaMitraSubmodulePermissions.Students.Payments.cs` and `AdmissionPaymentsController.cs` |
| PostgreSQL mapping and additive migration | `Finance/Admissions/AdmissionPaymentTransactionConfiguration.cs` and `20260911064407_AddAdmissionPaymentRuntime.cs` |

Admissions owns workflow orchestration; Finance owns the monetary record. Restrictive application and stage foreign keys preserve evidence. Unique tenant/stage attempt and idempotency indexes prevent duplicate collection, while tenant receipt and provider-reference indexes protect reconciliation identifiers.

## Managed Offer implementation map

| Concern | Owner |
| --- | --- |
| Versioned policy, bounds and localized errors | `Domain.Shared/Students/StudentEnrollments/Admissions/AdmissionOfferStagePolicy.cs` |
| Current offer state and lifecycle invariants | Existing `Domain/Students/StudentEnrollments/Admissions/AdmissionApplication.cs` |
| Immutable action audit and retry identity | Existing `AdmissionApplicationDecisionHistory.cs` with nullable Offer metadata |
| Scope-safe reads, commands, completion and transition facts | `Application/Students/StudentEnrollments/Admissions/AdmissionOffer*Service.cs` |
| Explicit negative-outcome routing | `Domain/Students/StudentEnrollments/Admissions/Services/AdmissionOfferRouteRules.cs` |
| Permissions and HTTP routes | `ShikshaMitraSubmodulePermissions.Students.Offers.cs` and `AdmissionOffersController.cs` |
| Additive PostgreSQL change | `20260911071841_AddManagedAdmissionOffers.cs` |

Managed Offer strengthens the existing application aggregate instead of introducing a competing Offer table. The migration adds nullable audit fields to existing decision history and a filtered tenant/application/stage idempotency index, preserving every earlier decision row. Workflow history snapshots the authoritative Offer facts used to choose the next transition.
Academic filtering uses explicit wildcard grants rather than treating ancestor IDs from a narrow assignment as broad permissions.
Workflow advancement overlays backend-owned assessment facts over form values and stores evaluated facts with the transition history.

## Managed Inquiry ownership

| Concern | Source |
| --- | --- |
| Versioned policy, enums, validation, and error codes | `src/ShikshaMitra.Domain.Shared/Students/StudentEnrollments/Admissions/AdmissionInquiryPolicy.cs` |
| Audited management and follow-up aggregates | `src/ShikshaMitra.Domain/Students/StudentEnrollments/Admissions/Aggregates/AdmissionInquiryManagement.cs` and `AdmissionInquiryFollowUp.cs` |
| Offering policy resolution and compatibility | Application admissions `AdmissionInquiryPolicyResolver` and `AdmissionInquiryOfferingValidator` |
| Tenant, UserScope, and counsellor checks | Application admissions `AdmissionInquiryScopeAccessService` and `AdmissionInquiryPeopleService` |
| Assignment and lifecycle behavior | Application admissions `AdmissionInquiryAssignmentService`, `AdmissionInquiryRuntimeService`, and `AdmissionInquiryManagementAppService` |
| Application-start and workflow gates | Application admissions `AdmissionApplicationLifecycleService` and `AdmissionApplicationWorkflowService` |
| Explicit HTTP boundary | `src/ShikshaMitra.HttpApi/Controllers/Student/Enrollment/Admissions/AdmissionInquiryManagementController.cs` and `AdmissionInquirySettingsController.cs` |
| Persistence | EF admissions `AdmissionInquiryManagementConfiguration.cs` and the Student DbContext partial |

Management is tenant scoped and linked restrictively to its Inquiry and pinned stage definition.
The unique Inquiry index prevents two active management records for one Inquiry.
Follow-up queue indexes cover inquiry/status/due and assigned counsellor/status/due access paths.
Migration `20260911045603_AddAdmissionInquiryManagement` adds only the management and follow-up tables with supporting indexes and restrictive foreign keys.

## Persistence rules

Offering foreign keys use restrictive delete behavior.
Application and inquiry references to offerings use restrictive delete behavior.
The migration `src/ShikshaMitra.EntityFrameworkCore/Migrations/20260814180000_ProductionizeAdmissionOfferings.cs` makes override dates nullable, adds defaults, indexes runtime queries, and creates restrictive foreign keys.

## Document verification ownership

`AdmissionDocumentPolicy` and `AdmissionDocumentCondition` in Domain.Shared define versioned configuration and bounded rules. Domain `Aggregates/AdmissionDocumentEvidence.cs` and `AdmissionDocumentReview.cs` preserve immutable payload/history; `Services/AdmissionDocumentRules.cs` evaluates collection completeness and applicability. Shared `Students/Documents/DocumentFileValidator.cs` validates file signatures, MIME, format, actual size, and hashes.

Application `AdmissionDocumentAccessService`, `AdmissionDocumentEvaluationService`, `AdmissionDocumentRuntimeService`, `AdmissionDocumentReadService`, `AdmissionDocumentCommandService`, and `AdmissionDocumentFileService` separate scope, evidence decisions, lifecycle gates, projections, writes, and private storage. `AdmissionApplicationScopeAccessService` applies the shared precise offering-scope query to touched application detail/list and decision/finalization paths. The thin `AdmissionDocumentVerificationAppService` and controller enforce distinct permissions.

EF `AdmissionDocumentConfiguration` maps tenant evidence/review tables, unique stage/requirement/version and evidence/review keys, application serialization, and restrictive foreign keys. File bytes use the configured private ABP database blob provider within the command unit of work. Evidence links to Student without copying or deleting canonical blobs.

New evidence, review, and Student-link timestamps use explicit UTC instants. The shared Student document manager delays replacement-blob cleanup until successful unit-of-work completion, preserving the previous file when an onboarding update rolls back.

`Students/Records/StudentRecordProfileAppService.cs` projects the current Student identity and placement into `StudentRecordProfileDto`; the explicit `StudentRecordProfileController` exposes the read-only GUID route.
`StudentScopeAccessService.EnsureCanAccessStudentAsync` uses `StudentRecordScope` for direct Student reads and commands, including admission-evidence access.
The matcher uses explicit wildcard grant sets and exact division IDs with an academic-year boundary, because ordinary ancestor ID lists also contain the parents of narrower assignments.
This correction is limited to the shared Student direct-record boundary; it is not a rewrite or audit of generic cross-module, list, or attendance query filters.

Additive migration `20260910123420_AddAdmissionDocumentVerification` creates only `AppAdmissionDocumentEvidence` and `AppAdmissionDocumentReviews`, with restrictive application, runtime-stage, document-type, evidence, and Student links. It does not rewrite existing application, Student, or assessment rows.

Workflow facts expose `documents.status` (`verified` or `pending`), `documents.missingCount`, `pendingCount`, `rejectedCount`, `waivedCount`, and `verifiedCount`. `verified` means all applicable mandatory requirements are satisfied; optional rejected evidence remains visible through counts. Generic `assessment.*` conditions use the application's latest non-cancelled attempt. Inquiry snapshot keys use the saved binding key when one exists, otherwise the original field code.

## Eligibility ownership

| Responsibility | Source |
|---|---|
| Versioned policy, controlled field/operator schema, errors | Domain.Shared `Students/StudentEnrollments/Admissions/AdmissionEligibilityPolicy.cs` and `AdmissionEligibilityConditionSchema.cs` |
| Calendar ages, three-valued typed comparisons, explanations, route evidence | Domain admissions `Services/AdmissionEligibility*` |
| Immutable evaluations and separate exception request/decision records | Domain admissions `Aggregates/AdmissionEligibilityEvaluation.cs` and `AdmissionEligibilityException.cs` |
| Scoped context and pinned source resolution | Application admissions `AdmissionEligibilityAccessService`, `AdmissionEligibilitySourceStageService`, `AdmissionEligibilityAnswerService` |
| Offering form compatibility, configuration and availability guards | Application admissions `AdmissionEligibilityOfferingValidator`, shared by Offering configuration and read services |
| Evidence fingerprints, summaries, explicit writes, continuation and finalization gates | Application admissions `AdmissionEligibilityEvaluationService`, `AdmissionEligibilityStateService`, `AdmissionEligibilityReadService`, `AdmissionEligibilityCommandService`, `AdmissionEligibilityRuntimeService` |
| Contracts, permissions, HTTP boundary | `AdmissionEligibilityDtos`, `IAdmissionEligibilityAppService`, Students Eligibility permission constants, `AdmissionEligibilityController` |
| Tenant tables, unique stage/sequence and request/decision indexes, restrictive links | EF admissions `AdmissionEligibilityConfiguration.cs` |

Eligibility composes facts outside `AdmissionWorkflowRuntimeValueService`, avoiding a dependency cycle through document evaluation. Assessment/document criteria resolve their configured source code inside the application's pinned workflow and require that source instance to be completed. Source identity, selected evidence and declared values feed a deterministic fingerprint; generic latest-assessment facts and form-supplied reserved results cannot substitute for them. Successful finalized history is read from immutable snapshots without recalculating against the newly linked Student identity.

Every mutation serializes against the parent application concurrency stamp. Evaluations and exception decisions are append-only through the API, use UTC audit timestamps, and have tenant filters and restrictive application/stage/evaluation links. Initial finalization revalidates eligibility inside the existing finalization unit of work; the already-successful retry branch preserves its outcome. New inquiry source form IDs use the existing extra-properties column, leaving legacy snapshots untouched.

Migration `20260910133935_AddAdmissionEligibility` adds only the evaluation and exception tables, eight explicit indexes and six restrictive foreign keys. It does not rewrite existing admissions or inquiry snapshots. Apply it before running a host build that includes these entities.

## Managed Review ownership

| Concern | Source |
|---|---|
| Versioned policy and localized errors | `Domain.Shared/Students/StudentEnrollments/Admissions/AdmissionReviewPolicy.cs` |
| Pinned source dominance and negative route rules | Domain `AdmissionReviewGraphRules` and `AdmissionReviewRouteRules` |
| Immutable recommendations and actual decisions | Domain `AdmissionReviewRecord` and `AdmissionReviewDecision` |
| Current assignment, deadline and operational queue state | Domain `AdmissionReviewWorkItem` |
| Scoped context, evidence snapshots and freshness | Application `AdmissionReviewAccessService`, `AdmissionReviewEvidenceService` and `AdmissionReviewStateService` |
| Reviewer assignment, configured roles and independent approver | Application `AdmissionReviewPeopleService` |
| Default reviewer compatibility with a concrete offering | Application `AdmissionReviewOfferingValidator`, reusing `AdmissionOfferingScopeQuery` |
| Existing admission decision and workflow integration | Application `AdmissionReviewDecisionGuard`, `AdmissionReviewRuntimeService`, `AdmissionApplicationDecisionService` and `AdmissionApplicationWorkflowService` |
| Commands, safe summaries and server-filtered queue | Application `AdmissionReviewCommandService`, `AdmissionReviewReadService` and `AdmissionReviewQueueService` |
| Explicit HTTP boundary | `HttpApi/Controllers/Student/Enrollment/Admissions/AdmissionReviewsController.cs` |
| Persistence | EF `AdmissionReviewConfiguration` and the Student DbContext partial |

All three Review records are tenant scoped and use restrictive application/stage links; recommendation sequences and actual decisions have unique ownership constraints.
Mutations serialize on the application concurrency stamp and share the existing unit of work with admission history and decisions.
Stage entry creates the queue work item, so GET requests never create operational history.
The queue filters offering UserScope, active stage, assignment and deadlines in SQL before paging and evaluating the returned summaries.
The fingerprint sorts unordered evidence collections and reads the canonical linked Student identity when present; raw fingerprints, storage keys and snapshot JSON are not public DTO fields.
The main application detail exposes Review decision capabilities so existing decision controls do not depend on a separate Review workspace read.

## Managed Form ownership

`AdmissionFormStagePolicy` defines the opt-in versioned behavior while the existing `AdmissionFormDefinition`, `AdmissionFormInstance`, immutable submissions, and change history remain the source of truth. `AdmissionFormStageAccessService` resolves a scope-safe pinned context and enforces that the stage form equals the frozen application form. Read, command, and runtime services separately compose summaries, audited verification/correction commands, submit gates, and workflow completion gates.

Section verification is reconstructed from immutable form change history. A correction marker makes every earlier verification stale, so no mutable verification table or migration is required. Writes serialize through the parent application concurrency stamp and append both form history and admission activity. The Offering validator checks workflow/form compatibility before activation and again before new applications start.

## Managed Seat ownership

| Concern | Source |
|---|---|
| Versioned configuration and validation | `Domain.Shared/Students/StudentEnrollments/Admissions/AdmissionSeatStagePolicy.cs` |
| Capacity and applicant allocation invariants | Domain `AdmissionSeatPool` and `AdmissionSeatReservation` |
| Negative-outcome route safety | Domain `AdmissionSeatRouteRules` |
| Scope, pinned workflow, reads, commands and entry/completion gates | Application `AdmissionSeat*Service.cs` |
| Permissions and HTTP boundary | `ShikshaMitraSubmodulePermissions.Students.Seats.cs` and `AdmissionSeatsController.cs` |
| Tenant tables, unique ownership and operational indexes | EF `AdmissionSeatConfiguration.cs` |

The pool concurrency stamp serializes changes to shared offering capacity, while the application and reservation stamps protect applicant state. Filtered unique indexes prevent duplicate live pools and duplicate application-stage allocations. The additive `20260911084131_AddManagedAdmissionSeats` migration creates only the pool and reservation tables, indexes, and restrictive ownership links.
## Student dashboard ownership

| Layer | Source |
|---|---|
| HTTP | `src/ShikshaMitra.HttpApi/Controllers/Student/StudentModuleDashboardController.cs` |
| Context, role priorities, destination grants | `src/ShikshaMitra.Application/Students/Dashboard/StudentModuleDashboardAppService.cs`, `StudentModuleDashboardCatalog.cs` |
| SQL summaries | `StudentModuleDashboardRecordsReader.cs`, `StudentModuleDashboardAdmissionsReader.cs`, `StudentModuleDashboardAttendanceReader.cs` in that directory |
| Contract/errors | `src/ShikshaMitra.Application.Contracts/Students/Dashboard/`, `src/ShikshaMitra.Domain.Shared/Students/Dashboard/` |

Readers reuse Records enrollment scope, Admission inquiry/application scope, exact division scope and Portal association services. Counts/grouping/projections execute in SQL; full populations are not materialized. Queries run sequentially on the request DbContext. The frontend independently loads authorized sections, so one failed section can be retried alone. There is no server aggregate cache that could cross identity/scope boundaries and no new persistence schema. Existing Reporting dashboard providers remain separate for compatible consumers.
