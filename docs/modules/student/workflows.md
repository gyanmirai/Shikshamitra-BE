---
module: student
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-09-25
owner: backend-team
audience: humans-and-agents
---

# Trace Student Enrollment Admissions workflows

TL;DR: setup commands produce validated offering defaults, while inquiry and application runtime commands consume only offerings that are open and ready.

## Reserve an identity and finalize admission

1. Create the inquiry/applicant and persist one backend-generated `ReservedStudentId`; do not create a Student yet.
2. Complete the existing application, review and offer workflow without changing the reservation.
3. After offer acceptance, run Finalize Admission with the current concurrency stamp, idempotency key, guardian confirmation and explicit duplicate resolution where required.
4. Reuse a valid linked/selected Student, or create a new Student with the applicant's reserved ID and current validated profile/guardian information.
5. Atomically link the applicant and application, hand off documents, create the onboarding case and record history.
6. Continue operational onboarding; finalization does not create the enrollment or assign placement.

A failed transaction leaves the reservation intact and no committed partial Student/onboarding handoff.
Refresh after a concurrency conflict before retrying; a reservation conflict must be investigated if it persists.
Later applications reuse an already linked Student and still obey existing same-year enrollment/onboarding restrictions.
Separate applicant records do not automatically become the same person merely because names match; retain the existing staff duplicate-resolution process.

## Operate an Admission Cycle

1. Create a Draft with name, unique code, Academic Year, open/close dates, and optional description.
2. Edit and save until ready. Drafts cannot accept inquiries or applications.
3. Run publication validation. Resolve every error; an expired application window must be extended
   before publication.
4. Publish immediately or schedule a future publish time.
5. Monitor lifecycle, live window, linked Offering count, and Application count independently.
6. If dates must change after publishing, revise the window with a reason. Existing Offering date
   overrides must remain inside it.
7. Close immediately or schedule closure. Before immediate closure, review impact counts, enter a
   reason, and explicitly acknowledge any active Offerings. Existing applications are retained.
8. Reopen only a Closed cycle. Enter a reason and extend the end date when the old window has ended.
9. Archive only after closure and after active Offerings are removed or deactivated. Delete only an
   unused Draft; otherwise retain the cycle as history.
10. Use View details and history to audit actors, timestamps, reasons, schedules, and transitions.

A scheduled publication is also rechecked when it becomes due. If its Academic Year is no longer
available or its application window has ended, the schedule is cancelled and the reason is added to
history for staff follow-up.

For annual reuse, clone the old cycle into a new Academic Year. The clone starts as Draft and does
not inherit Offering/Application records, preventing accidental exposure of last year's setup.

## Create or edit an offering

1. The controller authorizes the granular operation.
2. FluentValidation checks request shape and date presence.
3. `AdmissionOfferingConfigurationValidator` checks cycle, hierarchy, UserScope, dates, published references, and full Offering-identity uniqueness. The operational code is not used as an identity key. RTE offerings additionally require direct Registration entry and a workflow without Inquiry or Assessment stages; the other tracks retain configurable entry modes.
4. Active offerings require complete readiness; inactive offerings may save missing runtime references for later completion.
5. The command service persists through the aggregate and returns a scoped detail projection.
6. Update compares the concurrency stamp and locks academic identity after an application exists.

## Activate, deactivate, and delete

Activation reruns full readiness validation before changing state.
Deactivation preserves all history and immediately blocks new inquiries and applications.
Delete compares concurrency and succeeds only when no application references the offering.

## Clone and bulk setup

Clone copies dates, workflow, inquiry form, application form, and default active state from the source.
The caller supplies a new cycle and academic identity, and the backend revalidates uniqueness and scope.
Bulk preview validates each standard and returns structured row errors together with separate
`CanSaveInactive` and `CanActivate` decisions. Bulk commit inserts every row in one unit of work only
when every row is valid for the requested inactive or active state.

## Configure intake and assignment for new Offerings

1. Save reusable Inquiry and Counsellor Assignment profiles through the Admission Intake Profiles API. Inquiry settings control allowed sources, duplicates, deadlines, and the application-start requirement. Assignment settings specify an active, scoped staff pool, active-case capacity, and optional fallback manager.
2. Select both profiles on a new Inquiry or Both-mode Offering; select the assignment profile on a new Direct Registration Offering. New active Offerings require these references. Historical Offerings retain the pinned Inquiry-stage policy until they opt in.
3. At inquiry creation, the Offering supplies the policy and assignment pool. Assignment counts open inquiry, application, and onboarding cases, then chooses the least loaded counsellor below capacity. A database transaction lock serializes assignments for one profile. Direct Registration uses the same assignment when no owner is selected manually.
4. A staff-captured inquiry with no known Offering is tied to a published cycle and remains unassigned for triage. Staff confirm an open Offering later; its policy and assignment then apply. Linked inquiry and application ownership is kept together when the application owner changes, and scheduled follow-ups move with it. Finalization seeds onboarding ownership from the application.
5. An administrator may create a revocable public inquiry link for one published cycle and assignment profile, with a fixed source, optional campaign detail, and optional expiry. The raw link token is returned only on creation; only its SHA-256 hash is stored. The anonymous basic inquiry form records class interest in source detail and requires contact consent. Repeated phone submissions receive the same acknowledgement without creating another inquiry. Requests are limited per link in the HTTP host. The lead is assigned before the Offering is known, then staff confirm a suitable open Offering.

## Historical managed Inquiry

1. Existing published Inquiry-stage policies retain their application-start requirement.
2. Configure allowed sources, required source detail, duplicate behavior, identity requirements, counsellor assignment, deadlines, follow-up defaults, and qualification checklist as needed.
3. Validate and publish the workflow, then attach it to an offering whose forms and counsellor scope are compatible.
4. Create an inquiry; the server validates the selected source and required source detail, stores the preferred language, checks current-cycle duplicates, suggests same-mobile families from other cycles only when their applicant is visible in staff admission scope, snapshots policy and deadlines, assigns a counsellor when configured, and records duplicate-override context in audit activity.
5. Work from All, My inquiries, Unassigned, a specific counsellor, Overdue, Awaiting first contact, Awaiting qualification, or Open follow-ups. Operational queues are filtered before server paging, URL-backed clients can restore the same view, and each row carries its next-action and SLA summary.
6. Assign one inquiry or atomically assign up to 100 inquiries. Manual assignment lists only active staff who can access the inquiry's offering and rechecks that scope on save; default and round-robin workflow policies remain available for historical Offerings.
7. Edit identity, contact, source, source detail, or language before application start. The concurrency stamp rejects stale edits, and the activity timeline preserves the change.
8. Compose a preferred-language family communication from the Inquiry list or Pipeline. Staff with Communication Add permission can create a draft; staff with Communication Send permission can request immediate delivery. Existing Communication approval, provider, consent, tracking, retry, and usage rules stay authoritative.
9. Record structured contact channel, direction, and outcome; schedule, complete, or cancel follow-ups; then record the checklist-backed qualification outcome and standard closure reason where required.
10. Start an application only after the exact linked Inquiry satisfies its pinned start requirement.
11. Read the unified historical timeline after application start; mutable Inquiry details and management close when the inquiry reaches application-started or converted state.

Basic workflows retain the existing inquiry commands and do not create managed records.

When linking a sibling to an existing family, Inquiry and direct Registration both require an active family
visible through the staff member's admission scope and a matching primary-guardian mobile number.
An arbitrary family ID or a name/email-only duplicate suggestion does not authorize linking;
staff must verify or update a changed guardian number through a separate family-maintenance flow.

## Inquiry and application runtime

Inquiry creation checks cycle consistency and shared offering availability before validating inquiry form values. Create-time duplicate enforcement stays in the selected admission cycle. The duplicate precheck additionally suggests scoped prior-cycle families with the same guardian mobile for sibling linking; a name/email-only signal does not cross cycles.
Application start checks the same availability service, validates any linked inquiry, and freezes the offering's published workflow and application form references.
Later changes to offering defaults affect only future runtime records.

## Configure and publish a workflow

1. Create a draft identity and, optionally, its stages and routes in the same request. A selected template's published Registration form is attached to its Form stage. The workflow may also store a published Inquiry form as a default for future Offerings; it does not replace the Offering's own Inquiry form reference. Clone, new-version, and rollback operations preserve this default.
2. Add selectable stages from the server stage-type registry. Inquiry, Seat, Enrollment, and Custom remain registered for immutable historical workflows but are deprecated and not selectable for new stages. Appointment remains non-selectable until its runtime scheduling and completion flow is available. The Approval enum value is retained for compatibility and is presented to schools as **Admission decision**.
3. Mark exactly one entry stage and at least one terminal stage.
4. Add controlled conditional transitions and at most one default route per source stage. Each conditional route from a stage uses a unique non-negative priority; a route without a condition is always the single default route. Condition fields must use the server allowlist (`application`, `inquiry`, `assessment`, `documents`, `payment`, `eligibility`, `approval`, `offer`, `seat`, `enrollment`, or `appointment`).
   A required stage cannot enable ordinary manual skip. Stage types without a runtime-ready handler are publication errors, not advisory warnings.

Admission forms use the same controlled rule foundation for section visibility, field visibility, and conditional requiredness. The form runtime also supports canonical Applicant, Guardian, and Address bindings; these values remain domain-owned and are written back through the binding registry.

Every new form version carries an Inquiry or Registration purpose. Clone and new-version operations preserve it, and changing it is allowed only on a Draft. Earlier Correction and Consent values may remain on existing test records but cannot be used for new forms. Workflow publication requires a published Registration form on each Form stage and a published Inquiry form for any configured workflow default. Offering readiness rejects a purpose mismatch before activation.
5. Validate reachability, dead ends, duplicate routes, cycles, terminal reachability, published form references, and every enabled managed-stage policy. Form, Review, Payment, Offer, and Seat policies are parsed with the same domain validators used by runtime, so malformed or out-of-range settings cannot pass publication validation.
6. If school maker-checker governance is disabled, publish the validated version directly with a
   change reason. If it is enabled, submit it with a change reason and select a different active
   user who has the workflow `Approve` permission.
7. The assigned approver reviews validation, impact, and version comparison, then approves or
   requests changes. A request for changes records the reason and returns the version to Draft.
8. Publish an approved version immediately or set a future effective time. A scheduled worker
   publishes due versions; the previously current version becomes Superseded.
9. Create a new immutable version for later edits. Existing Offerings and Applications continue to
   use their pinned Published, Superseded, or Archived version.
10. For rollback, select a historical Published, Superseded, or Archived version and create a new
    Draft with a required reason. Validate and govern that Draft normally; never repoint existing
    runtime records silently.
11. Archive a Published or Superseded version only when the usage rule permits it. Use governance
    history and notifications to audit actors, decisions, reasons, and effective times.

Publication and Offering activation validate different scopes. Workflow publication owns graph, runtime-handler, managed-policy, and SLA/deadline-range validation. People assignments cannot be fully checked until the workflow is attached to an Offering with a concrete academic hierarchy. Activation therefore verifies the new Offering's assignment profile has eligible active staff in that hierarchy, and that a default Review assignee is active, has the configured role, and can access the same hierarchy. Historical Offerings using pinned Inquiry-stage assignment rules retain their existing compatibility checks. A reusable published workflow is not coupled to one campus, but an Offering with unusable staff assignments cannot become active.

Version-history reads include every version's complete stages and transitions plus Offering, distinct Campus, and Application usage counts. School-wide offerings with no Campus are reported as school-wide scope rather than being invented as a Campus. This lets authorized clients compare historical configuration and copy a selected immutable version into the next Draft without changing published versions or applications already pinned to them.

The permission-protected workflow Usage read derives runtime analytics from the selected version's workflow and stage instances. It reports started, active, completed, completion-rate, duration, stage throughput, skips, and oldest active timestamps without combining metrics across immutable versions.

The frontend presents routes under **What happens next?** on their source stage instead of exposing a separate technical Transitions workspace. The persisted transition graph remains authoritative: visual display order never determines the entry stage or runtime routing, and existing published definitions are not rewritten.

## Advance an application workflow

1. Application start freezes a published workflow version and creates stage instances from the explicit entry stage.
2. A transition command supplies the expected current stage, application concurrency stamp, and an idempotency key.
3. The stage handler verifies that the current stage can complete.
4. Conditional routes are evaluated by priority, followed by the default route.
5. Completion or an authorized skip updates the current stage and next stage in one unit of work. Required stages reject ordinary skip at runtime; only the separately permissioned emergency override with a reason can bypass them.
6. Reaching an explicit terminal stage completes the workflow instance.
7. Every transition creates immutable history and an Applicant 360 activity entry.
8. The response derives a deterministic next required action from the active stage type; completed workflows return no next action.

An **Admission decision** stage is the structured terminal decision point. Its retained enum value is `Approval` for API and database compatibility, but the stage can complete after an authorized Approved, Waitlisted, or Rejected decision. This lets negative decisions end at the same governed stage without adding fake Rejected or Waitlisted stages.

## Run a managed entrance assessment

1. Add an Assessment stage to a draft workflow, enable its assessment policy, and select result-only or scheduled operation.
2. Configure attendance, scoring, review, and attempts; add explicit routes for negative outcomes when those outcomes should leave the stage.
3. Validate and publish a new version, then attach it to an offering for future applications.
4. For scheduled operation, create shared future sessions for the offering and exact workflow stage.
5. While an applicant is in the assessment stage, create an attempt and reserve a matching session when required.
6. Record attendance and results after the session starts, or directly in result-only mode according to its attendance requirement.
7. If review is required, a different authorized reviewer approves the result or returns it for correction.
8. For failed or absent results, request an allowed retake and create another attempt, record an authorized waiver, or use an explicit configured outcome route.
9. Complete the stage through the existing workflow advance command; completion remains separate from recording evidence.

The assessment summary returns capability flags for each permitted action and explains missing evidence, pending review, future sessions, and missing outcome routes.
Changes after an assessment stage has completed are rejected; its attempts remain readable within tenant and academic scope.

## Verify applicant documents

1. Configure a managed Document Verification stage in a new workflow version, including applicability, collection mode, formats, limits, catalog mapping, independent review, and optional waivers.
2. Publish validated settings and use the new version for future applications. Existing applicants keep their pinned requirements.
3. Collect uploads at the active document stage, or during its explicitly linked earlier Form stage. Original-document observations remain available only in the active document stage. Each action creates a new evidence revision with its collector identities.
4. A permitted reviewer accepts complete evidence or requests replacement with a reason. Rejection is available before an original check; acceptance still requires every configured collection component.
5. Replace rejected evidence, record a fresh original check when required, and review the new revision. Earlier revisions and decisions remain available as history.
6. Resolve all mandatory and matched conditional requirements, or record a separately authorized policy-supported waiver with a reason. Missing condition inputs block continuation.
7. Complete the workflow stage explicitly. Normal completion, manual skip, and administrator override share the mandatory-evidence gate.
8. Finalize admission through the existing accepted-offer and completed-workflow process. Link canonical evidence to the Student in the finalization unit of work and continue operational onboarding.

Mapped accepted uploads may satisfy corresponding onboarding document uploads. Where an admission waiver, physical-only check, or custom document leaves a mandatory onboarding upload outstanding, authorized staff use the scoped active-onboarding document upload flow. This preserves the separate onboarding readiness rule while providing a route to complete it.

Class applicability uses the offering's authoritative `StandardId`, combined with any existing conditional rule.
Missing class context fails closed; an applicant's typed class answer cannot change applicability.
`standardIds: null` or omission means all classes; selected IDs must be nonempty, unique, and belong to the tenant at publication.
Early collection requires a configured `collectionFormStageCode`, an active matching Form stage, a pending document stage, a Draft or Submitted application, and a forward route in the same frozen workflow.
Only uploads are permitted early; original checks, review, waiver, and document-stage completion are rejected until that stage is active.
Early and later uploads use the same application/stage/requirement evidence history, and replacement still requires fresh acceptance.

## Check eligibility

1. Enable managed Eligibility in a draft workflow and add only the school's required criteria. Select published source forms or earlier managed source stages explicitly.
   Select those same application and inquiry form versions when configuring the Offering; incompatible selections cannot be activated or used for new applicants.
2. Validate and publish a new version. Existing applications retain their pinned policy and source stage identities.
3. At Eligibility, check the saved information. Review each criterion's expected value, actual value, source and explanation.
4. Correct unknown or invalid information at its source. If the application form must be reopened, resubmit it, then check eligibility again; resubmission does not automatically complete managed Eligibility.
5. For a genuine failing criterion, request an allowed exception with a reason. The configured authorized reviewer decides it independently when required; the original result remains in history.
6. Continue only with fresh evidence. Explicit negative-result routes may send an ineligible or incomplete application for review, while admission rejection remains a separate decision.
7. Recheck a visited Eligibility stage if source information changes later. First-time finalization verifies fresh passing or exception-approved evidence; successful finalization retries remain idempotent.

## Managed Review workflow

1. Publish a version 1 Review policy with any earlier managed prerequisites, reviewer/approver restrictions and checklist.
2. Enter Review; the server initializes the assignment/deadline work item without making an admission decision.
3. Read the current evidence and blockers, optionally assign an authorized reviewer, and save a draft or submit a reasoned recommendation.
4. An authorized approver uses the existing admission Approve, Waitlist or Reject command, or explicitly requests an application-form correction.
5. A correction reopens the form; save and resubmit remain at Review and require a new recommendation.
6. Complete Review normally after the actual decision; negative outcomes require an explicit matching route.
7. Before offering or finalization, check that the approved recommendation still matches current evidence; refresh the completed Review and reaffirm approval when a correction made it stale.

Changing an assignment or saving a newer draft supersedes an earlier recommendation until resubmitted.
Review history and actual decisions remain immutable, with the actor, time and safe prerequisite/checklist snapshots retained.
An overdue queue is available for scoped active Review stages; this delivery does not send deadline reminders.

## Run a managed Form stage

1. Enable Form management in a draft workflow, select the same published application form used by the Offering, and configure only the optional behavior the school needs.
2. Publish the workflow and activate a compatible Offering. New applications freeze both versions; existing applications keep their original configuration.
3. Save drafts when permitted, then submit through the existing dynamic form. Required-field, deadline, and attempt checks run on the server.
4. If verification is configured, authorized staff verify each selected section. Revoking verification requires a reason.
5. Request a correction with a reason when allowed. The form returns to draft and every previous verification becomes stale.
6. Resubmit and repeat verification. Continue only when the server reports no blockers; automatic advancement is used only when the policy permits it.

All actions retain immutable form submissions and change history. Managed Form settings require no new persistence table or database migration.

## Run a managed Payment stage

1. Add Payment to a draft workflow and choose **Managed collection with financial controls**.
2. Set the purpose, currency, amount, payer, staff methods, deadline, installment rules, second-person verification, and waiver policy.
3. Validate and publish the workflow. Online gateway choices block publishing until the tenant has a trusted gateway.
4. In the application, record received money. Non-cash methods require the bank or provider reference; the server generates the admission receipt number.
5. If independent verification is enabled, a different authorized staff member verifies or rejects the payment.
6. Continue after the full amount settles, or record an authorized full waiver with a reason. A reversal reopens the outstanding balance while the stage is active.

Transition rules may use only `payment.status`, `payment.settledAmount`, and `payment.outstandingAmount`.

## Run a managed Offer stage

1. Add Offer to a draft workflow and enable the managed offer process.
2. Choose unconditional or conditional content, terms and confirmations, validity and override limits, decline reasons, extension/reissue limits, allowed completion outcomes, and acceptance continuation.
3. Add explicit `offer.status` transitions for every enabled declined, expired, or withdrawn outcome. Acceptance may use the normal default path.
4. Validate and publish the workflow. Existing applications and basic Offer stages keep their pinned behavior.
5. At the active stage, issue the offer and optionally select an eligible Admission counsellor; an inquiry-linked application starts with its inquiry counsellor. Staff may extend or withdraw only when the server capability permits the action. Reissue may change the counsellor.
6. Record acceptance with the required confirmations, or record a decline reason. Expired, declined, and withdrawn offers may be reissued within the configured limit.
7. Continue only after the server reports an allowed completed outcome and safe route.

The application card shows the current version, type, expiry, conditions, assigned counsellor, blockers, available actions, and permanent event history. Direct Registration may select a counsellor at entry; authorized staff may later reassign an open application with an auditable note. The application queue supports a server-side assigned-counsellor filter. This workflow records staff-mediated responses; communication delivery and applicant self-service are separate integrations.

## Run a legacy managed Seat stage

Seat is not selectable in new workflow drafts. Published workflow versions and applications already pinned to a Seat stage continue to use the existing capacity, reservation, renewal, waitlist, exception, release, and completion policy without mutation.

For those historical journeys, capacity, expiry, and waitlist facts remain server-computed. Confirm, renew, release, promote, or adjust capacity only when the returned server capability permits it; every action records immutable history. Staff should refresh and retry after a concurrency conflict instead of overwriting a newer allocation decision.

Room-derived class capacity is configured by assigning Institute Setup rooms to a class from Admission Offering details. Its shared remaining-seat count is a warning only and does not reserve seats or implement a waitlist. Any future offering-level reservation and waitlist contract remains separate from this class-capacity display and from the historical managed Seat stage.
