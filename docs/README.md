---
description: Documentation router for ShikshaMitra backend (developer + AI-agent friendly).
tags: [docs, backend, api-contracts, modules]
---

# ShikshaMitra Backend Documentation

This documentation is backend-first, pattern-driven, and contract-centric.

## Start Here

1. [Best Practices](./best-practices.md) - engineering standards and docs contribution rules.
2. [Modules Index](./modules/README.md) - module-first vertical documentation.
3. [Architecture](./architecture/README.md) - ADRs and design decisions.
4. [Common](./common/README.md) - shared platform, tenancy, security, testing, and documentation standards.

## Information Architecture

- `docs/modules/<module>/` is the canonical home for module-owned docs.
- `docs/modules/<module>/contracts/curated/` is the canonical home for AI/dev contract consumption.
- `docs/modules/<module>/contracts/raw/` stores downloaded/generated raw contract files.
- Module contracts live only under `docs/modules/<module>/contracts/`.
- Shared cross-cutting docs live only under `docs/common/`.

## Quick Links

- [HR Module](./modules/hr/README.md)
- [Institute Setup Module](./modules/institute-setup/README.md)
- [HR PRD](./modules/hr/prd/README.md)
- [HR Curated Contracts](./modules/hr/contracts/curated/README.md)
- [Common Docs](./common/README.md)

## Legacy and Archive

- Obsolete root HR exports were moved to [meta/legacy/hr-root](./meta/legacy/hr-root/).
- Historical reorg notes remain under [meta](./meta/README.md).
- Cleanup decisions are tracked in [meta/legacy-migration-log.md](./meta/legacy-migration-log.md).
