---
module: finance
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-10-17
owner: backend-team
audience: humans-and-agents
---

# Validate Finance backend changes

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
