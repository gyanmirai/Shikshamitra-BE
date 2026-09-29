---
module: student
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-09-24
owner: backend-team
audience: humans-and-agents
---

# Locate Student Enrollment Admissions code

TL;DR: Admissions remains inside the Student modular-monolith boundary and separates aggregate invariants, authoritative validation, reads, commands, and runtime availability.

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
