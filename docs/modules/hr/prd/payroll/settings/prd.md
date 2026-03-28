# Payroll Settings -- Behavior Specification

## Purpose

Manages the master data that drives payroll computation: earning components (Basic Salary, HRA, DA, etc.), deduction components (PF, PT, TDS, etc.), pay elements, provident fund rules, and professional tax slabs. These are tenant-scoped configuration entities referenced by employee salary structures and payroll runs.

## Actors

| Actor | Description |
|-------|-------------|
| Admin | CRUDs all payroll setting entities; activates/deactivates components |
| System | References settings during salary slip generation and payroll runs |

---

## Behaviors

### 1. Earning Components

1. Admin creates an earning component with name, type (Fixed or Variable/Percentage), dependency (None/Basic/Gross/Custom), amount, and visibility.
2. **Fixed components**: amount is a plain decimal value.
3. **Percentage/Variable components**: amount stored with `%` suffix; validated 0-100 range. Requires a `dependOn` value.
4. Dependency can reference a custom component (`CustomComponentId`).
5. Active components are cached (24-hour TTL) for fast retrieval.
6. Admin can soft-delete by deactivating instead of hard-deleting.

### 2. Deduction Components

1. Identical structure and behavior to earning components.
2. Same CRUD, activation/deactivation, caching, and validation rules.

### 3. Pay Elements

1. Admin CRUDs pay elements -- generic credit/debit entries.
2. Each element has a code (unique, max 50), name (max 100), type (Credit or Debit), and optional dependency configuration.
3. Employee-specific flag indicates whether the element amount varies per employee.

### 4. Provident Fund Rules

1. Admin CRUDs PF rules defining employee and employer contribution percentages.
2. Code must be uppercase alphanumeric with underscores (`^[A-Z_]+$`, 2-20 chars).
3. Both percentages must be 0-100.
4. PF rules are referenced by employee payroll details (`pfRuleId`).

### 5. Professional Tax

1. Admin CRUDs tax slab configurations per gender type.
2. Each slab defines: from-salary, to-salary, and tax amount.
3. No HTTP controller exposed; available only via application service.

---

## Business Rules

- Component names: 1-200 chars; descriptions: max 500 chars.
- Fixed components ignore `dependOn`; variable/percentage components require it.
- Percentage amount must be 0-100.
- PF codes must be unique within the tenant.
- Component `displayOrder` controls rendering sequence.
- Active components are served from distributed cache (24h TTL); mutations invalidate cache.
- Multi-tenancy: all settings are tenant-isolated.

---

## Permissions

| Permission Key | Description |
|----------------|-------------|
| `HR.PayrollManagement.Settings.EarningComponents.View` | View earning components |
| `HR.PayrollManagement.Settings.EarningComponents.Add` | Create/update earning components |
| `HR.PayrollManagement.Settings.EarningComponents.Delete` | Delete earning components |
| `HR.PayrollManagement.Settings.DeductionComponents.View` | View deduction components |
| `HR.PayrollManagement.Settings.DeductionComponents.Add` | Create/update deduction components |
| `HR.PayrollManagement.Settings.DeductionComponents.Delete` | Delete deduction components |
| `HR.PayrollManagement.Settings.PfRule.View` | View PF rules |
| `HR.PayrollManagement.Settings.PfRule.Add` | Create/update PF rules |
| `HR.PayrollManagement.Settings.PfRule.Delete` | Delete PF rules |

---

## Error Codes

Payroll settings currently rely on ABP's built-in entity-not-found and validation exceptions. No custom `HRErrorCodes` are defined specifically for settings CRUD operations.

---

## Enums

| Enum | Values |
|------|--------|
| EarningComponentType | Fixed=0, Variable=1 |
| DeductionComponentType | Fixed=0, Variable=1 |
| ComponentDependency | None=0, Basic=1, Gross=2, CustomComponent=3 |
| SalaryComponentType | Fixed=0, Percentage=1 |
| SalaryComponentDependency | None=0, Basic=1, Gross=2 |
| PayElementType | Credit=0, Debit=1 |

---

## Domain Events

None currently published by the Payroll Settings submodule.

---

## Dependencies

- [Payroll Operations](../prd.md) -- salary slip generation uses component definitions
- [Employees](../../employees/prd.md) -- employee payroll references PF rules and component structures
