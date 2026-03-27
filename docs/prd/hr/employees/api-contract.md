# Employees -- API Contract

## Authentication

All endpoints require a Bearer token and `__tenant` header.

---

## Endpoints Overview

### Employee List & Search (`api/employees`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/employees` | List employees (paginated, filterable) |
| GET | `/api/employees/{employeeId}` | Get employee details |
| GET | `/api/employees/search` | Search employees by name/code |

### Personal Info (`api/employees`)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/employees/personal-info` | Create employee |
| GET | `/api/employees/{employeeId}/personal-info` | Get personal info |
| PUT | `/api/employees/{employeeId}/personal-info` | Update personal info |

### Family Info

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/employees/{employeeId}/family-info` | Get family info |
| PUT | `/api/employees/{employeeId}/family-info` | Upsert family info |

### Work Experience

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/employees/{employeeId}/work-experience` | Get work experience list |
| PUT | `/api/employees/{employeeId}/work-experience` | Replace work experience list |

### Additional Info

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/employees/{employeeId}/additional-info` | Get additional details |
| PUT | `/api/employees/{employeeId}/additional-info` | Update additional details |

### Employee Payroll

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/employees/{employeeId}/payroll` | Get payroll details |
| PUT | `/api/employees/{employeeId}/payroll` | Update payroll details |

### Reporting Manager

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/employees/{employeeId}/reporting-manager` | Get reporting manager |
| PUT | `/api/employees/{employeeId}/reporting-manager` | Assign reporting manager |
| GET | `/api/employees/reporting-manager/{managerId}/employees` | List employees under manager |

### Document Configuration (`api/employees/documents`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/employees/documents` | List document settings (paginated) |
| GET | `/api/employees/documents/{documentId}` | Get document setting |
| POST | `/api/employees/documents` | Create document setting |
| PUT | `/api/employees/documents/{documentId}` | Update document setting |
| DELETE | `/api/employees/documents/{documentId}` | Delete document setting |

---

## Endpoint Details

### GET /api/employees

List employees with pagination and filtering.

**Query params:**

| Param | Type | Required | Default | Constraints |
|-------|------|----------|---------|-------------|
| mediumStageId | Guid | no | | |
| status | string | no | | Active/Inactive/Left/Retired |
| page | int | no | 1 | >= 1 |
| pageSize | int | no | 10 | 10-50 |
| isTeaching | bool | no | | |
| roleId | Guid | no | | |

**Response (200):**

```
EmployeeListResponse {
  totalCount: int
  currentPage: int
  pageSize: int
  totalPages: int
  items: EmployeeSummary[]
}

EmployeeSummary {
  id: Guid
  roleName: string?
  fullName: string?
  thumbnailPicture: string?
  employeeIdentifier: string?
  dob: DateTime?
  gender: string?
  doj: DateTime?
  email: string?
  mobileNo: string?
  isTeaching: bool
  shift: { id: Guid, name: string, startTime: string, endTime: string }?
  status: int                    // 0=Active, 1=Inactive, 2=Left, 3=Retired
}
```

---

### GET /api/employees/{employeeId}

**Path params:** `employeeId` (Guid, required)

**Response (200):** `EmployeeDetails` (same shape as `EmployeeSummary`)

**Status codes:** 200, 403 (IDOR), 404

---

### GET /api/employees/search

**Query params:**

| Param | Type | Required | Constraints |
|-------|------|----------|-------------|
| search | string | no | minLength 3 |

**Response (200):** `EmployeeSearchResponse { items: EmployeeSearchItem[] }` (max 50 results)

---

### POST /api/employees/personal-info

Create a new employee.

**Request body -- `PersonalInfoRequest`:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| firstName | string | yes | max 256 |
| middleName | string | no | max 256 |
| lastName | string | yes | max 256 |
| shortName | string | no | max 128 |
| dob | DateTime | no | must be past; age >= 18 at DOJ |
| gender | int | no | 1=Male, 2=Female |
| religion | string | no | max 128 |
| casteType | int | no | 0=General, 1=OBC, 2=SC, 3=ST |
| familyCaste | string | no | max 128 |
| maritalStatus | int | no | 0=Unmarried, 1=Married, 2=Divorced, 3=Widowed |
| bloodGroup | int | no | 1=A+ .. 8=O- |
| email | string | **yes** (create) | valid email, max 256; required when creating an employee (identity + welcome flow) |
| mobileNo | string | yes | `^[6-9]\d{9}$`, max 15 |
| secondaryMobileNo | string | no | max 15; if set, must use a different number than `mobileNo` (digits compared after normalization) |
| doj | DateTime | yes | <= 1 year in future |
| isTeaching | bool | yes | |
| department | string | no | |
| roleId | Guid | yes | |
| shiftId | Guid | yes | |
| address1-3 | string | no | max 512 each |
| country | { name, isoCode } | no | |
| state | { name, isoCode } | no | |
| city | string | no | max 128 |
| pinCode | string | no | `^\d{6}$` |

**Response (201):**

```
PersonalInfoResponse {
  id: Guid
  employeeCode: string
  fullName: string
  ...all PersonalInfoRequest fields
}
```

**Status codes:** 201, 400 (validation or business rule), 403 (missing HR employee-create permission)

**Error response (typical):** HTTP 4xx with JSON body:

```
{
  "isSuccess": false,
  "error": {
    "code": "VALIDATION_ERROR" | "BUSINESS_ERROR" | ...,
    "message": "human-readable summary",
    "fields": { "mobileNo": "...", "email": "..." }
  }
}
```

`fields` is present for many validation failures (per-property messages); it may be omitted when not applicable.

---

### GET /api/employees/{employeeId}/personal-info

**Response (200):** `PersonalInfoResponse` (same as create response)

**Status codes:** 200, 403, 404

---

### PUT /api/employees/{employeeId}/personal-info

**Request body:** `PersonalInfoRequest` (same shape as create, relaxed required fields)

**Status codes:** 200, 400 (validation or business rule), 403, 404

---

### GET /api/employees/{employeeId}/family-info

**Response (200):**

```
FamilyInfoResponse {
  id: Guid
  fatherDetails: FamilyMemberInfo?
  motherDetails: FamilyMemberInfo?
  spouseDetails: FamilyMemberInfo?
}

FamilyMemberInfo {
  name: string?          // max 256
  occupation: string?    // max 256
  mobileNo: string?      // max 15
  address1-3: string?    // max 512
  country: { name, isoCode }?
  state: { name, isoCode }?
  city: string?          // max 128
  pincode: string?       // max 10
}
```

---

### PUT /api/employees/{employeeId}/family-info

**Request body:** `FamilyInfoRequest { id: Guid?, fatherDetails?, motherDetails?, spouseDetails? }`

Null or empty-name member = delete existing record.

**Status codes:** 204, 403, 404

---

### GET /api/employees/{employeeId}/work-experience

**Response (200):** `List<WorkExperienceContract>`

```
WorkExperienceContract {
  id: Guid?
  mediumStageName: string?   // max 256
  designation: string?       // max 256
  department: string?        // max 256
  country: { name, isoCode }?
  state: { name, isoCode }?
  city: string?              // max 128
  startDate: DateTime?
  endDate: DateTime?
}
```

---

### PUT /api/employees/{employeeId}/work-experience

**Request body:** `List<WorkExperienceContract>` (replaces entire list)

**Status codes:** 200, 404

---

### GET /api/employees/{employeeId}/additional-info

**Response (200):**

```
AdditionalInfoContract {
  bplCardNo: string?         // max 50
  personWithDisability: bool
  secondLanguage: string?    // max 100
  isLibraryMember: bool
  isTransportMember: bool
}
```

---

### PUT /api/employees/{employeeId}/additional-info

**Request body:** `AdditionalInfoContract` (same shape)

**Status codes:** 200, 404

---

### GET /api/employees/{employeeId}/payroll

**Response (200):**

```
PayrollContract {
  bankDetails: {
    bankName: string?
    branchName: string?
    accountHolderName: string?
    accountNumber: string?        // ^\d{9,18}$
    ifscCode: string?             // ^[A-Z]{4}0[A-Z0-9]{6}$
  }?
  salaryDetails: {
    pfRuleId: Guid?
    epfNumber: string?
    uanNumber: string?
    basicSalary: decimal?
    punchId: string?
    tdsPercentage: decimal?       // 0-100
    tdsAmount: decimal?
  }?
  earningComponents: [{
    componentId: Guid
    amount: decimal               // >= 0
  }]?
  deductionComponents: [{
    componentId: Guid
    amount: decimal               // >= 0
  }]?
}
```

**Status codes:** 200, 403 (ownership required), 404

---

### PUT /api/employees/{employeeId}/payroll

**Request body:** `PayrollContract` (same shape)

**Status codes:** 200, 403 (sensitive field access), 404

---

### PUT /api/employees/{employeeId}/reporting-manager

**Request body:** `{ reportingManagerId: Guid }`

**Status codes:** 200, 404

---

### GET /api/employees/{employeeId}/reporting-manager

**Response (200):**

```
ReportingManagerResponse {
  employeeId: Guid
  reportingManager: {
    id: Guid
    employeeIdentifier: string
    fullName: string
    designation: string
  }?
}
```

---

### GET /api/employees/reporting-manager/{managerId}/employees

**Response (200):**

```
EmployeesUnderManagerResponse {
  manager: { id, employeeIdentifier, fullName, designation }
  employees: [{ id, employeeIdentifier, fullName, designation }]
}
```

---

### POST /api/employees/documents

**Request body -- `DocumentRequest`:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| title | string | yes | `^[A-Za-z ]+$` |
| documentSize | int | yes | 1-20 (MB) |
| documentTypes | int[] | yes | minLength 1; values: 0=Jpg, 1=Jpeg, 2=Png, 3=Docx, 4=Pdf, 5=Xlsx |

**Response (201):** `{ id: Guid }`

---

### GET /api/employees/documents

**Query params:** `page` (int, default 1), `pageSize` (int, default 10)

**Response (200):**

```
DocumentListResponse {
  totalCount: int
  currentPage: int
  pageSize: int
  totalPages: int
  items: [{ id: Guid, title: string, documentSize: int, documentTypes: int[] }]
}
```

---

### GET /api/employees/documents/{documentId}

**Response (200):** `{ id, title, documentSize, documentTypes }`

---

### PUT /api/employees/documents/{documentId}

**Request body:** `DocumentRequest` (same as create)

**Status codes:** 204, 404

---

### DELETE /api/employees/documents/{documentId}

**Status codes:** 204, 404
