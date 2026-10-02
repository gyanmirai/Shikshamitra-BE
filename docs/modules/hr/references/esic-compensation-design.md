---
module: hr
domain: payroll-esic-compensation
content_type: Conceptual
status: active
updated_on: 2026-09-30
owner: product-and-engineering
audience: humans-and-agents
---

# ESIC configuration and compensation design

TL;DR: configure ESIC once and calculate payroll automatically using an explicit employee salary basis: Monthly salary or Monthly cost to school (CTC).
This active design reference records the agreed feature and the plan for integrating it into the canonical HR documentation.
It describes intended behaviour and acceptance criteria, not a claim that every change is implemented, tested, merged, or deployed.

## Scope and documentation ownership

Fresh employee compensation setup is the primary scenario.
Legacy salary migration and backward compatibility are not required for this development iteration.
Retain the Gujarat statutory profile.
PF configuration, jurisdiction expansion, finance ledger posting, and unrelated payroll redesign remain outside scope.
Existing applicable employer contributions still count toward CTC; deferring PF configuration does not mean excluding existing PF amounts from a calculation.

Backend HR owns calculations, eligibility, contracts, permissions, effective dates, audit, and snapshot integrity.
Frontend HR owns routes, shared-component usage, client wiring, screen workflows, loading/error states, and browser validation.
Link across repositories rather than duplicating the detailed rules.
Follow the [Finance Engineering Constitution](../../finance/references/constitution.md) for monetary ownership and reconciliation.

## Salary terminology and setup

| Term | Meaning |
|---|---|
| Monthly salary | Full-month earnings before employee deductions; employer contributions are additional school costs |
| Monthly cost to school (CTC) | Full-month budget including earnings and applicable employer contributions |
| Gross salary | Actual earnings for the payroll month after attendance adjustments and applicable earning components |
| Net pay | Actual gross less employee deductions |
| Employer contribution | School-funded statutory contribution, recorded separately from employee deductions |

Provide one salary-basis choice in the proposed setup flow.
Remove the separate protection-policy selector and take-home-based setup from that flow.
The chosen basis determines which amount the initial compensation calculation must satisfy.
Default new templates to Monthly cost to school (CTC).
The school-default template supplies the initial selection and its salary basis; allow authorized employee-level overrides.
Show the selected basis explicitly and label the amount field accordingly; do not use an ambiguous standalone Salary label.
Use persistent labels and only add helper text when a label cannot prevent a likely mistake.

Persist the employee's selected basis, entered amount, effective month, and approved compensation breakdown.
Show earnings, employee deductions, net pay, employer contributions, and total school cost before saving.
The employee's agreed breakdown and the payroll/payslip calculation must agree.
Use the existing payable-day rules, including applicable paid leave, rather than interpreting attendance as physical presence alone.

## Calculation and presentation

Reuse the existing compensation and statutory calculation services.
Do not calculate an independent set of monetary totals in the frontend or payslip renderer.

| Basis | Initial full-month calculation |
|---|---|
| Monthly salary | Preserve the entered earnings amount and add employer contributions to school cost |
| Monthly cost to school (CTC) | Solve for earnings such that earnings plus applicable employer contributions fit the entered budget |

Employee ESIC reduces net pay.
Employer ESIC remains an employer contribution in both modes.
Calculate contributions on the statutory wage base, which must not be assumed to equal CTC or every earning component.
Apply statutory rounding and component constraints within the solver, rather than subtracting 3.25 percent of CTC.
Return an actionable error if the budget cannot produce a valid compensation structure.
Any unavoidable rounding remainder must be visible and must not silently exceed the budget.

### Worked example

Assumptions: full attendance, all gross earnings are ESIC wages, standard contribution rates, no exemptions, and no PF or other contributions/deductions.

| Amount | Monthly salary of INR 10,000 | CTC of INR 10,000 |
|---|---:|---:|
| Gross earnings | 10,000 | 9,685 |
| Employee ESIC | 75 | 73 |
| Employer ESIC | 325 | 315 |
| Net pay | 9,925 | 9,612 |
| Total school cost | 10,325 | 10,000 |

The CTC example satisfies `9,685 + 315 = 10,000` and `9,685 - 73 = 9,612`.
The employee share is rounded upward from `9,685 × 0.0075`, and the employer share is rounded upward from `9,685 × 0.0325`.
The resulting gross is not INR 9,775, and the employer contribution is not calculated on the INR 10,000 package value.

In a fresh CTC setup with ESIC off and no other employer contributions, the full budget becomes monthly earnings.
If another employer contribution remains applicable, it remains within the budget.
Attendance then determines earned payroll amounts; do not spend the full monthly budget on a partial payable month.

System-generated overtime carries an explicit calculation identity rather than relying on its display name.
Under the retained statutory wage profile, overtime is a specified exclusion subject to the existing exclusion add-back calculation.
It does not enter agreed regular monthly wages when determining coverage.
Custom earnings with the same name remain unclassified until reviewed.
See the Ministry's [additional labour-code FAQs dated 16 March 2026](https://www.labour.gov.in/static/uploads/2026/03/a4ccf4c6d97c4f1f36a6d83f8c64213d.pdf) for overtime in the 50 percent calculation and the [ESIC Revenue Manual, L.6.11](https://www.esic.gov.in/Publications/RevenueManual.pdf) for the distinction between overtime and coverage wages.

Payroll readiness and locking use the same blocking-issue predicate.
A missing insurance number, unclassified earning or unresolved compensation variance must not be presented as ready to lock.
Resolved coverage facts also block missing-number payroll even when its contribution amounts and recorded issue list are empty.

Payslips show actual earnings, employee ESIC deductions, and net pay.
Keep employer ESIC outside the employee-facing payslip while retaining it in school payroll cost reporting and calculation/audit evidence.
Finance expense/payable posting is not part of this iteration.

### Statutory boundaries

Determine eligibility from statutory wages, employee facts, applicable exceptions, and contribution history, not a permanent manual ESIC/non-ESIC assignment at onboarding.
An entry ceiling is not a lifetime exclusion, and CTC is not the eligibility wage base.
Preserve contribution-period continuity when a covered employee crosses the applicable threshold.
Prior finalized or paid payslips establish continuity even when their payroll snapshots remain unlocked or are absent.
Their immutable ESIC evidence takes precedence over a conflicting snapshot for the same employee-month; otherwise, locked snapshots remain valid evidence.
Exclude draft payslips, unlocked snapshots alone, and evidence outside the prior months of the applicable contribution period.
Document the standard ceiling, supported disability ceiling, employee-share exemption, and classifications using the effective statutory profile.

The example explains budget allocation and does not certify that the resulting earnings meet every applicable wage requirement.
Validate applicable wage and component constraints before approving compensation.
Initial agreement of a CTC breakdown is distinct from later reducing established wages solely to fund employer contributions.
Do not treat payslip formatting as legal authorization for that later reduction.
Future changes to established compensation remain subject to statutory wage protection and an explicit compensation workflow.

## Compact ESIC settings experience

Retain the Statutory Schemes route and standard HR breadcrumbs.
Use one compact Formik form with shared sections, small MUI controls, theme spacing, neutral surfaces, and 30 px labelled actions.

| Section | Content |
|---|---|
| ESIC | Calculate ESIC switch, on by default for new schools |
| Registration | Optional registration number and legal coverage start date, revealed when enabled |
| Effective scope | Applies to current and future unlocked payroll. |
| Save and Cancel | Direct save; confirmation only to discard unsaved changes |
| Secondary disclosures | Calculation rules, earning classifications, and change history |
| Employees needing attention | Visible only when issues exist, opening the existing employee drawer through the shared table |
| View employees | Optional shared employee table with server paging, search, and sorting |

No month picker, scheduling controls, draft impact section, preview button, routine confirmation, or separate activation journey.
Do not add a second school cost-bearing switch.
Salary basis owns that choice.
Known earning types have explicit statutory defaults; custom unclassified earnings require review.
Keep headers and actions stable during loading, use form-shaped skeletons, and preserve drafts on failed saves and background refreshes.
Salary setup uses earnings and deductions side by side on desktop and stacked on smaller screens.
The existing calculation section shows the breakdown before saving.

## Configuration lifecycle and contracts

ESIC configuration belongs to the school and continues across academic years.
Persist one idempotent, versioned default-on baseline for a new school.
The baseline date is a technical calculation boundary, never a legal coverage date.
Unknown registration numbers and legal coverage dates stay empty.
Missing school registration facts do not block salary calculation or introduce an activation gate.
A failed settings request must show a load error, never a fabricated default.

GET and PUT /api/payroll/statutory-schemes/esi/configuration are the settings operations.
PUT accepts enablement, optional registration facts, earning-treatment edits, the current concurrency stamp, and an idempotency key.
The server derives the current month in the Gujarat payroll timezone, Asia/Kolkata.
The client cannot select a different configuration month.
Save requires Edit and Activate permission plus school-wide HR scope, without an approval screen.
View, ManageEnrollment, and ApproveOverride retain their existing boundaries.

Configuration saves and payroll locking share the tenant payroll input-revision transaction fence.
A stale concurrency stamp or reuse of an idempotency key for different inputs is rejected.
An identical retry returns the saved settings without creating another version.
Same-month edits supersede the earlier configuration with audit history.
Affected current/future unlocked snapshots and reviews are invalidated and must be recalculated before finalization.
Earlier months and locked or finalized employee snapshots remain intact, including within a partly locked payroll month.
Compensation previews, payroll snapshots, and stored payslip evidence identify the applicable configuration version.

When disabled, omit ESIC amounts and presentation while retaining employee enrollment facts.
The settings page explains that stopping platform calculation does not establish statutory exemption.
There is no external-amount entry.
Employee eligibility, exemptions, and contribution continuity remain automatic.
Existing employee onboarding, supported registration-pending status, insurance-detail checks, and payroll membership rules still apply.
Employee activation requires effective compensation and an insurance number when the applicable ESIC calculation covers the employee.
Registration pending permits salary preparation but never bypasses the activation check.
Activation re-evaluates saved full-month earnings using the applicable configuration, employee registration, contribution continuity, and supported exceptions.
ESIC off and employees outside coverage do not require an insurance number for activation.
HR can enter registration details for an inactive employee through the existing enrollment operation, subject to Statutory Schemes View, ManageEnrollment, and employee scope.
The onboarding Payroll step and salary setup screen open the same ESIC drawer before salary saving or activation.
Salary setup refreshes readiness and invalidates its displayed calculation after registration changes.
No onboarding bypass setting or separate ESIC activation step is introduced.
Payroll locking retains its insurance-detail and earning-classification checks.
No additional school-configuration activation gate is introduced.

Established wages are not silently reduced following a statutory or configuration change.
A changed protected compensation target is surfaced as a payroll blocker and resolved through compensation setup.
This is distinct from the initial CTC budget calculation.

## Documentation ownership and implementation status

This reference records the approved design.
It is not deployment or acceptance evidence.
The owning backend API and workflow docs describe contracts and invariants.
The frontend HR docs describe screen behavior, typed clients, and validation.
Keep actual test results separate from pending browser or acceptance criteria.
PF configuration, finance ledger posting, legacy migration, and deployment changes are excluded.

## Acceptance and validation

| Area | Required scenarios |
|---|---|
| Salary basis | Both bases, explicit employee basis, default/exception handling, and saved breakdown |
| Money | Worked-example totals, statutory rounding, full/partial attendance, other employer contributions, invalid budgets, and component constraints |
| Eligibility | Threshold boundaries, contribution continuity, supported exemptions, missing registration details, and custom unclassified earnings |
| Lifecycle | School-local month boundaries, earlier/locked snapshot preservation, unlocked recalculation, stale edits, retries, and concurrent save/lock operations |
| Reconciliation | Matching earnings and employee/employer shares across compensation preview, payroll, reports, and stored payslip evidence |
| Off mode | No ESIC calculation or presentation remnants; enrollment facts retained |
| Security | Allowed/denied actions, tenant isolation, employee scope, and server-side revalidation |
| Browser | Authenticated 390, 768, and 1440 px layouts, keyboard operation, form-shaped loading, and draft retention after errors |
| Development data | Coherent enabled, disabled-history, and employee-exception fixtures; cleanup limited to development fixtures and dependent sample payroll |

Feature implementation requires focused backend/frontend tests, typecheck, lint, documentation checks, and security regression checks.
Complete automated checks before final authenticated browser validation in the Codex in-app browser.
Treat the table above as acceptance criteria, not as evidence that those checks have run for the new salary-basis design.

For documentation delivery, run these from the backend repository:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module hr
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -AllModules
git diff --check
```

When frontend documentation is updated, run these from the frontend repository:

```text
npm run docs:hr:check
npm run docs:check
git diff --check
```

Report the exact checks executed and distinguish unrelated baseline failures from this documentation change.

## Implementation evidence: 2026-09-30

Implementation is local in the existing frontend and backend ESIC feature worktrees, synchronized with main.
No deployment files were changed, and no merge or deployment was performed.

| Check | Actual result |
|---|---|
| Focused domain tests | 51 passed, including eligibility, rounding, continuity, and fixture catalogue checks |
| Focused application tests | 67 passed, including both INR 10,000 examples, default initialization, and finalized snapshot preservation |
| API and permission tests | 28 passed across EsiStatutoryApi, SalaryTemplateApi, CompensationRevisionApi, and PermissionConsistencyApi |
| PostgreSQL configuration integration | 1 composite test passed for concurrent saves, save versus payroll-lock fencing, idempotent retries, and snapshot invalidation |
| Focused frontend tests | 34 passed across ESIC settings, salary template editor, compensation builder, and salary API contracts |
| Additional frontend settings and navigation tests | 22 passed; this run overlaps the ESIC settings tests above |
| TypeScript and scoped ESLint | Passed |
| HR documentation validators | Passed in both repositories |
| Authenticated local Playwright | Passed at 390, 768, and 1440 px, including loading, keyboard switching, failed-save draft retention, retry, and restoration of enabled state |
| Authenticated Codex browser | ESIC and salary-template layouts reviewed at 390, 768, and 1440 px; verified the two salary bases, CTC default, amount-label changes, keyboard disclosure, and discard confirmation |

The browser used a disposable local FullDemo school.
These results do not establish production deployment or a complete payroll-to-PDF acceptance sign-off.

### Remaining validation and wider issues

- The frontend security regression run passed 92 tests and failed one existing workflow-file expectation in permission-wiring.test.ts.
  Deployment files were deliberately left unchanged.
- Full documentation validation reports existing finance documentation issues in both repositories and additional frontend documentation metadata/map issues.
  The HR-specific validators pass.
- A wider backend payroll test run exposed salary-slip and component fixture failures beyond the focused suite.
  These failures were not reproduced against a clean main checkout, so their baseline status remains unverified.
- An over-broad API filter also selected unrelated pagination tests, producing five failures and three environment-dependent PostgreSQL skips.
  The corrected focused API filter passed all 28 selected tests.
- The shared breadcrumb clips at 390 px even though the ESIC form and actions remain usable.
  Fixing the shared header is a separate UI task.
- A complete authenticated create-compensation, attendance, payroll finalization, school-cost report, and downloaded payslip reconciliation remains an acceptance task.
  Calculation and snapshot tests provide focused evidence, not a replacement for that full workflow.

Reproduce the authenticated ESIC browser test from the frontend with ESI_LOCAL_E2E=1 and E2E_BASE_URL set to the disposable loopback frontend, then run:

```text
npx playwright test e2e/hr/esi-configuration.guest.spec.ts --project=chromium-guest --reporter=line
```

The configuration PostgreSQL test requires ESI_TEST_POSTGRES to identify a new empty loopback payroll_tests_* database.
It must never target a school database.

## Official sources

These sources were consulted during design on 2026-09-30.
Recheck applicable official rules before changing statutory calculation logic.

- [Social Security (Central) Rules, 2026](https://www.labour.gov.in/static/uploads/2026/05/49aa9b62c2125499c37399b90e969d67.pdf): Rule 19 contribution rates and upward whole-rupee rounding.
- [ESIC employer guide](https://www.esic.gov.in/attachments/publicationfile/7d5c1e6435befca342a31d9e36d40e27.pdf): coverage and contribution guidance.
- [ESIC FAQ](https://www.esic.gov.in/attachments/files/faq.pdf): continuation after crossing the wage ceiling within a contribution period.
- [Code on Social Security](https://labour.gov.in/sites/default/files/ss_code_as_passed_by_lok_sabha.pdf): section 124 wage-protection principle.

The example is a product calculation specification, not a legal compliance certification.

## Continuity changes and payroll readiness

Locking payroll or finalizing a payslip invalidates later unlocked calculations and reviews for affected employees through the end of the same contribution period.
Completed preview runs containing those employees become SUPERSEDED; queued or running previews in affected months are superseded conservatively.
Evidence changes, invalidation, preview publication and locking share the tenant payroll transaction fence.
A superseded worker cannot publish its earlier calculation.
Earlier periods and locked, finalized or paid employee snapshots remain unchanged.

Covered payroll requires an insurance number even when registration is pending or the employee contribution is zero.
Unclassified manual earning adjustments appear in the existing earning-classification list for the selected payroll month and permitted employees.
Saving their statutory treatment removes the classification blocker only after payroll recalculation.
Salary preparation remains available during pending registration; activation and payroll finalization remain blocked.
Exceptions select complete start and end months, with inclusive first-day and last-day API dates.
Partial-month exception requests are rejected rather than silently adjusted.

Employee ESIC drawer dismissal protects dirty forms through the shared discard confirmation.
Successful saves do not require confirmation, and a follow-up refresh error does not turn a successful write into a failed save.
Employee salary loading preserves the form geometry without a permanent save-explanation banner.
