# Deduction Components API - Quick Reference

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/payroll/deduction-components` | List all deduction components (paginated) |
| GET | `/api/payroll/deduction-components/active` | Get all active components (cached) |
| GET | `/api/payroll/deduction-components/{id}` | Get component by ID |
| POST | `/api/payroll/deduction-components` | Create new component |
| PUT | `/api/payroll/deduction-components/{id}` | Update component |
| DELETE | `/api/payroll/deduction-components/{id}` | Delete component |
| POST | `/api/payroll/deduction-components/{id}/activate` | Activate component |
| POST | `/api/payroll/deduction-components/{id}/deactivate` | Deactivate component |

---

## Request/Response Examples

### 1. GET - List All Deduction Components

**Request:**
```http
GET /api/payroll/deduction-components?page=1&limit=10&search=tax
```

**Query Parameters:**
| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| search | string | Filter by name | - |
| SkipCount | int | Items to skip | 0 |
| MaxResultCount | int | Items per page | 10 |
| Sorting | string | Sort expression | DisplayOrder ASC |

**Response (200 OK):**
```json
{
  "totalCount": 5,
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Provident Fund (PF)",
      "description": "Mandatory PF deduction",
      "componentType": 0,
      "dependOn": 0,
      "amount": "1800",
      "visibleInPayslip": true,
      "displayOrder": 1,
      "isActive": true,
      "creationTime": "2024-01-15T10:30:00Z"
    },
    {
      "id": "4ea95f64-5717-4562-b3fc-2c963f66afa7",
      "name": "Professional Tax",
      "description": "State professional tax",
      "componentType": 0,
      "dependOn": 2,
      "amount": "200",
      "visibleInPayslip": true,
      "displayOrder": 2,
      "isActive": true,
      "creationTime": "2024-01-15T10:31:00Z"
    }
  ]
}
```

### 2. POST - Add Deduction Component

**Request:**
```http
POST /api/payroll/deduction-components
Content-Type: application/json

{
  "name": "Health Insurance",
  "description": "Monthly health insurance deduction",
  "componentType": 0,
  "dependOn": 0,
  "amount": "500",
  "visibleInPayslip": true,
  "displayOrder": 4
}
```

**Response (201 Created):**
```json
{
  "id": "5fa95f64-5717-4562-b3fc-2c963f66afa8",
  "name": "Health Insurance",
  "description": "Monthly health insurance deduction",
  "componentType": 0,
  "dependOn": 0,
  "amount": "500",
  "visibleInPayslip": true,
  "displayOrder": 4,
  "isActive": true,
  "creationTime": "2024-01-15T10:32:00Z"
}
```

### 3. PUT - Update Deduction Component

**Request:**
```http
PUT /api/payroll/deduction-components/{id}
Content-Type: application/json

{
  "name": "Loan Recovery",
  "description": "Auto deducted loan repayment",
  "componentType": 1,
  "dependOn": 2,
  "amount": "10%",
  "visibleInPayslip": true,
  "displayOrder": 5
}
```

**Response (200 OK):**
```json
{
  "id": "5fa95f64-5717-4562-b3fc-2c963f66afa8",
  "name": "Loan Recovery",
  "description": "Auto deducted loan repayment",
  "componentType": 1,
  "dependOn": 2,
  "amount": "10%",
  "visibleInPayslip": true,
  "displayOrder": 5,
  "isActive": true,
  "lastModificationTime": "2024-01-15T10:35:00Z"
}
```

### 4. DELETE - Delete Deduction Component

**Request:**
```http
DELETE /api/payroll/deduction-components/{id}
```

**Response (204 No Content):**
```
No content returned
```

### 5. GET - Get Active Components (Cached)

**Request:**
```http
GET /api/payroll/deduction-components/active
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Provident Fund (PF)",
      "componentType": 0,
      "amount": "1800",
      "visibleInPayslip": true,
      "isActive": true
    }
  ]
}
```

---

## Enums

### DeductionComponentType
```csharp
public enum DeductionComponentType
{
    Fixed = 0,      // Fixed amount (e.g., "1800")
    Variable = 1    // Percentage-based (e.g., "10%")
}
```

### ComponentDependency
```csharp
public enum ComponentDependency
{
    None = 0,   // No dependency
    Basic = 1,  // Depends on Basic Salary
    Gross = 2   // Depends on Gross Salary
}
```

---

## Validation Rules

### Required Fields

| Field | Type | Required | Max Length | Notes |
|-------|------|----------|------------|-------|
| name | string | ✔ | 200 | Must be unique |
| description | string | ❌ | 500 | Optional |
| componentType | enum | ✔ | - | 0=Fixed, 1=Variable |
| dependOn | enum | ✔ | - | 0=None, 1=Basic, 2=Gross |
| amount | string | ✔ | 20 | Numeric or percentage |
| visibleInPayslip | boolean | ✔ | - | Default: true |
| displayOrder | int | ✔ | - | Default: 0 |

### Business Rules

1. **Name Uniqueness**: Each name must be unique per tenant (case-insensitive)
2. **Amount Format**:
   - Fixed + None: Must be numeric (e.g., "500")
   - Variable/Dependent: Can be numeric or percentage (e.g., "10%" or "500")
3. **Percentage Range**: If percentage, must be 0-100
4. **Numeric Values**: If numeric, must be ≥ 0

---

## Error Responses

### 400 - Bad Request (Duplicate Name)
```json
{
  "error": {
    "code": "ShikshaMitra:DeductionComponentNameAlreadyExists",
    "message": "Component name already exists",
    "data": {
      "name": "Provident Fund"
    }
  }
}
```

### 404 - Not Found
```json
{
  "error": {
    "code": "Volo.Abp.Domain.Entities.EntityNotFoundException",
    "message": "Deduction component not found",
    "data": {}
  }
}
```

### 422 - Validation Error (Invalid Amount)
```json
{
  "error": {
    "code": "ShikshaMitra:InvalidAmount",
    "message": "Amount must be a valid non-negative number for fixed components.",
    "validationErrors": [
      {
        "message": "Amount must be a valid non-negative number",
        "members": ["amount"]
      }
    ]
  }
}
```

---

## Sample Deductions (Seeded Data)

| Name | Type | Depends On | Amount | Visible | Description |
|------|------|------------|--------|---------|-------------|
| Provident Fund (PF) | Fixed | None | ₹1,800 | ✅ | Mandatory PF contribution |
| Professional Tax | Fixed | None | ₹200 | ✅ | State professional tax |
| Income Tax (TDS) | Variable | Gross | 10% | ✅ | Tax deducted at source |
| Health Insurance | Fixed | None | ₹500 | ✅ | Health insurance premium |
| Loan Recovery | Variable | Gross | 5% | ✅ | Employee loan repayment |
| ESI | Variable | Gross | 0.75% | ✅ | Employee State Insurance |
| Canteen Charges | Fixed | None | ₹300 | ❌ | Canteen facility charges |

---

## Example Payroll Calculation

**Gross Salary**: ₹62,500

| Deduction | Calculation | Amount |
|-----------|-------------|--------|
| Provident Fund | Fixed | ₹1,800 |
| Professional Tax | Fixed | ₹200 |
| Income Tax (10% of Gross) | 62,500 × 10% | ₹6,250 |
| Health Insurance | Fixed | ₹500 |
| ESI (0.75% of Gross) | 62,500 × 0.75% | ₹469 |
| **Total Deductions** | | **₹9,219** |
| **Net Salary** | 62,500 - 9,219 | **₹53,281** |

---

## Quick Start

### 1. Run Migration
```bash
dotnet ef migrations add AddDeductionComponentsTable \
  --project src/ShikshaMitra.EntityFrameworkCore \
  --startup-project src/ShikshaMitra.DbMigrator

dotnet ef database update \
  --project src/ShikshaMitra.EntityFrameworkCore \
  --startup-project src/ShikshaMitra.DbMigrator
```

### 2. Run Seeder
```bash
cd src/ShikshaMitra.DbMigrator
dotnet run
```

### 3. Test API
```bash
# Get all active deductions
curl -X GET "https://localhost:44300/api/payroll/deduction-components/active"

# Create new deduction
curl -X POST "https://localhost:44300/api/payroll/deduction-components" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Custom Deduction",
    "componentType": 0,
    "dependOn": 0,
    "amount": "100",
    "visibleInPayslip": true
  }'
```

---

## Features

✅ Full CRUD operations  
✅ Name uniqueness validation  
✅ Amount format validation  
✅ Distributed caching (24-hour TTL)  
✅ Soft delete (Activate/Deactivate)  
✅ Multi-tenancy support  
✅ Audit trail  
✅ Pagination & sorting  
✅ Database indexes  
✅ Sample seed data (7 components)

---

## Database Schema

```sql
CREATE TABLE AppDeductionComponents (
    Id                    UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NULL,
    Name                  NVARCHAR(200) NOT NULL,
    Description           NVARCHAR(500) NULL,
    ComponentType         INT NOT NULL,
    DependOn              INT NOT NULL,
    Amount                NVARCHAR(20) NOT NULL,
    VisibleInPayslip      BIT NOT NULL DEFAULT 1,
    DisplayOrder          INT NOT NULL DEFAULT 0,
    IsActive              BIT NOT NULL DEFAULT 1,
    CreationTime          DATETIME2 NOT NULL,
    CreatorId             UNIQUEIDENTIFIER NULL,
    LastModificationTime  DATETIME2 NULL,
    LastModifierId        UNIQUEIDENTIFIER NULL,
    IsDeleted             BIT NOT NULL DEFAULT 0,
    DeleterId             UNIQUEIDENTIFIER NULL,
    DeletionTime          DATETIME2 NULL,
    
    CONSTRAINT UQ_DeductionComponents_Name UNIQUE (Name)
);

CREATE INDEX IX_DeductionComponents_TenantId_IsActive 
    ON AppDeductionComponents(TenantId, IsActive);
    
CREATE INDEX IX_DeductionComponents_DisplayOrder 
    ON AppDeductionComponents(DisplayOrder);
```

---

**Status**: ✅ Complete and Ready  
**Build**: ✅ Success  
**Next**: Run migration and test the API
