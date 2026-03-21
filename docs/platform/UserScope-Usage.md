# UserScope - Usage Guide

## Overview

UserScope provides hierarchical access control for academic entities. It supports wildcard access (NULL = ALL within parent hierarchy) and specific entity assignments.

---

## Quick Start

### 1. Get User Scope

```csharp
var scope = await _userScopeService.GetUserScopeAsync(userId);
```

Returns `UserScopeContext` with:
- `IsAdmin` - Admin users bypass all checks
- `AcademicYearIds`, `BoardIds`, `StandardIds`, `DivisionIds` - Specific accessible entities
- `HasWildcardAcademicYear`, `HasWildcardBoard`, `HasWildcardStandard`, `HasWildcardDivision` - Wildcard flags

---

## Usage Patterns

### Access Checks (Single Entity)

**Use when**: Checking if user can access a specific entity

```csharp
// Load entity (must include hierarchy properties)
var student = await _studentRepository.GetAsync(studentId);

// Get user scope
var scope = await _userScopeService.GetUserScopeAsync(CurrentUser.GetId());

// Check access
if (!UserScopeQueryExtensions.HasAccessToEntity(student, scope))
{
    throw new UnauthorizedAccessException();
}
```

### Query Filtering (Lists)

**Use when**: Retrieving lists of entities user can access

```csharp
// Get queryable
var query = await _studentRepository.GetQueryableAsync();

// Get user scope
var scope = await _userScopeService.GetUserScopeAsync(CurrentUser.GetId());

// Apply filter (automatically handles wildcards + hierarchy)
var filteredQuery = query.ApplyUserScope(scope);
var students = await AsyncExecuter.ToListAsync(filteredQuery);
```

### Direct Scope Access (Simple Checks)

**Use when**: Simple checks without loading entities

```csharp
var scope = await _userScopeService.GetUserScopeAsync(userId);

// Check admin
if (scope.IsAdmin)
{
    // Full access
}

// Check specific IDs
if (scope.DivisionIds.Contains(divisionId))
{
    // Has access
}

// Check wildcards
if (scope.HasWildcardDivision)
{
    // Has access to all divisions (within assigned standards)
}
```

---

## Hierarchy Rules

### NULL = ALL Principle

When an assignment has `NULL` for a field, user has access to **ALL** entities at that level **within parent constraints**:

| Assignment | Meaning |
|------------|---------|
| `StandardId = 10, DivisionId = null` | All divisions **in Standard 10** |
| `BoardId = CBSE, StandardId = null` | All standards **in CBSE board** |
| `AcademicYearId = null` | All academic years (no parent) |

### Hierarchy Chain

```
AcademicYear (top-level, cross-cutting)
    ↓
Board
    ↓
Medium
    ↓
Medium Stage
    ↓
Standard
    ↓
Division (most specific)
```

Wildcards are constrained by parent assignments. Example:
- User has: `StandardId = 10, HasWildcardDivision = true`
- Can access: Divisions in Standard 10 only
- Cannot access: Divisions in Standard 9, 8, etc.

---

## Cache Management

### Invalidate User Cache

Call when a user's scope assignments change:

```csharp
await _userScopeService.InvalidateUserScopeCacheAsync(userId);
```

### Invalidate Role Cache

Call when role-based scope assignments change:

```csharp
await _userScopeService.InvalidateRoleScopeCacheAsync("Teacher");
```

This invalidates cache for **all users** with that role.

---

## Common Patterns

### Application Service Example

```csharp
public class StudentAppService : ApplicationService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IUserScopeService _userScopeService;

    public async Task<StudentDto> GetAsync(Guid id)
    {
        // Load entity
        var student = await _studentRepository.GetAsync(id);
        
        // Check access
        var scope = await _userScopeService.GetUserScopeAsync(CurrentUser.GetId());
        if (!UserScopeQueryExtensions.HasAccessToEntity(student, scope))
        {
            throw new UnauthorizedAccessException();
        }
        
        return ObjectMapper.Map<Student, StudentDto>(student);
    }

    public async Task<List<StudentDto>> GetListAsync()
    {
        // Get queryable
        var query = await _studentRepository.GetQueryableAsync();
        
        // Apply scope filter
        var scope = await _userScopeService.GetUserScopeAsync(CurrentUser.GetId());
        var filtered = query.ApplyUserScope(scope);
        
        // Execute query
        var students = await AsyncExecuter.ToListAsync(filtered);
        return ObjectMapper.Map<List<Student>, List<StudentDto>>(students);
    }
}
```

### Repository Pattern

```csharp
// Option 1: Filter at query level (recommended)
var query = await _repository.GetQueryableAsync();
var scope = await _userScopeService.GetUserScopeAsync(userId);
var items = await AsyncExecuter.ToListAsync(query.ApplyUserScope(scope));

// Option 2: Filter after loading (if already in memory)
var allItems = await _repository.GetListAsync();
var scope = await _userScopeService.GetUserScopeAsync(userId);
var accessible = allItems.FilterByUserScope(scope);
```

---

## Entity Requirements

Entities must implement `IUserScopedEntity`:

```csharp
public interface IUserScopedEntity
{
    Guid? AcademicYearId { get; }
    Guid? EducationBoardId { get; }
    Guid? EducationMediumId { get; }
    Guid? EducationMediumStageId { get; }
    Guid? StandardId { get; }
    Guid? DivisionId { get; }
}
```

All properties must be accessible for hierarchy checks to work correctly.

---

## Best Practices

### ✅ DO

- **Always load full entity** when checking wildcards
- **Use `ApplyUserScope()`** for list queries
- **Use `HasAccessToEntity()`** for single entity checks
- **Invalidate cache** after assignment changes
- **Check `IsAdmin`** first for performance

### ❌ DON'T

- **Don't assume** wildcard = global access
- **Don't skip** entity loading for wildcard checks
- **Don't cache** scope context in instance variables (use per-request)
- **Don't forget** to invalidate cache after changes

---

## API Reference

### IUserScopeService

```csharp
// Get user scope (cached)
Task<UserScopeContext> GetUserScopeAsync(Guid userId, CancellationToken cancellationToken = default);

// Cache management
Task InvalidateUserScopeCacheAsync(Guid userId, CancellationToken cancellationToken = default);
Task InvalidateRoleScopeCacheAsync(string roleName, CancellationToken cancellationToken = default);

// Utilities
Task<bool> IsAdminUserAsync(Guid userId, CancellationToken cancellationToken = default);
```

### UserScopeQueryExtensions

```csharp
// Query filtering
IQueryable<T> ApplyUserScope<T>(this IQueryable<T> queryable, UserScopeContext scope);

// Entity checks
bool HasAccessToEntity<T>(T entity, UserScopeContext scope);

// In-memory filtering
IEnumerable<T> FilterByUserScope<T>(this IEnumerable<T> entities, UserScopeContext scope);

// Utilities
int CountWithUserScope<T>(this IQueryable<T> queryable, UserScopeContext scope);
bool AnyWithUserScope<T>(this IQueryable<T> queryable, UserScopeContext scope);
```

---

## Performance Tips

1. **Cache scope context** per request (not per operation)
2. **Filter at database level** using `ApplyUserScope()` on queryables
3. **Check admin first** - admins bypass all filtering
4. **Batch operations** - get scope once, use multiple times
5. **Use `AnyWithUserScope()`** instead of `CountWithUserScope() > 0`

---

## Example Scenarios

### Teacher with Specific Divisions

```csharp
// Assignment: Standard 10, Division A & B
var assignments = new[] {
    new { StandardId = 10, DivisionId = divA },
    new { StandardId = 10, DivisionId = divB }
};

// Scope result
scope.StandardIds = [10];
scope.DivisionIds = [divA, divB];
scope.HasWildcardDivision = false;

// Can access: Students in 10-A and 10-B
// Cannot access: Students in 10-C
```

### Principal with Wildcard Standards

```csharp
// Assignment: Medium Stage = Secondary, StandardId = null
var assignment = new { MediumStageId = secondary, StandardId = null };

// Scope result
scope.MediumStageIds = [secondary];
scope.HasWildcardStandard = true;

// Can access: All standards in Secondary stage
// Cannot access: Standards in Primary stage
```

### Academic Year Restriction

```csharp
// Assignment: AcademicYearId = 2024
var assignment = new { AcademicYearId = 2024 };

// Scope result
scope.AcademicYearIds = [2024];

// Can access: Only entities in 2024
// Cannot access: Entities in 2023, 2025, etc.
```

---

## Troubleshooting

### User sees too much data
- Check if user has wildcard assignments at high levels (Board, Medium)
- Verify parent constraints are set correctly
- Confirm academic year filtering is working

### User sees too little data
- Verify assignments are active (`IsActive = true`)
- Check cache invalidation after assignment changes
- Confirm entity properties (BoardId, StandardId, etc.) are populated

### Wildcard not working
- Ensure you're using `HasAccessToEntity()` or `ApplyUserScope()`
- Verify entity implements `IUserScopedEntity` correctly
- Check that parent constraint properties are available

---

## Summary

UserScope provides powerful, hierarchical access control with minimal code:

```csharp
// Three-line pattern for most scenarios
var scope = await _userScopeService.GetUserScopeAsync(userId);
var query = await _repository.GetQueryableAsync();
var filtered = await AsyncExecuter.ToListAsync(query.ApplyUserScope(scope));
```

Remember: **Wildcards are hierarchy-aware**, not global!
