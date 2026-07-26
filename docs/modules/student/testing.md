---
module: student
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-07-25
owner: backend-team
audience: humans-and-agents
---

# Validate Student backend changes

TL;DR: use focused Student API tests for enrollment, records, attendance, and certificates, then run module docs validation.

## Required docs checks

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```

## Focused test map

| Change area | First tests |
|---|---|
| Enrollment admissions CRM, playbooks, multilingual registration, documents, automation, operations, and CRM entrance exam | `AdmissionCrmApi_Tests`, `AdmissionCrmPlaybookApi_Tests`, `AdmissionCrmRegistrationApi_Tests` |
| Individual and bulk result permissions, duplicate IDs, correction reasons, override governance, and retries | `AdmissionCrmApi_Tests`, `AdmissionCrmBulkOperationsApi_Tests` |
| Configuration references, impact fingerprints, clone conflicts, confirmation, draft recreation, and replay | `AdmissionCrmConfigurationSafetyApi_Tests` |
| Active Student notes | `StudentNotesApi_Tests` |
| Student attendance and leave | `StudentAttendanceApi_Tests`, `StudentLeaveApi_Tests` |
| Records, dashboard, and certificates | `StudentRecordsListApi_Tests`, `StudentDashboardApi_Tests`, `StudentCertificateApi_Tests` |
| House settings | `HouseSettingsApi_Tests` |
| Scope access | `StudentScopeAccessService_Tests` |
| Readiness approval governance | `AdmissionCrmDocumentChecklistAssignment_Tests` |

## Example commands

```bash
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~Students"
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Students"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~AdmissionCrm"
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~AdmissionCrmBulkOperationsApi_Tests|FullyQualifiedName~AdmissionCrmConfigurationSafetyApi_Tests|FullyQualifiedName~AdmissionCrmRegistrationApi_Tests"
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~StudentScopeAccessService_Tests"
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter "FullyQualifiedName~AdmissionCrmDocumentChecklistAssignment"
```

## Acceptance gates

| Gate | Pass condition |
|---|---|
| Student data integrity | Identity, contact, academic, medical, family, and document data are not silently corrupted |
| Tenant isolation | Student reads and writes stay school-scoped |
| Academic scope | CRM lists and lead-backed mutations enforce the effective Board, Medium, Stage, Standard, and Division scope |
| Journey lock | Locked cycle and pipeline structure cannot be changed through another configuration endpoint |
| Result correction | Individual, bulk, and imported corrections require reasoned override permission and preserve audit evidence |
| Configuration safety | Critical updates reject missing or stale preview fingerprints, clone confirmation rejects stale fingerprints, nonempty targets, and unmapped hierarchy IDs, and concurrent confirmations remain idempotent |
| Historical settings | Inactive Inquiry Sources remain visible where historically assigned |
| Docs | `validate-docs.ps1 -Module student` passes |
