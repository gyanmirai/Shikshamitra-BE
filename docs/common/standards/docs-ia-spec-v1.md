# Docs IA Spec v1

Canonical module-owned documentation layout:

```text
docs/
  README.md
  best-practices.md
  architecture/
  common/
  modules/
  meta/
```

Canonical module layout:

```text
docs/modules/<module>/
  README.md
  prd/
  contracts/
    raw/
    curated/
  implementation/
  testing/
```

Rules:
- Module docs live in module folders.
- Cross-cutting docs live under `docs/common/`.
- No root-level API folder is used; API contracts are module-owned.
