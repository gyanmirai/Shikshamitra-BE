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
2. Preview returns per-employee: basic salary, gross salary, total deductions, net pay, present/half-day/total working days.
3. Admin locks payroll for the month. If already locked, returns `PayrollAlreadyLocked`. If no employees found, returns `PayrollNoEmployees`.
4. Lock response reports: employees processed, skipped, failed, and any error messages.
5. System publishes `PayrollLockedEto` (distributed event) after successful lock.

### 3. Salary Slips

1. Admin retrieves a salary slip for an employee + month (format: `MMMM-YYYY`).
2. Controller converts `MMMM-YYYY` to internal `YYYY-MM` format.
3. `SalarySlipManager.GenerateOrGetSalarySlipAsync`:
   - If finalized and `forceRegenerate` -> `SalarySlipAlreadyFinalized`
   - If finalized and not force -> returns existing
   - If draft exists -> returns existing
   - Otherwise generates from employee payroll components (earnings + deductions)
4. Month-year format validated: `MonthYearRequired`, `InvalidMonthYearFormat`.

### 4. Employee Salary Structure

1. Admin retrieves per-employee earning and deduction component amounts.
2. Admin updates the component amounts.
3. Each component: name (2-100 chars), amount >= 0, type (Fixed or Percentage), dependency (None/Basic/Gross when Percentage).

---

## Business Rules

- Month-year format for run payroll and salary slips: `MMMM-YYYY` (e.g., `JANUARY-2025`) at the API level, `YYYY-MM` internally.
- Payroll can only be locked once per month; subsequent attempts return `PayrollAlreadyLocked`.
- Finalized salary slips cannot be regenerated.
- Salary structure components with `ComponentType=Percentage` require a `DependOn` value (Basic or Gross).
- Component amounts must be >= 0; percentage amounts must be 0-100.
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

---

## Domain Events

| Event | Trigger | Payload |
|-------|---------|---------|
| `PayrollLockedEto` | Payroll locked for a month | Month, Year, EmployeeCount, TenantId, LockedAt |

---

## Dependencies

- [Employees](../employees/prd.md) -- employee payroll details, payroll structure assignment
- [Payroll Settings](settings/prd.md) -- earning/deduction component definitions, PF rules
- [Attendance & Leaves](../attendance-leaves/prd.md) -- working days data for payroll computation
