# Postman Assets

Canonical Postman layout for ShikshaMitra.

## Active Files

### Collections

Import collections from `postman/collections/`:

| Collection | File | Purpose |
|---|---|---|
| Main | `postman/collections/ShikshaMitra.postman_collection.json` | Authentication, tenant, user, role, and baseline UserScope flows |
| HR Module | `postman/collections/HR-Module.postman_collection.json` | HR domain testing |
| UserScope API | `postman/collections/UserScope-API.postman_collection.json` | Advanced UserScope and permission testing |
| Rooms Management | `postman/collections/RoomsManagement.postman_collection.json` | Institute setup rooms/buildings APIs |
| Class Management | `postman/collections/ClassManagement.postman_collection.json` | Class, teacher assignment, and academic workflows |

### Environment

Use the single active environment:

- `postman/environments/ShikshaMitra.postman_environment.json`

## Quick Start

1. Import one collection from `postman/collections/`.
2. Import `postman/environments/ShikshaMitra.postman_environment.json`.
3. Select the imported environment in Postman.
4. Update core values such as `base_url`, `tenant_id`, `username`, and `password`.
5. Run the collection's `Authentication` request first.

## Folder Rules

- `postman/collections/` contains only active importable collection JSON files.
- `postman/environments/` contains only active environment JSON files.
- `postman/archive/` contains legacy guides, helper SQL, and deprecated environment files kept only for reference.
- Archived files are not the primary source of truth for onboarding or day-to-day testing.

## Archive

Reference-only material lives under:

- `postman/archive/docs/`
- `postman/archive/environments/`
- `postman/archive/sql/`
