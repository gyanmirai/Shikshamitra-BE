# HR Payroll Module — API Documentation

> **Module:** HR → Payroll Management  
> **Base URL:** `/api/payroll`  
> **Authentication:** All endpoints require Bearer token (`[Authorize]`)  
> **Framework:** ABP Framework (Volo.Abp) on .NET 9

---

## Table of Contents

1. [Overview](#overview)
2. [Sub-Modules](#sub-modules)
3. [API Reference](#api-reference)
   - [Earning Components](#1-earning-components)
   - [Deduction Components](#2-deduction-components)
   - [Pay Elements](#3-pay-elements)
   - [PF Rules (Provident Fund)](#4-pf-rules-provident-fund)
   - [Day Calculation Settings](#5-day-calculation-settings)
   - [Employee Salary Structure](#6-employee-salary-structure)
   - [Run Payroll](#7-run-payroll)
   - [Salary Slips](#8-salary-slips)
4. [Enumerations](#enumerations)
5. [Shared DTOs](#shared-dtos)
6. [Architecture Overview](#architecture-overview)
7. [Not Yet Implemented](#not-yet-implemented)

---

## Overview

The Payroll module handles all salary-related operations for the HR system, including:

- **Settings** — configure earning/deduction components, pay elements, PF rules, and day calculation rules.
- **Employee Salary Structure** — assign per-employee earning and deduction components with amounts.
- **Run Payroll** — preview and lock (finalize) monthly payroll for all employees.
- **Salary Slips** — retrieve finalized salary slips for individual employees.

---

## Sub-Modules

| Sub-Module | Route Prefix | Status |
|---|---|---|
| Earning Components | `/api/payroll/earning-components` | ✅ Implemented |
| Deduction Components | `/api/payroll/deduction-components` | ✅ Implemented |
| Pay Elements | `/api/payroll/pay-elements` | ✅ Implemented |
| PF Rules (Provident Fund) | `/api/payroll/pf-rules` | ✅ Implemented |
| Day Calculation | `/api/payroll/day-calculation` | ✅ Implemented |
| Employee Salary Structure | `/api/payroll/employees/{employeeId}/salary-structure` | ✅ Implemented |
| Run Payroll | `/api/payroll/run` | ✅ Implemented |
| Salary Slips | `/api/payroll/salary-slips` | ✅ Implemented |
| Professional Tax | — | ⚠️ Domain + DTOs exist, no controller |

---

## API Reference

### 1. Earning Components

Manage salary earning component templates (e.g., Basic Salary, HRA, Allowances).

**Route:** `/api/payroll/earning-components`

#### Endpoints

| Method | Path | Permission | Description |
|---|---|---|---|
| `GET` | `/` | `PayrollManagement_SettingsEarningComponents.View` | List earning components (paginated) |
| `GET` | `/{id}` | `PayrollManagement_SettingsEarningComponents.View` | Get a single earning component |
| `POST` | `/` | `PayrollManagement_SettingsEarningComponents.Add` | Create a new earning component |
| `PUT` | `/{id}` | `PayrollManagement_SettingsEarningComponents.Add` | Update an earning component |
| `DELETE` | `/{id}` | `PayrollManagement_SettingsEarningComponents.Delete` | Delete an earning component |

#### GET `/` — List Earning Components

**Query Parameters:**

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | `int` | `1` | Page number (1-based) |
| `pageSize` | `int` | `20` | Items per page |
| `search` | `string?` | `null` | Filter by name |

**Response:** `200 OK` → `EarningComponentPaginatedResponseDto`

```json
{
  "totalCount": 5,
  "currentPage": 1,
  "pageSize": 20,
  "totalPages": 1,
  "items": [
    {
      "id": "guid",
      "earningComponentName": "Basic Salary",
      "description": "Base salary component",
      "earningType": "Fixed",
      "calculationType": "Amount",
      "amount": 30000,
      "percentage": null,
      "selectedEarningComponentId": null,
      "visibleInPayslip": true,
      "customComponent": null
    }
  ]
}
```

#### GET `/{id}` — Get Earning Component

**Response:** `200 OK` → `EarningComponentResponseDto`

#### POST `/` — Create Earning Component

**Request Body:** `EarningComponentCreateRequestDto`

```json
{
  "earningComponentName": "HRA",
  "description": "House Rent Allowance",
  "earningType": "Variable",
  "calculationType": "Percentage",
  "amount": null,
  "percentage": 40,
  "selectedEarningComponentId": null,
  "visibleInPayslip": true
}
```

**Response:** `201 Created` → `ComponentCreateResponseDto`

```json
{
  "id": "guid",
  "createdAt": "2025-01-15T10:30:00Z"
}
```

#### PUT `/{id}` — Update Earning Component

**Request Body:** `EarningComponentUpdateRequestDto` (same shape as create)

**Response:** `200 OK`

#### DELETE `/{id}` — Delete Earning Component

**Response:** `204 No Content`

---

### 2. Deduction Components

Manage salary deduction component templates (e.g., PF, Tax, ESI).

**Route:** `/api/payroll/deduction-components`

#### Endpoints

| Method | Path | Permission | Description |
|---|---|---|---|
| `GET` | `/` | `PayrollManagement_SettingsDeductionComponents.View` | List deduction components (paginated) |
| `GET` | `/{id}` | `PayrollManagement_SettingsDeductionComponents.View` | Get a single deduction component |
| `POST` | `/` | `PayrollManagement_SettingsDeductionComponents.Add` | Create a new deduction component |
| `PUT` | `/{id}` | `PayrollManagement_SettingsDeductionComponents.Add` | Update a deduction component |
| `DELETE` | `/{id}` | `PayrollManagement_SettingsDeductionComponents.Delete` | Delete a deduction component |

#### GET `/` — List Deduction Components

**Query Parameters:**

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | `int` | `1` | Page number (1-based) |
| `pageSize` | `int` | `20` | Items per page |
| `search` | `string?` | `null` | Filter by name |

**Response:** `200 OK` → `DeductionComponentPaginatedResponseDto`

```json
{
  "totalCount": 3,
  "currentPage": 1,
  "pageSize": 20,
  "totalPages": 1,
  "items": [
    {
      "id": "guid",
      "deductionComponentName": "Provident Fund",
      "description": "Employee PF contribution",
      "deductionType": "Fixed",
      "calculationType": "Percentage",
      "amount": null,
      "percentage": 12,
      "selectedDeductionComponentId": null,
      "visibleInPayslip": true,
      "customComponent": null
    }
  ]
}
```

#### POST `/` — Create Deduction Component

**Request Body:** `DeductionComponentCreateRequestDto`

```json
{
  "deductionComponentName": "ESI",
  "description": "Employee State Insurance",
  "deductionType": "Variable",
  "calculationType": "Percentage",
  "amount": null,
  "percentage": 1.75,
  "selectedDeductionComponentId": null,
  "visibleInPayslip": true
}
```

**Response:** `201 Created` → `ComponentCreateResponseDto`

#### PUT `/{id}` — Update Deduction Component

**Request Body:** `DeductionComponentUpdateRequestDto`

**Response:** `200 OK`

#### DELETE `/{id}` — Delete Deduction Component

**Response:** `204 No Content`

---

### 3. Pay Elements

Manage generic pay elements with credit/debit types and dependency support.

**Route:** `/api/payroll/pay-elements`

> **Note:** This controller does not enforce specific permissions — it relies on the class-level `[Authorize]`.

#### Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/` | List pay elements (paginated, sortable) |
| `GET` | `/{id}` | Get a single pay element |
| `POST` | `/` | Create a new pay element |
| `PUT` | `/{id}` | Update a pay element |
| `DELETE` | `/{id}` | Delete a pay element |

#### GET `/` — List Pay Elements

**Query Parameters:** Standard ABP `PagedAndSortedResultRequestDto` (`SkipCount`, `MaxResultCount`, `Sorting`)

**Response:** `200 OK` → `PagedResultDto<PayElementDto>`

```json
{
  "totalCount": 10,
  "items": [
    {
      "id": "guid",
      "code": "BASIC",
      "name": "Basic Salary",
      "type": 0,
      "isEmployeeSpecific": false,
      "isDependent": false,
      "dependent": null,
      "isVisibleInPaySlip": true
    }
  ]
}
```

#### POST `/` — Create Pay Element

**Request Body:** `CreateUpdatePayElementDto`

```json
{
  "code": "HRA",
  "name": "House Rent Allowance",
  "type": 0,
  "isEmployeeSpecific": false,
  "isDependent": true,
  "dependent": {
    "elementId": "guid-of-basic",
    "percentage": 40.0,
    "amount": null
  },
  "isVisibleInPaySlip": true
}
```

**Response:** `201 Created` → `PayElementDto`

---

### 4. PF Rules (Provident Fund)

Manage Provident Fund contribution rules with employee and employer percentages.

**Route:** `/api/payroll/pf-rules`

#### Endpoints

| Method | Path | Permission | Description |
|---|---|---|---|
| `GET` | `/` | `PayrollManagement_SettingsPfRule.View` | List PF rules (paginated) |
| `GET` | `/{id}` | `PayrollManagement_SettingsPfRule.View` | Get a single PF rule |
| `POST` | `/` | `PayrollManagement_SettingsPfRule.Add` | Create a new PF rule |
| `PUT` | `/{id}` | `PayrollManagement_SettingsPfRule.Add` | Update a PF rule |
| `DELETE` | `/{id}` | `PayrollManagement_SettingsPfRule.Delete` | Delete a PF rule |

#### GET `/` — List PF Rules

**Query Parameters:**

| Parameter | Type | Default | Range | Description |
|---|---|---|---|---|
| `page` | `int` | `1` | ≥ 1 | Page number |
| `pageSize` | `int` | `20` | 10–100 | Items per page |
| `search` | `string?` | `null` | — | Filter by name/code |

**Response:** `200 OK` → `PaginatedPfRuleResponseDto`

```json
{
  "totalCount": 2,
  "currentPage": 1,
  "pageSize": 20,
  "totalPages": 1,
  "items": [
    {
      "id": "guid",
      "name": "Standard PF",
      "code": "STD_PF",
      "employeePercentage": 12.00,
      "employerPercentage": 12.00,
      "createdAt": "2025-01-01T00:00:00Z",
      "updatedAt": null
    }
  ]
}
```

#### POST `/` — Create PF Rule

**Request Body:** `CreateUpdateProvidentFundDto`

```json
{
  "name": "Standard PF",
  "code": "STD_PF",
  "employeePercentage": 12.00,
  "employerPercentage": 12.00
}
```

**Response:** `201 Created` → `PfRuleCreateResponseDto`

```json
{
  "id": "guid",
  "createdAt": "2025-01-15T10:30:00Z"
}
```

#### PUT `/{id}` — Update PF Rule

**Request Body:** `CreateUpdateProvidentFundDto`

**Response:** `200 OK` → `PfRuleUpdateResponseDto`

```json
{
  "id": "guid",
  "updatedAt": "2025-01-15T12:00:00Z"
}
```

#### DELETE `/{id}` — Delete PF Rule

**Response:** `204 No Content`

---

### 5. Day Calculation Settings

Configure how working days are calculated for payroll (business days, calendar days, or manual override).

**Route:** `/api/payroll/day-calculation`

#### Endpoints

| Method | Path | Permission | Description |
|---|---|---|---|
| `POST` | `/` | `PayrollManagement_SettingsDayCalculation.Add` | Save day calculation setting |

#### POST `/` — Save Day Calculation Setting

**Request Body:** `DayCalculationRequestDto`

```json
{
  "dayCalculationType": "WorkingDays",
  "manualDays": null
}
```

**`dayCalculationType` values:** `WorkingDays` (0), `TotalMonthDays` (1), `ManualDays` (2)

> When `dayCalculationType` is `ManualDays`, the `manualDays` field is required.

**Response:** `200 OK` → `DayCalculationResponseDto`

```json
{
  "success": true,
  "message": "Day calculation setting saved successfully"
}
```

---

### 6. Employee Salary Structure

Manage per-employee salary structure — assign earning and deduction components with individual amounts.

**Route:** `/api/payroll/employees/{employeeId}/salary-structure`

#### Endpoints

| Method | Path | Permission | Description |
|---|---|---|---|
| `GET` | `/` | `Employee_EmployeeList.View` | Get employee salary structure |
| `PUT` | `/` | `Employee_EmployeeList.Add` | Update employee salary structure |

#### GET `/` — Get Employee Salary Structure

**Response:** `200 OK` → `SalaryStructureResponseDto`

```json
{
  "earningComponents": [
    {
      "name": "Basic Salary",
      "description": "Base component",
      "amount": 30000,
      "isFixed": true,
      "isTemp": false,
      "componentType": "Fixed",
      "dependOn": null
    },
    {
      "name": "HRA",
      "description": "House Rent Allowance",
      "amount": 12000,
      "isFixed": true,
      "isTemp": false,
      "componentType": "Percentage",
      "dependOn": "Basic"
    }
  ],
  "deductionComponents": [
    {
      "name": "PF",
      "description": "Provident Fund",
      "amount": 3600,
      "isFixed": true,
      "isTemp": false,
      "componentType": "Percentage",
      "dependOn": "Basic"
    }
  ]
}
```

#### PUT `/` — Update Employee Salary Structure

**Request Body:** `UpdateSalaryStructureDto`

```json
{
  "earningComponents": [
    {
      "name": "Basic Salary",
      "amount": 35000,
      "isFixed": true,
      "isTemp": false,
      "componentType": "Fixed",
      "dependOn": null
    }
  ],
  "deductionComponents": [
    {
      "name": "PF",
      "amount": 4200,
      "isFixed": true,
      "isTemp": false,
      "componentType": "Percentage",
      "dependOn": "Basic"
    }
  ]
}
```

**Validation Rules:**
- Each component must have a non-empty `name`.
- `amount` cannot be negative.
- `dependOn` is **required** when `componentType` is `Percentage`.

**Response:** `200 OK`

```json
{
  "message": "Salary structure updated successfully"
}
```

---

### 7. Run Payroll

Preview and lock (finalize) monthly payroll for all active employees.

**Route:** `/api/payroll/run`

#### Endpoints

| Method | Path | Permission | Description |
|---|---|---|---|
| `GET` | `/` | `PayrollManagement_RunPayroll.View` | Preview payroll for a month |
| `POST` | `/` | `PayrollManagement_RunPayroll.Add` | Lock (finalize) payroll for a month |

#### GET `/` — Preview Payroll

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `month` | `string` | ✅ | — | Month in `MMMM-YYYY` format (e.g., `JANUARY-2025`) |
| `page` | `int` | No | `0` | Page index (0-based) |
| `pageSize` | `int` | No | `50` | Items per page (10–100) |

**Response:** `200 OK` → `RunPayrollResponse`

```json
{
  "isGenerated": false,
  "totalCount": 25,
  "currentPage": 0,
  "pageSize": 50,
  "totalPages": 1,
  "items": [
    {
      "employeeName": "John Doe",
      "employeeIdentifier": "EMP001",
      "isTeaching": true,
      "basicSalary": 30000.0,
      "grossSalary": 45000.0,
      "totalDeduction": 5400.0,
      "netPay": 39600.0,
      "presentDays": 22.0,
      "halfWorkingDays": 0.0,
      "totalWorkingDays": 22.0,
      "earningComponents": [
        { "name": "Basic Salary", "amount": 30000.0 },
        { "name": "HRA", "amount": 12000.0 }
      ],
      "deductionComponents": [
        { "name": "PF", "amount": 3600.0 },
        { "name": "PT", "amount": 200.0 }
      ]
    }
  ]
}
```

#### POST `/` — Lock Payroll

**Request Body:** `RunPayrollRequest`

```json
{
  "month": "JANUARY-2025"
}
```

**Response:** `200 OK` → `PayrollLockResponse`

```json
{
  "message": "Payroll locked successfully",
  "month": "JANUARY-2025",
  "wasAlreadyLocked": false,
  "employeesProcessed": 25,
  "employeesSkipped": 0,
  "employeesFailed": 0,
  "errors": null
}
```

---

### 8. Salary Slips

Retrieve finalized salary slips for individual employees after payroll is locked.

**Route:** `/api/payroll/salary-slips`

#### Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/` | Get salary slip for an employee for a given month |

> No specific permission attribute beyond `[Authorize]` at class level.

#### GET `/` — Get Salary Slip

**Query Parameters:**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `employeeId` | `Guid` | ✅ | Employee identifier |
| `monthYear` | `string` | ✅ | Month in `MMMM-YYYY` format (e.g., `JANUARY-2025`) |

**Response:** `200 OK` → `SalarySlipResponse`

```json
{
  "employeeId": "guid",
  "monthYear": "JANUARY-2025",
  "totalEarning": 45000.0,
  "totalDeduction": 5400.0,
  "netPay": 39600.0,
  "earnings": [
    {
      "name": "Basic Salary",
      "description": "Base salary",
      "amount": 30000.0,
      "isFixed": true,
      "isTemp": false,
      "componentType": "FIXED",
      "dependOn": null
    }
  ],
  "deductions": [
    {
      "name": "PF",
      "description": "Provident Fund",
      "amount": 3600.0,
      "isFixed": true,
      "isTemp": false,
      "componentType": "PERCENTAGE",
      "dependOn": "BASIC"
    }
  ],
  "generatedAt": "2025-01-31T18:00:00Z"
}
```

---

## Enumerations

### EarningComponentType / DeductionComponentType

| Value | Int | Description |
|---|---|---|
| `Fixed` | `0` | Fixed amount component |
| `Variable` | `1` | Percentage-based / variable component |

### CalculationType

| Value | Int | Description |
|---|---|---|
| `Amount` | `0` | Flat amount |
| `Percentage` | `1` | Percentage of a dependent component |

### ComponentDependency

| Value | Int | Description |
|---|---|---|
| `None` | `0` | No dependency (fixed amount) |
| `Basic` | `1` | Depends on Basic Salary |
| `Gross` | `2` | Depends on Gross Salary |
| `CustomComponent` | `3` | Depends on another custom component |

### SalaryComponentType (Employee-level)

| Value | Description |
|---|---|
| `Fixed` | Fixed amount |
| `Percentage` | Percentage-based |

### SalaryComponentDependency (Employee-level)

| Value | Description |
|---|---|
| `Basic` | Based on Basic Salary |
| `Gross` | Based on Gross Salary |

### PayElementType

| Value | Int | Description |
|---|---|---|
| `Credit` | `0` | Added to employee pay |
| `Debit` | `1` | Deducted from employee pay |

### DayCalculationType

| Value | Int | Description |
|---|---|---|
| `WorkingDays` | `0` | Business days (excludes weekends/holidays) |
| `TotalMonthDays` | `1` | Calendar days in the month |
| `ManualDays` | `2` | Admin-specified number of days |

---

## Shared DTOs

### `ComponentCreateResponseDto`

Returned on successful creation of earning/deduction components.

```json
{
  "id": "guid",
  "createdAt": "ISO-8601 datetime"
}
```

### `CustomComponentDto`

Nested in earning/deduction component responses when `ComponentDependency` is `CustomComponent`.

```json
{
  "id": "guid",
  "componentName": "Special Allowance",
  "componentOf": "Earning",
  "description": "...",
  "visibleInPayslip": true
}
```

### `DependentPropertiesDto` (Pay Elements)

```json
{
  "elementId": "guid-of-parent-element",
  "percentage": 40.0,
  "amount": null
}
```

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                        HttpApi Layer                             │
│  Controllers/HR/Payroll/                                         │
│  ├── EarningComponentController          (CRUD)                  │
│  ├── DeductionComponentController        (CRUD)                  │
│  ├── PayElementController                (CRUD)                  │
│  ├── PfRuleController                    (CRUD)                  │
│  ├── DayCalculationController            (Save)                  │
│  ├── SalaryStructure/                                            │
│  │   └── EmployeeSalaryStructureController (Get, Update)         │
│  ├── RunPayroll/                                                 │
│  │   └── RunPayrollController            (Preview, Lock)         │
│  └── SalarySlips/                                                │
│      └── SalarySlipController            (Get)                   │
├──────────────────────────────────────────────────────────────────┤
│                    Application Layer                              │
│  Application/HR/Payroll/                                         │
│  ├── Settings/                                                   │
│  │   ├── EarningComponentAppService                              │
│  │   ├── DeductionComponentAppService                            │
│  │   ├── PayElementAppService                                    │
│  │   ├── ProvidentFundAppService                                 │
│  │   ├── ProfessionalTaxAppService       (no controller yet)     │
│  │   └── DayCalculationSettingAppService                         │
│  ├── SalaryStructure/                                            │
│  │   └── EmployeeSalaryStructureAppService                       │
│  ├── RunPayroll/                                                 │
│  │   └── RunPayrollAppService                                    │
│  └── SalarySlips/                                                │
│      └── SalarySlipAppService                                    │
├──────────────────────────────────────────────────────────────────┤
│                      Domain Layer                                 │
│  Domain/HR/Payroll/                                              │
│  ├── Settings/     (Aggregates, Managers, Repositories)          │
│  └── SalarySlips/  (Aggregates, Managers, Repositories)          │
├──────────────────────────────────────────────────────────────────┤
│                  Infrastructure (EF Core)                         │
│  EntityFrameworkCore/HR/Payroll/                                  │
│  ├── Settings/     (Configurations, EfCore Repositories)         │
│  └── SalarySlips/  (Configurations, EfCore Repositories)         │
└──────────────────────────────────────────────────────────────────┘
```

---

## Not Yet Implemented

The following areas have domain/DTO definitions but are **not yet exposed as API endpoints**:

| Feature | Domain Entity | App Service | Controller | Notes |
|---|---|---|---|---|
| **Professional Tax** | `ProfessionalTax` aggregate | `ProfessionalTaxAppService` | ❌ None | DTOs (`CreateUpdateProfessionalTaxDto`, `ProfessionalTaxDto`) and repository exist. Slab-based tax by gender and salary range. |
