---
description: Index of backend implementation guides (HR, employee, security).
tags: [implementation, docs, hr]
---

# Implementation guides

Step-by-step and deep-dive documentation for **how** features are built: EF Core, application services, permissions, pagination, and payroll-related subdomains.

## Layout

```
implementation/
├── employee/           # Employee entity: caching, eager loading, additional details
├── hr/                 # HR sub-features (components, leave, pagination, permissions)
│   ├── deduction-components/
│   ├── earning-components/
│   ├── leave-management/
│   ├── pagination/
│   └── permissions/
└── security/           # Cross-cutting permission / bypass notes
```

## Module index

| Area | README | Notes |
|------|--------|--------|
| **Employee** | [employee/README.md](./employee/README.md) | Eager loading, caching, additional details; payroll → API doc + source |
| **HR (payroll components, leave, etc.)** | [hr/README.md](./hr/README.md) | Entry point for all `hr/` subfolders |
| **HR — earning components** | [hr/earning-components/README.md](./hr/earning-components/README.md) | |
| **HR — deduction components** | [hr/deduction-components/README.md](./hr/deduction-components/README.md) | |
| **HR — leave management** | [hr/leave-management/README.md](./hr/leave-management/README.md) | |
| **HR — pagination** | [hr/pagination/README.md](./hr/pagination/README.md) | |
| **HR — permissions** | [hr/permissions/README.md](./hr/permissions/README.md) | |
| **Security** | [security/admin-bypass-permissions.md](./security/admin-bypass-permissions.md) | Admin bypass / permission examples |

## Conventions

When adding guides:

1. Place them under the **module folder** that owns the code (`employee/`, `hr/...`, etc.).
2. Prefer names: `Module-Feature-Implementation.md`, `Implementation-Complete.md`, `Quick-Start.md`, or a local `README.md` as the index.
3. Link to **API** docs under `docs/api/` instead of duplicating endpoint tables.
4. Follow [docs/best-practices.md](../best-practices.md) for layering and structure.

## Related

- [API documentation](../api/README.md)
- [Main documentation hub](../README.md)

[Back to docs home](../README.md)
