---
module: finance
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-09-24
owner: backend-team
audience: humans-and-agents
---

# Validate Finance backend changes

TL;DR: use focused Finance tests for changed financial behavior, then run module docs validation.

Fee Structure configuration, publication and revision contracts: [workspace implementation](./references/fee-structure-workspace.md).

Charge Management browser, API, Fee Collection, receipt, reversal, security, and edge-case coverage: [end-to-end test plan](./references/charge-management-e2e-test-plan.md).

## Required docs checks

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module finance
git diff --check
```

## Focused test map

| Change area | First tests |
|---|---|
| Charges | `ChargeAppService_Tests`, `ChargeCoreApi_Tests`, `ChargeValidationApi_Tests`, `ChargeWorkflowApi_Tests`; add the missing isolation, mixed-batch, concurrency, idempotency, source-replay, receipt/account, and correction cases from [Charge management end to end](./references/charge-management-end-to-end.md) |
| Concessions | `ConcessionTypeLifecycle_Tests`, `ConcessionRequestCoreApi_Tests`, `ConcessionRequestValidationApi_Tests`, `ConcessionRequestWorkflowApi_Tests` |
| Paid-fee concessions | `ConcessionEligibility_Tests`, `DueFee_Mutations_Tests`; verify a fully paid installment remains Paid, approval creates the exact receipt-linked refund request, and only settlement decreases the paid cash balance |
| Fee adjustments | `FeeAdjustmentApi_Tests` |
| Fee collection | `FeeCollectionAppServiceV2_Tests`, `FeeCollectionV2Api_Tests`, `CollectFeeInputValidator_Tests` |
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
