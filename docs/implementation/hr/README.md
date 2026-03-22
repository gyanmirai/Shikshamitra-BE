# HR implementation notes

HR-specific **process** documentation: how the application layer was refactored and how HttpApi controllers were migrated. For product behavior and REST contracts, use the [HR PRDs](../../prd/hr/README.md).

## Documents

| Document | Purpose |
|----------|---------|
| [application-layer-patterns.md](./application-layer-patterns.md) | HR application-layer conventions (AppService split, validation, errors, mapping) |
| [application-layer-refactoring.md](./application-layer-refactoring.md) | Chronological summary of the HR application refactor |
| [httpapi-migration.md](./httpapi-migration.md) | HttpApi controller migration from monolithic employee service to focused services |

## Related

- [HR PRD index](../../prd/hr/README.md)
- [HR module reference](../../modules/hr.md)
- [Architecture decisions (ADRs)](../../architecture/decisions/README.md) -- eager loading, caching, payroll tiers
- [Best practices](../../best-practices.md)
