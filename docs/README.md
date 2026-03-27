---
description: Main documentation hub for ShikshaMitra backend (ABP .NET school CRM).
tags: [docs, abp, multi-tenant, hr]
---

# ShikshaMitra Documentation

Multi-tenant school management system built with ABP Framework (.NET 9).

## Structure

```
docs/
├── prd/                 → PRD & API contracts (behavior specs + endpoint reference)
├── api/                 → API reference (rooms, general endpoints)
├── architecture/        → Cross-module design & optimization narratives
├── implementation/      → How features are built (EF, caching, migrations)
├── platform/            → UserScope & bulk scope assignment
├── multi-tenancy/       → Tenants, roles
├── testing/             → Test guides and summaries
├── modules/             → Module overviews (e.g. HR)
└── meta/                → Historical reorg notes (not source of truth)
```

## Quick Start

### API Testing

The [postman/](../postman/) folder contains collections and environments:

- `ShikshaMitra.postman_collection.json`
- `ShikshaMitra.postman_environment.json`
- [Setup guide](../postman/README.md)

### Development

```bash
dotnet restore && dotnet build
dotnet run --project src/ShikshaMitra.DbMigrator
dotnet run --project src/ShikshaMitra.HttpApi.Host
```

## Core concepts

### Architecture and best practices

- [Project best practices](./best-practices.md) — canonical rules for layers, naming, and patterns (link here instead of copying).

### Module documentation

- [HR module](./modules/hr.md) -- technical reference (entities, domain services, repositories)
- [HR PRD & API Contracts](./prd/hr/README.md) -- behavior specs and endpoint reference for all HR submodules

### UserScope

Hierarchical access: Board → Medium → Stage → Class → Division. Principle: `NULL = ALL`.

- [UserScope usage](./platform/UserScope-Usage.md)

### Multi-tenancy

- [Tenant setup](./multi-tenancy/tenants.md)
- [Role configuration](./multi-tenancy/roles.md)

### Authentication

- Host admin: tenant creation
- Tenant user: day-to-day operations
- [Postman architecture notes](../postman/ARCHITECTURE.md)

## Documentation map

| Topic | Document |
|-------|----------|
| **Standards** | |
| Best practices | [best-practices.md](./best-practices.md) |
| **PRD & API contracts** | |
| HR PRD index | [prd/hr/README.md](./prd/hr/README.md) |
| Employees | [prd/hr/employees/](./prd/hr/employees/prd.md) |
| Employees API contract | [prd/hr/employees/api-contract.md](./prd/hr/employees/api-contract.md) |
| Calendar | [prd/hr/calendar/](./prd/hr/calendar/prd.md) |
| Attendance & Leaves | [prd/hr/attendance-leaves/](./prd/hr/attendance-leaves/prd.md) |
| Payroll Operations | [prd/hr/payroll/](./prd/hr/payroll/prd.md) |
| Payroll Settings | [prd/hr/payroll/settings/](./prd/hr/payroll/settings/prd.md) |
| Reports | [prd/hr/reports/](./prd/hr/reports/prd.md) |
| **API reference (other)** | |
| API hub | [api/README.md](./api/README.md) |
| Rooms / facilities | [api/rooms/README.md](./api/rooms/README.md) |
| General endpoints | [api/endpoints.md](./api/endpoints.md) |
| **Architecture** | |
| Architecture hub | [architecture/README.md](./architecture/README.md) |
| **Architecture decisions (ADRs)** | |
| ADR index | [architecture/decisions/README.md](./architecture/decisions/README.md) |
| Employee eager loading | [architecture/decisions/001-employee-eager-loading.md](./architecture/decisions/001-employee-eager-loading.md) |
| Employee caching | [architecture/decisions/002-employee-caching-strategy.md](./architecture/decisions/002-employee-caching-strategy.md) |
| Payroll two-tier model | [architecture/decisions/003-payroll-two-tier-architecture.md](./architecture/decisions/003-payroll-two-tier-architecture.md) |
| **Implementation guides** | |
| HR implementation notes | [implementation/hr/README.md](./implementation/hr/README.md) |
| HR Application layer patterns | [implementation/hr/application-layer-patterns.md](./implementation/hr/application-layer-patterns.md) |
| HR Application refactoring log | [implementation/hr/application-layer-refactoring.md](./implementation/hr/application-layer-refactoring.md) |
| HR HttpApi migration | [implementation/hr/httpapi-migration.md](./implementation/hr/httpapi-migration.md) |
| All implementation areas | [implementation/README.md](./implementation/README.md) |
| **Platform** | |
| UserScope | [platform/UserScope-Usage.md](./platform/UserScope-Usage.md) |
| Bulk scope assignment | [platform/BulkScopeAssignment-API-Guide.md](./platform/BulkScopeAssignment-API-Guide.md) |
| **Multi-tenancy** | |
| Tenants | [multi-tenancy/tenants.md](./multi-tenancy/tenants.md) |
| Roles | [multi-tenancy/roles.md](./multi-tenancy/roles.md) |
| **Testing** | |
| Postman | [../postman/README.md](../postman/README.md) |
| Test docs | [testing/README.md](./testing/README.md) |
| **Meta** | |
| Doc history / old layouts | [meta/README.md](./meta/README.md) |

## Documentation types

### `docs/prd/`

Behavior specifications (PRD) and API contracts per module. Each submodule has a `prd.md` (business rules, permissions, error codes) and `api-contract.md` (endpoints, request/response shapes). The source of truth for HR API surface. After building `ShikshaMitra.HttpApi.Host`, OpenAPI JSON is emitted under `src/ShikshaMitra.HttpApi.Host/obj/**/EndpointInfo/` (use Swagger UI at runtime for the live schema).

**Obsolete:** [HR-Employee-PersonalInfoAPI.md](./HR-Employee-PersonalInfoAPI.md) — replaced by the employees PRD contract above.

### `docs/api/`

Remaining API reference (rooms, general endpoints). HR-specific docs have moved to `docs/prd/hr/`.

### `docs/architecture/`

Architecture decision records ([decisions/README.md](./architecture/decisions/README.md)) and any module-level design notes.

### `docs/implementation/`

Step-by-step or deep technical guides for backend work (EF Core, caching, migrations, module features).

### `docs/platform/`

UserScope and related APIs that span modules.

### `docs/meta/`

Historical reorganization notes. Use [docs/README.md](./README.md) for the current taxonomy.

## SSVM tenant (example)

```
ID: 3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e
User: admin
Pass: Temp@123
```

## Quick links

### Developers

- [HR PRD & API contracts](./prd/hr/README.md)
- [API endpoints](./api/endpoints.md)
- [Implementation guides](./implementation/README.md)

### Architects

- [Architecture decisions (ADRs)](./architecture/decisions/README.md)
- [Multi-tenancy](./multi-tenancy/README.md)
- [UserScope](./platform/UserScope-Usage.md)

### Project / product

- [HR PRD index](./prd/hr/README.md)
- [HR implementation index](./implementation/hr/README.md)

---

**.NET 9** | **ABP Framework** | **EF Core** | **PostgreSQL**
