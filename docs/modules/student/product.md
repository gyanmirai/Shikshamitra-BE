---
module: student
domain: product
content_type: Reference
status: canonical
updated_on: 2026-10-08
owner: backend-team
audience: humans-and-agents
---

# Understand Student Enrollment Admissions behavior

TL;DR: an Admission Offering is the tenant-scoped, auditable configuration that connects one cycle and academic hierarchy to published workflow and form versions.

## Onboarding credential emails and portal access

| Rule | Behavior |
|---|---|
| Configuration | Applicable school/year readiness rules own separate student and guardian email flags, both off for legacy settings; manual/automatic mode; activation/full-completion timing; and the selected father/mother/guardian for automatic provisioning. Email requires its corresponding account handoff. |
| Mandatory readiness | Both timings require an active committed enrollment, GR number, roll number, division and every current required document validated, accepted from Admissions or explicitly overridden. Optional document-step policy and a generic step waiver do not bypass the email gate. Completion timing also waits for the existing Finance and required handoff completion. |
| Document override | Authorized Onboarding.Waive staff may override specific or all current required document types, with a mandatory note and audited staff/time. No second approver is required. Newly required document types remain unsatisfied. |
| Account ownership | Email is mandatory. Student gets its own identity; a single selected Parent identity can link multiple children. Existing verified Parent linkage preserves its password. Staff/privileged identities and unverified email collisions require identity review. Automatic provisioning respects current identity grants and verified guardian contact. |
| Delivery | A durable tenant-owned request tracks Pending/Sent/Failed/Cancelled; dispatch rechecks current actor grants/scope, owner mailbox, links, settings and readiness. A new passwordless account receives a random initial password and owner setup link; temporary secrets are encrypted until transport acceptance or cancellation. Staff responses never return secrets. Existing identities keep their password; repeats use owner recovery. |
| Exit | Applied Withdraw/Transfer/Graduate transitions set Left and revoke only that child's portal links. Current portal reads and token renewal deny departed children; shared Parent identities retain access to other children. Reviewed corrections restore only recorded links that still match the owner profile and exclusive role. |
| Verification boundary | Sent means the configured email sender accepted delivery. Inbox arrival and the separately maintained mobile-app UI require their own acceptance checks. |

## Universal daily Student Attendance

| Area | Implemented rule |
|---|---|
| Observation | One school-date record per enrollment; Present/Absent with policy-enabled Late/Half-day details. Pending means unmarked. Individual shortened schedules do not automatically produce Half-day. |
| Membership | Formal enrollment and a school-confirmed AttendanceStartDate determine eligibility. Dated placement/activation/exit history prevents current class changes from rewriting the historical roster. Fee or portal provisioning does not independently block operational attendance. |
| School days | Existing published Academic Calendar owns working days, holidays, vacations and emergency closures. Missing or ambiguous calendar setup fails closed and keeps affected unsnapshotted reports provisional; an explicit holiday is a confirmed exclusion. Individual recurring or temporary schedules narrow working-day eligibility. |
| Configuration | Save draft, preview, publish an effective-dated immutable policy. Academic-year policy precedes school default; scoped exceptions resolve Board > Medium > Stage > Standard > Division. Equal-priority overlapping rules are rejected. |
| Save and submit | Partial drafts are allowed. Submit checks all eligible students, including persisted draft rows, and pins policy/credits/membership/roll snapshots. No unmarked row may be submitted. |
| Corrections | Same-day recorded corrections follow the school reason rule. Prior-day or locked corrections follow approval rules. Review cannot be performed by the requester; a locked-day correction also requires lock-management authority. |
| Leave | Approval annotates the roster without fabricating an observation. Configured leave treatment changes denominator credit after explicit attendance marking. Leave approved after submission requires an audited correction under the school's review rule, even when Present/Absent stays unchanged; existing credits remain pinned until application. Conflicting Present observations require a reason; overlapping approved school-day leave is rejected. |
| Reports | Register, profile, daily cumulative metrics and dashboards use the same calculation. Percentage uses recorded eligible credit; unknown days remain separate with completeness/provisional flags. No recorded denominator produces null, never an invented zero percent. Low-attendance comparison uses the effective school threshold. |
| Cutoff | The configured local-school cutoff supplies a reminder for expected unsubmitted days; it does not silently lock attendance. |
| Concurrency | Revision/roster checks and serializable commands reject stale writes. Matching actor/payload retries reuse durable operation results. Enrollment/date uniqueness prevents recording the same student in two divisions. |

For 17 Present, one Absent and two unmarked expected days, the recorded percentage is 94.44%, completion is 18/20, and the result is provisional.
Legacy migration retains original observations and ambiguous conflicts, bridges valid enrolled identities only when formal enrollment is absent, and leaves attendance-start dates unconfirmed.
It never invents historical presence, activation dates, calendar definitions, policy snapshots or provisioning completion.
Parent alerts, offline marking and biometric/RFID integrations are outside this rollout.
Local tests and source availability do not certify deployment or authenticated browser verification.

## Permanent Student Records behavior

| Area | Backend rule |
|---|---|
| Entry | A completed onboarding case or explicitly approved historical override permits the permanent profile. Provisioning-only entry follows the school's OnboardingOnly/Summary/Full policy. Operational modules continue to use enrollment independently. |
| Identity | The same Student ID and GR record remain across promotion, repetition, graduation, withdrawal, transfer, and return. Historical overrides are audited; duplicate review does not merge profiles. |
| Changes | Action policy chooses audited direct execution or one to three ordered central-role approval stages. Optional conditions narrow approval. Pending requests pin the published policy and source evidence; self-approval is prohibited. |
| Sensitive data | Current database roles govern field groups. Current and historical-year school policy both constrain history and export reads. Free-text reasons and event remarks are withheld when relevant groups are restricted. |
| Guardian changes | Published school policy chooses SuspendUntilReview or KeepUntilReview for previous portal access. Resolution uses verified identity and preserves sibling relationships. |
| Documents | IDs and certificates retain issued snapshots, numbering, issue history, reasoned reprint/void actions, and idempotency. Leaving certificates for withdrawal/transfer require the configured ApprovedExit or EffectiveExit point. Graduation requires an Applied case whose effective date has arrived, including when ApprovedExit is selected. |
| Year-end | Promote, Repeat, Hold, Graduate, Withdraw, and Transfer use batch preview, owner checks, approval, and per-student atomic apply. ReadyOnly skips exception rows; HoldAll keeps the batch waiting when any submitted row cannot proceed. |
| Annual renewal | DirectEnrollment creates a unique next-year enrollment after approval and placement checks. ShortChecklist opens year-specific short onboarding; the prior permanent profile stays visible. Finance and portal completion follow enrollment activation. |
| Returns | Every route follows the frozen Records Status approval rule. ReuseProfile opens short renewal. FreshApplication follows the Offering's Inquiry or DirectRegistration entry; Both requires an explicit choice. ResumeStage resumes the eligible linked application's current stage. Fresh/Resume remain AwaitingAdmission until real Onboarding completion. |
| Return closure | Terminal Admission rejection or preactivation Onboarding cancellation automatically changes its linked AwaitingAdmission return to Cancelled in the owner's transaction. Student identity, the inactive enrollment, original completed onboarding, intake journey and return history remain. A new reviewed request uses a new idempotency key; replay of the old key returns the closed request. Activated returns cannot be cancelled this way. |
| Corrections | A correction retains the applied original and records its own school-governed request. Changed placement/status evidence, downstream attendance/documents, or new-year Finance assignment block reversal. Records never reverses money. |

Settings are school defaults with optional academic-year overrides. Only published immutable versions affect runtime; saving a draft does not alter pending or future execution. Approver roles must exist in the current tenant's central Security catalogue. The safe starter requests approval for identity, guardian, placement, status, medical, bank, corrections, and historical overrides.

Transition dates use the same institution calendar as Admissions (`Asia/Kolkata`). Exit roster removal follows Approval or EffectiveDate. Scheduled apply rechecks current permission, active identity, every recorded approval's database role, scope, placement snapshot, capacity, roll uniqueness, and owner checks. Failed apply becomes an explicit Exception requiring staff retry.

Required academic evidence comes from a published/frozen source-class result; this is evidence availability, not an inferred promotion decision. Finance uses the canonical collection ledger and immutable assignment evidence. Library requires outstanding issues to be returned; Transport requires the current active route assignment to be closed. Required evidence is never accepted from a client-supplied pass flag. Unsupported required renewal check keys block configuration consumption instead of silently passing.

The short renewal snapshots the existing target-year Onboarding readiness rules for Returning student, including inherited campus evidence. It does not manufacture an initial admission/onboarding case or bypass Finance's confirmed first installment. Missing owner evidence is reported as an actionable blocking issue.

Admissions return integration is implemented in source and remains under verification. A fresh Inquiry is a real New inquiry and must complete the configured Admissions journey; it is never marked contacted or qualified by Records. Fresh DirectRegistration creates a new applicant journey linked to the permanent Student. Initial completed Onboarding history remains intact, while finalization creates a separately tagged return case. A reviewed same-year return reopens the original inactive enrollment and preserves its origin case and permanent GR; a reviewed historical profile without enrollment requires independent approved legacy authority before its first real enrollment. Governed profile and guardian changes remain separate from intake. Automated and live verification limits are recorded in [testing.md](testing.md).

Wrong-class renewal fee correction is deferred by the user for this implementation. Immutable Finance assignments and fixed provisioning class remain guarded; same-class division/roll edits remain available. A future correction must obtain Finance-owned review and preserve financial history before renewal completion.

## Reserved Student identity

Creating an applicant through Inquiry reserves a server-generated `ReservedStudentId` GUID without inserting an `AppStudents` row.
The reservation belongs to the applicant, so all applications for that applicant share it.
It is immutable, nonempty, globally unique across tenants and soft-deleted applicants, and is not a Student foreign key or an operational GR/roll number.
Profile edits, offers, rejection, withdrawal, and failed finalization never replace or recycle it.

Actual Student creation remains at Finalize Admission after the existing accepted/unexpired-offer, completed-workflow, identity, guardian, evidence, eligibility, review, seat, permission and scope checks.
A new Student uses the reservation as `AppStudents.Id`; an explicitly resolved existing Student retains its real ID and leaves the reservation unused.
`AdmissionApplication.StudentId` remains null until successful finalization and then identifies the actual Student, not necessarily the reservation.
Applicant linking, Student creation, document handoff, onboarding case creation and audit writes participate in the same finalization unit of work.
Repeated successful finalization returns the existing Student/case; collisions never silently link another Student or generate a replacement reservation.
Operational enrollment and placement remain in Student Onboarding.

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

The active identity is Admission Cycle, optional Campus, Board, Medium, Stage, Standard, and
admission Track (General, RTE, Management, or Staff). A class may therefore have one offering
per track with a distinct process, while a second offering for the same track and identity is rejected.
Division is not part of Admission Offering identity.
RTE offerings must use direct Registration entry and a published workflow without Inquiry or
Assessment stages. General, Management, and Staff tracks can independently choose inquiry or
direct Registration entry according to their offering workflow. An inactive RTE draft may omit
published references, but cannot save an incompatible entry mode or workflow.
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
Managed Inquiry constrains allowed sources and assigns offering-scoped active counsellors manually or round robin.
All intake profiles, workflow policies, and persisted snapshot reads use standard behavior: contact and qualification do not gate application start, source detail is optional for every source, and policy-level guardian email and applicant date-of-birth requirements are disabled.
Stored historical JSON and entered data are preserved; published form requirements remain authoritative.
Automatic duplicate detection remains enabled; a separate family on an actual current-cycle duplicate requires a reason, while an eligible sibling can link to its existing family.
First-contact and qualification deadlines are snapshotted when the inquiry is created.
Follow-ups retain assignment, purpose, due time, completion or cancellation state, actor, and audit timestamps.
Inquiry staff can create a linked family communication for WhatsApp, SMS, or Email. The CRM records the campaign handoff in admission activity while the shared Communication module owns approval, consent, providers, delivery attempts, retries, and usage; no duplicate Admissions messaging store is introduced.

Application Experience creates Inquiry and Registration forms. Correction requests reopen the submitted Registration form, and consent questions may be included in that form. Offering activation accepts only a published Inquiry form in the inquiry slot and a published Registration form in the application slot. Admission lifecycle content is stored as `Admissions lifecycle` Communication templates for inquiry acknowledgement, application receipt, missing documents, exam schedule, hall ticket, result, offer, payment reminder, and finalization. Communication remains the delivery owner.
Qualification uses a versioned checklist and records Qualified, Nurture, Not interested, Lost, or Duplicate with a reason where required.
Application start rechecks the linked inquiry, offering, and scope on the server.
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
Admissions finalization requires a completed admissions workflow and normally an accepted, unexpired offer; it creates or links the Student plus a Student Onboarding case. Non-General direct-registration offerings without an Offer stage can also finalize from Approved. If staff issue an optional offer on that path, it must be accepted and unexpired before finalization; the accepted-offer path remains valid. Guardian confirmation, identity resolution, and evidence checks apply to both paths.
Operational StudentEnrollment creation remains part of onboarding activation.
Activation starts Provisioning rather than completing onboarding. Completion requires the academic fee assignment, confirmed settlement of its first scheduled installment group, required student/guardian account links, and staff confirmation of a saved consent record when a legacy policy requires service consent. Finance owns all money writes. New portal accounts are passwordless; owners use the protected Account password-reset flow to establish access. The case never treats an unconfirmed payment, a queued account request, or unconfirmed consent as completed evidence.
Subject-level marks, public applicant self-booking, and external examination provider integrations are not included in this assessment policy version.

## Managed document verification

Document Verification opts into `ConfigurationJson.documentVerification` version 1. Missing or disabled policy retains legacy manual behavior. Published workflow versions pin the requirements for existing applicants. Each requirement has a stable code, required/optional/conditional applicability, upload/original/either/both collection, safe PDF/JPEG/PNG formats, a 1 to 20 MB limit, and optional tenant document-type mapping. Mapped uploads satisfy both policy and catalog constraints.

Evidence is owned by the application and runtime stage. Every upload, replacement, original observation, and waiver adds an immutable revision. A replacement resets original observation and review; recording an original can retain the previous upload with its original uploader identity. Accept/reject reviews are immutable and apply only to the latest unreviewed revision. File validity alone never means acceptance. Independent review excludes the revision submitter, retained file uploader, and original observer. Waivers require policy support, a distinct permission, and a recorded reason.

Conditional requirements use saved application answers, linked inquiry snapshot answers, and assessment facts. Missing referenced answers block completion; an explicitly saved blank answer supports `IsEmpty`. Application status and reserved inquiry/assessment/document facts come from server records. Required and matched conditional requirements block normal advance, skip, and administrator override until accepted collection evidence or an authorized waiver exists. Optional evidence does not block completion. Finalization rechecks visited managed document stages before creating a Student; successful finalized retries preserve their existing outcome.

Canonical document selectors cover Admission type, Class, Category, RTE status, Transfer admission, Scholarship, and Assessment outcome. Application answers are normalized to these keys, and Class falls back to the offering Standard identifier.

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

### Saved fee structure intent

Admissions can save a published Finance structure revision and selected head references against each application.
The first Offer or Payment stage actually entered owns selection; later stages display that same intent.
An application without either stage retains the existing Student Onboarding fee workflow.
Managed Payment policy version 1 adds `requiresFeeStructureSelection`, defaulting to false for historical compatibility.
Enabling it in any pinned managed Payment definition makes saved fee intent required at the workflow's first entered Offer or Payment stage, including when Offer comes before Payment.
When enabled, the required selection is validated before collecting payment, issuing an offer and completing the relevant stage.
Payment evidence locks the saved choice; corrections and refunds remain Finance responsibilities.
Onboarding staff explicitly confirm saved intent before provisioning completes, while Finance assignment and confirmed-payment readiness remain independent requirements.
Selection never changes the configured admission amount or Finance pricing, allocation, settlement and automatic continuation behavior.

Payment stages opt into `configurationJson.payment` version 1. Schools configure a named purpose, ISO currency, required amount, payer, staff collection methods, optional deadline, late-payment behavior, installments, independent verification, authorized full waivers, and continuation after settlement. Missing or disabled settings retain basic manual stage behavior.

The workflow cannot skip a managed Payment stage. It completes only after the configured amount is settled or a full waiver is recorded with a reason. Pending verification reserves its amount so staff cannot collect the same balance twice. Overpayments, disallowed methods, stale writes, same-user verification, duplicate retries with changed payloads, and unreasoned rejection, waiver, or reversal are rejected.

Admission payment rows remain Finance-owned audited records while the Admissions module owns stage orchestration. Online gateway collection stays unavailable until a trusted tenant gateway is configured; a draft that selects `onlineGateway` cannot be published.

## Managed Offer stages

Offer stages opt into `configurationJson.offer` version 1. Schools can configure unconditional or conditional content, terms, acceptance evidence, default and override validity, decline reasons, extension and reissue limits, completion outcomes, and automatic continuation after acceptance. Missing or disabled settings preserve the established admission decision and offer behavior.

Application counsellor ownership is independent of a workflow stage. Inquiry-linked applications inherit their inquiry counsellor; Direct Registration can assign one immediately; offer issue or reissue may confirm or change it. Only active users with access to the application Offering are assignable. Authorized staff can reassign an open application later, and server-filtered application lists can track each counsellor's applicants. Counsellor selection is optional so schools without counsellor teams are not blocked.

The existing Admission Application remains the single source for current offer status, issue and response timestamps, and expiry. Every managed issue, acceptance, decline, extension, withdrawal, and reissue appends immutable decision history with the stage, actor, version, reason, safe metadata, and idempotency key. Commands use optimistic concurrency, and a matching retry returns the existing result without creating a second event.

Managed Offer cannot be skipped. It completes only with a policy-allowed accepted, declined, expired, or withdrawn outcome. Acceptance enforces configured terms and conditional evidence. Declined, expired, and withdrawn outcomes require an explicit matching `offer.status` route so they cannot follow the acceptance path accidentally. This staff runtime records the response; applicant portal delivery, email/SMS delivery, and automated reminders require a separate notification integration.

## Managed Seat stages

Class-level admission capacity is a separate warning-only projection: for an academic year and Standard, sum the capacities of assigned Institute Setup rooms and subtract active enrollments plus accepted or finalized admissions not already enrolled. General, RTE, Management, and Staff offerings for the same class share this count. A full class warns staff at inquiry or registration but does not stop either action. No room assignment means capacity is unknown. This does not change the historical managed Seat-stage reservation and waitlist policy below.
An Enrolled application does not hold a second seat after its active StudentEnrollment is deactivated;
transfers and withdrawals free that class seat in the live projection.

Seat stages opt into `configurationJson.seat` version 1. Schools set the offering capacity, choose manual reservation or reservation on stage entry, define hold duration and staff override limits, enable bounded renewals and FIFO waitlisting, control authorized capacity exceptions, require release reasons, and choose which outcomes may complete the stage. Missing or disabled settings preserve the basic workflow step.

One tenant-scoped pool owns reserved and confirmed counts for each Admission Offering. Each application and frozen Seat stage owns at most one reservation record. Confirmed seats remain allocated after stage completion until admission finalization or an authorized reasoned release. Expired holds stop consuming effective capacity and are reconciled into permanent history on the next Seat command.

Managed Seat cannot be skipped. Confirmation is always an allowed completion outcome; waitlisted, released, or expired completion requires both policy permission and an explicit matching `seat.status` transition. Every mutation checks application, pool, and reservation concurrency, and repeats with the same idempotency key return the recorded result.

The Form-stage definition must equal the Offering application form. Applications still pin the published definition and record immutable submission snapshots. Managed submit enforces visible required fields, deadline, and attempt limits. Automatic advancement occurs only when configured and no section verification remains. Staff verification and reasoned revocation are audited. A correction request reopens the same form and invalidates all earlier section verification; the applicant must resubmit within the remaining attempt limit. Normal completion cannot bypass unresolved Form blockers.
## Student dashboard

One dashboard serves all authorized identities. Persisted roles select admin, leadership, teacher or common priorities; a role label never grants data access. Existing Dashboard View, exact destination action grants, submodule visibility, active database identity, tenant and current UserScope constrain every section read.

| Section | Definitions |
|---|---|
| Records | Enrolled, inactive and left permanent records eligible through completed onboarding or approved legacy override; selected-year scoped enrollment membership. Left legacy records without enrollment retain their own-year directory fallback. Class strength counts distinct eligible active students in active scoped enrollments, bounded to the 12 largest classes. |
| Admissions | Open inquiry statuses New/Contacted/Interested/FollowUp; selected-year scoped cycles. Active applications in scoped offerings exclude terminal outcomes. Admin/leadership summarize allowed scope; other staff inquiry and setup counts additionally require assignment to the current identity. Applications remain scoped and are labelled as visible applications. |
| Student setup | Unfinished excludes Completed/Cancelled; ready means ReadyForActivation; attention means NeedsAttention or an overdue unfinished case. Provisioning remains unfinished. |
| Attendance | Saved register counts grouped by submitted/draft status and pending leave requests within selected-year enrollment/division scope. These are year work summaries, not daily completeness or attendance percentages. |

Student/Parent-only database roles receive names and relationships from persisted verified portal associations, including existing self-link revocation rules; they receive no staff sections or shortcuts. Mixed staff/portal roles follow staff grants and scope. No grants, seeders, snapshots or migrations are created by a dashboard read. Existing Attendance reports remain authoritative for credit, percentage, cutoff, provisional and missing-day behavior.
