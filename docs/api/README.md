---
description: API documentation index for ShikshaMitra (REST, modules, Postman).
tags: [api, docs, rest]
---

# API documentation

REST API documentation for ShikshaMitra.

## Documentation organization

### HR module (PRD + API contracts)

Employee, payroll, calendar, attendance/leave, and report API contracts have moved to the PRD structure:

- **[HR PRD & API Contracts](../prd/hr/README.md)** -- behavior specs and endpoint reference for all HR submodules
  - [Employees](../prd/hr/employees/api-contract.md)
  - [Calendar](../prd/hr/calendar/api-contract.md)
  - [Attendance & Leaves](../prd/hr/attendance-leaves/api-contract.md)
  - [Payroll Operations](../prd/hr/payroll/api-contract.md)
  - [Payroll Settings](../prd/hr/payroll/settings/api-contract.md)
  - [Reports](../prd/hr/reports/api-contract.md)

### Other modules

- **[Rooms / facilities](./rooms/)** -- Buildings, room types, rooms

### General reference

- **[endpoints.md](./endpoints.md)** -- Endpoint listing

### Implementation (backend)

- **[implementation/](../implementation/)** -- EF Core, caching, HR submodule guides

## Quick overview

### Base URL

```
https://localhost:44300
```

### Authentication

JWT in the `Authorization` header:

```
Authorization: Bearer {token}
```

### Tenant context

```
__tenant: {tenant-id}
```

### Testing

See [postman/](../../postman/) for collections and environments.

## API categories

### Core

- **Authentication** -- login and tokens
- **Tenants** -- host admin
- **Users / Roles** -- tenant scope
- **UserScope** -- scope assignment (see [platform docs](../platform/))

### HR

- **All HR APIs** -- [prd/hr/](../prd/hr/README.md)

### Planned / other product areas

Student management, academic setup, and broader payroll/payslip flows may expand over time; check `endpoints.md` and OpenAPI for the latest surface.

## Start here

### General API usage

1. [endpoints.md](./endpoints.md)
2. [Postman README](../../postman/README.md)

### HR module

1. [HR PRD & API Contracts](../prd/hr/README.md)
2. [Implementation guides](../implementation/hr/README.md)
3. [Architecture decisions (ADRs)](../architecture/decisions/README.md)

### By role

- **Backend** -- [implementation/](../implementation/README.md) + [best practices](../best-practices.md)
- **Frontend** -- [HR API Contracts](../prd/hr/README.md) and `endpoints.md`
- **Architect** -- [ADRs](../architecture/decisions/README.md) + [modules/hr.md](../modules/hr.md)

---

[Back to main docs](../README.md)
