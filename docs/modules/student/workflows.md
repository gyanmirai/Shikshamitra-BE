---
module: student
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-07-25
owner: backend-team
audience: humans-and-agents
---

# Trace Student backend workflows

TL;DR: Student workflows start with inquiry and registration, then move into admission, records, attendance, documents, and certificates.

## Core workflows

| Workflow | Main steps | Validation focus |
|---|---|---|
| Inquiry Source setup | Create, update, activate, deactivate, delete only unused source | Active-only selection, inactive historical display, usage count, delete eligibility |
| Inquiry | Capture lead details, source, notes, and follow-up information | Academic year, active source, contact data, and tenant scope |
| Registration | Configure versioned English, Gujarati, and Hindi sections and fields, issue a secure form, save partial responses, submit, correct, and verify | Canonical option values, localized rendering, section persistence, validation, document access, and status |
| Entrance exam results | Enter first-time marks individually or in bulk, import scanner-exported or office spreadsheets, and correct recorded results with evidence | Academic scope, duplicate exam IDs, mark bounds, correction reason, override permission, and per-item failure |
| Admission | Convert eligible inquiry or registration into admitted student record | Duplicate prevention, academic scope, admission state, and required data |
| Student records | List, search, update, bulk upload, and manage student dashboard data | Scope filters, pagination, dashboard preview freshness, and data integrity |
| Attendance and leave | Capture student attendance, manage leave, and configure leave types | Date consistency, student eligibility, leave type validity, and reporting impact |
| Certificates | Create templates and issue certificates | Student identity, template validity, and audit trail |
| Configuration safety | Inspect code references, preview critical impact, preview a source-to-target academic-year clone, and confirm the unchanged fingerprint | Academic-year-wide scope, applicant activity, target emptiness, draft recreation, ID remapping, and idempotent replay |

## Inquiry Source rules

| Scenario | Expected behavior |
|---|---|
| Active-only list after deactivation | Inactive source is excluded |
| Existing record with unchanged inactive source | Save remains valid |
| New or changed record uses inactive source | Request is rejected |
| In-use source delete | Request is rejected |
| In-use source deactivate | Request succeeds |
| Unused inactive source delete | Request succeeds |

## Admissions CRM governance rules

| Scenario | Expected behavior |
|---|---|
| Standard-scoped CRM list | Only leads whose `TargetStandardId` is inside the user's effective academic scope are returned |
| Lower-scope lead without target standard | The lead remains hidden because it cannot be assigned safely below academic-year scope |
| Out-of-scope CRM mutation | The request fails with `UserScope:AccessDenied` before changing admission data |
| Protected-stage override | The caller must have both stage-change and bulk-operations permissions, and the override is recorded in the applicant timeline |
| Registration fee waiver during issuance | Standard and direct issuance require the dedicated fee-waiver permission before a waived application can be created |
| Activation placement | Options are scope-filtered, mutations recheck Standard and Division access, and a captured target standard cannot be changed during activation |
| Legacy activation without target standard | An academic-year-wide user may select a valid placement while lower-scoped users remain unable to access the unattributable lead |
| School-default mutation | Only a tenant-wide user scope may change the tenant-global enrollment default |
| Retention-policy mutation | Create, submit, and publish require academic-year-wide user scope |
| Scheduled-report automation | The trusted worker validates the current tenant and academic year without depending on an interactive user |
| Locked journey structure edit | Cycle, custom-stage, stage-order, enabled-state, or allowed-transition changes fail with `AdmissionCrm:JourneyLocked` |
| Existing bulk result correction | A reason and `Students.Enrollment.AdmissionsCrm.OverrideExamResult` are required in addition to bulk and result-change permissions |
| Configuration reference lookup | Stage, Round, Exam, Seat Plan, or Route code usage is grouped by authoritative configuration and applicant families |
| Critical configuration preview | The response reports changed fields, affected applicants, references, warnings, and a deterministic fingerprint |
| Critical configuration update | The submitted cycle or pipeline-stage payload must carry the still-current preview fingerprint |
| Academic-year clone preview | The target must have no applicant activity or admission configuration before confirmation is enabled |
| Academic-year clone confirm | The submitted fingerprint must still match the source and target snapshots, and every source hierarchy identifier must map to an equivalent target hierarchy record |
| Academic-year clone replay | Repeating the same confirmed source and fingerprint reports `AlreadyApplied` without duplicating configuration |
