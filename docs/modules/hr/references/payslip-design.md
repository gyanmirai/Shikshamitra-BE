---
module: hr
domain: payslips
content_type: Reference
status: active
updated_on: 2026-10-07
owner: product-and-engineering
audience: humans-and-agents
---

# Payslip requirements and implementation

TL;DR: issue one accurate, immutable employee document after payroll finalization, distribute it through HR or configurable employee email, and correct mistakes without silently changing issued records.
This is the active acceptance specification for payslip completion.
Requirements below describe the target behavior; the implementation ledger distinguishes work still pending.
No legacy payslip migration or backward-compatible API is required because the feature is in development.
On 2026-10-07 the user deferred DOC-15 legal-format verification and asked engineering to proceed with the existing document.
DOC-15 remains recorded as follow-up work rather than an engineering-completion gate for this iteration.
This decision does not certify legal compliance or change statutory calculations, recovery constraints, permissions or immutable issued records.

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
| Legal-format follow-up | DOC-15 is deferred by the user for this iteration; retain configured document facts and the existing signature field while current Gujarat applicability is reviewed separately. |

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
| DOC-15 | Deferred by the user on 2026-10-07: verify applicable Gujarat wage-slip fields and authorization/signature wording before claiming legal-format acceptance. | Follow-up for both private unaided and government-aided schools; document current authoritative sources and field/signature decisions without changing existing issued artifacts. |

The document preserves the finalized monetary evidence to paise and does not round individual components or net pay up for presentation.
Amount-in-words is derived from that same recorded net amount when issuing a document or an explicit replacement.
Previously stored PDFs remain immutable, including after a renderer repair.

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
HR sees the intended recipient before sending and corrects a missing/wrong address through the controlled Identity-managed account email workflow.
School settings persist across academic years.
Changing automatic email applies to subsequent successful locking/issuance events, not a retroactive mass send.
Only employees successfully finalized in a partially successful batch are eligible for automatic delivery.

The user chose Identity-managed account email changes on 2026-10-06.
Keep generic personal-information updates protected by HR:Employee:010034.
The dedicated account email read/save requires both HR.Employee.EmployeeList.Edit and AbpIdentity.Users.Update, tenant membership and employee scope.
HR supplies the new address, a reason and explicit confirmation that the address was verified with the employee.
Save updates Employee.Email and the linked Identity email atomically, renames an email-based username, preserves a distinct username and records an HR audit event.
It rotates the security stamp and revokes persisted OAuth tokens and authorizations without changing passwords, activation, payroll figures or issued PDFs.
Existing self-contained access tokens remain subject to the unchanged host validation policy and expiry; do not describe persisted revocation as instant termination of every bearer session.
Concurrency covers both records, duplicate addresses are rejected, and request replay cannot undo a later account change.
No separate payslip address, arbitrary recipient override, new grant seed or real-email verification transport is introduced.
Queued delivery resolves the current account address when claimed.
Changing the address after claim but before transport fails that attempt with RECIPIENT_CHANGED; HR must request a new attempt explicitly.
An in-progress provider call and an account change serialize on the existing tenant fence, so the account change cannot commit between the final recipient check and that provider call.

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

## Pre-implementation source findings

These findings were inspected before implementation on 2026-10-02.
Retain them as problem provenance; the implementation sequence and owning testing guide record subsequent changes and executed evidence.

| Finding | Owning source |
| --- | --- |
| Employee and bank fields are read from live records; slip number depends on current employee code. | SalarySlipDtoMapper and SalarySlipProjection |
| School address is passed as null into the official PDF model. | SalarySlipProjection.ToPdfModel |
| Browser print and fallback preview generation are independent from the official server PDF. | payslip-print-view.tsx and use-payslip-actions.ts |
| Screen preview exposes protected compensation and government ESI information absent from the PDF. | salarySlipGenerate.tsx |
| PDF/print statutory details lack insurance number even though the source salary slip retains it. | PayslipPdfModels and PayslipPdfSections |
| Finalized/paid documents are immutable, but no complete correction/reissue or payslip delivery workflow exists. | SalarySlipWorkflow, SalarySlip aggregate and SalarySlipController |
| Existing HR report email sends authenticated links rather than employee PDF attachments. | HrReportScheduleDeliveryService |

## Implementation and executed verification

The engineering implementation and recorded acceptance journeys are complete for this iteration.
The user deferred DOC-15 on 2026-10-07; it remains explicitly unverified follow-up work.
Independent review and publication remain with the supervisor.

| Area | Implemented behavior and executed evidence |
| --- | --- |
| Official documents | Immutable configured facts, stable identity/revision/timestamps and stored PDF bytes are protected across profile edits, replacement and repeated downloads. |
| Calculations and ESIC | Recorded employee-only deductions, exact paise, attendance applied once, statutory continuity, exemptions and component constraints reconcile with authoritative payroll evidence. |
| Corrections | Reasoned unpaid drafts recalculate, review, cancel or reissue through payroll; paid differences apply once to a selected later unlocked month with source-period/revision evidence and protected originals. |
| Concurrency and security | Tenant-fenced correction/payment/lock/publication workflows enforce freshness, review, registration, permission, tenant and employee-scope guards, including direct-service and rollback regressions. |
| Distribution | Default-off school settings, after-commit intents, explicit Send/Resend and controlled Identity-managed recipient repair preserve audited retries and truthful provider/uncertain feedback. |
| Worker restart | Actual separate OS processes prove interrupted-send recovery; DI scopes or an API restart are not substituted for process evidence. |
| HR controls | Settings refresh, dirty drafts, failed writes, navigation/discard, repeated/interrupted selection, Retry and focus recovery pass focused frontend and authenticated browser checks. |
| Canonical preview and printing | Bundled PDF.js5.6.205 renders the stored artifact at390/768/1440 with keyboard zoom, readable Gujarati/paise text and no outer overflow; preview, download and email hashes agree, and actual print-frame text/dimensions/A4 preparation are verified. |
| Persistence and migrations | The two generated feature migrations remain retained; fresh-chain and fixture checks applied111 migrations with no pending or feature-specific model differences; the duplicate reconciliation scaffold stays unapplied/excluded. |

The final sealed-source 20-class PostgreSQL acceptance run on 2026-10-07 passed52 cases with zero failures or skips against26 fresh owned fixtures.
The post-paise application payroll selection passed324 cases, the renderer/shared-portability selection passed36 and the focused frontend selection passed145 across22 files.
These selections overlap other regression runs and must not be summed as distinct coverage.
Authenticated browser evidence includes unpaid cancellation/reissue, paid October-to-November settlement and selected-employee lock, synthetic Send/Resend, controlled Identity email, settings failure/refresh/discard, and actual API interruption during employee selection.
Final November revision3 reconciles net INR10,526.55 and matching paise words; stored, downloaded and capture-only MIME artifacts share the same checksum.
Native printer selection/physical output remain unrun, and provider acceptance is not a claim of inbox receipt.
The earlier two Admissions type errors, deployment-workflow assertion failure and1242 baseline runtime timestamp differences remain recorded failures within their original source scope.
Current-main integration checks must report their actual results separately rather than relabel those earlier failures as passes.
Detailed commands and meaningful regressions are in the owning testing guides; exact logs, TRXs, hashes and browser observations remain in the independent-review handoff.

Reuse the existing payroll calculators, transaction fence, snapshot workflow, ABP background jobs, email sender and protected blob infrastructure.
Keep domain transitions in the domain layer, orchestration in application services and UI contracts in Application.Contracts.
Do not write generated EF migration snapshots manually.
The original user/yvadher/payslip-completion worktrees are preserved; final integrated source is in user/yvadher/payslip-final-reconcile, based on main.
No feature commit, push, PR mutation, merge into main, deployment-file changes or deployment is included in this writer handoff.
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

## Recorded Unicode text

The actual employee personal-information API accepts Gujarati profile facts that the payroll response preserves correctly.
The original issued-document regression reproduced missing conjunct characters and reordered vowel marks despite correct visual rendering.
The approved shared QuestPDF 2026.9.1 upgrade embeds Noto Sans Gujarati alongside bundled Lato and retains strict font/glyph validation without system-font dependence.
Every newly generated payslip text span records its original value in a standard tagged span and stays within one page using `ShowEntire`.
This avoids repeating full replacements when PDF.js exposes a separate structure tree on each page.
The supported PDF.js 5.6.205 reader uses those recorded associations; it never infers payroll facts or deduplicates separate fields by equal text.
Backend tests validate exact logical values and page/MCID coverage for NUL-mapped shaping glyphs, including intentionally missing, incorrect and misassociated tags.
Frontend qualification must verify the exact issued PDF against its recorded response facts and reject uncovered NUL or replacement characters.
Existing immutable PDFs are never rewritten, and tagged generation alone does not establish PDF/UA or Gujarat legal certification.
The affected Finance report and receipt generators use the same portable font families without changing their financial facts or layouts.
Existing HR, Academic, Library, appraisal and generic document page styles explicitly select the same fallback families because registration alone does not supply a missing glyph fallback.
Custom font choices retain priority, and employee cards retain their existing SVG renderer.

## Official references

- [Social Security (Central) Rules, 2026](https://www.labour.gov.in/static/uploads/2026/05/49aa9b62c2125499c37399b90e969d67.pdf), including contribution rates and rounding.
- [Code on Wages (Central) Rules, 2026](https://www.labour.gov.in/static/uploads/2026/05/6eb0c35ba63b776487a025e5123b6b12.pdf), rule 52 and Form V as a reference for wage-slip fields.
- [Code on Social Security, 2020, updated to 21 November 2025](https://www.indiacode.nic.in/bitstream/123456789/16823/1/aA2020-36.pdf), section 31 for employee-contribution recovery and the prohibition on recovering employer contributions from employees.

Section 31 restricts employee contribution deductions to wages for the corresponding contribution period.
Paid settlement must retain source-period statutory evidence and must not silently deduct an old-period contribution from unrelated later-period wages.
Settlement enforces corresponding-source-earnings recovery and keeps employer shares separate; the broader statutory matrix remains acceptance work and no legal certification is claimed.

Central rules alone do not establish the applicable Gujarat school requirements.
The remaining jurisdiction and signature check is deferred acceptance work, not a claim that the current or proposed layout has legal certification.

The [official Gujarat Act and Rules catalogue](https://col.gujarat.gov.in/e-citizen-act-and-rule.htm) and [Minimum Wages rules summary](https://col.gujarat.gov.in/e-citizen-miniwagesact.htm) were reachable on 2026-10-03.
The linked Labour Codes and detailed minimum-wages citizen pages timed out during verification, and an applicable current Gujarat wage-slip form was not recovered from the official catalogue.
The Gujarat-hosted [Code on Wages notification](https://ifp.gujarat.gov.in/IC/StaticAttachment?AttachmentFileName=%2Fpdf%2FIMP_INFO%2FCode_Wages_1.pdf) identifies itself as a draft dated 31 March 2021.
The supplied reference file was independently hash-checked as SHA256 `C340B98F43BAAD8F0B6A7C372FA2792180770A0A20FE15E784E0907624E60455`.
That draft cannot establish currently applicable wage-slip fields or signature requirements and does not resolve DOC-15.
The user confirmed that both private unaided and government-aided schools are in scope.
The school profile currently records IN-GJ, but does not establish the applicable rule/form or appropriate-government jurisdiction for either category.
Retain the existing central-reference fields without claiming they certify a Gujarat school wage slip; current Gujarat form and signature applicability remain deferred DOC-15 work.
The rejected government download must not be retried through another interface or bypassed.
An authorized current authoritative reference remains required to resolve legal-format acceptance for both categories when this deferred work resumes.
The [Code on Wages, 2019](https://www.labour.gov.in/static/uploads/2025/06/c328da14bbb15fc4ad571dc33e7a4ab3.pdf), sections 2(y) and 18, supplies the statutory wage definition and aggregate deduction constraint used for paid overpayment recovery.
