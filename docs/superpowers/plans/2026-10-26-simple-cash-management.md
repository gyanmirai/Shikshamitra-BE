# Simple Cash Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement one Cash Management page connected to collection, approval, Income, Expenses and bank deposits.
**Architecture:** Finance-owned cash source aggregate plus immutable deposit/payment links. Reuse existing category Income confirmation, student settlement, bank setup, register forms and shared FE components.
**Tech Stack:** .NET 9/ABP/EF Core/PostgreSQL, Next.js/React/TypeScript/MUI/Formik/RTK Query.
**Spec:** ../specs/2026-10-26-cash-management-design.md

## Global Constraints
- Only one new Cash Management submenu; no handovers, daily count or advances.
- Immediate official receipt; Income only after independent bank-required approval.
- Direct cash expenses allowed; only actual payment reduces available cash.
- Decimal money, DateTimeOffset, tenant/year/payer scope, idempotency and concurrency.
- No historical cash replay, existing grant reseeding, production mutations, merge or deployment.
- Work in dedicated feat/simple-cash-management branches using existing dependencies.

## Review Focus
- Mixed/split cash instruments: approve only matching category Income rows and preserve cent totals.
- Guessed pending Income IDs and legacy receipt fallbacks must not expose pending cash.
- Simultaneous expense/deposit must not overspend; conflicting retry payloads must fail.
- Cross-year/tenant source IDs and inactive banks must fail before mutation.
- Partial bank credit must preserve residual transit without changing Income.

### Task 1: Backend cash workflow and integration
**Files:** new Finance/Accounts/CashManagement domain/contracts/application/controller/EF configuration; modify CollectFeeWorkflow.Routing.cs, Transaction aggregate/app service/request contracts, FinanceRoutingOptionsAppService, canonical permission catalogue; add Domain and API tests. Keep migrations for root integration task.
**Interfaces:** Frozen HTTP/type contract is in ../../../../tmp/simple-cash-management-contract.md (wrapper tmp). All endpoints require /api/finance/cash-management and exact action permissions. Existing Add Expense gains cashCollectionId and cashConcurrencyStamp.
- [ ] Write and run failing domain tests: 10000 pending -> approve -> expense 2000 -> dispatch 6000 -> credit 6000 leaves 2000; mismatched count, self approval, overdraft, duplicate command conflicts.
- [ ] Implement aggregate named transitions, cash manager and scoped app service with transactional locking/concurrency.
- [ ] Integrate fee/charge/admission collection and manual cash income pending rows, actual expense source consumption, immutable cash-linked expenses, bank lookups and permissions.
- [ ] Add API tests for denial/retry/persistence; build application and run focused tests.

### Task 2: Frontend single page and expense integration
**Files:** src/constants/finance-menu.ts, new app cash-management page/layout, redux/finance/cashManagementApi.ts, screens/finance/accounts/CashManagement/*; modify ManualEntryForm, accountsActivityApi, relevant schemas/tests and permission editor grouping if needed.
**Interfaces:** Consume frozen contract. Use existing collector, no new source field on collection: cash source is automatically the linked payment receipt.
- [ ] Write/run failing client/form/navigation tests for pending/approved state, approval bank/count validation, expense cash source, independent deposit credit.
- [ ] Implement typed queries/actions and permission-gated shared table/detail/dialog flow with one submenu.
- [ ] Integrate existing Expense cash-source selection and Cash Income success message; invalidate affected cash/register tags after collection/expense.
- [ ] Run focused tests, typecheck and lint.

### Task 3: Cross-system integration, migration and verification
**Files:** AccountsActivity queries/details, migration plus snapshot, Finance docs and end-to-end tests.
**Interfaces:** Consume Task 1 domain/DTO and Task 2 UI. Freeze API adjustments through explicit communication.
- [ ] Write/run pending-cash register exposure regression and partial-credit/idempotency API tests.
- [ ] Close all pending Income fallback/detail/totals/export paths; generate additive EF migration and verify legacy safety.
- [ ] Run backend build and focused Finance/security/API tests; frontend tests/type/lint/build; docs checks and diff checks.
- [ ] Run in-app browser on actual page/controlled fixtures, inspect desktop/mobile forms and table; report fixture limits.
- [ ] Independent task and whole-change review, repair material findings, update canonical Finance docs and final handoff with evidence.
