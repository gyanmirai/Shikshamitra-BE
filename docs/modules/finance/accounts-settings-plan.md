---
module: finance
domain: accounts-settings
content_type: Conceptual
status: active
updated_on: 2026-09-09
owner: finance-team
audience: humans-and-agents
---

# Accounts Settings Governance Plan

## Scope

This release governs four academic-year and tenant-scoped masters: Account Heads, Income Categories, Expense Categories, and Bank Accounts. Collection-channel routing and fee-policy attachment remain separate follow-up capabilities; these masters provide their safe accounting foundation.

## Accounting model

1. The Chart of Accounts uses the five standard head types: Asset, Liability, Equity, Income, and Expense.
2. Account Heads have a stable unique code, optional same-type parent, posting/control flags, currency, effective dates, lifecycle state, version, and optimistic-concurrency stamp.
3. Every Income Category selects exactly one active, posting-enabled Income Account Head as its default ledger destination.
4. Every Expense Category selects exactly one active, posting-enabled Expense Account Head as its default ledger destination.
5. Every Bank Account selects exactly one active, posting-enabled Asset Account Head. An Asset Account Head can represent only one Bank Account in an academic year.
6. Every Fee Head selects exactly one active same-year Income Category. That category resolves the posting-enabled Income Account Head; Fee Heads never point directly to ledger accounts.
7. Category-to-head, Fee-Head-to-category, and bank-to-head mappings are defaults/configuration. Transaction and journal records must retain their resolved posting references so a later configuration change never rewrites history.

## Lifecycle and safety

- Create and edit validate tenant/year scope, unique code/name, compatible head type, effective period, and active posting eligibility.
- Updates carry `ConcurrencyStamp`; stale edits fail instead of overwriting another administrator's work.
- Deactivation is the normal retirement path and requires a reason. Used Account Heads cannot be deactivated.
- Hard delete is restricted to inactive and unused records. The UI displays delete as unavailable when the API reports `canDelete: false`.
- Category default heads cannot change after transactions exist. The category can be renamed or described without changing historical classification.
- Bank account numbers and IFSC/UPI values are excluded from audit property diffs. Lists return masked account numbers; full values are returned only by the authorized detail endpoint used for editing.
- ABP entity auditing supplies immutable change history. Version endpoints expose only approved non-sensitive fields and actor/time metadata.
- All endpoints require action-level permissions and academic-scope authorization.

## Real-world example

Setup:

- Asset head `AST-HDFC-COLLECTION` - HDFC School Collection Account
- Income head `INC-TUITION` - Tuition Fee Income
- Income category `FEE-TUITION` - Fee Collection / Tuition, default head `INC-TUITION`
- Bank account `BANK-HDFC-COL` - HDFC Bank, linked asset head `AST-HDFC-COLLECTION`

When student Aarav pays INR 10,000 tuition through the HDFC collection channel, the future posting engine resolves two independent configuration dimensions:

- What was earned: credit `INC-TUITION` (resolved from the income/fee classification)
- Where money arrived: debit `AST-HDFC-COLLECTION` (resolved from the collection channel's bank destination)

The balanced journal is `Dr HDFC School Collection Account 10,000 / Cr Tuition Fee Income 10,000`. The student, receipt, fee allocation, category, bank destination, and both resolved account-head IDs must be retained on the posting trace. Class-wise accounting is supported by a separate effective-dated routing rule (class/fee head/channel priority), not by duplicating Bank Accounts or changing the core category master.

## Delivery phases

- Phase 1 (this release): governed CRUD, five-type Account Heads, required compatible mappings, required Fee Head to Income Category mapping, status/archive, safe delete, masking, concurrency, audit history, migration/backfill, action permissions, and frontend forms/tables.
- Phase 2: collection channels and effective-dated routing rules for campus/class/standard/fee-head/payment-mode to Bank Account. Income classification continues to resolve through the Fee Head's Income Category.
- Phase 3: immutable double-entry journal posting, reversals, suspense handling, reconciliation, period locks, and accounting reports.
