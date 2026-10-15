---
module: institute-setup
domain: workflows
content_type: How-to
status: canonical
updated_on: 2026-10-14
owner: backend-team
audience: humans-and-agents
---

# Trace Institute Setup workflows

TL;DR: trace initial setup and Rooms Management through controller, contracts, application orchestration, domain rules and persistence.
Use this page for building, room type, and room changes.

## Initial setup lifecycle

1. Host provisioning creates the tenant, initial administrator and blank progress in one transaction.
2. School session context reads progress and reports whether completion is recorded.
3. The administrator saves partial drafts; reopening reads the latest server snapshot.
4. Profile, year and structure actions validate permissions, tenant ownership and the latest stamp before committing configuration and progress.
5. Optional users are created through Identity with retry IDs; passwords never enter progress JSON.
6. Review completion verifies confirmed changes, required profile data, a coherent academic branch and an active administrator.
7. Completion stores the operator/time and changes subsequent session context to completed.

Migration classifies pre-existing schools without granting or reseeding access.
Explicit completed markers remain complete; legacy partial states preserve their corresponding step; operational schools without markers remain complete only with a tenant-owned academic branch.
The [frontend workflows](../../../../Shikshamitra-FE/docs/initial-setup/workflows.md) own redirects, autosave feedback, explicit reload and working-year selection.

## Trace building changes

Building changes start in `BuildingController`.

1. Inspect `src/ShikshaMitra.HttpApi/Controllers/TenantSettings/RoomsManagement/Buildings/BuildingController.cs`.
2. Inspect DTOs under `src/ShikshaMitra.Application.Contracts/TenantSettings/RoomsManagement/Buildings/**`.
3. Inspect `src/ShikshaMitra.Application/TenantSettings/RoomsManagement/Buildings/BuildingAppService.cs`.
4. Inspect entities under `src/ShikshaMitra.Domain/TenantSettings/RoomsManagement/Buildings/**`.
5. Inspect mappings under `src/ShikshaMitra.EntityFrameworkCore/TenantSettings/RoomsManagement/Buildings/**`.

## Trace room type changes

Room type changes start in `RoomTypeController`.

1. Inspect `src/ShikshaMitra.HttpApi/Controllers/TenantSettings/RoomsManagement/RoomTypes/RoomTypeController.cs`.
2. Inspect DTOs under `src/ShikshaMitra.Application.Contracts/TenantSettings/RoomsManagement/RoomTypes/**`.
3. Inspect `src/ShikshaMitra.Application/TenantSettings/RoomsManagement/RoomTypes/RoomTypeAppService.cs`.
4. Inspect entities under `src/ShikshaMitra.Domain/TenantSettings/RoomsManagement/RoomTypes/**`.
5. Inspect mappings under `src/ShikshaMitra.EntityFrameworkCore/TenantSettings/RoomsManagement/RoomTypes/**`.

## Trace room changes

Room changes start in `RoomController`.

1. Inspect `src/ShikshaMitra.HttpApi/Controllers/TenantSettings/RoomsManagement/Rooms/RoomController.cs`.
2. Inspect DTOs under `src/ShikshaMitra.Application.Contracts/TenantSettings/RoomsManagement/Rooms/**`.
3. Inspect `src/ShikshaMitra.Application/TenantSettings/RoomsManagement/Rooms/RoomAppService.cs`.
4. Inspect entities under `src/ShikshaMitra.Domain/TenantSettings/RoomsManagement/Rooms/**`.
5. Inspect mappings under `src/ShikshaMitra.EntityFrameworkCore/TenantSettings/RoomsManagement/Rooms/**`.

## Trace downstream references

Rooms can appear in academic scheduling and examinations.
Inspect these paths before deleting fields or changing identifiers.

| Downstream area | Inspect |
|---|---|
| Timetable room assignment | `src/ShikshaMitra.Application.Contracts/Academic/Timetable/Timetables/AssignRoomsDto.cs` |
| Lecture slot room APIs | `test/ShikshaMitra.HttpApi.Tests/Academic/Timetable/LectureSlotRoomApi_Tests.cs` |
| Exam room DTOs | `src/ShikshaMitra.Application.Contracts/Academic/Examination/Exams/Dtos/ExamRoomDtos.cs` |

## School Identity lifecycle

1. The controller delegates configuration requests to the explicitly authorized application service.
2. Scope checks validate tenant-owned board/medium ancestry and the caller's data scope.
3. Uploads validate raster contents before creating immutable blob/metadata entries; no saved choice changes yet.
4. Submit validates selected asset kinds, ownership, signature permission and the profile stamp, retains referenced files, updates the profile and records a revision transactionally.
5. Resolution selects each asset independently from medium, board and defaults. Hidden stops fallback.
6. Draft previews use the same resolver without saving. Signature content is omitted for callers without access.
7. Certificate and student-card issuance freezes references; later reprints load retained files from that snapshot.
8. Cross-year copying requires an explicit source/target mapping and target stamp. It copies choices while reusing files.
9. Cleanup selects bounded batches of unretained old drafts, reserves them conditionally, removes their blobs and completes metadata deletion. Failed transactions can retry; retained document files are excluded.

The [frontend workflow](../../../../Shikshamitra-FE/docs/institute-setup/workflows.md) owns editor state, source labels, confirmations and previews.
