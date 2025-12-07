# Earning Components API Documentation

## Overview
The Earning Components API allows you to manage master data for payroll earning components such as Basic Salary, HRA, Transport Allowance, etc. These components serve as templates that can be used when configuring employee payroll.

## Base URL
```
/api/payroll/earning-components
```

---

## API Endpoints

### 1. GET - List All Earning Components (Paginated)

Retrieve a paginated list of all earning components.

**Endpoint**
```http
GET /api/payroll/earning-components
```

**Query Parameters**
| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| SkipCount | int | ❌ | 0 | Number of records to skip |
| MaxResultCount | int | ❌ | 10 | Maximum number of records to return |
| Sorting | string | ❌ | "DisplayOrder ASC, Name ASC" | Sort order (e.g., "Name DESC") |

**Response (200 OK)**
```json
{
  "totalCount": 5,
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Basic Salary",
      "description": "Base salary component",
      "componentType": 0,
      "dependOn": 0,
      "amount": "30000",
      "visibleInPayslip": true,
      "displayOrder": 1,
      "isActive": true,
      "creationTime": "2024-01-15T10:30:00Z",
      "creatorId": null,
      "lastModificationTime": null,
      "lastModifierId": null
    },
    {
      "id": "2ba85f64-5717-4562-b3fc-2c963f66afa7",
      "name": "House Rent Allowance",
      "description": "HRA component based on basic salary",
      "componentType": 1,
      "dependOn": 1,
      "amount": "40%",
      "visibleInPayslip": true,
      "displayOrder": 2,
      "isActive": true,
      "creationTime": "2024-01-15T10:31:00Z",
      "creatorId": null,
      "lastModificationTime": null,
      "lastModifierId": null
    }
  ]
}
```

**Sorting Examples**
```
?Sorting=Name ASC
?Sorting=DisplayOrder DESC
?Sorting=ComponentType ASC, Name DESC
```

---

### 2. GET - All Active Earning Components (Not Paginated)

Retrieve all active earning components without pagination. **This endpoint uses caching for better performance.**

**Endpoint**
```http
GET /api/payroll/earning-components/active
```

**Response (200 OK)**
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Basic Salary",
      "description": "Base salary component",
      "componentType": 0,
      "dependOn": 0,
      "amount": "30000",
      "visibleInPayslip": true,
      "displayOrder": 1,
      "isActive": true,
      "creationTime": "2024-01-15T10:30:00Z"
    }
  ]
}
```

**Cache Details**
- Cache Duration: 24 hours
- Cache Key: `EarningComponents:Active`
- Cache is automatically invalidated on Create/Update/Delete/Activate/Deactivate operations

---

### 3. GET - Get Single Earning Component

Retrieve a specific earning component by ID.

**Endpoint**
```http
GET /api/payroll/earning-components/{id}
```

**Path Parameters**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| id | Guid | ✔ | Unique identifier of the earning component |

**Response (200 OK)**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Basic Salary",
  "description": "Base salary component",
  "componentType": 0,
  "dependOn": 0,
  "amount": "30000",
  "visibleInPayslip": true,
  "displayOrder": 1,
  "isActive": true,
  "creationTime": "2024-01-15T10:30:00Z",
  "creatorId": null,
  "lastModificationTime": null,
  "lastModifierId": null
}
```

**Error Response (404 Not Found)**
```json
{
  "error": {
    "code": "Volo.Abp.Domain.Entities.EntityNotFoundException",
    "message": "There is no such an entity. Entity type: ShikshaMitra.HR.Payroll.Settings.EarningComponent, id: {id}",
    "details": null
  }
}
```

---

### 4. POST - Create Earning Component

Create a new earning component.

**Endpoint**
```http
POST /api/payroll/earning-components
```

**Request Body**
```json
{
  "name": "Special Allowance",
  "description": "Additional allowance for employees",
  "componentType": 1,
  "dependOn": 2,
  "amount": "10%",
  "visibleInPayslip": false,
  "displayOrder": 5
}
```

**Request Body Schema**
| Field | Type | Required | Validation | Description |
|-------|------|----------|------------|-------------|
| name | string | ✔ | Max 200 chars, must be unique | Component name |
| description | string | ❌ | Max 500 chars | Optional description |
| componentType | int | ✔ | 0=Fixed, 1=Variable | Component type |
| dependOn | int | ✔ | 0=None, 1=Basic, 2=Gross | Dependency |
| amount | string | ✔ | Max 20 chars | Amount (numeric or percentage) |
| visibleInPayslip | bool | ❌ | Default: true | Show in payslip |
| displayOrder | int | ❌ | Default: 0 | Display order |

**Success Response (200 OK)**
```json
{
  "id": "4fa85f64-5717-4562-b3fc-2c963f66afa8",
  "name": "Special Allowance",
  "description": "Additional allowance for employees",
  "componentType": 1,
  "dependOn": 2,
  "amount": "10%",
  "visibleInPayslip": false,
  "displayOrder": 5,
  "isActive": true,
  "creationTime": "2024-01-15T11:00:00Z",
  "creatorId": "1fa85f64-5717-4562-b3fc-2c963f66afa1",
  "lastModificationTime": null,
  "lastModifierId": null
}
```

**Error Responses**

**400 Bad Request - Validation Error**
```json
{
  "error": {
    "code": "Volo.Abp.Validation.AbpValidationException",
    "message": "Request validation failed",
    "validationErrors": [
      {
        "message": "The Name field is required.",
        "members": ["Name"]
      }
    ]
  }
}
```

**400 Bad Request - Duplicate Name**
```json
{
  "error": {
    "code": "ShikshaMitra:EarningComponentNameAlreadyExists",
    "message": "An earning component with the name 'Basic Salary' already exists.",
    "data": {
      "name": "Basic Salary"
    }
  }
}
```

**400 Bad Request - Invalid Amount**
```json
{
  "error": {
    "code": "ShikshaMitra:InvalidAmount",
    "message": "Amount must be a valid non-negative number for fixed components.",
    "data": {
      "message": "Amount must be a valid non-negative number for fixed components."
    }
  }
}
```

---

### 5. PUT - Update Earning Component

Update an existing earning component.

**Endpoint**
```http
PUT /api/payroll/earning-components/{id}
```

**Path Parameters**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| id | Guid | ✔ | Unique identifier of the earning component |

**Request Body**
```json
{
  "name": "Transport Allowance",
  "description": "Monthly transport allowance",
  "componentType": 0,
  "dependOn": 0,
  "amount": "3000",
  "visibleInPayslip": true,
  "displayOrder": 3
}
```

**Success Response (200 OK)**
```json
{
  "id": "4fa85f64-5717-4562-b3fc-2c963f66afa8",
  "name": "Transport Allowance",
  "description": "Monthly transport allowance",
  "componentType": 0,
  "dependOn": 0,
  "amount": "3000",
  "visibleInPayslip": true,
  "displayOrder": 3,
  "isActive": true,
  "creationTime": "2024-01-15T11:00:00Z",
  "creatorId": "1fa85f64-5717-4562-b3fc-2c963f66afa1",
  "lastModificationTime": "2024-01-15T12:00:00Z",
  "lastModifierId": "1fa85f64-5717-4562-b3fc-2c963f66afa1"
}
```

**Error Responses**

Same as Create endpoint (404 if not found, 400 for validation errors)

---

### 6. DELETE - Remove Earning Component

Delete an earning component.

**Endpoint**
```http
DELETE /api/payroll/earning-components/{id}
```

**Path Parameters**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| id | Guid | ✔ | Unique identifier of the earning component |

**Success Response (204 No Content)**

No response body.

**Error Response (404 Not Found)**
```json
{
  "error": {
    "code": "Volo.Abp.Domain.Entities.EntityNotFoundException",
    "message": "There is no such an entity.",
    "details": null
  }
}
```

⚠️ **Warning**: Deleting an earning component that is referenced by employee payroll records may cause issues. Consider using Deactivate instead.

---

### 7. POST - Activate Earning Component

Activate an earning component.

**Endpoint**
```http
POST /api/payroll/earning-components/{id}/activate
```

**Path Parameters**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| id | Guid | ✔ | Unique identifier of the earning component |

**Success Response (200 OK)**

No response body.

---

### 8. POST - Deactivate Earning Component

Deactivate an earning component (soft delete).

**Endpoint**
```http
POST /api/payroll/earning-components/{id}/deactivate
```

**Path Parameters**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| id | Guid | ✔ | Unique identifier of the earning component |

**Success Response (200 OK)**

No response body.

---

## Data Models

### Enums

#### EarningComponentType
```csharp
public enum EarningComponentType
{
    Fixed = 0,      // Fixed amount (e.g., "30000")
    Variable = 1    // Variable/Percentage (e.g., "40%")
}
```

#### ComponentDependency
```csharp
public enum ComponentDependency
{
    None = 0,   // No dependency - standalone amount
    Basic = 1,  // Depends on Basic Salary
    Gross = 2   // Depends on Gross Salary
}
```

---

## Validation Rules

### Name
- ✔ Required
- ✔ Max length: 200 characters
- ✔ Must be unique (case-insensitive)

### Description
- ❌ Optional
- ✔ Max length: 500 characters

### ComponentType
- ✔ Required
- ✔ Valid values: 0 (Fixed), 1 (Variable)

### DependOn
- ✔ Required
- ✔ Valid values: 0 (None), 1 (Basic), 2 (Gross)

### Amount
- ✔ Required
- ✔ Max length: 20 characters
- ✔ Format validation:
  - **Fixed + None**: Must be numeric (e.g., "30000")
  - **Fixed + Basic/Gross**: Can be numeric or percentage (e.g., "10000" or "40%")
  - **Variable + Basic/Gross**: Can be numeric or percentage
  - Percentage must be 0-100
  - Numeric must be ≥ 0

### VisibleInPayslip
- ❌ Optional
- ✔ Default: true

### DisplayOrder
- ❌ Optional
- ✔ Default: 0

---

## Business Logic

### Component Type & Dependency Rules

| Component Type | Depend On | Amount Format | Example |
|----------------|-----------|---------------|---------|
| Fixed | None | Numeric only | "30000" |
| Fixed | Basic | Numeric or % | "15000" or "50%" |
| Fixed | Gross | Numeric or % | "5000" or "10%" |
| Variable | None | Numeric only | "5000" |
| Variable | Basic | Numeric or % | "2000" or "20%" |
| Variable | Gross | Numeric or % | "1000" or "5%" |

### Common Components Examples

#### 1. Basic Salary
```json
{
  "name": "Basic Salary",
  "componentType": 0,
  "dependOn": 0,
  "amount": "30000",
  "visibleInPayslip": true
}
```

#### 2. HRA (40% of Basic)
```json
{
  "name": "House Rent Allowance",
  "componentType": 1,
  "dependOn": 1,
  "amount": "40%",
  "visibleInPayslip": true
}
```

#### 3. Transport Allowance (Fixed)
```json
{
  "name": "Transport Allowance",
  "componentType": 0,
  "dependOn": 0,
  "amount": "3000",
  "visibleInPayslip": true
}
```

#### 4. Special Allowance (10% of Gross)
```json
{
  "name": "Special Allowance",
  "componentType": 1,
  "dependOn": 2,
  "amount": "10%",
  "visibleInPayslip": false
}
```

---

## Usage Examples

### cURL Examples

#### Get All Components (Paginated)
```bash
curl -X GET "https://localhost:44300/api/payroll/earning-components?SkipCount=0&MaxResultCount=10" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

#### Get Active Components
```bash
curl -X GET "https://localhost:44300/api/payroll/earning-components/active" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

#### Create Component
```bash
curl -X POST "https://localhost:44300/api/payroll/earning-components" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "name": "Medical Allowance",
    "description": "Medical expenses allowance",
    "componentType": 0,
    "dependOn": 0,
    "amount": "2500",
    "visibleInPayslip": true,
    "displayOrder": 4
  }'
```

#### Update Component
```bash
curl -X PUT "https://localhost:44300/api/payroll/earning-components/{id}" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "name": "Medical Allowance Updated",
    "description": "Updated medical expenses allowance",
    "componentType": 0,
    "dependOn": 0,
    "amount": "3000",
    "visibleInPayslip": true,
    "displayOrder": 4
  }'
```

#### Delete Component
```bash
curl -X DELETE "https://localhost:44300/api/payroll/earning-components/{id}" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

#### Deactivate Component (Recommended over Delete)
```bash
curl -X POST "https://localhost:44300/api/payroll/earning-components/{id}/deactivate" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### TypeScript/JavaScript Example

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface EarningComponentDto {
  id: string;
  name: string;
  description?: string;
  componentType: EarningComponentType;
  dependOn: ComponentDependency;
  amount: string;
  visibleInPayslip: boolean;
  displayOrder: number;
  isActive: boolean;
}

export enum EarningComponentType {
  Fixed = 0,
  Variable = 1
}

export enum ComponentDependency {
  None = 0,
  Basic = 1,
  Gross = 2
}

@Injectable({ providedIn: 'root' })
export class EarningComponentService {
  private apiUrl = '/api/payroll/earning-components';

  constructor(private http: HttpClient) {}

  getAll(skipCount = 0, maxResultCount = 10): Observable<PagedResultDto<EarningComponentDto>> {
    return this.http.get<PagedResultDto<EarningComponentDto>>(
      `${this.apiUrl}?SkipCount=${skipCount}&MaxResultCount=${maxResultCount}`
    );
  }

  getAllActive(): Observable<ListResultDto<EarningComponentDto>> {
    return this.http.get<ListResultDto<EarningComponentDto>>(`${this.apiUrl}/active`);
  }

  get(id: string): Observable<EarningComponentDto> {
    return this.http.get<EarningComponentDto>(`${this.apiUrl}/${id}`);
  }

  create(input: CreateEarningComponentDto): Observable<EarningComponentDto> {
    return this.http.post<EarningComponentDto>(this.apiUrl, input);
  }

  update(id: string, input: UpdateEarningComponentDto): Observable<EarningComponentDto> {
    return this.http.put<EarningComponentDto>(`${this.apiUrl}/${id}`, input);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  activate(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/activate`, {});
  }

  deactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/deactivate`, {});
  }
}
```

### C# Client Example

```csharp
using ShikshaMitra.HR.Payroll.Settings;

public class EarningComponentExample
{
    private readonly IEarningComponentAppService _earningComponentAppService;

    public EarningComponentExample(IEarningComponentAppService earningComponentAppService)
    {
        _earningComponentAppService = earningComponentAppService;
    }

    public async Task CreateBasicSalaryComponent()
    {
        var input = new CreateEarningComponentDto
        {
            Name = "Basic Salary",
            Description = "Base salary component",
            ComponentType = EarningComponentType.Fixed,
            DependOn = ComponentDependency.None,
            Amount = "30000",
            VisibleInPayslip = true,
            DisplayOrder = 1
        };

        var result = await _earningComponentAppService.CreateAsync(input);
        Console.WriteLine($"Created component with ID: {result.Id}");
    }

    public async Task GetAllActiveComponents()
    {
        var result = await _earningComponentAppService.GetAllActiveAsync();
        foreach (var component in result.Items)
        {
            Console.WriteLine($"{component.Name}: {component.Amount}");
        }
    }
}
```

---

## Performance Considerations

### Caching
- **Active Components**: Cached for 24 hours to improve performance
- **Cache Invalidation**: Automatic on any Create/Update/Delete operation
- **Cache Key**: `EarningComponents:Active`

### Database Indexes
The following indexes are automatically created for optimal performance:

1. **Unique Index on Name**: Fast lookup and uniqueness check
2. **Index on (TenantId, IsActive)**: Efficient filtering of active components
3. **Index on DisplayOrder**: Fast sorting by display order

### Recommendations
- Use `GET /api/payroll/earning-components/active` for dropdown lists and lookups (cached)
- Use paginated endpoint for admin screens with large datasets
- Set appropriate `DisplayOrder` for UI presentation

---

## Security & Permissions

### Recommended Permissions
- `Payroll.EarningComponents.View`: View components
- `Payroll.EarningComponents.Create`: Create new components
- `Payroll.EarningComponents.Edit`: Update existing components
- `Payroll.EarningComponents.Delete`: Delete components

### Multi-Tenancy
- All components are tenant-specific (isolated per tenant)
- Name uniqueness is enforced per tenant

---

## Database Schema

### Table: AppEarningComponents

| Column | Type | Nullable | Default | Constraints |
|--------|------|----------|---------|-------------|
| Id | Guid | No | - | Primary Key |
| TenantId | Guid | Yes | - | Multi-tenant isolation |
| Name | nvarchar(200) | No | - | Unique index |
| Description | nvarchar(500) | Yes | - | - |
| ComponentType | int | No | - | 0=Fixed, 1=Variable |
| DependOn | int | No | - | 0=None, 1=Basic, 2=Gross |
| Amount | nvarchar(20) | No | - | - |
| VisibleInPayslip | bit | No | true | - |
| DisplayOrder | int | No | 0 | - |
| IsActive | bit | No | true | Soft delete flag |
| CreationTime | datetime | No | GETUTCDATE() | - |
| CreatorId | Guid | Yes | - | - |
| LastModificationTime | datetime | Yes | - | - |
| LastModifierId | Guid | Yes | - | - |
| IsDeleted | bit | No | false | ABP soft delete |
| DeleterId | Guid | Yes | - | - |
| DeletionTime | datetime | Yes | - | - |

---

## Testing

### Unit Test Example

```csharp
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ShikshaMitra.HR.Payroll.Settings
{
    public class EarningComponentAppService_Tests : ShikshaMitraApplicationTestBase
    {
        private readonly IEarningComponentAppService _earningComponentAppService;

        public EarningComponentAppService_Tests()
        {
            _earningComponentAppService = GetRequiredService<IEarningComponentAppService>();
        }

        [Fact]
        public async Task Should_Create_Earning_Component()
        {
            // Arrange
            var input = new CreateEarningComponentDto
            {
                Name = "Test Component",
                Description = "Test Description",
                ComponentType = EarningComponentType.Fixed,
                DependOn = ComponentDependency.None,
                Amount = "10000",
                VisibleInPayslip = true,
                DisplayOrder = 1
            };

            // Act
            var result = await _earningComponentAppService.CreateAsync(input);

            // Assert
            result.ShouldNotBeNull();
            result.Name.ShouldBe("Test Component");
            result.Amount.ShouldBe("10000");
            result.IsActive.ShouldBeTrue();
        }

        [Fact]
        public async Task Should_Not_Create_Duplicate_Name()
        {
            // Arrange
            await CreateTestComponent("Duplicate Name");

            // Act & Assert
            await Should.ThrowAsync<BusinessException>(async () =>
            {
                await CreateTestComponent("Duplicate Name");
            });
        }

        [Fact]
        public async Task Should_Get_All_Active_Components()
        {
            // Arrange
            await CreateTestComponent("Active 1");
            await CreateTestComponent("Active 2");

            // Act
            var result = await _earningComponentAppService.GetAllActiveAsync();

            // Assert
            result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        }

        private async Task<EarningComponentDto> CreateTestComponent(string name)
        {
            return await _earningComponentAppService.CreateAsync(new CreateEarningComponentDto
            {
                Name = name,
                ComponentType = EarningComponentType.Fixed,
                DependOn = ComponentDependency.None,
                Amount = "1000",
                VisibleInPayslip = true
            });
        }
    }
}
```

---

## Troubleshooting

### Common Issues

#### 1. Duplicate Name Error
**Error**: "An earning component with the name 'X' already exists."
**Solution**: Use a unique name or update the existing component

#### 2. Invalid Amount Format
**Error**: "Amount must be a valid non-negative number"
**Solution**: 
- For Fixed components with DependOn=None, use numeric value (e.g., "30000")
- For dependent components, use numeric or percentage (e.g., "40%")

#### 3. Cache Not Updating
**Issue**: Changes not reflected in `/active` endpoint
**Solution**: Cache is automatically invalidated. If issue persists, check distributed cache configuration.

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-01-XX | Initial release with full CRUD operations |

---

## Support

For issues or questions:
- Email: support@shikshamitra.com
- Documentation: [API Documentation](/docs/api/)

---

## Related APIs

- [Employee Payroll API](/docs/api/employee/Employee-Payroll-API.md) - Assign earning components to employees
- [Payroll Structure API](#) - Define complete payroll structures

