---
module: finance
domain: accounts-frontend-backend-delivery
content_type: Conceptual
status: active
updated_on: 2026-09-30
owner: product-and-engineering
audience: humans-and-agents
---

# Deliver the simplified Fees-to-Accounts flow

TL;DR: preserve the existing Fees and Accounts modules, reuse Fee Collections payment methods, and build a traceable chain from school fee events to student balances, journals, bank settlement, and reports. This is an implementation contract and status map, **not** a claim that the target APIs or accounting entries are live. The user-facing flow is in [Simplified Accounts operating flow](./accounts-simplified-operating-flow.md); the [Finance Engineering Constitution](./constitution.md) governs every monetary change.
Use the [integrated end-to-end contract](./accounts-integrated-end-to-end-flow.md) for the current detailed accounting equations, instrument statuses, credit reservations, source-owned posting, screen handoff, API proposals and P0/P1 acceptance sequence.
Fee income examples here require earned-service or deferred-income treatment; a schedule or future-service demand must not automatically recognise annual income.

## Decisions that must not drift

1. **No Payment Modes screen, CRUD, or policy selector in Accounts Settings.** Fee Collections already offers Cash, Cheque, Demand Draft, Bank Transfer, Credit Card, Debit Card, and UPI. Reuse its backend `PaymentMethodEnum` and the frontend labels; share conditional instrument metadata rather than copying a second list. Gateway and Wallet are not current methods.
2. Accounts Settings contains Account Heads, Income Categories, Expense Categories, Bank Accounts, and - only after backend delivery - a read-only-first Accounting Mappings summary. Opening Balances is migration-only. Account and Fee Heads do not ask for manual codes.
3. Fees owns fee structure, assignment, demands, collections, concessions, waivers, adjustments, refunds, and receipts. Accounts consumes approved source events; it must never create a second Income entry for tuition or an Expense entry for an advance refund.
4. Payment verification accepts or rejects one instrument. Bank reconciliation matches actual bank-statement evidence. A verified UPI instrument remains in UPI Clearing until bank settlement; verification alone does not debit School Bank.
5. Routine users enter business facts, not debit/credit lines, policy IDs, version IDs, or idempotency keys. The backend owns effective rule resolution, mapping, balanced posting, source links, and audit.

## Current delivery status, verified in source

| Area | Current implementation | Required change |
|---|---|---|
| Account Heads | Active CRUD, hierarchy, status/history and Asset/Liability/Equity/Income/Expense types; no manual code form | Seed/protect control-account roles and block changes that would reinterpret posted history. |
| Income/Expense Categories | Active category-to-default-head mapping; optional tags in current branch | Keep tags optional; version or supersede used mappings rather than changing historical posting meaning. |
| Bank Accounts | Asset-head link and real bank details; current branch generates omitted internal code | Preserve sensitive-detail controls; link payout and statement workflows to stored Bank Account IDs. |
| Payment methods | Fee Collections `PaymentMethodEnum` has seven values; Income/Expense use matching frontend labels and numeric values. Current branch validates new transaction methods against that enum and fixes an obsolete code comment; stored values remain plain `int` | Extract one cross-flow contract and conditional evidence; audit legacy stored values before any migration. No new Accounts setting or payment-method CRUD. |
| Fee setup and assignment | Fee Head, Fee Structure, Payment Terms, and Student Fee Structure Assignment exist | Add treatment-aware income-versus-liability mapping and readiness; retain compatible assignment preview/commit. |
| Dues, collection, receipts | DueFee, Fee Collection, and receipt flows exist | Add an explicit demand view, instrument-level attribution, student-credit handling, and source-to-journal links. |
| Concessions and refunds | Separate Fees workflows exist; the refund payment-details UI currently contains a hardcoded school-bank option list | Connect approved application and payout events to student balance, journal, stored BankAccountId, and reversal evidence; replace hardcoded payout banks with scoped active bank queries. |
| Adjustments | Existing Fees workflow exists, but `DueFee.AdjustableBalance` currently returns outstanding debt and target application lacks equivalent source-credit consumption | Replace debt-as-credit with real available credit, common adjustment/refund reservation, consumption, and concurrency proof. |
| Income and Expenses | Authenticated CRUD at `/api/incomes` and `/api/expenses`; current branch uses `decimal` request/response amounts, requires whole paise, validates the shared method range, and makes entry tags optional. No voucher/journal lifecycle | Add exact action permissions, paid/payable distinction, approval, immutable post, and reversal. Audit old rows with more than two decimal places before tightening database precision. |
| Income/Expense evidence | Current branch removes the misleading Academic Curriculum upload from Accounts entry forms; the current transaction API has no persisted evidence link | Add a Finance-owned staged-document contract, scoped access, retention, and audit before offering upload or requiring voucher evidence. |
| Reconciliation | `/api/v1/finance/reconciliations` approves/rejects receipt verification | Rename the officer-facing concept Payment Verification; build a separate bank-statement matching aggregate and API. |
| Draft Postings / Posting Automation | Frontend-only calls or hardcoded save; no matching backend posting API | Current branch removes them from navigation and makes direct routes explicitly unavailable. Replace only after real journals/mappings exist. |
| Journals, periods, student account, bank matching | No complete cross-module accounting foundation | Build with migrations, immutable source links, exact permissions, reconciliation proofs, and migration-safe rollout. |

The source owners are `src/ShikshaMitra.Application.Contracts/Finance/**`, `src/ShikshaMitra.Application/Finance/**`, `src/ShikshaMitra.Domain/Finance/**`, and `src/ShikshaMitra.HttpApi/Controllers/Finance/**` in the backend. Frontend navigation is the [Finance menu](../../../../../Shikshamitra-FE/src/constants/finance-menu.ts); the [frontend Finance docs](../../../../../Shikshamitra-FE/docs/finance/README.md) own screen and client paths.

## Officer flow with school examples

| Step | Officer sees/enters | Authoritative backend effect | Accounting target, not yet shipped |
|---|---|---|---|
| Setup | Accountant names Tuition Income, Student Receivable, Cash, School Bank, UPI Clearing, and Student Advance; links actual bank and categories | Validate year, type, active status, uniqueness, dependencies, and mapping readiness | System-owned control accounts and immutable mapping versions. |
| Demand | Aarav's published structure schedules Tuition INR 12,000 and Transport INR 3,000 | Generate one idempotent INR 15,000 demand with two fee-head lines | Dr Student Receivable 15,000; Cr Tuition Income 12,000; Cr Transport Income 3,000. |
| Collection | Aarav pays Cash INR 4,000 and UPI INR 6,000 against INR 15,000 due | Record two instruments, allocate INR 10,000, issue linked receipt; leave INR 5,000 due | Dr Cash 4,000; Dr UPI Clearing 6,000; Cr Student Receivable 10,000. |
| Verify and settle | Verify Aarav's UPI reference, then match the INR 6,000 bank credit | Verification affects that instrument only; actual settlement consumes the clearing balance | On settlement: Dr School Bank 6,000; Cr UPI Clearing 6,000. No second income. |
| Advance refund | Meera has INR 2,000 available advance and is approved for INR 1,000 back | Reserve source credit, pay from selected stored bank, leave INR 1,000 advance | Dr Student Advance 1,000; Cr School Bank 1,000. No Expense record. |
| Other income | School receives INR 10,000 donation by bank transfer | Create an Income voucher linked to Donation category and payment evidence | Dr School Bank 10,000; Cr Donation Income 10,000. |
| Expense | School receives an INR 25,000 electricity bill, payable later | Create Expense payable, approve, then make one bank payout | Bill: Dr Electricity Expense / Cr Vendor Payable 25,000; payout: Dr Vendor Payable / Cr School Bank 25,000. |

The example uses INR with integer paise/decimal storage and **proposes accrual accounting** (fee income on demand). That accounting-basis choice must be confirmed before journal implementation. Actual account IDs, policy versions, journal numbers, timestamps, and status transitions are server-owned. A concession after payment creates a refundable credit/payable, not a negative receivable. A reversal posts a linked inverse event rather than editing the original receipt or journal.

## Decisions required before P1 journal implementation

| Decision | Why it changes implementation | Current document assumption |
|---|---|---|
| Fee recognition basis: accrual on demand or cash on receipt | Determines receivable and fee-income journal timing, cancellation treatment, statements, and reports | Accrual examples only; product approval pending. |
| Historical cutover: journal backfill or verified opening balances | Determines migration volume, audit provenance, control-account opening figures, and rollback | No historical journal is invented; cutover method pending. |
| Financial-year/period boundary and permitted backdating | Determines period generation, close blockers, late postings, and correction dates | Monthly periods proposed; school-specific rule pending. |
| Overpayment destination and refund eligibility | Determines whether excess becomes Student Advance, an unapplied receipt, or is rejected | Explicit advance path proposed; eligibility rules pending. |

Until these are settled, do not post real journals for existing school transactions or run a migration that reinterprets historical balances. The safe UI and documentation corrections in this branch do not depend on those decisions.

## Frontend change contract

Every money-changing form uses **enter facts -> server preview -> review impact -> confirm once -> show source/status/journal**. Show loading, empty, filtered-empty, forbidden, validation, retryable error, conflict, and success states. Preserve inputs on recoverable errors. Use existing shared components and exact permission guards.

| Screen | Inputs and actions | Required frontend change |
|---|---|---|
| Accounts Settings | Account Head: name/type/optional parent/notes. Category: name/matching default head/optional description and tags. Bank: real bank/account/IFSC/Asset head; optional UPI/details. | Keep codes and fee policies out. Keep sensitive values masked in lists; show dependencies and effective status. No Payment Modes tile. |
| Fee Categories and Heads | Category/name/treatment; income mapping only when no safe inherited default or authorized override exists. | Use one rule for standalone and embedded Fee Head forms; deposit maps to liability, never a fake Income category. |
| Fee Structure/Assignment | Fee heads, amounts, terms, applicable students and dates. | Show server readiness and demand preview; preserve existing preview/commit; never ask Accounts policy selection. |
| Student Accounts and Demands | Student/year search, schedule/generate/cancel where permitted. | New chronological due, payment, credit, refund, receipt, and journal drill-down; cancellation reason and impact preview. |
| Collections and Receipts | Student, allocation, split instruments using existing Fee Collections methods, conditional reference/bank. | Show due-before-advance allocation, pending instruments, receipt state, duplicate/retry outcome and journal link. |
| Payment Verification | Pending instrument, Verify/Reject/Failed; mandatory reason for negative decision. | Split from current Reconciliation wording; do not imply bank settlement on verification. |
| Concessions, waivers, adjustments, refunds | Business source, eligible amount, reason/evidence, approver decision; stored payout bank for refund. | Show limits and before/after balances; adjustment source must be available credit; refund treatment read-only; no Expense shortcut. |
| Income and Expenses | Category, payer/payee, amount, date; conditional received/paid or payable, bank/reference/evidence. | Replace unrestricted CRUD with preview, draft/submit/approve/post/pay/reverse according to backend available actions. |
| Journals, exceptions, periods, bank matching | Accountant/auditor read and restricted actions. | Add only after backend APIs exist; no fake Save/Finalise or hardcoded event rules. |

## Backend, database, and API change contract

Existing CRUD routes remain compatibility adapters until their owning screens migrate. The following are target contracts; do not expose a route before its permission, validation, transaction, idempotency, audit, and tests are ready.

| Boundary | Backend rule and database requirement | API work |
|---|---|---|
| Common posting | Decimal money with fixed precision and currency; balanced positive journal lines; one event per source transition; immutable posted versions; unique tenant/year/source-event key. | `POST /api/v1/finance/accounting-preview`; `GET /api/v1/finance/setup/readiness`; errors for closed period, missing mapping, stale preview, duplicate command. |
| Setup | Seed control heads and internal event mappings; validate Asset/Income/Expense and active effective versions; preserve account and bank history. No PaymentModeConfiguration entity. | Keep existing setup CRUD; add dependency/readiness queries and restricted mapping list/simulation only after persistence exists. No `/payment-modes` CRUD. |
| Periods and ledger | AccountingPeriod, FinancialEvent, JournalEntry/Line, MappingVersion, command receipt; unique source and idempotency indexes; optimistic concurrency and atomic unit of work. | Period close-preview/close/reopen; journal list/detail/reversal; ledger account drill-down; posting exception retry. |
| Student obligations | Existing assignment and DueFee remain owners; add demand header/lines and student financial projection without duplicating mutable balances. | Demand preview/generate/cancel; student financial account and available-credit reads. |
| Collections | Attribute amount and status to each instrument; sum instruments = receipt; allocation sum = collected amount; handle due, advance, failure, reversal once. | Collection preview/confirm/instrument detail/reverse with idempotency key and row version. |
| Corrections/refunds | Reserve and consume genuine credit; validate refundable paid source and prior refunds; maker-checker; payout from stored BankAccountId; linked reversal. | Credit-source/target queries, adjustment preview/apply, refund accounting-preview/process/fail/reverse. |
| Income/Expense | Exact View/Create/EditDraft/Submit/Approve/Post/Pay/Reverse permissions; paid/payable states; no edit/delete after posting. | Add preview and lifecycle commands beside `/api/incomes` and `/api/expenses`; migrate legacy clients before disabling CRUD writes. |
| Verification and bank matching | Existing verification remains receipt/instrument acceptance. BankStatementImport/Line, Match and Batch are separate; duplicate-import hash, partial-match balance, approval and close controls. | Keep verification compatibility route; add statement import/candidates/match/close routes under a separate bank reconciliation namespace. |
| Reports/audit | Source-to-journal-to-statement links and append-only actor/reason/correlation; report totals from canonical rows. | Trial balance, P&L, balance sheet, cash/bank book, receivable aging, student statement; permissioned export. |

Every command validates current tenant, academic year, user scope, exact action, source state, available balance, effective rule/mapping/period, and expected version inside one transaction. The same idempotency key and payload returns the original result; the same key with a different payload conflicts. Database indexes, not query checks alone, close duplicate races. Background event delivery must use an outbox or equivalent recoverable mechanism. Do not convert existing data to new posting meaning without a tenant-by-tenant backfill and reconciliation report.

## Delivery order and acceptance gates

| Priority | Deliverable | Acceptance evidence |
|---|---|---|
| P0 | Remove false posting UI; secure existing Income/Expense/Adjustment/Refund actions; define shared payment-method interpretation and block unsupported money movement. | Denied API tests, tenant/scope tests, no fake save, no due-as-credit, no duplicate payout. |
| P1 | Accounting periods, seeded control mappings, financial events, balanced journals, command receipts, readiness and previews. | One balanced journal per source event; idempotent retries; closed-period block; migration/backfill proof. |
| P2 | Student Accounts, Fee Demands, collection instruments, receipts, verification, source-to-ledger drill-down. | Aarav split-payment scenario; student receivable equals control account; UPI clearing equals unsettled instruments. |
| P3 | Concession after payment, waiver, genuine-credit adjustment, advance/fee refunds and reversals. | Meera refund and over-refund race tests; no refund as expense; historical receipts unchanged. |
| P4 | Income/Expense voucher lifecycle, bank statement matching, period close, reports and opening balances. | Vendor payable scenario; bank balance matches closed statement; trial balance and subledgers reconcile. |

Release each priority behind a compatibility/migration plan. Do not expose a new menu item merely because a target route is named here. The [backend API guide](../api.md) documents shipped contracts; the [frontend Finance docs](../../../../../Shikshamitra-FE/docs/finance/README.md) own active routes, clients, and browser checks.
