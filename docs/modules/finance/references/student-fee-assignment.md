---
module: finance
domain: student-fee-assignment
content_type: Reference
status: canonical
updated_on: 2026-11-03
owner: backend-team
audience: humans-and-agents
---

# Student fee assignment contracts

Assignment selects one published class/year structure, calculates included heads, previews the result, and commits its evidence with its dues.
An explicit never-paid correction may supersede an unused assignment schedule while preserving its prior evidence.
Concession configuration, eligibility and allocation are separate operations.
Assignment creates no receipt, posting, payment, late fee or refund.

## Eligibility and academic history

The persisted `Student` academic placement and `Enrolled` lifecycle status are the authoritative source for creating an assignment. The domain's `StudentEnrollment` aggregate has no EF mapping and is not queried by this implementation. `EnrollmentId` identifies the source academic record, not a newly created enrollment. Inquiry, registration, inactive and left students cannot receive new assignments.

Historical reads use `StudentFeeStructure.StudentId + AcademicYearId`, including after the student's current year changes. The structure supplies the historical class; no old division is inferred. A division-only user without adequate historical class scope is therefore denied. ABP tenant filters stay enabled. The server additionally checks the user scope hierarchy against the requested academic year.

The student list returns full name (including middle name), canonical string GR number with legacy numeric fallback, nullable roll number, class/division, separate class/division names, board, medium and stage, plus enrollment/assignment status. Academic labels come from the scoped placement's class. Historical records omit roll number and division because those values are not preserved by the assignment. No current-year roll number is projected into a historical year.

`Filters` accepts the shared Finance table JSON contract; `SortBy` and `SortOrder` (`asc`/`desc`) use an allowlist of `studentName`, `grNumber`, `rollNumber`, `standardDivision`, `className`, `divisionName`, `boardName`, `mediumName`, `stageName`, `isActive` and `isFeeStructureAssigned`. Status enum values follow label order: enrollment `0 = Active`, `1 = Inactive`; assignment `0 = Assigned`, `1 = Unassigned`. Search is case-insensitive across full name, GR number and roll number. Filters apply after tenant/year/user/academic scope and before counting, sorting and paging. Sorts use the displayed field plus a stable enrollment identity tie-breaker. Numeric roll filters exclude missing values. Unsupported columns/operators/values are rejected. The query is read-only and does not change assignment eligibility, dues, receipts or stored student details; no migration is required.

## Endpoints and permissions

All routes below are under `/api/student-fee-structure`; the client base URL already contains `/api`.

| Method / suffix | Permission | Result |
|---|---|---|
| GET `students` | `Finance.Fees.StructureAssign.View` | Paged student academic records with assignment status for the selected year; year, board, medium, standard, division and search filters |
| GET `candidates` | `.Add` | Published structures matching the accessible active student/year/class |
| GET `transport-sources` | `.Add` | That student's verified route-assignment periods overlapping the year |
| POST `preview` | `.Add` | Authoritative heads, service evidence, installments, selected policy summaries, totals and protected preview token; no writes |
| POST `preview-bulk` | `.Add` | Independent preview/error for each student |
| POST `assign` | `.Add` | `assignmentId`, outcome `Assigned` or `AlreadyAssigned`, and canonical assignment snapshot |
| POST `assign-bulk` | `.Add` | Independent committed, already assigned, skipped or failed outcomes and counts |
| GET `student/{studentId}?academicYearId=<year>` | `.View` | Saved evidence plus current canonical due balances; no repair or regeneration |
| GET `policy-options?studentId=<student>&academicYearId=<year>` | `.View` + `.OverridePolicies` | Latest active, effective versions for each policy in the accessible student's academic year |
| PUT `student/{studentId}/policies` | `.View` + `.OverridePolicies` | Concurrency-protected update of concession, refund and collection selections; historical assignment proration stays pinned; no obligation regeneration |
| POST `student/{studentId}/optional-service-stop/preview` | `.View` + `.StopOptionalService` | Count and total of eligible future installments and a protected confirmation token |
| POST `student/{studentId}/optional-service-stop` | `.View` + `.StopOptionalService` | Apply audited non-cash cancellation credits to the previewed installments |
| GET `student/{studentId}/optional-service-restart/context` | `.View` + `.Add` + `.StopOptionalService` | Available dates, saved monthly rate, active proration versions, published monthly terms and available credit for the stopped optional fee |
| GET `student/{studentId}/history` | `.View` | Newest-first assignment history for the scoped student and academic year |
| POST `student/{studentId}/optional-service-restart/preview` | `.View` + `.Add` + `.StopOptionalService` | Server-calculated monthly installment breakdown and a protected confirmation token |
| POST `student/{studentId}/optional-service-restart` | `.View` + `.Add` + `.StopOptionalService` | Add a new service period and its canonical dues in one audited transaction |

The abbreviated permission suffixes refer to `Finance.Fees.StructureAssign`.
Fixed amounts are read-only. A correction preserves an existing fixed amount on the same structure, including a previously saved override. A correction preserves the saved service dates for required heads on the same structure. Required heads on a replacement structure use verified defaults. Optional service date changes require the existing authorization. Variable heads accept a term amount or a monthly rate where supported.
Manual dates require the existing `Finance.Policies.ProrationPolicy.OverrideChargePeriod` permission.
An authorized manual range does not require a date-change reason. Start/end dates and academic-year boundaries remain mandatory; supplied historical reasons remain readable. Policy overrides, unpaid corrections and optional-service cancellations retain their separate reason requirements.

Before the first Finance assignment of an unprepared admission selection, optional heads can be selected or removed. The server validates the selected structure and heads and synchronizes the initial admission selection with the new canonical obligations in the same transaction. `FinanceInitialFeeSelectionHistory` retains the previous selection, new selection, actor/time and account identity in ExtraProperties. Prepared accounts still require an exact match; payments and existing obligations continue through the protected correction/cancellation workflows.
Neither operation checks concession permissions.

## Correct an unused student assignment

The existing `preview`, `preview-bulk`, `assign` and `assign-bulk` endpoints accept optional `editAssignmentId`, `assignmentConcurrencyStamp` and `editReason` fields.
Only an explicit edit identity enters the correction path; an ordinary create command still cannot overwrite a different saved assignment.
Correction uses `.Add`; actual concession/refund/collection policy changes additionally require `.OverridePolicies` and the existing policy reason.
Omitted policies retain the student's current pins during a correction; retaining an unchanged inactive historical version does not require new override authority.
Proration remains independently resolved under the current assignment rules.

| Contract | Authoritative fields |
|---|---|
| Student row | `assignmentId`, `structureId`, `structureRevision`, `assignmentConcurrencyStamp`, `canEditStructure`, `structureEditBlocker` |
| Assignment detail | `structureRevision`, `canEditStructure`, `structureEditBlocker`, existing `concurrencyStamp` |
| Assigned context | Existing assignment/structure IDs and revision, `assignmentConcurrencyStamp`, `canEditStructure`, `structureEditBlocker`, and `assignedAssignment` with saved heads, periods and current pinned policies |

An edit requires an accessible active enrollment, the current assignment stamp, a nonempty reason, and a fresh protected preview. The fee structure change reason has no character limit; blank or whitespace-only reasons are rejected.
Its command fingerprint binds edit identity, stamp and reason; its prepared fingerprint additionally binds old due concurrency stamps and admission/account source revisions.
The server permits another published structure for the same student/year/class, optional-head selections, variable amounts, verified dates and authorized policy selections under the existing validation rules.

Any recorded payment blocks structure correction, including partial payments, refunded payments and reversed instruments whose current paid balance is zero.
Eligibility checks historical principal/late-fee allocations, year/student/account collection evidence, positive legacy admission-stage transactions, permanent due payment markers, and admission/account flags.
Historical evidence checks disable only the soft-delete filter; tenant scope remains enabled.
Concession, adjustment, refund, waiver, withdrawal and persisted late-fee use also block replacement with an actionable explanation.
Legacy or inconsistent assignment/due evidence requires reconciliation.
After payment, policy changes and optional-service changes use their own commands. They preserve the original schedule and its financial evidence.

### Re-enable a stopped optional service

The student must have an accessible active enrollment and a saved structure containing the recurring optional fee. A service with a recorded stop can resume from the cancellation effective date. A previously unselected service can be added from the assignment charge start. Dates are inclusive and capped by the academic year. When one retained installment on the same assignment covers the selected days for that installment, it carries forward at its existing billed amount and outstanding balance. Conflicting or partial coverage that would create a duplicate obligation is rejected. The restart adds no charges for the gap between service periods. Initial activation uses the same permissions and monthly preview without replacing required dues.

The assignment history combines creation metadata, saved correction and policy evidence, and optional-service audit events. It displays recorded old and new values, occurrence times, actor names where available, and reasons. Raw snapshots, receipt data, and other students' activity are outside this endpoint.

The restart uses the selected active proration policy version and an eligible published monthly payment term. The existing calculator applies the configured rule to each new installment and uses the payment term's demand and due dates. The preview shows the monthly base, applied factor, reduction and payable amount for new charges, plus the retained installments and their balances. A period already fully covered can be saved with zero new charges. Existing prepaid credit from cancelled installments is shown separately for the adjustment or refund workflow.

Saving pins the monthly rate, term version and policy version to the new period. The original assignment evidence, cancelled rows and payments remain available. The new dues appear in Collections using the same ledger and allocation rules as other assignment dues. A scoped preview token binds the request and current evidence. Replaying the same request ID returns the original result, and the period-specific schedule keys keep restarted dues distinct from cancelled dues. Later cancellation targets the latest service period. The period history uses the existing ExtraProperties columns.

Student withdrawal and Finance clearance verify both the original assignment and the saved restart installments. A withdrawal applies each restarted service period's own pinned policy and leaves prior service cancellation credits in place.

Correction retains the canonical assignment ID and appends original structure/revision, snapshot, current policy pins, complete prior due evidence, actor, timestamp and reason to `AssignmentCorrectionHistory` in existing ABP ExtraProperties.
It archives only unused tracked dues through `SupersedeUnusedAssignment` and normal checked updates, flushes those updates to release filtered schedule uniqueness keys, and inserts the recalculated schedule in the same new Serializable transaction.
The correction deliberately avoids ABP's `DeleteAsync` path, which reloads soft-deleted entities and would refresh a stale concurrency stamp.
EF's original due concurrency predicates contend with collection; late-fee-only collection also updates the due's payment-evidence stamp.
The assignment, account snapshot, admission selection/history and current-year student shortcut update atomically.
No migration is required.

Bulk corrections require the same original structure ID, revision and year and keep independent per-student transactions/results.
Successful retries recover the original group identity from correction history after a structure switch.
A first correction returns `Assigned`; an identical successful replay returns `AlreadyAssigned` without new dues, including after later payment.
`Finance:Assignment:StructureEditBlocked` explains payment/downstream restrictions; `Finance:Assignment:MixedOriginalStructures` rejects incompatible bulk groups.
Stale identity, stamp, token, reason or source facts use the existing `StalePreview` error.

## Stop an optional service

An authorized staff member can cancel an included recurring optional service by choosing the first day without service and entering a reason. The API receives the previous calendar day as the last service day. Installments with coverage starting on or after the effective date are eligible, including those with cleared payments. Earlier and overlapping coverage remains chargeable. Unsettled payments and conflicting financial adjustments or downstream activity block the affected installments. The cutoff is inside the assigned service dates.

Confirmation revalidates a short-lived, user- and tenant-bound preview of the exact due rows, stamps, cancelled amount, outstanding reduction and prepaid credit. It saves the effective end date and credits the remaining obligation on each eligible row in the same serializable transaction. Collections deducts these credits from the fee total and displays cancelled rows with zero due. Cleared payments on cancelled installments become available credit through the existing refund and adjustment workflows. Original principal, payment history, receipts and the assignment snapshot remain as evidence. Staff can review a previously recorded end date and apply it to installments that still qualify. Already credited installments are excluded, and a repeated request ID replays the result. The audit records the actor, reason, cutoff and due amounts.

`Finance.Fees.StructureAssign.StopOptionalService` is a separate exact grant. Existing non-admin roles receive it through the permission editor. Student and year scope and active enrollment apply to preview and confirmation.

## Student policy selection and edits

| Contract concern | Behavior |
|---|---|
| Outcome/owner | Finance assignment owns the student's pinned Concession, Refund, Collection and Proration selections; fee structure and policy definitions are unchanged |
| Actor/scope | Assignment `.Add` creates; `.View` reads; dedicated `.OverridePolicies` authorizes exceptions and later edits. Existing tenant/year/student/class UserScope checks apply |
| Default/precedence | Omitted concession, refund or collection inherits the exact structure version; explicit student selections override those families and null clears an optional family. Collection cannot be cleared. Proration independently uses the active academic-year default for new Admission preparation and Finance assignment, including students without an Admission link. Legacy structure proration pins are not inherited. Saved assignment pins cannot be overridden |
| Fields | `policies: [{ family, versionId }]` (unique, at most three selectable families: concession, refund, collection); `policyOverrideReason` (1-500 characters when changed). Edit requires those three families, `academicYearId`, nonempty `concurrencyStamp`, and `reason`. A proration selection is rejected |
| Preconditions | Current active/effective same-year version for newly selected policies; already-pinned historical versions may remain unchanged. Family/year mismatches, empty identifiers and unavailable versions fail closed |
| State/correction | Assignment remains assigned; later changes supersede its current selections and append before/after version IDs, actor, timestamp and reason to persisted aggregate ExtraProperties |
| Monetary equation | Policy selection alone applies no concession, penalty or refund. The Finance default proration rule calculates the included head entitlement from its selected service dates before installment allocation. Missing defaults and unmatched rules retain existing full-charge treatment. Later policy edits leave stored original, paid and outstanding amounts unchanged |
| Side effects/history | No due, receipt, posting or payment mutation. `AssignmentSnapshotJson` remains immutable; read responses show original policies separately from current selections. Collection and refund evaluate assignment's current pinned versions on subsequent operations; existing transaction evidence stays pinned |
| Failure/retry | Initial policy selections and reason are covered by command fingerprint and protected preview. Edit validates every selection before mutation, uses a transactional unit of work and checks the supplied aggregate concurrency stamp; identical selections are a no-op |
| Evidence | `StudentFeeAssignmentPolicies_Tests`, assignment HTTP tests, frontend workflow and permission-group tests cover defaults, reasons, token invalidation, policy persistence, denied grants, stale edit and obligation preservation |

Concession is a selection for separate concession processing, not an automatic award. Current structure configuration leaves its Concession attachment empty; historical attachments are inherited when present. No eligibility calculation or approval is performed by assignment.

The new exact permission is registered in the backend catalogue. It is included in the existing frontend assignment Add group for explicit grants; existing deployed roles are not reseeded or granted access automatically. No schema migration is needed: current version columns, ABP ExtraProperties and the existing concurrency token store the change.

## Command and review boundary

```json
{
  "studentId": "<student UUID>",
  "academicYearId": "<year UUID>",
  "standardFeeStructureId": "<published revision UUID>",
  "selectedOptionalFeeHeadIds": [],
  "feeHeadDetails": [],
  "servicePeriods": [],
  "previewToken": "<token returned by preview; required at commit>"
}
```

`feeHeadDetails` accepts `{feeHeadId, amount}` as evidence of the published fixed amount or a previously saved fixed amount on the same structure. Use `variableFeeHeads` to enter a new variable price.
Included variable heads use `variableFeeHeads: [{feeHeadId, amount, zeroAmountReason}]`; a zero amount requires its reason.
Per-month installment replacements are not accepted.
All mandatory heads remain included.
Excluded optional heads receive no dues; attempts to attach amounts or dates to them are rejected.
Duplicate/foreign head IDs, negative amounts and more than two currency decimals are invalid.

Each `servicePeriods` item contains `feeHeadId`, `source`, and source-specific fields:

| Source | Verification |
|---|---|
| `AcademicYear` (default) | Configured year boundaries; explicitly not a verified joining date |
| `BoardAcademicYear` | The published structure board in the same year supplies the verified default dates when they fall within the academic year. Provided endpoints must match the board configuration. |
| `Registration` | Finance verifies the student's saved joining date and application ID. The service starts on the joining date or the verified board start, whichever comes later, and ends at the board end. The academic year supplies the boundaries when the board dates are unavailable. |
| `TransportService` | `sourceReferenceId` must resolve to that student's route assignment and the fee head must belong to Transport; server reads and intersects the dates with the academic year |
| `ManualOverride` | ISO dates within the academic year, required reason, explicit date-override permission; no source-reference ID |
| Hostel, Exit or unknown | Rejected as unavailable until an authoritative source adapter is implemented |

The server normalizes service dates to UTC midnight. It stores the verification label, source reference/revision and reasons in the assignment snapshot. Finance uses one calendar month for one-time heads. It also uses a service month for required full-charge annual heads with a single installment and a verified admission joining date. The published installment still determines the due date.

The protected token expires after 20 minutes and binds tenant, user, command, exact prepared result and source/configuration revisions.
Preview additionally returns optional UTC `previewExpiresAt` for client feedback; this field is absent from saved evidence and the prepared-result fingerprint.
Expiry is checked against the server's confirmation acceptance time, captured once for the entire bulk command, before expensive recalculation.
A token already expired when confirmation arrives is rejected without writes; a valid token cannot expire merely because earlier bulk items or recalculation take time.
Commit still reloads and recalculates each student; changed inputs, scope or authoritative facts require a new preview.
An identical saved command returns `AlreadyAssigned` before token validation, including after expiry, without generating additional dues.
App hosts use ASP.NET Data Protection.
Multiple instances must share the application's protected key ring for previews to survive routing between instances.

## Amounts, terms and policy evidence

- A recurring structure's default amount is a **service total**. A one-time amount is charged once. For a term with one installment per covered calendar month, a fixed structure or a student-specific variable fee can explicitly use a **monthly rate**.
- Monthly pricing generates the rounded monthly amount for each installment whose coverage intersects the verified service dates. The pinned proration rule is evaluated for that installment's coverage. Equal monthly charges take precedence over the term's percentage split when a monthly rate is selected. The same calculation is used for withdrawal against the saved rate and installment dates.
- Service totals are allocated using the exact published term percentages and dates, including structure-local schedule overrides. Every included head requires a published term version.
- Daily proration uses the academic-year service denominator. Monthly cutoff divides the annual total into service months independently of billing frequency. New policy versions configure admission and early withdrawal independently; legacy snapshots retain their existing withdrawal-month behavior. See [the event calculation contract](./proration-policy-end-to-end.md#independent-admission-and-early-withdrawal-contract).
- Currency rounding is deterministic. The final installment absorbs the residual; very small totals redistribute excess rounding in reverse installment order to prevent negative dues. Installments always sum to the head total.
- One-time heads and heads with no matching proration rule receive full charge. The review says so explicitly.
- Collection, Refund and optional Concession use the resolved exact immutable policy versions. Missing inherited versions fail closed. Authorized student selections override concession, refund or collection attachments without substituting current defaults. Proration uses the active same-tenant, same-year Finance default independently of legacy structure storage and does not require a student Admission link.
- Snapshot evidence contains structure revision/name, all head selections, original/effective amounts, term name/version, policy versions/treatment, service periods, calculation version, schedule and due IDs. Current paid/outstanding amounts are read from `DueFee` on retrieval.

New `assignment-v3` recurring obligations include only published installments whose coverage overlaps that head's verified service period, inclusive of both boundary dates.
New Finance assignments automatically pin the same default Proration Policy used by Admission preparation; the context and preview label its source `FinanceDefault`.
Finance manual/transport dates remain verified by their existing permissions and source adapters; selecting the whole academic year without a saved joining date does not imply a late admission.
Changing the default policy or version invalidates an uncommitted preview, while saved assignment/account snapshots and idempotent retries retain their original version and obligations.
The head entitlement still uses the academic-year denominator and pinned proration rules; full-charge and one-time pricing retain their existing meanings.
The included installments retain their published IDs, sequence, coverage, demand and due dates.
Their relative allocation percentages are normalized to 100 at six-decimal precision, with the final installment absorbing the percentage and monetary residuals.
Saved scheduled amounts sum to the annual base, and saved obligations sum to the calculated head entitlement; withdrawal, collection and other consumers use those stored amounts and effective percentages.
A service period without any overlapping positive-weight term fails with `Finance:Assignment:ConfigurationInvalid` rather than generating unrelated obligations.

Automatic late-fee accrual excludes due dates before the latest known registration joining date, assignment charge start, or that fee head's service start, including stored due-level proration evidence.
Date boundaries use calendar dates: a due date on the effective start date remains eligible for the normal grace and overdue rules.
The canonical late-fee balance service applies this boundary to student and pre-enrollment account collections, payment validation, details, summaries and waivers.
Previously recorded assessments, payments and waivers remain financial evidence and cannot cause a second common charge on a newly eligible installment.
Historical payment recovery preserves the transaction timestamp's recorded calendar date; server-local offset conversion must not create an extra late day.
This change requires no schema migration and does not rewrite existing assignment snapshots or dues during reads or retries.
Already saved incorrect schedules or assessments require a reviewed financial correction; paid history must use the existing adjustment, reversal or refund workflows.
Withdrawal recalculates each eligible head against its saved service start and the earlier of its saved service end and leaving date, preserving the academic-year denominator.
Leaving before a head's service starts gives zero revised entitlement; leaving after its saved service end cannot extend the charged period.
Older snapshots without head-level dates retain the recorded joining/assignment-period fallback.
Proration result `joiningDate` stays null when no actual joining date is recorded; calculation/service-period fallbacks are never displayed as an actual joining date.

## Transactions, retries and integrity

Each student commit owns a new transactional unit of work containing the assignment, all generated dues and the current-year student shortcut.
Bulk items commit independently, and counters are incremented only after a durable canonical outcome is established.
The bulk controller and application service keep their outer unit of work nontransactional; each student's commit still owns its independent transaction. This prevents the preflight read from creating a surrounding transaction that conflicts with those commits.
The existing unique tenant/student/year assignment index arbitrates concurrent requests.
After a non-authorization, non-cancellation failure, the write context is disposed before a fresh scoped read checks for an identical saved command.
This also recovers a durable assignment when a completion callback fails after commit; mismatching commands never count as a recovered success.
If no matching saved assignment exists, known business failures retain their original error.

The normalized command fingerprint excludes the preview token.
An identical retry returns `AlreadyAssigned` with the saved result; a different ordinary create command cannot overwrite an existing assignment.
The explicit correction contract above is the only structure replacement path.
Correction preparation calculates a transient replacement schedule after validating the canonical assignment. Ordinary due generation retains its duplicate-generation guard; calculation does not archive or insert obligations. Archival and replacement insertion remain part of the correction commit transaction.
Subsequent payments and master edits never cause assignment regeneration.
Existing collection resolves an assignment's exact pinned policy and fails if it cannot be found.

`Finance:Assignment:*` errors distinguish invalid input, missing/inactive enrollment, structure mismatch, stale preview, existing/missing assignment, unverified source, invalid configuration and commit failure. Bulk errors expose stable messages without exception internals.

## Migration and deployment

`20260915122353_canonicalFeeAssignment` (class `AddCanonicalStudentFeeAssignments`) targets the repository's actual PostgreSQL provider.
It makes the two concession references nullable, adds `AssignmentSnapshotJson`, adds the nullable due-to-assignment FK and persisted `numeric(18,6)` allocation percentage, and creates the FK index.
This is the migration supplied by main; the earlier unpublished local duplicate is not part of the candidate.
It does not backfill guessed history or alter financial amounts.

Deploy the migration before releasing the new API/client together.
Existing clients that commit without a preview token must migrate.
Rollback locks both affected tables in its migration transaction before checking for snapshot evidence, due links, allocation percentages, or missing concession IDs.
It refuses rollback if any such record exists rather than discarding evidence or inventing concession IDs.
Use a forward fix once new assignments exist.
Local disposable PostgreSQL validation has applied this migration; no deployed database was changed.

`CanonicalStudentFeeAssignmentPostgres_Tests` starts from the preceding migration, inserts synthetic legacy financial records, and proves upgrade preservation, legacy-only rollback, five independent rollback guards, nullable concession support, restricted deletion, foreign-key integrity, precision, and complete model migration coverage.
Set `FINANCE_MIGRATION_TEST_POSTGRES` to an empty loopback `payroll_tests_*` database before running this test in `test/ShikshaMitra.EntityFrameworkCore.Tests`.
It rejects nonlocal or populated databases and never resets or deletes a database.

Legacy assignments remain readable with an explicit warning. Only their recorded dues and balances are displayed. Unknown historical selections, service sources and allocation percentages stay unknown. Missing referenced dues require reconciliation.

## Current boundaries and validation

Unused assignments can use the explicit correction workflow above, including adopting a different published revision.
Paid or otherwise financially used assignments remain locked against structure/schedule replacement.
Bulk waivers are protected by persisted `LateFeeWaiverApplication` rows, including their bulk-request link and soft-deleted history. The current module has no database mapping for `BulkWaiverRequest` itself, so pending request objects do not supply durable assignment-use evidence.
Transfers, additional verified service adapters and per-period pricing bases remain outside this contract.
Existing separate adjustment/refund/concession operations remain responsible for their own financial transitions.

Focused coverage lives in `StudentFeeAssignmentWorkspace_Tests` and `AssignmentAmountCalculator_Tests`. Application tests use SQLite and disable transaction wrapping in their shared harness: they verify preview, commit, retry, year reads and per-student outcomes, but are not proof of PostgreSQL rollback or concurrent index arbitration. A real PostgreSQL staging run must verify migration, concurrent identical/different commits and injected persistence failure before production rollout.

`StudentFeeAssignmentCorrection_Tests` covers unused correction/history/retry, payment locks including historical reversals, stale previews, same/mixed bulk originals, protected downstream use and canonical due concurrency. The focused application/workspace/admission selection passes23 cases, and actual assignment HTTP coverage passes19 cases.

`StudentFeeAssignmentPostgres_Correction_Tests` passes three cases on a private PostgreSQL18 database: collection wins after correction read and prevents stale archival; correction wins after collection read and rejects the obsolete payment; and an injected replacement insert failure after old-dues flush restores the original schedule, canonical snapshot/fingerprint/history. It uses the production receipt-sequence repository and checks sequence persistence/rollback. The guarded fixture requires a new empty high-port loopback `assignment_correction_test_*` database via `FEE_ASSIGNMENT_TEST_POSTGRES`, never resets populated databases, and creates the schema from the current EF model. These cases prove correction transaction behavior; they do not execute migrations or production OAuth.

Frontend route, components and browser checks: [frontend assignment reference](../../../../../Shikshamitra-FE/docs/finance/references/student-fee-assignment.md).

## Bounded assignment reads and captured placement

Bulk contexts authorize the selected enrollments first, then batch canonical assignments, blockers, admission links and joining-date facts.
`includeEditDetails=false` omits candidate/policy editing data for a read-only single-assignment view; the default remains true for existing editing clients.
Bulk preview shares structure, metadata, year, head, term and current-policy inputs within that preview only.
Each confirmation retains fresh authoritative reads and its own student transaction; cached preview inputs never authorize a write.

New assignments capture academic class, division and roll at commit.
Historical scope and labels use that placement after promotion; legacy history uses only verified recorded enrollment/account evidence and leaves unknown division or roll unknown.
`20261117143000_IndexAssignmentHeadEvidence` adds nullable placement and selected-head evidence with a GIN index.
Its tolerant backfill distinguishes unknown/corrupt selections from an explicitly empty set, includes selected zero-value heads, and preserves original snapshots and amounts.
Apply the additive migration before deploying the matching backend; no deployed database was changed by this work.
