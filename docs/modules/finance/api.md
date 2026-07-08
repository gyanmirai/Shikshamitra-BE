---
module: finance
domain: api
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Use Finance backend APIs

TL;DR: use this page to find Finance endpoint families and route ownership.
Controller source paths are canonical when a route detail changes.

## Shared request rules

Finance APIs require authenticated access and tenant context through the platform pipeline.
Mutations must enforce backend permissions and audit important financial actions.

## Fees API families

| Family | Controller path | Main routes or base route |
|---|---|---|
| Charges | `Controllers/Finance/Fees/Charges/ChargeController.cs` | `api/app/charges` |
| Concession requests | `Controllers/Finance/Fees/Concessions/ConcessionRequestController.cs` | `api/concession-request`, `api/concession-requests` |
| Fee adjustments | `Controllers/Finance/Fees/Adjustments/FeeAdjustmentController.cs` | `api/fee-adjustments` |
| Fee collection | `Controllers/Finance/Fees/Collections/FeeCollectionController.cs` | Fee collection routes |
| Fee receipts | `Controllers/Finance/Fees/Receipts/FeeReceiptController.cs` | Fee receipt routes |
| Fee refunds | `Controllers/Finance/Fees/Refunds/FeeRefundController.cs` | Fee refund routes |
| Student fee assignment | `Controllers/Finance/Fees/StudentFeeAssignment/StudentFeeStructureController.cs` | Student fee structure routes |
| Fee settings | `Controllers/Finance/Fees/Settings/**` | Fee category, fee head, fee structure, receipt book, concession type, and charge category routes |

## Accounts API families

| Family | Controller path |
|---|---|
| Income | `Controllers/Finance/Accounts/Income/IncomeController.cs` |
| Expense | `Controllers/Finance/Accounts/Expense/ExpenseController.cs` |
| Bank accounts | `Controllers/Finance/Accounts/Settings/BankAccountController.cs` |
| Transaction categories | `Controllers/Finance/Accounts/Settings/IncomeCategoryController.cs`, `ExpenseCategoryController.cs` |
| Reconciliation | `Controllers/Finance/Accounts/Reconciliation/ReconciliationController.cs` |

## Reports and audit API families

| Family | Controller path |
|---|---|
| Finance reports | `Controllers/Finance/Reports/FinanceReportController.cs` |
| Finance audit trail | `Controllers/Finance/Audit/FinanceAuditTrailController.cs` |

## Permission families

Use backend constants as the security source of truth.
Known Finance families include `Finance.Fees.Charges.*`, `Finance.Fees.Concession.*`, `Finance.Fees.Adjustment.*`, and related Fees, Accounts, Reports, and Audit permissions.

## Contract rule

Do not create a parallel curated contract tree for Finance.
Add endpoint details here when Finance API docs need more route detail.
Do not restore generated exports under `docs/`.
