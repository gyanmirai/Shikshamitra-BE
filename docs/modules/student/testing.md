---
module: student
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-07-06
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
| Enrollment inquiry | `InquiryApi_Tests` |
| Registration sections | `RegistrationGeneralDetailsApi_Tests`, `RegistrationFamilyDetailsApi_Tests`, `RegistrationAdditionalDetailsApi_Tests`, `RegistrationMedicalPreviousSchoolApi_Tests` |
| Admission | `AdmissionApi_Tests` |
| Entrance exam and notes | `EntranceExamApi_Tests`, `StudentNotesApi_Tests` |
| Student attendance and leave | `StudentAttendanceApi_Tests`, `StudentLeaveApi_Tests` |
| Records, dashboard, and certificates | `StudentRecordsListApi_Tests`, `StudentDashboardApi_Tests`, `StudentCertificateApi_Tests` |
| House settings | `HouseSettingsApi_Tests` |
| Scope access | `StudentScopeAccessService_Tests` |

## Example commands

```bash
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~Students"
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Students"
```

## Acceptance gates

| Gate | Pass condition |
|---|---|
| Student data integrity | Identity, contact, academic, medical, family, and document data are not silently corrupted |
| Tenant isolation | Student reads and writes stay school-scoped |
| Historical settings | Inactive Inquiry Sources remain visible where historically assigned |
| Docs | `validate-docs.ps1 -Module student` passes |
