---
description: API documentation index for ShikshaMitra (REST, modules, Postman).
tags: [api, docs, rest]
---

# API documentation

REST API documentation for ShikshaMitra.

## Documentation organization

### Module documentation

- **[Employee](./employee/)** — CRUD, profile, documents, additional details, payroll endpoints  
  - [Payroll API](./employee/Employee-Payroll-API.md)  
  - Optimization narratives: [architecture/employee/](../architecture/employee/)  
  - Backend implementation notes: [implementation/employee/](../implementation/employee/)

- **[Payroll (master data)](./payroll/)** — Earning and deduction component APIs  
  - [Earning Components](./payroll/Earning-Components-API.md)  
  - [Deduction Components](./payroll/Deduction-Components-API.md)

- **[Rooms / facilities](./rooms/)** — Buildings, room types, rooms

### General reference

- **[endpoints.md](./endpoints.md)** — Endpoint listing

### Implementation (backend)

- **[implementation/](../implementation/)** — EF Core, caching, HR submodule guides (not a substitute for this API reference)

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

- **Authentication** — login and tokens  
- **Tenants** — host admin  
- **Users / Roles** — tenant scope  
- **UserScope** — scope assignment (see [platform docs](../platform/))

### HR

- **Employees** — [employee/](./employee/)  
- **Payroll master data** — [payroll/](./payroll/)

### Planned / other product areas

Student management, academic setup, and broader payroll/payslip flows may expand over time; check `endpoints.md` and OpenAPI for the latest surface.

## Start here

### General API usage

1. [endpoints.md](./endpoints.md)  
2. [Postman README](../../postman/README.md)

### Employee module

1. [Employee README](./employee/README.md)  
2. [Employee quick reference](./employee/Employee-QuickRef.md)  
3. [Payroll API](./employee/Employee-Payroll-API.md)  
4. [Implementation guides](../implementation/employee/README.md)  
5. [Optimization summary](../architecture/employee/Employee-Complete-Optimization-Summary.md)

### By role

- **Backend** — [implementation/](../implementation/README.md) + [best practices](../best-practices.md)  
- **Frontend** — this `api/` tree and `endpoints.md`  
- **Architect** — [architecture/](../architecture/employee/) for employee patterns  

---

## Featured: employee module optimizations

Patterns documented for Employee (caching, eager loading, indexes) apply to other modules:

```
Before → After (example)
├─ Queries: 301+ → 1 (99.7% reduction)
├─ Memory: 500MB → 1MB (99% reduction)
├─ Response: 3000ms → 150ms (95% faster)
└─ DB load: 1500/min → 150/min (90% reduction)
```

---

[Back to main docs](../README.md)
