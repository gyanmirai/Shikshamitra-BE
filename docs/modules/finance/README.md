---
module: finance
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-10-14
owner: backend-team
audience: humans-and-agents
---

# Understand the Finance backend module

Receipt classification, approval, PDF format and naming: [Provisional and official receipts](./references/receipt-lifecycle.md).

Pooled cash expenses, academic-year cash deposits and legacy bank credits: [Cash Management](./references/cash-management.md).

TL;DR: use this page as the entrypoint for Finance behavior, API families, architecture, workflows, and validation.
Finance covers fees, collections, concessions, adjustments, refunds, receipts, accounts, audit, and reports.

Fee Structure configuration, publication and revision contracts: [workspace implementation](./references/fee-structure-workspace.md).
For current Fees-to-Accounts work, use the [integration-only posting foundation](./references/accounts-posting-foundation.md); older Accounts proposals in this directory include credit and collection-flow changes outside that scope.

Category-based income, refund expenses, and school bank selections: [Income and expense routing](./references/income-expense-routing.md).

Variable structure pricing and manual student entry: [variable fee assignment](./references/variable-fee-assignment.md).

## Read order

| Step | File | Use it to |
|---|---|---|
| 1 | [product.md](./product.md) | Understand Finance product areas and data-integrity rules |
| 2 | [api.md](./api.md) | Find endpoint families, permissions, and route ownership |
| 3 | [architecture.md](./architecture.md) | Locate source paths and domain boundaries |
| 4 | [workflows.md](./workflows.md) | Trace charge, concession, adjustment, collection, refund, and reconciliation flows |
| 5 | [testing.md](./testing.md) | Pick focused Finance validation commands |
| 6 | [Concession policy flow](./references/concession-policy-flow.md) | Review the proposed versioned concession policy, fee assignment, request, approval, allocation, and correction design |
| 7 | [Concession policy end-to-end](./references/concession-policy-end-to-end.md) | Configure, integrate, deploy, and verify concession policies |
| 8 | [Collection policy end-to-end](./references/collection-policy-end-to-end.md) | Follow the target staff, frontend, backend, runtime, and rollout flow for collection policies |
| 9 | [Refund Policy flow](./references/refund-policy-flow.md) | Review the proposed policy, receipt-linked request, approval reservation, settlement, accounting, reversal, and reporting design |
| 10 | [Proration Policy end-to-end](./references/proration-policy-end-to-end.md) | Review the current gap and implement policy versioning, fee attachment, charge periods, demand calculation, downstream integrations, and rollout |
| 11 | [Concession flow review](./references/concession-flow-end-to-end-review.md) | Review the current cross-stack verdict, defects, edge cases, test gaps, remediation priorities, and release gates |
| 12 | [Charge management end to end](./references/charge-management-end-to-end.md) | Review the shipped charge, collection, receipt, source-integration flow, release gaps, target contract, and phased delivery gates |
| 13 | [Fee Collection waiver end to end](./references/fee-collection-waiver-end-to-end.md) | Review the approved target for a reason-only full-balance waiver toggle on eligible late-fee and charge rows in Fee Collection |
| 14 | [Fee Collection to reconciliation](./references/fee-collection-reconciliation-flow.md) | Trace the current payment-verification path, release gaps, bank-match target, and edge-case acceptance gates |
| 15 | [Simplified Accounts operating flow](./references/accounts-simplified-operating-flow.md) | Reorganize Fees and Accounts around officer workflows, add the missing submodules, automatic accounting, target APIs, and rollout gates |
| 16 | [Accounts frontend/backend delivery](./references/accounts-frontend-backend-delivery.md) | Track shipped versus target behavior, Fee Collections payment-method reuse, cross-stack work, examples, and acceptance gates |
| 17 | [Integrated Accounts end-to-end flow](./references/accounts-integrated-end-to-end-flow.md) | Implement one source-to-journal chain for Fees, credit, refunds, other income/expense, settlement, and close with simple staff forms |
| 18 | [Accounts posting foundation](./references/accounts-posting-foundation.md) | Inspect the partial event and journal schema, invariants, and live-posting gates |

## Source of truth

| Need | Source |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Finance/**` |
| Application logic | `src/ShikshaMitra.Application/Finance/**` |
| Domain rules | `src/ShikshaMitra.Domain/Finance/**` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Finance/**` |
| HTTP controllers | `src/ShikshaMitra.HttpApi/Controllers/Finance/**` |
| Tests | `test/ShikshaMitra.*.Tests/Finance/**` |

## For agents

Use this table before searching code:

| Question | First file |
|---|---|
| What does Finance own? | [product.md](./product.md) |
| Which endpoint or permission should I use? | [api.md](./api.md) |
| Where is the code? | [architecture.md](./architecture.md) |
| Which workflow should I trace? | [workflows.md](./workflows.md) |
| Which tests should I run? | [testing.md](./testing.md) |
| How should Concession Policy integrate with fee structures and student requests? | [Concession policy flow](./references/concession-policy-flow.md) |
| How does concession policy work end to end? | [Concession policy end-to-end](./references/concession-policy-end-to-end.md) |
| How should collection policy work end to end? | [Collection policy end-to-end](./references/collection-policy-end-to-end.md) |
| How should Refund Policy integrate with payments, receipts, settlement, accounts, and reports? | [Refund Policy flow](./references/refund-policy-flow.md) |
| How should Proration Policy connect fee structures, assignments, demands, and downstream Finance modules? | [Proration Policy end-to-end](./references/proration-policy-end-to-end.md) |
| Is the current concession flow production-ready, and which scenarios must be covered? | [Concession flow review](./references/concession-flow-end-to-end-review.md) |
| How does Charge Management connect to fee structures, policies, collections, receipts, accounts, and source modules? | [Charge management end to end](./references/charge-management-end-to-end.md) |
| How should late-fee and charge waivers work in Fee Collection? | [Fee Collection waiver end to end](./references/fee-collection-waiver-end-to-end.md) |
| How should Fees and Accounts be reorganized into a simpler finance-officer flow? | [Simplified Accounts operating flow](./references/accounts-simplified-operating-flow.md) |
| Which Accounts and Fees changes must frontend and backend deliver, and what is already shipped? | [Accounts frontend/backend delivery](./references/accounts-frontend-backend-delivery.md) |

## Validation

Run these commands from the `Shikshamitra-BE` repo root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module finance
git diff --check
```

## Student fee assignment

The current preview/commit, year-scoped history, permission and migration contracts are documented in [Student fee assignment](./references/student-fee-assignment.md). That contract supersedes older assignment descriptions that require concession configuration.

## Income and Expenses workspaces

See [the workspace contract](./references/income-expense-workspaces.md) for separate registers, academic-year months, selected-record details, manual creation and automatic refund visibility.
