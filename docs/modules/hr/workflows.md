---
module: hr
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-07-06
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
| Employee onboarding | Create identity user, create employee aggregate, assign role, assign academic scope, and persist profile data | Required fields, duplicate contact data, role existence, tenant scope, and generated employee code |
| Employee profile update | Update personal, family, additional, work experience, payroll, reporting, and academic data | Round-trip persistence, date validation, bank validation, and manager validity |
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
| Payroll setup | Create earning, deduction, provident fund, pay element, and payroll policy records | Academic year, active status, component dependency, and destructive-change impact |
| Salary structure | Assign fixed and temporary earning or deduction lines to employees | Active components, amount ranges, temporary month ranges, and reload consistency |
| Payroll preview | Resolve policy, read attendance and leave facts, compute basis days, and calculate gross, deductions, and net pay | Month format, readiness blockers, policy basis, proration, and cached preview freshness |
| Payroll review | Mark rows reviewed and add adjustments before lock | Permission checks, row state, audit, and adjustment totals |
| Payroll lock | Finalize selected rows and create salary slip snapshots | Idempotency, already locked rows, snapshot basis, and error reporting |
| Salary slip | Read slip data or PDF for employee and month | Locked snapshot existence, totals, policy basis, and PDF access |

## Policy workflows

Policy workflows resolve effective behavior for employees:

| Workflow | Main steps | Validation focus |
|---|---|---|
| Work calendar policy | Define academic-year calendar and weekly-off behavior | Date bounds, holiday sync mode, and default behavior |
| Attendance policy | Define punch, missing punch, late, early, overtime, and holiday work rules | Evaluation results and holiday behavior |
| Payroll policy | Define day calculation, Saturday units, manual month days, and payroll holiday overrides | Payroll basis days and locked-month safety |
| Policy assignment | Assign policy to employee or group with effective dates and priority | Overlap detection, specificity, priority, and preview explanation |
| Policy versioning | Compare, restore, clone, archive, and audit policy versions | Immutable snapshots and restore-as-draft behavior |

## Reports and notifications

Reports and notifications expose HR data outside operational screens:

| Workflow | Main steps | Validation focus |
|---|---|---|
| HR report preview | Select source, options, template, and filters | Filter correctness and tenant isolation |
| HR report export | Generate PDF or Excel export and track history | Export parity with preview and audit metadata |
| HR notifications | Configure employee, attendance, leave, and event notification rules | Channel settings, permission checks, and history visibility |

## Partial workflow rule

If a workflow depends on a commented menu item or unconfirmed route, mark it partial in docs and tests.
Do not present partial routes as shipped navigation until frontend `hr-menus.ts` confirms visibility.
