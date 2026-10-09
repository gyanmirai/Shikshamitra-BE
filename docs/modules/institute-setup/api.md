---
module: institute-setup
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-09
owner: backend-team
audience: humans-and-agents
---

# Find Institute Setup backend APIs

TL;DR: Institute Setup APIs cover initial progress, completion, buildings, room types, and rooms.
Use controllers for route truth and contracts for request and response shapes.

## Initial setup API

All operations use `api/institute-setup/initial` and require authenticated identity whose tenant matches the server's current tenant.
Host identities cannot enter this school API.

| Method and suffix | Input | Behavior |
| --- | --- | --- |
| GET collection | None | Current progress; users without setup grants receive masked draft/user details |
| PUT `draft` | `currentStep`, typed `draft`, `concurrencyStamp` | Persist partial entries within available steps |
| PUT `profile` | Profile fields and stamp | Update the current tenant's whitelisted details and confirm step 1 |
| PUT `academic-year` | Name, start/end dates, optional existingYearId, createNew, stamp | Create, select or update a tenant-owned year and confirm step 2 |
| PUT `structure` | Typed draft and stamp | Create/reuse the academic branch and confirm step 3 |
| PUT `advance` | Step and stamp | Advance only Welcome (0) or optional Users (4) |
| POST `users` | Identity create input, requestId, stamp | Create optional staff through the standard Identity service |
| PUT `complete` | Stamp | Verify readiness and record completion; already complete returns current status |

Writes require both `InstituteSetup.BasicInformation.Edit` and `InstituteSetup.AcademicSetup.Add`.
Mutating existing academic records additionally requires `InstituteSetup.AcademicSetup.Edit`; reusing unchanged records does not.
Creating staff additionally requires `AbpIdentity.Users.Create`, with Identity enforcing role and password rules.
The school session context at `api/host/session-context` exposes `setupCompleted` and `canManageSetup`; its existing host capabilities remain separate.

| Response or error | Meaning |
| --- | --- |
| Progress response | isCompleted, canManageSetup, currentStep, furthestStep, stamp, schoolCode, year/division IDs, completedAt, draft, createdUsers and pendingSteps |
| 401 / 403 | Missing authentication, wrong tenant/host identity or missing exact action permission |
| 404 | Referenced record is not owned by the current tenant |
| 409 | Stale concurrency stamp |
| 400 with `ShikshaMitra:InitialSetup:*` | Invalid order, incomplete profile/structure, unconfirmed edits or invalid request reuse |
| Identity business errors | Password, username, email or role validation failure |

Contracts live in `src/ShikshaMitra.Application.Contracts/TenantSettings/InitialSetup/InitialSetupDtos.cs`.
Localized error codes live in `src/ShikshaMitra.Domain.Shared/TenantSettings/InitialSetup/InitialSetupErrorCodes.cs`.

## Rooms Management endpoint families

This table maps the active routes to controllers and contracts.

| Surface | Route family | Controller | Contract owner |
|---|---|---|---|
| Buildings | `api/institute-setup/buildings` | `BuildingController` | `IBuildingAppService` |
| Room types | `api/institute-setup/rooms-management/room-types` | `RoomTypeController` | `IRoomTypeAppService` |
| Rooms | `api/institute-setup/rooms` | `RoomController` | `IRoomAppService` |

## Route operations

Each Rooms Management controller exposes the standard collection and item operations.

| Controller | Operations |
|---|---|
| `BuildingController` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` |
| `RoomTypeController` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` |
| `RoomController` | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` |

## Request and response sources

Use these paths before changing payload shape.

| Surface | DTO path |
|---|---|
| Buildings | `src/ShikshaMitra.Application.Contracts/TenantSettings/RoomsManagement/Buildings/**` |
| Room types | `src/ShikshaMitra.Application.Contracts/TenantSettings/RoomsManagement/RoomTypes/**` |
| Rooms | `src/ShikshaMitra.Application.Contracts/TenantSettings/RoomsManagement/Rooms/**` |

## Source inspection rule

Inspect controllers and DTOs when this page does not include enough request or response detail.
Do not restore raw generated exports under `docs/`.
