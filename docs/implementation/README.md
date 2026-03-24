---
description: Index of backend implementation guides (HR, security).
tags: [implementation, docs, hr]
---

# Implementation guides

Deep-dive documentation for **how** HR application-layer refactors and HttpApi migrations were done, plus cross-cutting security notes. For **behavior and API surface**, use [PRDs](../prd/hr/README.md).

## Layout

```
implementation/
├── hr/                 # HR application layer + HttpApi migration notes
└── security/           # Cross-cutting permission / bypass notes
```

## Module index

| Area | Document | Notes |
|------|----------|--------|
| **HR** | [hr/README.md](./hr/README.md) | Application patterns, refactoring log, HttpApi migration |
| **Security** | [security/admin-bypass-permissions.md](./security/admin-bypass-permissions.md) | Admin bypass / permission examples |

## Architecture decisions (EF, caching, payroll model)

Recorded as ADRs (not step-by-step implementation folders):

- [Architecture decisions index](../architecture/decisions/README.md)

## Conventions

When adding guides:

1. Place them under the folder that owns the code (`hr/`, `security/`, etc.).
2. Prefer a local `README.md` as the index for that folder.
3. Link to [PRDs](../prd/hr/README.md) for endpoints and behavior instead of duplicating tables.
4. Follow [docs/best-practices.md](../best-practices.md) for layering and structure.

## Related

- [API documentation](../api/README.md)
- [Main documentation hub](../README.md)

[Back to docs home](../README.md)
