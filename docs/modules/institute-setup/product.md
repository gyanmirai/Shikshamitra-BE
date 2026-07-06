---
module: institute-setup
domain: product
content_type: Conceptual
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Understand Institute Setup product behavior

TL;DR: the active Institute Setup docs describe Rooms Management.
Rooms Management stores buildings, floors, room types, and rooms used by academic scheduling and operations.

## Active product areas

This table names the curated Institute Setup surface.

| Area | Owns | Primary source |
|---|---|---|
| Buildings | Building records and floors | `TenantSettings/RoomsManagement/Buildings/**` |
| Room types | Room type labels and limits | `TenantSettings/RoomsManagement/RoomTypes/**` |
| Rooms | Room identity, capacity, benches, building, floor, type, and features | `TenantSettings/RoomsManagement/Rooms/**` |

## Data-integrity rules

Rooms Management changes must preserve these rules.

| Rule | Why it matters |
|---|---|
| A room must reference valid building and room type records | Timetable and exam assignments depend on those links |
| Capacity and benches must remain valid numeric values | Scheduling and room allocation use them |
| Building floor data must stay consistent with room floor data | Staff need a reliable physical location map |
| Delete behavior must respect downstream references | Timetable and examination flows may reference rooms |

## Known partial surfaces

Institute Setup is broader than these active docs.
Use source inspection for Academic Setup, shift management, organization options, and tenant setup behavior.

| Surface | Current documentation status |
|---|---|
| Rooms Management | Curated in this module doc set |
| Academic Setup | Source-backed only in this compact pass |
| Shift Management | Source-backed only in this compact pass |
| Tenant setup state | Source-backed only in this compact pass |
