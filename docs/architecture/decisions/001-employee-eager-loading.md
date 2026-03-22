# ADR-001: Employee aggregate eager loading

## Status

Accepted

## Context

Loading many employees caused an N+1 query pattern: each navigation property loaded in separate round-trips. For ~100 employees the system could issue **301+ queries**, with slow list endpoints and high database load.

## Decision

Use EF Core **explicit relationship configuration** plus a deliberate **AutoInclude** policy on the `Employee` aggregate:

- **AutoInclude** for navigations that are almost always needed for display or authorization: `Role`, `EducationMediumStage`, `Shift`.
- **Do not** AutoInclude `PayrollStructure` (heavier; load only when needed).
- Configure **owned** collections: `WorkExperiences` (JSON column where configured), `Documents` (owned table) per existing `EmployeeConfiguration`.
- Add **indexes** for common filters: `EmployeeCode` (unique), `UserId`, `Email`, `RoleId`, `EducationMediumStageId`, composite `(TenantId, EmployeeStatusType)`.

Use repository methods such as `GetWithDetailsAsync` when optional heavy includes (e.g. payroll structure) are required.

## Consequences

**Positive**

- List/detail paths that hit the hot path can collapse to **a single query** for the aggregate graph (order-of-magnitude fewer round-trips; historical measurement: **301+ queries to 1** for 100 employees in the optimization summary).

**Negative**

- Auto-included navigations are always loaded when the entity is materialized through default queries; slightly higher per-row payload when those navigations are unused.
- Team must keep AutoInclude list in sync with real access patterns.

## Key details

- Implementation lives in `ShikshaMitra.EntityFrameworkCore` employee entity configuration.
- Profile pictures are **not** loaded as byte arrays on the entity; blob names + on-demand download avoid large memory per row.
