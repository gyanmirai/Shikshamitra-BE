---
module: student
domain: product-prd
content_type: Reference
status: canonical
updated_on: 2026-07-11
owner: backend-team
audience: humans-and-agents
---

# PRD: Enrollment operations, analytics, and governance

TL;DR: Give admissions teams one exception-driven workspace and give leadership reconciled, reproducible, privacy-aware reporting across the complete enrollment lifecycle.

## Relationship to the enrollment suite

Each companion PRD owns its domain actions and local analytics.
This PRD owns cross-stage operations, shared queue behavior, end-to-end metrics, audit governance, retention, data quality, exports, and release controls.

## Goals

- Let staff operate by next action, deadline, and exception rather than by disconnected registers.
- Give leadership one reconciled view from inquiry demand through first-day-ready students.
- Make workload, SLA, capacity, fairness, and override patterns visible.
- Preserve tenant isolation, academic scope, privacy, audit, and reproducibility.
- Support high-volume schools without unbounded lists, silent bulk failures, or manual spreadsheet reconciliation.

## Non-goals

- Do not replace domain-specific configuration or transactional workspaces.
- Do not expose protected identity, custody, category, or financial detail in general dashboards.
- Do not use predictive models to make final admission decisions automatically.
- Do not treat exported spreadsheets as the system of record.

## Admissions command center

The command center should show admission-cycle health, current funnel, seats, offers, payment conditions, verification, activation, SLA, and exception summaries.
Users should see only campuses, boards, mediums, classes, routes, and applicants within their scope.
The workspace should prioritize work requiring human action and should separate information from blocking exceptions.
Each item should show applicant, current milestone, owner, deadline, next action, blocking reason, and applicable quick actions.
Users should be able to move from a summary to the authoritative domain record without losing filters.

## Shared queue framework

Every queue should define inclusion rule, owner, priority, due date, escalation path, permitted actions, bulk behavior, and completion condition.
Queues should support saved views by cycle, campus, board, medium, class, round, route, category, stage, owner, age, deadline, and risk.
Common queues should include unassigned work, SLA breach, policy exception, applicant correction, document review, exam exception, seat conflict, offer response, payment condition, household resolution, identity conflict, final approval, activation repair, and cancellation review.
An applicant may appear in multiple queues, but each queue item should identify its independent resolution.
Closing a queue item should not silently close another unresolved condition.

## Assignment and workload

Work may be assigned manually or by team, campus, class, language, route, expertise, workload, or round-robin rule.
Reassignment should preserve prior ownership and reason after work has started.
Team leads should see workload, aging, due items, blocked items, and unassigned work.
The product should avoid routing protected cases to staff without the required permission.
Absence or leave coverage should support temporary delegation with start and end dates.

## SLA and escalation

SLAs should be configurable by milestone, route, queue, priority, campus, class, and cycle.
SLA clocks should support school working calendars, pauses for parent action, external authority action, and approved holds.
Every pause should have a reason and resume condition.
Escalations may notify owner, team lead, admissions coordinator, principal, Finance, or another configured role.
Repeated reminders should be capped and should stop when the action is resolved or no longer applicable.

## Bulk operations

Bulk actions may include assignment, task creation, reminder, offer preparation, waitlist review, document routing, approval routing, and activation preview.
Before commitment, the product should show eligible, ineligible, conflicted, and policy-blocked records.
Each applicant should have an independent result and retry path.
Bulk actions should not bypass maker-checker, capacity, payment, identity, or policy controls.
High-risk bulk actions should require downloadable preview evidence and second approval when configured.

## Unified timeline and evidence

The unified timeline should include CRM, exam, registration, document, policy, offer, seat, payment, household, decision, activation, communication, and repair events.
Events should distinguish staff action, applicant action, automation, Finance response, external authority response, system decision, and import.
Timeline access should respect protected-note and document permissions.
An event should identify source, actor, time, prior state, resulting state, reason, and correlation reference when applicable.
Corrections should append evidence and should not rewrite historical events silently.

## Operational analytics

Operational analytics should include current stage volume, stage aging, first response, next-action SLA, overdue tasks, unassigned work, verification backlog, offer expiry, payment gate aging, identity conflicts, activation repair, and cancellation review.
Users should be able to move from a metric to the matching governed worklist.
Counts should use the same inclusion definition in dashboards, exports, and queue lists.

## Funnel and yield analytics

The end-to-end funnel should cover inquiry, qualified, exam scheduled, exam attended, eligible, registration submitted, admission ready, offered, accepted, payment condition satisfied, approved, activated, cancelled, and first-day ready.
Metrics should support source, campaign, cycle, campus, board, medium, class, round, route, seat pool, owner, and period dimensions.
Yield should distinguish offer yield, accepted-seat yield, paid or cleared yield, activation yield, and net yield after cancellation.
Waitlist reporting should include position movement, promotion response, time to offer, and seats recovered.

## Capacity and financial-readiness analytics

Capacity reports should reconcile approved, reserved, offered, accepted, activated, released, cancelled, and remaining seats.
Financial-readiness reports should include quotation, payment-gate satisfaction, concession, scholarship, sponsored, mismatch, and refund-handoff status without duplicating Finance accounting.
Cross-module differences should appear in a reconciliation report with source timestamps.

## Equity and policy analytics

Authorized leadership should be able to review route, category, exception, override, and decision patterns for operational fairness and policy adherence.
Protected category analytics should be aggregated, purpose-limited, and access controlled.
The product should not display comparative rankings that expose an individual applicant's protected status.
Policy reports should identify rule version, exceptions, overrides, approver, and outcome.

## Forecasting and decision assistance

Phase three may forecast inquiry demand, offer acceptance, waitlist movement, seat fill, staffing need, and activation volume.
Forecasts should display inputs, confidence, historical comparison, and limitations.
Forecasts should not automatically reject applicants, change protected-category priority, waive policy, or overbook capacity.
Human users should remain accountable for consequential decisions.

## Data quality and reconciliation

Data-quality checks should identify duplicate applicants, duplicate people, impossible state combinations, orphaned seats, accepted offers without reservations, activated Students without lineage, identifier duplicates, invalid placements, stale payment states, and missing audit references.
Each issue should have severity, owner, detected time, impacted reports, and resolution history.
Corrections should use domain actions rather than direct report edits.
Daily or on-demand reconciliation should compare cross-stage counts and cross-module handoffs.

## Audit governance

Critical actions should preserve actor, delegated actor when applicable, tenant, academic scope, reason, evidence, source, policy context, prior state, resulting state, and correlation reference.
Audit events should be immutable to ordinary users.
Authorized auditors should be able to search by applicant, Student, household, offer, seat, payment reference, identifier, user, event type, and period.
Audit exports should be watermarked or otherwise traceable according to platform policy.

## Privacy, retention, and deletion

Retention should be configurable by unsuccessful applicant, withdrawn applicant, duplicate, activated Student, financial reference, consent, government acknowledgement, custody evidence, and audit category.
Deletion or anonymization requests should identify records that must be retained and should avoid breaking statutory or audit history.
Parent withdrawal of optional consent should stop future optional processing.
Sensitive exports should expire, require permission, and record download activity.
Analytics should minimize identity exposure and use aggregation where individual detail is unnecessary.

## Localization, accessibility, and low bandwidth

Parent-facing status, forms, offers, reminders, correction requests, and confirmations should support English, Gujarati, and Hindi.
Staff labels should use canonical product terms and support school-configured display names without changing report meaning.
Critical parent actions should remain usable on mobile and constrained networks.
Printable outputs should preserve readable multilingual content and version references.
Accessibility checks should include keyboard navigation, focus order, labels, contrast, error association, and screen-reader status updates.

## Exports and scheduled reporting

Exports should preserve applied filters, generation time, data-as-of time, tenant, cycle, and report definition version.
Large exports should run asynchronously and should not expose another user's or tenant's scope.
Scheduled reports should deliver only through approved destinations and should stop when the recipient loses access.
Government or board exports should use a versioned specification and should preserve acknowledgement or rejection results.

## Permissions

Permissions should distinguish command-center access, queue management, bulk action, leadership analytics, protected-category analytics, audit search, sensitive export, retention administration, and reconciliation repair.
Backend authorization should enforce every scope regardless of hidden frontend controls.
Delegation should never grant more access than the delegating user's active authority.

## Acceptance scenarios

- A coordinator sees one prioritized list of unresolved admissions work across stages.
- A queue count, drill-down list, and export use the same definition and filters.
- A bulk offer operation excludes applicants blocked by capacity, policy, or approval and reports each result.
- Leadership reconciles inquiry demand, offers, accepted seats, payment gates, activations, cancellations, and remaining capacity.
- A policy override report identifies the rule, reason, approver, applicant, and outcome.
- A duplicate identifier or activated Student without lineage creates a critical data-quality issue.
- A forecast is visibly advisory and cannot make or change a final admission decision.
- A sensitive export is tenant-scoped, permission-controlled, time-bound, and audited.
- English, Gujarati, and Hindi parent outputs retain the accepted wording version.
- Users without permission cannot view protected analytics, audit evidence, sensitive exports, or another tenant's applicants.

## Public concepts

Define Admissions Command Center, Operations Queue, Queue Item, Work Assignment, SLA Clock, SLA Pause, Escalation, Bulk Operation, Unified Timeline, Reconciliation, Data-Quality Issue, Report Definition, Data-As-Of Time, Protected Analytics, Audit Export, Retention Policy, and Enrollment Forecast.

## Delivery phases

Phase one should include command center, shared queues, assignment, SLA, unified timeline, core funnel, seat and activation reconciliation, audit, and scoped exports.
Phase two should add protected policy analytics, retention workflows, government exports, multilingual scheduled outputs, and richer cross-module reconciliation.
Phase three should add explainable demand, yield, waitlist, and workload forecasting with human decision governance.
