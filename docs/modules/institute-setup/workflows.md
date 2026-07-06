---
module: institute-setup
domain: workflows
content_type: How-to
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Trace Institute Setup workflows

TL;DR: trace Rooms Management workflows from route to controller, then contract, application service, domain entity, and database configuration.
Use this page for building, room type, and room changes.

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
