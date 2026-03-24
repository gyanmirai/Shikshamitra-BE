# HR Module -- PRD & API Contracts

Concise behavior specifications and API contracts for every HR submodule.

Each submodule has two files:
- **prd.md** -- behaviors, business rules, permissions, error codes, domain events
- **api-contract.md** -- endpoint reference with request/response shapes

---

## Submodules

| Submodule | PRD | API Contract | Scope |
|-----------|-----|-------------|-------|
| **Employees** | [prd.md](employees/prd.md) | [api-contract.md](employees/api-contract.md) | Personal info, family, work experience, additional details, documents, bank details, payroll, status, search, reporting manager |
| **Calendar** | [prd.md](calendar/prd.md) | [api-contract.md](calendar/api-contract.md) | Events, event types, holidays, holiday types, unified calendar view |
| **Attendance & Leaves** | [prd.md](attendance-leaves/prd.md) | [api-contract.md](attendance-leaves/api-contract.md) | Leave management, leave types, leave policies, attendance tracking, notifications |
| **Payroll Operations** | [prd.md](payroll/prd.md) | [api-contract.md](payroll/api-contract.md) | Run payroll, salary slips, employee salary structure, payroll structures |
| **Payroll Settings** | [prd.md](payroll/settings/prd.md) | [api-contract.md](payroll/settings/api-contract.md) | Earning/deduction components, pay elements, PF rules, professional tax |
| **Reports** | [prd.md](reports/prd.md) | [api-contract.md](reports/api-contract.md) | Employee reports, contact reports, PDF/Excel export |

---

## Related Documentation

- [HR Module -- Technical Reference](../../modules/hr.md) -- entities, domain services, repositories, folder structure
- [Best Practices](../../best-practices.md) -- project-wide engineering standards
- [Application Layer Patterns](../../implementation/hr/application-layer-patterns.md) -- refactoring patterns and service decomposition
- [HttpApi Migration](../../implementation/hr/httpapi-migration.md) -- controller migration details
- [Architecture decisions (ADRs)](../../architecture/decisions/README.md) -- employee eager loading, caching, payroll two-tier model
