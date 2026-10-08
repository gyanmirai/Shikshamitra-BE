---
description: Documentation router for ShikshaMitra backend (developer + AI-agent friendly).
tags: [docs, backend, api-contracts, modules]
---

# Find backend documentation

This documentation is backend-first, module-first, and agent-friendly.

## Start Here

1. [Best Practices](./best-practices.md) - engineering standards and docs contribution rules.
2. [Modules Index](./modules/README.md) - module-first vertical documentation.
3. [Architecture](./architecture/README.md) - ADRs and design decisions.
4. [Common](./common/README.md) - shared platform, tenancy, security, testing, and documentation standards.

## Information Architecture

- `docs/modules/<module>/` is the canonical home for module-owned docs.
- New or refactored modules follow [Module Docs Standard](./common/standards/module-docs-standard.md).
- `docs/modules/<module>/api.md` is the canonical home for module API behavior.
- Active module docs use six canonical files and optional `references/`.
- Generated exports and old plans do not belong in `docs/`.
- Shared cross-cutting docs live only under `docs/common/`.

## Quick Links

- [HR Module](./modules/hr/README.md)
- [Examination Module](./modules/examination/README.md)
- [Institute Setup Module](./modules/institute-setup/README.md)
- [Host Tenant Management](./modules/host-tenants/README.md)
- [Module Docs Standard](./common/standards/module-docs-standard.md)
- [HR product](./modules/hr/product.md)
- [HR API](./modules/hr/api.md)
- [Common docs](./common/README.md)
