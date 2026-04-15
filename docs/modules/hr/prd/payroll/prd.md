# Payroll Operations -- Behavior Specification

## Purpose

Handles payroll execution and salary output: assigning payroll structures to employees, previewing/locking monthly payroll runs, generating salary slips, and managing per-employee salary structures (earning/deduction component amounts).

## Actors

| Actor | Description |
|-------|-------------|
| Admin | Previews and locks payroll, views salary slips, updates employee salary structures |
| System | Computes salary slips from employee payroll components, locks payroll for a month |

---

## Behaviors

### 1. Payroll Structures

1. A payroll structure links an employee to configured earning and deduction components with amounts, scoped by education medium/stage.
2. `PayrollStructureManager.SetEmployeePayrollStructureAsync` validates employee and structure exist, assigns the structure, and optionally auto-initializes payroll data (EmployeePayrollDetails + EmployeePayrollComponents).
3. If the employee already has payroll data, components are synced with the new structure; otherwise initialized from scratch.
4. `RemoveEmployeePayrollStructureAsync` clears the assignment; optionally deletes payroll data.
5. `ReinitializeEmployeePayrollAsync` requires an existing structure assignment (`PayrollNoStructureAssigned`).
6. The `PayrollStructureController` endpoints are currently **commented out** (inactive).

### 2. Run Payroll

1. Admin previews payroll for a given month (format: `MMMM-YYYY`, e.g., `JANUARY-2025`).
2. Preview returns per-employee: basic salary, gross salary, total deductions, net pay, present/half-day counts, **payroll basis days** (denominator from tenant day-calculation setting), **payable days** (present + 0.5 × half days), **proration factor** (capped at 1), **day calculation mode** (`working-days` / `total-month-days` / `manual-days`), **and itemized earning/deduction component line items** (name + amount each). Amounts are **LOP-prorated**: non-temporary components are multiplied by `min(1, payableDays / basisDays)`. Temporary (`IsTemp`) lines are not prorated.
3. If no day-calculation row exists for the tenant, behaviour matches **`working-days`** (weekdays in the month minus holidays on weekdays).
4. Admin locks payroll for the month. If already locked, returns `PayrollAlreadyLocked`. If no employees found, returns `PayrollNoEmployees`.
5. Lock response reports: employees processed, skipped, failed, and any error messages.
6. Salary slips generated at lock use the **same** basis, factor, and prorated line amounts as preview for that attendance snapshot.
7. System publishes `PayrollLockedEto` (distributed event) after successful lock.

### 3. Salary Slips

1. Admin retrieves a salary slip for an employee + month (format: `MMMM-YYYY`).
2. Controller converts `MMMM-YYYY` to internal `YYYY-MM` format.
3. `SalarySlipManager.GenerateOrGetSalarySlipAsync`:
   - If finalized and `forceRegenerate` -> `SalarySlipAlreadyFinalized`
   - If finalized and not force -> returns existing
   - If draft exists -> returns existing
   - Otherwise generates from employee payroll components (earnings + deductions) with **LOP proration** and persists **payroll basis days, payable days, proration factor, and day-calculation mode** on the slip for audit.
4. Month-year format validated: `MonthYearRequired`, `InvalidMonthYearFormat`.

### 4. Employee Salary Structure

1. Admin retrieves per-employee earning and deduction component amounts.
2. Admin updates the component amounts.
3. Each component: name (2-100 chars), amount >= 0, type (Fixed or Percentage), dependency (None/Basic/Gross when Percentage).

### 5. Attendance Import Logs (Payroll-facing)

1. Payroll users can list attendance import files from `/api/payroll/attendance-imports`.
2. Each file row includes: file name, imported by, imported at, payroll month, row counters, and status.
3. Status lifecycle:
   - `preview`: preview generated and cached, not confirmed.
   - `confirmed`: confirmed and attendance rows applied.
   - `payroll_locked`: confirmed import where finalized salary slips exist for that payroll month.
4. Payroll users can open one file (`/api/payroll/attendance-imports/{importId}`) and read normalized row content (`/api/payroll/attendance-imports/{importId}/rows`).
5. Import preview enforces **single payroll month per file**; multi-month files are rejected.

---

## Business Rules

- Month-year format for run payroll and salary slips: `MMMM-YYYY` (e.g., `JANUARY-2025`) at the API level, `YYYY-MM` internally.
- Payroll can only be locked once per month; subsequent attempts return `PayrollAlreadyLocked`.
- Finalized salary slips cannot be regenerated.
- Salary structure components with `ComponentType=Percentage` require a `DependOn` value (Basic or Gross).
- Component amounts must be >= 0; percentage amounts must be 0-100.
- Attendance import file must contain attendance dates from only one month-year.
- Import log status `payroll_locked` is derived from finalized salary slips for the same month.
- Multi-tenancy: all payroll data is tenant-isolated.

---

## Permissions

| Permission Key | Description |
|----------------|-------------|
| `HR.PayrollManagement.Dashboard.View` | View payroll dashboard |
| `HR.PayrollManagement.RunPayroll.View` | Preview payroll |
| `HR.PayrollManagement.RunPayroll.Add` | Lock payroll |
| `HR.PayrollManagement.RunPayroll.Delete` | Delete payroll run |
| `HR.PayrollManagement.GenerateSalarySlip.View` | View salary slips |
| `HR.PayrollManagement.GenerateSalarySlip.Add` | Generate salary slips |
| `HR.PayrollManagement.GenerateSalarySlip.Delete` | Delete salary slips |
| `HR.Employee.EmployeeList.View` | View employee salary structure |
| `HR.Employee.EmployeeList.Add` | Update employee salary structure |
| `HR.PayrollManagement.RunPayroll.View` | View payroll attendance import logs and row content |

---

## Error Codes

| Code | Constant | When |
|------|----------|------|
| `HR:Payroll:030001` | PayrollAlreadyLocked | Payroll already locked for the month |
| `HR:Payroll:030002` | PayrollNoEmployees | No employees found for payroll run |
| `HR:Payroll:030003` | SalarySlipNotFound | Salary slip not found |
| `HR:Payroll:030004` | SalarySlipAlreadyFinalized | Cannot regenerate a finalized slip |
| `HR:Payroll:030005` | InvalidMonthYearFormat | Invalid month-year format |
| `HR:Payroll:030006` | MonthYearRequired | Month-year parameter is required |
| `HR:Payroll:030007` | PayrollStructureNotFound | Payroll structure not found |
| `HR:Payroll:030008` | PayrollNoStructureAssigned | No payroll structure assigned to employee |
| `HR:Leave:020210` | AttendanceImportMultiplePayrollMonths | Attendance import contains multiple payroll months |

---

## Domain Events

| Event | Trigger | Payload |
|-------|---------|---------|
| `PayrollLockedEto` | Payroll locked for a month | Month, Year, EmployeeCount, TenantId, LockedAt |

---

## Earning & Deduction Component API Tiers

The system exposes earning and deduction components through two distinct API tiers.

### Master / Settings APIs (tenant-level catalog)

Full CRUD for component definitions shared across the tenant. These define **what** components exist (name, calculation type, visibility, etc.), not what any specific employee is assigned.

- `GET/POST/PUT/DELETE /api/payroll/earning-components` -- `EarningComponentController`
- `GET/POST/PUT/DELETE /api/payroll/deduction-components` -- `DeductionComponentController`
- Permissions: `PayrollManagement_SettingsEarningComponents`, `PayrollManagement_SettingsDeductionComponents`
- PRD: [Payroll Settings](settings/prd.md)

### Employee-scoped APIs (per-employee instances with amounts)

These assign component **instances** to individual employees and expose them at different granularity levels:

| Endpoint | Granularity | Shape |
|----------|-------------|-------|
| `GET /api/employees/{id}/payroll` | Lightweight contract | `ComponentAmountContract` (`componentId` + `amount`) |
| `GET /api/payroll/employees/{id}/salary-structure` | Full structure | `SalaryComponentDto` (name, amount, type, dependency, flags) |
| `GET /api/payroll/run` (preview) | Aggregated + line items | `RunPayrollComponentItem` (name + amount) per employee |
| `GET /api/payroll/salary-slips` | Finalized slip | `SalaryComponent` (name, description, amount, type, dependency, flags) |

**Relationship:** Settings APIs define the catalog; employee-scoped APIs assign instances with amounts derived from (or independent of) that catalog.

---

## Dependencies

- [Employees](../employees/prd.md) -- employee payroll details, payroll structure assignment
- [Payroll Settings](settings/prd.md) -- earning/deduction component definitions, PF rules
- [Attendance & Leaves](../attendance-leaves/prd.md) -- working days data for payroll computation
- [Implementation Reference](../../../hr/payroll/README.md) -- full endpoint-level documentation with request/response examples
