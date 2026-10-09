---
module: institute-setup
domain: product
content_type: Conceptual
status: canonical
updated_on: 2026-10-09
owner: backend-team
audience: humans-and-agents
---

# Understand Institute Setup product behavior

TL;DR: Institute Setup owns durable first-login configuration and Rooms Management.
Rooms Management stores buildings, floors, room types, and rooms used by academic scheduling and operations.

## Active product areas

This table names the curated Institute Setup surface.

| Area | Owns | Primary source |
|---|---|---|
| Initial setup | Profile, academic foundation, optional staff and audited completion | `TenantSettings/InitialSetup/**` |
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
| Initial setup progress | Curated in this module doc set |

## Initial setup rules

Each institute has one tenant-scoped progress aggregate.
Its current step, furthest step, draft, selected year/division, concurrency stamp and completion audit persist on the backend.
Steps are Welcome (0), Profile (1), Academic year (2), Structure (3), Users (4), Review (5) and Complete (6).

| Rule | Behavior |
| --- | --- |
| Draft versus confirmation | Draft saves may be incomplete; profile, year and structure saves validate real records before confirming a step |
| Revisiting earlier steps | Preserve later work; mark changed confirmed sections as pending until saved again |
| Changing year | Clear the selected division and require a structure for the new year |
| Academic foundation | Require an ordered year and a tenant-coherent board / medium / stage / class / positive-capacity division |
| Initial administrator | Reuse the administrator provisioned with the tenant; completion requires an active current administrator |
| Additional staff | Optional, requires the exact Identity User Create permission; limited to 32 setup-created summaries |
| Credentials | Passwords are validated by Identity and excluded from drafts, responses and setup-user input auditing |
| Retry | User request IDs prevent duplicate creation; reuse with a different username/email is rejected |
| Concurrent editing | A stale stamp returns a conflict and cannot overwrite another administrator's work |
| Completion | Require review, confirmed changes and valid real configuration; record completion time and user; repeated completion is idempotent |

New host tenant provisioning creates blank setup progress without demo academic data.
The migration preserves explicit legacy completion and operational institutes without a legacy marker when a complete tenant-owned academic branch exists.
Incomplete legacy markers remain incomplete.
Setup does not restore grants, seed removed permissions or overwrite credentials.
