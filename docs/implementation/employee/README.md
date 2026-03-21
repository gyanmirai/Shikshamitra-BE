# Employee module — implementation guides

Backend-focused documentation: EF Core, caching, eager loading, and related implementation detail. **API contracts and examples** live under [docs/api/employee/](../../api/employee/README.md). **Optimization summaries and “big picture”** narratives live under [docs/architecture/employee/](../../architecture/employee/README.md).

## Available guides (in this folder)

| Document | Topic |
|----------|--------|
| [Employee-EagerLoading-Implementation.md](./Employee-EagerLoading-Implementation.md) | Navigation properties, AutoInclude, N+1 avoidance |
| [Employee-Caching-Implementation.md](./Employee-Caching-Implementation.md) | Distributed caching, invalidation, Redis-oriented notes |
| [Employee-AdditionalDetails-Implementation.md](./Employee-AdditionalDetails-Implementation.md) | Additional details feature implementation |

## Payroll

Payroll is implemented in the codebase (entities, DTOs, `EmployeeAppService`, etc.). There is no separate step-by-step markdown guide in this repository.

- **API reference:** [Employee-Payroll-API.md](../../api/employee/Employee-Payroll-API.md)
- **Code:** `src/ShikshaMitra.Application/HR/Employees/`, `src/ShikshaMitra.Domain/HR/Employees/`, `src/ShikshaMitra.HttpApi/HR/Employees/`, and related EF configurations.

## Related links

- [Employee API README](../../api/employee/README.md)
- [Project best practices](../../best-practices.md)

[Back to implementation home](../README.md)
