---
module: finance
domain: product
content_type: Reference
status: canonical
updated_on: 2026-07-06
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

## Partial surfaces

No generated Finance contract pack is canonical in this docs tree.
Update [api.md](./api.md) from current source instead of adding raw exports.
