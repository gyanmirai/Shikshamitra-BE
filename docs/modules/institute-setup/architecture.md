---
module: institute-setup
domain: architecture
content_type: Conceptual
status: canonical
updated_on: 2026-10-21
owner: backend-team
audience: humans-and-agents
---

# Understand Institute Setup backend architecture

TL;DR: initial setup progress and Rooms Management live under Tenant Settings.
Controllers expose Institute Setup routes, while application services and domain entities remain in `TenantSettings/RoomsManagement`.
Shared academic setup and room-management tables use the `academic_` prefix.
See [academic performance and table naming](../examination/references/academic-performance-and-table-prefix.md) for the complete ownership boundary and migration deployment/rollback procedure.

## School Documents ownership

The feature mirrors `TenantSettings/SchoolDocuments` across Domain.Shared, Domain, Application.Contracts, Application and EntityFrameworkCore. `SchoolDocument` is a full-audited, multi-tenant aggregate; `SchoolDocumentFile` stores separately audited attachment metadata. `SchoolDocumentFileRules` validates count, safe names, sizes and signatures. Thin `SchoolDocumentsController` binds multipart requests and emits file responses; `SchoolDocumentAppService` performs authorization, transactional orchestration and bounded list projections.

`AppSchoolDocuments` and `AppSchoolDocumentFiles` are linked with a restrictive parent foreign key and tenant/deletion indexes. Files use generated `school-documents/{tenant}/{record}/{file}` keys through the existing database-backed ABP blob container. Entity history is enabled for both entity types; binary upload properties are excluded from auditing.

Migration `20261021052944_AddSchoolDocuments` adds only these tables and indexes. Apply it before exposing the feature, deploy the backend, explicitly assign permissions, then expose the frontend. Downgrading the application can leave the additive schema in place; running the migration Down deletes this feature's records and must not be used as a routine rollback for a populated library.

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

## Shift tenant boundary

`TenantSettings/ShiftManagement` implements `IMultiTenant`, enabling ABP filtering in repositories and navigation queries. `ShiftManagementAppService` also uses explicit ownership predicates for list and ID access. ABP's CRUD creation path assigns the server tenant, and `HRSeederContributor.Shifts` sets it for seeded shifts. `ShiftReferenceValidator` checks server ownership for standard and employee assignments.

Migration `20261014063540_AddShiftTenantIsolation` adds only `academic_ShiftManagements.TenantId` and its index. It preserves legacy rows without inferring ownership. Its designer and snapshot retain the preceding migration's unrelated legacy student-table definitions.

## School Identity ownership

| Layer | Source |
| --- | --- |
| Constants | `src/ShikshaMitra.Domain.Shared/TenantSettings/SchoolIdentity/SchoolIdentityPermissions.cs` |
| Error codes/localization | `SchoolIdentityErrorCodes.cs`, the Domain Shared exception-code mapping and English resource |
| Domain | `src/ShikshaMitra.Domain/TenantSettings/SchoolIdentity/**` |
| Contracts | `src/ShikshaMitra.Application.Contracts/TenantSettings/SchoolIdentity/**` |
| Configuration and resolution | `src/ShikshaMitra.Application/TenantSettings/SchoolIdentity/**` |
| EF mappings/repository | `src/ShikshaMitra.EntityFrameworkCore/TenantSettings/SchoolIdentity/**` |
| HTTP | `src/ShikshaMitra.HttpApi/Controllers/TenantSettings/SchoolIdentityController.cs` |
| Cleanup | `src/ShikshaMitra.HttpApi.Host/BackgroundWorkers/SchoolIdentityDraftCleanupWorker.cs` |

Profiles are unique by tenant and scope key. Choices are bounded JSON; ABP owns profile concurrency stamps. Revision records preserve prior selections. Immutable asset metadata points to tenant-owned blobs. Retention and cleanup-reservation flags are concurrency tokens; conditional SQL claims prevent cleanup from racing saved references.

The profile repository maps a concurrent first-insert violation of the tenant/scope unique index to a concurrency conflict. Unrelated database failures retain their original classification. History resolves creator usernames in one bounded tenant-filtered projection.

Migration `20261014053153_AddSchoolIdentity` adds only assets, profiles and revisions. It preserves unrelated snapshot entries for legacy student tables missing from the current source model; those tables are not dropped. Apply the additive migration before running the legacy import contributor or the updated host.

Document consumers enforce their own document action and record scope before using `SchoolIdentityDocumentService`. Configuration read permissions do not replace document authorization. The common template-asset service validates tenant/kind/origin scope and retains custom files.
