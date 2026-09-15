---
module: finance
domain: student-fee-assignment
content_type: Reference
status: canonical
updated_on: 2026-09-10
owner: backend-team
audience: humans-and-agents
---

# Student fee assignment contracts

Assignment selects one published class/year structure, calculates included heads, previews the result, and commits an immutable assignment with its dues. Concession configuration, eligibility and allocation are separate operations. Assignment creates no receipt, posting, payment, late fee or refund.

## Eligibility and academic history

The persisted `Student` academic placement and `Enrolled` lifecycle status are the authoritative source for creating an assignment. The domain's `StudentEnrollment` aggregate has no EF mapping and is not queried by this implementation. `EnrollmentId` identifies the source academic record, not a newly created enrollment. Inquiry, registration, inactive and left students cannot receive new assignments.

Historical reads use `StudentFeeStructure.StudentId + AcademicYearId`, including after the student's current year changes. The structure supplies the historical class; no old division is inferred. A division-only user without adequate historical class scope is therefore denied. ABP tenant filters stay enabled. The server additionally checks the user scope hierarchy against the requested academic year.

## Endpoints and permissions

All routes below are under `/api/student-fee-structure`; the client base URL already contains `/api`.

| Method / suffix | Permission | Result |
|---|---|---|
| GET `students` | `Finance.Fees.StructureAssign.View` | Paged student academic records with assignment status for the selected year; year, board, medium, standard, division and search filters |
| GET `candidates` | `.Add` | Published structures matching the accessible active student/year/class |
| GET `transport-sources` | `.Add` | That student's verified route-assignment periods overlapping the year |
| POST `preview` | `.Add` | Authoritative heads, service evidence, installments, three policy summaries, totals and protected preview token; no writes |
| POST `preview-bulk` | `.Add` | Independent preview/error for each student |
| POST `assign` | `.Add` | `assignmentId`, outcome `Assigned` or `AlreadyAssigned`, and canonical assignment snapshot |
| POST `assign-bulk` | `.Add` | Independent committed, already assigned, skipped or failed outcomes and counts |
| GET `student/{studentId}?academicYearId=<year>` | `.View` | Saved evidence plus current canonical due balances; no repair or regeneration |

The abbreviated permission suffixes refer to `Finance.Fees.StructureAssign`. Amount overrides additionally require `.OverrideAmount`. Manual dates require the existing `Finance.Policies.ProrationPolicy.OverrideChargePeriod` permission. Neither operation checks concession permissions.

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

`feeHeadDetails` accepts `{feeHeadId, amount}` as an authorized total amount override, with `overrideReason` on the command. It does not accept per-month installment replacements. All mandatory heads remain included. Excluded optional heads receive no dues; attempts to attach amounts or dates to them are rejected. Duplicate/foreign head IDs, negative amounts and more than two currency decimals are invalid.

Each `servicePeriods` item contains `feeHeadId`, `source`, and source-specific fields:

| Source | Verification |
|---|---|
| `AcademicYear` (default) | Configured year boundaries; explicitly not a verified joining date |
| `TransportService` | `sourceReferenceId` must resolve to that student's route assignment and the fee head must belong to Transport; server reads and intersects the dates with the academic year |
| `ManualOverride` | ISO dates within the academic year, required reason, explicit date-override permission; no source-reference ID |
| Admission, Hostel, Exit or unknown | Rejected as unavailable until an authoritative source adapter is implemented |

The server normalizes service dates to UTC midnight. It stores the verification label, source reference/revision and reasons in the assignment snapshot.

The protected token expires after 20 minutes and binds tenant, user, command, exact prepared result and source/configuration revisions. Commit reloads and recalculates; changed inputs or facts require a new preview. App hosts use ASP.NET Data Protection. Multiple instances must share the application's protected key ring for previews to survive routing between instances.

## Amounts, terms and policy evidence

- The existing recurring structure amount is an **annual service total**; a one-time amount is charged once. Payment frequency never changes this amount basis.
- A head's service obligation is calculated first, then allocated using the exact published term percentages and dates, including structure-local schedule overrides. Every included head requires a published term version.
- Daily proration uses the academic-year service denominator. Monthly cutoff divides the annual total into the year's service months and applies the configured joining cutoff, independently of billing frequency. Eligible withdrawal month remains a full month; no new withdrawal-specific policy is inferred.
- Currency rounding is deterministic. The final installment absorbs the residual; very small totals redistribute excess rounding in reverse installment order to prevent negative dues. Installments always sum to the head total.
- One-time heads and heads with no matching proration rule receive full charge. The review says so explicitly.
- Collection, Proration and Refund use the structure's exact immutable policy versions. Missing attached versions fail closed. No current default or concession policy is substituted.
- Snapshot evidence contains structure revision/name, all head selections, original/effective amounts, term name/version, policy versions/treatment, service periods, calculation version, schedule and due IDs. Current paid/outstanding amounts are read from `DueFee` on retrieval.

## Transactions, retries and integrity

Each student commit owns a new transactional unit of work containing the assignment, all generated dues and the current-year student shortcut. Bulk items commit independently, and counters are incremented only after commit completes. The existing unique tenant/student/year assignment index arbitrates concurrent requests. A failed context is disposed before checking for a concurrent successful commit.

The normalized command fingerprint excludes the preview token. An identical retry returns `AlreadyAssigned` with the saved result; a different command cannot overwrite an existing assignment. Subsequent payments and master edits never cause assignment regeneration. Existing collection resolves an assignment's exact pinned policy and fails if it cannot be found.

`Finance:Assignment:*` errors distinguish invalid input, missing/inactive enrollment, structure mismatch, stale preview, existing/missing assignment, unverified source, invalid configuration and commit failure. Bulk errors expose stable messages without exception internals.

## Migration and deployment

`20260910180000_AddCanonicalStudentFeeAssignments` targets the repository's actual PostgreSQL provider. It makes the two concession references nullable, adds `AssignmentSnapshotJson`, adds the nullable due-to-assignment FK and persisted allocation percentage, and creates the FK index. It does not backfill guessed history or alter financial amounts.

Deploy the migration before the new API/client together. Existing clients that commit without a preview token must migrate. Rollback refuses to discard canonical assignment evidence or invent mandatory concession IDs; use a forward fix once new assignments exist. No migration has been applied by this implementation task.

Legacy assignments remain readable with an explicit warning. Only their recorded dues and balances are displayed. Unknown historical selections, service sources and allocation percentages stay unknown. Missing referenced dues require reconciliation.

## Current boundaries and validation

Service changes after assignment, transfers and adopting a different revision need a separate reviewed correction workflow; this implementation rejects overwrite and does not introduce that workflow. Verified admission/hostel/exit adapters and per-period pricing bases are also outside the shipped contract. Existing separate adjustment/refund/concession operations remain responsible for their own financial transitions.

Focused coverage lives in `StudentFeeAssignmentWorkspace_Tests` and `AssignmentAmountCalculator_Tests`. Application tests use SQLite and disable transaction wrapping in their shared harness: they verify preview, commit, retry, year reads and per-student outcomes, but are not proof of PostgreSQL rollback or concurrent index arbitration. A real PostgreSQL staging run must verify migration, concurrent identical/different commits and injected persistence failure before production rollout.

Frontend route, components and browser checks: [frontend assignment reference](../../../../../Shikshamitra-FE/docs/finance/references/student-fee-assignment.md).
