---
module: hr
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-10-05
owner: backend-team
audience: humans-and-agents
---

# Trace HR backend workflows

TL;DR: use this page to understand how HR actions move through employees, attendance, leave, policies, payroll, and reports.
Each workflow points to the API and testing pages that validate it.

## Employee workflows

Employee workflows create and maintain staff records:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Employee onboarding | Validate the submitted application, create the identity user and employee aggregate, assign an explicitly onboarding-approved Employee-audience role compatible with the employee group, apply policy defaults and teaching scope, finalize in one unit of work, and run profile, event, and welcome-email effects after commit | Terminal application status, Category and Family Caste, duplicate contact data, role approval and employee-group audiences, BulkAssign permission for teaching scope, tenant scope, academic hierarchy, rollback, generated employee code, and deferred effects |
| Employee profile update | Update personal, family, additional, work experience, payroll, reporting, and academic data | Round-trip persistence, date validation, bank validation, manager validity, and role compatibility when the role or employee group changes |
| Employee status change | Submit target status and remarks, then persist transition and audit metadata | Required remarks, blocked transitions, idempotent same-status behavior, and payroll or attendance eligibility impact |
| Employee documents | Upload, list, download, replace, and delete configured document types | File size, file type, magic bytes, blob storage, and tenant scope |

## Attendance workflows

Attendance workflows create payroll-relevant facts:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Manual attendance | Select date, update employee statuses, save day facts, and optionally lock day | Lock behavior, scoped employee set, status mapping, and payroll preview invalidation |
| Attendance tracking | Query date-wise or employee-wise attendance and grouped register rows | Pagination, date range, scope filtering, and adjustment behavior |
| Attendance import | Preview file, classify rows, confirm rows, and create batch audit records | Header parsing, employee matching, single payroll month, warnings, and batch status |
| Automated attendance run | Read automated run rows, update row state, and lock or unlock batch | Device-run audit, row replacement, batch lock, and payroll log mirror |

Manual attendance saves synchronize attendance-owned teacher unavailability inside the authorized, employee-scoped save workflow. The synchronization service uses a unit of work and preserves overlapping records from other sources. An authorized HR operator does not need the administrator role to persist this derived fact. Present cancels only attendance-owned unavailability; it does not cancel substitution duties.
The HR quick-substitution workflow uses the existing Academic APIs: affected periods require Timetable Management View and are filtered by UserScope; creation requires Timetable Management Add at the application-service boundary and validates the target scope and current substitute/room conflicts. Attendance permission alone does not grant assignment authority. Teacher conflicts recognize both employee IDs and linked user IDs in published timetable slots.

## Leave workflows

Leave workflows use policy rules and affect payroll:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Leave policy setup | Configure paid and unpaid rule buckets for an academic year | Required paid type, finite or unlimited balance, half-day rules, and duplicate rules |
| Leave request | Submit date range, paid type, reason, and optional attachment | Balance validation, date validation, overlap checks, and employee eligibility |
| Leave approval | Approver accepts or rejects request and updates balance or status | Authorization, approver chain, rejection reason, and payroll impact |
| Leave center | Show balances, history, and request state for one employee | Accrued days, remaining days, unlimited unpaid display, and negative-balance rules |

## Payroll workflows

Payroll workflows depend on employee, attendance, leave, and policy data:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Payroll setup | Create earning, deduction, provident fund, professional-tax, ESI establishment, salary-template, pay element, and payroll policy records | Tenant scope, effective dates, active status, statutory wage classification, same-tenant active dependency targets, acyclic earning dependencies, template gross reconciliation, and destructive-change impact |
| Compensation revision | Quote recurring salary, calculate, save directly, and resolve by effective month | Template version, effective statutory rules, controlled overrides, idempotency, concurrency, locked payroll, and immutable effective snapshots |
| One-time components | Assign temporary earning or deduction lines to employees | Active components, amount ranges, applicable month, and reload consistency |
| Payroll preview | Resolve the effective revision, append one-time lines, read attendance and leave facts, compute basis days, and calculate gross, deductions, net pay, and employer cost | Month format, effective compensation readiness, policy basis, proration, statutory provenance, blocking protected-target drift, and cached preview freshness |
| Payroll review | Mark rows reviewed and add adjustments before lock | Permission checks, row state, audit, and adjustment totals |
| Payroll lock | Finalize selected rows and create salary slip snapshots | Idempotency, already locked rows, snapshot basis, and error reporting |
| Salary slip | Read slip data or PDF for employee and month | Locked snapshot existence, totals, policy basis, and PDF access |

The salary builder preserves full-month earnings for Monthly salary or solves earnings plus employer contributions within Monthly cost to school (CTC).
The solver partitions professional-tax slabs and ESI coverage boundaries and evaluates the same statutory calculators used by payroll.
It calculates Basic and other earnings, assigns the balancing earning, calculates employee deductions, and returns employer-funded contributions separately.
Salary Template settings opens one table of Active, Needs setup, and Archived templates.
Selecting an editable row opens its URL-backed editor directly in edit mode.
Create and Save changes require a complete valid template and atomically create the active immutable version.
Saving an existing template creates version N+1 and marks version N as Replaced for future assignments only.
Existing employee compensation stays pinned to the version used when salary was saved.
New employees are created Inactive and may complete onboarding before salary is configured.
Salary setup preselects the school default template, its quote defaults and PF defaults, and the employee joining month.
The administrator calculates the salary and saves it directly as an effective immutable compensation revision.
Saving another salary for the same month supersedes the older effective revision, while a future-effective revision remains scheduled.
The revision records effective month, template, quote, permitted overrides, and statutory provenance.
Employee activation is a separate action and uses the server activation-readiness check.
The readiness check requires a valid effective revision covering the later of the joining month or current payroll month.
Pending registration is a warning and does not block activation when the salary and calculation rules are otherwise valid.
HR can save registration details while the employee is inactive using the existing scoped enrollment endpoint.
Readiness recalculates eligibility from saved earnings and applicable ESIC configuration, so off mode and employees outside coverage do not require a number.
Later template or statutory changes do not silently change saved compensation.
One-time earnings and deductions remain separate employee salary components and do not become revision lines.
ESIC uses the school-level default-on configuration without an activation journey.
Save applies directly to current and future unlocked payroll with a concurrency stamp and idempotency key.
School registration facts may be completed independently of salary calculation.
The optional employee table projects the complete scoped population with server paging and attention filters.
An active ESI scheme is evaluated for every payroll employee even when the selected salary template has no ESI row.
ESI coverage above the entry ceiling continues only through the recorded contribution-period end or an audited override.
Derive continuity from prior finalized or paid payslips in the same contribution period, including when their payroll snapshots remain unlocked.
Use locked snapshot evidence only where that employee-month has no finalized or paid payslip; drafts do not establish continuity.
PwD employer-share relief applies only to the PwD wage-ceiling category with a recorded start date and disability evidence.
The employee contribution remains a deduction, while an eligible employer share is recorded as government funded and excluded from employer cost.
Users with override approval permission can review immutable exception history and add a dated exception with a mandatory reason and optional evidence reference.

Finalized and Paid salary slips are both immutable payroll facts and both block attendance, policy, component, preview-refresh, and month-lock changes that would invalidate their snapshots.
An exiting employee remains eligible for the final employment month and is excluded from following months.
Payroll and correction preparation filter attendance and approved leave using the existing inclusive employment lifecycle boundary.
This prevents retained post-exit punches, overtime and paid leave from increasing earned pay after a retroactive exit.
Source attendance and leave records remain intact, and locked or issued payroll stays immutable.

## Policy workflows

Policy workflows resolve effective behavior for employees:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Work calendar policy | Define academic-year calendar and weekly-off behavior | Years 1900 through 2100, at least one working day, holiday sync mode, and default behavior |
| Attendance policy | Define punch, missing punch, late, early, overtime, and holiday work rules | Evaluation results and holiday behavior |
| Payroll policy | Define day calculation, Saturday units, manual month days, and payroll holiday overrides | Payroll basis days and locked-month safety |
| Policy assignment | Assign policy to employee or group with effective dates and priority | Overlap detection, specificity, priority, and preview explanation |
| Policy versioning | Compare, restore, clone, archive, and audit policy versions | Immutable snapshots, semantic holiday changes, readable weekly-off days, and restore-as-draft behavior |

Archived work-calendar, attendance, payroll, and leave policies release their name for reuse while active and draft records remain protected by tenant- and academic-year-scoped unique indexes.
Current and archived policy workspaces use independent server-side search, count, and pagination so accumulated history cannot hide live policies.
Leave policies are restored as drafts, and restoration stops with the standard duplicate-name error if a current policy has reused the archived name.
Normal leave-policy create and update calls reject Archived status, and only the separately authorized archive workflow may archive a current policy.

## Reports and notifications

Reports and notifications expose HR data outside operational screens:

| Workflow | Main steps | Validation focus |
|---|---|---|
| HR report preview | Select source, options, template, and filters | Filter correctness and tenant isolation |
| HR report export | Generate PDF or Excel export, filter history by requested date, and remove an owned failed job | Export parity with preview, wide-PDF layout, owner and tenant scope, soft deletion, and audit metadata |
| HR notifications | Configure employee, attendance, leave, and event notification rules | Channel settings, permission checks, and history visibility |

When an export is queued, retried, regenerated, or run from a schedule, the Reports Center clears the history date filter before highlighting the new job.
An inverted custom history range does not issue an unfiltered export-history request.
PDF history metadata reports the capped row count and the document tells the user to choose Excel for the complete result.
Direct sharing and schedules reject a recipient unless the recipient has report-view permission and a current tenant UserScope that covers the export owner's complete generation scope.
Schedule execution rechecks the owner's report-view permission and every recipient's permission and scope before generating or distributing an export.
The schedule worker processes each due schedule in an independent transaction, pauses permanent authorization or validation failures, defers transient failures with bounded retry delay, and continues with later schedules.
Email delivery begins only after the export and schedule transaction commits, then uses a fresh tenant-scoped read unit of work and contains delivery failures as best-effort notifications.
Every owner or shared-recipient export read, share, or download repeats the scope check so later permission or scope changes cannot expose a previously generated artifact.
Export workers load the current persisted request and generation-scope snapshot instead of trusting queued argument copies, so a stale cancelled invocation cannot bypass a narrower retry scope.
Each retry receives a new generation identity and generation-specific blob name, and only the still-active generation may publish bytes or change the job outcome.
The worker persists a current owner-scope snapshot before generating any legacy queued job that predates snapshot storage.

## Partial workflow rule

If a workflow depends on a commented menu item or unconfirmed route, mark it partial in docs and tests.
Do not present partial routes as shipped navigation until frontend `hr-menus.ts` confirms visibility.

## Built-in Gross Salary

Full-month gross is resolved from the employee compensation basis and saved with the immutable revision.
The attendance-adjusted Gross Salary equals full-month gross multiplied by payable days divided by salary basis days, using the existing school policies.
Regular earnings must allocate this value completely, with an optional explicit remainder earning.
Gross-based earning and deduction formulas run against this value and receive no second attendance reduction.
One-time bonuses and incentives are additional earnings, excluded from this base and included in total earnings and net pay.
Statutory calculators retain their existing wage treatment and effective rules.
Payroll snapshots and payslip lines retain monthly amounts, formula types, formula values, and component dependencies.

ESIC school settings use direct save with no month picker, preview confirmation, or activation journey.
Changes apply to current/future unlocked payroll; prior and locked figures are preserved.
New templates default to CTC, with Monthly salary as the alternative and authorized employee overrides.
The chosen basis determines the compensation target; there is no independent protection selector.

## ESIC continuity invalidation

The payroll lock and payslip finalization workflows invoke EsiContinuityInvalidator within their evidence transaction.
Only later months in the same contribution period are affected.
The service removes affected unlocked snapshots and review markers and supersedes dependent preview runs.
Immutable payslips protect their employee-month even when the snapshot remains unlocked.
Payroll preview publication rechecks persisted run state under the tenant fence before writing snapshots.
Regeneration is required before reviewing or locking superseded results.
Registration pending permits salary preparation, payroll locking and payslip finalization.
Eligible employee and employer contributions apply before identifiers are available; adding identifiers later does not change amounts.

## Issued payslip documents and delivery

The [payslip acceptance specification](references/payslip-design.md) owns the completion requirements and remaining work.
Payroll locking and direct finalization persist frozen document facts, PDF bytes and a SHA-256 checksum in the same transaction as finalization.
Configured school name and address are required for draft PDFs and official issuance.
Draft PDFs use the saved school facts rather than the tenant display name or employee name.
Missing school facts block document rendering with the existing actionable error while payroll preparation remains available.
Direct finalization repeats current-preview, review, setup and insurance checks under the tenant payroll fence.
After persistence starts, an issuance exception aborts the lock transaction instead of committing a partial slip or delivery intent.
Issued downloads read stored bytes; changes to employee, bank, school or renderer facts cannot silently alter them.
The preview endpoint also returns those stored bytes once a revision is issued.
Payslip request diagnostics retain employee/period/status/timing fields without earnings, deduction or net-pay totals.
The document labels the recorded payroll-basis-minus-payable aggregate as Non-payable days because it includes employment-boundary days as well as unpaid attendance.
This label applies to newly issued documents without rewriting earlier stored PDFs or changing their payroll facts.
Payment status, date/reference and actor are recorded separately and never regenerate the issued artifact.
Payment timestamps use PostgreSQL microsecond precision so identical retries remain idempotent after a reload.
Reasoned document-detail replacement retains monetary/attendance/statutory evidence and creates another immutable revision.
Older revisions remain readable, but manual sending and mutating actions target only the current revision.

School automatic email is off by default and applies to subsequent successful issuance events.
Issuance stores a delivery intent and background job within its transaction.
The worker revalidates actor permission, employee scope, tenant, current revision and recipient before attaching the exact stored PDF.
Automatic delivery from payroll locking records and rechecks RunPayroll.Lock, independently of the RunPayroll.Add permission used to calculate and review payroll.
Direct payslip issuance and manual Send/Resend retain GenerateSalarySlip.Add as their delivery action.
Concurrent workers serialize under the tenant fence.
Provider acceptance is recorded separately from inbox delivery.
Processing attempts receive a delayed recovery job; an ambiguous crash or SMTP failure becomes UNCERTAIN without an automatic resend.
Delivery failure does not roll back finalized payroll.
Only successfully issued employees in a partially successful lock receive automatic delivery intents.
Changing automatic email does not cancel previously committed intents; subsequent issuance uses the saved setting.
The user approved Identity-managed account email changes on 2026-10-06.
HR uses the dedicated employee account-email operation, requiring both EmployeeList.Edit and AbpIdentity.Users.Update, a reason, explicit address verification and a current concurrency token.
The transaction serializes with recipient selection and delivery, changes the employee and linked Identity email together, records the old/new values and revokes persisted credentials.
If the username matched the old email, the new email becomes the sign-in name; a distinct existing username is retained.
An acknowledged request can be retried without undoing subsequent changes, and a changed payload or stale account token is rejected.
The generic personal-info immutability check remains in force, and generic Identity/profile email edits cannot disconnect a linked employee's address.
Audit or Identity failure rolls back the account, employee, security stamp and credential revocation; mutable credential caches are evicted immediately and after commit.
Recipient repair leaves finalized payroll and issued PDF bytes unchanged.
The employee write participates in the existing conservative payroll input-revision tracking, so unlocked previews and reviews may require ordinary recalculation before locking.
Retrying a previously failed delivery requires an explicit new delivery attempt after correction, while replaying its old key returns the original failure without sending.
The account workflow sends no address-verification message and does not relax the host's existing bearer-token validation policy.

Unpaid monetary correction starts a reasoned draft with recorded attendance and an immutable copy of the source calculation evidence.
Draft calculation reuses PayrollSnapshotRefresher without overwriting the issued snapshot or derived system adjustments.
HR can correct clock-in and clock-out alongside attendance status to recalculate overtime and lateness through the same payroll services.
The correction calculation rejects supplied punches from another attendance date and reversed or equal time ranges, while preserving status-only records, missing punches and the explicit absent-day midnight pair.
The existing same-day attendance clock contract applies; multi-day and overnight punches are not inferred or silently normalized.
Review and reissue recalculate under the tenant transaction fence and reject changed statutory or ordinary input evidence.
Reissue applies the existing component, total and missing-insurance guards, replaces unpaid payroll and stores the next PDF revision atomically.
The original PDF and correction evidence remain readable.
Subsequent corrections start from the latest applied correction attendance, including after a non-monetary document replacement, so recalculation cannot silently restore older raw attendance.
The resulting payroll evidence identifies its attendance source as payslip_correction.
Cancellation retains the draft and allows payment to proceed; repeated cancellation returns the same cancelled record.
Pending corrections block payment and non-monetary replacement with an actionable error.
Corrected ESIC evidence invalidates later unlocked contribution-period calculations, including when coverage is removed from the source.
Executed correction continuity, fault-injection and authenticated browser cases are recorded separately in the specification ledger and implementation handoff.
On 2026-10-07 the user deferred DOC-15 current Gujarat legal-format verification for both private unaided and government-aided schools and asked engineering to proceed with the existing document.
This deferral does not change statutory calculations, signature fields, immutable issued records or authorization guards, and does not establish legal certification.
Paid corrections retain the paid original and stage signed source-component differences in a selected later unlocked payroll.
Settlement validates source review freshness, target membership, current preview evidence, reconciliation and recovery constraints under the tenant fence.
It supersedes target previews and removes the affected employee's snapshot and reviews, requiring ordinary recalculation, review and locking.
The ordinary payroll calculation applies each committed correction once after attendance and statutory calculation, so source-period differences are neither prorated nor assessed as new target-period ESIC wages.
Subsequent corrections start from the previous applied correction's logical source evidence and attendance without changing the paid snapshot.
Corrected source coverage is used when resolving contribution-period continuity.
Employee contribution increases require corresponding positive source-period earnings; employer contribution differences never become employee deductions.
Recovery validates the aggregate deduction limit against supported statutory wage classifications and rejects unknown custom earning classifications.
The current recovery classifier supports canonical Basic, DA, retaining, special and medical allowance names, with HRA and transport exclusions and the statutory inclusion floor.
Unknown custom recovery classifications return an actionable error rather than an invented statutory treatment.
Executed statutory, security and concurrency cases do not establish browser or jurisdiction-specific legal acceptance.
The document-detail replacement operation is not a monetary correction mechanism.

## Review a full-month salary estimate

HR selects a staff member and preview month to see saved gross, effective salary date and current statutory decisions.
The summary uses the saved earnings distribution without silently upgrading the salary template or rebalancing agreed gross.
Salary-template previews and saved summaries use the same professional-tax rule ordering and configured cessation-month exemption as monthly payroll.
Missing rules or wage classifications remain actionable blockers; missing registration numbers remain follow-up warnings.
Actual attendance, loss of pay and one-time adjustments belong to Run Payroll.
Existing unlocked payroll receives corrected contributions on refresh and changed rows require renewed review.
Locked payroll and finalized or paid payslips remain immutable.
