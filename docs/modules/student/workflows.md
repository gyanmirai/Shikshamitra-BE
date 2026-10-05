---
module: student
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-10-03
owner: backend-team
audience: humans-and-agents
---

# Trace Student Enrollment Admissions workflows

TL;DR: setup commands produce validated offering defaults, while inquiry and application runtime commands consume only offerings that are open and ready.

## Configure and operate daily attendance

1. Use the existing academic hierarchy and published Academic Calendar; Attendance does not duplicate their definitions.
2. Save/preview/publish school defaults or an academic-year policy with scoped exceptions and an effective date.
3. Confirm formal enrollments' attendance-start dates and add individual recurring or temporary schedules when required.
4. Read the dated roster and its capabilities/revisions; show expected, not scheduled, approved leave and unconfirmed membership distinctly.
5. Mark Present/Absent, add optional details and save a partial draft if needed.
6. Submit only after all expected observations are known; the server pins membership and policy-credit snapshots.
7. Correct recorded observations with the configured reason/review flow; independent approval records immutable evidence and preserves day-lock state.
8. Read register, profile and dashboard metrics from the common calculation, including missing observations, provisional state and effective school target.

A working-day cutoff is a pending-completion reminder, not an automatic observation or lock.
Approved leave remains visible for explicit teacher marking; school leave policy determines credited denominator treatment.
Leave approved after submission does not change the stored percentage automatically. The teacher requests a leave correction, retaining Present/Absent if appropriate; the school rule determines audited direct application or independent approval. Until application, reports retain the submitted credits and the roster identifies the needed correction. Changed leave facts during review invalidate the request.
Records placement and exit/return operations supply dated enrollment membership so a later class change does not alter earlier attendance.

## Upgrade historical attendance

Apply the Attendance migration after `20261001021100_AddStudentRecordRenewalHistory`.
Unambiguous existing observations gain school-date/enrollment references; ambiguous same-date observations remain intact for review.
Valid enrolled students missing formal enrollment receive a marked legacy bridge with no invented attendance-start date.
Confirm membership before new daily marking.
The down migration permits an untouched-feature rollback but rejects removal once policy, schedules, operations, submitted snapshots or confirmed membership have been used; use a forward fix in that case.

## Operate a permanent student record

1. Admissions finalizes the real Student and initial onboarding case; Onboarding activates the first enrollment and completes required provisioning.
2. Directory opens the permanent profile. Eligible historical students need an explicitly approved override; duplicate flags keep separate identities pending review.
3. A record change captures its source evidence and published school policy. Direct policy applies with audit; Approval policy advances one to three role stages without self-approval. Effective dates and current authority are checked again on apply.
4. Approved guardian changes create the required access review and apply the school's captured SuspendUntilReview/KeepUntilReview choice. Guardian resolution verifies identity and the selected relationship.
5. Documents/IDs issue immutable student/template snapshots. Reprints reuse the issued number; duplicates and void actions preserve their own history and reasons. Leaving issuance references the matching approved/effective exit.

## Complete year-end and returns

| Step | Behavior |
|---|---|
| Preview | Read the source enrollment, validate target hierarchy/roll/capacity, validate school date range, and obtain selected result/Finance/Library/Transport evidence from owners. Check batch target roll duplicates and aggregate seats. |
| Submit | Pin policy, source placement/status, idempotency key, and ordered approval rule. ReadyOnly persists ready rows and recorded exceptions; HoldAll rejects a preview with unresolved rows. |
| Approve/apply | Persist each approval before applying. Recheck all approving actors' active identities, grants and database roles, scope, source evidence and owner checks. HoldAll waits for the batch. Apply/schedule is atomic per student. |
| Promote/repeat | DirectEnrollment creates the unique target-year enrollment and retires the previous active enrollment. ShortChecklist opens renewal first; old profile visibility remains intact. |
| Short renewal | Confirm the staff-audited guardian/contact/service review, complete captured Onboarding checks, and activate the target enrollment. Finance assigns the target-year obligations; the real confirmed first installment and required verified portal links permit completion. Portal provisioning follows enrollment and uses the Onboarding-owned renewal account handoff with current central Identity grants. |
| Exit | Graduate/Withdraw/Transfer retain distinct outcomes and reason/date history while projecting legacy Left and inactive enrollment at the configured roster timing. Hold records the decision without inventing next-year enrollment. |
| Return | Frozen Records Status approval governs all routes. ReuseProfile opens short renewal. Fresh follows configured Inquiry/DirectRegistration, requiring an explicit choice for Both and configured Inquiry evidence. Resume validates the eligible linked application's current stage. Fresh/Resume wait in AwaitingAdmission; finalization creates a tagged return Onboarding case, and reviewed activation reuses the original same-year enrollment or creates a legitimate later-year row. All routes preserve permanent identity. |
| Close an unsuccessful Admission return | Admissions rejection or preactivation Onboarding cancellation closes only the matching linked return as Cancelled and records the owner's actor and reason atomically. It preserves all prior history and enrollment. A new request goes through the school's Records review again; an old-key retry replays the closed request. Offer decline/withdrawal and reopenable Inquiry outcomes retain their existing lifecycle. |
| Correct | Preview affected source/target evidence and downstream dependencies; submit a separate governed correction. Original remains Applied. Finance obligations, changed snapshots, dependent attendance or document issuance block reversal. |

Annual and returning-student approvals use only the frozen Records Placement/Status rule. Onboarding continues to own target safety, numbering configuration, readiness, portal accounts and Finance handoffs; its initial admission approval chain remains unchanged.

Numbering follows the target-year Onboarding school setting: manual, automatic, or explicit staff choice when both are permitted. Preview proposes automatic roll numbers in the configured incoming-cohort order, accounting for current enrollments and pending Onboarding, Records transitions and renewals. Submission checks the proposal again inside a serializable transaction and freezes the accepted number. It never renumbers the existing roster. Rules requiring roster recalculation, replacement of existing numbers, first-academic-day assignment or a separate manual-bulk numbering workflow block this command explicitly instead of pretending those workflows have run.

The permanent profile stays visible during a pending renewal. Before activation, the requester may cancel a mistaken target with a reason if the case is PendingChecklist and has no new target enrollment or new-year Finance dependency. Cancellation preserves the old case and its linked transition; a replacement receives a new review case. Provisioning cannot be cancelled. School-permitted governed division/roll edits can synchronize the active renewal, but its target class is fixed: there is currently no automated wrong-class recovery while Provisioning. Transition correction becomes available only after Applied and still blocks financial/downstream dependencies; staff must not treat completion as a promise that financial obligations can be reversed. Repeated same-year withdrawals and returns retain every completed review, reopen the same enrollment row, and preserve historical GR, payments, attendance and document relationships.

The schedulers poll every minute. Transition dates use the Admissions institution-date helper; change timestamps use the same application clock as their due selector. A case becomes eligible at its effective date/time and applies on a due worker tick. Failed transitions retain a code and become Exception; staff resolves the source issue and uses Apply to retry. Failed changes become Conflict and require a reviewed replacement request. Policy republishing affects new requests, while live authority and privacy rules are rechecked for existing requests and report download.

## Reserve an identity and finalize admission

Admission finalization links a Student and onboarding case, without operational enrollment.
Saved year-specific onboarding configuration takes precedence over school configuration; the matching admission route, optional campus, and academic path select readiness and numbering rules. A campus-specific rule matches the campus captured from the finalized Admission Offering; a rule without campus targeting continues to cover all campuses. Older cases without a campus snapshot use their linked Offering when available, without writing a snapshot merely by reading the case; this preserves the returned concurrency stamp. A case with no campus cannot match a campus-specific rule.
Schools may name additional onboarding routes in readiness rules. Authorized staff can switch an open case to a route saved in its effective academic-year configuration; the four built-in routes remain valid for existing cases. Changing a route resets approval review and refreshes readiness. A custom route must have a readiness rule matching the case's academic path, including campus when targeted; if that rule is removed later, readiness refresh and activation fail closed until staff restore coverage or choose another route.
Staff refresh evidence, reserve placement and identifiers, and record any required approval.
Activation creates one enrollment and enters Provisioning (6), rather than completing the case.
Finance assignment uses that enrollment and remains the only writer of dues and receipts.
Completion requires a canonical assignment, settlement of the earliest due-date installment group, and sufficient confirmed payment-allocation evidence for its paid amount.
Later installments may remain unpaid; zero-payable or fully credited obligations do not require artificial payment.
Required student/guardian identities are created passwordless and linked to the student by authorized staff.
Their owners use the existing Account password-reset flow; successful first password setup using a valid protected token also verifies the email for linked portal-only identities.
The self-service linked-student endpoint requires an active, email-confirmed identity and returns only that user's saved tenant-local links.
If a saved onboarding policy requires service consent, authorized staff first store the family's signed consent in the school's records, then enter its reference in Student Setup after enrollment. The scoped confirmation is audited and acknowledges the consent handoff; it cannot substitute for the signed record, fee settlement, or required portal links.
Readiness refresh rechecks Finance and account links, completing the case only when all required handoffs are acknowledged.
In Student Setup, **Recheck payment and onboarding** runs that backend readiness refresh after Finance records the first installment; reloading the case alone does not complete provisioning.
Completed historical cases are not reopened by this process.

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

1. An Admission Head or administrator creates one reusable Admission Intake Setup. The setup combines allowed inquiry sources, deadlines, and follow-up defaults with Counsellor routing rules; its fallback manager is required. Duplicate handling uses automatic detection with a reason for a separate family, and application start does not require contact or qualification. Source detail, guardian email, and applicant date of birth have no policy-level requirement. Published form validation still applies. Counsellors do not configure these rules and only work the inquiries assigned to them.
2. Select the setup on a new Inquiry or Both-mode Offering. The system stores both underlying profile references. For Direct Registration, it stores only the setup's assignment-profile reference. New active Offerings require the applicable references. Historical Offerings keep their existing references until an administrator explicitly replaces them.
3. At inquiry creation, the Offering supplies the policy and assignment pool. Assignment counts open inquiry, application, and onboarding cases, then chooses the least-loaded counsellor below capacity. A database transaction lock serializes assignments for one profile. Direct Registration uses the same assignment when no owner is selected manually; the mandatory fallback receives the case only when every configured counsellor is at capacity.
4. A staff-captured inquiry with no known Offering is tied to a published cycle and remains unassigned for triage. Staff confirm an open Offering later; its current setup policy and assignment then apply. Linked inquiry and application ownership is kept together when the application owner changes, and scheduled follow-ups move with it. Finalization seeds onboarding ownership from the application.
5. An administrator may create a revocable public inquiry link for one published cycle and Admission Intake Setup, with a fixed source, optional campaign detail, and optional expiry. The link initially uses the setup's assignment profile; the confirmed Offering supplies its Inquiry policy later. The raw link token is returned only on creation; only its SHA-256 hash is stored. The anonymous basic inquiry form records class interest in source detail and requires contact consent. Repeated phone submissions receive the same acknowledgement without creating another inquiry. Requests are limited per link in the HTTP host. The lead is assigned before the Offering is known, then staff confirm a suitable open Offering.
   Link creation requires an active Offering in the selected cycle with Inquiry or Both entry mode, at least one active pool counsellor with academic access to such an Offering, and an eligible fallback manager for a managed setup. Missing Offering, counsellor access, fallback access, and past expiry return distinct `Admissions.PublicInquiry.OfferingRequired`, `CounsellorUnavailable`, `FallbackUnavailable`, and `ExpiryInvalid` errors with corrective guidance. A rejection does not create a link; staff may correct the selection and retry.
6. Editing a setup changes the shared rules for future processing wherever it is selected. Archiving is allowed only when no active Offering or active unexpired public link still uses it. Existing cases keep their persisted snapshots and ownership history.

## Historical managed Inquiry

1. Existing published Inquiry-stage policies and stored snapshots retain historical JSON; effective reads normalize retired duplicate, application-start, source-detail, email, and date-of-birth settings to the same standard behavior as new intake setups.
2. Allowed sources, counsellor assignment, deadlines, follow-up defaults, and qualification checklist continue to apply. The current stage editor does not create new Inquiry stages.
3. Validate and publish the workflow, then attach it to an offering whose forms and counsellor scope are compatible.
4. Create an inquiry; the server validates the selected source and published form, accepts optional source detail (including QR-code and school-fair sources), stores the preferred language, checks current-cycle duplicates, suggests same-mobile families from other cycles only when their applicant is visible in staff admission scope, snapshots policy and deadlines, assigns a counsellor when configured, and records duplicate-override context in audit activity. A separate family on an actual duplicate requires a reason in both basic and managed flows.
5. Work from All, My inquiries, Unassigned, a specific counsellor, Overdue, Awaiting first contact, Awaiting qualification, or Open follow-ups. Operational queues are filtered before server paging, URL-backed clients can restore the same view, and each row carries its next-action and SLA summary.
6. Assign one inquiry or atomically assign up to 100 inquiries. Manual assignment lists only active staff who can access the inquiry's offering and rechecks that scope on save; default and round-robin workflow policies remain available for historical Offerings.
7. Edit identity, contact, source, source detail, or language before application start. The concurrency stamp rejects stale edits, and the activity timeline preserves the change.
8. Compose a preferred-language family communication from the Inquiry list or Pipeline. Staff with Communication Add permission can create a draft; staff with Communication Send permission can request immediate delivery. Existing Communication approval, provider, consent, tracking, retry, and usage rules stay authoritative.
9. Record structured contact channel, direction, and outcome; schedule, complete, or cancel follow-ups; then record the checklist-backed qualification outcome and standard closure reason where required.
10. Start an application after the exact linked Inquiry is saved and its open Offering and staff scope are valid. Contact and qualification remain optional CRM actions and do not gate start or historical Inquiry-stage completion.
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
3. Mark exactly one entry stage and at least one terminal stage. Schools may choose any stage type as an ending, including Form and Assessment. A single stage may be both entry and ending. Ending stages have no outgoing routes; their configured completion requirements still apply.
4. Add controlled conditional transitions and at most one default route per source stage. Each conditional route from a stage uses a unique non-negative priority; a route without a condition is always the single default route. Condition fields must use the server allowlist (`application`, `inquiry`, `assessment`, `documents`, `payment`, `eligibility`, `approval`, `offer`, `seat`, `enrollment`, or `appointment`).
   A required stage cannot enable ordinary manual skip. Stage types without a runtime-ready handler are publication errors, not advisory warnings.

Admission forms use the same controlled rule foundation for section visibility, field visibility, and conditional requiredness. The form runtime supports 100 canonical Student field bindings: 15 original Applicant, Guardian, and Address bindings plus 85 additional profile, family, address, previous-school, health, eligibility, identity, and admission-preference bindings. Financial assistance fields are excluded from the expanded catalogue. The original bindings update admission identity and contact properties. Additional bindings persist on the applicant, prefill registration after inquiry, and are copied to the Student record's captured admission fields during finalization. Staff can review those captured values on the onboarding case detail; they are not automatically treated as verified SIS profile values. Clearing an additional answer removes its applicant value before finalization.

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
