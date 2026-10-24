---
module: finance
domain: receipt-lifecycle
content_type: Reference
status: active
updated_on: 2026-10-23
owner: product-and-engineering
audience: humans-and-agents
---

# Provisional and official receipts

## Operational flow

| Collection method | At collection | Reconciliation | Receipts history |
| --- | --- | --- | --- |
| Cash | Issue an Official Receipt immediately for fees and/or charges | Read-only Cash tab; no approval required | Official document, payment method, amount, date, PDF |
| Cheque, Demand Draft, UPI, Bank Transfer, Credit Card, Debit Card | Issue a Provisional Receipt immediately | Existing payment verification approval/rejection | Provisional document remains accessible while pending |
| Non-cash approved | Create a new Official Receipt; link both documents | Original provisional row becomes Approved and points to the official number | Both numbers retained; one underlying payment |
| Non-cash rejected | No official receipt issued | Existing rejection reverses the collection and requires remarks | Provisional document remains as rejected evidence |

"Kachi" is displayed as **Provisional Receipt**; "Pakki" is displayed as **Official Receipt**. The enum values and historical receipt numbers remain unchanged. Fee, Charge, and Fees and Charges identify document scope separately from its provisional/official lifecycle.

Approval is the existing reconciliation command, not bank-statement import or automatic bank matching. Those services are outside this delivery. The Cash tab acknowledges counter collection, not bank deposit reconciliation.

## Flow contract and integrity

| Concern | Contract |
| --- | --- |
| Outcome / owner | Finance Receipts owns numbered payment evidence; Collections owns the recorded methods and allocations; Reconciliation owns verification |
| Actors / scope | Existing authorized collectors and reconciliation operators; existing tenant, academic-year and payer scope checks remain authoritative |
| Permissions | Receipt list/detail/PDF: `Finance.Fees_InvoiceReceipt.View` constant; reconciliation list/count: `Finance.Accounts_Reconciliation.View`; approve/reject: existing `Finance.Accounts_Reconciliation.Add` constant in `ShikshaMitraSubmodulePermissions` |
| Preconditions | Existing valid dues/charges, collection policy, permitted method, positive amounts, exact allocation/payment checks, idempotency key and payer access |
| Classification | Only cash can finalize at collection. Legacy electronic `PaymentConfirmed` may be absent, true or false; none permits bypassing verification |
| Supported unit | One non-cash instrument per collection. Fee and charge documents for that instrument are verified together under the existing atomic verification workflow. Combined receipt policy is preserved |
| Mixed instruments | Any non-cash payment mixed with another instrument is rejected before collection. No inferred allocation split or partial rollback |
| State | Cash: Official / NotApplicable. Non-cash: Provisional / PendingVerification -> Verified with linked Official, or Rejected. A second terminal action is rejected by existing domain checks |
| Amounts | Receipt totals retain original allocations; approval adds evidence for the same payment and must not collect money a second time. Combined fee plus charge breakdown equals the document total |
| Corrections | Rejection uses the existing collection reversal. Existing approved documents are never renumbered or bulk reclassified. Refund/reversal remains its separate workflow |
| Side effects | Preserve existing collection income routing, approval confirmation, reversal, audit and events. Receipt classification and collection InReconciliation status both follow the same non-cash rule |
| Failure / concurrency | Existing collection retry key and fingerprint retained; existing serializable verification unit of work retained. PDF errors propagate through existing transaction handling; this change does not introduce an outbox or bank match model |
| Compatibility | No database migration. Existing final electronic receipts remain official history. Pending legacy mixed-instrument receipts remain blocked for manual resolution |

Permission names above identify source constant members; the constant values and enforcement in source govern access. This change does not expand approval authority or introduce maker-checker separation. The existing broader reconciliation permission and bank-matching gaps remain tracked in [the reconciliation reference](fee-collection-reconciliation-flow.md).

## Receipt PDF and download contract

- A4 printable document with institution/tenant heading, clear lifecycle title, number/date, student or applicant, GR number, academic year, scope, recorded payment method, fee installment rows, total in INR, signatory and seal spaces, and page numbering.
- Charge-only documents are titled with the same lifecycle as fee documents. Combined documents retain fee/charge amounts. Missing legacy payment evidence says "Not recorded"; it is never inferred to be cash.
- Official documents created on approval identify their provisional predecessor and explain that the two documents represent the same payment.
- Filename: `<Official|Provisional>-Receipt_<Fee|Charge|Fees-And-Charges>_<receipt-number>_<yyyy-MM-dd>.pdf`. Example: `Official-Receipt_Charge_FCH-2026-000006_2026-10-23.pdf`. Receipt numbers are sanitized for safe filenames; pupil names and bank identifiers are excluded.
- Reconciliation PDF previews use the same `/api/v1/fee-receipts/{id}/pdf` endpoint as Receipts, selecting the linked official ID after approval and retaining the server filename.
- Download metadata is exposed as `pdfFileName`; `paymentMethodLabel` appears in receipt list and detail responses. The PDF endpoint returns the same filename in Content-Disposition.
- PDF template blobs use `fee-receipts/{tenant}/v2/{kind}/{id}.pdf`. Old template blobs remain retained; the first download regenerates the new presentation from the original receipt and collection records, keeping its ID, number and date.
- Number prefixes (`FIN`, `FCH`, `KFE`, `KCH`) remain stable to preserve receipt-book references and existing sequences. Display terminology and filenames carry the new names.

## Source and acceptance

`FeeReceiptPaymentPolicy` owns collection classification; `CollectFeeInputValidator` blocks unsafe mixed instruments; `FeeReceiptPresentation` owns labels and filenames; `FeeReceiptManager` and `ReconciliationManager` own issuance/linkage; `FeeReceiptPdfGenerator` owns printable presentation.

Focused coverage: `OfficialReceiptFlow_Tests`, `FeeReceiptPaymentPolicy_Tests`, `CollectFeeInputValidator_Tests`, `FeeReceiptPdfGenerator_Tests`, `ReceiptLifecycleApi_Tests`, `ReceiptGroupingReconciliationApi_Tests`, `ReconciliationApi_Tests` and `FeeReceiptApi_Tests`. Frontend checks and browser evidence belong in the [frontend testing page](../../../../../Shikshamitra-FE/docs/finance/testing.md).

### Verification evidence (23 October 2026)

- Full HTTP host compilation passed with zero errors; existing repository warnings remain.
- Domain receipt suites: 41 passed, 0 failed. Collection validator and production PDF generator checks: 39 passed, 0 failed using standard VSTest and the actual compiled application libraries.
- Receipt student reads use the existing repository query without automatic academic navigation includes; detail lookups project scalar board/medium/class/division fields. This avoids loading the full academic graph for a receipt and preserves tenant/payer scope checks.
- HTTP acceptance: 45 cases passed in the final lifecycle suite; the remaining tab-count shape case passed after its focused rebuild/rerun, covering all 46 selected cases. The ten lifecycle cases exercise actual HTTP collection, idempotent retry, receipt list/detail/PDF, all six non-cash methods, cash fees/charges, linked approval, rejection reversal and exact document counts. Grouping cases verify combined/separate documents and atomic decisions.
- Repeated terminal reconciliation decisions map to HTTP 409 through the canonical problem-details mapper. The exact renamed provisional label and supported tab-count contract are asserted.
- The specialized API test factory retains the real host, test authentication, tenant filters, SQLite persistence, and academic hierarchy while omitting unrelated HR/demo fixture setup. Synthetic setup uses domain fixtures for students; the payment and verification actions run through real HTTP endpoints. This is local acceptance, not a deployed PostgreSQL or authenticated live-tenant smoke test.
- Real API exports: provisional `KFE-2026-000007` and linked official `FIN-2026-000008`, both UPI/INR 100.00, each one A4 page. Every page was rendered and visually inspected; long-name, charge-only and combined examples were checked separately.
- Frontend builds, tests and browser evidence belong to the [frontend testing page](../../../../../Shikshamitra-FE/docs/finance/testing.md). Browser fixture state is synthetic and does not prove persisted ledger transitions.
