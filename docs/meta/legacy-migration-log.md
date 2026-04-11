# Legacy Migration Log

This log records strict cleanup decisions for legacy documentation surfaces.

| Source | Action | Target | Rationale |
|---|---|---|---|
| `docs/prd/hr/**` | ARCHIVE (remove from active tree) | Canonical retained in `docs/modules/hr/prd/**` | Content is duplicate of module PRD files (hash match), not needed in legacy path. |
| `docs/api/rooms/RoomsManagement.md` | MIGRATE | `docs/modules/institute-setup/contracts/raw/rooms-management-api.raw.2026-03-27.md` + curated contract | High-value endpoint/domain context moved to module contracts. |
| `docs/api/rooms/RoomManagement-QuickRef.md` | MIGRATE | `docs/modules/institute-setup/implementation/rooms-management-quick-reference.md` | High-value quick implementation context moved to module implementation docs. |
| `docs/api/endpoints.md` | ARCHIVE (remove from active tree) | Contracts indexed via `docs/modules/*/contracts/curated/README.md` | Legacy centralized endpoint index replaced by module-first contract indexes. |
| `docs/modules/hr.md` | ARCHIVE (remove from active tree) | `docs/modules/hr/README.md` | Redundant pointer file after module index stabilized. |
| `docs/HR-Employee-*.md` (root pointers) | ARCHIVE (remove from active tree) | `docs/meta/legacy/hr-root/**` | Root-level module pointers violate strict module-only policy; archive copies preserved. |
| `docs/meta/legacy/hr-root/employee-personal-info-api.md` | MIGRATE (note only) | `docs/modules/hr/contracts/curated/employees-api.md` | Useful warning that old export mismatched backend; added concise clarification to curated contract. |
| `docs/meta/legacy/hr-root/employee-status.md` | MIGRATE (note only) | `docs/modules/hr/contracts/curated/employees-api.md` | Useful warning to prioritize current PRD/contract source; captured as legacy clarification note. |
| `docs/meta/legacy/hr-root/employee-salary-structure.md` | MIGRATE (note only) | `docs/modules/hr/contracts/curated/payroll-operations-api.md` | Useful warning about stale salary structure export; captured in curated payroll operations contract. |
| `docs/meta/legacy/hr-root/employee-family-info.md` | ARCHIVE | `docs/meta/legacy/hr-root/employee-family-info.md` | Mostly outdated export schema dump; no unique stable rule needed in canonical docs. |
