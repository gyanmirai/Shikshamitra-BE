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

### Attendance Import Logs (`api/payroll/attendance-imports`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/payroll/attendance-imports` | Get paginated import file logs |
| GET | `/api/payroll/attendance-imports/{importId}` | Get single import log detail |
| GET | `/api/payroll/attendance-imports/{importId}/rows` | Get paginated rows for one imported file |

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
    payableDays: double
    prorationFactor: double
    dayCalculationMode: string
    usedDefaultDayCalculation: bool
    earningComponents: RunPayrollComponentItem[]
    deductionComponents: RunPayrollComponentItem[]
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
| monthYear | string | yes | `^[A-Z]+-\d{4}$` |

**Response (200):**

```
SalarySlipResponse {
  employeeId: Guid
  monthYear: string
  totalEarning: double
  totalDeduction: double
  netPay: double
  earnings: SalaryComponent[]
  deductions: SalaryComponent[]
  generatedAt: DateTime
}
```

---

### GET /api/payroll/employees/{employeeId}/salary-structure

**Permission:** `HR.Employee.EmployeeList.View`

**Response (200):**

```
SalaryStructureResponseDto {
  earningComponents: SalaryComponentDto[]
  deductionComponents: SalaryComponentDto[]
}
```

---

### PUT /api/payroll/employees/{employeeId}/salary-structure

**Permission:** `HR.Employee.EmployeeList.Add`

**Request body:**

```
UpdateSalaryStructureDto {
  earningComponents: SalaryComponentDto[]
  deductionComponents: SalaryComponentDto[]
}
```

---

### GET /api/payroll/attendance-imports

Get paginated attendance import file logs used by payroll users.

**Permission:** `HR.PayrollManagement.RunPayroll.View`

**Query params:**

| Param | Type | Required | Default | Constraints |
|-------|------|----------|---------|-------------|
| search | string | no | | matches `fileName` and `importedByName` |
| payrollMonth | string | no | | `MMMM-YYYY` (e.g., APRIL-2026) |
| skipCount | int | no | 0 | >= 0 |
| maxResultCount | int | no | 50 | 1-200 |

**Response (200):**

```
PagedResultDto<AttendanceImportLogListItemDto> {
  totalCount: int
  items: [{
    id: Guid
    fileName: string
    importedByUserId: Guid
    importedByName: string
    importedAt: DateTime
    status: string                 // preview | confirmed | payroll_locked
    payrollMonth: string?
    totalRows: int
    validRows: int
    invalidRows: int
    createCount: int
    replaceCount: int
    processedRows: int
    createdCount: int
    replacedCount: int
    canOpen: bool
  }]
}
```

---

### GET /api/payroll/attendance-imports/{importId}

Get detailed metadata for one import file.

**Permission:** `HR.PayrollManagement.RunPayroll.View`

**Path params:** `importId` (Guid)

**Response (200):**

```
AttendanceImportLogDetailDto {
  ...all list item fields
  confirmedAt: DateTime?
  previewTokenExpiresAt: DateTime?
  errorsSummary: string[]
  warningsSummary: string[]
}
```

---

### GET /api/payroll/attendance-imports/{importId}/rows

Get paginated normalized rows for one import file.

**Permission:** `HR.PayrollManagement.RunPayroll.View`

**Query params:**

| Param | Type | Required | Default | Constraints |
|-------|------|----------|---------|-------------|
| skipCount | int | no | 0 | >= 0 |
| maxResultCount | int | no | 50 | 1-500 |

**Response (200):**

```
PagedResultDto<AttendanceImportRowDto> {
  totalCount: int
  items: [{
    rowNumber: int
    action: string
    attendanceDate: string
    employeeCode: string
    employeeName: string?
    contactNo: string?
    timeIn: string?
    timeOut: string?
    status: string?
    remarks: string?
    errors: string[]
    warnings: string[]
  }]
}
```

---

## Notes

- Attendance import preview enforces a single payroll month per file.
- Status `payroll_locked` is derived from finalized salary slips for that batch month.
