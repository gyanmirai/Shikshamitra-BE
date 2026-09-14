---
module: hr
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-09-03
owner: backend-team
audience: humans-and-agents
---

# Trace HR backend workflows

TL;DR: use this page to understand how HR actions move through employees, attendance, leave, policies, payroll, and reports.
Each workflow points to the API and testing pages that validate it.

## Employee workflows

Employee workflows create and maintain staff records:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Employee onboarding | Validate the submitted application, create the identity user and employee aggregate, assign an explicitly onboarding-approved Employee-audience role compatible with the employee group, apply policy defaults and teaching scope, finalize in one unit of work, and run profile, event, and welcome-email effects after commit | Terminal application status, Category and Family Caste, duplicate contact data, role approval and employee-group audiences, BulkAssign permission for teaching scope, tenant scope, academic hierarchy, rollback, generated employee code, and deferred effects |
| Employee profile update | Update personal, family, additional, work experience, payroll, reporting, and academic data | Round-trip persistence, date validation, bank validation, manager validity, and role compatibility when the role or employee group changes |
| Employee status change | Submit target status and remarks, then persist transition and audit metadata | Required remarks, blocked transitions, idempotent same-status behavior, and payroll or attendance eligibility impact |
| Employee documents | Upload, list, download, replace, and delete configured document types | File size, file type, magic bytes, blob storage, and tenant scope |

## Attendance workflows

Attendance workflows create payroll-relevant facts:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Manual attendance | Select date, update employee statuses, save day facts, and optionally lock day | Lock behavior, scoped employee set, status mapping, and payroll preview invalidation |
| Attendance tracking | Query date-wise or employee-wise attendance and grouped register rows | Pagination, date range, scope filtering, and adjustment behavior |
| Attendance import | Preview file, classify rows, confirm rows, and create batch audit records | Header parsing, employee matching, single payroll month, warnings, and batch status |
| Automated attendance run | Read automated run rows, update row state, and lock or unlock batch | Device-run audit, row replacement, batch lock, and payroll log mirror |

## Leave workflows

Leave workflows use policy rules and affect payroll:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Leave policy setup | Configure paid and unpaid rule buckets for an academic year | Required paid type, finite or unlimited balance, half-day rules, and duplicate rules |
| Leave request | Submit date range, paid type, reason, and optional attachment | Balance validation, date validation, overlap checks, and employee eligibility |
| Leave approval | Approver accepts or rejects request and updates balance or status | Authorization, approver chain, rejection reason, and payroll impact |
| Leave center | Show balances, history, and request state for one employee | Accrued days, remaining days, unlimited unpaid display, and negative-balance rules |

## Payroll workflows

Payroll workflows depend on employee, attendance, leave, and policy data:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Payroll setup | Create earning, deduction, provident fund, professional-tax, ESI establishment, salary-template, pay element, and payroll policy records | Tenant scope, effective dates, active status, statutory wage classification, same-tenant active dependency targets, acyclic earning dependencies, template gross reconciliation, and destructive-change impact |

The salary builder resolves full-month Gross directly, solves the lowest Gross that reaches a Take-home quote, or solves the highest Gross that stays within an Employer Cost quote.
The solver partitions professional-tax slabs and ESI coverage boundaries and evaluates the same statutory calculators used by payroll.
It calculates Basic and other earnings, assigns the balancing earning, calculates employee deductions, and returns employer-funded contributions separately.
Salary Template settings starts with a blank draft and lets users compose variable earning and deduction rules from the active tenant component catalog.
Draft validation allows incomplete structures but rejects malformed rows, duplicate components or special roles, invalid dependencies, and cycles.
Publishing additionally requires exactly one Basic earning and one remainder earning.
When a published template receives a new draft, employee onboarding continues to use the current published version until the draft is published.
Users can save one reusable Draft compensation revision, update it, and submit it when ready.
The revision records effective month, template, quote, and statutory provenance.
A different authorized user must approve the revision before payroll can use it.
Approved revisions are immutable, and a replacement approved for the same month supersedes the older revision.
Later template or statutory changes do not silently change approved compensation.
Approval recalculates the quote and rejects a stale draft when the result has changed.
One-time earnings and deductions remain separate employee salary components and do not become revision lines.
ESI activation follows establishment details, earning-component wage mapping, employee enrollment review, tenant-wide impact preview, and explicit approval.
HR can complete establishment details, earning classifications, and employee review in any order before approval.
The monthly impact preview loads as a paged employee projection, but readiness and contribution totals always use the complete scoped population.
Activation must submit the fingerprint HR reviewed and recalculates all activation-relevant inputs in the same month before making changes.
A changed rule, establishment, treatment, enrollment, override, compensation revision, or calculated contribution makes the reviewed fingerprint stale and requires a fresh review.
An active ESI scheme is evaluated for every payroll employee even when the selected salary template has no ESI row.
ESI coverage above the entry ceiling continues only through the recorded contribution-period end or an audited override.
PwD employer-share relief applies only to the PwD wage-ceiling category with a recorded start date and disability evidence.
The employee contribution remains a deduction, while an eligible employer share is recorded as government funded and excluded from employer cost.
Users with override approval permission can review immutable exception history and add a dated exception with a mandatory reason and optional evidence reference.
| Compensation revision | Quote recurring salary, submit for approval, approve or reject, and resolve by effective month | Template version, effective statutory rules, separate approver, stale calculation detection, locked payroll, and immutable approved snapshots |
| One-time components | Assign temporary earning or deduction lines to employees | Active components, amount ranges, applicable month, and reload consistency |
| Payroll preview | Resolve the approved revision, append one-time lines, read attendance and leave facts, compute basis days, and calculate gross, deductions, net pay, and employer cost | Month format, approved compensation readiness, policy basis, proration, statutory provenance, warning-only protected-target drift, and cached preview freshness |
| Payroll review | Mark rows reviewed and add adjustments before lock | Permission checks, row state, audit, and adjustment totals |
| Payroll lock | Finalize selected rows and create salary slip snapshots | Idempotency, already locked rows, snapshot basis, and error reporting |

Finalized and Paid salary slips are both immutable payroll facts and both block attendance, policy, component, preview-refresh, and month-lock changes that would invalidate their snapshots.
| Salary slip | Read slip data or PDF for employee and month | Locked snapshot existence, totals, policy basis, and PDF access |

## Policy workflows

Policy workflows resolve effective behavior for employees:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Work calendar policy | Define academic-year calendar and weekly-off behavior | Years 1900 through 2100, at least one working day, holiday sync mode, and default behavior |
| Attendance policy | Define punch, missing punch, late, early, overtime, and holiday work rules | Evaluation results and holiday behavior |
| Payroll policy | Define day calculation, Saturday units, manual month days, and payroll holiday overrides | Payroll basis days and locked-month safety |
| Policy assignment | Assign policy to employee or group with effective dates and priority | Overlap detection, specificity, priority, and preview explanation |
| Policy versioning | Compare, restore, clone, archive, and audit policy versions | Immutable snapshots, semantic holiday changes, readable weekly-off days, and restore-as-draft behavior |

Archived work-calendar, attendance, payroll, and leave policies release their name for reuse while active and draft records remain protected by tenant- and academic-year-scoped unique indexes.
Current and archived policy workspaces use independent server-side search, count, and pagination so accumulated history cannot hide live policies.
Leave policies are restored as drafts, and restoration stops with the standard duplicate-name error if a current policy has reused the archived name.
Normal leave-policy create and update calls reject Archived status, and only the separately authorized archive workflow may archive a current policy.

## Reports and notifications

Reports and notifications expose HR data outside operational screens:

| Workflow | Main steps | Validation focus |
|---|---|---|
| HR report preview | Select source, options, template, and filters | Filter correctness and tenant isolation |
| HR report export | Generate PDF or Excel export, filter history by requested date, and remove an owned failed job | Export parity with preview, wide-PDF layout, owner and tenant scope, soft deletion, and audit metadata |
| HR notifications | Configure employee, attendance, leave, and event notification rules | Channel settings, permission checks, and history visibility |

When an export is queued, retried, regenerated, or run from a schedule, the Reports Center clears the history date filter before highlighting the new job.
An inverted custom history range does not issue an unfiltered export-history request.
PDF history metadata reports the capped row count and the document tells the user to choose Excel for the complete result.
Direct sharing and schedules reject a recipient unless the recipient has report-view permission and a current tenant UserScope that covers the export owner's complete generation scope.
Schedule execution rechecks the owner's report-view permission and every recipient's permission and scope before generating or distributing an export.
The schedule worker processes each due schedule in an independent transaction, pauses permanent authorization or validation failures, defers transient failures with bounded retry delay, and continues with later schedules.
Email delivery begins only after the export and schedule transaction commits, then uses a fresh tenant-scoped read unit of work and contains delivery failures as best-effort notifications.
Every owner or shared-recipient export read, share, or download repeats the scope check so later permission or scope changes cannot expose a previously generated artifact.
Export workers load the current persisted request and generation-scope snapshot instead of trusting queued argument copies, so a stale cancelled invocation cannot bypass a narrower retry scope.
Each retry receives a new generation identity and generation-specific blob name, and only the still-active generation may publish bytes or change the job outcome.
The worker persists a current owner-scope snapshot before generating any legacy queued job that predates snapshot storage.

## Partial workflow rule

If a workflow depends on a commented menu item or unconfirmed route, mark it partial in docs and tests.
Do not present partial routes as shipped navigation until frontend `hr-menus.ts` confirms visibility.
