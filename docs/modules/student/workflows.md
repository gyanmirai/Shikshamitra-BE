---
module: student
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-10-09
owner: backend-team
audience: humans-and-agents
---

# Trace Student Enrollment Admissions workflows

TL;DR: setup commands produce validated offering defaults, while inquiry and application runtime commands consume only offerings that are open and ready.

## Carry admission answers into student profiles

1. Choose predefined fields from the admission catalogue. Photo, student Aadhaar, optional PAN, single-child/single-parent conditions, sibling students and permanent country use stable bindings; school-specific answers remain captured admission details.
2. Save and reload answers on the applicant. Photo accepts a JPEG/PNG data URI up to 2 MiB with file-signature validation. Sibling choices search the current staff scope; saved GUIDs are validated against the full scoped query and cannot include the resolved Student itself.
3. Finalization creates a new Student and maps supported family, health, previous-school, contact and identification details while preserving unsupplied values. Aadhaar uses encrypted Identity Compliance storage; the Student photo uses the existing blob store. Raw identifiers and image payloads are not duplicated into Student captured-detail extras.
4. For an existing Admission/Registration Student, staff explicitly confirms applying the previewed answers. Without confirmation, canonical profile details remain unchanged.
5. For an Enrolled/Inactive/Left Student, confirmation submits section change requests through Student Records permissions, masks, snapshots and school approval policy. Requests join the finalization transaction; direct-policy changes apply through the Records worker after commit. Aadhaar, APAAR, PEN and UDISE require a separate Identity Compliance review.

The applicant retains original captured evidence. Finalization retries preserve the existing Student, onboarding case and review requests. Admissions finalization does not activate enrollment; Onboarding retains that responsibility.

## Deliver student and parent credentials

1. Configure the applicable readiness rule's account/email flags, sending mode, timing and one guardian relationship.
2. Finalize Admissions and complete placement, identifiers and required document evidence; authorized staff may record document-specific overrides with a note.
3. Activate enrollment; automatic mode prepares eligible identities using current identity grants, while manual mode retains explicit staff verification/linking.
4. Activation timing may queue immediately; completion timing waits for existing Finance and required handoffs, without waiting for email delivery to complete onboarding.
5. Queue a durable request; after commit the worker rechecks actor scope, exact grants, active enrollment, mailbox and documents before preparing/sending credentials.
6. The owner uses the secure setup link to confirm email and choose their password. An existing verified Parent links another child without password changes.
7. Failed/interrupted delivery remains visible for controlled retry; Sent records sender acceptance. Repeated access emails do not reveal an existing password.
8. Records terminal application unlinks the child immediately; eligible siblings remain available to the Parent. A reviewed correction checks exact recorded links before restoration.

## Configure and operate daily attendance

1. Use the existing academic hierarchy and published Academic Calendar; Attendance does not duplicate their definitions.
2. Manage the available student leave types in Attendance Settings; attendance calculation uses fixed defaults.
3. Keep historical attendance-start dates in their owning student records workflow. An active student in today's class can be marked before that date is confirmed.
4. Read the dated roster and its capabilities/revisions; show expected, approved leave and unconfirmed membership distinctly.
5. Mark Full day, Half day, Absent or Pending, add optional details and save a partial draft if needed. The current school date can be marked while calendar setup is missing; published closures still exclude marking.
6. Submit only after all expected observations are known; the server pins membership and policy-credit snapshots.
7. Correct recorded observations with the fixed reason/review flow; independent approval records immutable evidence and preserves day-lock state.
8. Read register, profile and dashboard metrics from the common calculation, including missing observations, provisional state and the 75% school target.

A working-day cutoff is a pending-completion reminder, not an automatic observation or lock.
Approved leave remains visible for explicit teacher marking; the fixed leave rule determines credited denominator treatment.
Leave approved after submission does not change the stored percentage automatically. The teacher requests a leave correction, retaining Present/Absent if appropriate; the fixed rule determines audited direct application or independent approval. Until application, reports retain the submitted credits and the roster identifies the needed correction. Changed leave facts during review invalidate the request.
Records placement and exit/return operations supply dated enrollment membership so a later class change does not alter earlier attendance.

## Upgrade historical attendance

Apply the Attendance migration after `20261001021100_AddStudentRecordRenewalHistory`.
Unambiguous existing observations gain school-date/enrollment references; ambiguous same-date observations remain intact for review.
Valid enrolled students missing formal enrollment receive a marked legacy bridge with no invented attendance-start date.
Set attendance start dates through the owning Student Records workflow before new daily marking.
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
| Close an unsuccessful Admission return | Admissions rejection, explicit End Journey withdrawal, or preactivation Onboarding cancellation closes only the matching linked return as Cancelled and records the owner's actor and reason atomically. It preserves all prior history and enrollment. A new request goes through the school's Records review again; an old-key retry replays the closed request. Legacy offer decline/withdrawal and reopenable Inquiry outcomes retain their existing lifecycle. |
| Correct | Preview affected source/target evidence and downstream dependencies; submit a separate governed correction. Original remains Applied. Finance obligations, changed snapshots, dependent attendance or document issuance block reversal. |

Annual and returning-student approvals use only the frozen Records Placement/Status rule. Onboarding continues to own target safety, numbering configuration, readiness, portal accounts and Finance handoffs; its initial admission approval chain remains unchanged.

Numbering follows the target-year Onboarding school setting: manual, automatic, or explicit staff choice when both are permitted. Preview proposes automatic roll numbers in the configured incoming-cohort order, accounting for current enrollments and pending Onboarding, Records transitions and renewals. Submission checks the proposal again inside a serializable transaction and freezes the accepted number. It never renumbers the existing roster. Rules requiring roster recalculation, replacement of existing numbers, first-academic-day assignment or a separate manual-bulk numbering workflow block this command explicitly instead of pretending those workflows have run.

The permanent profile stays visible during a pending renewal. Before activation, the requester may cancel a mistaken target with a reason if the case is PendingChecklist and has no new target enrollment or new-year Finance dependency. Cancellation preserves the old case and its linked transition; a replacement receives a new review case. Provisioning cannot be cancelled. School-permitted governed division/roll edits can synchronize the active renewal, but its target class is fixed: there is currently no automated wrong-class recovery while Provisioning. Transition correction becomes available only after Applied and still blocks financial/downstream dependencies; staff must not treat completion as a promise that financial obligations can be reversed. Repeated same-year withdrawals and returns retain every completed review, reopen the same enrollment row, and preserve historical GR, payments, attendance and document relationships.

The schedulers poll every minute. Transition dates use the Admissions institution-date helper; change timestamps use the same application clock as their due selector. A case becomes eligible at its effective date/time and applies on a due worker tick. Failed transitions retain a code and become Exception; staff resolves the source issue and uses Apply to retry. Failed changes become Conflict and require a reviewed replacement request. Policy republishing affects new requests, while live authority and privacy rules are rechecked for existing requests and report download.

## Reserve an identity and finalize admission

### Current Offering-linked onboarding

Admission finalization links a Student and an onboarding case without creating operational enrollment. One effective readiness rule must cover the Admission Offering; a rule may cover multiple Offerings. Academic context comes from those Offerings. New cases freeze the readiness, applicable published workflow document policies, approval and access requirements. An older unactivated case without that snapshot requires an explicit requirements review; reading it does not silently adopt current settings. Completed and activated historical cases retain their history.

Applicable required documents and fee-structure selection may be deferred from Admissions into Onboarding. Activation requires confirmed admission, verified required workflow documents, Class/Division placement, assigned GR and Roll numbers, confirmation of an existing matching Finance fee structure, and configured approval. Waivers and manual document-complete overrides cannot satisfy required evidence. Existing Admissions validation owns required profile data; legacy blanket family, medical, contact and service-consent settings do not add requirements to an Offering snapshot.

Fee selection and confirmation save admission intent only. Onboarding does not assign Finance obligations, create dues, collect payments or wait for first payment before activation, completion or credentials. Finance source and APIs remain unchanged.

GR and Roll assignment are separate commands. GR assignment does not require Roll; Roll assignment does not require a GR series. GR duplicates are rejected across boards. Roll uniqueness is academic year, Class and Division, starting at 1, so the same Roll is allowed in different divisions. First manual GR assignment needs no correction reason; later GR corrections require one. Exact GSEB school-level uniqueness and promotion policy remain backlog.

GR and Roll settings are saved as Draft and then Published. Published edits create a new Draft version; published and superseded history remains immutable. Unassigned numbers use the currently published applicable numbering rule; requirements snapshots do not freeze an unselected number format. Existing assigned numbers are not renumbered by publication.

GR and Roll locks are independent policies for a selected division group. They lock identifiers rather than Division placement. Authorized administrators may unlock with an audited reason and correct identifiers. A new entrant into a locked group receives the next automatic GR and division Roll even if the school's ordinary mode is manual. Placement remains editable; a preserved locked Roll cannot move into a conflicting occupied number.

Activation commits an enrollment before preparing app access. The active student is visible in Student Records even if account preparation or credential delivery fails. Automatic or staff-operated credentials run after activation; failures retain the enrollment and expose access pending for retry in Credentials. No credential email is sent before activation. A completed case is not reopened by a settings edit.

### Historical route-based onboarding and Records renewal

The following paragraphs describe retained historical and renewal behavior. They do not add first-payment, service-consent or other legacy gates to the current Offering-linked flow above.

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
3. New offering writes use General/Inquiry routing. The workflow's one distinct configured Form-stage reference supplies the Application form, ignoring a separately supplied Application form ID. Multiple different Form-stage references remain invalid because application runtime freezes one form instance. Inquiry form selection remains independent, with a workflow default as a fallback. `AdmissionOfferingConfigurationValidator` still checks cycle, hierarchy, UserScope, dates, published references, and full Offering-identity uniqueness; an operational code is not an identity key.
4. Active offerings require complete readiness; inactive offerings may save missing runtime references for later completion.
5. The command service persists through the aggregate and returns a scoped detail projection.
6. Update compares the concurrency stamp and locks academic identity after an application exists. Routing on an offering already used by applications remains historical. An unused legacy offering can be explicitly saved as General/Inquiry. A stored legacy Application form is retained only when the same workflow has no explicit Form-stage reference; changing workflows resolves the new workflow's form.

Bulk preview and creation resolve each selected Standard's own school level before hierarchy and scope validation, so one medium can include classes from different levels. No shared first-class school-level filter is required.

Fresh public direct-registration and family-match commands are retired, and ordinary application start requires a linked Inquiry. Existing applications and their frozen runtime continue. The internal Student Records return coordinator retains its governed direct path for historical offering configurations; this is not a public bypass.

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

1. An Admission Head or administrator may create a reusable Admission Intake Setup when shared handling and automatic routing are needed. The setup combines allowed inquiry sources, deadlines, and follow-up defaults with Counsellor routing rules; its fallback manager is required when a setup is used. Duplicate handling uses automatic detection with a reason for a separate family, and application start does not require contact or qualification. Source detail, guardian email, and applicant date of birth have no policy-level requirement. Published form validation still applies. Counsellors do not configure these rules and only work the inquiries assigned to them.
2. Optionally select the setup on an Inquiry Offering. Selecting it stores both underlying profile references and validates the selected setup; leaving both references empty permits activation when the workflow, forms, hierarchy and dates are ready. Historical Offerings retain their stored references and routing once applications exist; editing an unused legacy Offering converts it to the current Inquiry route.
3. When an Offering has a setup, it supplies the policy and assignment pool at inquiry creation. Assignment counts open inquiry, application, and onboarding cases, then chooses the least-loaded counsellor below capacity. A database transaction lock serializes assignments for one profile. The configured fallback receives the case only when every configured counsellor is at capacity. Governed Student Records returns retain their internal assignment route; fresh public Direct Registration is retired.
4. A staff-captured inquiry with no known Offering is tied to a published cycle and remains unassigned for triage. Staff confirm an open Offering later; its current setup policy and assignment then apply when configured. For an Offering without an assignment profile, staff manually assign from active Admissions Counselor identities with access to that Offering; the inquiry leaves Unassigned after assignment. Configured Offerings retain their pool and manual capacity restrictions. Linked inquiry and application ownership is kept together when the application owner changes, and scheduled follow-ups move with it. Finalization seeds onboarding ownership from the application.
5. An administrator may create a revocable public inquiry link for one published cycle and published Inquiry form, with an optional Admission Intake Setup, fixed source, optional campaign detail, and optional expiry. Selecting a setup retains automatic assignment from its pool. Leaving the setup empty explicitly keeps every inquiry from that link Unassigned, even when the selected Offering has its own automatic routing or legacy workflow default; staff assign a counsellor later. The confirmed Offering supplies its Inquiry handling policy. The raw link token is returned only on creation; only its SHA-256 hash is stored. The public form requires contact consent. Repeated phone submissions receive the same acknowledgement without creating another inquiry. Requests are limited per link in the HTTP host.
   Link creation requires an accessible active Inquiry/Both Offering in the selected cycle using the selected Inquiry form. A configured link additionally requires that Offering to use its assignment profile and validates active pool/fallback access. A manual link does not require a matching setup and stores a null assignment profile. The authenticated `GET public-inquiry-links/options?admissionCycleId=...` projection requires Admission Offerings View and returns distinct compatible profile/form pairs, including null-profile manual options, within the current user's Offering scope; archived setups and unpublished or non-Inquiry forms are excluded. Missing Offering, counsellor access, fallback access, and past expiry return distinct `Admissions.PublicInquiry.OfferingRequired`, `CounsellorUnavailable`, `FallbackUnavailable`, and `ExpiryInvalid` errors with corrective guidance. `OfferingRequired` includes `Reason` (`NoActiveInquiryOffering`, `SetupNotLinked`, or `FormNotLinked`) and explicit corrective text. A rejection does not create a link; staff may correct the selection and retry.
6. Editing a setup changes the shared rules for future processing wherever it is selected. Archiving is allowed only when no active Offering or active unexpired public link still uses it. Existing cases keep their persisted snapshots and ownership history.

## Historical managed Inquiry

1. Existing published Inquiry-stage policies and stored snapshots retain historical JSON; effective reads normalize retired duplicate, application-start, source-detail, email, and date-of-birth settings to the same standard behavior as new intake setups.
2. Allowed sources, counsellor assignment, deadlines, follow-up defaults, and qualification checklist continue to apply. The current stage editor does not create new Inquiry stages.
3. Validate and publish the workflow, then attach it to an offering whose forms and counsellor scope are compatible.
4. Create an inquiry; the server validates the selected source and published form, accepts optional source detail (including QR-code and school-fair sources), stores the preferred language, checks current-cycle duplicates, suggests same-mobile families from other cycles only when their applicant is visible in staff admission scope, snapshots policy and deadlines, assigns a counsellor when configured, and records duplicate-override context in audit activity. A separate family on an actual duplicate requires a reason in both basic and managed flows.
5. Work from All, My inquiries, Unassigned, a specific counsellor, Overdue, Awaiting first contact, Awaiting qualification, or Open follow-ups. Operational queues are filtered before server paging, URL-backed clients can restore the same view, and each row carries its next-action and SLA summary.
6. Assign one inquiry or atomically assign up to 100 inquiries. Manual assignment lists only active staff who can access the inquiry's offering and rechecks that scope on save; default and round-robin workflow policies remain available for historical Offerings.
7. Edit identity, contact, source, source detail, or language before application start. The concurrency stamp rejects stale edits, and the activity timeline preserves the change.
8. Compose a preferred-language family communication from the Inquiry list. Staff with Communication Add permission can create a draft; staff with Communication Send permission can request immediate delivery. Existing Communication approval, provider, consent, tracking, retry, and usage rules stay authoritative.

Pipeline projects active inquiry-linked applications before Admission Decision from existing workflow state, current application ownership and retained inquiry follow-ups.
Explicit inquiry handoff remains idempotent and never resets scheduled reminder dates.
Post-handoff follow-up commands use the linked active application, existing management policy/owner restrictions and concurrency stamps; they do not unlock inquiry qualification or details.
Entering Admission Decision transfers workspace ownership from Pipeline to Applications before approve/reject/waitlist; no application is duplicated or deleted.
Applications retains decision, finalized and correction cases using persisted decision-entry evidence, while pre-decision terminal status alone does not imply transfer.
Successful admission finalization retains the existing Student/Onboarding handoff; failed commands preserve the application and its workspace ownership.
Historical direct applications and governed Student Records return admissions remain outside the fresh inquiry Pipeline.
9. Record structured contact channel, direction, and outcome; schedule, complete, or cancel follow-ups; then record the checklist-backed qualification outcome and standard closure reason where required.
10. Start an application only after the exact linked Inquiry is Interested and its open Offering and staff scope are valid. Structured contact and the separate legacy qualification checklist remain optional CRM actions; saving Interested does not perform the explicit handoff.
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

The compact pre-handoff follow-up command records the staff note, Pending/Interested/Not interested status and next reminder atomically.
Saving Pending or Interested supersedes previous open reminders and creates one replacement; Not interested cancels the remaining open reminders without scheduling another.
The old tasks and their cancellation reasons remain in history, and separate general scheduling keeps its maximum-open limit.
First Interested schedules the existing durable qualification-email intent once; it does not move the inquiry to Pipeline.
Reminder inputs retain an absolute offset-bearing instant, and new scheduled tasks persist through the field-level timezone-aware binding.
For previously shifted tasks, the current-reminder projection can recover an original offset only through exact task-ID history; existing database rows are not migrated or repaired by this read.
Once the inquiry is handed off, its details and qualification remain locked and the linked active application owns subsequent follow-up commands.

## Configure and publish a workflow

1. Create a draft identity and, optionally, its stages and routes in the same request. A selected template's published Registration form is attached to its Form stage. The workflow may also store a published Inquiry form as a default for future Offerings; it does not replace the Offering's own Inquiry form reference. Clone, new-version, and rollback operations preserve this default.
2. Select only Form (1), Assessment (2), Payment (4), Documents (5), or Admission decision (7) from the server registry. Eligibility (3), Review (6) and Offer (8) are retired and reject publication/runtime commands without renumbering stored enum values. Inquiry, Seat, Enrollment, and Custom remain registered for immutable historical workflows but are deprecated and not selectable for new stages. Appointment remains non-selectable until its runtime scheduling and completion flow is available. The Approval enum value is retained for compatibility and is presented to schools as **Admission decision**.
3. Mark exactly one entry stage and at least one terminal stage. Schools may choose any stage type as an ending, including Form and Assessment. A single stage may be both entry and ending. Ending stages have no outgoing routes; their configured completion requirements still apply.
4. Add controlled conditional transitions and at most one default route per source stage. Each conditional route from a stage uses a unique non-negative priority; a route without a condition is always the single default route. Condition fields must use the server allowlist for supported application, assessment, document and admission-decision facts; Payment status is feeStructureConfirmed rather than monetary settlement. Retired stage and old payment balance facts are not valid new transition conditions.
   A required stage cannot enable ordinary manual skip. Stage types without a runtime-ready handler are publication errors, not advisory warnings.

Admission forms use the same controlled rule foundation for section visibility, field visibility, and conditional requiredness. The form runtime supports 100 canonical Student field bindings: 15 original Applicant, Guardian, and Address bindings plus 85 additional profile, family, address, previous-school, health, eligibility, identity, and admission-preference bindings. Financial assistance fields are excluded from the expanded catalogue. The original bindings update admission identity and contact properties. Additional bindings persist on the applicant, prefill registration after inquiry, and are copied to the Student record's captured admission fields during finalization. Staff can review those captured values on the onboarding case detail; they are not automatically treated as verified SIS profile values. Clearing an additional answer removes its applicant value before finalization.

Every new form version carries an Inquiry or Registration purpose. Clone and new-version operations preserve it, and changing it is allowed only on a Draft. Earlier Correction and Consent values may remain on existing test records but cannot be used for new forms. Workflow publication requires a published Registration form on each Form stage and a published Inquiry form for any configured workflow default. Offering readiness rejects a purpose mismatch before activation.
5. Validate reachability, dead ends, duplicate routes, cycles, terminal reachability, published form references, and every enabled managed-stage policy. Form, Assessment and Documents policies are parsed with the same domain validators used by runtime, so malformed or out-of-range settings cannot pass publication validation.
6. Publish the validated version immediately with Workflow Add authority and a current concurrency
   stamp. Stored maker-checker settings do not gate publication; no scheduling or notification
   configuration is part of this flow. The previous published version becomes Superseded.
7. Create a new immutable version for later edits. Existing Offerings and Applications continue to
   use their pinned Published, Superseded, or Archived version.
8. Historical scheduled records do not auto-publish. An explicit publish request revalidates and
   publishes an eligible historical schedule immediately, preserving its existing audit history.
9. Archive a Published or Superseded version only when the existing usage rule permits it.
   Historical audit records and compatibility recovery contracts remain intact.

Publication and Offering activation validate different scopes. Workflow publication owns graph, runtime-handler, managed-policy, and SLA/deadline-range validation. People assignments cannot be fully checked until the workflow is attached to an Offering with a concrete academic hierarchy. Activation therefore verifies the new Offering's assignment profile has eligible active staff in that hierarchy, without adding retired Review assignee requirements. Historical Offerings using pinned Inquiry-stage assignment rules retain their existing compatibility checks. A reusable published workflow is not coupled to one campus, but an Offering with unusable staff assignments cannot become active.

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

An **Admission decision** stage retains enum value Approval (7). Approve, Waitlist and Reject require the active Decision stage with existing authorization, scope and concurrency. Approved and Rejected may complete it; Waitlisted remains pending until a later decision. General admissions finalize from Approved plus a completed journey without OfferAccepted, while identity, guardian, duplicate, capacity, scope and idempotency guards remain.

## Run a managed entrance assessment

1. Configure managed Assessment with optional scheduling, attendance, scoring and Maximum attempts; independent result review is disabled.
2. Publish and attach the workflow to an Offering. Scheduled assessments require a matching future session and booking; result-only assessments do not.
3. Create an attempt, record attendance and result under the existing scope, permission and concurrency checks.
4. Retakes respect Maximum attempts and retain earlier evidence. Failed, absent and retake outcomes require valid explicit routes when leaving the stage.
5. Advance explicitly after the evidence and route gates pass. Recording a result does not itself advance the journey.

The workbench exposes To book, Attendance, Result, Ready, Attention and Completed queues; no separate result-review queue is part of the supported flow. Completed-stage attempts remain readable within tenant and academic scope.

## Verify applicant documents

1. Configure managed Documents with required/optional upload requirements, class applicability, formats/limits and optional Student document category. Codes are hidden from UI and generated uniquely on workflow save, then preserved on subsequent saves.
2. Publication normalizes uploads at the active stage, disables waivers/independent review and clears earlier Form-stage collection.
3. Upload evidence, accept it or request a reasoned replacement through existing permission, scope and concurrency checks. Replacements need fresh acceptance; prior revisions remain readable.
4. Resolve every applicable mandatory requirement before advancing. Missing required uploads and unavailable class context fail closed.
5. Finalize an Approved completed journey; link accepted evidence to the Student in the finalization unit of work and continue operational Onboarding. Mapped uploads can satisfy corresponding onboarding documents; unmet onboarding requirements retain their scoped upload flow.

`standardIds: null` or omitted means All classes. Selected IDs must be nonempty, unique and tenant-local. Conditional requirement configuration and physical-original/evidence-mode setup are not offered in the supported editor.

## Check eligibility

Eligibility, Review, and Offer are retired Admission Stage types. New drafts cannot select them, publication rejects them, application workspaces do not mount their working panels, and their direct admission operations reject unsupported stages. Existing enum numbers and stored audit records remain intact. Inquiry qualification, Student Records reviews, class capacity, and Finance ownership are separate contracts.

## Managed Review workflow

Eligibility, Review, and Offer are retired Admission Stage types. New drafts cannot select them, publication rejects them, application workspaces do not mount their working panels, and their direct admission operations reject unsupported stages. Existing enum numbers and stored audit records remain intact. Inquiry qualification, Student Records reviews, class capacity, and Finance ownership are separate contracts.

## Run a managed Form stage

1. Form is managed Submission with rules by default. Attach the published Registration form and configure responsibility, deadline/late behavior, corrections and optional section verification.
2. Applicant Save Draft remains enabled. Submission-limit and automatic-next configuration controls are hidden; server submission/correction safeguards remain.
3. Submit validates frozen fields and policy. Without required verification, submit advances automatically.
4. With verification, authorized staff verify configured sections and Continue explicitly only after all blockers resolve. Revocation/correction requires a reason; correction makes earlier verification stale.
5. Preserve immutable submissions/change history and current concurrency through every command.

## Run a managed Payment stage

1. Add Payment as Finance fee-structure confirmation; no independent admission collection policy is authored.
2. Read only published structures matching the Offering tenant, academic year, Board, Medium and Class. Missing candidates block fee confirmation with a clear error.
3. Save the structure and optional heads using application concurrency. Mandatory heads and an immutable structure-version snapshot are included by the server.
4. Advance with the valid saved snapshot, or defer selection to Onboarding when the effective Offering-linked readiness rule supports that handoff. Onboarding still requires structure confirmation before activation. The runtime reports feeStructureRequired or feeStructureConfirmed; it never equates confirmation with Paid.
5. Finalization hands the saved intent to Onboarding. Finance alone assigns fees, creates dues and records/allocates actual payments.

Admission record, verification, waiver and reversal commands are unavailable in this flow. No database migration or Finance ledger mutation is required by confirmation.

## Run a managed Offer stage

Eligibility, Review, and Offer are retired Admission Stage types. New drafts cannot select them, publication rejects them, application workspaces do not mount their working panels, and their direct admission operations reject unsupported stages. Existing enum numbers and stored audit records remain intact. Inquiry qualification, Student Records reviews, class capacity, and Finance ownership are separate contracts.

## Run a legacy managed Seat stage

Seat is not selectable in new workflow drafts. Published workflow versions and applications already pinned to a Seat stage continue to use the existing capacity, reservation, renewal, waitlist, exception, release, and completion policy without mutation.

For those historical journeys, capacity, expiry, and waitlist facts remain server-computed. Confirm, renew, release, promote, or adjust capacity only when the returned server capability permits it; every action records immutable history. Staff should refresh and retry after a concurrency conflict instead of overwriting a newer allocation decision.

Room-derived class capacity is configured by assigning Institute Setup rooms to a class from Admission Offering details. Its shared remaining-seat count is a warning only and does not reserve seats or implement a waitlist. Any future offering-level reservation and waitlist contract remains separate from this class-capacity display and from the historical managed Seat stage.
## Student dashboard reads

1. Authenticate with Dashboard.View; resolve an active user and role membership from the tenant database.
2. For Student/Parent-only identities, resolve verified personal associations and return only those names/relationships.
3. For staff, resolve current academic scope and exact destination grants; return section eligibility and permitted links.
4. On each section GET, repeat the authority/context check, validate the selected tenant year and scope, and run only permitted aggregate reads. There is no write side effect.
5. Follow the owning route to perform work under that module's existing action authorization. Client refresh repeats live reads; the Reporting snapshot refresh command remains a separate workflow.
