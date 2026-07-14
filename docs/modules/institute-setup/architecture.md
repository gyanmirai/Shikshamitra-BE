---
module: institute-setup
domain: architecture
content_type: Conceptual
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Understand Institute Setup backend architecture

TL;DR: Institute Setup Rooms Management lives under Tenant Settings.
Controllers expose Institute Setup routes, while application services and domain entities remain in `TenantSettings/RoomsManagement`.

## Layer map

Use this map before changing Rooms Management behavior.

| Layer | Path | Role |
|---|---|---|
| HTTP API | `src/ShikshaMitra.HttpApi/Controllers/TenantSettings/RoomsManagement/**` | Route declarations and HTTP actions |
| Contracts | `src/ShikshaMitra.Application.Contracts/TenantSettings/RoomsManagement/**` | App service interfaces and DTOs |
| Application | `src/ShikshaMitra.Application/TenantSettings/RoomsManagement/**` | Workflow orchestration and validation |
| Domain | `src/ShikshaMitra.Domain/TenantSettings/RoomsManagement/**` | Building, floor, room type, and room entities |
| Domain shared | `src/ShikshaMitra.Domain.Shared/TenantSettings/RoomsManagement/**` | Constants and shared limits |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/TenantSettings/RoomsManagement/**` | Entity Framework configuration |

## Domain areas

Rooms Management is split by the physical setup object.

| Area | Owns |
|---|---|
| `Buildings` | Buildings and floors |
| `RoomTypes` | Room type records |
| `Rooms` | Room records and their physical attributes |

## Agent inspection order

Start with the smallest source set that answers the change.

| Task | Inspect first |
|---|---|
| Route change | `HttpApi/Controllers/TenantSettings/RoomsManagement/**` |
| Payload change | `Application.Contracts/TenantSettings/RoomsManagement/**` |
| Validation or relationship change | `Application/TenantSettings/RoomsManagement/**` and `Domain/TenantSettings/RoomsManagement/**` |
| Database mapping change | `EntityFrameworkCore/TenantSettings/RoomsManagement/**` |
| Academic setup behavior | `TenantSettings/AcademicSetups/**` source paths |
