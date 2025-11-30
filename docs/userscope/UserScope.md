# UserScope - Technical Documentation

## 🎯 Overview

**Purpose**: Control what data users can access based on hierarchical scope (Board → Medium → Stage → Class → Division).

**Core Principle**: `NULL = ALL` at that level (simple, performant, no complex inclusion/exclusion logic).

---

## 📊 Database Schema

```sql
CREATE TABLE [UserScopeAssignments] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    
    -- User or Role (XOR constraint)
    [UserId] UNIQUEIDENTIFIER NULL,
    [RoleName] NVARCHAR(256) NULL,
    
    -- Hierarchy (NULL = ALL at that level)
    [AcademicYearId] UNIQUEIDENTIFIER NULL,
    [EducationBoardId] UNIQUEIDENTIFIER NULL,
    [EducationMediumId] UNIQUEIDENTIFIER NULL,
    [EducationMediumStageId] UNIQUEIDENTIFIER NULL,
    [StandardId] UNIQUEIDENTIFIER NULL,
    [DivisionId] UNIQUEIDENTIFIER NULL,
    
    -- Metadata
    [Description] NVARCHAR(500) NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,
    
    -- ABP Audit Fields
    [CreationTime] DATETIME2 NOT NULL,
    [CreatorId] UNIQUEIDENTIFIER NULL,
    [LastModificationTime] DATETIME2 NULL,
    [LastModifierId] UNIQUEIDENTIFIER NULL,
    [DeletionTime] DATETIME2 NULL,
    [DeleterId] UNIQUEIDENTIFIER NULL,
    [IsDeleted] BIT NOT NULL DEFAULT 0,
    
    CONSTRAINT [CHK_UserOrRole] CHECK (
        (UserId IS NOT NULL AND RoleName IS NULL) OR 
        (UserId IS NULL AND RoleName IS NOT NULL)
    ),
    CONSTRAINT [UQ_UserScopeAssignment] UNIQUE (
        TenantId, UserId, RoleName, AcademicYearId, 
        EducationBoardId, EducationMediumId, EducationMediumStageId, 
        StandardId, DivisionId
    )
);

-- Indexes
CREATE INDEX IX_UserScopeAssignments_Lookup
ON UserScopeAssignments(TenantId, UserId, IsActive, IsDeleted)
INCLUDE (EducationBoardId, EducationMediumId, StandardId, DivisionId);

CREATE INDEX IX_UserScopeAssignments_Role
ON UserScopeAssignments(TenantId, RoleName, IsActive, IsDeleted);
```

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────┐
│           Presentation Layer (API)                  │
│  UserScopeController                                │
│    - POST /api/user-scope/assign                    │
│    - DELETE /api/user-scope/revoke                  │
│    - GET /api/user-scope/{userId}                   │
│    - POST /api/user-scope/bulk-assign               │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│         Application Layer                           │
│  UserScopeAppService                                │
│    - AssignScopeAsync()                             │
│    - RevokeScopeAsync()                             │
│    - GetUserScopeAsync()                            │
│    - BulkAssignAsync()                              │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│         Domain Layer                                │
│  UserScopeService (Caching, Validation)             │
│  UserScopeAssignment (Aggregate Root)               │
│  IUserScopedEntity (Interface for filtered entities)│
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│         Infrastructure Layer                        │
│  EfCoreUserScopeAssignmentRepository                │
│  UserScopeDataFilter (Global EF Core Filter)        │
│  DistributedCache (Redis - 15min TTL)               │
└─────────────────────────────────────────────────────┘
```

---

## 🗂️ Folder Structure

```
src/ShikshaMitra.Domain.Shared/
  └── Security/UserScope/
      └── ScopeLevel.cs (enum)

src/ShikshaMitra.Domain/
  └── Security/UserScope/
      ├── Entities/
      │   └── UserScopeAssignment.cs
      ├── Services/
      │   ├── IUserScopeService.cs
      │   └── UserScopeService.cs
      ├── ValueObjects/
      │   └── UserScopeContext.cs
      ├── Repositories/
      │   └── IUserScopeAssignmentRepository.cs
      └── Interfaces/
          └── IUserScopedEntity.cs

src/ShikshaMitra.Application.Contracts/
  └── Security/UserScope/
      ├── Dtos/
      │   ├── UserScopeAssignmentDto.cs
      │   ├── CreateUserScopeAssignmentDto.cs
      │   ├── UserScopeDto.cs
      │   └── BulkScopeAssignmentDto.cs
      └── IUserScopeAppService.cs

src/ShikshaMitra.Application/
  └── Security/UserScope/
      └── UserScopeAppService.cs

src/ShikshaMitra.EntityFrameworkCore/
  └── Security/UserScope/
      ├── EfCoreUserScopeAssignmentRepository.cs
      ├── UserScopeAssignmentConfiguration.cs
      └── UserScopeDataFilter.cs

src/ShikshaMitra.HttpApi/
  └── Security/UserScope/
      └── UserScopeController.cs
```

---

## 📝 Core Entities

### ScopeLevel Enum
```csharp
namespace ShikshaMitra.Security.UserScope;

public enum ScopeLevel
{
    Organization = 0,     // Full access
    AcademicYear = 1,     // All boards in year
    Board = 2,            // All mediums in board
    Medium = 3,           // All stages in medium
    MediumStage = 4,      // All classes in stage
    Standard = 5,         // All divisions in class
    Division = 6          // Specific division
}
```

### UserScopeAssignment Entity
```csharp
public class UserScopeAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant, ISoftDelete
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? RoleName { get; private set; }
    
    // NULL = ALL at that level
    public Guid? AcademicYearId { get; private set; }
    public Guid? EducationBoardId { get; private set; }
    public Guid? EducationMediumId { get; private set; }
    public Guid? EducationMediumStageId { get; private set; }
    public Guid? StandardId { get; private set; }
    public Guid? DivisionId { get; private set; }
    
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; set; }
    
    // Constructor, validation, etc.
}
```

### IUserScopedEntity Interface
```csharp
public interface IUserScopedEntity
{
    Guid? EducationBoardId { get; }
    Guid? EducationMediumId { get; }
    Guid? EducationMediumStageId { get; }
    Guid? StandardId { get; }
    Guid? DivisionId { get; }
}
```

### UserScopeContext (DTO)
```csharp
public class UserScopeContext
{
    public Guid UserId { get; set; }
    public bool IsAdmin { get; set; }
    public List<Guid> BoardIds { get; set; } = new();
    public List<Guid> MediumIds { get; set; } = new();
    public List<Guid> MediumStageIds { get; set; } = new();
    public List<Guid> StandardIds { get; set; } = new();
    public List<Guid> DivisionIds { get; set; } = new();
}
```

---

## 🔍 Query Logic

### Scope Resolution
```csharp
// UserScopeService.GetUserScopeAsync()
var query = await _studentRepository.GetQueryableAsync();

query = query.Where(s =>
    _userScopes.Any(scope =>
        (scope.EducationBoardId == null || scope.EducationBoardId == s.EducationBoardId) &&
        (scope.EducationMediumId == null || scope.EducationMediumId == s.EducationMediumId) &&
        (scope.EducationMediumStageId == null || scope.EducationMediumStageId == s.EducationMediumStageId) &&
        (scope.StandardId == null || scope.StandardId == s.StandardId) &&
        (scope.DivisionId == null || scope.DivisionId == s.DivisionId)
    )
);
```

### Global EF Core Filter
```csharp
// In DbContext.OnModelCreating()
builder.Entity<Student>().HasQueryFilter(e =>
    !IsUserScopeFilterEnabled || 
    IsAdminUser() ||
    _userScopeAssignments.Any(scope =>
        (scope.DivisionId == null || scope.DivisionId == e.DivisionId) &&
        (scope.StandardId == null || scope.StandardId == e.StandardId)
        // ... etc
    )
);
```

---

## 🌐 API Endpoints

### UserScopeController
```csharp
[Route("api/app/user-scope")]
public class UserScopeController : ShikshaMitraController
{
    [HttpPost("assign")]
    public Task<UserScopeAssignmentDto> AssignAsync(CreateUserScopeAssignmentDto input);
    
    [HttpPost("bulk-assign")]
    public Task BulkAssignAsync(BulkScopeAssignmentDto input);
    
    [HttpDelete("{id}")]
    public Task RevokeAsync(Guid id);
    
    [HttpGet("{userId}")]
    public Task<UserScopeDto> GetUserScopeAsync(Guid userId);
    
    [HttpGet("by-division/{divisionId}")]
    public Task<List<UserDto>> GetUsersByDivisionAsync(Guid divisionId);
}
```

### DTOs
```csharp
// CreateUserScopeAssignmentDto
public class CreateUserScopeAssignmentDto
{
    public Guid? UserId { get; set; }
    public string? RoleName { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? EducationBoardId { get; set; }
    public Guid? EducationMediumId { get; set; }
    public Guid? EducationMediumStageId { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public string? Description { get; set; }
}

// UserScopeDto (Response)
public class UserScopeDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public bool IsAdmin { get; set; }
    public List<UserScopeAssignmentDto> Assignments { get; set; }
    public UserScopeContext EffectiveScope { get; set; }
}

// BulkScopeAssignmentDto
public class BulkScopeAssignmentDto
{
    public List<Guid> UserIds { get; set; }
    public Guid? StandardId { get; set; }
    public List<Guid>? DivisionIds { get; set; }
    public string? Description { get; set; }
}
```

---

## 📋 Use Cases

### Example 1: Assign Class Access (All Divisions)
```http
POST /api/app/user-scope/assign
{
  "userId": "teacher-guid",
  "educationBoardId": "cbse-guid",
  "educationMediumId": "english-guid",
  "educationMediumStageId": "primary-guid",
  "standardId": "class-2-guid",
  "divisionId": null,  // NULL = All divisions
  "description": "Class 2 teacher"
}
```

### Example 2: Assign Specific Divisions
```http
POST /api/app/user-scope/bulk-assign
{
  "userIds": ["teacher-guid"],
  "standardId": "class-2-guid",
  "divisionIds": ["div-a-guid", "div-b-guid"],
  "description": "Class 2, Div A and B only"
}
```

### Example 3: Get User's Effective Scope
```http
GET /api/app/user-scope/teacher-guid
```

Response:
```json
{
  "userId": "teacher-guid",
  "userName": "Ms. Priya",
  "isAdmin": false,
  "assignments": [
    {
      "standardId": "class-2-guid",
      "divisionId": "div-a-guid",
      "description": "Class 2, Div A"
    },
    {
      "standardId": "class-2-guid",
      "divisionId": "div-b-guid",
      "description": "Class 2, Div B"
    }
  ],
  "effectiveScope": {
    "divisionIds": ["div-a-guid", "div-b-guid"]
  }
}
```

---

## ✅ Implementation Checklist

### Phase 1: Domain Layer (Week 1) ✅ COMPLETED
- [x] Create `ScopeLevel` enum in `Domain.Shared`
- [x] Create `UserScopeAssignment` entity
- [x] Create `IUserScopedEntity` interface
- [x] Create `UserScopeContext` DTO
- [x] Create `IUserScopeService` interface
- [x] Implement `UserScopeService` with caching
- [x] Create `IUserScopeAssignmentRepository`
- [x] Create `UserScopeOptions` configuration class
- [ ] Unit tests for `UserScopeService` ⏳ NEXT

### Phase 2: Infrastructure Layer (Week 1) ✅ COMPLETED
- [x] Create `UserScopeAssignmentConfiguration` (EF Core)
- [x] Implement `EfCoreUserScopeAssignmentRepository`
- [x] Create migration: `Add_UserScopeAssignments_Table` (v27)
- [x] Create indexes for performance
- [ ] Implement `UserScopeDataFilter` (global query filter) ⏳ NEXT
- [ ] Register filter in `DbContext`
- [ ] Integration tests for repository

### Phase 3: Application Layer (Week 2) ✅ COMPLETED
- [x] Create DTOs in `Application.Contracts`
- [x] Create `IUserScopeAppService` interface
- [x] Implement `UserScopeAppService`
- [x] Add permissions (`UserScope.Assign`, `UserScope.Revoke`, `UserScope.View`, `UserScope.Manage`, `UserScope.BulkAssign`)
- [x] Add AutoMapper profiles
- [ ] Add validation (FluentValidation) ⏳ Optional
- [ ] Application service tests

### Phase 4: API Layer (Week 2) ✅ COMPLETED
- [x] Create `UserScopeController`
- [x] Add Swagger documentation
- [x] Add authorization attributes
- [ ] API integration tests
- [ ] Postman collection

### Phase 5: Entity Updates (Week 2-3)
- [ ] Implement `IUserScopedEntity` on:
  - [ ] `Student`
  - [ ] `StudentEnrollment`
  - [ ] `EmployeeAttendance`
  - [ ] `StudentAttendance`
  - [ ] `HomeworkAssignment`
  - [ ] `ClassTeacher`
- [ ] Test queries with filter

### Phase 6: Data Migration (Week 3)
- [ ] Create migration scripts from old permission tables
- [ ] Test migration in staging
- [ ] Run migration in production
- [ ] Verify data integrity

### Phase 7: Testing & Deployment (Week 4)
- [ ] E2E tests for all scenarios
- [ ] Performance testing (10k+ students)
- [ ] Security audit
- [ ] UAT with users
- [ ] Deploy to production

---

## 🔧 Configuration

### appsettings.json
```json
{
  "UserScope": {
    "CacheEnabled": true,
    "CacheDurationMinutes": 15,
    "EnableGlobalFilter": true,
    "AdminRoles": ["admin", "super-admin"]
  }
}
```

---

## 🎯 Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **NULL = ALL** | Simpler than inclusion/exclusion logic |
| **Multiple rows for divisions** | Acceptable trade-off for query simplicity |
| **Global EF Core filter** | Automatic, hard to bypass |
| **Distributed cache (Redis)** | Performance, scalability |
| **User OR Role scope** | Flexibility, both supported |
| **Single table** | Clean, not coupled to legacy |

---

## 📊 Performance Targets

| Metric | Target |
|--------|--------|
| Scope resolution | < 50ms (p90) |
| Query overhead | < 10% |
| Cache hit rate | > 80% |
| Concurrent users | 10,000+ |

---

## 🔒 Security

- ✅ Global query filter (prevents data leakage)
- ✅ Permission-based API (`UserScope.Assign` required)
- ✅ Audit logging (ABP audit fields)
- ✅ Cache signed with tenant/user ID
- ✅ Fail-safe: No scope = No access

---

**Version**: 1.0  
**Last Updated**: January 2025  
**Status**: Ready for Implementation
