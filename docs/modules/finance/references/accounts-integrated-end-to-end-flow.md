---
module: finance
domain: accounts-integrated-end-to-end-flow
content_type: Conceptual
status: active
updated_on: 2026-09-30
owner: product-and-engineering
audience: humans-and-agents
---

# Connect Fees and Accounts end to end

TL;DR: staff enter a financial action once in its existing module; the backend updates the student records, records the balanced Accounts entry, and links the receipt, approvals, settlement, audit, and reports.
This is the active target flow and implementation contract, based on source reviewed on 30 September 2026.
It does not claim that journals, student advances, accounting periods, or bank-statement reconciliation are implemented.
The [frontend screen contract](../../../../../Shikshamitra-FE/docs/finance/references/accounts-integrated-screen-flow.md) owns fields, interactions, clients, and navigation.
The [existing delivery assessment](./accounts-frontend-backend-delivery.md) and [Finance constitution](./constitution.md) remain applicable.

## 1. Current Accounts module assessment

The existing Fees workflows should remain the owners of their transactions.
Accounts supplies accounting evidence for those workflows rather than asking staff to enter the same transaction again.

| Source reviewed | Current behavior | Target integration |
|---|---|---|
| [Account types](../../../../src/ShikshaMitra.Domain.Shared/Finance/Accounts/Settings/AccountSettingTypes.cs) and [AccountHead](../../../../src/ShikshaMitra.Domain/Finance/Accounts/Settings/AccountHead.cs) | Asset, Liability, Equity, Income, and Expense types exist; heads are academic-year scoped | Preserve them and add protected, durable ledger identities and control-account roles |
| [Income/Expense service](../../../../src/ShikshaMitra.Application/Finance/Accounts/Transactions/TransactionAppService.cs) | Records category, amount, date, method, and notes; validates active category/default head; supports update/delete | Introduce a voucher lifecycle and automatic balanced postings; preserve old records during cutover |
| [Collection command](../../../../src/ShikshaMitra.Application/Finance/Fees/Collections/CollectFeeWorkflow.cs) | Allocates payments, changes dues, generates receipts and audit; serializable transaction and retry fingerprint | Add per-instrument attribution, cash/bank/clearing postings, and explicit student credit |
| [Payment receipt rules](../../../../src/ShikshaMitra.Domain/Finance/Fees/Collections/Policies/FeeReceiptPaymentPolicy.cs) | Cash final; cheque/DD provisional; electronic receipt depends on cashier bank confirmation | Keep receipt evidence but distinguish payment acceptance, settlement, and bank matching |
| [Receipt verification](../../../../src/ShikshaMitra.Domain/Finance/Accounts/Managers/ReconciliationManager.cs) | Approval creates final receipt; rejection restores allocations; mixed collections are guarded as ambiguous | Add instrument-specific acceptance/failure; keep external bank matching separate |
| [Refund processing](../../../../src/ShikshaMitra.Application/Finance/Fees/Refunds/FeeRefundAppService.Commands.cs) | Scoped commands, settlement attempts, approval/reservation logic, serializable processing and reversal exist | Add stored payout bank, credit consumption, accounting source links, and settlement/reversal evidence |
| [Adjustment manager](../../../../src/ShikshaMitra.Domain/Finance/Fees/Adjustments/FeeAdjustmentManager.cs) and [DueFee](../../../../src/ShikshaMitra.Domain/Finance/Fees/Collections/Aggregates/DueFee.cs) | Manager reads `AdjustableBalance`; that property currently returns outstanding debt; application reduces target due without an equivalent source-credit consumption | Correct this before expanding adjustments; use genuine credit with reservation and consumption |
| [Waiver service](../../../../src/ShikshaMitra.Application/Finance/Fees/Collections/FeeWaiverAppService.cs) | Immediate full remaining late-fee/charge waiver with reason, dedicated permission and retry protection | Preserve the short staff action; post the applied financial reduction where an obligation was booked |
| [Finance event handlers](../../../../src/ShikshaMitra.Application/Finance/EventHandlers/FinanceEventHandlers.cs) | Existing handlers log collection, concession, charge, adjustment and refund events | Logging is not accounting; add authoritative local posting plus recoverable external delivery |

## 2. What is already correct

- Keep fee structures, published payment-term versions, assignment snapshots and individual DueFee lines.
- Keep allocated collection, split payment instruments, numbered receipts, retry protection and collection audit.
- Keep the concession request, evidence, maker-checker and reversal journey.
- Keep receipt-linked refund eligibility, approval, settlement attempts and reversal history.
- Keep category-to-account-head and bank-to-Asset-head mappings.
- Keep codes, debit/credit inputs and Accounts policy selectors out of routine forms.
- Reuse the existing seven Fee Collections payment methods across all workflows.
- Keep the direct, reason-only waiver action; do not add another waiver approval module.

## 3. Current gaps and issues

| Priority | Confirmed gap | Required result |
|---|---|---|
| P0 | Unpaid outstanding is exposed as adjustment credit | No adjustment can manufacture credit from debt |
| P0 | Allocation links are collection-level; a mixed provisional receipt cannot be decided independently | Rejecting one cheque must preserve accepted cash and other instruments |
| P0 | Existing financial actions have no authoritative balanced journal | Every enabled monetary transition has one traceable posting, or the local action rolls back |
| P0 | Mutable Income/Expense CRUD is not posted accounting history | Drafts remain editable; posted amounts are corrected through linked reversals |
| P0 | Existing double contracts/ETOs and tolerance comparisons remain in touched flows | New accounting contracts use decimal/whole paise; legacy amounts are checked before conversion |
| P1 | No shared student credit ledger across advances, concession credits, adjustments and refunds | One available/reserved/consumed credit calculation prevents double use |
| P1 | No bank statement matching or durable cash/bank ledger | Bank movements, clearing and statement matches reconcile without duplicate income |
| P1 | No period close and source-to-journal report chain | Accountant can prove totals and close periods with traceable exceptions |

## 4. Finance integration review and the simple operating flow

### Staff journey

```text
One-time setup by accountant and fee administrator
    -> publish fee structure and payment terms
    -> assign student fees and show installment schedule
    -> issue eligible fee demands
    -> collect and allocate money
    -> verify instruments / record confirmed settlement when necessary
    -> issue or link receipt evidence
    -> match bank statement
    -> review balances and close the accounting period

Optional actions from the relevant student/source record:
    concession | eligible waiver | apply credit | refund | cancel | reverse

Other school activity:
    Accounts Income -> record non-fee income
    Accounts Expense -> record school cost and its payment

Every applied monetary action:
    source record -> financial event -> balanced journal -> ledger/report/audit
```

Staff do not construct journal lines or visit Accounts after each collection.
Pending requests and scheduled installments remain visible in the activity timeline without being mistaken for posted money.
Accounts shows routine summaries with an optional accountant-only journal drill-down.

### One-time settings

| Owner | Staff input | System behavior |
|---|---|---|
| Accountant: Account Heads | Name, type; parent and notes optional | Seed standard control roles; internal identifiers; protect used mappings and historical meaning |
| Accountant: Income/Expense Categories | Name and matching account head; description optional | Resolve normal manual vouchers to their account; tags remain optional |
| Accountant: Bank Accounts | Actual bank/account/IFSC details; linked bank head confirmed or safely created | Generate internal code; mask sensitive data; reuse stored bank IDs in collection, payout and statement matching |
| Fee administrator: Fee Heads | Name/category; concession eligibility where relevant; normal fee versus refundable deposit only when necessary | Inherit the category mapping when unambiguous; accountant handles exceptions once |
| Fee administrator: terms/structure | Amounts, service coverage, installments, demand/due dates and applicable students | Publish versions and preserve assignment snapshots |
| Finance administrator: existing Fees policies | Collection, concession, refund and proration rules in their existing owners | Resolve applicable versions on Fees commands; Accounts receives their calculated result |
| Accountant: ledger activation | Financial year, cutover date, verified opening balances, recognition basis | Create periods automatically; bind existing year-specific heads to durable ledger accounts |

There is no Accounts Payment Modes master, fee-policy selector, manual code field, or staff-authored posting-rule builder.
Use fixed tested accounting templates with administrator-confirmed account mappings.
Mappings may be inspected under existing Settings; versioning is internal and effective for future events only.
Monthly periods are generated inside the configured financial year; they are not manually recreated for each installment.
The financial year and academic year are different concepts and must not be silently equated.

### Integration contracts

The journal examples use an accrual design for an earned service period; recognition and historical cutover remain product decisions until confirmed.
Creating a schedule or collecting money early must not automatically recognise a full year's tuition as earned income.
For services earned over time, recognise revenue according to that service coverage; pre-billed unearned fees use deferred income where applicable.
This is a design principle informed by [IFRS 15's performance-obligation guidance](https://www.ifrs.org/issued-standards/list-of-standards/ifrs-15-revenue-from-contracts-with-customers/), not a claim about which reporting framework applies to every school.

| Fees area | Current flow | Required flow and Accounts effect |
|---|---|---|
| Fee Structure | Defines heads, amounts and term/version links | Preserve authoring; validate fee/deposit treatment and inherited mapping; a draft structure has no journal |
| Fee Structure Assignment | Creates student schedule/DueFee snapshots | Preserve preview/commit; distinguish scheduled from issued obligations; assignment alone must not post all annual revenue |
| Payment Terms / Installments | Define coverage, demand dates, due dates and percentages | Preserve version pins; schedule demand issuance and, when applicable, service-period revenue recognition |
| Fee Demand | Demand state is largely implicit in DueFee demand dates | Add explicit issued/cancelled evidence over existing dues; earned demand: Dr Student Receivable / Cr Fee Income; unearned billed portion: Cr Deferred Fee Income |
| Fee Collection | Reduces due via allocations and creates receipt/payment rows | Accepted payment: Dr Cash, Bank or Payment Clearing / Cr Receivable for issued dues; any genuine excess: Cr Student Advance |
| Future installment payment | Can allocate to a known future DueFee snapshot today | Keep staff ability; if no receivable has been issued, hold money as student advance earmarked to that schedule, then consume it once demand is issued |
| Concession before issue | Request calculates and applies fee reductions | Update unissued obligation snapshot and benefit evidence; issue only net amount; no duplicate reduction journal |
| Concession after issue | Reduces DueFee; excess paid can generate linked refund request | Dr Fee Reductions, or debit the corresponding deferred balance for unearned fees; Cr Receivable up to live debt; Cr Student Credit Liability for excess already paid |
| Waiver | Eligible late-fee/charge full remaining balance is waived | Preserve source eligibility/reason; reverse booked income/deferred balance against receivable; an unbooked calculated penalty changes preview only |
| Adjustment | Source balance is unsafe; target due is reduced | Apply reserved genuine credit: Dr Student Credit Liability / Cr Receivable; a receipt-allocation transfer preserves total collected money and does not create another concession |
| Refund | Approval and processing store return-money facts | Refund already recognised credit/advance/deposit: Dr relevant liability / Cr Cash or Bank on confirmed payout; no duplicate Expense voucher |
| Refund of previously paid fee | Eligible paid source may support refund | Establish the fee reduction/cancellation/refund entitlement and resulting payable once, then settle that payable; refund approval alone must not reverse revenue already reversed by concession |
| Student Financial Account | Collection derives student due/payment details | Add a read model over demands, allocations, credits, concessions and settlements; show debt, available credit, reserved credit and pending instruments separately |
| Ledger | No complete GL journal foundation | Immutable event-linked journals; one durable ledger across academic years; student, bank, fee-head and source dimensions |
| Other Income | Separate Accounts transaction CRUD | Record non-fee income once; confirmed receipt Dr Cash/Bank / Cr mapped Income; pending instruments follow clearing flow |
| Expense | Separate Accounts transaction CRUD | Paid cost Dr mapped Expense / Cr Cash/Bank; approved unpaid bill Dr Expense / Cr Vendor Payable, later payout Dr Payable / Cr Bank |
| Payment modes | Seven shared collection choices | Reuse choices; required account/reference details depend on the method; cards and UPI may settle net of charges |
| Reconciliation | Provisional receipt verification | Instrument verification retains compatibility route; actual bank matching uses separate statement records, settlement evidence and match allocations |
| Library / Transport / Hostel | Integration is not safely assumed from menu/module labels | Approved source fees/fines enter existing fee/charge contract with stable external identity; Finance posts demand/collection once |
| Payroll / Purchasing | Their operational records are owned by their modules | Add explicit approved payable/payment adapters later; another module must not write Accounts tables directly |

### Settlement and verification

Each payment instrument has its own amount, attributable allocations, reference, evidence and status.
Use business status, posting status and settlement status as separate facts rather than one overloaded status.
The officer sees short labels such as Pending verification, Accepted, Settled, Failed or Reversed.

1. Cash is accepted immediately and posts to Cash.
2. An unverified cheque/electronic claim reserves the intended allocations and appears as pending; it does not create confirmed payment or income.
3. When accepted evidence establishes the payment, post Dr the relevant Clearing account / Cr Receivable or Student Advance.
4. A confirmed direct bank receipt may post to Bank immediately using verified settlement evidence.
5. Settlement of a clearing item posts Dr Bank / Cr Clearing once.
6. A bank statement may supply settlement evidence, or match settlement already recorded; matching the same movement does not post it again.
7. If an instrument fails before acceptance, release its reservations; if it fails after acceptance, reverse only its journal/allocation and reopen the affected debt or credit.
8. Cash plus cheque preserves the cash portion if the cheque fails; never reverse the whole collection to reject one instrument.

For a card settlement of INR 1,000 with an evidenced INR 20 processing fee, the settlement entry is Dr Bank 980, Dr Processing Expense 20, Cr Card Clearing 1,000.
Do not silently write off an unexplained INR 20 discrepancy.
Bank charges entered from statement review use their own approved expense event and cannot duplicate an existing expense.

### Refund and expense connection

Refunds and Expenses share payout mechanics: source bank/cash account, method-specific evidence, external reference, settlement state and audit.
Their accounting purpose differs.
School electricity is an expense; returning an advance or refundable deposit reduces a liability.
For an earned-fee refund, recognise the approved fee reduction once and then pay its liability.
The refund screen derives treatment from the source; staff do not select an Expense Category or choose debit/credit treatment.
Do not reuse Expense CRUD to execute a refund.

### School example A: complete normal journey

Amounts are fictional INR; the two example demands represent earned service periods.
UPI acceptance and bank settlement are separate events.
Bank totals assume zero opening balance and no payment-provider charges in this example.

| Step | Staff action | Student debt | Available credit | Automatic journal |
|---|---|---:|---:|---|
| 1 | Issue Aarav's tuition 12,000 and transport 3,000 demand | 15,000 | 0 | Dr Receivable 15,000; Cr Tuition Income 12,000; Cr Transport Income 3,000 |
| 2 | Independently approve and apply a 1,000 tuition concession | 14,000 | 0 | Dr Tuition Reductions 1,000; Cr Receivable 1,000 |
| 3 | Accept 4,000 cash plus 6,000 UPI against the demand | 4,000 | 0 | Dr Cash 4,000; Dr UPI Clearing 6,000; Cr Receivable 10,000 |
| 4 | Confirm UPI settlement in the school's bank | 4,000 | 0 | Dr Bank 6,000; Cr UPI Clearing 6,000 |
| 5 | Accept a confirmed bank payment of 5,000; show 4,000 due + 1,000 advance before confirmation | 0 | 1,000 | Dr Bank 5,000; Cr Receivable 4,000; Cr Student Advance 1,000 |
| 6 | Approve and confirm a 600 advance refund | 0 | 400 | Dr Student Advance 600; Cr Bank 600; approval reserves credit before payout |
| 7 | Issue the next earned-period tuition demand of 2,000 | 2,000 | 400 | Dr Receivable 2,000; Cr Tuition Income 2,000 |
| 8 | Apply 200 available advance to the new demand | 1,800 | 200 | Dr Student Advance 200; Cr Receivable 200 |
| 9 | Assess a valid 300 late fee and then waive that unpaid balance with permission/reason | 1,800 | 200 | Assessment: Dr Receivable 300 / Cr Late Fee Income 300; waiver: Dr Late Fee Reductions 300 / Cr Receivable 300 |
| 10 | Record a confirmed unrestricted donation of 10,000 in Accounts Income | 1,800 | 200 | Dr Bank 10,000; Cr Donation Income 10,000 |
| 11 | Record a paid electricity bill of 2,000 in Accounts Expense | 1,800 | 200 | Dr Electricity Expense 2,000; Cr Bank 2,000 |
| 12 | Match statement entries and run close checks | 1,800 | 200 | Statement matching creates no second fee income or payment |

The example ends with Cash 4,000, Bank 18,400, Receivable 1,800, Clearing 0 and Student Advance liability 200.
Net fee/late-fee income is 16,000; donation income is 10,000; expense is 2,000.
Assets 24,200 equal liabilities 200 plus the example's net result 24,000.
Every journal in the sequence balances and the student balance agrees with the receivable control account.

### School example B: concession after full payment

Meera has an earned tuition demand of 5,000 and has paid 5,000, leaving no debt or credit.
An independently approved concession of 500 posts Dr Tuition Reductions 500 / Cr Student Credit Liability 500.
The existing concession-linked refund request should refer to this same credit, not create another 500 reduction.
If a 200 refund is approved, show 300 available and 200 reserved credit.
Confirmed payout posts Dr Student Credit Liability 200 / Cr Bank 200 and leaves credit 300.
An adjustment racing with that refund can consume only the unreserved 300.
The original receipt remains 5,000 with linked concession/refund evidence.

## 5. Missing business rules to define and enforce

| Rule | Required behavior and reason |
|---|---|
| Recognition | Confirm school accounting basis; separate schedule, demand, service earning, acceptance and settlement so annual future fees do not inflate current income |
| Event timing | Draft/submitted requests have audit/timeline evidence; applied benefit, issued obligation or accepted/settled money triggers its named posting |
| Amounts | Decimal with explicit currency and whole paise at boundaries; central rounding and deterministic final-line remainder; reject fractions of a paise in commands |
| Journal | At least two positive lines; each line is debit or credit; one currency; exact debit total equals credit total |
| Allocation | Instrument amounts equal collection total; due + charges + late fee + advance allocations equal accepted amount; reserve pending allocations separately |
| Debt equation | Issued obligations + assessed debits - applied reductions - accepted allocated payments - applied credit + linked reversals; do not subtract an advance refund from unrelated debt |
| Credit equation | Accepted unapplied funds + approved excess/reductions - completed applications - completed refunds; available = remaining credit - active reservations |
| Reservation | Refunds and adjustments use one shared reservation service; release on rejection/cancel/failure only when financial state permits; consume once on success |
| Ordinary paid fee | Fully paid tuition is not automatically available credit; freeing an allocation must explicitly reopen its source debt or create an authorised credit entitlement |
| Negative adjustment | Reject negative money inputs; use a named adjustment reversal or authorised debit correction; staff cannot edit a signed amount to increase debt |
| Refund cap | Settled refundable sources minus completed refunds and competing reservations; check current policy/version and source entitlement inside the transaction |
| Refund reversal | A real payout cannot be undone by a UI label; require failed/reversed settlement or evidenced recovery, with source, bank and credit corrections linked |
| Duplicate reference | Unique per tenant/provider/appropriate account scope where reliable; a generic narration is not always globally unique; keep command idempotency separately |
| Date | Actual occurrence, settlement date and posting date are separate; normal future actual-payment dates rejected; scheduled bills/payment instructions remain unpaid |
| Closed period | No posting to a closed period; dated correction in an open period references original; reopen needs dedicated authority and reason |
| Cancellation | Unissued schedule cancellation has no journal; issued cancellation creates the inverse obligation effect and converts eligible paid excess to credit, preserving receipt history |
| Backdating | Authorised backdating uses open period and source-effective mappings; close and command revalidate at commit to prevent races |
| Correction | Never edit/delete posted entry, receipt or approved allocation; source command creates linked correction and keeps applied journals immutable |
| Scope | Enforce tenant, academic context and student/account access; shared control accounts do not grant access to other students' journal lines |
| Cross-year | Keep academic dimensions on entries, use the configured financial period, and carry bank/credit balances continuously; cross-year credit transfer requires explicit permitted scope |

## 6. Edge cases and acceptance behavior

| Scenario | Current assessment | Expected behavior |
|---|---|---|
| Partial/full payment | Allocated collection exists | Show remaining debt; post accepted amount only |
| Multiple heads/installments | Supported allocations | Preserve row breakdown and per-instrument attribution; exact totals |
| Split accepted/pending payment | Receipt verification is ambiguous at collection level | Accepted portion settles its allocations; pending portion reserves; independent failure |
| Early known-installment payment | Existing future due allocation exists | Preserve early-payment option; advance/earmark until a receivable exists; no premature revenue |
| Overpayment | No complete independent advance ledger | Explicit preview of due and excess; create traceable Student Advance; never silently discard excess |
| Unallocated payment | No complete unapplied-credit flow | Separate intentional student advance action; known student/reference required; unknown bank receipt goes to controlled unidentified-receipts liability review |
| Failed payment | Provisional rejection exists | Release reservation or reverse that accepted instrument once; source receipt history preserved |
| Duplicate payment/API retry | Collection retry fingerprint exists | Extend command receipt and reliable external-ref duplicate controls to all money commands |
| Concession after demand/payment | Applied concession and linked refund workflow exist | Split receivable reduction versus student credit, post once and share refundable credit |
| Late-fee/charge waiver | Direct action exists | Preserve full remaining reason-only action; account for booked versus unbooked amount correctly |
| Adjustment / concurrent credit use | Unsafe source and incomplete consumption | Genuine available credit, shared reservation and atomic apply; exactly one winner on overlap |
| Partial refund | Refund workflow exists; end-to-end journal proof absent | Reserve and pay only approved amount; remaining entitlement survives; each settlement unique |
| Payout timeout | Recording settlement does not establish a gateway integration | Unknown external result stays pending investigation; retry checks provider/bank evidence before another payout |
| Reversal / cancellation | Source-dependent support exists | Named source correction; reverse monetary effect once; retain all source-to-journal links |
| Duplicate journal event | No complete journal store | Unique source transition/event identity and payload fingerprint; duplicate delivery returns original |
| Backdated/future transaction | No accounting-period guard | Explicit date rules, effective mapping and open-period validation |
| Reconciliation discrepancy | No real statement matching | Residual balance and evidenced fees/chargebacks; no forced match or silent write-off |
| Period close race | No period store | Posting and close lock/check the same period; cannot post after close passes its checks |

## 7. Frontend changes

Follow the [screen-by-screen frontend contract](../../../../../Shikshamitra-FE/docs/finance/references/accounts-integrated-screen-flow.md).
Retain current Fees/Accounts positions and routes where possible.
Add Student Account as a tab/detail on the collection/student view first, and Demand as a tab over assignment/dues, rather than requiring many new staff menu items.
Add Ledger and accountant-only close/settlement tooling only when its backend is available.
Keep the staff review compact: amount received, allocated to fees, advance created, remaining debt/credit, receipt and payment status.
Show a read-only Accounts entry link only to users who have its view permission.

## 8. Backend changes and transaction design

| Component | Responsibility | Why required |
|---|---|---|
| AccountingPostingManager | Resolve fixed template and snapshotted mappings; validate and persist balanced immutable journal | One accounting equation shared by all source modules |
| FinancialEvent | Source type/id, transition identity, occurrence/posting date, currency, safe source dimensions and fingerprint | Prevent repeated delivery from posting twice and link every journal to a real action |
| StudentCreditManager | Create, reserve, release, apply and refund traceable student credit | Prevent adjustment/refund double consumption and fake credit |
| Collection instrument manager | Per-instrument status, amount and allocation reservation/acceptance/settlement/reversal | Correct split-payment failure and mixed receipt behavior |
| Refund settlement service | Approved source reservation, stored bank, payment attempt and confirmed outcome | Preserve refund meaning and prevent a timeout retry from paying twice |
| Voucher manager | Income/Expense lifecycle with received/paid/payable meaning, evidence and named corrections | Prevent posted CRUD edits and duplicate school money movements |
| Bank reconciliation manager | Import statements, suggest matches, allocate amounts, record residuals and approved discrepancies | Distinguish matching from verification and recognise each bank movement once |
| Accounting period manager | Generate periods, close preview, close/reopen with checks, lock and reason | Protect finalised history and coordinate closing with ongoing commands |
| Ledger/report query services | Read immutable journals and canonical subledger facts with consistent scopes/as-of dates | Prove student, bank and GL totals rather than calculating independent UI balances |

### Local command boundary

```text
Check exact permission and tenant/user scope
    -> load command receipt or start unique command
    -> load source, current stamps, balances, effective mappings and period
    -> recompute preview and check source transition
    -> reserve/consume/release credit or instrument allocations
    -> update source and receipt intent
    -> persist FinancialEvent and balanced JournalEntry/Lines
    -> write audit and durable external-delivery intent
    -> commit once
    -> generate/deliver recoverable PDFs, notifications and integration messages
```

For records owned by the same Finance database, the source mutation and its journal share one unit of work.
Do not rely on the current logging handlers to create accounting later after a successful local fee mutation.
If required mapping or period validation fails, the enabled monetary command fails without changing balances.
External payout calls and PDF/blob delivery cannot be included in a database rollback; persist an attempt/job and recover its outcome safely.
Use the existing ABP event/outbox capabilities only after verifying actual configuration, with duplicate-delivery tests.
Unknown payout outcome is distinct from confirmed failure.
Posting retries never initiate the source payment again.

### Lifecycle and permissions

| Source | Staff-facing lifecycle | Monetary transition |
|---|---|---|
| Demand | Scheduled -> Issued -> Part-paid/Paid; Cancelled with correction | Issue/cancel; payment state derived from allocations |
| Instrument | Pending verification -> Accepted -> Settled; Failed/Reversed | Acceptance, settlement, linked failure/reversal; never repeated income |
| Concession | Keep existing draft/request/approval/reversal states | Applied approval and linked reversal |
| Waiver | Immediate Applied under existing dedicated action permission | Apply remaining eligible amount and audit reason |
| Adjustment | Draft/Submitted -> Approved -> Part-applied/Applied; Rejected/Cancelled/Reversed | Reserved genuine-credit application and linked reversal |
| Refund | Keep Draft/Submitted/UnderReview/Approved/Processing/Processed/Failed/Cancelled/Rejected/Reversed semantics | Entitlement if not already posted, confirmed payout and evidenced reversal |
| Income | Draft -> Submitted where required -> Recorded; Pending receipt/Received; Reversed | Recognise source and actual receipt separately when applicable |
| Expense | Draft -> Submitted -> Approved; Unpaid/Part-paid/Paid; Reversed | Approved payable or paid cost, then actual payout |
| Journal | Posted plus reversal link | Posted lines immutable; source command owns corrections |
| Period | Open -> Closed -> Reopened with reason | Close/reopen audit; postings must pass open-period check |

Existing permission identifiers remain compatibility contracts until deliberately migrated.
Add dedicated proposed Accounts capabilities for Ledger View/Export, Posting Exception View/Retry, Period View/Close/Reopen, Bank Reconciliation View/Import/Match/Approve and voucher lifecycle actions.
Use the exact backend constants and permission catalogue before wiring frontend buttons; this document does not create or grant those identifiers.
Apply/reject Adjustment currently reuse Add and include a privileged role-name maker-checker bypass; replace with dedicated action authority and enforce independent approval.
Collectors need their source-action authority, not raw-journal creation authority.
Bank summaries/full trial balance require school-wide finance scope; a restricted cashier can only see permitted sources.

## 9. Database and API changes

### Entities and constraints

| Entity/change | Essential fields/relationships | Constraints and migration |
|---|---|---|
| Durable LedgerAccount | Tenant, name/type, protected control role; links from year-scoped AccountHead | Stable identity across academic years; never merge old heads automatically by name |
| AccountingPeriod / ledger activation | Tenant, financial year, start/end, status, closed/reopened by/at/reason | Non-overlapping periods; posting/close concurrency boundary; explicit activation/cutover |
| FinancialEvent | Tenant, academic context, source type/id, source transition id/version, currency, dates, fingerprint | Unique tenant/source-transition identity including version; fingerprint conflict check |
| JournalEntry / JournalLine | Event, period, journal number; account, side, amount; source/student/fee-head/bank dimensions; reversal link | Decimal precision/scale, FK/type/scope checks; database-backed balance validation at post; unique event and journal numbering |
| Mapping snapshot | Event role/template version and resolved account IDs/treatment | Historical event retains original mapping; future change cannot rewrite old journals |
| StudentCredit / CreditMovement / CreditReservation | Student, origin, currency, available/reserved/consumed facts, request/settlement links | Unique origin/movement; no negative available; lock or concurrency guard shared by adjustments and refunds |
| PaymentMethodRecord extension and InstrumentAllocation | Existing instrument, status, confirmed/settled times, stored bank, reference; due/charge/advance allocations | Positive amounts, allocation sums and source ownership; no collection-wide rollback for one instrument |
| Demand header/line evidence | Existing assignment and DueFee IDs, issue/cancel events and snapshots | Unique eligible due/version issuance; no duplicate monetary due store |
| Voucher/source links | Existing Income/Expense transaction plus lifecycle, payout links and evidence; journal IDs via event relationship | Preserve legacy CRUD rows; new posted rows immutable; staged documents tenant/scoped |
| StatementImport / StatementLine / BankMatchAllocation | Stored bank, import hash, row identity, dates, debit/credit, source settlement and matched amount | Duplicate import/line controls; total match cannot exceed statement/internal residual; signed balance reconciliation |
| CommandReceipt / audit / recovery intent | Actor/resource/key, payload hash, result, correlation and pending external operation | Unique correctly scoped key; actor access rechecked on replay; one immutable result and recovery state |

Do not assume new journals can be safely backfilled from totals or logging messages alone.
At cutover, prove student receivable totals, credit liabilities, vendor payables, bank/cash balances and prior reductions/refunds.
Choose verified openings or traceable historical reconstruction; investigate old duplicate/invalid adjustment effects before establishing openings.
Carry old records as legacy history and link them where evidence is sufficient.
Migration must not fabricate receipt, settlement, creator or service-recognition dates.

### API contract: existing versus proposed

Every new endpoint below is a target, not a shipped endpoint.
Existing route families remain available through compatibility adapters until clients migrate.

| Current API family | Required compatible changes |
|---|---|
| `/api/account-heads`, `/api/income-categories`, `/api/expense-categories`, `/api/bank-accounts` | Keep short settings forms; add dependencies, durable account link, valid bank options and setup readiness; no payment-mode or fee-policy selector |
| `/api/fee-heads`, `/api/fee-categories`, `/api/fee-structures` | Add inherited treatment-aware mapping; apply same validation to standalone/embedded heads; preserve version pins |
| `/api/finance/payment-terms` | Preserve schedules; expose demand/recognition coverage and clarify schedule versus issued debt |
| `/api/student-fee-structure` | Preserve preview/assign; return schedule/issued totals and accounting readiness |
| `/api/fee-collections` | Add server preview, instrument-linked allocation/credit contracts, stored bank IDs and posting links; preserve existing retry response |
| `/api/fee-collections/waivers` | Preserve reason-only full live-balance waiver; return financial event/journal links where relevant |
| `/api/concession-request` and existing approval family | Preserve evidence and approval; show receivable versus credit impact; share created credit with linked refund |
| `/api/fee-adjustments` | Replace outstanding-as-source; add genuine-credit eligibility/reservation, exact permissions and source consumption; preserve corrections |
| `/api/fee-refunds` | Add `BankAccountId`, common credit source/reservation, immutable payout/event links; keep process/retry/reverse transitions |
| `/api/incomes`, `/api/expenses` | Add preview and lifecycle commands; limit update/delete to unposted drafts after client migration |
| `/api/v1/finance/reconciliations` | Keep receipt compatibility behavior; migrate safe cases to instrument-specific verification |
| `/api/app/finance-report` | Extend current options/preview/export pipeline with journal-based reports and as-of/scoped reconciliation |

The new Contracts/HttpApi layer defines these endpoints before frontend clients are added:

| Proposed endpoint | Purpose / minimum inputs |
|---|---|
| `GET /api/v1/finance/accounts/readiness` | Scope/cutover, missing mappings or periods, feature availability; academic context query |
| `GET /api/v1/finance/accounts/source-postings` | Permitted source type/id -> events, journal links and posting status |
| `GET /api/v1/finance/student-accounts/{studentId}` | Year/as-of filters; issued debt, scheduled fees, pending instruments, credit and timeline |
| `GET /api/v1/finance/student-accounts/{studentId}/credits` | Available/reserved credit sources, currency and permitted uses |
| `GET /api/v1/finance/fee-demands` | Scoped scheduled/issued demand list and server pagination |
| `POST /api/v1/finance/fee-demands/preview` | Eligible DueFee IDs/context; derived amounts and dates |
| `POST /api/v1/finance/fee-demands/issue` | Eligible source IDs, current preview/stamps and command key; amount calculated server-side |
| `POST /api/v1/finance/fee-demands/{id}/cancel` | Reason, preview/stamp and command key; resulting credit and correction links |
| `POST /api/fee-collections/preview` | Existing allocation/payment facts; due/advance/pending split and server journal impact |
| `POST /api/v1/finance/payment-instruments/{id}/verify` | Evidence, expected version and command key; accept one instrument |
| `POST /api/v1/finance/payment-instruments/{id}/fail` | Reason/evidence, expected version and command key; fail one instrument |
| `POST /api/v1/finance/payment-instruments/{id}/settle` | Stored bank, settled amount/date, external reference/fees evidence and command key |
| `POST /api/v1/finance/payment-instruments/{id}/reverse` | Reason/impact preview and stamps; source-owned correction |
| `POST /api/fee-adjustments/preview` | Available credit IDs and target due IDs; exact reservation/credit/debt impact |
| `POST /api/fee-refunds/{id}/processing-preview` | Approved reservation, payout method/bank and current stamps; cap and treatment |
| `POST /api/incomes/preview`, `POST /api/expenses/preview` | Business amount/category/date and actual received/paid/payable facts; no user-supplied journal accounts |
| `POST /api/incomes/{id}/submit`, `/approve`, `/record`, `/reverse` | Named allowed lifecycle action, reason/evidence/stamp/key as appropriate |
| `POST /api/expenses/{id}/submit`, `/approve`, `/pay`, `/reverse` | Named allowed action; payout amount/bank/reference for pay; prevent overpayment |
| `GET /api/v1/finance/accounts/journals`, `GET /api/v1/finance/accounts/journals/{id}` | Scoped, paged journal read/detail; correction navigates to original source action |
| `GET /api/v1/finance/accounts/ledger` | Account, dates/as-of and permitted dimensions; opening/debit/credit/closing |
| `GET /api/v1/finance/accounts/posting-exceptions`, `POST /api/v1/finance/accounts/posting-exceptions/{id}/retry` | Accountant recovery for imported/asynchronous work; never duplicate source payment |
| `GET /api/v1/finance/accounts/periods` | Configured financial periods and current status |
| `POST /api/v1/finance/accounts/periods/{id}/close-preview`, `/close`, `/reopen` | Reconciliation checks and stamps; explicit reason for reopen |
| `POST /api/v1/finance/accounts/bank-reconciliations/imports` | Stored bank, statement file, format/date context and command key |
| `GET /api/v1/finance/accounts/bank-reconciliations/{id}/candidates` | Scoped residual statement/source candidates |
| `POST /api/v1/finance/accounts/bank-reconciliations/{id}/matches`, `/close` | Explicit line/source amount allocations; manual-match reason and current versions |

Use a common command result containing source ID/reference/status, updated debt/credit/pending totals where applicable, receipt links, posting links, available actions and correlation ID.
Use a common preview with exact proposed impact, current stamps/fingerprint, warnings and readiness blockers.
Money is currency plus decimal amount; UI sends paise-safe values and does not recalculate authoritative journals.
Clients reuse existing body idempotency keys or the agreed header through an adapter; never silently change old request contracts.
Return stable business codes for missing mapping, invalid source transition, closed period, stale preview, insufficient credit, over-refund, unknown settlement, duplicate reference and scope denial.
A stale preview or changed reservation requires refresh and review before a new command; a network retry with the same unchanged command returns its recorded outcome.
There is no public routine endpoint that lets a cashier post arbitrary debit/credit lines or externally trigger accounting by sending an untrusted event.

## 10. Priority-wise implementation plan

| Priority / feature | Current status -> gap -> required change | Frontend | Backend | Database | API | QA |
|---|---|---|---|---|---|---|
| P0: adjustment integrity | Existing unsafe credit source -> debt treated as credit -> genuine credit plus shared consumption | Show available credit sources and blockers | Replace source equation; enforce independent approval and atomic reservation | Credit origins/movements/reservations; investigate old adjustment effects | Eligibility/preview/apply and exact grants | Unpaid/full-paid-without-credit cannot fund adjustment; adjustment/refund race has one winner |
| P0: monetary/source contract | Fees operational records exist -> no authoritative source/posting equation -> define decimal events and date/recognition/cutover contract | Server impact preview and clear state | Stable financial event identity, rounding and ownership | Event identity, money precision and command receipts | Compatible event/posting result extensions | Rounding, repeat commands, version conflicts, tenant scope |
| P0: journal foundation | No GL -> no balanced evidence -> immutable events/journals, protected mappings and periods | Read-only readiness and linked posting | Local source+journal UOW; reject missing mapping/closed period | Durable ledger, journal/line/period constraints | Readiness, source-postings, journal reads | Balance and unique-source constraints; rollback on posting failure; close/post race |
| P0: split instruments | Shared allocation/receipt -> unsafe per-instrument decisions -> instrument attribution | Separate accepted/pending status per method | Reserve/accept/fail each instrument; retain receipt links | Instrument allocation/status and settlement identity | Preview/verify/fail/settle/reverse | Cash survives cheque rejection; independent partial settlement |
| P1: demand and collection integration | Existing assignment/dues/collection -> missing issue and advance accounting -> post issue/payment/advance once | Student Account and Demand tabs; simple excess preview | Issue/cancel and future-payment earmark; due-before-advance allocation | Demand evidence over existing DueFee, student credit | Demand and collection extensions | Aarav flow; no annual income on assignment; future-payment consumption once |
| P1: benefit/refund integration | Existing approvals/waiver/payouts -> no credit/GL link -> one applied benefit and one payout | Before/after debt/credit; stored payout bank | Concession/waiver journals and common credit reservations; payout proof | Benefit/credit/refund/settlement links | Existing approve/process/waive responses plus previews | Meera flow, concurrent partial refund, no duplicated fee reduction or Expense |
| P1: income/expense | CRUD records -> mutable money and no paid/payable distinction -> lifecycle vouchers | Keep forms short; add paid/payable and conditional bank/evidence | Exact action authority, payable/payment/reversal transitions | Source voucher/evidence/payout links | Compatible lifecycle commands | Other income once; unpaid bill once; partial pay caps; posted-edit denial |
| P1: bank matching and reports | Receipt verification/report infrastructure -> no bank/GL control -> statement matching and accounting reports | Accountant settlement/match tabs; trial balance/statement drill-down | Residual matching, clearing reconciliation and common report queries | Statement/hash/match allocation/close controls | Bank-matching endpoints and existing report options | No duplicate bank posting; net card fee; totals and export parity |
| P1: migration/activation | Legacy history -> no verified GL openings -> controlled tenant cutover | Accountant migration/readiness checklist only | Traceable opening/reconstruction adapter and exception report | Preserve history; verified openings; currency/year continuity | Restricted activation and opening-preview commands defined with migration contract | Historical totals, legacy precision, no invented settlement, rehearsal/forward-fix |
| P2: operational edge cases | Partial support -> backdating/chargebacks/negative corrections incomplete -> explicit named corrections | Recovery messages and chronological corrections | Chargeback, credit transfer, refund recovery and eligible demand changes | Immutable reversal/linked correction records | Source-specific correction previews/actions | Linked refund/concession reversal; multi-year transfer scope; timeout recovery |
| P2: performance and accountant controls | Lists/read models exist -> growth and close require control -> paged projections and scheduled checks | Paginated detail; close blockers with source links | Incremental read models, reconciliation jobs and period reopen | Query indexes and recoverable projections | As-of reporting and close commands | Rebuild equals canonical journals; realistic volume; cross-tenant caches |
| P3: enterprise connections | Source modules separate -> no assumed automatic settlement -> explicit approved adapters | Only genuine service-specific links | Payroll, purchasing, Library/Hostel/Transport adapters, imports/exports | Versioned external source IDs and outbox/inbox | Authenticated integration contracts | Duplicate delivery, cancelled source, mapping version and payout provenance |

P0/P1 are delivery dependencies, not permission to release an incomplete accounting system.
Cutover only when demand, collection, benefits, credit, refund, other income/expense, settlement, reports and opening balances agree end to end.
Business users should not see half-working journal or mapping controls while those dependencies are being built.

## 11. QA and test requirements

| Gate | Required proof |
|---|---|
| Domain | Every journal balances; debt/credit equations; paise rounding; no credit from unpaid or ordinary fully paid fee |
| Application/API | Every allowed/rejected transition, exact action authority, independent checker, scope/tenant denial, stale preview and duplicate command fingerprint |
| Database | Event/reference uniqueness, credit race, refund/apply overlap, period close/post conflict and all-or-nothing source+journal rollback |
| Payments | Cash-only, each supported method, mixed pending/accepted instruments, accepted failure, duplicate external reference, refund timeout and evidenced recovery |
| Fees | Multiple installments/heads, future payment before issue, concession before/after payment, direct waiver, issue/cancel, credit application and partial refund |
| Reports | Student receivable sum equals GL receivable; credit sum equals liability; instrument clearing equals GL clearing; bank/cash book equals movements; preview/export parity |
| Example A | Reproduce all twelve steps; final debt 1,800, credit 200, Bank 18,400, Cash 4,000, Clearing 0 and balanced totals |
| Example B | Payment 5,000 -> concession credit 500 -> reserve/pay refund 200 -> available credit 300; benefit journal only once |
| Migration | Validate legacy double/precision and adjustment history; reconcile actual openings and carry forward without changing old receipts |
| Frontend | Authoritative previews, duplicate-click protection, retry preserving inputs/key, forbidden/loading/empty/error/conflict states, cache refresh and narrow-screen keyboard operation |
| Recovery | Posting/event retries and read-model rebuild never repost source money; PDF failure cannot produce another collection |

Run focused Finance suites from [backend testing](../testing.md) and [frontend testing](../../../../../Shikshamitra-FE/docs/finance/testing.md), permission regression when grants change, docs validators and diff hygiene.
No runtime/E2E success is implied by this design document.

## 12. Final recommended next steps and remaining decisions

Build against the current ABP layers, existing source commands, shared FE components and RTK Query clients.
Start with adjustment source integrity, instrument attribution, and the balanced journal/period foundation; connect the existing source workflows to that foundation before exposing accountant screens.
Then run the two complete school examples with reports and source-to-journal proof before tenant activation.

| Decision | Recommended design used here | Why activation depends on it |
|---|---|---|
| Accounting basis and service recognition | Accrual ledger; issued receivables; recognise earned service periods; defer unearned billed service as applicable | Determines when income/deferred balances post and how cancellation/concession is reversed |
| Historical cutover | Verified opening balances from an agreed start date; earlier receipts and source history retained | Determines which historical facts may enter the new ledger without invented evidence |
| Financial-year boundary | Reuse a real configured school financial year if available; otherwise confirm it during accountant activation | Academic-year account settings do not determine the financial-year reporting boundary |
| Advance destination | Explicit Student Advance liability and earmark for intended future installment where supplied | Enables excess/early payment without fake income or negative receivable |

The first two choices were requested from the user while preparing this flow.
Recommendations are design assumptions, not an unanswered approval or a claim of a completed runtime implementation.

### Inconsistencies

Confirmed source mismatch: `DueFee.AdjustableBalance` uses outstanding debt, while adjustment application creates a reduction in another due; it must be replaced by genuine reserved/consumed credit.
Confirmed delivery-doc drift: the backend account-type enum includes Liability and Equity, not only Asset/Income/Expense.
Confirmed flow distinction: existing reconciliation approves provisional receipts, and current event handlers only log; neither establishes a bank ledger or GL posting.
Frontend rendered behavior and new runtime APIs remain unverified because this turn establishes the design contract rather than changing production money handling.
