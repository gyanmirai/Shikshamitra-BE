---
module: institute-setup
domain: product
content_type: Conceptual
status: canonical
updated_on: 2026-10-15
owner: backend-team
audience: humans-and-agents
---

# Understand Institute Setup product behavior

TL;DR: Institute Setup owns durable first-login configuration and Rooms Management.
Rooms Management stores buildings, floors, room types, and rooms used by academic scheduling and operations.

## School Identity

School Identity has no Delete permission or profile deletion endpoint. Its action catalogue contains View, ManageDefaults, ManageOverrides and AccessSignature. The shared permission editor keeps Delete disabled by design; this is working as intended. Use configuration updates to hide an asset or reset an override to Inherit. These changes preserve revision history and retained files referenced by issued documents.

School defaults supply logos, letterheads and principal signatures across one tenant. Board and board/medium profiles override individual assets. Resolution is medium override, board override, then school default. Hidden stops inheritance; resetting to Inherit restores it. Signature image, principal name and designation resolve as one selection.

Defaults are independent of academic year. Scoped profiles follow year-specific boards. Explicit cross-year copying maps one source to one target of the same level, preserves custom/hidden/inherited choices and reuses immutable files. Default changes do not replace custom overrides.

Management scope must cover the whole selected board or medium. A user assigned only one child stage can read inherited identity, but cannot change assets for all other stages in its medium. Read responses expose that restriction as a capability.

PNG/JPEG content is decoded and validated. Limits are 2 MB logo, 5 MB banner and 1 MB signature, with bounded dimensions/pixel count. Files remain drafts until saved configuration or templates reference them. Hourly cleanup reserves unretained drafts older than seven days atomically. Retained assets are never cleanup candidates; concurrent retention of a reserved draft is rejected.

Certificates and issued student ID cards capture asset IDs and signer details in their existing snapshots. Reprints use those snapshots. Timetables, staff cards and academic PDF exports resolve their context; multi-context exports use common board or school defaults. Ordinary reports do not receive principal signatures. A banner replaces the generated school letterhead.

Templates can inherit, use custom assets or omit supported assets. Existing custom certificate values and staff-logo references stay intact until replaced. Certificate options merge per asset; redacted signatures survive unrelated edits. Historical documents without identity snapshots retain their original behavior.

Legacy tenant Logo, Banner and PrincipleSign images are imported idempotently by the data migrator when no school-default profile exists. Original properties are preserved. Invalid assets remain for administrator review; imported principal details require review. Migration does not grant or restore permissions.

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
| Shift Management | Tenant isolation rules documented below |
| Initial setup progress | Curated in this module doc set |

## Shift ownership

Shifts belong to the active server tenant. Schools cannot list, read, change, delete or assign another school's shift. Standard and employee writes validate ownership before accepting a shift reference. Null shift references remain optional.

Migration `20261014063540_AddShiftTenantIsolation` adds nullable ownership and its index. The old aggregate had neither tenant ownership nor creator audit fields. Existing employee and class references could have been created through the leak, so the migration does not infer an owner from them. Legacy rows and references are preserved with null ownership and are hidden from schools until an operator verifies ownership and performs an approved audited repair. Do not reseed or assign all rows to DDVS as a repair.

Apply the migration before deploying the updated backend. Review unresolved legacy references before resuming shift-dependent attendance and payroll operations.

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
