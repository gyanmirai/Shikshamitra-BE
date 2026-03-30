# Reports -- API Contract

## Authentication

All endpoints require Bearer token + `__tenant` header. Controller uses `[Authorize]`.

---

## Endpoints Overview

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/hr/reports/employees` | Get filtered employee report |
| GET | `/api/hr/reports/employees/contacts` | Get filtered contact report |
| GET | `/api/hr/reports/employees/export` | Export report as PDF or Excel |

---

## Endpoint Details

### GET /api/hr/reports/employees

**Query params:**

| Param | Type | Required | Constraints |
|-------|------|----------|-------------|
| genderType | int | no | 0=Male, 1=Female, 2=Other |

**Response (200):** `EmployeeDto[]` -- full employee records (see [Employees API contract](../employees/api-contract.md) for field reference).

---

### GET /api/hr/reports/employees/contacts

**Query params:** same as above.

**Response (200):**

```
EmployeeContactDto[] {
  employeeName: string?
  email: string?
  mobileNo: string?
  dateOfBirth: DateOnly?
  employeeCode: string?
}
```

---

### GET /api/hr/reports/employees/export

**Query params:**

| Param | Type | Required | Default | Constraints |
|-------|------|----------|---------|-------------|
| genderType | int | no | | 0=Male, 1=Female, 2=Other |
| exportOptionType | int | no | 0 | 0=PDF, 1=Excel |

**Response (200):** Binary file download.

| Export Type | MIME Type |
|-------------|-----------|
| PDF | `application/pdf` |
| Excel | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |

**Error codes:** `ReportExportFailed`
