---
module: student
domain: product
content_type: Reference
status: canonical
updated_on: 2026-09-16
owner: backend-team
audience: humans-and-agents
---

# Understand Student Enrollment Admissions behavior

TL;DR: an Admission Offering is the tenant-scoped, auditable configuration that connects one cycle and academic hierarchy to published workflow and form versions.

## Admission Cycle governance

An Admission Cycle is the school-controlled season for accepting applications, for example
`Fresh Admission 2027-28`. It belongs to one Academic Year but its application dates may begin
before that year starts. This supports schools that open admissions during the preceding term.

Lifecycle and time window are deliberately separate:

| Lifecycle | Meaning |
|---|---|
| `Draft` | Editable setup; never accepts applications |
| `Published` | Approved for use; accepts applications only while the date window is `Open` |
| `Closed` | Manually or automatically stopped; can be reopened with a reason and valid end date |
| `Archived` | Retained historical configuration; cannot be reopened or edited |

| Window | Meaning |
|---|---|
| `Upcoming` | Institution-local date is before the open date |
| `Open` | Institution-local date is inclusively between open and close dates |
| `Ended` | Close date has passed, even if lifecycle remains Published |
| `Closed` / `Archived` | Lifecycle overrides the calendar window |

Cycle codes are tenant-unique and normalized to uppercase. The close date must be after the open
date. Draft details are editable; after publication, only the controlled window-revision command
can change dates, and all linked Offering overrides must still fit inside the revised window.

Publishing performs a readiness check. Closing and archiving require a reason and show dependency
counts. Closing a cycle with enabled Offerings requires explicit acknowledgement; existing
applications remain available but new inquiries/applications stop. Archiving is allowed only after
closure and only when no Offering remains active. Permanent deletion is restricted to an unused
Draft with no Offering or Application references. Every create, update, publish, close, reopen,
archive, clone, and schedule change appends tenant-scoped history. Concurrency stamps reject stale
commands.

Schools may schedule future publication and closure. A background worker evaluates due actions in
UTC every minute; business availability continues to use the institution date (`Asia/Kolkata` /
India Standard Time). If the Academic Year is removed or the application window expires before a
scheduled publication, the worker cancels that schedule and records the reason instead of exposing
an invalid cycle. Cloning creates a new Draft and never copies Offerings or Applications.

Admission Cycle uses exactly three assignable permissions: `View` for lists/details/impact/history,
`Add` for create/edit/publish/reopen/clone/window/schedule, and `Delete` for close/archive/permanent
draft deletion.

## Workflow Builder governance

Each school can choose direct publication or maker-checker governance. With maker-checker disabled,
an authorized workflow editor can validate and publish directly. With it enabled, a workflow moves
through `Draft`, `Validated`, `Submitted for approval`, `Approved`, then either `Scheduled` or
`Published`. The maker selects a different active user as checker and provides a change reason.
Only the assigned checker with the workflow `Approve` permission can approve or request changes.
A requested change returns the version to Draft while preserving the decision in audit history.

Every governance action records the actor, time, status transition, reason, assigned approver, and
effective time where applicable. Schools can enable in-app and email notifications independently.
Published versions become `Superseded` when a later version is published, but remain valid for
Offerings and Applications already pinned to them. A rollback never mutates a live historical
version: an authorized user selects Published, Superseded, or Archived history and creates a new
Draft with the old configuration and a required rollback reason.

Validated workflows may be edited; the first saved change returns them to Draft and clears the old
validation evidence. Submitted, Approved, Scheduled, Published, Superseded, and Archived versions
remain read-only.

## Offering identity

The active identity is Admission Cycle, optional Campus, Board, Medium, Stage, and Standard.
Division is not part of Admission Offering identity.
The database prevents duplicate non-deleted identities with separate filtered indexes for Campus and no-Campus records.
The Offering code is a normalized operational label, not a second identity key. It may repeat for
different Campus or academic-hierarchy combinations, and runtime APIs never resolve an Offering by
code alone. This keeps annual and cross-campus naming practical while the full identity remains
unambiguous.
Campus is backed by the tenant-scoped Admission Campus source. Campus codes are unique per tenant,
and only active campuses can be selected for an offering. Deactivating a campus does not delete
history, but offerings that reference it become configuration-incomplete until an active campus is
selected or the campus is cleared.

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

## Managed Inquiry stages

Inquiry stages can opt into version 1 of the `inquiry` policy inside `ConfigurationJson`.
Missing configuration or `enabled: false` preserves the existing Basic inquiry and application-start behavior.
Managed Inquiry can require contact or qualification before application start, constrain source capture, require guardian email or applicant date of birth, configure duplicate handling, and assign offering-scoped active counsellors manually or round robin.
First-contact and qualification deadlines are snapshotted when the inquiry is created.
Follow-ups retain assignment, purpose, due time, completion or cancellation state, actor, and audit timestamps.
Inquiry staff can create a linked family communication for WhatsApp, SMS, or Email. The CRM records the campaign handoff in admission activity while the shared Communication module owns approval, consent, providers, delivery attempts, retries, and usage; no duplicate Admissions messaging store is introduced.
Qualification uses a versioned checklist and records Qualified, Nurture, Not interested, Lost, or Duplicate with a reason where required.
Application start rechecks the linked inquiry, offering, scope, contact, outcome, and policy requirement on the server.
Existing inquiries and workflows remain compatible; management is created only for an enabled policy attached to the selected offering's published workflow.

## Managed assessment stages

Assessment stages can opt into version 1 of the `assessment` policy inside `ConfigurationJson`.
Missing configuration or `enabled: false` preserves existing manual assessment behavior, including published historical workflows.
Managed assessments support result-only and scheduled-session modes, optional attendance evidence, aggregate marks/pass thresholds, independent result review, bounded retakes, and authorized waivers.
The policy is pinned by the application's immutable workflow version and copied onto every attempt.

Shared sessions belong to an offering and workflow stage, with future dates, a location, and capacity.
Each applicant has independent attempts, attendance, marks, result, reviewer, and audit activities.
Booking uses both application and session concurrency protection; cancelled unstarted bookings release capacity.
Session identity cannot move to another offering or stage, and a session with bookings cannot be cancelled.

When enabled, attendance is required before recording a result.
Scheduled sessions must have started before staff can record attendance or results.
Scored outcomes are calculated by the backend; unscored results require an explicit passed or failed outcome.
Required review must come from a different authorized staff member than the result recorder.
A rejected review returns the attempt for correction, including attendance correction.
Retakes require a completed failed or absent result and remaining attempts; cancellation of an unstarted booking does not consume an attempt.

An assessment cannot normally advance without completed evidence or a recorded waiver.
Failed, absent, and retake outcomes additionally require an explicitly matching, non-default `assessment.outcome` route, preventing accidental use of a default success route.
Results and reviews do not advance the workflow automatically.
Existing separately authorized workflow skip and override controls remain available with their existing audit and permission requirements.

Assessment does not approve admission or create a Student.
Admissions finalization still requires an accepted offer and completed admissions workflow, and creates or links the Student plus a Student Onboarding case.
Operational StudentEnrollment creation remains part of onboarding activation.
Subject-level marks, public applicant self-booking, and external examination provider integrations are not included in this assessment policy version.

## Managed document verification

Document Verification opts into `ConfigurationJson.documentVerification` version 1. Missing or disabled policy retains legacy manual behavior. Published workflow versions pin the requirements for existing applicants. Each requirement has a stable code, required/optional/conditional applicability, upload/original/either/both collection, safe PDF/JPEG/PNG formats, a 1 to 20 MB limit, and optional tenant document-type mapping. Mapped uploads satisfy both policy and catalog constraints.

Evidence is owned by the application and runtime stage. Every upload, replacement, original observation, and waiver adds an immutable revision. A replacement resets original observation and review; recording an original can retain the previous upload with its original uploader identity. Accept/reject reviews are immutable and apply only to the latest unreviewed revision. File validity alone never means acceptance. Independent review excludes the revision submitter, retained file uploader, and original observer. Waivers require policy support, a distinct permission, and a recorded reason.

Conditional requirements use saved application answers, linked inquiry snapshot answers, and assessment facts. Missing referenced answers block completion; an explicitly saved blank answer supports `IsEmpty`. Application status and reserved inquiry/assessment/document facts come from server records. Required and matched conditional requirements block normal advance, skip, and administrator override until accepted collection evidence or an authorized waiver exists. Optional evidence does not block completion. Finalization rechecks visited managed document stages before creating a Student; successful finalized retries preserve their existing outcome.

Accepted and historical admissions evidence remains canonical after finalization and is linked to the same Student without overwriting Student documents. Only latest accepted, mapped file evidence meeting its collection mode can satisfy a matching onboarding upload requirement. A waiver, original-only observation, or unmapped custom requirement remains visible but does not silently satisfy an independently mandatory onboarding upload.

## Managed eligibility

Eligibility opts into `ConfigurationJson.eligibility` version 1. Missing or disabled settings retain basic completion. All configured criteria must pass; each answer criterion can combine bounded ALL/ANY rules. Schools select fixed age cutoffs, published application or inquiry answer fields, an explicit earlier managed assessment stage, or an explicit earlier managed document stage. Stage codes remain stable when a workflow version is cloned.

Application and inquiry criteria must use the exact form versions selected on the Offering. The runtime has no workflow-stage form fallback. Offering create/update rejects incompatible selections; missing selections may remain in an inactive draft but block activation. Availability checks and new inquiry/application creation also reject incompatible managed setup. Existing applications keep their pinned sources.

Age uses the canonical applicant DOB, or the current linked Student DOB, as a calendar date. Minimum and maximum ages count completed calendar months; maximum 72 permits 72 completed months and fails at 73. Leap-day and month-end anniversaries follow calendar month clamping. Missing, future, invalid, or incompatible source values need correction. A saved blank is distinct from an unavailable answer: explicit `IsEmpty`/`IsNotEmpty` evaluates it, while other blank comparisons remain unknown. Numeric equality is decimal equality, and multiple-choice membership compares whole choices.

Checking eligibility records an immutable evaluation with criterion explanations and a fingerprint of only the declared source information and policy. Changed DOB, source form answers, selected assessment evidence, or selected document revisions invalidate the prior result. Routine admission status changes do not invalidate criteria that never reference that status. Staff can recheck visited completed Eligibility stages before finalization. Resubmitting a corrected form at the managed Eligibility stage preserves that stage for an explicit check and continuation.

An optional exception requires a fresh failing evaluation, a recorded reason, separate request/review permissions, any configured real school role, and an independent reviewer when enabled. Requests and decisions are immutable; approval preserves the original failing evaluation. Unknown information cannot be waived through an eligibility exception. New evaluations or changed facts invalidate earlier exception authority.

Normal completion, skip, and administrator override all require a fresh evaluation. Eligible results and approved exceptions can follow ordinary routes. Ineligible or unknown results can leave only through an explicit matching `eligibility.status` route; this never rejects the admission automatically. Initial finalization rechecks every visited managed Eligibility stage and requires an eligible result or approved exception. Successfully finalized admissions retain their original outcome and read-only evaluation history on retries.

New inquiry snapshots record their source form ID in existing extra properties. Older snapshots without confirmed form provenance remain readable but produce unknown eligibility answer criteria; the evaluator never guesses their source from a subsequently changed offering.

## Managed Review

Review stage type 6 supports an opt-in `configurationJson.review` version 1 policy; missing or disabled policy retains the established stage behavior.
One managed Review owns the application's decision in this delivery.
Schools may select a reviewer role, a named reviewer, a due interval, required or optional checklist items, selected earlier managed Assessment/Document/Eligibility stages, and an independent approver or approver role.
Offering create, update and activation validate that a default named reviewer is active, belongs to the configured role and has exact access to that offering's hierarchy.
The same validation protects starting new applications; if an assigned reviewer later loses access, an authorized assignment change is the recovery for existing applications.
Selected prerequisite stages must occur on every path to Review in the pinned workflow.

Draft saves, assignment changes and submitted recommendations append immutable history.
Submitting requires all configured required confirmations, a recommendation and a reason, but never silently approves, rejects or waitlists an application.
Existing admission decision commands remain authoritative and additionally enforce the Review permission, role, reviewer separation and fresh submitted evidence.
Approval requires passed prerequisites; an explicit negative decision can use known adverse evidence but cannot use missing or stale evidence.
Only an explicit matching negative route can advance a waitlisted or rejected application; a default route never implies that decision.

Canonical applicant identity, current application form submissions and exact selected prerequisite evidence determine freshness.
Corrections invalidate earlier recommendations even when the application concurrency stamp did not change.
An authorized correction request reopens the application form, and resubmission remains at Review until staff submit a new recommendation and record a new decision.
Completed Review stages can be refreshed before finalization, including reaffirming approval after offer acceptance without resetting the admission lifecycle.
Generic skip requests are rejected for managed Review; administrative override cannot substitute for a recorded fresh decision.
Offer, offer acceptance and initial finalization require fresh approved Review evidence, while a successful finalized retry remains idempotent.
Committee voting, delegation, reminders and reopening historical Assessment or Document stages are outside this delivery.

## Managed Form stages

Form stages opt into `configurationJson.form` version 1. Schools can choose applicant, guardian, staff, or shared responsibility; save and resume; an optional 1-365 day deadline; late-submission behavior; automatic advancement; 1-20 submission attempts; correction requests; and verification of selected published form sections. Missing or disabled settings retain the established Form stage.

## Managed Payment stages

Payment stages opt into `configurationJson.payment` version 1. Schools configure a named purpose, ISO currency, required amount, payer, staff collection methods, optional deadline, late-payment behavior, installments, independent verification, authorized full waivers, and continuation after settlement. Missing or disabled settings retain basic manual stage behavior.

The workflow cannot skip a managed Payment stage. It completes only after the configured amount is settled or a full waiver is recorded with a reason. Pending verification reserves its amount so staff cannot collect the same balance twice. Overpayments, disallowed methods, stale writes, same-user verification, duplicate retries with changed payloads, and unreasoned rejection, waiver, or reversal are rejected.

Admission payment rows remain Finance-owned audited records while the Admissions module owns stage orchestration. Online gateway collection stays unavailable until a trusted tenant gateway is configured; a draft that selects `onlineGateway` cannot be published.

## Managed Offer stages

Offer stages opt into `configurationJson.offer` version 1. Schools can configure unconditional or conditional content, terms, acceptance evidence, default and override validity, decline reasons, extension and reissue limits, completion outcomes, and automatic continuation after acceptance. Missing or disabled settings preserve the established admission decision and offer behavior.

The existing Admission Application remains the single source for current offer status, issue and response timestamps, and expiry. Every managed issue, acceptance, decline, extension, withdrawal, and reissue appends immutable decision history with the stage, actor, version, reason, safe metadata, and idempotency key. Commands use optimistic concurrency, and a matching retry returns the existing result without creating a second event.

Managed Offer cannot be skipped. It completes only with a policy-allowed accepted, declined, expired, or withdrawn outcome. Acceptance enforces configured terms and conditional evidence. Declined, expired, and withdrawn outcomes require an explicit matching `offer.status` route so they cannot follow the acceptance path accidentally. This staff runtime records the response; applicant portal delivery, email/SMS delivery, and automated reminders require a separate notification integration.

## Managed Seat stages

Seat stages opt into `configurationJson.seat` version 1. Schools set the offering capacity, choose manual reservation or reservation on stage entry, define hold duration and staff override limits, enable bounded renewals and FIFO waitlisting, control authorized capacity exceptions, require release reasons, and choose which outcomes may complete the stage. Missing or disabled settings preserve the basic workflow step.

One tenant-scoped pool owns reserved and confirmed counts for each Admission Offering. Each application and frozen Seat stage owns at most one reservation record. Confirmed seats remain allocated after stage completion until admission finalization or an authorized reasoned release. Expired holds stop consuming effective capacity and are reconciled into permanent history on the next Seat command.

Managed Seat cannot be skipped. Confirmation is always an allowed completion outcome; waitlisted, released, or expired completion requires both policy permission and an explicit matching `seat.status` transition. Every mutation checks application, pool, and reservation concurrency, and repeats with the same idempotency key return the recorded result.

The Form-stage definition must equal the Offering application form. Applications still pin the published definition and record immutable submission snapshots. Managed submit enforces visible required fields, deadline, and attempt limits. Automatic advancement occurs only when configured and no section verification remains. Staff verification and reasoned revocation are audited. A correction request reopens the same form and invalidates all earlier section verification; the applicant must resubmit within the remaining attempt limit. Normal completion cannot bypass unresolved Form blockers.
