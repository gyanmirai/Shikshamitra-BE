# Payroll Operations -- API Contract

## Authentication

All endpoints require Bearer token + `__tenant` header. All controllers use `[Authorize]`.

---

## Endpoints Overview

### Run Payroll (`api/payroll/run`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/payroll/run` | Preview payroll for a month |
| POST | `/api/payroll/run` | Lock payroll for a month |

### Salary Slips (`api/payroll/salary-slips`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/payroll/salary-slips` | Get salary slip for employee + month |

### Employee Salary Structure (`api/payroll/employees/{employeeId}/salary-structure`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/payroll/employees/{employeeId}/salary-structure` | Get salary structure |
| PUT | `/api/payroll/employees/{employeeId}/salary-structure` | Update salary structure |

---

## Endpoint Details

### GET /api/payroll/run

Preview payroll for a given month.

**Permission:** `HR.PayrollManagement.RunPayroll.View`

**Query params:**

| Param | Type | Required | Default | Constraints |
|-------|------|----------|---------|-------------|
| month | string | yes | | `^[A-Z]+-\d{4}$` (e.g., JANUARY-2025) |
| page | int | no | 0 | 0-based |
| pageSize | int | no | 50 | 10-100 |

**Response (200):**

```
RunPayrollResponse {
  isGenerated: bool
  totalCount: int
  currentPage: int
  pageSize: int
  totalPages: int
  items: [{
    employeeName: string
    employeeIdentifier: string
    isTeaching: bool
    basicSalary: double
    grossSalary: double
    totalDeduction: double
    netPay: double
    presentDays: double
    halfWorkingDays: double
    totalWorkingDays: double
  }]
}
```

---

### POST /api/payroll/run

Lock payroll for a month. Irreversible for that month.

**Permission:** `HR.PayrollManagement.RunPayroll.Add`

**Request body:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| month | string | yes | `^[A-Z]+-\d{4}$` |

**Response (200):**

```
PayrollLockResponse {
  message: string
  month: string
  wasAlreadyLocked: bool
  employeesProcessed: int
  employeesSkipped: int
  employeesFailed: int
  errors: string[]?
}
```

**Error codes:** `PayrollAlreadyLocked`, `PayrollNoEmployees`, `InvalidMonthYearFormat`

---

### GET /api/payroll/salary-slips

**Query params:**

| Param | Type | Required | Constraints |
|-------|------|----------|-------------|
| employeeId | Guid | yes | |
| monthYear | string | yes | `^[A-Z]+-\d{4}$` (e.g., JANUARY-2025) |

**Response (200):**

```
SalarySlipResponse {
  employeeId: Guid
  monthYear: string
  totalEarning: double           // >= 0
  totalDeduction: double         // >= 0
  netPay: double                 // >= 0
  earnings: SalaryComponent[]    // minLength 1
  deductions: SalaryComponent[]
  generatedAt: DateTime
}

SalaryComponent {
  name: string                   // 2-100 chars
  description: string?           // max 255
  amount: double                 // >= 0
  isFixed: bool
  isTemp: bool
  componentType: string          // "FIXED" or "PERCENTAGE"
  dependOn: string?              // "BASIC" or "GROSS" (when PERCENTAGE)
}
```

**Error codes:** `InvalidMonthYearFormat`, `MonthYearRequired`, `SalarySlipNotFound`

---

### GET /api/payroll/employees/{employeeId}/salary-structure

**Permission:** `HR.Employee.EmployeeList.View`

**Path params:** `employeeId` (Guid)

**Response (200):**

```
SalaryStructureResponseDto {
  earningComponents: SalaryComponentDto[]
  deductionComponents: SalaryComponentDto[]
}

SalaryComponentDto {
  name: string
  description: string?
  amount: decimal
  isFixed: bool
  isTemp: bool
  componentType: int             // 0=Fixed, 1=Percentage
  dependOn: int?                 // 0=None, 1=Basic, 2=Gross
}
```

---

### PUT /api/payroll/employees/{employeeId}/salary-structure

**Permission:** `HR.Employee.EmployeeList.Add`

**Request body:**

```
UpdateSalaryStructureDto {
  earningComponents: SalaryComponentDto[]    // required, not empty
  deductionComponents: SalaryComponentDto[]  // required, not empty
}
```

**Validation per component:**
- `name`: required, 2-100 chars
- `description`: max 255
- `amount`: >= 0
- `componentType`: valid enum (0 or 1)
- `dependOn`: required when `componentType = 1` (Percentage)

**Response (200):** `{ message: "Salary structure updated successfully" }`
