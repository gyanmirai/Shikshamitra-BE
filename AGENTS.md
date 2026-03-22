# Agent / contributor guide

**ShikshaMitra** is a multi-tenant **school management / CRM backend** built with **ABP Framework** and **.NET**.

## Start here

1. **[docs/README.md](docs/README.md)** -- documentation map and folder taxonomy.
2. **[docs/best-practices.md](docs/best-practices.md)** -- canonical engineering rules (DDD layers, folders, permissions). Prefer linking here instead of duplicating standards.

## Task routing

| Task | Location |
|------|----------|
| HR behavior specs (PRD) & API contracts | `docs/prd/hr/` |
| Employee PRD & endpoints | `docs/prd/hr/employees/` |
| Calendar PRD & endpoints | `docs/prd/hr/calendar/` |
| Attendance & leave PRD & endpoints | `docs/prd/hr/attendance-leaves/` |
| Payroll operations PRD & endpoints | `docs/prd/hr/payroll/` |
| Payroll settings PRD & endpoints | `docs/prd/hr/payroll/settings/` |
| Reports PRD & endpoints | `docs/prd/hr/reports/` |
| Architecture decisions (ADRs: eager loading, caching, payroll model) | `docs/architecture/decisions/` |
| HR application-layer patterns & refactor notes | `docs/implementation/hr/` |
| Rooms / buildings API docs | `docs/api/rooms/` |
| UserScope & bulk scope assignment | `docs/platform/` |
| Multi-tenancy (tenants, roles) | `docs/multi-tenancy/` |
| Testing guides | `docs/testing/` |
| Historical doc moves / old structure notes | `docs/meta/` -- **not** current architecture |

When in doubt, search `docs/` for the module name and open the nearest `README.md`.
