# ADR-003: Payroll two-tier architecture

## Status

Accepted

## Context

Payroll needs both **organization-wide definitions** (reusable components, templates, rules) and **per-employee actuals** (bank details, UAN, component amounts that may differ from defaults). Mixing these in one model couples master data to employee rows and complicates updates and reporting.

## Decision

Split into **two tiers**:

**Tier 1 -- Master configuration (`HR.Payroll`)**

- Tenant-scoped masters: e.g. `EarningComponent`, `DeductionComponent`, `PayElement`, `PayrollStructure`, PF/professional tax configuration as applicable.
- Domain services (e.g. `PayrollStructureManager`, component managers) assign structures to employees and can **auto-initialize** employee-side rows from templates.

**Tier 2 -- Employee-specific data (`HR.Employees` payroll slice)**

- `EmployeePayrollDetails`: bank, statutory ids, base salary, TDS settings, etc.
- `EmployeePayrollComponent`: line-level earning/deduction amounts per employee; may override template defaults.
- Initialization/sync from Tier 1 is orchestrated by services such as `EmployeePayrollInitializationService` when a structure is assigned or updated.

API contracts (e.g. payroll contract DTOs) expose Tier 2 to clients; Tier 1 remains master-data CRUD and payroll run/slip pipelines.

## Consequences

**Positive**

- Clear boundary: change master definitions without rewriting every employee row; employee rows hold **instance** data.
- Supports templates + overrides (per-employee amounts).
- Aligns with payroll run and salary slip generation reading resolved amounts from Tier 2.

**Negative**

- More moving parts: assignment, initialization, and sync must stay consistent when masters change.
- Developers must know which tier owns which concern to avoid duplicate business rules.

## Key details

- Employee aggregate may hold `PayrollStructureId` (FK to Tier 1) while amounts live in Tier 2 owned tables.
- See [HR module reference](../../modules/hr/technical-reference.md) and [payroll PRD](../../modules/hr/prd/payroll/prd.md) for current endpoint and entity names.

