---
module: finance
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-10-29
owner: backend-team
audience: humans-and-agents
---

# Validate Finance backend changes

Stored None defaults: run `NoneCategory_Tests` and `NoneCategoryApi_Tests`, plus the existing account
settings permission/simplification suites. Verify None is first in fee-head income and refund-policy
expense lookups, absent from master lists and every other routing lookup, and ordinary categories
remain available. Verify valid fee-head saves, immutable defaults, new-year initialization, idempotent
retries and cross-year denial. Apply `AddNoneTransactionCategories` to existing contexts; verify the
data-only backfill on disposable PostgreSQL tables, including nullable tenants, existing/archived
None IDs, historical deleted duplicates and preservation of ordinary categories and references.

Cash verification on 2026-10-28: all 12 `CashCollection_Tests` and all 21 HTTP cases across `SimpleCashFlowApi_Tests`, `CashExpenseRoutingApi_Tests`, `CashManagementAuthorizationApi_Tests` and `PendingCashRegisterApi_Tests` passed using the .NET 9 SDK selected by `global.json`. Coverage includes pooled FIFO expenses without source/reference fields, overdraft rollback, academic-year totals independent of table/month criteria, Deposit-only direct deposits, optional document download, stale totals and exact/changed replays, scope loss after replay, visibility of every expense allocation, legacy bank destination snapshots and historical-year deposits. SQLite cannot translate collection-instant inequalities, so month matching and FIFO ordering use scoped ID/timestamp projections; row predicates still precede counts and paging.

The isolated targeted build passed through Domain.Shared, Domain, Application.Contracts, Application, EntityFrameworkCore, HttpApi, Host and both affected test projects. The existing external verification target omitted 109 historical generated migration designers while retaining the current runtime model, current snapshot and cash migration designer. No schema change, production migration or PostgreSQL allocation race was executed. `git diff --check` passed. The Finance docs validator still reports the pre-existing encoding issues in `references/charge-management-e2e-test-plan.md` and `references/income-expense-workspaces.md`.

Direct pending decisions: `ConcessionDirectDecisionApi_Tests.Checker_Can_Decide_Saved_Request_Without_Separate_Submission` covers scoped reviewer visibility, view-only draft privacy and approval/rejection without submission. Its percentage scenario increases live capacity after save and requires approval to equal the reviewed saved amount. `Checker_Can_Reject_Incomplete_Saved_Request_Without_Submission`, `ConcessionRequestLifecycle_Tests` and `ConcessionRequestPreview_Tests` cover incomplete rejection, audit status, independent actors and preserved monetary evidence.

Draft update/submission: `ConcessionRequestUpdateApi_Tests.Fixed_policy_draft_update_returns_the_persisted_stamp_and_can_be_submitted_immediately` exercises real HTTP/EF persistence for a fixed 1,000 concession over eligible fee heads of 58,166 and 717, including partially and fully paid installments with automatic refunds disabled. It requires the update response stamp to equal a subsequent read, rejects an older editor with a specific concurrency error and support reference, and submits with the update response stamp. `ConcessionRequestPreview_Tests` verifies the exact 988/12 allocation and preserved paid-fee eligibility; `ConcessionRequestLifecycle_Tests` verifies locked-state edit diagnostics. `ApiProblemDetailsPipeline_Tests` covers localized rule details and `traceId` through both exception middleware and ABP result normalization.

Annual type benefit regression: the HTTP scenario retains an earlier approved 200 benefit beside a pending 1,000 request, verifies typed eligibility and approval blockers, rejects a forged create `requestId` exclusion, permits rejection and another concession type, and checks that blocked commands leave dues and approved history unchanged. Domain coverage rejects new incomplete drafts for Pending, Approved and PartiallyReversed benefits. PostgreSQL guard concurrency coverage uses an explicitly configured disposable database; SQLite verifies HTTP behavior but does not establish PostgreSQL lock or Serializable snapshot guarantees.

`ConcessionBenefitGuardConcurrency_Tests` runs the production repository guard with real PostgreSQL transactions and a minimal header model. Six cases cover Read Committed and Serializable lock waiters, fresh idempotency evidence while the caller retains its older Serializable snapshot, a saturated writer pool, blocking/released status and tenant/student/year/type scope, own-request exclusion, approved-benefit priority, and uncommitted same-UOW claims before and after flushing. Model and ambient-UOW plumbing are isolated; this is repository concurrency coverage alongside the HTTP workflow test, rather than a full PostgreSQL HTTP application test.

Set `SHIKSHAMITRA_CONCESSION_BENEFIT_TEST_DB` to a synthetic loopback PostgreSQL connection with a fresh database name beginning `concession_benefit_test_`, then run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~ConcessionBenefitGuardConcurrency_Tests"`. The fixture requires this explicit opt-in, creates and drops only its dedicated test database, and skips when unset. The six PostgreSQL 18 cases passed with zero skips on 2026-10-28; the domain concession suites passed 26 cases.

The dedicated SQLite fixture uses minimal seed data, cheap hashing for synthetic test-auth users, and a real EF student repository that suppresses recursive automatic navigation expansion. Explicit includes, tenant query filters, student-scope checks, transaction boundaries and optimistic concurrency remain active.

Verification on 2026-10-28: all 26 focused domain cases, all 54 HTTP/pipeline cases and all six real PostgreSQL guard cases passed with zero skips. The pre-fix application binary failed the update-response versus persisted-stamp assertion; the rebuilt flush fix passed immediate submission and rejected stale edits. The final HTTP workflow also passed same-key submit/draft replay, annual-benefit rejection, recovery by rejection, and another concession type without changing approved history or blocked-command balances. HTTP compilation used a local focused target to avoid unrelated receipt-test compilation errors; the full HTTP suite was not run. PostgreSQL verification covers the actual repository guard and transaction behavior, while the complete HTTP workflow uses SQLite.

```powershell
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~ConcessionRequestLifecycle_Tests|FullyQualifiedName~ConcessionRequestPreview_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~ConcessionRequestUpdateApi_Tests|FullyQualifiedName~ApiProblemDetailsPipeline_Tests"
```

Copy-policy regressions live in `FeeStructureAppService_Copy_Tests.cs` (partial
`FeeStructureAppService_CollectionPolicy_Tests`). Cover current/stale refund selection,
active-master version precedence, legacy concession inspection, inactive/missing snapshots,
named errors, no insert on failure, unchanged source/policy data and idempotent retry.
`DemandDraftCollectionDates_Tests` covers independent dates, offset ISO inputs, legacy aliases,
missing dates and backward-compatible instrument JSON round trips.

Automatic collection allocation: `CollectionAllocation_Tests` covers snapshot compatibility,
invalid enums, decimal conservation, partial/excess and duplicate targets.
`LateFeeCollectionRuntime_Tests` has `Allocation_Basis_Survives_Save_Update_And_Clone` and
`Automatic_Allocation_Rejects_Tampering_And_Stale_Review_And_Replays_Safely` for both modes,
using tenant-scoped admin fixtures and real collection persistence. These verify omitted-field
compatibility, pinned snapshots, no writes on altered/stale commands and idempotent replay.

TL;DR: use focused Finance tests for changed financial behavior, then run module docs validation.

Fee Structure configuration, publication and revision contracts: [workspace implementation](./references/fee-structure-workspace.md).

Charge Management browser, API, Fee Collection, receipt, reversal, security, and edge-case coverage: [end-to-end test plan](./references/charge-management-e2e-test-plan.md).

## Required docs checks

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module finance
git diff --check
```

## Focused test map

### Combined family transaction history

`MobileFinanceTransactions_Tests.cs` (partial `MobileAcademicAppService_Tests`) covers Collection View, child/year association, revocation, independent Receipt View, exact decimal collection evidence, combined receipt identity, offset validation and the annual summary.
`PostgreSQL_family_transactions_union_refunds_returns_legacy_and_global_page` uses the dedicated `FAMILY_FINANCE_TEST_POSTGRES` harness to verify bounded timestamp-ordered source queries, posting/settlement deduplication, payout returns, successful legacy settlement fallback, omission of failed/pending/undated attempts, foreign-child exclusion, global pagination/ties and gross annual totals.
The ordinary SQLite provider cannot certify PostgreSQL `DateTimeOffset` ordering; the refund-union test therefore requires the existing isolated PostgreSQL opt-in.
`MobileFinanceWireContract_Tests` verifies the namespaced string IDs, fixed money strings, nullable collection IDs, date wire shape and shared offset defaults.
`MobileFinancePermissionContract_Tests` and `MobileFinancePermissionApi_Tests` cover the new action attribute, anonymous calls and missing family association.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Finance_transactions|FullyQualifiedName~PostgreSQL_family_transactions|FullyQualifiedName~MobileFinanceWireContract_Tests"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~MobileFinancePermission"
```

Verification on 2026-10-29: four focused persisted SQLite application cases, two runtime serializer cases and the PostgreSQL union case passed.
Focused Application and HttpApi/Host test builds passed with the external generated-migration exclusions described below; the HTTP exclusion list additionally required unchanged `CashManagementMigration_Tests` and `InstitutePerformancePostgres_Tests` sources.
The first HTTP run passed 41 of 42 cases, including the new action attribute and anonymous route; its first authenticated transaction association request hit a test-client cancellation during overloaded startup before an HTTP response could be asserted.
The isolated two-route retry remained in existing fixture seeders before reaching either selected route and was stopped; authenticated HTTP acceptance remains unverified in this environment.
Keep this runtime timeout separate from the passing direct-service scope checks; it does not certify deployed latency or HTTP acceptance.
The persisted serializer fixture contains two collection rows, a payout, a legacy payout and a recorded return, with gross annual paid amount `1000.30`.

### Admission and withdrawal proration

| Gate | Coverage |
|---|---|
| Calendar and ledger equation | `WithdrawalProration_Tests`, `AssignmentAmountCalculator_Tests`, `ProrationEventCalculation_Tests` |
| Registration/default/version/no-reduction/one-time behavior, paid-date lock, Onboarding activation continuity | `AdmissionProrationIntegration_Tests` (partial `AdmissionWorkflowRuntimeIntegration_Tests`) and existing Admission collection/handoff tests |
| Real PostgreSQL permissions, source replay, correction and rollback | `StudentRecordWithdrawalProrationPostgres_Tests` (partial `StudentRecordMidnightSchedule_Tests`); fresh disposable loopback `records_tests_*` database through `STUDENT_RECORDS_APPLICATION_POSTGRES` |

The SQLite application harness disables physical transactions and cannot certify rollback. PostgreSQL tests use separate synthetic records; they do not change live school policies or issue real payments. Receipt/refund approval and late-fee workflows retain their own gates.

### Receipt Book and audit permission boundaries

`FinancePermissionBoundaryApi_Tests` checks exact Receipt Book View versus writes,
module/action independence, and rejection of legacy audit or Collection View grants.
`FeeConfigurationPermissionAuthorization_Tests` also checks Receipt Book service
method attributes. Both suites are included in the reusable Security Regression gate.

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~FinancePermissionBoundaryApi_Tests|FullyQualifiedName~AccountSettingsPermissionApi_Tests" --artifacts-path .artifacts/finance-permissions-qa
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~FeeConfigurationPermissionAuthorization_Tests|FullyQualifiedName~FinanceAuditTrailAppService_Tests" --artifacts-path .artifacts/finance-permissions-qa
```

### Concessions and Collections list queries

`ConcessionRequestCoreApi_Tests.Should_Include_Board_And_Medium_In_Student_Concession_Details`
checks that the student concession detail response supplies the saved academic board and medium
names. Run the HTTP API suite with that test name as its `FullyQualifiedName` filter after
changing the student detail projection.

`test/ShikshaMitra.EntityFrameworkCore.Tests/Finance/FinanceTableQuery_Tests.cs` covers combined column alternatives, numeric ranges, explicit status zero, local-day date bounds, operational concession tab groups, query allowlists, stable sorting and PostgreSQL translation.
Build Application, HttpApi and EntityFrameworkCore after changing the contracts or query wiring.
Verify scoped concession counts/draft visibility and Collections canonical balance filters before pagination against a rebuilt local API.

### Accounts settings QA regression

| Suite | Coverage |
|---|---|
| `AccountSettingsTextValidation_Tests` | Income/expense and bank text boundaries; preservation of numbers, symbols and Unicode; constructor/update invariants |
| `AccountSettingsQaRegressionApi_Tests` | Real category/bank entity-history persistence with auditing enabled; safe UPI list projection; no full payment identifiers in history; invalid text payloads |
| `AccountSettingsSimplificationApi_Tests`, `AccountSettingsPermissionApi_Tests` | Existing simplified contracts and exact action authorization |

Run `dotnet test test/ShikshaMitra.Domain.Tests --filter FullyQualifiedName~AccountSettings`.
Run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~AccountSettingsQaRegressionApi|FullyQualifiedName~AccountSettingsSimplificationApi|FullyQualifiedName~AccountSettingsPermissionApi"`.
The regression factory deliberately enables auditing, which the default HTTP fixture disables. It disables transactions only inside this shared-connection SQLite fixture because ABP saves audit logs through a separate unit of work; SQLite cannot nest those transactions. Production transaction settings remain unchanged. Use PostgreSQL tests for transaction rollback guarantees.
Changes before entity-history registration cannot be reconstructed; missing known versions remain explicitly unavailable rather than being invented.

| Change area | First tests |
|---|---|
| Batch lifecycle | `ChargeWorkflowStatusResolver_Tests`, `ChargeBatchLifecycleApi_Tests`: approval-required categories remain pending, explicit no-approval categories apply immediately with collectible balances, partial decisions/payments, all-student completion, rejection inbox, waiver settlement, payment reversal, status validation and detail/list/count parity |
| Charges | `ChargeAppService_Tests`, `ChargeCoreApi_Tests`, `ChargeValidationApi_Tests`, `ChargeWorkflowApi_Tests`; add the missing isolation, mixed-batch, concurrency, idempotency, source-replay, receipt/account, and correction cases from [Charge management end to end](./references/charge-management-end-to-end.md) |
| Concessions | `ConcessionTypeLifecycle_Tests`, `ConcessionRequestCoreApi_Tests`, `ConcessionRequestValidationApi_Tests`, `ConcessionRequestWorkflowApi_Tests` |
| Paid-fee concessions | `ConcessionEligibility_Tests`, `DueFee_Mutations_Tests`; verify a fully paid installment remains Paid, approval creates the exact receipt-linked refund request, and only settlement decreases the paid cash balance |
| Fee adjustments | `FeeAdjustmentApi_Tests` |
| Fee collection | `FeeCollectionAppServiceV2_Tests`, `FeeCollectionV2Api_Tests`, `CollectFeeInputValidator_Tests` |
| Admissions collection and deferred handoff | `FinanceFeeAccount_Tests`, `FinanceFeeAccountModel_Tests`, `AdmissionFinanceCollectionIntegration_Tests`, `AdmissionFinanceSelectionRaceIntegration_Tests`, `AdmissionFeeCollectionInputMapper_Tests`, `ApplicantRefundInput_Tests`, `FinanceReportPayerProjection_Tests`; verify unchanged due/receipt identities across finalization and activation |
| Admissions collection provider races | `AdmissionFinanceCollectionPostgres_Tests` and the applicant/adopted-receipt cases in `FeeRefundApi_Tests`; require a migrated, disposable loopback database named `refund_workflow_test_*` through `SHIKSHAMITRA_REFUND_TEST_DB` |
| Ledger due-date classification | `DueFeeCollectionStatusResolver_Tests`, `LateFeeCollectionRuntime_Tests`; cover due today, within-month due dates, legacy due dates, ungenerated bills, partial settlement, reconciliation and grid/form overdue parity |
| Finance policy defaults | `FinancePolicyDefaultLifecycle_Tests`, `FinancePolicyDefaultPersistence_Tests`, `ConcessionPolicyAppService_Tests`, `CollectionPolicyAppService_Tests`, `FeeStructureAppService_CollectionPolicy_Tests`; cover first activation, replacement, rejected default removal/archive, host and tenant uniqueness, immutable version pins and settings default projection |
| Fee Collection waivers | Cover absence of policy/charge waiver configuration, both source types, row-toggle visibility, reason-only input, server-derived full balance, dedicated apply permission, student scope, idempotency, and non-cash ledger evidence; use the [target acceptance scenarios](./references/fee-collection-waiver-end-to-end.md#acceptance-scenarios) |
| Refunds and receipts | `FeeRefundApi_Tests`, `FeeReceiptApi_Tests`, receipt domain tests |
| Accounts and reconciliation | `TransactionAppService_Tests`, `ReconciliationAppService_Tests`, `ReconciliationApi_Tests` |
| Audit | `FinanceAuditTrailAppService_Tests` |

## Example commands

```bash
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~ChargeCoreApi_Tests|FullyQualifiedName~ConcessionRequestCoreApi_Tests|FullyQualifiedName~FeeAdjustmentApi_Tests"
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Finance"
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~Finance"
```

## Acceptance gates

| Gate | Pass condition |
|---|---|
| Balance integrity | Due, paid, concession, adjustment, refund, and receipt rows reconcile |
| Permission safety | Backend permissions protect read and write actions |
| Tenant isolation | Finance data cannot cross schools |
| Auditability | Approval and payment actions are traceable |
| Charge integrity | Academic year and caller scope are enforced; mixed batches, concurrent collection, retries, source replay, correction/refund, receipt, account, and report totals reconcile |
| Docs | `validate-docs.ps1 -Module finance` passes |

## Student fee assignment

The current preview/commit, year-scoped history, permission and migration contracts are documented in [Student fee assignment](./references/student-fee-assignment.md). That contract supersedes older assignment descriptions that require concession configuration.

## Category and bank routing

See [Income and expense routing](./references/income-expense-routing.md) for the current category selection, school bank, recognition, historical compatibility, and validation contracts.


## Variable fee assignment checks

See [variable fee assignment](./references/variable-fee-assignment.md) for focused suites and the current workflow acceptance checks.

## Income and Expenses workspaces

See [the workspace contract](./references/income-expense-workspaces.md) for separate registers, academic-year months, selected-record details, manual creation and automatic refund visibility.

Accounts workspace coverage: `AccountsRegisterPeriod_Tests`, `IncomeExpenseRouting_Tests` and `AccountsRegisterApi_Tests`.
The register API suite uses isolated SQLite state fixtures for approval/paid/returned read projections; it does not execute payout commands.
Actual payout command/replay scenarios remain in `FeeRefundApi_Tests` and require SHIKSHAMITRA_REFUND_TEST_DB pointing to a migrated disposable refund_workflow_test_ PostgreSQL database.


## Cash Management verification (27 October 2026)

The 27 October pending-cash correction supersedes the previous approve-before-spend example.
Final focused verification passed 38/38 HTTP checks and 10/10 cash domain checks.
The final memory-safe build completed with 0 errors and 195 existing warnings; historical migration designers were omitted only by the local verification target.
The prior-academic-year deposit-date regression was observed failing before the date guard and passing after it; rejected approval preserves pending state, bank and Income/deposit balances.
The Finance docs validator still reports preexisting mojibake in `references/charge-management-e2e-test-plan.md` and `references/income-expense-workspaces.md`; unrelated encoding repairs were left outside this change.
`SimpleCashFlowApi_Tests` covers receipt 10000, pending paid expense 2000, net deposit approval8000, Income 8000, Expense 2000, held cash 0 and unchanged original receipt 10000.
It covers original gross manual-income retries after partial and fully spent finalization, zero Income suppression, four fractional rows with exact two-paise allocation, approval replay and separately seeded legacy approved/transit credit history.
`CashExpenseRoutingApi_Tests` checks pending source options and payer-scoped admission expenses without unrelated bank/student grants.
`CashManagementAuthorizationApi_Tests` and administrator cases retain exact action permission, current database-role self-approval, tenant/year/payer denial and membership-revocation checks.

`CashCollection_Tests` covers pending-only spending, net-count and zero finalization, money conservation, deterministic proportional allocation and immutable original-gross evidence.
Zero-value finalized Income remains stored with `IsConfirmed=false` for audit and is intentionally hidden by existing confirmed-only register, workspace and legacy list/detail queries.
Non-cash automatic confirmation excludes these cash rows.
The original receipt fallback remains blocked whenever modern source Income exists, including suppressed zero rows.

The API and EF test projects use a local verification target omitting unchanged historical generated migration designers; the new cash designer, current snapshot and runtime mappings remain included.
No production project configuration changed.
These isolated SQLite fixtures do not certify PostgreSQL concurrent allocation or physical transaction rollback; the existing serializable source lock, stamps and replay remain in force.
The design-time/runtime Npgsql timestamp difference remains a deployment-path validation requirement.
No production migration, grants or real cash commands are part of this verification.
See [Cash Management](./references/cash-management.md).

## Collection receipt grouping

`CollectionReceiptGrouping_Tests` covers version snapshots, legacy defaults, exact decimal component totals, one-component collections and provisional status parity. `ReceiptGroupingReconciliationApi_Tests` covers combined/separate queue row counts, total preservation, either-row whole-instrument approval/rejection, final receipt links, principal restoration, charge rollback and duplicate decisions. The collect-input validator suite accepts fee-plus-charge collections with one instrument and retains mixed-provisional-instrument containment.

## Family mobile Finance

`MobileFinanceConcessionBenefit_Tests.cs` combines the typed family contract with the annual concession guard for Pending, Approved and PartiallyReversed benefits. It checks eligibility/detail blockers, unaffected concession types, denied preview/create/edit/submit, cancellation with the canonical request stamp, stale-stamp rejection, cancellation replay and unchanged dues/history. `MobileFinanceWireContract_Tests.Canonical_concession_type_projection_preserves_annual_benefit_blocker` checks the shared type-option projection and serialized blocker evidence.

`MobileFinanceScreenSummary_Tests` verifies successful-payment/uncleared/reversed
separation, exact paise, net processed refunds, completed counts and real zero totals.
The persisted `Finance_overview_uses_owned_principal_records_instead_of_mixed_transaction_totals`
case also checks that payment summary is independent of page/status and excludes other
children/years; Concession summary uses applied owned-child dues even on an empty page.
`MobileFinanceWireContract_Tests` exports actual serializer examples containing the new
payment/request summary objects. Use these with the companion mobile summary parser tests.

`MobileFinanceCharges_Tests.cs` extends the persisted family fixture with live ABP principal/permission checks and actual database queries for the dedicated charge page/detail/allocation history. Cases cover the hand-derived `1000-200 verified-300 pending-100 waived=400` equation, legacy/overview/statement parity, reversed collections, full waivers, mixed settlement, voids, processed/reversed/failed refund outcomes, fee-only/charge/combined and provisional/final receipt documents, whole-document totals, independent receipt/refund grants, school calendar dates, search/category/tab/count/paging semantics, hidden decisions, wrong batch identity, sibling/year/tenant/revoked association and explicit denial.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~MobileAcademicAppService_Tests.Charges_|FullyQualifiedName~MobileAcademicAppService_Tests.Finance_" -m:1
# With the dedicated loopback opt-in described below, execute the same read fixtures against PostgreSQL:
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~PostgreSQL_family_charges_" -m:1
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~MobileFinancePermission" -m:1
```

The PostgreSQL cases execute the current EF query/model sources against freshly created test databases. They are not evidence of migration-inclusive CI, production acceptance or power-loss durability. HTTP authorization coverage includes the three additive charge routes; the application tests exercise actual persisted associations and the real permission checker rather than relying only on method metadata.

`MobileFinanceProjection_Tests` covers exact decimal billed/future/credit amounts,
the Fees overdue/due-now/upcoming partition with paise, zero totals, and future
installments that are paid or awaiting clearance (their statuses must not become Upcoming),
receipt ownership across tenant/child/year, real receipt IDs and final/provisional
linkage, and the student-self read-only discriminator. `MobileFinanceAccess_Tests`
adds cases to the persisted mobile fixture using the real ABP permission checker:
existing collection grant acceptance, old SelfService-only denial, exact feature View and guardian command permissions, revoked links,
foreign child/year, revoked enrollment without legacy fallback, and student-self
with a simultaneous guardian link. The standard application harness always allows
authorization, so these cases explicitly instantiate ShikshaMitraPermissionChecker.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~ShikshaMitra.Finance.MobileFinance|FullyQualifiedName~MobileAcademicAppService_Tests.Finance_|FullyQualifiedName~LateFeeCollectionRuntime_Tests.Ledger_" -m:1 -p:BuildInParallel=false
```

Request and activity cases are the `Guardian_and_database_administrator_guardian_submission_stays_pending_without_changing_dues`, `Every_parent_command_requires_guardian_and_exact_grant_before_body_or_resource_validation`, `Parent_request_permission_never_allows_an_unrelated_child`, `Durable_receipt_replays_same_command_and_rejects_changed_child_or_beneficiary`, and `Activity_omits_foreign_drafts_and_private_payloads_and_keeps_per_user_read_state` methods on `MobileAcademicAppService_Tests`. Their principals include the tenant claim, use committed exact grants and verify creator ownership. Guardian reassignment follows the existing one-active-guardian-account constraint.

`MobileFinancePermissionApi_Tests` exercises 21 HTTP authorization/association cases. Receipt coverage also checks the existing Receipts View and Collections View statement permissions, rejects another year and generates an actual owned statement PDF.

`FAMILY_FINANCE_TEST_POSTGRES` opts the application harness into a disposable PostgreSQL instance. It accepts only `Host=127.0.0.1;Port=55438;Database=family_finance_tests;Username=family_tests`, creates a fresh suffixed database per fixture and never resets an existing database. The PostgreSQL-only cases verify real Serializable connections, same-key replay, different-key reservation races and rollback of a losing duplicate command receipt. Parent and administrator-guardian submissions remain Pending with unchanged dues, including submission through the staff route. The ordinary SQLite harness cannot certify PostgreSQL transaction isolation or rollback.

Local source verification compiled Domain.Shared, Domain, Application.Contracts, Application, HttpApi, Host and current EF runtime model/configuration/repository sources. Unchanged generated migration sources and tests referencing those generated classes were excluded through an external local build target; a full migration-inclusive CI build remains a release check. Disposable PostgreSQL used relaxed filesystem synchronization for test speed; these tests establish logical transaction behavior, not power-loss durability. The existing Finance documentation validator reports mojibake in `references/charge-management-e2e-test-plan.md`.

Verification on 2026-10-26: all 47 focused application cases passed across targeted runs, all 21 HTTP authorization cases passed, and all six disposable PostgreSQL workflow/concurrency cases passed. These earlier results cover statement PDF generation, the former family-permission mapping, current association/year checks, creator-only private request/document access, notification privacy/read state, reconciliation and year regressions, policy-calculated refund history, decimal/currency contracts, replay, competing reservations and rollback. The local test database was stopped after verification.

Verification on 2026-10-27 for existing web Finance grants: 18 focused application cases and 30 HTTP/permission-contract cases passed, including role inheritance with explicit user denial, feature View filtering, association/year revocation, guardian-only commands and exact concession action checks. The companion mobile suite passed 109 cases before review and 26 affected cases after review fixes, including draft-only retry recovery; full and focused Flutter analysis were clean. The focused backend build retained the external generated-migration exclusion described above. The docs validator currently flags existing encoding issues in `references/charge-management-e2e-test-plan.md` and `references/income-expense-workspaces.md`.

Synthetic test data only; no deployed school writes, permission reseeding, push,
gateway checkout or scheduled reminder execution is part of this implementation.

## Bank account activity

`BankActivityQuery_Tests` covers paging/query rejection, later historical credit windows, original admission-account scope precedence, manual-row exclusion for partial scopes, paise and disjoint provisional/transit totals.
`BankAccountActivityApi_Tests` checks exact bank View on all four endpoints and rejects operational reads/exports with bank settings View alone.
`BankActivityPostgres_Tests` executes the real composable provider projection against isolated temporary tables: split instruments, signed corrections, cash Income suppression, unverified cash refunds, historical destination JSON, actual credit-date boundaries, transit, tenant/year rejection and stable paging.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter FullyQualifiedName~BankActivityQuery_Tests -m:1
dotnet test test/ShikshaMitra.HttpApi.Tests --filter FullyQualifiedName~BankAccountActivityApi_Tests -m:1
# Set BANK_ACTIVITY_TEST_POSTGRES only for the dedicated isolated cluster described below.
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests --filter FullyQualifiedName~BankActivityPostgres_Tests -m:1
```

The opt-in PostgreSQL suite accepts only host `127.0.0.1`, port `55443`, database `postgres`, user `bank_activity_tests`.
It uses connection-local temporary tables, never an application database, and skips explicitly when its environment variable is absent.
SQLite authorization tests exercise denial before provider execution; they do not certify PostgreSQL JSON projection or snapshot isolation.
Production deployment, real bank settlement matching and production evidence downloads require their separate operational validation.
