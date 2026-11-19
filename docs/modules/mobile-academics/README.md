---
module: mobile-academics
domain: overview
content_type: Landing
status: canonical
updated_on: 2026-10-30
owner: backend-team
audience: humans-and-agents
---

# Mobile Academics

TL;DR: dedicated student and guardian APIs serve Dashboard, Homework & Assignment, Attendance, Syllabus, Exam, Result, Holidays & Events, and Timetable. Elective preferences are submitted once through mobile and approved through the existing school assignment lock.

## Read order

| Page | Purpose |
|---|---|
| [product.md](./product.md) | Published data, ownership, and availability |
| [api.md](./api.md) | Routes and request conventions |
| [architecture.md](./architecture.md) | Source and persistence |
| [workflows.md](./workflows.md) | Reads, submissions, leave, and downloads |
| [testing.md](./testing.md) | Commands and validation limits |

## Source of truth

| Layer | Path |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Academic/Mobile/MobileAcademicDtos.cs` |
| Services | `src/ShikshaMitra.Application/Academic/Mobile/` |
| HTTP | `src/ShikshaMitra.HttpApi/Controllers/Academic/Mobile/` |
| Tests | `test/ShikshaMitra.Application.Tests/Academic/Mobile/MobileAcademicAppService_Tests.cs` |
| Timetable / elective contracts | `src/ShikshaMitra.Application.Contracts/Academic/Timetable/Portal/` |
| Flutter integration | [Mobile binding guide](../../../../Gyanmirai-Mobile/docs/mobile-academics.md) |

## For agents

Read [api.md](./api.md) before modifying a Flutter request. Keep persisted portal links and tenant checks on every endpoint. Do not replace unavailable records with examples.

## Validation

Run from the backend repository:

```powershell
dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~MobileAcademicAppService_Tests
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module mobile-academics
git diff --check
```
