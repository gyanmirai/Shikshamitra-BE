---
module: student
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-07-28
owner: backend-team
audience: humans-and-agents
---

# Validate Student backend changes

TL;DR: use focused Student API tests for enrollment, records, attendance, and certificates, then run module docs validation.
Do not claim Enrollment behavior is production-ready until Pipeline, exam, registration, Finance handoff, placement, activation, permissions, and tenant scope have been validated.

## Required docs checks

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```

## Focused test map

| Change area | First tests |
|---|---|
| Admission Year Setup rules, versioning, publish readiness, and academic-year attachment | `AdmissionCrmAdmissionYearSetup_Tests` |
| Applicant Stepper state, blockers, skips, and next action | `AdmissionCrmApplicantProgressProjector_Tests` |
| Pipeline lead capture, duplicate detection, continue reason, source, type, owner, task, and message behavior | `AdmissionCrmApi_Tests` |
| Fixed Pipeline projection and grouped Admission Reports | `AdmissionCrmPipelineQueryService_Tests`, `AdmissionCrmConfigurationSafetyApi_Tests` |
| Entrance exam applicability, scheduling, result, eligibility, retest, and override behavior | `AdmissionCrmApi_Tests`, `AdmissionCrmBulkOperationsApi_Tests` |
| Registration issue, staff entry, parent entry, document checklist, correction, and verification | `AdmissionCrmRegistrationApi_Tests` |
| Finance handoff status and activation blocking | `AdmissionCrmFinancialReadiness_Tests`, `AdmissionCrmApplicantProgressProjector_Tests`, `AdmissionCrmActivationApi_Tests` |
| Placement options, capacity state, and override reason | `AdmissionCrmActivationApi_Tests` |
| Activation preview, approval, execution, and repair | `AdmissionCrmActivation_Tests`, `AdmissionCrmActivationApi_Tests` |
| Communication templates, consent, quiet hours, reminder cancellation, duplicate suppression, opt-out, and delivery-log states | `AdmissionCrmCommunicationDispatcher_Tests`, `AdmissionCrmApi_Tests` |
| Provider credential redaction and preserve-on-blank behavior | `AdmissionCrmCommunicationProviderSettings_Tests`, `AdmissionCrmApi_Tests` |
| Configuration references and impact fingerprints | `AdmissionCrmConfigurationSafetyApi_Tests` |
| Active Student notes | `StudentNotesApi_Tests` |
| Student attendance and leave | `StudentAttendanceApi_Tests`, `StudentLeaveApi_Tests` |
| Records, dashboard, and certificates | `StudentRecordsListApi_Tests`, `StudentDashboardApi_Tests`, `StudentCertificateApi_Tests` |
| Scope access | `StudentScopeAccessService_Tests` |

## Example commands

```bash
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~Students"
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Students"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~AdmissionCrm"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~StudentScopeAccessService_Tests"
```

## Acceptance gates

| Gate | Pass condition |
|---|---|
| Pipeline source of truth | Every pre-admission path creates or references a Pipeline lead |
| Duplicate prevention | Likely duplicate inquiries open the existing applicant or require permission and an audit reason to continue |
| Admission Year Setup | One active setup attaches to an Institute Setup academic year, publishes valid versions, and does not create a parallel year model |
| Applicant Stepper | Step status, blockers, skipped steps, and next action match configured rules and applicant evidence |
| Tenant isolation | Student reads and writes stay school-scoped |
| Academic scope | CRM lists and lead-backed mutations enforce effective Board, Medium, Stage, Standard, and Division scope |
| Exam governance | Required exams block registration until eligibility, bypass, or retest decision is valid |
| Registration readiness | Activation cannot proceed until registration and required documents are verified |
| Finance boundary | Enrollment does not post money and blocks activation when required Finance verification is missing |
| Placement boundary | Placement uses Institute Setup and Class Management capacity truth or returns unknown capacity |
| Activation integrity | Activation is idempotent or repairable and does not report false completion |
| Historical settings | Inactive Inquiry Sources, types, templates, and labels remain visible where historically assigned |
| Communication honesty | Delivery state is queued, sent, failed, provider-not-configured, opt-out-blocked, or provider-confirmed |
| Provider credential safety | Provider header values, parameter values, and callback secrets are never returned after storage |
| Report security | Admission Reports require Analytics permission and enforce tenant, academic-year, effective UserScope, date, and standard filters |
| Report consistency | Admission Reports and Pipeline use the same fixed lifecycle projection |
| Bounded processing | Report grouping stays in the database and lifecycle reminders process at most 250 applicants per unit of work |
| Docs | `validate-docs.ps1 -Module student` passes |

## Current validation note

The focused Domain and Application tests and the HTTP API test project build passed during the 2026-07-28 Enrollment refinement.
The permission-safe Admission Reports HTTP test passed after a 2 minute 41 second full-demo fixture initialization.
The provider, opt-out, delivery-log, timeline, and credential-redaction HTTP workflow passed after the UTC timeline sort fix.
The full-demo fixture is slow, but these focused HTTP assertions are runtime-verified.
