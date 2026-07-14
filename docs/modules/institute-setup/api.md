---
module: institute-setup
domain: api
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Find Institute Setup backend APIs

TL;DR: the documented Institute Setup API surface covers buildings, room types, and rooms.
Use controllers for route truth and contracts for request and response shapes.

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
