# Payroll Settings -- API Contract

## Authentication

All endpoints require Bearer token + `__tenant` header. All controllers use `[Authorize]`.

---

## Endpoints Overview

### Earning Components (`api/payroll/earning-components`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/payroll/earning-components` | List earning components (paginated) |
| POST | `/api/payroll/earning-components` | Create earning component |
| PUT | `/api/payroll/earning-components/{id}` | Update earning component |
| DELETE | `/api/payroll/earning-components/{id}` | Delete earning component |

### Deduction Components (`api/payroll/deduction-components`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/payroll/deduction-components` | List deduction components (paginated) |
| POST | `/api/payroll/deduction-components` | Create deduction component |
| PUT | `/api/payroll/deduction-components/{id}` | Update deduction component |
| DELETE | `/api/payroll/deduction-components/{id}` | Delete deduction component |

### Pay Elements (`api/payroll/pay-elements`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/payroll/pay-elements` | List pay elements (paginated) |
| GET | `/api/payroll/pay-elements/{id}` | Get pay element |
| POST | `/api/payroll/pay-elements` | Create pay element |
| PUT | `/api/payroll/pay-elements/{id}` | Update pay element |
| DELETE | `/api/payroll/pay-elements/{id}` | Delete pay element |

### PF Rules (`api/payroll/pf-rules`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/payroll/pf-rules` | List PF rules (paginated) |
| GET | `/api/payroll/pf-rules/{id}` | Get PF rule |
| POST | `/api/payroll/pf-rules` | Create PF rule |
| PUT | `/api/payroll/pf-rules/{id}` | Update PF rule |
| DELETE | `/api/payroll/pf-rules/{id}` | Delete PF rule |

---

## Endpoint Details

### GET /api/payroll/earning-components

**Permission:** `HR.PayrollManagement.Settings.EarningComponents.View`

**Query params:**

| Param | Type | Default | Constraints |
|-------|------|---------|-------------|
| page | int | 1 | >= 1 |
| pageSize | int | 20 | |

**Response (200):**

```
{
  totalCount: int
  currentPage: int
  pageSize: int
  totalPages: int
  items: [{
    id: Guid
    componentName: string
    description: string?
    isFixed: bool
    componentType: string?       // "FIXED" or "PERCENTAGE"
    amount: double
    visibleInPayslip: bool
    customComponent: {
      id: Guid
      componentName: string
      dependOn: string
      description: string?
      isFixed: bool
      visibleInPayslip: bool
    }?
  }]
}
```

---

### POST /api/payroll/earning-components

**Permission:** `HR.PayrollManagement.Settings.EarningComponents.Add`

**Request body:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| componentName | string | yes | |
| description | string | no | |
| isFixed | bool | yes | If true, componentType = FIXED |
| componentType | string | no | "FIXED" or "PERCENTAGE" |
| amount | double | no | |
| visibleInPayslip | bool | no | |
| dependOn | string | no | "BASIC", "GROSS", or "CUSTOM_COMPONENT" |
| customComponentId | Guid | no | Required when dependOn = "CUSTOM_COMPONENT" |

**Mapping rules:**
- `isFixed: true` takes priority -> Fixed component, amount stored as plain number
- `componentType: "PERCENTAGE"` -> Variable, amount stored with `%` suffix (validated 0-100)
- `dependOn` parsed case-insensitively; common aliases accepted ("basic_salary" -> Basic)

**Response (201):** `{ id: Guid, createdAt: DateTime }`

---

### PUT /api/payroll/earning-components/{id}

**Permission:** `HR.PayrollManagement.Settings.EarningComponents.Add`

**Request body:** same as POST

**Response (200):** OK

---

### DELETE /api/payroll/earning-components/{id}

**Permission:** `HR.PayrollManagement.Settings.EarningComponents.Delete`

**Response (204):** No Content

---

### Deduction Components

Identical API shape and behavior to Earning Components. Replace `earning-components` with `deduction-components` in all routes. Permissions use `DeductionComponents` instead of `EarningComponents`.

---

### GET /api/payroll/pay-elements

**Query params:** ABP standard `PagedAndSortedResultRequestDto` (skipCount, maxResultCount, sorting)

**Response (200):** `PagedResultDto<PayElementDto>`

```
PayElementDto {
  id: Guid
  code: string                   // max 50
  name: string                   // max 100
  type: int                      // 0=Credit, 1=Debit
  isEmployeeSpecific: bool
  isDependent: bool
  dependent: {
    elementId: string
    percentage: float?
    amount: float?
  }?
  isVisibleInPaySlip: bool
}
```

---

### POST /api/payroll/pay-elements

**Request body -- CreateUpdatePayElementDto:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| code | string | yes | max 50 |
| name | string | yes | max 100 |
| type | int | yes | 0=Credit, 1=Debit |
| isEmployeeSpecific | bool | yes | |
| isDependent | bool | yes | |
| dependent | object | no | { elementId, percentage?, amount? } |
| isVisibleInPaySlip | bool | yes | |

**Response (201):** `PayElementDto`

---

### GET /api/payroll/pf-rules

**Permission:** `HR.PayrollManagement.Settings.PfRule.View`

**Query params:**

| Param | Type | Default | Constraints |
|-------|------|---------|-------------|
| page | int | 1 | >= 1 |
| pageSize | int | 20 | 10-100 |

**Response (200):**

```
PaginatedPfRuleResponseDto {
  totalCount: int
  currentPage: int
  pageSize: int
  totalPages: int
  items: [{
    id: Guid
    name: string
    code: string
    employeePercentage: decimal
    employerPercentage: decimal
    createdAt: DateTime
    updatedAt: DateTime?
  }]
}
```

---

### POST /api/payroll/pf-rules

**Permission:** `HR.PayrollManagement.Settings.PfRule.Add`

**Request body:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| name | string | yes | 2-100 chars |
| code | string | yes | 2-20 chars, `^[A-Z_]+$` |
| employeePercentage | decimal | yes | 0-100 |
| employerPercentage | decimal | yes | 0-100 |

**Response (201):** `{ id: Guid, createdAt: DateTime }`

---

### PUT /api/payroll/pf-rules/{id}

**Permission:** `HR.PayrollManagement.Settings.PfRule.Add`

**Request body:** same as POST

**Response (200):** `{ id: Guid, updatedAt: DateTime }`

---

### DELETE /api/payroll/pf-rules/{id}

**Permission:** `HR.PayrollManagement.Settings.PfRule.Delete`

**Response (204):** No Content

---

## Professional Tax (No HTTP Controller)

Professional tax is managed via `IProfessionalTaxAppService` only (no REST endpoints). DTO shape for reference:

```
CreateUpdateProfessionalTaxDto {
  genderType: int                // 0=Male, 1=Female, 2=Other
  fromSalary: double
  toSalary: double
  taxAmount: double
}
```
