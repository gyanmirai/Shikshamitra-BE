# Agent / contributor guide

**ShikshaMitra** is a multi-tenant **school management / CRM backend** built with **ABP Framework** and **.NET**.

## Start here

1. **[docs/README.md](docs/README.md)** — documentation map and folder taxonomy.
2. **[docs/best-practices.md](docs/best-practices.md)** — canonical engineering rules (DDD layers, folders, permissions). Prefer linking here instead of duplicating standards.

## Task routing

| Task | Location |
|------|----------|
| Employee HTTP API reference, quick ref | `docs/api/employee/` |
| Employee payroll endpoints | `docs/api/employee/Employee-Payroll-API.md` |
| Employee optimization / “why we did X” summaries | `docs/architecture/employee/` |
| Employee backend implementation (EF, caching, eager loading, additional details) | `docs/implementation/employee/` |
| Payroll master APIs (earning/deduction components) | `docs/api/payroll/` |
| HR feature implementation (pagination, leave, components, permissions) | `docs/implementation/hr/` |
| Rooms / buildings API docs | `docs/api/rooms/` |
| UserScope & bulk scope assignment | `docs/platform/` |
| Multi-tenancy (tenants, roles) | `docs/multi-tenancy/` |
| Testing guides | `docs/testing/` |
| Historical doc moves / old structure notes | `docs/meta/` — **not** current architecture |

When in doubt, search `docs/` for the module name and open the nearest `README.md`.
