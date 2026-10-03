---
module: hr
domain: payslips
content_type: Reference
status: active
updated_on: 2026-10-03
owner: product-and-engineering
audience: humans-and-agents
---

# Payslip requirements and implementation

TL;DR: issue one accurate, immutable employee document after payroll finalization, distribute it through HR or configurable employee email, and correct mistakes without silently changing issued records.
This is the active acceptance specification for payslip completion.
Requirements below describe the target behavior; the implementation ledger distinguishes work still pending.
No legacy payslip migration or backward-compatible API is required because the feature is in development.

## Confirmed product decisions

| Decision | Requirement |
| --- | --- |
| Issuance | Official payslips are available after payroll finalization, before or after actual payment. |
| Audience | HR manages access, downloads, printing and distribution; no employee portal is included. |
| Employee deductions | Display the employee ESIC share only, using the applicable calculated evidence and statutory rounding/exemptions. |
| Employer amounts | Do not show employer ESIC, government-funded employer relief, employer PF or school CTC on the employee document. |
| Automatic email | A school-level option sends each employee their own finalized payslip after payroll locking; default off. |
| Manual email | Authorized HR can Send and Resend regardless of the automatic-email setting. |
| Unpaid corrections | Preserve the original, require a reason, correct the underlying payroll and issue a replacement. |
| Paid corrections | Retain the paid payroll and apply monetary differences through a later payroll. |
| History | Preserve every issued document version and its audit/delivery evidence. |
| Scope | Retain the existing Gujarat statutory profile; PF configuration and finance ledger posting are excluded. |

## Document requirements

| ID | Requirement | Acceptance evidence |
| --- | --- | --- |
| DOC-01 | School legal/display identity and address come from configured school facts, never a fabricated fallback institution. | Configured school details appear in a generated PDF; missing required identity yields an actionable validation error. |
| DOC-02 | Include employee name, stable employee code, designation, relevant statutory identity and pay period. | PDF text and structured snapshot assertions. |
| DOC-03 | Assign a stable slip number, issue timestamp and revision at issuance. | Changing employee code later cannot change the issued number. |
| DOC-04 | Snapshot school, employee, bank, statutory, attendance and monetary facts used by the issued document. | Later profile, bank, school and rule changes leave the prior document unchanged. |
| DOC-05 | Show regular earnings, additional earnings including overtime, employee deductions and net pay with reconciled totals. | Total earnings minus employee deductions equals net pay across preview, payroll and PDF. |
| DOC-06 | Distinguish full-month agreed earnings from earned amounts when attendance reduces pay. | Partial attendance is applied once, and unpaid days do not become a second monetary deduction. |
| DOC-07 | Show concise payroll-basis/payable-day information and relevant unpaid days without internal formula jargon. | Full, partial, joining, leaving and zero-pay months remain understandable. |
| DOC-08 | Employee ESI uses the finalized statutory wages and effective rate; never recompute it in a renderer. | Standard, exempt, rounded, continuity and zero-wage cases match payroll. |
| DOC-09 | Employer contributions and CTC/protection/debug fields are absent from the employee document and delivery attachment. | PDF text, employee-document contract and browser assertions. |
| DOC-10 | ESIC-off documents omit ESI fields and lines; covered employee-share exemptions retain accurate coverage with a zero deduction where needed. | Off, non-covered and exempt coverage cases are separate tests. |
| DOC-11 | Drafts are clearly marked as previews and cannot be emailed as official documents. | API denies draft delivery; draft PDF has a visible preview label. |
| DOC-12 | Screen document preview, Print, Download and email use the same server-generated PDF for a given issued revision. | Attachment hash equals download hash; print opens that document. |
| DOC-13 | Persist the issued PDF or an equivalent immutable artifact and checksum; downloading an issued version must not regenerate it from current profiles or templates. | Repeat downloads return identical bytes across profile and renderer changes. |
| DOC-14 | Use a neutral, readable A4 layout with persistent labels, aligned amounts, clear net pay and predictable pagination. | Render and inspect normal, long-name and multi-page cases. |
| DOC-15 | Verify applicable Gujarat wage-slip fields and authorization/signature wording against official sources before claiming legal-format acceptance. | Document the applicable source and resulting field decisions; do not rely on a generic no-signature footer. |

## Finalization and corrections

Finalization freezes payroll money and creates the first issued document version atomically.
An email failure cannot undo finalized payroll.
A PDF creation/storage failure must leave a recoverable, accurately labelled state and must not expose a missing or partially issued official artifact.
No independent payslip-only salary adjustment is allowed.

| ID | Requirement | Acceptance evidence |
| --- | --- | --- |
| LIFE-01 | Payroll lock, direct finalization and correction issuance enforce the same freshness, review, insurance, permission and tenant requirements. | Authenticated tests for each entry point, including stale and missing-number rejection. |
| LIFE-02 | Record payment status independently from document issuance, with payment date/reference and actor. | Marking paid does not change the issued PDF; duplicated requests are idempotent. |
| LIFE-03 | Unpaid correction starts an explicit reasoned workflow, preserves the original and prepares a recalculation through the existing payroll pipeline. | Original remains readable while the correction is a draft. |
| LIFE-04 | Correction review and finalization atomically replace the current payroll evidence and issue the next document revision. | Concurrent correction, payment and locking cannot produce two current revisions or mixed amounts. |
| LIFE-05 | A pending correction cannot silently become paid; payment and correction state transitions serialize. | Competing requests have one valid winner and an actionable response for the other. |
| LIFE-06 | Paid monetary corrections create traceable adjustments in a selected unlocked later payroll, with a reference to the original period and no duplicate application. | Paid original remains unchanged; later payroll reconciles the adjustment exactly once. |
| LIFE-07 | Non-monetary document corrections retain the source payroll totals and record a reason and replacement relation. | Bank/name corrections cannot edit salary amounts. |
| LIFE-08 | Every replacement retains the original version and its delivery history; the UI identifies which version is current. | HR can inspect prior revisions without mistaking them for the current slip. |
| LIFE-09 | Corrections that alter statutory continuity invalidate affected later unlocked calculations using the existing contribution-period rules. | Earlier-month correction followed by later-month recalculation and locking. |
| LIFE-10 | Missing identifiers, invalid budgets and statutory constraints remain authoritative during correction. | Correction workflow cannot bypass the existing payroll guards. |

## Distribution and email

Use the employee's recorded email address, with no arbitrary recipient override on the payslip screen.
HR sees the intended recipient before sending and corrects a missing/wrong address through the existing employee workflow.
School settings persist across academic years.
Changing automatic email applies to subsequent successful locking/issuance events, not a retroactive mass send.
Only employees successfully finalized in a partially successful batch are eligible for automatic delivery.

| ID | Requirement | Acceptance evidence |
| --- | --- | --- |
| MAIL-01 | Automatic delivery is off by default and is a server-enforced school setting. | New school, repeat initialization and changed academic year tests. |
| MAIL-02 | Each message has one employee recipient and only that employee's official PDF. | Captured messages verify recipient, attachment and tenant correspondence. |
| MAIL-03 | Persist delivery intent in the payroll transaction and send through a background worker after commit. | Rolled-back locks send nothing; worker/service restarts do not lose committed intent. |
| MAIL-04 | Repeated lock events, duplicate requests and concurrent workers cannot create duplicate automatic delivery attempts for a document version. | Database uniqueness and concurrent PostgreSQL tests. |
| MAIL-05 | Manual Send, Resend and Retry enforce permissions and scope independently of automatic delivery. | Allow/deny, cross-tenant and out-of-scope API tests. |
| MAIL-06 | Explicit Resend creates an audited new attempt for the selected current document; a retried HTTP request reuses its idempotency key. | Same key returns the same attempt, a new explicit resend creates one new attempt. |
| MAIL-07 | Missing or invalid email blocks delivery only, with an actionable state and no fabricated address. | Payroll locks successfully and HR can correct the address and retry. |
| MAIL-08 | Track queued, processing, provider-accepted, failed and uncertain outcomes accurately; show recipient, actor, document revision, time and error category. | Failure/recovery tests and delivery-history UI. |
| MAIL-09 | Do not label provider acceptance as confirmed inbox delivery unless the configured provider supplies delivery evidence. | UI and API status semantics agree. |
| MAIL-10 | Reuse supported email infrastructure and attach the exact official PDF with a readable filename. | No employee login is needed to read the attachment. |
| MAIL-11 | Handle the crash-after-send ambiguity explicitly; SMTP cannot guarantee exactly-once delivery by database state alone. | Ambiguous attempts are not blindly resent; provider idempotency is used where supported. |
| MAIL-12 | Delivery errors and logs contain no credentials, PDF bytes or unnecessary salary details. | Log and response inspection. |
| MAIL-13 | Pending deliveries revalidate document currency; superseded versions are not automatically sent after correction. | Correction racing with queued delivery is covered. |

## HR experience

Retain the existing Payslips route and standard HR breadcrumbs.
Use the existing employee and payroll-month controls, shared form/table/dialog components and compact labelled actions.
Show one document preview rather than independent dashboard, browser-print and fallback-PDF layouts.
Offer Download, Print, Send or Resend according to the selected document's state and the caller's permissions.
Expose delivery history and document revisions as secondary disclosures.
Show email configuration in a compact settings surface with normal Save/Cancel actions.
Avoid decorative metric cards, status duplication, permanent instructional banners and routine confirmation dialogs.
Use confirmation for consequential corrections and intentional resend when needed to prevent an accidental duplicate, not ordinary viewing or downloading.
Keep header/filter/action positions stable during loading; preserve edits after failures and background refreshes.
Keyboard focus, dialog dismissal, responsive layout and errors must follow the shared component patterns.

## Security and data integrity

Server permissions, tenant isolation and employee scope apply to reads, PDFs, history, correction, payment recording and every send action.
Background work carries explicit tenant and document identifiers and revalidates persisted state before processing.
Never reuse a generic public blob URL for a payslip or expose documents through unauthenticated routes.
Use decimal server calculations, the existing payroll transaction fence and optimistic concurrency/idempotency for mutations.
No production email is sent during development verification; use a capture transport and synthetic employees.
Do not expand role grants or reseed revoked permissions to make the feature accessible.

## Current source findings

These findings were inspected on 2026-10-02 and require executable reproduction before their implementation fixes.

| Finding | Owning source |
| --- | --- |
| Employee and bank fields are read from live records; slip number depends on current employee code. | SalarySlipDtoMapper and SalarySlipProjection |
| School address is passed as null into the official PDF model. | SalarySlipProjection.ToPdfModel |
| Browser print and fallback preview generation are independent from the official server PDF. | payslip-print-view.tsx and use-payslip-actions.ts |
| Screen preview exposes protected compensation and government ESI information absent from the PDF. | salarySlipGenerate.tsx |
| PDF/print statutory details lack insurance number even though the source salary slip retains it. | PayslipPdfModels and PayslipPdfSections |
| Finalized/paid documents are immutable, but no complete correction/reissue or payslip delivery workflow exists. | SalarySlipWorkflow, SalarySlip aggregate and SalarySlipController |
| Existing HR report email sends authenticated links rather than employee PDF attachments. | HrReportScheduleDeliveryService |

## Implementation sequence

The entries below are implementation work, not claims of existing endpoints or completed behavior.

| Step | Work | Completion gate | Status |
| --- | --- | --- | --- |
| 1 | Save this functional specification and owning frontend workflow notes. | HR documentation validators and user decisions recorded. | Complete: both HR validators passed on 2026-10-02. |
| 2 | Reproduce document drift and renderer differences through authenticated local API/UI. | Saved synthetic PDFs, extracted text and permanent failing regressions. | Complete: real PostgreSQL bank-change regression failed before the fix and passed after it. |
| 3 | Add immutable document identity/facts, issued artifact storage and a shared server document contract. | Historical facts and byte-stable download regressions pass. | Implemented: profile/school drift, artifact hash, defensive byte copies and prior-revision download checks pass. |
| 4 | Consolidate employee PDF layout, ESIC presentation and screen/print/download behavior. | Monetary reconciliation and rendered PDF/browser checks pass. | In progress: one server PDF, monthly rates and employee-only ESIC implemented; full calculation matrix and final browser review remain open. |
| 5 | Implement payment evidence, unpaid correction/reissue and paid future-period adjustments. | State-machine, idempotency, continuity and PostgreSQL race tests pass. | In progress: reasoned unpaid drafts, recalculation, review, reissue, cancellation, original artifact retention and competing reissue/payment checks pass on PostgreSQL; paid settlement covers signed later-payroll differences and idempotent retries; correction PDF persistence rollback passes; the full continuity/security/fault matrix remains outstanding. |
| 6 | Implement school email settings, durable delivery attempts, worker and manual Send/Resend/Retry. | Capture-transport, crash/retry, isolation and concurrency tests pass. | In progress: automatic delivery, rollback, duplicate requests, competing workers and uncertain-send recovery pass; termination and restart of the actual delivery-worker test process now pass with preserved queue state and artifact hashes; full distribution browser acceptance remains open. |
| 7 | Complete compact HR controls, history and recovery states through typed RTK Query contracts. | Frontend tests, scoped lint and authenticated browser journeys pass. | In progress: unpaid correction controls and typed contracts added; refreshed settings and employee-history isolation regressions pass; paid settlement controls and settings link/Back guards are implemented; full browser acceptance remains open. |
| 8 | Generate migrations with EF tooling, refresh scoped development fixtures and update owning docs. | Fresh PostgreSQL setup, docs validation and no unrelated/deployment diff. | In progress: the complete migration chain applies to fresh PostgreSQL and EF reports no pending model changes; synthetic school facts and finalization fixtures are updated; final documentation validation remains required. |
| 9 | Run the full acceptance matrix, investigate failures and fix bugs before completion. | Every requirement has direct evidence or is explicitly still outstanding. | Pending |

Reuse the existing payroll calculators, transaction fence, snapshot workflow, ABP background jobs, email sender and protected blob infrastructure where appropriate.
Keep domain transitions in the domain layer, orchestration in application services and UI contracts in Application.Contracts.
Do not write generated EF migration snapshots manually.
Keep frontend and backend changes on user/yvadher/payslip-completion, based on main containing the merged ESIC implementation.
No merge, deployment-file changes or deployment is included.

## Verification matrix

| Area | Required scenarios |
| --- | --- |
| Calculation | GROSS and EMPLOYER_COST; INR 10,000 examples; full/partial/zero attendance; joining/leaving; overtime; additional earnings; deductions; rounding; invalid budgets. |
| ESIC | On/off; eligible/non-covered; contribution continuity; zero employee-share exemption; missing number; employer relief; unclassified earnings; employer amounts excluded from document. |
| Historical accuracy | Employee code/name/designation/bank changes, school changes, statutory/configuration changes, renderer update, repeat download and explicit replacement. |
| Lifecycle | Draft preview, successful lock, blocked lock, direct finalization, payment record, unpaid correction, paid adjustment, cancelled correction, old-version visibility. |
| Concurrency | Lock/finalize/correct/pay overlap; earlier evidence changes versus later calculation; duplicate send requests and competing delivery workers. |
| Delivery | Auto off/on, manual send with auto off, deliberate resend, invalid/missing address, provider failure, ambiguous send outcome, restart, rollback, corrected document queued for sending. |
| Security | Exact action allow/deny, tenant isolation, employee scope, protected artifact access, unproxied service enforcement, no automatic grant restoration. |
| Documents | Short and long names, many earnings/deductions, multi-page output, Indian currency/words, correct issue date/timezone, no clipping or blank section clutter. |
| UI | 390/768/1440 px, keyboard navigation, screen-reader labels, loading/error/retry, draft preservation, disabled actions, correct recipient and revision visibility. |

Run focused backend domain/application/API tests and PostgreSQL transaction tests, frontend unit/contract tests, typecheck, scoped lint, HR documentation validators, relevant security regressions and git diff --check.
Finish with authenticated Codex in-app browser verification and rendered PDF inspection.
Report executed results separately from blocked or unrun acceptance checks.

## Official references

- [Social Security (Central) Rules, 2026](https://www.labour.gov.in/static/uploads/2026/05/49aa9b62c2125499c37399b90e969d67.pdf), including contribution rates and rounding.
- [Code on Wages (Central) Rules, 2026](https://www.labour.gov.in/static/uploads/2026/05/6eb0c35ba63b776487a025e5123b6b12.pdf), rule 52 and Form V as a reference for wage-slip fields.
- [Code on Social Security, 2020, updated to 21 November 2025](https://www.indiacode.nic.in/bitstream/123456789/16823/1/aA2020-36.pdf), section 31 for employee-contribution recovery and the prohibition on recovering employer contributions from employees.

Section 31 restricts employee contribution deductions to wages for the corresponding contribution period.
Paid settlement must retain source-period statutory evidence and must not silently deduct an old-period contribution from unrelated later-period wages.
Settlement enforces corresponding-source-earnings recovery and keeps employer shares separate; the broader statutory matrix remains acceptance work and no legal certification is claimed.

Central rules alone do not establish the applicable Gujarat school requirements.
The remaining jurisdiction and signature check is an acceptance item, not a claim that the current or proposed layout has legal certification.

The [official Gujarat Act and Rules catalogue](https://col.gujarat.gov.in/e-citizen-act-and-rule.htm) and [Minimum Wages rules summary](https://col.gujarat.gov.in/e-citizen-miniwagesact.htm) were reachable on 2026-10-03.
The linked Labour Codes and detailed minimum-wages citizen pages timed out during verification, and an applicable current Gujarat wage-slip form was not recovered from the official catalogue.
The Gujarat-hosted [Code on Wages notification](https://ifp.gujarat.gov.in/IC/StaticAttachment?AttachmentFileName=%2Fpdf%2FIMP_INFO%2FCode_Wages_1.pdf) identifies itself as a draft dated 31 March 2021.
The supplied reference file was independently hash-checked as SHA256 `C340B98F43BAAD8F0B6A7C372FA2792180770A0A20FE15E784E0907624E60455`.
That draft cannot establish currently applicable wage-slip fields or signature requirements and does not resolve DOC-15.
The school profile currently records IN-GJ, but does not establish private/government/aided status or the appropriate-government applicability needed to select jurisdiction-specific wording.
Retain the verified central reference fields without claiming they certify a Gujarat school wage slip; the current Gujarat form and signature applicability remain DOC-15 acceptance work.
The [Code on Wages, 2019](https://www.labour.gov.in/static/uploads/2025/06/c328da14bbb15fc4ad571dc33e7a4ab3.pdf), sections 2(y) and 18, supplies the statutory wage definition and aggregate deduction constraint used for paid overpayment recovery.
