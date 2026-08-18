---
module: student
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-08-14
owner: backend-team
audience: humans-and-agents
---

# Validate Student Enrollment Admissions changes

TL;DR: run domain, application contract, EF mapping, and HTTP contract tests for Admission Offering changes, then validate module docs and migration safety.

## Focused commands

Run from `Shikshamitra-BE`:

```powershell
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionOffering_Tests
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AdmissionOfferingContract_Tests
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~AdmissionOfferingModelMapping_Tests
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionOfferingApiContract_Tests
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module student
git diff --check
```

## Coverage map

| Surface | Evidence |
|---|---|
| Effective dates and availability boundaries | `test/ShikshaMitra.Domain.Tests/Students/Enrollment/AdmissionOffering_Tests.cs` |
| Bulk shape and granular permissions | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionOfferingContract_Tests.cs` |
| Defaults, nullability, indexes, restrictive FKs | `test/ShikshaMitra.EntityFrameworkCore.Tests/Students/Enrollment/AdmissionOfferingModelMapping_Tests.cs` |
| Required HTTP routes and concurrency inputs | `test/ShikshaMitra.HttpApi.Tests/Students/Enrollment/AdmissionOfferingApiContract_Tests.cs` |

## Acceptance gates

Do not release if an active offering can bypass published configuration, an out-of-scope hierarchy is visible or writable, runtime versions are not frozen, identity changes after applications exist, or migration SQL drops historical data.
