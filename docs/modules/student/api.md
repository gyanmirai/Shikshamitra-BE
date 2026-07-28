---
module: student
domain: api
content_type: Reference
status: canonical
updated_on: 2026-07-25
owner: backend-team
audience: humans-and-agents
---

# Use Student backend APIs

TL;DR: use this page to find Student endpoint families and compatibility notes.
Controller source paths are canonical when route details change.

## Enrollment API families

| Family | Controller path | Notes |
|---|---|---|
| Inquiry | `Controllers/Student/Enrollment/InquiryController.cs` | Enrollment inquiry workflows |
| Registration | `Controllers/Student/Enrollment/RegistrationController.cs` and registration sub-controllers | Multi-section registration data |
| Admission | `Controllers/Student/Enrollment/AdmissionController.cs` | Admission workflow |
| Entrance exam | `Controllers/Student/Enrollment/EntranceExamController.cs` | Entrance exam slot and workflow APIs |
| Notes | `Controllers/Student/Enrollment/NotesController.cs` | Student enrollment notes |
| Enrollment settings | `Controllers/Student/Enrollment/Settings/**` | Inquiry Source, document type, and entrance exam slot settings |
| Admissions CRM | `Controllers/Student/Enrollment/AdmissionsCrm/**` | Future inquiry-to-entrance-exam CRM APIs |

## Records API families

| Family | Controller path |
|---|---|
| Student list | `Controllers/Student/Records/StudentListController.cs` |
| Student management | `Controllers/Student/Records/StudentManagementController.cs` |
| Bulk upload | `Controllers/Student/Records/StudentBulkUploadController.cs` |
| Dashboard | `Controllers/Student/Records/StudentDashboardController.cs` |
| Certificates | `Controllers/Student/Records/StudentCertificateController.cs` |
| House settings | `Controllers/Student/Records/Settings/HouseSettingsController.cs` |

## Attendance, leave, and compliance API families

| Family | Controller path |
|---|---|
| Student attendance | `Controllers/Student/Attendance/StudentAttendanceController.cs` |
| Student leave | `Controllers/Student/Attendance/StudentLeaveController.cs` |
| Student leave type settings | `Controllers/Student/Attendance/Settings/StudentLeaveTypeController.cs` |
| Identity compliance | `Controllers/Student/Compliance/StudentIdentityComplianceController.cs` |

## Inquiry Source compatibility

Inquiry Source is exposed through the existing `inquiry-preferences` route for compatibility.
Product copy should say Inquiry Source, while route and DTO names can still use Inquiry Preference.

| Route | Purpose |
|---|---|
| `/api/student-enrollment/{academicYearId}/settings/inquiry-preferences` | List or create Inquiry Sources |
| `/api/student-enrollment/settings/inquiry-preferences/{id}` | Get, update, or delete one source |
| `/api/student-enrollment/settings/inquiry-preferences/{id}/status` | Activate or deactivate one source |

## Admissions CRM API

The CRM route family is `api/student-enrollment/admissions-crm`.
Use it for future-state inquiry leads, CRM configuration, and entrance exam CRM behavior.
Keep legacy inquiry and entrance exam routes stable until their consumers are fully migrated.
CRM inquiry lead payloads support optional applicant DOB for duplicate review and eligibility handoff context without making DOB required for quick inquiry creation.
Every authenticated staff lead-creation payload requires a nonblank first name regardless of its selected channel, while anonymous public forms continue through their separate dynamic-form contract.
Quick inquiry payloads preserve separate first, middle, and last name fields, validate email on the server, and snapshot the selected effective admission-route definition.
An effective published Fresh definition is the compatibility fallback when a staff client omits the route definition.
An academic year with no route catalog can still accept a route-neutral legacy staff inquiry, while a configured catalog without exactly one effective Fresh fallback rejects the omission.
A supplied route enum without a definition is always rejected.
When `TargetStandardId` is supplied, the backend validates its academic-year hierarchy and derives `DesiredClassName` from the authoritative Standard name.
Registration prefill uses the immutable route definition ID, version, and name snapshot, with the legacy route enum name only as a fallback for records created before route-definition snapshots existed.
Special-route evidence references use `CODE:REFERENCE` format and must cover every evidence code required by the captured route-definition version before assignment, eligibility approval, or activation.
Sibling-priority assignment also requires the referenced tenant student to share a normalized contact identifier recorded on the student or guardian profile with the applicant rather than accepting an arbitrary student ID.
An applicant may continue assignment work against an exact route version captured while published after that version expires or is retired, and later route changes must retain an unbroken assignment lineage to the captured intent.

| Route pattern | Purpose |
|---|---|
| `/{academicYearId}/leads` | Create and list lightweight CRM inquiry leads after validating that the tenant academic year exists |
| `/{academicYearId}/quick-inquiry-options` | Return safe operational cycle flags, enabled ordered labeled channels, and currently effective published admission routes for staff with CRM Add or View permission after validating that the tenant academic year exists |
| `/{academicYearId}/leads/duplicate-preview` | Preview likely duplicate CRM leads |
| `/leads/{leadId}/duplicates/merge` | Merge one duplicate lead into the surviving CRM lead with reason |
| `/leads/{leadId}/owner` | Assign or clear a CRM lead owner |
| `/leads/{leadId}/priority` | Set manual CRM lead priority with optional reason |
| `/leads/{leadId}/stage` | Move a CRM lead across operational inquiry stages with reason |
| `/leads/{leadId}/registration-form` | Issue a CRM-native registration form on one lead before exam scheduling when the cycle requires it |
| `/{academicYearId}/registrations/from-paper` | Create an inquiry and registration application from a staff-entered physical form with paper provenance and data-entry state |
| `/leads/{leadId}/score/recalculate` | Recalculate a baseline CRM lead score |
| `/leads/{leadId}/archive` | Archive one CRM lead with reason |
| `/leads/{leadId}/reopen` | Reopen one stale or archived CRM lead with reason |
| `/{academicYearId}/automation/stale-archive/run` | Run configured stale and archive thresholds and create internal stale follow-up tasks |
| `/{academicYearId}/automation/sla-reminders/run` | Create follow-up tasks for CRM leads that missed their stage SLA threshold, with cycle default fallback |
| `/{academicYearId}/automation/journeys/run` | Run configured condition-aware journey rules for one trigger and create deduplicated follow-up tasks, message delivery logs, or failure evidence |
| `/leads/{leadId}/tasks` | Create or list follow-up tasks for one CRM lead |
| `/leads/{leadId}/timeline` | List timeline events for one CRM lead |
| `/leads/{leadId}/applicant-checklist` | List cycle-configured applicant checklist items merged with lead-specific progress |
| `/leads/{leadId}/applicant-checklist/{key}` | Update one lead-specific applicant checklist item with progress and internal notes |
| `/leads/{leadId}/notes` | Add a staff lead note or call note to the CRM timeline |
| `/tasks/{taskId}/complete` | Complete one follow-up task |
| `/{academicYearId}/exams` | List CRM entrance exam records |
| `/leads/{leadId}/exam` | Get the CRM entrance exam record for one lead |
| `/leads/{leadId}/exam/schedule` | Schedule or reschedule a CRM lead into an entrance exam slot after cycle readiness checks |
| `/exams/{examRecordId}/attendance` | Mark CRM entrance exam attendance |
| `/exams/{examRecordId}/result` | Record CRM entrance exam marks and manual or class-wise merit rank |
| `/{academicYearId}/bulk/exam-results` | Record distinct results for up to 100 exam records and return per-item success or failure |
| `/exams/{examRecordId}/eligibility` | Set eligibility, waitlist, or rejection decision |
| `/exams/{examRecordId}/eligibility-handoff` | Return read-only lead and exam context for admission handoff |
| `/{academicYearId}/analytics/summary` | Return funnel, lead-level conversion rates, stage-configured first-response SLA, follow-up SLA miss, source, campaign, date-range trend, staff workload, task, duplicate, automation run, automation failure, exam, class-wise exam, and exam governance analytics |
| `/{academicYearId}/operations/overview` | Return permission-aware operational insight counts and urgent work items for paper entry, documents, fee follow-up, exams, offers, Class Placement Plan risk, activation, and handoff blockers |
| `/{academicYearId}/operations/queues/summary` | Return permission-aware admissions operations queue counts and urgent counts |
| `/{academicYearId}/operations/queues/{queueType}` | Return one permission-aware admissions operations queue with applicant context and supported filters |
| `/setup/cycles` | List academic-year admission cycles with status, readiness, applicant count, owner placeholder, last-updated metadata, and required actions for staff with Manage Configuration permission |
| `/setup/cycles/{academicYearId}/summary` | Return the selected admission cycle summary, current policy snapshot, communication rule counts, readiness items, and required actions without claiming unsupported live integrations |
| `/setup/{academicYearId}/status` | Return setup readiness for the selected academic-year admission cycle |
| `/{academicYearId}/configuration/cycle` | Get or update CRM cycle defaults, inquiry channel enablement and defaults, exam readiness fields, merit ranking mode, lead scoring rules, routing strategy, owner pool, routing rules, and condition-aware journey rules |
| `/{academicYearId}/configuration/pipeline-stages` | Get fixed CRM pipeline milestones with color, SLA target, owner responsibility, and allowed next-stage metadata for staff with CRM Add, View, or Manage Configuration permission after validating that the tenant academic year exists |
| `/configuration/pipeline-stages/{stageId}` | Update one CRM pipeline stage display and governance configuration used by manual transition enforcement |
| `/{academicYearId}/configuration/form-templates` | Create and list inquiry form templates |
| `/configuration/form-templates/{id}` | Get or update one inquiry form template |
| `/configuration/form-templates/{id}/status` | Activate or deactivate one inquiry form template |
| `/{academicYearId}/configuration-safety/references` | Return authoritative Stage, Round, Exam, Seat Plan, or Route reference counts and sample IDs for one normalized code |
| `/{academicYearId}/configuration-safety/impact-preview` | Return changed fields, affected applicant count, references, warnings, and a SHA-256 preview fingerprint |
| `/configuration-safety/clone/preview` | Preview source-to-target academic-year clone families, conflicts, warnings, and confirmation fingerprint |
| `/configuration-safety/clone/confirm` | Confirm the unchanged clone preview with its fingerprint and report idempotent replay |
| `/{academicYearId}/configuration/message-templates` | Create and list WhatsApp, SMS, and email message templates |
| `/configuration/message-templates/{id}` | Get or update one message template |
| `/configuration/message-templates/{id}/status` | Update message template approval status and active state |
| `/{academicYearId}/communication/message-templates/preview` | Render one draft or approved message template with sample, provided, or lead variables without creating a delivery log |
| `/{academicYearId}/communication/providers` | Get or initialize WhatsApp, SMS, and email provider settings |
| `/{academicYearId}/communication/providers/{channel}` | Update one channel provider setting |
| `/{academicYearId}/communication/opt-outs` | List, filter, or record communication opt-out records |
| `/{academicYearId}/communication/opt-outs/clear` | Clear one active communication opt-out |
| `/leads/{leadId}/messages/send-template` | Render one approved template, apply opt-out and provider checks, optionally dispatch to provider webhook, and create a communication delivery log |
| `/{academicYearId}/communication/delivery-logs` | List communication delivery logs with provider message ID evidence and provider ID filtering |
| `/communication/delivery-logs/{deliveryLogId}/retry` | Queue a delivery retry after provider and opt-out checks |

Journey communication rules can target inquiry acknowledgement, exam, registration, document, fee, offer, admission confirmation, activation, and welcome moments through fixed journey triggers.
Delivery status must be read from communication delivery logs and provider callbacks rather than inferred from a configured rule.

## Registration localization contract

Registration sections support `localizedTitles` and `localizedDescriptions`.
Registration fields support `localizedLabels`, `localizedHelpTexts`, and `localizedOptions`.
Localized option arrays must have the same item count as their canonical option arrays so submitted values remain language-neutral.

## Result correction governance

`POST /api/student-enrollment/admissions-crm/{academicYearId}/bulk/exam-results` requires `Students.Enrollment.AdmissionsCrm.BulkOperations` and `Students.Enrollment.AdmissionsCrm.ChangeExamResult`.
Duplicate `examRecordId` values reject the batch with `AdmissionCrm:DuplicateBulkExamResultItems`.
Replacing an existing result through either the individual or bulk endpoint requires a nonblank `correctionReason` and `Students.Enrollment.AdmissionsCrm.OverrideExamResult`.
Missing correction evidence rejects with `AdmissionCrm:ExamResultCorrectionReasonRequired`.
An unchanged retry is idempotent and returns the existing result without a second correction.
Individual and bulk result evidence records previous values, new values, and the correction reason in the applicant timeline.

## Granular action permissions

| Action family | Required permissions |
|---|---|
| Exam results | `Students.Enrollment.AdmissionsCrm.ChangeExamResult`, plus `Students.Enrollment.AdmissionsCrm.BulkOperations` for bulk entry and `Students.Enrollment.AdmissionsCrm.OverrideExamResult` for replacement |
| Offers and capacity | `Students.Enrollment.AdmissionsCrm.ManageOffers`, `Students.Enrollment.AdmissionsCrm.ApproveOffers`, and `Students.Enrollment.AdmissionsCrm.OverrideSeatCapacity` according to the action |
| Waitlist and decision | `Students.Enrollment.AdmissionsCrm.ManageWaitlist` and `Students.Enrollment.AdmissionsCrm.MakeAdmissionDecision` |
| Registration | `Students.Enrollment.AdmissionsCrm.IssueRegistrationForm`, `Students.Enrollment.AdmissionsCrm.ManageRegistrationLinks`, `Students.Enrollment.AdmissionsCrm.VerifyRegistration`, or `Students.Enrollment.AdmissionsCrm.WaiveRegistrationFee` according to the action |
| Documents and readiness | `Students.Enrollment.AdmissionsCrm.ManageDocumentConfiguration`, `Students.Enrollment.AdmissionsCrm.ReviewDocuments`, `Students.Enrollment.AdmissionsCrm.WaiveDocuments`, `Students.Enrollment.AdmissionsCrm.ConfirmOriginalSeen`, or `Students.Enrollment.AdmissionsCrm.ConfirmAdmissionReadiness` according to the action |
| Government readiness | `Students.Enrollment.AdmissionsCrm.ManageGovernmentReadiness` |
| Financial readiness | `Students.Enrollment.AdmissionsCrm.IssueQuotations`, `Students.Enrollment.AdmissionsCrm.RecordFinanceStatus`, `Students.Enrollment.AdmissionsCrm.RequestConcession`, `Students.Enrollment.AdmissionsCrm.ApproveConcession`, or `Students.Enrollment.AdmissionsCrm.OverridePaymentGates` according to the action |
| Activation | `Students.Enrollment.AdmissionsCrm.PreviewActivation`, `Students.Enrollment.AdmissionsCrm.RequestActivation`, `Students.Enrollment.AdmissionsCrm.ApproveActivation`, `Students.Enrollment.AdmissionsCrm.ExecuteActivation`, or `Students.Enrollment.AdmissionsCrm.RepairActivation` according to the action |
| Activation identifiers and handoffs | `Students.Enrollment.AdmissionsCrm.AssignManualIdentifier` and `Students.Enrollment.AdmissionsCrm.ManageServiceHandoffs` |
| Admission cancellation | `Students.Enrollment.AdmissionsCrm.CancelAdmission` |
| Configuration safety | `Students.Enrollment.AdmissionsCrm.ManageConfiguration` |

Standard and direct registration issuance also require `Students.Enrollment.AdmissionsCrm.WaiveRegistrationFee` whenever `waiveFee` is true.
Changing a protected pipeline stage with `overrideWorkflowGuards` requires both `Students.Enrollment.AdmissionsCrm.ChangeStage` and `Students.Enrollment.AdmissionsCrm.BulkOperations`, and the override is recorded in the applicant timeline.
Activation placement options and preview, request, approval, and execution paths apply the current user's academic scope.
When an applicant has `TargetStandardId`, activation placement must use that standard.

## Configuration safety contract

All configuration-safety routes require `Students.Enrollment.AdmissionsCrm.ManageConfiguration` and academic-year-wide user scope for every referenced year.
Lower academic scope rejects configuration safety with `AdmissionCrm:AcademicYearWideScopeRequired`.
Reference `kind` and `code` must be supplied together or the preview rejects with `AdmissionCrm:ConfigurationReferenceKindAndCodeRequiredTogether`.
Clone preview rejects identical source and target years with `AdmissionCrm:ConfigurationCloneDistinctYearsRequired`.
Clone confirm rejects a changed preview with `AdmissionCrm:ConfigurationCloneFingerprintMismatch`.
Clone confirm rejects a target with applicant activity or existing admission configuration with `AdmissionCrm:ConfigurationCloneTargetNotEmpty`.
Cycle and pipeline-stage updates require the exact `impactPreviewFingerprint` returned for their proposed payload and current configuration snapshot.
Missing or stale update evidence rejects with `AdmissionCrm:ConfigurationImpactPreviewRequired` or `AdmissionCrm:ConfigurationImpactFingerprintMismatch`.
Clone confirmation is serialized per tenant and target academic year, blocks unmapped source hierarchy identifiers, and remaps equivalent Board, Medium, Medium Stage, Standard, and Division identifiers.
Successful cloning copies cycle settings unlocked, remaps configuration and academic hierarchy IDs, recreates approval-controlled artifacts as drafts, resets counters and occupancy, shifts effective dates, and reuses global policy packs.

## Academic scope and journey lock

CRM lead lists and lead-backed reads apply the current user's Board, Medium, Stage, Standard, and Division scope through `TargetStandardId`.
A lead without `TargetStandardId` is visible only to tenant-wide or academic-year-wide users because it cannot be attributed safely to a lower scope.
Out-of-scope lead access rejects with `UserScope:AccessDenied`.
Academic-year-wide configuration mutations reject lower Board, Medium, Stage, Standard, or Division scope with `AdmissionCrm:AcademicYearWideScopeRequired`.
Tenant-global school-default mutations reject lower user scope with `AdmissionCrm:TenantWideScopeRequired`.
Retention-policy create, submit, and publish operations require academic-year-wide user scope.
After a journey is locked, cycle-structure and pipeline-structure mutations reject with `AdmissionCrm:JourneyLocked`.
Scheduled-report automation validates the tenant-owned academic year without requiring an interactive current user.

## Public Admissions CRM API

The public CRM route family is `api/public/student-enrollment/admissions-crm`.
Public routes are tenant-qualified until a school slug or domain resolver is added.
Public form submission stores consent and the complete submitted dynamic field payload.
Public form system-field mapping can populate optional applicant DOB and use it for internal duplicate review.
OTP challenge creation uses the applicant's consented SMS, WhatsApp, or email channel when a matching CRM communication provider is configured.
OTP challenge creation is rate-limited per status token, and repeated invalid verification attempts lock the current OTP challenge.
OTP delivery logs store a masked body snapshot instead of the raw OTP value.

| Route pattern | Purpose |
|---|---|
| `/{tenantId}/forms/{templateId}` | Read one active public inquiry form |
| `/{tenantId}/forms/{templateId}/submit` | Submit one public inquiry form and create a CRM lead |
| `/{tenantId}/status/{token}/otp` | Issue an applicant status OTP challenge and create provider delivery evidence when a consented channel is available |
| `/{tenantId}/status/{token}/verify` | Verify OTP and return applicant status, checklist, exam schedule, exam result, and eligibility details |
| `/{tenantId}/communication/delivery-logs/{deliveryLogId}/callback` | Reconcile a provider callback using direct callback fields or `callbackMappings` JSON paths in provider settings |
| `/{tenantId}/communication/{academicYearId}/{channel}/delivery-callback` | Reconcile a provider callback by provider message ID, academic year, and channel using direct callback fields or `callbackMappings` JSON paths |

## Contract rule

Do not add a parallel curated contract tree for Student.
Add endpoint summaries here when Student API docs need more route detail.
Do not restore generated exports under `docs/`.
