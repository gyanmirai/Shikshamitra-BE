# Pending Cash Net Deposit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Spend expenses from pending cash, then approve/deposit only the remaining balance into Income.

**Architecture:** Preserve source receipts and cash conservation, update existing cash state transitions and unconfirmed Income allocation atomically, and keep the existing DTO interface. Frontend uses server balances/eligibility and the established Finance components.

**Tech Stack:** .NET9, ABP, EF Core/PostgreSQL; React/TypeScript, RTK Query, MUI/Formik/Yup.

**Spec:** `../specs/2026-10-27-pending-cash-net-deposit.md`

## Global Constraints

- Preserve unrelated work; exact permissions/tenant/year/payer scope remain server-enforced.
- Never alter original receipts or posted history; pending Income may be finalized at its deposited net amount with original-gross audit evidence.
- No production financial writes, migration application, grants, commit or push.
- Update the local server only from one coherent complete verified Host build output.

## Review Focus

- Concurrent expense and approval: no stale approval, overspending or duplicate net Income.
- Fully spent and sub-paise allocation edges: zero finalization and exact rounded allocation.
- Manual Income creation retry after its Amount changes to net: compare the original gross request.
- Source scope, action grants and admin self-approval retain current database authority.
- Existing approved/in-transit history is immutable; new approval needs no extra deposit command.

### Task 1: Backend state, net Income and API acceptance

Files: Domain CashCollection and Transaction aggregates; CashManagementAppService; TransactionAppService cash/retry guards; register queries where zero cash finalizations would leak; cash domain/API tests; active Finance backend docs.

- [x] Add and run failing pending-expense -> net-approval tests, zero-spend finalization, exact original retry and fractional/multiple-entry allocation tests.
- [x] Implement pending-only spending, pending source options, net count validation, atomic confirmed deposit evidence and net Income allocation with gross audit metadata.
- [x] Preserve legacy transit credit behavior, permissions, scope, locks/stamps/replay and receipt totals.
- [x] Run focused build/domain/API checks; request independent review and fix findings. Final build0 errors,38 HTTP checks and10 domain checks passed. Prior-year deposit date review defect verified RED then fixed and GREEN. Actual PostgreSQL overlap/race execution remains unverified.

### Task 2: Frontend amounts and pending source workflow

Files: CashManagement screen/dialog/details, cashManagementRules, ManualEntryForm labels, focused tests, controlled cash fixture, Finance frontend docs.

- [x] Add and run failing tests for three approval amounts and remaining-count validation, zero finalization and pending expense sources.
- [x] Show collected/spent/remaining amounts; prefill count with remaining cash; update copy and pending summaries while preserving shared UI and stale/uncertain retry handling.
- [x] Run focused tests/types/lint and in-app browser flow using synthetic amounts10000/2000/8000. Evidence:47 tests across10 files, focused TypeScript/typed lint and in-app browser confirmed pending spending, net approval and exclusion after settlement.
- [x] Request independent review and fix findings. FE review found no actionable defects; TypeScript/lint gates passed.

### Task 3: Integration and local activation

- [x] Verify real HTTP scenario: receipt10000, pending expense2000, remainder8000, deposit approval8000, Income8000, Expense2000, held0, exact retries and unchanged receipt. Isolated SQLite HTTP fixtures; no real tenant financial writes.
- [ ] Validate docs and diff checks. Review both task results together.
- [ ] Copy one complete successful Host output into a fresh local runtime folder; verify first-party DLL hashes, restart only the known local API, and perform read-only live probes. Never approve/spend real cash for testing.
- [ ] Record results and remaining release limits in the handoff; report user-visible outcome.
