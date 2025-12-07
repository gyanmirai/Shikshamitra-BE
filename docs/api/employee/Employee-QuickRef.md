# Employee API - Quick Reference Guide

## Overview
Quick reference for working with the Employee module after the optimization updates.

## Repository Methods

### Get Employee by User ID
```csharp
// Auto-includes: Role, EducationMediumStage, Shift
// Cached for 15 minutes - subsequent calls use cache
var employee = await _employeeRepository.GetEmployeeByUserIdAsync(userId);
```

**Performance:** 1-2ms (cached) vs 15-20ms (database)

### Get Employee with All Details
```csharp
// Without PayrollStructure (lighter, cached)
var employee = await _employeeRepository.GetWithDetailsAsync(employeeId);

// With PayrollStructure (complete data, uses cache for existence check)
var employee = await _employeeRepository.GetWithDetailsAsync(
    employeeId, 
    includePayrollStructure: true
);
```

**Cache Strategy:** Cache verifies existence, database loads full entity

### Get Last Employee (for code generation)
```csharp
// Not cached - infrequently called
var lastEmployee = await _employeeRepository.GetLastEmployeeAsync();
```

## API Endpoints

### Employee CRUD
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/employee/{id}` | Get employee with full details |
| GET | `/api/app/employee` | Get employee list (paginated) |
| POST | `/api/app/employee` | Create employee (deprecated - use CreateBasicInfoAsync) |
| PUT | `/api/app/employee/{id}` | Update employee (deprecated - use UpdateBasicInfoAsync) |
| DELETE | `/api/app/employee/{id}` | Delete employee |

### Employee Specialized Operations
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/employee/by-user/{userId}` | Get employee by user ID |
| POST | `/api/app/employee/basic-info` | Create employee with basic info |
| PUT | `/api/app/employee/{id}/basic-info` | Update employee basic info |
| PUT | `/api/app/employee/{id}/family-info` | Update family details |
| PUT | `/api/app/employee/{id}/experience-info` | Update work experience |
| PUT | `/api/app/employee/{id}/bank-details` | Update bank details |
| PUT | `/api/app/employee/{id}/status` | Update employee status |
| PUT | `/api/app/employee/{id}/payroll-structure` | Update payroll structure |
| GET | `/api/app/employee/{id}/additional-details` | Get additional details (BPL, disability, etc.) |
| PUT | `/api/app/employee/{id}/additional-details` | Update additional details |

### Profile Picture Operations
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/app/employee/{id}/profile-picture` | Upload profile picture |
| GET | `/api/app/employee/{id}/profile-picture` | Download profile picture |

### Document Operations
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/app/employee/{id}/documents` | Upload documents |
| GET | `/api/app/employee/{id}/documents/{docId}` | Get specific document |
| GET | `/api/app/employee/{id}/documents/metadata` | Get all document metadata |
| PUT | `/api/app/employee/{id}/documents/{docId}` | Update document |
| DELETE | `/api/app/employee/{id}/documents/{docId}` | Delete document |

## DTOs

### CreateEmployeeBasicInfoDto
```csharp
{
    "firstName": "John",           // Required, MaxLength(256)
    "lastName": "Doe",              // MaxLength(256)
    "middleName": "Smith",          // MaxLength(256)
    "email": "john@example.com",    // EmailAddress, MaxLength(256)
    "mobileNo": "+1234567890",      // Phone, MaxLength(20)
    "dateOfBirth": "1990-01-01",    // Must be 18+ at joining
    "dateOfJoining": "2020-01-01",  // Cannot be before DOB
    "roleId": "guid",               // Required
    "educationMediumStageId": "guid", // Required
    "employeeStatusType": 1,
    // ... other optional fields
}
```

### UpdateEmployeeBasicInfoDto
```csharp
{
    "firstName": "John",
    "lastName": "Doe",
    "email": "john.doe@example.com",
    "roleId": "guid",               // Required
    "educationMediumStageId": "guid", // Required
    // ... same validation as Create
}
```

### EmployeeDto (Response)
```csharp
{
    "id": "guid",
    "employeeCode": "CBSTE2401",
    "firstName": "John",
    "employeeName": "John Doe",
    "employeeNameWithFather": "John Smith Doe",
    "profilePictureBlobName": "profile-pictures/guid_image.jpg", // Not byte[]
    "role": {                       // Auto-included
        "id": "guid",
        "name": "Teacher"
    },
    "educationMediumStage": {       // Auto-included
        "id": "guid",
        "name": "Primary"
    },
    "shift": {                      // Auto-included (if assigned)
        "id": "guid",
        "name": "Morning"
    },
    // ... other fields
}
```

## Validation Rules

### Business Rules
```csharp
// Age validation
if (DateOfJoining - DateOfBirth < 18 years)
    Error: "Employee must be at least 18 years old at the time of joining."

// Future date validation
if (DateOfBirth >= Today)
    Error: "Date of birth cannot be in the future."
```

### Field Constraints
| Field | Constraint | Notes |
|-------|-----------|-------|
| FirstName | Required, MaxLength(256) | Cannot be null/empty |
| Email | EmailAddress, MaxLength(256) | Valid email format |
| MobileNo | Phone, MaxLength(20) | Valid phone format |
| EmployeeCode | Required, MaxLength(50), Unique | Auto-generated |
| ProfilePictureBlobName | MaxLength(512) | Blob reference only |

## Common Scenarios

### 1. Create New Employee
```csharp
// Step 1: Create employee with basic info
var createDto = new CreateEmployeeBasicInfoDto
{
    FirstName = "John",
    LastName = "Doe",
    Email = "john.doe@school.com",
    MobileNo = "+1234567890",
    DateOfBirth = new DateOnly(1990, 1, 1),
    DateOfJoining = new DateOnly(2020, 1, 1),
    RoleId = teacherRoleId,
    EducationMediumStageId = primaryStageId,
    EmployeeStatusType = EmployeeStatusType.Active
};

var employee = await _employeeAppService.CreateBasicInfoAsync(createDto);

// Step 2: Upload profile picture (optional)
var blobName = await _employeeAppService.UploadProfilePictureAsync(
    employee.Id,
    "profile.jpg",
    imageBytes
);

// Step 3: Add family info (optional)
await _employeeAppService.UpdateFamilyInfo(employee.Id, familyDto);

// Step 4: Add work experience (optional)
await _employeeAppService.UpdateExperienceInfo(employee.Id, experienceList);

// Step 5: Upload documents (optional)
await _employeeAppService.UploadDocumentsAsync(employee.Id, documentList);
```

### 2. Get Employee Details
```csharp
// Get with auto-included navigations (Role, EducationMediumStage, Shift)
var employee = await _employeeAppService.GetAsync(employeeId);

// Access navigation properties (already loaded, no additional queries)
var roleName = employee.Role.Name;  // No lazy loading!
var stageName = employee.EducationMediumStage.Name;
```

### 3. Update Employee Information
```csharp
// Update basic info
var updateDto = new UpdateEmployeeBasicInfoDto
{
    FirstName = "Jane",
    LastName = "Doe",
    Email = "jane.doe@school.com",
    RoleId = adminRoleId,  // Can change role
    EducationMediumStageId = secondaryStageId
};

await _employeeAppService.UpdateBasicInfoAsync(employeeId, updateDto);

// Update profile picture
await _employeeAppService.UploadProfilePictureAsync(
    employeeId,
    "new-profile.jpg",
    newImageBytes
);
```

### 4. List Employees (Optimized)
```csharp
// Single query with all navigations
var input = new PagedAndSortedResultRequestDto
{
    MaxResultCount = 20,
    SkipCount = 0,
    Sorting = "EmployeeCode DESC"
};

var employees = await _employeeAppService.GetListAsync(input);

// All navigation properties already loaded (Role, EducationMediumStage, Shift)
foreach (var emp in employees.Items)
{
    Console.WriteLine($"{emp.EmployeeName} - {emp.Role.Name}"); // No N+1!
}
```

### 5. Filter by Status
```csharp
// Uses composite index (TenantId + EmployeeStatusType)
var queryable = await _employeeRepository.GetQueryableAsync();
var activeEmployees = await queryable
    .Where(e => e.EmployeeStatusType == EmployeeStatusType.Active)
    .ToListAsync();
```

## Performance Tips

### ✅ DO
```csharp
// Use repository methods that leverage eager loading AND caching
var employee = await repository.GetEmployeeByUserIdAsync(userId);  // Cached!

// Use GetWithDetailsAsync for complete data
var employee = await repository.GetWithDetailsAsync(id, includePayrollStructure: true);

// Let AutoInclude handle common navigations
var employees = await repository.GetListAsync();

// Rely on automatic cache invalidation
await repository.UpdateAsync(employee);  // Cache auto-invalidated
```

### ❌ DON'T
```csharp
// Don't manually load navigation properties
var employee = await repository.GetAsync(id);
var role = await roleRepository.GetAsync(employee.RoleId); // N+1!

// Don't bypass repository (loses caching benefit)
var employee = await dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id);

// Don't manually invalidate cache (repository handles it)
await cacheManager.InvalidateEmployeeAsync(id);  // Not needed!

// Don't use .Include() on auto-included properties
var query = await repository.GetQueryableAsync();
var employees = await query
    .Include(e => e.Role)  // Already auto-included!
    .ToListAsync();
```

### Cache Performance
```csharp
// First access - cache miss (15-20ms)
var employee1 = await repository.GetEmployeeByUserIdAsync(userId);

// Subsequent access within 15 min - cache hit (1-2ms) 
var employee2 = await repository.GetEmployeeByUserIdAsync(userId);  // 90% faster!

// After update - cache invalidated automatically
employee1.FirstName = "Updated";
await repository.UpdateAsync(employee1);

// Next access - reloads from DB and repopulates cache
var employee3 = await repository.GetEmployeeByUserIdAsync(userId);
```

## Error Handling

### Common Errors
```csharp
// EntityNotFoundException
try
{
    var employee = await _employeeAppService.GetAsync(invalidId);
}
catch (EntityNotFoundException ex)
{
    // Handle: Employee not found
}

// UserFriendlyException (Validation)
try
{
    var employee = await _employeeAppService.CreateBasicInfoAsync(invalidDto);
}
catch (UserFriendlyException ex)
{
    // Handle: Validation failed
    // ex.Message contains user-friendly error
}

// ValidationException
try
{
    var employee = await _employeeAppService.UpdateBasicInfoAsync(id, dto);
}
catch (ValidationException ex)
{
    // Handle: Multiple validation errors
    foreach (var error in ex.ValidationErrors)
    {
        Console.WriteLine($"{error.MemberNames}: {error.ErrorMessage}");
    }
}
```

## Migration Guide

### From Old to New API

**Old: Profile Picture in DTO**
```csharp
// ❌ Old way (no longer works)
var createDto = new CreateEmployeeBasicInfoDto
{
    ProfilePicture = imageBytes  // Removed!
};
```

**New: Separate Profile Picture Upload**
```csharp
// ✅ New way
var employee = await _employeeAppService.CreateBasicInfoAsync(createDto);
await _employeeAppService.UploadProfilePictureAsync(
    employee.Id,
    "profile.jpg",
    imageBytes
);
```

**Old: Direct Repository Access**
```csharp
// ❌ Potential N+1
var employees = await _repository.GetListAsync();
foreach (var emp in employees)
{
    var role = await _roleRepository.GetAsync(emp.RoleId);
}
```

**New: Leveraging Auto-Include**
```csharp
// ✅ Single query
var employees = await _repository.GetListAsync();
foreach (var emp in employees)
{
    var roleName = emp.Role.Name;  // Already loaded!
}
```

## Testing

### Unit Test Example
```csharp
[Fact]
public async Task Should_Load_Employee_With_AutoIncluded_Navigations()
{
    // Arrange
    var employeeId = Guid.NewGuid();
    
    // Act
    var employee = await _employeeRepository.GetAsync(employeeId);
    
    // Assert
    employee.ShouldNotBeNull();
    employee.Role.ShouldNotBeNull();  // Auto-included
    employee.EducationMediumStage.ShouldNotBeNull();  // Auto-included
}
```

### Integration Test Example
```csharp
[Fact]
public async Task Should_Execute_Single_Query_For_Employee_List()
{
    // Arrange
    await CreateTestEmployeesAsync(100);
    
    // Act
    using (var queryLogger = new QueryLogger())
    {
        var employees = await _employeeRepository.GetListAsync();
        
        // Assert
        queryLogger.QueryCount.ShouldBe(1);  // Only 1 query!
        employees.Count.ShouldBe(100);
        employees.All(e => e.Role != null).ShouldBeTrue();
    }
}
```

## Database Indexes

The following indexes are automatically created:

| Index Name | Columns | Type | Purpose |
|------------|---------|------|---------|
| IX_Employees_UserId | UserId | Non-unique | User lookup |
| IX_Employees_EmployeeCode_Unique | EmployeeCode | Unique | Code validation |
| IX_Employees_Email | Email | Non-unique | Email search |
| IX_Employees_RoleId | RoleId | Non-unique | Role filtering |
| IX_Employees_EducationMediumStageId | EducationMediumStageId | Non-unique | Stage filtering |
| IX_Employees_TenantId_Status | TenantId, EmployeeStatusType | Composite | Status queries |

## Resources

- [Employee Improvements Summary](./Employee-Improvements-Summary.md) - Overview of all improvements
- [Eager Loading Implementation](./Employee-EagerLoading-Implementation.md) - Navigation property optimization details
- [Caching Implementation](./Employee-Caching-Implementation.md) - Distributed caching strategy and benchmarks
- [ABP Framework Documentation](https://docs.abp.io/)
- [EF Core Performance Best Practices](https://docs.microsoft.com/ef/core/performance/)

## Performance Summary

| Feature | Before | After | Improvement |
|---------|--------|-------|-------------|
| Employee List (100 items) | 301 queries | 1 query | 99.7% reduction |
| User ID Lookup (cached) | 15-20ms | 1-2ms | 90% faster |
| Memory per employee | 2-5 MB | 10 KB | 99%+ reduction |
| Database load (90% cache hit) | 1500 queries/min | 150 queries/min | 90% reduction |

---
**Last Updated**: December 2024  
**Version**: 3.0  
**Updates**: Added caching implementation and performance metrics
