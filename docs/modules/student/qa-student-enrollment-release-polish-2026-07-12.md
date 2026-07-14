---
module: student
domain: enrollment
content_type: Troubleshooting
status: canonical
updated_on: 2026-07-12
owner: backend-team
audience: humans-and-agents
---

# Student Enrollment release polish register

## Scope

This register covers the release-polish audit of the Student Enrollment CRM after the prior ENR-001 through ENR-011 fixes were merged.
Student Records, Attendance, Finance posting, government connectivity, vendor-specific messaging, and new admission capabilities are outside this audit.
The fresh DDVS automated browser run is complete, and every finding in this register is verified.

## Findings

| Finding | Severity | Persona | Route or area | Implementation status |
|---|---|---|---|---|
| SEP-001 | P2 | Administrator | Hidden legacy Enrollment routes and clients | Resolved and verified |
| SEP-002 | P2 | Receptionist and counselor | Pipeline, exams, and operations | Resolved and verified |
| SEP-003 | P2 | Administrator | `/student/enrollment/settings` | Resolved and verified |
| SEP-004 | P1 | Restricted staff | All protected Enrollment routes | Resolved and verified |
| SEP-005 | P1 | Admissions operations staff | DDVS role validation | Resolved and verified |
| SEP-006 | P2 | Keyboard and assistive-technology users | Protected Enrollment routes | Resolved and verified |
| SEP-007 | P1 | Release validator | Fresh DDVS `FullDemo` seed | Resolved and verified |

## SEP-001: Hidden legacy Enrollment workflows remain reachable

- Reproduction: Navigate directly to legacy dashboard, enroll, follow-up, or enrollment-status URLs, or import the obsolete Inquiry, registration-form, document-type, exam-slot, follow-up, or Enrollment-report Redux clients.
- Actual: Legacy route entry points and client code remain in the release bundle even though the CRM-first menu no longer exposes them.
- Expected: Proven-unreachable legacy Enrollment workflows should return `404`, and active Student Profile or Finance behavior should use neutral or owning-module components.
- Resolution: Removed hidden route entry points, isolated legacy screens, obsolete Redux clients and tag types, and moved shared profile fields, `FormSlider`, and fee assignment to neutral or Finance ownership.

## SEP-002: Workspaces preload unbounded operational data

- Reproduction: Open Pipeline, Exams, or Operations for a large admission cycle.
- Actual: Pipeline requests 200 leads, Exams requests 200 records, and staff selectors preload 100 users before the operator performs a search.
- Expected: Lists should use bounded server pages, board stages should load incrementally, and staff selectors should use debounced scoped search.
- Resolution: Lists, exams, and operations use 50-row pages, board stages use 25-lead pages with explicit load-more behavior, and staff selectors use a 25-result debounced server query while retaining selected identifiers.
- Verification: Workspace query guards prevent lead, stage, exam, analytics, communication, and staff requests from running outside their owning routes.

## SEP-003: Settings sections load as one oversized operational surface

- Reproduction: Open `/student/enrollment/settings` and inspect the initial requests and rendered panels.
- Actual: Journey, capture, routing, exams, registration, documents, seats, activation, governance, communications, and automation configuration load together.
- Expected: Each settings category should be an independently selected surface with only its required queries and actions.
- Resolution: Added ten explicit settings sections, lazy-loaded specialized configuration surfaces, section-gated core queries and panels, and unsaved-cycle protection for browser exit and section changes.

## SEP-004: Direct route access relies only on the Enrollment root permission

- Reproduction: Sign in with a role that has a limited Enrollment CRM permission and navigate directly to Offers, Activation, Analytics, or Settings.
- Actual: The shared layout authorizes the Enrollment root but does not enforce the route-specific frontend permission before rendering the workspace.
- Expected: The frontend should present a permission denial for direct unauthorized navigation while backend authorization remains authoritative.
- Resolution: Added route-specific workspace components and permission guards for Pipeline, Exams, Registrations, Offers and Seats, Activation, Operations, Analytics, and Settings.

## SEP-005: DDVS lacks deterministic operational admissions personas

- Reproduction: Run role-matrix browser validation against the seeded DDVS tenant.
- Actual: Principal and restricted users exist, but receptionist, counselor, and approver boundaries cannot be exercised with deterministic accounts.
- Expected: DDVS should seed least-privilege operational personas with stable credentials and explicit sensitive-action exclusions.
- Resolution: Added Admissions Receptionist, Admissions Counselor, and Admissions Approver roles, role grants, deterministic DDVS users, domain permission-boundary tests, and default browser-runner identities.

## SEP-006: Enrollment routes lack automated accessibility regression coverage

- Reproduction: Run the Enrollment browser suite.
- Actual: Functional journeys execute without an automated WCAG scan, so serious semantic, contrast, labeling, or focus regressions can pass unnoticed.
- Expected: Every protected Enrollment workspace should be scanned for serious and critical WCAG 2 A and AA violations.
- Resolution: Added `@axe-core/playwright` and route-level accessibility checks to the guarded CRM browser suite.
- Verification: The scan covers Pipeline, Exams, Registrations, Offers and Seats, Activation, Operations, Analytics, and Settings and reports no serious or critical WCAG 2 A or AA violations.

## SEP-007: A fresh FullDemo seed omits every operational aggregate

- Reproduction: Recreate the local DDVS schema and run the `FullDemo` profile once.
- Actual: The first run creates 20 leads and configuration but zero entrance exams, registration applications, seat offers, activations, or timeline events.
- Expected: A single fresh run should create the complete deterministic scenario graph, and a rerun should repair drift without duplicates.
- Root cause: The operational phase re-queries slots and leads that were created earlier in the same uncommitted seeding unit of work, so the first-run query sees no prerequisites and exits.
- Resolution: The configuration and lead phases now return their repaired aggregate instances directly to the operational phase, with explicit persistence checkpoints retained for reliable ordering and diagnostic prerequisite logging.
- Verification: A fresh run produced 20 leads, 15 entrance-exam records, 11 registration applications, three seat offers, three activations, and 15 timeline events, and an idempotent rerun preserved those counts.

## Validation gates

The backend build passed with zero errors, 85 Admission CRM HTTP tests passed, 10 Student application tests passed, and eight role and seeder domain tests passed.
The backend Student documentation validator passed, EF reported no pending model changes, and `git diff --check` passed.
The frontend production build passed all 352 routes, TypeScript passed, and 86 focused Student client, Enrollment, settings, and permission tests passed.
The local DDVS read-only preflight passed one test, and the complete mutating, permission, accessibility, public, exam, registration, correction, readiness, and handoff suite passed all 11 tests.
The connected Chrome visual pass remains the final manual review step for 1440 by 900, 1280 by 720, 768 by 1024, and 390 by 844 viewports.
