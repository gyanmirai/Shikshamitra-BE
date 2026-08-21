---
module: student
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-08-18
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
dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflow
dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests/ShikshaMitra.EntityFrameworkCore.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflow
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflow
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AdmissionWorkflowRuntimeIntegration
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
| Workflow graph, controlled conditions, handlers, and runtime state | `test/ShikshaMitra.Domain.Tests/Students/Enrollment/AdmissionWorkflowGraphValidator_Tests.cs` and `test/ShikshaMitra.Domain.Tests/Students/StudentEnrollments/Admissions/AdmissionWorkflowConditionEvaluator_Tests.cs` |
| Frozen published version, transition history, retry idempotency, and future-application version selection | `test/ShikshaMitra.Application.Tests/Students/Enrollment/AdmissionWorkflowRuntimeIntegration_Tests.cs` |
| Form version pinning and runtime lifecycle | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~Published_Form_Version_Should_Remain_Frozen` and `--filter FullyQualifiedName~Form_Runtime_Should_Save_Submit_Audit_And_Reopen` |
| Form aggregate lifecycle and soft-delete-safe defaults | `dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter FullyQualifiedName~AdmissionFormDefinition` |
| Approved choice sources and authoritative option membership | Application build plus form runtime integration tests; source keys are allow-listed by `AdmissionFormDataSourceRegistry` and checked again during submit |

## Acceptance gates

Do not release if an active offering can bypass published configuration, an out-of-scope hierarchy is visible or writable, runtime versions are not frozen, identity changes after applications exist, or migration SQL drops historical data.
