# Docs quality checks

Run module documentation checks before handing off docs changes.
The default freshness window is 180 days.

Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1
```

Run the HR-scoped check when HR docs are the only intended scope:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module hr
```

Run every backend module check before changing shared docs standards:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -AllModules
```

Override the freshness window only when reviewing older docs intentionally:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -AllModules -MaxAgeDays 365
```

Checks:
- broken internal links in active docs
- broken source-path references in inline code spans
- compact module structure for scoped module validation
- required module files: `README.md`, `product.md`, `api.md`, `architecture.md`, `workflows.md`, and `testing.md`
- active-doc frontmatter keys: `module`, `domain`, `content_type`, `status`, `updated_on`, `owner`, and `audience`
- valid `content_type` values
- stale `updated_on` dates older than the configured freshness window
- module README handoff sections: `TL;DR`, `Read order`, `Source of truth`, `For agents`, and `Validation`
- module docs allow only the `references/` support folder
- root docs whitelist (`architecture`, `common`, `modules`, plus root `README.md` and `best-practices.md`)
- no active archive, generated, or meta docs directories
- scoped module validation with `-Module <module>` when non-HR/global issues are out of scope
- all-module validation with `-AllModules` before a docs-standard handoff
