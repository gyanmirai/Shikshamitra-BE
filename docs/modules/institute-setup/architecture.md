---
module: institute-setup
domain: architecture
content_type: Conceptual
status: canonical
updated_on: 2026-10-09
owner: backend-team
audience: humans-and-agents
---

# Understand Institute Setup backend architecture

TL;DR: initial setup progress and Rooms Management live under Tenant Settings.
Controllers expose Institute Setup routes, while application services and domain entities remain in `TenantSettings/RoomsManagement`.
Shared academic setup and room-management tables use the `academic_` prefix.
See [academic performance and table naming](../examination/references/academic-performance-and-table-prefix.md) for the complete ownership boundary and migration deployment/rollback procedure.

## Layer map

### Initial setup

| Layer | Source | Responsibility |
| --- | --- | --- |
| HTTP | `src/ShikshaMitra.HttpApi/Controllers/TenantSettings/InitialSetupController.cs` | Explicit body binding and thin delegation |
| Contracts | `src/ShikshaMitra.Application.Contracts/TenantSettings/InitialSetup/` | Typed drafts, action inputs, snapshots and service interface |
| Application | `src/ShikshaMitra.Application/TenantSettings/InitialSetup/` | Authorization, transactional orchestration, readers, writers, confirmation and readiness |
| Domain | `src/ShikshaMitra.Domain/TenantSettings/InitialSetup/InstituteSetupProgress.cs` | Ordered durable workflow and completion audit |
| EF | `src/ShikshaMitra.EntityFrameworkCore/TenantSettings/InitialSetup/` | Aggregate mapping and unique tenant index |
| Migration | `src/ShikshaMitra.EntityFrameworkCore/Migrations/20261008120117_AddInstituteSetupProgress.cs` | Progress table and legacy classification |

`AppInstituteSetupProgress` uses TenantId as its primary identity and a unique tenant index.
DraftJson is bounded to 16384 characters and accepts only whitelisted typed fields.
Confirmed profile/year/structure projections and non-secret user summaries live in aggregate ExtraProperties.
The audited aggregate stores completion user/time and uses ABP/EF optimistic concurrency.
Writes run in transactional units of work so configuration and step advancement cannot partially commit.
Academic writes reuse domain managers; staff creation delegates to Identity.

Explicit tenant predicates apply to every academic join and target lookup, in addition to ABP filters.
The legacy InstituteSetup service maps reads to durable progress and routes only safe welcome/completion writes through the new service.
Rollback drops the new progress table and preserves the pre-existing tenant, identity, academic and legacy-state tables.

### Rooms Management

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
