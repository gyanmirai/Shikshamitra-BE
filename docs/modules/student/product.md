---
module: student
domain: product
content_type: Reference
status: canonical
updated_on: 2026-10-29
owner: backend-team
audience: humans-and-agents
---

# Understand Student Enrollment Admissions behavior

TL;DR: an Admission Offering is the tenant-scoped, auditable configuration that connects one cycle and academic hierarchy to published workflow and form versions.

## Resume an ended admission

Resume admission restores an eligible End Journey withdrawal within its original offering, cycle and unended academic year. The application ID/number, pinned workflow/form, completed or skipped evidence and Finance records remain unchanged. End Journey saves a versioned immutable decision-history snapshot; Resume appends its own actor/reason/closure event. Earlier withdrawal history is retained.

Closed/unavailable intake, expired offers, finalized/enrolled cases and cancelled Records return handoffs are blocked. Legacy withdrawals are reconstructed only from unambiguous recorded pre-decision stage/form evidence; unclear cases require review. Seat resources are reacquired through the seat owner under current capacity and saved policy. Cancelled assessment bookings and follow-ups remain cancelled.

## Parents and the main school contact

Parents are the normal intake path: staff records Father or Mother and may add the other parent or an other guardian. Both parents are not compulsory. An explicitly selected other guardian can be the main school contact even when parent details are saved; the guardian's actual relationship is retained.

The main-contact preference belongs to the child and never grants app access, changes a shared family identity, resets a password, or relabels an existing sibling relationship. Records changes keep the existing school approval policy and snapshot checks. A preference-only change or an added unlinked guardian does not suspend an unchanged linked parent's access; changing a linked person's identity/contact retains access review.

Inquiry messaging uses the captured selected person's own contact snapshot while the original parent phone remains the duplicate/family identity. Student-based parent-first Communication audiences use an explicit saved main contact; older records without a choice retain parent recipients, with a generic guardian fallback only when no parent is present. Missing contact details never borrow another person's email or phone. Existing Communication consent, approval and provider checks remain authoritative.

Explicit non-parent Communication audience mode retains its existing general Student contact destination; the main school contact preference changes only the parent-first recipient path.

New guardian app access requires explicit staff recipient authorisation. Automatic preparation remains parent-first and does not turn an optional guardian contact into an account. Existing verified adult links remain authoritative; changing the main contact does not replace them. Father/Mother sibling-account reuse retains its separate confirmed-family proof, and an explicit case-only guardian choice leaves the source sibling's accounts unchanged.

The credentials workspace exposes whether adult access is enabled by the saved account policy independently of whether it is required. Not needed remains disabled. An explicitly confirmed case guardian choice suppresses automatic new-parent creation even in a legacy case without sibling candidates; an unchecked main-contact suggestion has no such effect.

## Add or update an app-login email during onboarding

Authorized staff can add or update the selected Student, Father, Mother or Guardian email directly from App Login, including when Admission did not capture it.
Before account creation, the narrow edit saves the current profile and explicit Student-recipient provenance without sending credentials or granting portal access.
Discovering an existing Parent match does not confirm access: its unlinked profile email remains editable, and activation without a link does not require Identity Users Update. A persisted reviewed selection and an existing linked login retain their stronger change checks.
Manual Send Credentials retains saved account-access policy, activation/readiness checks, identity permissions, cooldown and durable delivery rules.
An email already used by an existing account never silently merges families or grants that account access to another child.
Father, Mother and Guardian contact profiles may retain the same family mailbox; saving it grants no access and creates no identity. App Login selects one adult. Student and adult app-login destinations must remain distinct, and actual conflicting identities still block the edit.
For an already-linked account, an authorized identity updater requests owner verification of the new email; the old login remains unchanged until that verification succeeds.
A shared parent change requires confirmation and applies to all existing child links atomically, keeping the same identity, password and relationships.
Pending guardian-access reviews, out-of-scope children, stale profiles, changed links, invalid/expired tokens and email collisions block the change rather than partially rewriting profiles.
Linked email changes recheck canonical opposite-account saved recipients for every linked child when requested, before verification delivery and when the owner confirms. This rejects a Student/adult destination collision even before the opposite identity exists. Legacy raw contact fallback without Student-own-email provenance remains ineligible as a Student login and does not create a new collision rule.
Saved Student, Father, Mother and Guardian recipients remain distinct. Guardian login email changes use the same owner verification, shared-child confirmation, expiry, cooldown and all-child scope checks; wider identity or relationship replacement still requires Records review.

An eligible existing Parent can be explicitly confirmed and linked after activation inside Onboarding without Admissions family confirmation. The saved relationship/email/user ID, active Parent-only identity, exact verified existing-child association, target/source scope, pending access reviews, current stamp and saved access policy are rechecked. Onboarding records its own confirmation and audit; historical Admissions evidence is unchanged. Existing adult links cannot be switched through this command. Unconfirmed parents require their exact source-child initial setup evidence.

Linked reused parents can receive explicit sign-in help through the current durable outbox. This sends a protected owner recovery/setup link and never generates, adds, resets or replaces a password. Automatic and ordinary credential sends remain suppressed for reused accounts; original failed account setup retains its separate retry path. Each selected recipient remains an independent email intent with its own status, cooldown and failure.

## Automatic application numbers

Schools may configure a common application-number format across all admission offerings. Allocation occurs at the existing application lifecycle entry point, including Inquiry handoff, RTE/Management/Staff registration and returning students. The offering's Admission Period identifies the academic year; the server clock is used only for explicit calendar-year formats. Schools without a saved rule retain `APP-YYYY-000001` generation.

Rule changes are immutable creation-audited revisions for future allocations. Previously issued application numbers remain unchanged across Pipeline, Applications and Onboarding. Counters are tenant/scope-specific and serialized with PostgreSQL transaction-level advisory locks; issuing an application and its number participates in the same transaction. Replay returns the existing application before allocation. Default-to-custom format changes carry the legacy sequence forward even when the prefix changes. Calendar reset uses that calendar's legacy counter, continuous uses the highest legacy counter, and academic reset uses issued default numbers in the selected academic year. Matching historic numbers (including soft-deleted applications), the existing custom counter and the configured starting minimum prevent rewinds. Starting number and reset policy lock after first custom issuance. Previews use the same calculation and show the actual next available number without saving or reserving it; concurrent registrations can change the next number. The existing tenant/application-number unique index remains authoritative.

## Staff data scope

The browser Student module uses the authenticated staff member's existing Academic UserScope assignments. It does not require a student browser login or a separate Student assignment table. Action permissions, tenant ownership and data scope are checked independently; Academic subject access does not confer Student action permissions.

| Boundary | Rule |
| --- | --- |
| Academic placement | Ancestor IDs on a division assignment describe its path. Only explicit wildcard grants authorize descendants; a division assignment cannot authorize sibling divisions. |
| Attendance | Year, class and division must match the stored hierarchy. Lists use the same strict division boundary, and batches reject missing or inaccessible student IDs. |
| Registration | An allocated placement requires its exact division scope. A placement without a division requires class-wide or broader scope. |
| Applicant identity | Reads and notes require an accessible linked application or inquiry. Applicant 360 includes only scoped applications and activity references. Shared profile changes reject links outside scope, including family links when guardian contact is edited. |
| Unallocated inquiries | Year-wide staff can read the year backlog. Narrow staff can read their own created or assigned unallocated inquiries within an authorized year. |
| Offerings | Lists, details and existing-offering commands share the canonical class-level offering scope. A division grant makes its class offering accessible because pre-enrollment admissions have no division placement. |
| Linked applications | Offering access governs pre-placement applications. Reserved Onboarding placement and current cycle-year enrollment add the student division boundary. Current enrollment overrides completed Onboarding snapshots after a Records move. Converted inquiry/identity reads cannot bypass an outside placed application. |
| Admission cycles | List/detail counts include only scoped offerings/applications. Whole-cycle lifecycle, history, validation, impact and capacity operations require year-wide scope. |
| Shared settings | Records policy changes require year-wide scope, or tenant-wide scope for school policy. Academic-term changes require year-wide scope. House configuration changes require medium-wide or broader scope; scoped staff may read their medium's catalogue. |

Existing student/guardian portal association and credential provisioning remain separate from staff browser scope. Manual Academic assignments are retained.

Admission event instants use UTC and timezone-aware persistence parameters. Previously shifted stored audit data is not automatically rewritten; historical correction requires reviewed evidence. Attendance returns canonical `GrNumber` alongside legacy numeric `GrNo` for identifier compatibility.

## Onboarding credential emails and portal access

| Rule | Behavior |
|---|---|
| Configuration | Admission supplies common readiness and published workflow documents automatically. The school's saved student/parent access choices apply to pending cases; new schools default to Student Not needed, Parent Required and manual sending. Activated, completed and cancelled case snapshots remain unchanged. Named readiness rules and Offering links are no longer prerequisites. |
| Mandatory readiness | Current Offering cases require confirmed admission, complete operational Student name/date of birth/gender, applicable required workflow documents verified, Division and GR/Roll. Reusing an incomplete Student cannot bypass the identity check through a complete Applicant. Admission owns approval; there is no separate default onboarding approval. Fee assignment, payment and legacy blanket service-consent/profile requirements do not gate activation or completion. Fee confirmation remains separately visible and pending until genuinely confirmed; activation never marks it Paid or Waived. Credential preparation failure leaves an active enrollment with access pending. |
| Required documents | Canonical published workflow policy determines upload/original evidence and verification. Waivers and document-complete overrides cannot satisfy required evidence. Accepted admission evidence remains available during Onboarding. |
| Account ownership | Email is mandatory. Student gets its own identity; a selected adult uses the Parent role. Confirmed Father/Mother reuse can link multiple children while preserving the existing password. Optional other guardians require explicit authorisation; existing identities/colliding emails retain reviewed repair. Automatic preparation does not create new generic-guardian access. |
| Retained parent links | A verified existing Father/Mother link is validated against the current child's saved owner, tenant, active Parent-only identity and any pinned sibling selection. It does not depend on another child's link remaining eligible for new sibling discovery. The first account prepared for a confirmed-family child remains ordinary manual credential delivery, not sibling reuse; its own link never becomes a new-link candidate. New links still require confirmed family/source provenance and eligible source evidence. Existing identities, passwords and other child links are not rewritten. |
| Delivery | A durable tenant-owned request tracks Pending/Sent/Failed/Cancelled; dispatch rechecks current actor grants/scope, owner mailbox, links, settings and readiness. A new passwordless account receives a cryptographically random 10-character initial password containing uppercase and lowercase letters, numbers and a symbol, and must change it on first sign-in; temporary secrets are encrypted until transport acceptance or cancellation. The configured Identity password policy is still enforced. Existing passwords, including previously issued longer passwords, are not shortened or reset on resend. Staff responses never return secrets. Existing identities keep their password; repeats use owner recovery. |
| Email presentation | The module-local `StudentCredentialEmailTemplate` renders an encoded, responsive Arial credential card with the school code, username, available temporary password and linked Student. It uses the existing primary/info/grey palette and an official, non-clickable Google Play badge while no published app destination is configured. Ordinary credential messages have no verification button, School login link or coming-soon banner. Existing accounts do not receive a replacement password; recovery-only messages retain a small text setup/reset link when needed. Rendering does not change the generated password, authentication, recipient checks or delivery semantics. |
| Account and delivery settings | Not needed disables new preparation/delivery; Optional permits it without blocking completion; Required gates completion on the account and required delivery work. Legacy false/true retains Not needed/Required semantics. Account preparation and delivery eligibility remain separate. Explicitly selecting automatic delivery enables email only for enabled account types; manual is the default and leaves sending to staff. Existing saved automatic choices remain intact. Optional accounts may be requested after completion without reopening the case, and requested delivery remains visible in Credentials until sent. |
| Automatic recipients | Automatic mode can prepare enabled accounts with eligible saved contact details and existing identity grants. New App Login rules with multiple saved parents require staff to select the recipient through Send Credentials. Missing optional contact details do not hold setup open. No new mailbox-verification bypass is introduced. |
| One-click staff delivery | Send Credentials authorizes only the current saved Student or explicitly confirmed parent/guardian recipient, prepares or safely reuses its account and records durable intent in one transaction. Adult manual requests require recipient-access confirmation; this is not mailbox verification. Explicit manual intent may send with automatic email Off without changing saved flags, required-account policy, readiness, timing or identity grants. The scoped App Login command saves missing/unlinked Student/Father/Mother/Guardian email; linked changes require owner verification and wider identity/relationship repairs retain Records review. Guardian-fallback Student email is not eligible. Completion-timed requests wait without generating a password until non-email requirements complete. |
| Exit | Applied Withdraw/Transfer/Graduate transitions set Left and revoke only that child's portal links. Current portal reads and token renewal deny departed children; shared Parent identities retain access to other children. Reviewed corrections restore only recorded links that still match the owner profile and exclusive role. |
| Verification boundary | Sent means the configured email sender accepted delivery. Inbox arrival and the separately maintained mobile-app UI require their own acceptance checks. |

## Universal daily Student Attendance

| Area | Implemented rule |
|---|---|
| Observation | The register offers Full day, Half day, Absent and Pending. The stored observation is Present or Absent, with a HalfDay flag for Half day; Pending means unmarked. |
| Daily visibility | On both current and past dates, Daily Attendance and its work queue show only students eligible for the current Active Student Directory, within the selected dated class and staff scope. Inactive, departed or no-longer-directory-eligible students cannot be marked through Daily Attendance. This filter does not activate or repair Onboarding records. |
| Membership | Formal enrollment and a school-confirmed AttendanceStartDate determine dated eligibility within that visible population. For the current school date, an active student in the selected class may be marked before an imported attendance start date is confirmed. Dated placement/activation/exit history remains authoritative for historical eligibility and saved attendance. Fee or portal provisioning does not independently block operational attendance. |
| Saved history | Removing a student from Daily visibility preserves saved Present/Absent observations, details, credits and enrollment revisions for Reports/History and existing audited corrections. Saving or resubmitting visible students leaves hidden saved observations untouched. Hidden Pending drafts do not block visible submission. |
| School days | Existing published Academic Calendar owns working days, holidays, vacations and emergency closures. The current school date remains expected when calendar or working-day setup is missing, and its unsnapshotted reports remain provisional. Other dates with missing setup and ambiguous calendars fail closed; an explicit holiday is a confirmed exclusion. |
| Settings | Staff manage student leave types by name, color and active status. The former attendance policy and individual-schedule configuration is retired; runtime uses fixed defaults while submitted attendance retains its stored snapshots. |
| Save and submit | Partial drafts are allowed. Submit checks all eligible students, including persisted draft rows, and pins policy/credits/membership/roll snapshots. No unmarked row may be submitted. |
| Corrections | Same-day recorded corrections require a reason. Prior-day or locked corrections require approval. Review cannot be performed by the requester; a locked-day correction also requires lock-management authority. |
| Leave | Approval annotates the roster without fabricating an observation. The fixed leave treatment changes denominator credit after explicit attendance marking. Leave approved after submission requires an audited correction, even when Present/Absent stays unchanged; existing credits remain pinned until application. Conflicting Present observations require a reason; overlapping approved school-day leave is rejected. |
| Reports | Register, profile, daily cumulative metrics and dashboards use the same calculation. Percentage uses recorded eligible credit; unknown days remain separate with completeness/provisional flags. No recorded denominator produces null, never an invented zero percent. Low-attendance comparison uses the fixed 75% threshold. |
| Cutoff | The fixed 14:00 school-local cutoff supplies a reminder for expected unsubmitted days; it does not silently lock attendance. |
| Concurrency | Revision/roster checks and serializable commands reject stale writes. Matching actor/payload retries reuse durable operation results. Enrollment/date uniqueness prevents recording the same student in two divisions. |

For 17 Present, one Absent and two unmarked expected days, the recorded percentage is 94.44%, completion is 18/20, and the result is provisional.
Legacy migration retains original observations and ambiguous conflicts, bridges valid enrolled identities only when formal enrollment is absent, and leaves attendance-start dates unconfirmed.
It never invents historical presence, activation dates, calendar definitions, policy snapshots or provisioning completion.
Parent alerts, offline marking and biometric/RFID integrations are outside this rollout.
Local tests and source availability do not certify deployment or authenticated browser verification.

## Permanent Student Records behavior

| Area | Backend rule |
|---|---|
| Entry | Persisted activated enrollment permits the permanent profile even when credentials remain pending. Completed onboarding or an explicitly approved historical override remains valid historical authority. Unactivated cases follow the school's OnboardingOnly/Summary/Full policy. |
| Identity | The same Student ID and GR record remain across promotion, repetition, graduation, withdrawal, transfer, and return. Historical overrides are audited; duplicate review does not merge profiles. |
| Changes | Action policy chooses audited direct execution or one to three ordered central-role approval stages. Optional conditions narrow approval. Pending requests pin the published policy and source evidence; self-approval is prohibited. |
| Inline profile form | The retained original Registration schema displays safe current answers. Authorized staff can save supported mapped and school-specific fields immediately as one atomic actor/time/reason/before/after audit, without an approval step. This explicit profile command leaves generic correction/approval policy unchanged; original submissions, protected identities, placement and fields outside the form remain intact. |
| Sensitive data | Current database roles govern field groups. Current and historical-year school policy both constrain history and export reads. Free-text reasons and event remarks are withheld when relevant groups are restricted. |
| Parent / guardian changes | A changed linked person's saved profile uses the published SuspendUntilReview or KeepUntilReview policy. Main-contact-only preferences and added unlinked guardians keep unchanged adult links. Resolution uses verified identity and preserves sibling relationships. |
| Documents | IDs and certificates retain issued snapshots, numbering, issue history, reasoned reprint/void actions, and idempotency. Certificates may be generated for all active Student List students in staff scope; onboarding or historical entry approval is not required for their certificate operations. Other Records operations and former-student certificate eligibility retain their existing approval rules. Certificate history groups students generated together and filters by generation month. Leaving certificates for withdrawal/transfer require the configured ApprovedExit or EffectiveExit point. Graduation requires an Applied case whose effective date has arrived, including when ApprovedExit is selected. |
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

Application-window boundaries are calendar dates. Create/edit, clone, window revision and the
optional reopen end date preserve the date written by the client, including offset-bearing
midnight payloads, independently of the API server timezone. UTC midnight is their existing
storage/wire representation; it does not convert the selected date to another timezone.
Scheduled publish/close actions and lifecycle/audit timestamps remain actual instants.
This fix does not infer or automatically shift dates already saved incorrectly; repair a confirmed
affected record through the existing governed edit/window-revision flow.

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

## Workflow Builder publication

Admission workflows publish immediately through the existing Workflow Add-authorized command after
server validation and concurrency checks. Stored maker-checker settings do not gate this command.
The compatibility governed-publish endpoint uses the same immediate command and ignores its old
optional effective time. Workflow-specific notifications are no longer generated, and the Workflow
schedule worker is no longer registered. Historical approval, schedule, notification and audit data
remain stored; a historical scheduled version changes only through an explicit publish command.

Publication continues to record actor/time/status history and supersede a previously published
version of the same workflow. Existing Offerings and Applications remain pinned to their original
versions. Validated workflows can still be edited; the first saved change returns them to Draft and
clears old validation evidence. Published and historical versions remain immutable. Compatibility
approval/recovery endpoints retain their existing authorization; they are absent from the current
builder UI and do not gate immediate publication.

Archived workflows support explicit Re-open of the same ID and version; published content stays immutable.
The latest archive history restores its prior Published or Superseded status; a different currently Published version forces Superseded, preserving that current version.
Legacy archives without prior history restore to Published only when no other Published version exists.
Re-open does not activate Offerings, migrate Applications, create a draft or update original publication metadata.
Publish and Re-open serialize by tenant and workflow code through a PostgreSQL transaction-scoped advisory lock held through commit.
The existing per-record concurrency stamp is checked after acquiring that lock; repeated successful restores with the current stamp do not duplicate audit history.

## Offering identity

The active identity is Admission Cycle, optional Campus, Board, Medium, Stage, Standard, and
admission Track (General, RTE, Management, or Staff). A class may therefore have one offering
per track with a distinct process, while a second offering for the same track and identity is rejected.
Division is not part of Admission Offering identity.
RTE offerings must use direct Registration entry and a published workflow without Inquiry or
Assessment stages. Fresh General admission requires Inquiry evidence.
RTE, Management, and Staff offerings configured for DirectRegistration (or retained Both for Management/Staff) can start through the authorized special Registration command, then continue the same common application workflow, decision and onboarding lifecycle.
Inquiry-first Management/Staff offerings remain compatible with Inquiry intake, not the special Registration command.
New starts and family lookup require an active, open, published-ready Offering in the caller's tenant and academic scope.
The command requires system-owned applicant/primary-guardian identity, explicit scoped family linking and a stable retry request ID; selecting RTE does not automatically approve or finalize admission.
An inactive RTE draft may omit
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
New Offering clones always inherit the target cycle's dates, even when the source has custom overrides.
The source remains unchanged, and later controlled target-cycle revisions automatically change the clone's effective dates.
Generic Offering create/update API calls retain validated custom-date compatibility.

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
Intake setup, automatic/manual counsellor assignment and Onboarding case ownership are retired. New cases carry no designated owner; authorized staff act within their existing tenant and academic scope. Legacy assignment columns and audit records remain intact.
All intake profiles, workflow policies, and persisted snapshot reads use standard behavior: contact and qualification do not gate application start, source detail is optional for every source, and policy-level guardian email and applicant date-of-birth requirements are disabled.
Stored historical JSON and entered data are preserved; published form requirements remain authoritative.
Automatic duplicate detection remains enabled; a separate family on an actual current-cycle duplicate requires a reason, while an eligible sibling can link to its existing family.
The explicit sibling flow supports Father/Mother only and treats a matching saved parent email/mobile as a family suggestion, not proof of relationship or portal authorization.
Staff confirms the existing family and source parent; a changed mobile with the same email is allowed after this confirmation without changing the shared parent's stored details.
Each child retains a separate admission identity; generic historical family links and public submissions do not gain trusted sibling-account provenance.
Current parent profiles take precedence over stale captured admission contact values; existing duplicate and finalization identity policies remain unchanged rather than treating a matching name/date of birth as conclusive identity.
First-contact and qualification deadlines are snapshotted when the inquiry is created.
Follow-ups retain purpose, due time, completion/cancellation, actual actor and audit timestamps without requiring an assignee. Historical follow-up timing settings remain readable. New activity/event/transition writes capture staff ID and display name on the server; completed steps expose their recorded handler. Anonymous/system actions and missing historical names remain explicit.
Inquiry staff can create a linked family communication for WhatsApp, SMS, or Email. The CRM records the campaign handoff in admission activity while the shared Communication module owns approval, consent, providers, delivery attempts, retries, and usage; no duplicate Admissions messaging store is introduced.

Application Experience creates Inquiry and Registration forms. Correction requests reopen the submitted Registration form, and consent questions may be included in that form. Offering activation accepts only a published Inquiry form in the inquiry slot and a published Registration form in the application slot. Admission lifecycle content is stored as `Admissions lifecycle` Communication templates for inquiry acknowledgement, application receipt, missing documents, exam schedule, hall ticket, result, offer, payment reminder, and finalization. Communication remains the delivery owner.
Qualification uses a versioned checklist and records Qualified, Nurture, Not interested, Lost, or Duplicate with a reason where required.
Application start rechecks the linked inquiry, offering, and scope on the server.
Existing inquiries and workflows remain compatible; management is created only for an enabled policy attached to the selected offering's published workflow.

## Managed assessment stages

New saved Assessment settings enable managed results by default, with optional scheduling, attendance, aggregate marks/pass thresholds, Maximum attempts, bounded retakes and existing authorized waivers. Independent result review is disabled. The frozen policy and attempt history remain authoritative.

Subject-wise marks are an opt-in version 2 mode inside the existing Assessment stage.
Each Board/Medium/class has exactly one profile, containing mandatory school or custom entrance subjects and their maximum marks.
Published Academic subject assignments are suggestions only; any active subject in the profile's Medium may be selected.
The school selects overall marks, every subject, or both passing rules.
One combined exam retains one booking, attendance and attempt, including when a shared session serves different class profiles.
The Offering determines the applicant's profile; booked attempts freeze names, subjects, marks and pass rules.
Partial draft scores do not complete the assessment and blanks are never zero.
Final submission requires every configured subject and calculates the outcome on the server.
Retakes create a fresh whole-exam attempt without carrying forward subject scores.
Existing version 1 single-score and qualitative outcomes retain their behavior.

Sessions belong to the Offering and workflow stage; booking enforces capacity and application/session concurrency. Scheduled sessions must have started before attendance/results. Scored results are derived by the backend; unscored results need an explicit outcome. Failed, absent and retake outcomes require matching routes to leave the stage. Recording a result does not advance automatically.

Assessment does not approve admission or create a Student. Approve, Waitlist and Reject belong to the active Admission decision stage. Approved plus a completed journey can finalize without Offer acceptance, preserving guardian/identity, duplicate, capacity, tenant, academic scope and concurrency checks. Finalization links the Student and Onboarding case; activation owns operational enrollment. Current Offering-linked Onboarding confirms a fee structure without payment or service-consent gates. Historical route-based and Records renewal completion retain their separately documented owner rules.

## Managed document verification

New saved Documents settings enable one upload checklist by default. Requirements are Required or Optional, with class applicability, PDF/JPEG/PNG formats, upload limits and optional Student document category. Hidden requirement codes are generated uniquely by the backend and remain stable on later saves. Independent review, waivers, earlier Form collection and physical-original/evidence-mode setup are disabled.

Every upload/replacement and acceptance/rejection remains audited against the current application stamp and latest revision. A replacement requires fresh acceptance. Applicable required uploads block advance, skip and override until accepted; optional evidence does not block completion. Class applicability uses the Offering Standard, not an applicant-typed answer. Finalization rechecks visited evidence and preserves idempotent finalized retries.

Accepted mapped uploads remain canonical and can satisfy corresponding Onboarding upload requirements without overwriting existing Student documents. Unmapped custom evidence does not satisfy an independently mandatory onboarding category. Historical revisions remain readable within record scope.

## Managed eligibility

Eligibility, Review, and Offer are retired Admission Stage types. New drafts cannot select them, publication rejects them, application workspaces do not mount their working panels, and their direct admission operations reject unsupported stages. Existing enum numbers and stored audit records remain intact. Inquiry qualification, Student Records reviews, class capacity, and Finance ownership are separate contracts.

## Managed Review

Eligibility, Review, and Offer are retired Admission Stage types. New drafts cannot select them, publication rejects them, application workspaces do not mount their working panels, and their direct admission operations reject unsupported stages. Existing enum numbers and stored audit records remain intact. Inquiry qualification, Student Records reviews, class capacity, and Finance ownership are separate contracts.

## Managed Form stages

Form defaults to managed Submission with rules with applicant Save Draft retained. Optional responsibility, deadlines, late submission and corrections use existing validators. Submission limits remain backend safeguards, not an editor field. Separate section verification is retired: new stage normalization clears its flags and effective runtime policy ignores legacy verification requirements. Valid submissions advance when configured; legacy manual-continuation settings still allow Continue after authoritative validation.

## Managed Payment stages

Payment is a fixed Finance collection checkpoint. Published structures must match the tenant and Offering academic-year/Board/Medium/Class. Selection saves the versioned structure and mandatory/optional heads with application concurrency, without creating Finance obligations. Ordinary continuation requires a valid selection plus any confirmed Finance payment or Finance-verified zero payable. Missing/unprepared balances and historical/unverified payment flags alone are insufficient. Active-stage collection deferral is available with its mandatory reason. Partial/Full and allowCollectionLater configuration are retired: runtime ignores old saved settings, new draft normalization strips them, and existing published definitions/history are not rewritten. Finance module code, permissions and monetary contracts remain unchanged.

Actual fee assignment and collection remain Finance-owned. Offering-linked Onboarding reads/selects/confirms the existing structure only. This confirmation is never a Paid status. Admission payer, purpose, independent amount, collection settings and financial transaction actions are removed from the supported stage.

## Managed Offer stages

Eligibility, Review, and Offer are retired Admission Stage types. New drafts cannot select them, publication rejects them, application workspaces do not mount their working panels, and their direct admission operations reject unsupported stages. Existing enum numbers and stored audit records remain intact. Inquiry qualification, Student Records reviews, class capacity, and Finance ownership are separate contracts.

## Managed Seat stages

Class-level admission capacity is a separate warning-only projection: for an academic year and Standard, sum the capacities of assigned Institute Setup rooms and subtract active enrollments plus accepted or finalized admissions not already enrolled. General, RTE, Management, and Staff offerings for the same class share this count. A full class warns staff at inquiry or registration but does not stop either action. No room assignment means capacity is unknown. This does not change the historical managed Seat-stage reservation and waitlist policy below.
An Enrolled application does not hold a second seat after its active StudentEnrollment is deactivated;
transfers and withdrawals free that class seat in the live projection.

Seat stages opt into `configurationJson.seat` version 1. Schools set the offering capacity, choose manual reservation or reservation on stage entry, define hold duration and staff override limits, enable bounded renewals and FIFO waitlisting, control authorized capacity exceptions, require release reasons, and choose which outcomes may complete the stage. Missing or disabled settings preserve the basic workflow step.

One tenant-scoped pool owns reserved and confirmed counts for each Admission Offering. Each application and frozen Seat stage owns at most one reservation record. Confirmed seats remain allocated after stage completion until admission finalization or an authorized reasoned release. Expired holds stop consuming effective capacity and are reconciled into permanent history on the next Seat command.

Managed Seat cannot be skipped. Confirmation is always an allowed completion outcome; waitlisted, released, or expired completion requires both policy permission and an explicit matching `seat.status` transition. Every mutation checks application, pool, and reservation concurrency, and repeats with the same idempotency key return the recorded result.

The Form-stage definition must equal the Offering application form. Applications still pin the published definition and record immutable submission snapshots. Managed submit enforces visible required fields, deadline, and attempt limits. Automatic advancement occurs only when configured. Historical verification and reasoned revocation records remain auditable but do not gate current Form completion. A correction request reopens the same form; the applicant must resubmit within the remaining attempt limit. Normal completion cannot bypass unresolved Form blockers.
## Division roll-number batches

Published Onboarding roll rules support Mixed, Boys First and Girls First grouping, first-name-first or surname-first names, and ascending/descending name, GR, admission-date or admission-number ordering.
Mixed ignores gender; grouped ordering blocks missing or unsupported genders rather than guessing.
Missing selected source values block the full proposal with actionable row feedback.
The same ordering evaluator serves initial Onboarding, active Records and annual transition cohorts.

Each academic-year Class-Division starts independently at 1.
Assign Missing reads the complete division on the server, preserves assigned numbers and appends after the persisted last-issued cursor; exits and cancelled reservations do not free that cursor.
An optional bounded onboarding-case target list limits assignment to eligible unassigned cases on the chosen page; omitted targets retain full-division behavior.
Published ordering, full-division reservations, freshness checks and active-enrollment approval remain authoritative for either scope.
The upgrade cursor baseline uses retained enrollment, onboarding, renewal, transition and placement audit history; records deleted before this release cannot be reconstructed.
Only an explicit authorized Renumber Division resets the current sequence, requires a reason, and respects individual/group identifier locks.
Existing Records placement approval applies once to a mixed or active-enrollment batch, with independent current-role approvers and atomic application after approval.
Renumbering uses the application day's school-local date and does not rewrite submitted attendance, issued documents or historical enrollment facts.
Pending return/promotion reservations and unresolved provisioning block unsafe renumbering.
Finance ownership and monetary behavior are unchanged.

## Student dashboard

One dashboard serves all authorized identities. Persisted roles select admin, leadership, teacher or common priorities; a role label never grants data access. Existing Dashboard View, exact destination action grants, submodule visibility, active database identity, tenant and current UserScope constrain every section read.

| Section | Definitions |
|---|---|
| Records | Enrolled, inactive and left permanent records eligible through activated enrollment, completed onboarding or approved legacy override; selected-year scoped enrollment membership. Access-pending activated students remain visible. Left legacy records without enrollment retain their own-year directory fallback. Class strength counts distinct eligible active students in active scoped enrollments, bounded to the 12 largest classes. |
| Admissions | Open inquiry statuses New/Contacted/Interested/FollowUp; selected-year scoped cycles. Active applications in scoped offerings exclude terminal outcomes. Admin/leadership summarize allowed scope; other staff inquiry and setup counts additionally require assignment to the current identity. Applications remain scoped and are labelled as visible applications. |
| Student setup | Unfinished excludes Completed/Cancelled; ready means ReadyForActivation; attention means NeedsAttention or an overdue unfinished case. Provisioning remains unfinished. |
| Attendance | Saved register counts grouped by submitted/draft status and pending leave requests within selected-year enrollment/division scope. These are year work summaries, not daily completeness or attendance percentages. |

Student/Parent-only database roles receive names and relationships from persisted verified portal associations, including existing self-link revocation rules; they receive no staff sections or shortcuts. Mixed staff/portal roles follow staff grants and scope. No grants, seeders, snapshots or migrations are created by a dashboard read. Existing Attendance reports remain authoritative for credit, percentage, cutoff, provisional and missing-day behavior.

## Inquiry-to-Registration field inheritance

Inheritance is an explicit versioned Registration setting. Offering references still determine Inquiry and workflow; the workflow supplies the one Registration form. Multiple Inquiry forms can share Registration, and separate routes can use other Registration forms. Approved source versions are checked against the application's exact captured Inquiry, rather than names, class ranges or the latest published version.

Composition includes only that source's questions plus common Registration questions. Registered Student binding identities are deduplicated; custom identities retain lineage or explicit reviewed equivalence. Source labels/translations, types, options, visibility and repeatable structure remain authoritative. Registration can strengthen requiredness and adjust compatible placement, but cannot independently delete, rename or change inherited types/options. A read-only field cannot become newly unanswerably required. Inquiry-only bindings remain read-only source context and do not become editable Student bindings.

Application start freezes the effective schema and safe source context in the existing form instance's extra properties. Rendering, writes, submission validation and retained Records evidence use that schema. Existing applications without a snapshot remain legacy; a corrupt enabled snapshot fails visibly. Registration edits and explicit clears are retained, source-only questions do not leak between paths, and later publications do not rewrite existing instances or submission history. No schema migration or automatic historical backfill is introduced.
