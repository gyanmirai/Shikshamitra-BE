---
module: examination
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-08-31
owner: backend-team
audience: humans-and-agents
---

# Validate Examination seating backend changes

TL;DR: focused application tests cover plan grouping, unique ownership, draft reset, legacy per-plan reopening, global coverage, atomic publication, post-publication immutability, compatibility, conflicts, and stale versions.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~ExamSeatAllocationAppService_Tests"
dotnet build src/ShikshaMitra.HttpApi/ShikshaMitra.HttpApi.csproj
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module examination
git diff --check
```

Before production deployment, inspect the generated migration and run the repository migration verification process.
Acceptance requires tenant and user-scope enforcement, one plan owner per exam subject at both application and database layers, shared plans across different schedules, a fully recoverable draft, no publication with unplanned subjects, all-plan atomic publication, one publication record, post-publication immutability, one exam dashboard row, no partial replacement during apply, repeat-safe requests, truthful plan counts and capacity, and actionable validation errors.
