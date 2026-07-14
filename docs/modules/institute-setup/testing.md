---
module: institute-setup
domain: testing
content_type: Troubleshooting
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Validate Institute Setup backend changes

TL;DR: run the docs validator for documentation changes and targeted Tenant Settings or Academic tests for behavior changes.
Rooms Management does not currently have a dedicated test inventory in this compact doc set.

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
