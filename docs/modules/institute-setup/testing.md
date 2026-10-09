---
module: institute-setup
domain: testing
content_type: Troubleshooting
status: canonical
updated_on: 2026-10-09
owner: backend-team
audience: humans-and-agents
---

# Validate Institute Setup backend changes

TL;DR: run the docs validator for documentation changes and targeted Tenant Settings or Academic tests for behavior changes.
Rooms Management does not currently have a dedicated test inventory in this compact doc set.

## Initial setup checks

| Scope | Command |
| --- | --- |
| State invariants | `dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter InitialSetupProgress_Tests --artifacts-path .artifacts/initial-setup-build` |
| HTTP integration | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter InitialSetupApi_Tests --artifacts-path .artifacts/initial-setup-build` |
| Host provisioning regression | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter HostTenantApi_Tests --artifacts-path .artifacts/initial-setup-build` |
| Model consistency | `dotnet ef migrations has-pending-model-changes --project src/ShikshaMitra.EntityFrameworkCore --startup-project src/ShikshaMitra.DbMigrator --configuration InitialSetup` |

HTTP checks exercise real services and persistence: new-school provisioning, reload, stale drafts, confirmed steps, staff retry, review edits, idempotent completion, denied writes, masked reads, anonymous/host denial and cross-school year selection.
The clean setup fixture avoids unrelated demo-data initialization.

Host tenant regressions also read the list after deactivation and reactivation, with and without a status filter.
They verify saved status, institute names, detail responses, concurrency protection, audit history and blocked school access.

Validate generated migration UP and DOWN scripts on a disposable PostgreSQL database before deployment.
Include explicit legacy completion, legacy partial progress, operational schools without markers, blank schools, mixed-tenant branches and deleted tenants.
Confirm rollback preserves all old school records.
Do not run a fixture or rollback against a live school database.
The [frontend testing page](../../../../Shikshamitra-FE/docs/initial-setup/testing.md) owns wizard and browser checks.

## Documentation checks

Run this command after editing active Institute Setup docs.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module institute-setup
```

## Focused backend tests

Use these tests as the first validation set when Rooms Management affects Tenant Settings or scheduling.

| Risk area | Command |
|---|---|
| Tenant authorization | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter TenantAuthorizationApi_Tests` |
| Academic room references | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter LectureSlotRoomApi_Tests` |
| Tenant Settings application behavior | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter TenantSettings` |

## Source checks

Use these checks when docs or routes change.

```powershell
rg -n "api/institute-setup|RoomsManagement|RoomController|BuildingController" src test docs/modules/institute-setup
git diff --check
```

## Add coverage when needed

Add or update tests when a change affects these areas.

| Change | Expected coverage |
|---|---|
| Building, floor, or room type relationship | Application test |
| Room payload or validation | HTTP API test |
| Delete behavior | Integration test with downstream references |
| Timetable or exam room impact | Academic HTTP API test |
