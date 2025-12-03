# Bulk Scope Assignment API - Usage Guide

## Overview

The enhanced bulk scope assignment API supports **lists at every hierarchical level**, allowing you to create multiple scope assignments in a single API call using a **cartesian product** approach.

**Important Features:**
- ⚠️ **OVERWRITE STRATEGY**: This API **DELETES all existing scope assignments** for the specified users before creating new ones. This is NOT an append operation.
- 🔒 **Transactional**: Either all operations (delete old + create new) succeed or none are applied. This ensures data consistency.

## API Endpoint

```
POST /api/app/user-scope/bulk-assign
```

## How It Works

The API operates in two steps within a single transaction:

**Step 1**: Delete all existing scope assignments for the specified users  
**Step 2**: Create assignments for all combinations of the provided scope parameters

**Formula**: `Total New Assignments = Users × AcademicYears × Boards × Mediums × MediumStages × Standards × Divisions`

## Payload Structure

```json
{
  "userIds": ["guid1", "guid2", ...],
  "academicYearIds": ["guid1", "guid2", ...],
  "educationBoardIds": ["guid1", "guid2", ...],
  "educationMediumIds": ["guid1", "guid2", ...],
  "educationMediumStageIds": ["guid1", "guid2", ...],
  "standardIds": ["guid1", "guid2", ...],
  "divisionIds": ["guid1", "guid2", ...],
  "description": "Optional description"
}
```

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `userIds` | `List<Guid>` | ✅ Required | List of user IDs to assign scopes to |
| `academicYearIds` | `List<Guid>?` | Optional | List of academic year IDs (null/empty = all years) |
| `educationBoardIds` | `List<Guid>?` | Optional | List of education board IDs (null/empty = all boards) |
| `educationMediumIds` | `List<Guid>?` | Optional | List of education medium IDs (null/empty = all mediums) |
| `educationMediumStageIds` | `List<Guid>?` | Optional | List of medium stage IDs (null/empty = all stages) |
| `standardIds` | `List<Guid>?` | Optional | List of standard/class IDs (null/empty = all standards) |
| `divisionIds` | `List<Guid>?` | Optional | List of division IDs (null/empty = all divisions) |
| `description` | `string?` | Optional | Description for all assignments (max 500 chars) |

**Note**: At least one scope parameter (academicYearIds, educationBoardIds, etc.) must be provided.

## Response Structure

```json
{
  "deletedAssignments": 5,
  "assignmentsCreated": 12,
  "totalCombinations": 12,
  "affectedUserIds": ["guid1", "guid2"],
  "message": "Successfully replaced scope assignments for 2 users. Deleted 5 old assignments and created 12 new assignments."
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `deletedAssignments` | `int` | Number of existing assignments that were deleted |
| `assignmentsCreated` | `int` | Number of new assignments successfully created (0 if operation failed) |
| `totalCombinations` | `int` | Total number of combinations (Users × Boards × Standards × ...) |
| `affectedUserIds` | `List<Guid>` | List of users whose assignments were replaced (empty if operation failed) |
| `message` | `string` | Human-readable message describing the result |

## Transactional Behavior with Overwrite Strategy

### ✅ All Operations Succeed
1. All existing assignments for specified users are deleted
2. All new assignments are created
3. User caches are invalidated
4. Success response is returned with delete/create counts

### ❌ Any Operation Fails
- **No changes are made** (complete rollback)
- Existing assignments remain unchanged
- No new assignments are created
- Error response is returned with details

### Important Notes on Overwrite Strategy

⚠️ **WARNING**: This operation will **DELETE ALL existing scope assignments** for the specified users before creating new ones.

**Example Scenario:**
- User A has 3 existing scope assignments (Board X, Standard 1, Division A/B/C)
- You call bulk assign for User A with (Board Y, Standard 2)
- Result: The 3 old assignments are deleted, 1 new assignment is created

**Use Cases:**
- ✅ **Reassigning** users to completely different scopes
- ✅ **Resetting** user permissions
- ✅ **Replacing** all assignments without worrying about duplicates
- ❌ **Adding** new assignments while keeping existing ones (use individual assign API instead)

### Common Failure Scenarios

1. **Invalid References**: One or more GUIDs don't exist (board, standard, etc.)
   - Error Code: `UserScope:InvalidReferences`
   - Solution: Verify all GUIDs are valid before calling the API
   - Result: No changes made (existing assignments preserved)

2. **Missing Users**: One or more user IDs don't exist
   - Error Code: `UserScope:UsersNotFound`
   - Solution: Ensure all user IDs are valid
   - Result: No changes made

## Examples

### Example 1: Simple Assignment (2 boards × 3 standards)

**Request:**
```json
{
  "userIds": ["user-guid-1", "user-guid-2"],
  "educationBoardIds": ["board-a-guid", "board-b-guid"],
  "standardIds": ["standard-1-guid", "standard-2-guid", "standard-3-guid"]
}
```

**Result**: Creates **12 assignments** (2 users × 2 boards × 3 standards = 12)

**Response (Success):**
```json
{
  "deletedAssignments": 0,
  "assignmentsCreated": 12,
  "totalCombinations": 12,
  "affectedUserIds": ["user-guid-1", "user-guid-2"],
  "message": "Successfully replaced scope assignments for 2 users. Deleted 0 old assignments and created 12 new assignments."
}
```

### Example 2: Multi-level Assignment

**Request:**
```json
{
  "userIds": ["teacher-1-guid"],
  "academicYearIds": ["2024-guid"],
  "educationBoardIds": ["cbse-guid", "icse-guid"],
  "standardIds": ["class-9-guid", "class-10-guid"],
  "divisionIds": ["div-a-guid", "div-b-guid"],
  "description": "Teacher assigned to CBSE and ICSE boards for Class 9 and 10"
}
```

**Result**: Creates **8 assignments** (1 user × 1 year × 2 boards × 2 standards × 2 divisions = 8)

Combinations created:
1. User → 2024 → CBSE → Class 9 → Division A
2. User → 2024 → CBSE → Class 9 → Division B
3. User → 2024 → CBSE → Class 10 → Division A
4. User → 2024 → CBSE → Class 10 → Division B
5. User → 2024 → ICSE → Class 9 → Division A
6. User → 2024 → ICSE → Class 9 → Division B
7. User → 2024 → ICSE → Class 10 → Division A
8. User → 2024 → ICSE → Class 10 → Division B

**Response (Success):**
```json
{
  "deletedAssignments": 3,
  "assignmentsCreated": 8,
  "totalCombinations": 8,
  "affectedUserIds": ["teacher-1-guid"],
  "message": "Successfully replaced scope assignments for 1 users. Deleted 3 old assignments and created 8 new assignments."
}
```

### Example 3: Multiple Users with Multiple Boards and Standards

**Request:**
```json
{
  "userIds": [
    "teacher-1-guid",
    "teacher-2-guid",
    "teacher-3-guid"
  ],
  "educationBoardIds": [
    "board-cbse-guid",
    "board-icse-guid",
    "board-state-guid"
  ],
  "standardIds": [
    "class-6-guid",
    "class-7-guid"
  ],
  "description": "Bulk assignment for three teachers across multiple boards"
}
```

**Result**: Creates **18 assignments** (3 users × 3 boards × 2 standards = 18)

### Example 4: Assignment with NULL Values (All Access)

**Request:**
```json
{
  "userIds": ["admin-user-guid"],
  "educationBoardIds": null,
  "standardIds": null,
  "divisionIds": null,
  "description": "Admin access to all boards, standards, and divisions"
}
```

**Result**: Creates **1 assignment** with NULL values (meaning access to all)

### Example 5: Overwrite Existing Assignments

**Scenario**: User has 2 existing assignments, we want to replace them completely

**Existing Assignments for user-guid-1:**
- Board A → Standard 1 → Division A
- Board A → Standard 1 → Division B

**Request:**
```json
{
  "userIds": ["user-guid-1"],
  "educationBoardIds": ["board-b-guid"],
  "standardIds": ["standard-2-guid", "standard-3-guid"]
}
```

**Response (Success):**
```json
{
  "deletedAssignments": 2,
  "assignmentsCreated": 2,
  "totalCombinations": 2,
  "affectedUserIds": ["user-guid-1"],
  "message": "Successfully replaced scope assignments for 1 users. Deleted 2 old assignments and created 2 new assignments."
}
```

**Result**: 
- ❌ Deleted: Board A → Standard 1 → Division A
- ❌ Deleted: Board A → Standard 1 → Division B
- ✅ Created: Board B → Standard 2
- ✅ Created: Board B → Standard 3

### Example 6: Failure - Invalid Board ID

**Request:**
```json
{
  "userIds": ["user-guid-1", "user-guid-2"],
  "educationBoardIds": ["invalid-board-guid", "board-b-guid"],
  "standardIds": ["standard-1-guid"]
}
```

**Response (Error - 400 Bad Request):**
```json
{
  "error": {
    "code": "UserScope:InvalidReferences",
    "message": "One or more referenced entities (Standard, Division, Board, Medium, etc.) do not exist. No changes were made. Please verify all IDs are valid. (Ref: 123e4567-e89b-12d3-a456-426614174000)",
    "details": null
  }
}
```

**Result**: **No changes made** - existing assignments preserved (transaction rolled back)

## Best Practices

1. **Understand the Overwrite Behavior** ⚠️
   - This API **DELETES ALL existing assignments** for the specified users
   - If you want to **add** assignments while keeping existing ones, use the individual `AssignScopeAsync` API instead
   - Best for: Complete permission resets, role changes, organizational restructuring

2. **Validate References First**: Ensure all GUIDs exist before calling the API
   - Check that all user IDs exist
   - Verify board, standard, division, etc. IDs are valid
   - Use GET endpoints to confirm IDs before bulk operations
   
3. **Preview Before Execute**: Consider implementing a preview/dry-run
   - Query existing assignments using `GetAllAssignmentsAsync` 
   - Show users what will be deleted and created
   - Confirm before executing the bulk operation

4. **Limit Combinations**: Be mindful of cartesian product explosion
   - Recommended: Keep total combinations under 1000 per call
   - Example: 10 users × 10 boards × 10 standards = 1000 assignments
   
5. **Use Idempotency**: The API is naturally idempotent due to overwrite strategy
   - Running the same request multiple times yields the same result
   - Safe to retry on transient failures
   
6. **Monitor Performance**: Large bulk operations may take time
   - Consider splitting very large operations (>1000 assignments)
   - Use background jobs for massive bulk operations

## Error Handling

### Error Codes

| Code | Description | Action |
|------|-------------|--------|
| `UserScope:NoUsers` | No user IDs provided | Provide at least one user ID |
| `UserScope:UsersNotFound` | One or more users don't exist | Verify all user IDs |
| `UserScope:InvalidReferences` | Invalid board/standard/etc. | Verify all reference IDs |
| `UserScope:BulkAssignFailed` | Unexpected error | Contact support with correlation ID |

**Note**: `UserScope:DuplicateAssignment` error is no longer possible due to the overwrite strategy.

### Handling Failures

```csharp
try
{
    var result = await userScopeAppService.BulkAssignAsync(input);
    Console.WriteLine($"Success: {result.Message}");
    Console.WriteLine($"Deleted {result.DeletedAssignments} old assignments");
    Console.WriteLine($"Created {result.AssignmentsCreated} new assignments");
}
catch (UserFriendlyException ex)
{
    switch (ex.Code)
    {
        case "UserScope:InvalidReferences":
            // Handle invalid references - verify IDs
            // Note: No changes were made, existing assignments preserved
            break;
        case "UserScope:UsersNotFound":
            // Handle missing users
            break;
        default:
            // Handle other errors
            break;
    }
}
```

## Performance Considerations

- **Transaction Scope**: All assignments are created within a single database transaction
- **Rollback on Failure**: Failed operations rollback quickly
- **Cache Invalidation**: Happens only after successful commit
- **Recommended Limits**: 
  - Maximum combinations: 1000 per call
  - For larger operations: Split into multiple API calls
  - Very large operations: Use background processing

## Validation Rules

1. At least one `userId` is required
2. At least one scope parameter must be provided
3. All `userIds` must exist in the system
4. Description max length: 500 characters
5. Duplicate user IDs in the list are automatically deduplicated

## HTTP Status Codes

| Status | Description |
|--------|-------------|
| 200 OK | All assignments created successfully |
| 400 Bad Request | Validation failed or duplicate/invalid references |
| 403 Forbidden | User lacks `UserScope.BulkAssign` permission |
| 500 Internal Server Error | Unexpected error occurred |

## Migration from Old API

**Old Payload (Single Values):**
```json
{
  "userIds": ["user1", "user2"],
  "educationBoardId": "board-guid",
  "standardId": "standard-guid",
  "divisionIds": ["div1", "div2"]
}
```

**New Payload (Lists):**
```json
{
  "userIds": ["user1", "user2"],
  "educationBoardIds": ["board-guid"],
  "standardIds": ["standard-guid"],
  "divisionIds": ["div1", "div2"]
}
```

**Key Changes:**
- All singular properties are now lists (e.g., `educationBoardId` → `educationBoardIds`)
- Response type changed from `int` to `BulkScopeAssignmentResultDto`
- **OVERWRITE STRATEGY**: All existing assignments for specified users are deleted before creating new ones
- **Transactional behavior**: All operations (delete + create) succeed or none are applied
- No duplicate errors - overwrite strategy handles this automatically
- Response includes both `deletedAssignments` and `assignmentsCreated` counts
