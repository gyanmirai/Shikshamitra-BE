---
description: Main documentation hub for ShikshaMitra backend (ABP .NET school CRM).
tags: [docs, abp, multi-tenant, hr]
---

# ShikshaMitra Documentation

Multi-tenant school management system built with ABP Framework (.NET 9).

## Structure

```
docs/
├── api/                 → API reference (endpoints, examples, quick refs)
├── architecture/        → Cross-module design & optimization narratives
├── implementation/      → How features are built (EF, caching, migrations)
├── platform/            → UserScope & bulk scope assignment
├── multi-tenancy/       → Tenants, roles
├── testing/             → Test guides and summaries
├── modules/             → Module overviews (e.g. HR)
├── meta/                → Historical reorg notes (not source of truth)
└── improvements/        → One-off fix notes
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

- [HR module](./modules/hr.md) — employees, attendance and leave, payroll, calendar.

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
| **API reference** | |
| API hub | [api/README.md](./api/README.md) |
| Employee API | [api/employee/README.md](./api/employee/README.md) |
| Employee payroll API | [api/employee/Employee-Payroll-API.md](./api/employee/Employee-Payroll-API.md) |
| Payroll components APIs | [api/payroll/README.md](./api/payroll/README.md) |
| Rooms / facilities | [api/rooms/README.md](./api/rooms/README.md) |
| General endpoints | [api/endpoints.md](./api/endpoints.md) |
| **Architecture narratives** | |
| Employee optimizations | [architecture/employee/README.md](./architecture/employee/README.md) |
| Payroll architecture | [architecture/payroll/PAYROLL-ARCHITECTURE.md](./architecture/payroll/PAYROLL-ARCHITECTURE.md) |
| **Implementation guides** | |
| Employee (backend) | [implementation/employee/README.md](./implementation/employee/README.md) |
| HR submodule guides | [implementation/hr/README.md](./implementation/hr/README.md) |
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

### `docs/api/`

Endpoint reference, request/response examples, validation — for API consumers and frontend work.

### `docs/architecture/`

Why and how the system is shaped at a high level; optimization summaries and cross-cutting design.

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

- [Employee API quick reference](./api/employee/Employee-QuickRef.md)
- [Employee implementation guides](./implementation/employee/README.md)
- [API endpoints](./api/endpoints.md)

### Architects

- [Employee optimization summary](./architecture/employee/Employee-Complete-Optimization-Summary.md)
- [Multi-tenancy](./multi-tenancy/README.md)
- [UserScope](./platform/UserScope-Usage.md)

### Project / product

- [Employee API overview](./api/employee/README.md)
- [HR implementation index](./implementation/hr/README.md)

---

**.NET 9** | **ABP Framework** | **EF Core** | **PostgreSQL**
