---
module: finance
domain: product
content_type: Reference
status: canonical
updated_on: 2026-08-31
owner: backend-team
audience: humans-and-agents
---

# Understand Finance product behavior

TL;DR: Finance protects fee accuracy, payment history, concessions, refunds, adjustments, and account reconciliation.
Treat incorrect Finance reports or due balances as production defects.

## Product areas

| Area | Status | Behavior owned by Finance |
|---|---|---|
| Fee settings | Implemented | Fee categories, fee heads, fee structures, receipt books, charge categories, concession types, and related setup |
| Student fee assignment | Implemented | Assign fee structures to students and expose fee head details |
| Fee collection | Implemented | Collect fees, allocate payments to due rows, expose student fee details, summaries, and exports |
| Receipts | Implemented | Read receipt lists and receipt details after payment activity |
| Charges | Implemented | Create ad hoc charges with maker-checker approval and derived student scope |
| Concession requests | Implemented | Request concessions, approve allocations to due fee rows, reject, cancel, and manage documents |
| Fee adjustments | Implemented | Create credit pools from paid due rows and apply them to pending obligations |
| Refunds | Implemented | Request and process fee refunds separately from adjustments |
| Accounts | Implemented | Income, expense, bank account, transaction categories, transactions, and reconciliation |
| Audit and reports | Implemented | Finance audit trail and Finance report APIs |

## Data-integrity rules

| Rule | Consequence |
|---|---|
| Student academic scope is server-owned | Charge and fee workflows must not trust caller-provided class or division scope |
| Paid and pending due rows are different states | Adjustments must use paid source rows and pending target rows |
| Concession approval allocates to `DueFee` rows | Approved concession amounts must reduce the correct outstanding lines |
| Cash returned to parents uses refunds | Fee adjustments are not cash refunds |
| Maker-checker rules protect approvals | Creators cannot approve or reject their own charge, concession, or adjustment where the domain enforces it |
| Receipt numbers and payment records must remain auditable | Do not silently mutate payment history |
| Concession type names are unique per tenant and academic year after normalization | Duplicate labels cannot create ambiguous request classifications |
| Concession types are lifecycle-controlled master data | Active types cannot be deleted; inactive unused types may be deleted, while used types remain resolvable in requests, reports, and version history |
| Concession evidence definitions are server-enforced | Each document declares mandatory/optional, maximum bytes, and accepted formats; only mandatory evidence blocks approval |

## Fee-head module field

The fee-head `Module` identifies the product module associated with that fee head. It is optional: `null` means the fee head is not associated with a specific product module. When present, it must be a valid `FeeHeadModule` enum value. Existing fee-head dependencies and edit restrictions are unchanged.

## Partial surfaces

No generated Finance contract pack is canonical in this docs tree.
Update [api.md](./api.md) from current source instead of adding raw exports.
