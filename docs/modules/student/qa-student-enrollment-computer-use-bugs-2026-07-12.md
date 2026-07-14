---
module: student
domain: enrollment
content_type: Troubleshooting
status: canonical
updated_on: 2026-07-12
owner: backend-team
audience: humans-and-agents
---

# Student Enrollment Computer Use QA Defects

## Test Context

This report records defects found during local end-to-end Student Enrollment testing on July 12, 2026.

Testing used the `user/yvadher/student-enrollment-admissions-crm` frontend and backend branches.

The local PostgreSQL schema was recreated from the intentional `20260712225012_v1` baseline and seeded with the DDVS `FullDemo` profile.

Manual testing used Vivaldi at a desktop viewport with the seeded `principal-gseb@ddvs.com` account and the 2026-2027 academic context.

The final read-only CRM Playwright preflight passed one test.

The final mutating CRM Playwright suite passed all seven tests, including principal, restricted-user, and anonymous coverage.

The final deterministic FullDemo baseline contained 20 CRM leads, 15 entrance-exam records, 11 registration applications, three seat offers, three activations, and the supporting templates, checklists, slots, documents, waitlist, reservations, and timeline events.

## Release Assessment

All eleven documented findings are resolved on the tested frontend and backend branches.

The release gates cover route compilation, authentication denial, deterministic scenarios, operational UX, public inquiry, entrance exams, registration, document correction, readiness, and handoff.

## Resolution Summary

| Finding | Status | Resolution evidence |
|---|---|---|
| ENR-001 | Resolved | Runtime and type-only imports are separated, TypeScript passes, and the production Next.js build renders all Enrollment routes. |
| ENR-002 | Resolved | Development auto-auth is disabled by default and requires explicit Development-only opt-in, while browser coverage verifies anonymous denial and the seeded restricted user verifies permission denial. |
| ENR-003 | Resolved | Enrollment Settings retains its client boundary and route contract coverage asserts it. |
| ENR-004 | Resolved | The paginated backend contract is normalized at the RTK boundary, the hard-coded stage fallback is removed, and stage-load failures are visible. |
| ENR-005 | Resolved | FullDemo and FastDemo repair complete configuration and scenario aggregates, while Minimal remains configuration-only. |
| ENR-006 | Resolved | Seeded advanced stages now have corresponding exam, registration, document, offer, waitlist, activation, timeline, and audit records. |
| ENR-007 | Resolved | Owner and assignee workflows use scoped searchable staff selectors while retaining UUIDs only in internal state and API contracts. |
| ENR-008 | Resolved | Desktop boards use stable fixed-basis horizontal columns and mobile layouts stack columns vertically. |
| ENR-009 | Resolved | Dedicated workspaces render route-specific primary content first and skip unrelated requests. |
| ENR-010 | Resolved | The authenticated content scrollbar resets when the pathname changes without discarding workspace filters. |
| ENR-011 | Resolved | Provider configuration uses validated adapter, payload, webhook, sender, header, parameter, response-path, and callback fields instead of editable JSON. |

## Resolved Defects

### ENR-001: CRM workspaces fail to compile because value imports are declared as type-only

- Severity: P0 release blocker.
- Area: Frontend shared Admissions CRM workspace.
- File: `../../../../Shikshamitra-FE/src/screens/student/enrollment/admissions-crm/index.tsx`.
- Reproduction: Start the Next.js development server and open `/student/enrollment/pipeline`.
- Actual: Next.js reports `The 'type' modifier cannot be used on a named import when 'import type' is used on its import statement` at line 22.
- Expected: The pipeline and all other workspaces that reuse the shared CRM screen should render.
- Impact: Pipeline, exams, registrations, offers, activation, operations, and analytics are unavailable from the published frontend branch.
- Root cause: Runtime RTK Query hooks and value enums are included in an `import type` block, which also contains an inline `type CreateAdmissionCrmLeadDto` modifier.
- QA status: Resolved and verified by TypeScript, route contract tests, and the production Next.js build.

### ENR-002: Anonymous requests are automatically elevated to an authenticated DDVS user

- Severity: P0 release-validation blocker and P1 environment-security risk.
- Area: Backend Development authentication pipeline.
- Files: `src/ShikshaMitra.HttpApi.Host/Middleware/DevAutoAuthMiddleware.cs` and `src/ShikshaMitra.HttpApi.Host/ShikshaMitraHttpApiHostModule.cs`.
- Reproduction: Send an unauthenticated GET request to `/api/student-enrollment/admissions-crm/{academicYearId}/leads` while the host runs in `Development`.
- Actual: The request returns `200` because `DevAutoAuthMiddleware` injects a privileged bearer token and the DDVS tenant header into every request without an Authorization header.
- Expected: The protected endpoint should return `401`, `403`, or an authentication redirect.
- Impact: Local anonymous-access and tenant-isolation tests cannot detect authorization regressions, and any environment accidentally running as Development would silently authenticate anonymous API callers.
- Evidence: The CRM Playwright test `anonymous users cannot access protected CRM lead APIs` failed because the protected endpoint returned `200`.
- Scope note: Production exposure was not confirmed because the middleware is registered only when `env.IsDevelopment()` is true.
- QA status: Resolved with disabled-by-default configuration, explicit opt-in, middleware tests, and anonymous browser denial.

### ENR-003: Enrollment Settings calls a client hook from a server component

- Severity: P0 settings blocker.
- Area: Frontend Enrollment Settings route.
- File: `../../../../Shikshamitra-FE/src/app/(auth-route)/student/enrollment/settings/page.tsx`.
- Reproduction: Open `/student/enrollment/settings` after correcting ENR-001.
- Actual: Next.js reports `Attempted to call useBoolean() from the server but useBoolean is on the client` at line 5.
- Expected: The Admissions CRM configuration screen should render.
- Root cause: The route component calls `useBoolean()` without a `'use client'` directive.
- QA status: Resolved and covered by Enrollment route and menu regression tests.

### ENR-004: Pipeline-stage API contract crashes Settings and is silently hidden in Pipeline

- Severity: P0 settings blocker.
- Area: Frontend RTK Query contract and pipeline-stage configuration.
- Files: `../../../../Shikshamitra-FE/src/redux/student/admissions-crm/admissions-crm-api.ts` and `../../../../Shikshamitra-FE/src/screens/student/enrollment/setting/admissions-crm-configuration.tsx`.
- Reproduction: Open `/student/enrollment/settings` after correcting ENR-001 and ENR-003.
- Actual: The screen throws `stages.map is not a function` at `admissions-crm-configuration.tsx:2576`.
- Expected: The configured pipeline stages should render and remain editable.
- Root cause: The backend returns `PagedResultDto<AdmissionCrmPipelineStageDto>`, while the RTK endpoint declares `AdmissionCrmPipelineStageDto[]` and does not extract `response.items`.
- Additional impact: The Pipeline workspace does not crash because it silently replaces the invalid response with hard-coded fallback stages, which can hide server configuration changes.
- QA status: Resolved and covered by RTK contract tests and explicit stage-load error behavior.

### ENR-005: FullDemo seeding does not create the documented operational scenario matrix

- Severity: P1.
- Area: Backend `AdmissionCrmSeederContributor` and DDVS validation readiness.
- Reproduction: Recreate the database, run the DDVS `FullDemo` seed, and inspect Enrollment settings and workspaces.
- Actual: The database contains 21 inquiry leads, 3 exam slots, and 8 document types, but contains zero playbooks, registration templates, document checklist templates, public inquiry forms, entrance-exam records, registration applications, seat offers, or activations.
- Expected: FullDemo should seed the documented playbooks, multilingual forms, checklists, and deterministic applicant scenarios for scheduled exams, no-shows, results, registration correction, document rejection, offers, waitlists, activation repair, and first-day readiness.
- Impact: Exams, registrations, offers, activation, and public-form surfaces are empty after a successful seed and cannot be manually validated without creating their prerequisites during the test.
- Evidence: Enrollment Settings displays `No admission playbooks configured` and `No form templates configured`, while the operational routes contain no corresponding records.

### ENR-006: Seeded stage labels and analytics contradict the underlying operational data

- Severity: P1 reporting defect in the seeded validation environment.
- Area: CRM pipeline and analytics summary.
- Reproduction: Open the pipeline board and compare stage counts with the summary cards and Entrance Exams workspace.
- Actual: The board shows leads in Exam Scheduled, Exam Completed, and Eligible stages, while the summary reports zero scheduled exams and zero eligibility handoffs and the Entrance Exams workspace reports no CRM exams.
- Expected: Stage counts, operational records, and analytics should be reproducible from the same audited events.
- Root cause: FullDemo assigns advanced lead stages without creating the corresponding exam, result, eligibility, offer, or activation aggregates.
- Impact: Demonstration analytics are misleading and cannot be used as release evidence.

### ENR-007: Applicant operations require raw user UUIDs

- Severity: P1 usability and data-integrity risk.
- Area: Pipeline applicant management and Operations filters.
- File: `../../../../Shikshamitra-FE/src/screens/student/enrollment/admissions-crm/index.tsx`.
- Reproduction: Open a lead from List view and select `Manage`.
- Actual: The Applicant Operations dialog asks staff to enter `Owner user ID`, and the Operations workspace also exposes `Owner user ID` and `Assigned to user ID` text fields.
- Expected: Staff should use scoped staff autocomplete controls showing names, roles, and current assignment context.
- Impact: Ordinary users cannot discover valid identifiers and can easily paste an incorrect user ID.

### ENR-008: New Inquiry board column is collapsed and hides lead actions

- Severity: P1 workspace usability defect.
- Area: Pipeline board layout.
- Reproduction: Open `/student/enrollment/pipeline`, scroll to the board, and keep the horizontal scrollbar at its leftmost position.
- Actual: The New Inquiry column is rendered as a narrow sliver with clipped title, lead text, and actions, while later columns receive normal widths.
- Expected: Every pipeline stage should have a stable minimum width and readable cards.
- Impact: Reception staff cannot reliably open or act on newly created inquiries from the default board view.
- Workaround: Switch to List view.

### ENR-009: Dedicated workspaces place their primary queues several viewports below shared dashboard content

- Severity: P2.
- Area: Pipeline, Exams, Registrations, Offers, Activation, Operations, and Analytics page composition.
- Reproduction: Open any dedicated Enrollment workspace at the top of the page.
- Actual: Summary cards, daily trends, communication delivery logs, communication opt-outs, and staff workload appear before the route-specific operational surface.
- Expected: Each dedicated route should show its primary queue or tool in the first viewport, with shared analytics available through the Analytics route or secondary panels.
- Impact: Repeated operational work requires excessive scrolling and the route distinction is visually weak.

### ENR-010: Enrollment route changes preserve the previous inner scroll position

- Severity: P2.
- Area: Frontend Enrollment shell navigation.
- Reproduction: Scroll one Enrollment route to the bottom and navigate to another Enrollment route.
- Actual: The destination opens at the previous scroll offset and can appear mostly blank until the user returns to the top.
- Expected: A route change should reset the main content scroll position or restore a route-specific saved position.
- Impact: Users can interpret a populated workspace as empty or broken.

### ENR-011: Normal configuration exposes raw provider JSON

- Severity: P2 configuration usability and validation risk.
- Area: Communication provider configuration.
- File: `../../../../Shikshamitra-FE/src/screens/student/enrollment/setting/admissions-crm-configuration.tsx`.
- Reproduction: Open Enrollment Settings and inspect communication-provider configuration.
- Actual: A normal editable field labeled `Settings JSON` is exposed alongside structured callback fields.
- Expected: Normal workflows should use validated structured controls, with raw JSON limited to a permission-controlled diagnostics view.
- Impact: Invalid JSON or unsupported keys can be entered without product-level guidance.

## Passing Journeys

- Minimal quick inquiry correctly blocks missing contact and missing admission intent.
- Minimal quick inquiry succeeds with only contact and admission intent.
- The created inquiry appears in List view with New Inquiry stage and a calculated score.
- Public inquiry captures consent and creates a status token in automated browser coverage.
- Entrance-exam scheduling, attendance, result, and eligibility handoff pass in automated browser coverage after test setup creates the missing prerequisites.
- Registration-before-exam gating passes in automated browser coverage.
- Parent registration, document correction, readiness, and handoff pass in automated browser coverage.
- Anonymous protected-API denial passes in automated browser coverage.
- Restricted-user CRM denial passes with the deterministic `student-crm-denied@ddvs.com` account.
- The production Next.js build completes all 356 routes, including every Enrollment workspace and Settings.
- The backend Admission CRM and development-auth pack passes 69 tests.
- The backend Student application pack passes 10 tests.
- The focused frontend Student Enrollment pack passes 83 tests.

## Validation Notes

- Vendor-specific communication dispatch was not tested because generic manual and webhook adapters are the current supported baseline.
- Government connectivity and Finance posting were not tested because those integrations remain deferred.
- Computer Use connected to Vivaldi and exercised the local sign-in surface, but the browser password-manager prompt repeatedly interrupted manual authenticated navigation.
- Desktop authenticated Enrollment composition and all mutating journeys were therefore verified with the real Chromium Playwright suite, while responsive layout behavior is covered by the focused component and route tests.
