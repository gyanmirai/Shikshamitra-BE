---
module: institute-setup
domain: rooms-management
status: canonical
source: ../raw/rooms-management-api.raw.2026-03-27.md
updated_on: 2026-03-27
owner: backend-team
---

# Rooms Management API

## Purpose
CRUD APIs for buildings, room types, and rooms in institute setup.

## Endpoint(s)
- `/api/institute-setup/rooms-management/buildings`
- `/api/institute-setup/rooms-management/room-types`
- `/api/institute-setup/rooms-management/rooms`

## Auth & permissions
Use tenant-scoped auth and module permissions as configured in backend authorization policies.

## Request headers
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Path/query/body contract
Canonical detailed schema is in [raw contract](../raw/rooms-management-api.raw.2026-03-27.md).

## Response contract (success + errors)
Responses return room/building/room-type DTOs with validation and business errors for invalid references.

## Business rules / invariants
- Room must reference existing building and room type.
- Capacity/floor/bench values follow server validation constraints.

## Idempotency / concurrency notes (if relevant)
Standard CRUD semantics; no custom idempotency policy documented.

## Examples (minimal, copy-ready)
```http
GET /api/institute-setup/rooms-management/rooms
Authorization: Bearer <token>
__tenant: <tenant-id>
```

## Change log
- 2026-03-27: Created curated contract from legacy API guide.
