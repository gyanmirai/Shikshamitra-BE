---
module: finance
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Validate Finance backend changes

TL;DR: use focused Finance tests for changed financial behavior, then run module docs validation.

Fee Structure configuration, publication and revision contracts: [workspace implementation](./references/fee-structure-workspace.md).

## Required docs checks

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module finance
git diff --check
```

## Focused test map

| Change area | First tests |
|---|---|
| Charges | `ChargeAppService_Tests`, `ChargeCoreApi_Tests`, `ChargeValidationApi_Tests`, `ChargeWorkflowApi_Tests` |
| Concessions | `ConcessionTypeLifecycle_Tests`, `ConcessionRequestCoreApi_Tests`, `ConcessionRequestValidationApi_Tests`, `ConcessionRequestWorkflowApi_Tests` |
| Fee adjustments | `FeeAdjustmentApi_Tests` |
| Fee collection | `FeeCollectionAppServiceV2_Tests`, `FeeCollectionV2Api_Tests`, `CollectFeeInputValidator_Tests` |
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
| Docs | `validate-docs.ps1 -Module finance` passes |
