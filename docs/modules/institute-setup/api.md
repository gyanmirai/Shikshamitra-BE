---
module: institute-setup
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-21
owner: backend-team
audience: humans-and-agents
---

# Find Institute Setup backend APIs

TL;DR: Institute Setup APIs cover initial progress, completion, buildings, room types, and rooms.
Use controllers for route truth and contracts for request and response shapes.

## Reports

Base route: `/api/app/institute-setup-report`. `InstituteReportController` delegates to `IInstituteReportAppService`; application methods enforce access for direct calls too.

| Method / suffix | Purpose | Report permission |
| --- | --- | --- |
| GET `options` | Permitted catalogue and scoped filter choices | View |
| POST `preview` | Current data, 1-based page and pageSize 1-100 | View |
| POST `exports` | Queue normalized request | View + Add |
| GET `exports` | Current user's history | View |
| GET `exports/{jobId}` | Job metadata and saved request | View |
| GET `exports/{jobId}/download` | Authenticated file bytes | View |
| POST `ai/plan` | Clarification or validated plan and approval token | View + Add |
| POST `ai/preview`, `ai/exports` | Preview/queue exact approved plan | View + Add |

All operations also enforce applicable source View permissions. `InstituteReportRequest` contains reportType, format (`pdf`, `excel`, `csv`), filters, columns, conditions, sortField, descending and preview pagination. Report IDs are `academic-hierarchy`, `class-division-summary`, `division-capacity`, `rooms-inventory`, `shift-register`. Academic filters follow year/board/medium/stage/standard/division; class summaries omit the division filter and expose numeric `divisionCount` plus `divisions`. For classes with exactly one permitted division, use a `divisionCount` condition with operator `eq` and value `"1"`. Physical filters are building/floor/roomType; shift and roomAssignment apply only to their supported reports. Conditions are ANDed and allow eq/contains plus numeric gt/lt. Unsupported source, fields, operators and unrelated filters are rejected.

AI requests contain a 10-2000 character prompt, optional current academic year and up to six clarification answers. Approved calls contain `{ plan, approvalToken }`. Error families use `InstituteReports:*`; `AiInvalidResponse` identifies unusable provider output and `AiResponseIncomplete` identifies truncated output. Institute Setup owns response parsing and never emits Curriculum planner errors. Unavailable files and expired approval produce actionable messages. Downloads do not expose blob keys. The new additive `AppInstituteReportJobs` migration must precede deployment.

`shift-register` supports columns `shift`, `start`, `end` and calculated `totalHours`. Include `totalHours` in `columns` to request it, or leave `columns` empty for all four. `totalHours` supports numeric `eq`, `gt`, `lt` conditions and sorting. It contains decimal hours rounded to two places; next-day end times wrap across midnight and equal times produce zero, without break deductions. Unknown computed fields and formula expressions are rejected. Existing saved requests with explicit three-column selections retain that selection; generate a new plan to add hours.

Additional report datasets:

| Report ID | Source grants in addition to Reports View/Add as applicable | Data |
| --- | --- | --- |
| `school-document-register` | `InstituteSetup.SchoolDocuments.View` | Active entry and attachment metadata |
| `school-document-counts` | School Documents View | Category counts, attachment counts and bytes, plus All categories total |
| `school-identity-register` | `InstituteSetup.SchoolIdentity.View` | Scoped saved logo/letterhead configuration |
| `school-identity-counts` | School Identity View | Saved profile/custom-selection counts, plus All scopes total |
| `school-identity-signatures` | School Identity View and `InstituteSetup.SchoolIdentity.AccessSignature` | Scoped saved signature selection, principal name and designation |

Options include `requiredPermissions` on each definition and string filter `values`. `filters.documentCategory` accepts `GovernmentApproval`, `PermissionNoc`, `BoardAffiliation`, `Registration`, `Other` only for document reports. `filters.identityScope` accepts `school`, `board`, `medium`, `organization` only for identity reports. Identity reports optionally accept academicYearId/boardId/mediumId/stageId, where stageId selects the identity organization; standard/division filters are rejected. File contents, blob keys, image data and signature fields outside the signature report cannot be selected. Counts and byte totals support numeric filtering/sorting.

## School Documents

Base: `/api/institute-setup/school-documents`. Tenant context comes from the authenticated server context; payloads do not select a tenant. DTOs live under `Application.Contracts/TenantSettings/SchoolDocuments`, and multipart binding is in `HttpApi/Models/SchoolDocumentWriteRequest.cs`.

| Method / suffix | Contract and permissions |
| --- | --- |
| `GET /` | View; `search` (name, up to 200 characters), optional numeric `category`, `skipCount`, `maxResultCount` (capped at 100), `sorting` (`name asc/desc` or `creationTime asc/desc`). Newest first by default; returns `items`/`totalCount`. |
| `GET /{id}` | View; metadata, creator username snapshot, creation time, current `concurrencyStamp`, and attachment metadata. No blob keys or public URLs. |
| `POST /` | View + Add; multipart `name`, `category`, optional `notes`, and repeated `files`. At least one file required. Returns saved record. |
| `PUT /{id}` | View + Add; same fields, required current `concurrencyStamp`, repeated `retainedFileIds`, and optional new `files`. Omitting a saved ID removes that attachment and additionally requires Delete. Total retained/new count must remain 1-5. |
| `DELETE /{id}` | View + Delete; soft deletes record and attachments; 204. |
| `GET /{id}/files/{fileId}` | View; authenticated PDF/image bytes for that record only. `download=true` returns attachment disposition with the original safe filename. Private/no-store and nosniff headers. |

Action names are `InstituteSetup.SchoolDocuments.View`, `.Add` and `.Delete`; visibility is `ShikshaMitra.Module.InstituteSetup.SchoolDocuments`. Categories are stable numeric values: 0 Government Approval, 1 Permission / NOC, 2 Board Affiliation, 3 Registration, 4 Other.

Validation errors return 400, missing actions or host context 403, missing/cross-tenant/deleted records or mismatched file parents 404, and stale stamps 409. Upload endpoints accept at most 30 MiB including multipart overhead; each file remains limited to 5 MiB. Align reverse-proxy request limits with this endpoint. `ShikshaMitra:SchoolDocuments:InvalidDetails`, `InvalidFiles` and `InvalidFile` are localized business codes.

## Academic Setup validation

Standards use the ABP routes `POST /api/app/standard` and `PUT /api/app/standard/{id}`. Duplicate names within the same medium stage return HTTP 409 with the existing ProblemDetails format: `errorCode: ShikshaMitra.TenantSettings:DuplicateStandardName`, `detail: A standard named 'Class 3' already exists in this academic stage.`, and `data.name`. Names are compared literally after trimming and case normalization; editing excludes the current standard. Repository tenant filters remain enabled. The same name is allowed in another stage.

Division create/update routes remain `/api/app/division` and `/api/app/division/{id}`. Omitting or passing null for `roomNumber` leaves existing allocations unchanged on update and creates an unassigned division on create. An explicitly supplied empty string clears the room. Academic hierarchy forms no longer expose room allocation; no schema change is required.

Division creation needs only `name`, `strength` (a whole number from 1 to 500), and `standardId`. Neither a room nor a shift is required. Within the current tenant and standard, names are trimmed and compared case-insensitively as literal text. Duplicate creation or renaming returns HTTP 409 with `errorCode: ShikshaMitra.TenantSettings:DuplicateDivisionName` and `detail: A division named '{name}' already exists in this standard.` Editing excludes the current record, including case-only name changes. No database migration is needed for this validation change.

## Shift Management API

ABP conventionally exposes `ShiftManagementAppService` at `/api/app/shift-management`: GET/POST collection and GET/PUT/DELETE `/{id}`. Its DTOs contain name and start/end times; tenant ownership comes from the authenticated server context, never a client payload. Authentication is required. The existing action-permission model is unchanged.

Lists and their total counts use the current tenant. Foreign and host-only IDs return 404 to school callers for reads, updates and deletes. Standard and employee writes reject foreign shift references with `HR:Attendance:020310`. Equal or overnight time ranges and deletion of assigned shifts remain rejected.

## School Identity API

The base route is `api/institute-setup/school-identity`. Configuration operations explicitly check actions in the application service. Host identities are rejected; board/medium/organization references must belong to the current tenant and match their parent hierarchy. `organizationId` identifies an education medium stage and requires both `boardId` and `mediumId`.

| Method and suffix | Permission and contract |
| --- | --- |
| GET collection | `InstituteSetup.SchoolIdentity.View`; optional boardId/mediumId/organizationId, choices, stamp, effective assets, sources and canManage capability |
| PUT collection | View plus ManageDefaults or ManageOverrides; choices and concurrencyStamp |
| POST assets | Corresponding management grant; multipart file/kind/context; immutable asset ID and image metadata |
| GET assets/{id} | View, authorized origin/effective context or the caller's draft; signatures additionally require AccessSignature |
| POST preview | View; draft previews additionally require management; returns PDF without saving |
| GET history | View; latest 100 revisions with tenant-owned creator usernames; signature selections redacted without AccessSignature |
| POST copy | View, ManageOverrides and AccessSignature; explicit source/target, differing years, matching levels and target stamp |
| GET branding | Authenticated current-school defaults; only nullable logo/banner metadata containing id, contentType, width and height |
| GET branding/logo | Authenticated school-default logo bytes, or 204 when absent/hidden |
| GET branding/banner | Authenticated school-default banner bytes, or 204 when absent/hidden |

Branding images use `Cache-Control: private, no-cache`, tenant-and-asset-specific ETags, and `Vary: Authorization, Cookie, __tenant`.
Every conditional request authenticates and resolves the current tenant before returning 304 for an unchanged image.
Replacing an image changes its immutable asset ID and ETag.
Metadata and empty image responses use `private, no-store`.
Numeric branding routes and signature branding routes do not exist.

Action prefixes are `InstituteSetup.SchoolIdentity`; navigation visibility is separately `ShikshaMitra.Module.InstituteSetup.SchoolIdentity`. ManageDefaults additionally requires tenant-wide data scope. Resetting choices records a revision; there is no Delete permission.

Override writes require full scope over the selected board, medium or organization. Descendant-only scope permits inheritance reads, not changes to a whole parent profile. Organization context also applies to multipart uploads, history, downloads and previews. Copy accepts optional sourceOrganizationId/targetOrganizationId; both contexts must have the same override level. The read response's canManage combines exact action permission and full-context scope; it is a UI capability, not a replacement for write authorization. Reusing retained immutable assets checks readable origin scope; attaching an unsaved draft additionally requires management scope at its origin.

Modes are Inherit=0, Custom=1 and Hidden=2. Custom signatures include principalName and designation. An omitted signature choice preserves it for callers without signature access. Missing configuration is optional. Invalid content, sizes, dimensions, asset kinds/references and copy contexts use `SchoolIdentity:*` errors; stale stamps use ABP concurrency errors.

Public admission image routes are `api/public/admission-inquiries/{token}/branding/{kind}` with optional offeringId. They validate the existing link, cycle and permitted offering before resolving its tenant context, allow only logo/banner, and never return signature data.

Staff ID previews read `GET api/employees/i-card/templates/school-logo` with optional mediumStageId. It requires `HR.Employee.ICard.View`, validates tenant and board/medium scope for the stage, and resolves the same shared logo as the exported card. It returns image bytes with the stored content type, or 204 when the effective logo is absent. School Identity configuration grants are not required to view an authorised card preview.

## Initial setup API

All operations use `api/institute-setup/initial` and require authenticated identity whose tenant matches the server's current tenant.
Host identities cannot enter this school API.

| Method and suffix | Input | Behavior |
| --- | --- | --- |
| GET collection | None | Current progress; users without setup grants receive masked draft/user details |
| PUT `draft` | `currentStep`, typed `draft`, `concurrencyStamp` | Persist partial entries within available steps |
| PUT `profile` | Profile fields and stamp | Update the current tenant's whitelisted details and confirm step 1 |
| PUT `academic-year` | Name, start/end dates, optional existingYearId, createNew, stamp | Create, select or update a tenant-owned year and confirm step 2 |
| PUT `structure` | Typed draft and stamp | Create/reuse the academic branch and confirm step 3 |
| PUT `advance` | Step and stamp | Advance only Welcome (0) or optional Users (4) |
| POST `users` | Identity create input, requestId, stamp | Create optional staff through the standard Identity service |
| PUT `complete` | Stamp | Verify readiness and record completion; already complete returns current status |

Writes require both `InstituteSetup.BasicInformation.Edit` and `InstituteSetup.AcademicSetup.Add`.
Mutating existing academic records additionally requires `InstituteSetup.AcademicSetup.Edit`; reusing unchanged records does not.
Creating staff additionally requires `AbpIdentity.Users.Create`, with Identity enforcing role and password rules.
The school session context at `api/host/session-context` exposes `setupCompleted` and `canManageSetup`; its existing host capabilities remain separate.

| Response or error | Meaning |
| --- | --- |
| Progress response | isCompleted, canManageSetup, currentStep, furthestStep, stamp, schoolCode, year/division IDs, completedAt, draft, createdUsers and pendingSteps |
| 401 / 403 | Missing authentication, wrong tenant/host identity or missing exact action permission |
| 404 | Referenced record is not owned by the current tenant |
| 409 | Stale concurrency stamp |
| 400 with `ShikshaMitra:InitialSetup:*` | Invalid order, incomplete profile/structure, unconfirmed edits or invalid request reuse |
| Identity business errors | Password, username, email or role validation failure |

Contracts live in `src/ShikshaMitra.Application.Contracts/TenantSettings/InitialSetup/InitialSetupDtos.cs`.
Localized error codes live in `src/ShikshaMitra.Domain.Shared/TenantSettings/InitialSetup/InitialSetupErrorCodes.cs`.

## Rooms Management endpoint families

Room create/update DTOs accept optional `columnRows`, an ordered array of bench-row counts per column. When supplied, the domain requires 1 to 50 columns, 1 to 100 rows in each column, and 1 to 20 students per bench. It derives `benchesPerColumn` from the array length, `benchesPerRow` from its maximum, `totalBenches` from its sum, and `capacity` from total benches times bench capacity. Older requests may omit the array and keep their rectangular layout contract. Responses return the saved array.

| Feature catalogue operation | Contract | Permission |
| --- | --- | --- |
| `GET /api/institute-setup/rooms/features` | `{ items: [{ id, name }] }` for the current tenant | Room Management View |
| `POST /api/institute-setup/rooms/features` | `{ name }`, trimmed, 1 to 100 characters; returns `{ id, name }` | Room Management Add |

Feature names are unique per tenant after case normalization. Adding an existing name returns the existing entry. Room selections remain string arrays in `features`; saving resolves them to catalogue names and rejects unknown names with `Rooms:UnknownFeature`. Invalid column layouts raise `Rooms:InvalidColumnLayout`. Read and write policies also run on direct room app-service calls.

Apply migration `20261019063242_AddRoomColumnLayoutAndFeatures` before starting the updated API. It adds the layout array, a tenant-owned audited feature catalogue, the ten existing default feature names for existing tenants/host, and valid legacy room feature names. `RoomFeatureDataSeedContributor` seeds missing defaults for newly seeded tenants. Defaults are available choices; rooms do not automatically select every feature.

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
