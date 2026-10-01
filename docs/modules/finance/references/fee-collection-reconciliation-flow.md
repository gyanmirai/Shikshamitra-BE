---
module: finance
domain: fee-collection-reconciliation
content_type: Reference
status: active
updated_on: 2026-10-01
owner: product-and-engineering
audience: humans-and-agents
---

# Fee Collection to bank reconciliation: current gaps and target flow

## Decision and scope

This is the implementation contract for money received through Fee Collection, from the collection command to bank evidence, receipt finalization, accounting, correction, and reports. The two distinct decisions are **payment verification** (is a provisional instrument genuinely settled?) and **bank reconciliation** (does a bank statement line match the booked settlement?). A cashier assertion is evidence to record, not a bank match. Cash is verified at the counter but still belongs in cash/deposit reconciliation. The workflow must keep fee, charge, late-fee, and payment-instrument amounts traceable without rewriting the original collection.

**Current readiness: blocked for full bank reconciliation.** The implemented approve/reject API handles provisional receipt verification. There is no bank statement ingestion, statement-line match model, or operational account-posting chain in this path. Existing approval must not be represented to users as bank-reconciled.

The collection validator now rejects a provisional payment mixed with another
instrument or spanning both fee and charge receipts. It asks staff to record
those payments separately until each instrument has its own allocation shares.
The frontend no longer exposes the nonexistent `match`, `by-head`, or
`post-statement` operations or the In-Transit placeholder. These safeguards do
not add bank matching or journal posting.

## Source trace and observed behavior (28 September 2026)

| Link | Current implementation | Observed gap |
|---|---|---|
| Collection | `FeeCollectionAppService`, `FeeCollectionManager`, `FeeRecordMetadata`, DueFee and charge allocations, payment-method records, and fee/charge receipts | One transaction may contain multiple instruments and fee plus charge scopes, but receipts are per scope and not linked to a specific instrument or its allocation share. |
| Receipt policy | `FeeReceiptPaymentPolicy` and `FeeReceiptManager.CreateReceiptsForTransactionAsync` | Cheque/DD make a transaction provisional; cashier confirmation determines fee receipt status for other noncash methods. Charge receipt kind uses only cheque/DD verification and can disagree with a provisional fee receipt in the same transaction. |
| Verification | `ReconciliationManager.ApproveAsync/RejectAsync` and `/api/v1/finance/reconciliations/{id}/approve|reject` | Approval creates a final receipt, then marks the provisional receipt verified. This change permits provisional charge transitions, returns the verified provisional status and ID, and blocks decisions when a collection has multiple instruments or original receipts. For the remaining single-instrument/single-receipt path, approval marks the parent Paid/Partial; rejection reverses that parent's scope allocations and marks it Reversed. Instrument-level attribution is still required before the blocked combinations can be processed. |
| Inbox | `EfCoreFeePaymentReceiptRepository` and reconciliation list/count APIs | List filters by any payment method on the parent transaction. Row detail chooses the first noncash instrument. Counts join every instrument to each provisional receipt. In a split payment, row, tab, amount, and evidence can describe different instruments. |
| Accounting | Reconciliation event handlers in `FinanceEventHandlers` | The approved/rejected handlers only log. No verified posting, clearing-account transfer, bank match, or compensating journal is proved by this flow. |
| Frontend | `/finance/reconciliation`, `Finance-reconciliation`, `redux/finance/reconciliation.ts` | Cheque/DD use approve/reject. Bank Transfer calls a `match` endpoint absent from `ReconciliationController`; `by-head` and `post-statement` clients also have no controller endpoints. "In-Transit" is a placeholder, and `BankStatements` reads the receipt-list response as `data.data`. Gateway/wallet count fields exist in the client contract but are not populated by backend count mapping. |
| Tests | `ReconciliationApi_Tests`, validator tests, receipt sequence tests | Single receipt happy paths and duplicate terminal actions are covered. No bank statement, mixed-instrument, fee-plus-charge sibling, concurrent checker, or PostgreSQL posting/balance proof exists. |

The current frontend route and client wiring are owned by the [frontend Finance documentation](../../../../../Shikshamitra-FE/docs/finance/workflows.md). This page owns the financial invariants and target backend contract.

## Target operational journey

```text
Collect and allocate -> record each payment instrument -> issue provisional/final evidence
      -> receive external settlement evidence -> match/exception review
      -> independent verification -> final receipt and balanced posting
      -> bank statement match -> reconciled status -> reports and period close
```

| Step | Actor and system action | Required evidence and outcome |
|---|---|---|
| 1. Collect | Cashier submits a stable idempotency key. Server validates live dues, charges, late fee, currency precision, scope, and exact sum of instruments and allocations. | Immutable collection ID, allocation IDs, payment instrument IDs, receipt IDs, policy/account mapping snapshots, actor, and time. |
| 2. Classify | Server classifies **each instrument** as counter-settled, awaiting external confirmation, failed, or exceptional. | Cheque/DD remain pending until clearance; an electronic payment requires provider/bank evidence unless a documented trusted webhook confirms it. Cash is counter-settled and later linked to a deposit. |
| 3. Ingest | Authorized accountant imports a bank statement or receives a signed provider settlement event. | Bank account, statement identity, line ID, amount, currency, value date, reference, source hash, import actor, and duplicate detection. Raw evidence is immutable and sensitive identifiers are masked in UI/logs. |
| 4. Suggest matches | Deterministic service proposes matches by tenant, bank account, amount, currency, reference, direction, and date window. | Exact, unique candidate may be suggested; ambiguous, partial, duplicate, fee-net, and no-reference lines remain exceptions. Suggestions never finalize money. |
| 5. Review | A checker independent of the collector confirms or rejects a candidate with a reason and evidence reference. | Server rechecks current state, scope, period, amount, unused statement capacity, and concurrency token in one transaction. Rejection of an instrument restores only its attributable allocation share; unaffected instruments remain valid. |
| 6. Finalize | System issues final receipt evidence for verified value, creates/reverses the corresponding accounting entries, and links provisional and final evidence. | One finalization per instrument and scope, stable retry outcome, immutable audit, recoverable PDF generation, and event delivery after commit. |
| 7. Bank reconcile | Accountant confirms the booked bank/clearing entry against one or more statement lines. | Match group records both sides and exact applied amounts. Reconciliation is separate from receipt verification, with an independently auditable status. |
| 8. Close and report | Authorized user reviews unmatched items, in-transit items, exceptions, and ledger-to-bank differences before period close. | As-of reconciliation report, source drill-down, reviewer, timestamps, correction links, and no unexplained difference. |

## Data and state contract

Use an **instrument settlement** as the smallest independently verifiable unit. Every payment-method record needs a stable ID and allocated shares of fee DueFees, charges, and late fees. A scope receipt may show several instruments, but its provisional/final status must be derived from its exact settlement shares. A mixed cash plus cheque payment cannot use one undifferentiated provisional receipt to roll back all value. Until allocation shares and an instrument-to-receipt link exist, reject mixed provisional collections at collection validation or route them to a controlled legacy exception queue; never guess how to split their money on rejection.

| Fact | Required fields or invariant |
|---|---|
| Instrument settlement | Tenant, academic year, collection ID, payment-method-record ID, amount as `decimal(18,2)` with currency, payment reference, received/value timestamps, expected bank account, status, version, evidence IDs. Unique source reference is scoped by provider/bank account and tenant where provider rules permit. |
| Allocation share | Settlement ID plus DueFee/charge/late-fee allocation ID and positive amount. Shares sum exactly to instrument amount; fee/charge receipt scope totals are derived from shares. |
| Statement import | Tenant, bank account, statement external ID, source file hash, imported-at and actor; immutable line identity and raw-to-normalized mapping. Duplicate file or line is idempotent. |
| Match group | One or more settlement/posting sides and one or more statement lines with positive applied amounts, evidence, status, maker/checker, version, reason. Sum of applied amounts must agree on both sides in one currency. |
| Journal | Balanced, immutable debit/credit lines linked to source settlement/match/correction and immutable account mapping version. Do not use a current Fee Head or bank master to relabel historical postings. |
| Receipt | Number, scope, settlement/share identity, status, linked predecessor/successor, stable PDF artifact state. A final receipt never appears before its value is verified. |

Instrument states: `Recorded -> AwaitingSettlement -> EvidenceReceived -> Verified` or `Recorded/AwaitingSettlement/EvidenceReceived -> Failed`. `Verified -> Reversed` is a separate correction with linked evidence, never a return to Pending. Bank match states: `Unmatched -> Suggested -> Matched -> ReversedMatch`, with `Exception` for unresolved discrepancies. A cash deposit can be `InTransit` between counter acceptance and bank credit. Receipt verification and bank matching expose separate status fields; the screen must not collapse them into one "Approved" flag.

### Monetary equations

- For each collection: `sum(payment instrument amounts) = sum(fee principal + late fee + charge allocations)` exactly at two decimal places. No floating-point tolerance.
- For each instrument: `sum(attributed allocation shares) = instrument amount`.
- For each due/charge: `outstanding = original obligation + permitted increases - concessions/waivers - verified and reserved pending payment effects - adjustments + reversed payment effects`; the UI must show pending versus verified effects separately.
- For each settlement: `verified + failed + pending = original instrument amount`, with partial settlement represented explicitly rather than silently changing a receipt total.
- For each bank account and as-of date: `book balance + outstanding deposits - uncleared payments +/- documented bank-only adjustments = statement balance`. Every difference must link to an open item, not a balancing plug.
- Every journal has `sum(debits) = sum(credits)` in the same currency. Bank fees, gateway fees, interest, and chargebacks are separate facts and postings.

## API, permissions, and failure contract

Current list/count and approve/reject routes remain legacy receipt-verification APIs. Add explicit endpoints only after the instrument model is in place: create/preview statement import, list unmatched statement lines and pending settlements, suggest match, confirm match, mark exception, reverse match, verify/fail settlement, and retrieve an as-of bank reconciliation report. A confirm request carries statement-line IDs, applied amounts, expected versions, and an idempotency key; the server recomputes every amount. Use `409` for stale version, duplicate use, or terminal transition; return stable business error codes. Do not create a `match` button that merely toggles a boolean.

Define distinct backend permissions for statement import, view, suggest, confirm match, exception handling, verify, fail/reject, reverse, report/export, and period override. Enforce tenant, academic-year, bank-account and student scope on both list and ID actions. The collector cannot verify or fail their own instrument; a statement importer should not confirm their own exceptional manual match. Frontend menu, route, buttons, and clients must map to those exact backend permissions. The current `Accounts_Reconciliation.Add` grant used for both approve and reject is insufficient for that target.

Store each command and audit intent with its financial DB transaction. Use transactional outbox for downstream posting/notifications and an idempotent, repairable PDF job after commit. A timeout retry returns the already committed result. Competing checkers cannot both finalize a settlement or consume a statement line. A failed import or posting is visible in an operations queue with retry and a linked error, rather than being dropped or silently marked reconciled.

## Edge-case acceptance matrix

| Scenario | Required result |
|---|---|
| Cash only; later cash deposit | Final counter receipt, pending deposit reconciliation, then match to bank credit without changing collection. |
| Cheque/DD clears or bounces | Clearance verifies only that instrument; bounce restores its attributed dues/charges and creates correction evidence. |
| Cash + cheque; fee + charge in one collection | Cash share remains final if cheque fails; fee and charge share totals and receipts remain consistent. |
| Two electronic instruments; only one settles | Settled share becomes verified; other remains pending/failed. No transaction-wide Paid/Reversed shortcut. |
| Partial bank credit, net gateway fee, or bank charge | Keep gross student payment and explicit fee/charge difference; require approved split or exception, not an amount overwrite. |
| One statement line for several collections; one collection across several statement lines | Explicit many-to-many applied amounts; no double use of line capacity. |
| Duplicate reference, duplicate import, missing reference, same amount from two students | No automatic final match; idempotent import and ambiguous candidate queue. |
| Refund, chargeback, reversal after verification/match | Linked compensating receipt/journal and match reversal; original evidence retained. Refund eligibility excludes failed/reversed amounts. |
| Late bank arrival after month/year close | Value date and posting date both retained; closed-period policy requires authorized correction, never silent backdating. |
| Student transfer, inactive bank account, revoked permission, or tenant mismatch | Historical facts remain readable to authorized users; new settlement/match fails scope/configuration checks. |
| Two cashiers or two checkers race | One durable winner, one conflict, no overcollection, duplicate final receipt, or duplicate match. Test on PostgreSQL. |
| Client timeout after collection, verification, PDF, import, or match | Same key produces same result, or a visible recoverable state; no duplicate money movement. |
| Bank account/currency mismatch, negative amount, zero amount, malformed date, timezone boundary | Reject or exception with stable reason. Use `DateOnly` for value date and `DateTimeOffset` for event time. |
| Receipt PDF/blob failure or event handler failure | Durable financial state remains explainable and a repair worker regenerates evidence; no orphaned "success" claim. |
| Reconciliation report, exports, student balance, account ledger disagree | Release gate fails until totals trace to the same settlement, allocation, and posting rows. |

## Delivery order and release gates

1. **P0 containment:** the backend now blocks unsafe approval/rejection of mixed or multi-receipt transactions, permits provisional charge transitions, and returns the provisional receipt ID in the action response. Add integration tests for fee-plus-charge and duplicate/concurrent actions. Remove or disable frontend controls for routes not implemented by the backend, including `match`, `by-head`, `post-statement`, and the In-Transit placeholder.
2. **P0 canonical model:** migrate money to decimal in the touched chain, add instrument allocation shares, separate settlement and bank-match status, backfill only deterministic legacy rows, and quarantine ambiguous history for manual review. Add unique constraints and concurrency versions.
3. **P0 evidence and posting:** implement bank/provider import, normalized statement lines, matching and exceptions, balanced postings and reversals, outbox/PDF repair, and period gates. Test real PostgreSQL transactions and rollback.
4. **P1 operations UI:** replace method-specific duplicated tables with one typed queue and tabs for Pending Verification, Unmatched Bank, Exceptions, In Transit, and Reconciled. Show receipt, instrument, account, amount, evidence, exact available actions, and reason. Provide an as-of reconciliation report and export.
5. **Acceptance gate:** run end-to-end collection -> statement import -> independent verification -> posting -> match -> report, then failure, refund/reversal, concurrency, tenant/scope, permissions, duplicate import, and period-close cases. Prove `statement balance - adjusted book balance = 0` or explain every open item.

Do not bulk-mark old provisional receipts "verified" during migration. For each legacy collection, derive instrument attribution only when one instrument and one receipt scope make it unambiguous; otherwise preserve the original record and place it in a reviewed exception queue. The frontend must label the current queue "Payment verification" until bank matching and account reconciliation are implemented.
