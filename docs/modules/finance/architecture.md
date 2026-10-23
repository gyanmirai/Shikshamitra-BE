---
module: finance
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-10-23
owner: backend-team
audience: humans-and-agents
---

# Locate Finance backend code

TL;DR: Finance is split into Fees, Accounts, Audit, Reports, and shared helpers.
Start in contracts for DTO shape, then trace to application services and domain managers.

Fee Structure configuration, publication and revision contracts: [workspace implementation](./references/fee-structure-workspace.md).

Admissions collections use one Finance account and canonical due schedule through real Student linking and enrollment assignment: [admission fee collection](./references/admission-fee-collection.md).

## Layer map

Proration integration uses `AdmissionProrationPolicyResolver` for the Finance year default, `AdmissionJoiningDateReader` for the Registration fact, assignment preparation for canonical calculation, and `WithdrawalProrationService` for withdrawal projection/application/correction. `WithdrawalProrationAllocation` and named `DueFee` transitions enforce credit invariants. `FinanceAuditWriter` accepts a deterministic owner event ID. Existing DueFee extensions and Finance audit storage are reused; no migration is added.

| Layer | Path |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Finance/**` |
| Application | `src/ShikshaMitra.Application/Finance/**` |
| Domain | `src/ShikshaMitra.Domain/Finance/**` |
| EF Core | `src/ShikshaMitra.EntityFrameworkCore/Finance/**` |
| HTTP API | `src/ShikshaMitra.HttpApi/Controllers/Finance/**` |
| Tests | `test/ShikshaMitra.*.Tests/Finance/**` |

## Domain subsystems

| Subsystem | Source area |
|---|---|
| Fees | `Finance/Fees/**` |
| Fee settings | `Finance/Fees/Settings/**` |
| Collections and due fees | `Finance/Fees/Collections/**` |
| Charges | `Finance/Fees/Charges/**` |
| Concessions | `Finance/Fees/Concessions/**` |
| Adjustments | `Finance/Fees/Adjustments/**` |
| Refunds and receipts | `Finance/Fees/Refunds/**`, `Finance/Fees/Receipts/**` |
| Accounts | `Finance/Accounts/**` |
| Audit and reports | `Finance/Audit/**`, `Finance/Reports/**` |

## Implementation rules

| Rule | Why it matters |
|---|---|
| Keep fee balance mutations transactional | Partial financial updates corrupt dues and receipts |
| Use domain methods for due-fee mutation | Concession, payment, adjustment, and refund effects must stay auditable |
| Derive student scope server-side | Caller-supplied scope can leak or corrupt Finance data |
| Preserve maker-checker restrictions | Finance approvals must be traceable and separated from creation |
| Update report docs with calculation changes | Report accuracy is a production requirement |

## Category and bank routing

See [Income and expense routing](./references/income-expense-routing.md) for the current category selection, school bank, recognition, historical compatibility, and validation contracts.
