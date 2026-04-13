# Docs Quality Checks

Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1
```

Checks:
- broken internal links in active docs
- curated contract naming (`*-api.md`)
- curated contract required frontmatter keys
- curated contract required section order and headings
- root docs whitelist (`architecture`, `common`, `meta`, `modules`, plus root `README.md` and `best-practices.md`)
