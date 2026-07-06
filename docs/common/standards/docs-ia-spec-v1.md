---
module: common
domain: documentation
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Use the documentation information architecture

TL;DR: backend docs are module-first.
New and refactored modules follow the compact module standard.

## Root layout

Use this root layout:

```text
docs/
  README.md
  best-practices.md
  architecture/
  common/
  modules/
```

## Module layout

Use this module layout for new and refactored modules:

```text
docs/modules/<module>/
  README.md
  product.md
  api.md
  architecture.md
  workflows.md
  testing.md
  references/
```

See [module-docs-standard.md](./module-docs-standard.md) for the complete standard and [module-docs-template.md](./module-docs-template.md) for a copyable template.

## Rules

- Module docs live in module folders.
- Cross-cutting docs live under `docs/common/`.
- Canonical API behavior for compact modules lives in `api.md`.
- Generated exports and old plans do not belong in `docs/`.
- Retired module folders such as `prd/`, `contracts/`, `implementation/`, and `testing/` are not allowed in compact modules.
