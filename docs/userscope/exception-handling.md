# UserScope Exception Handling Guide

## 🎯 Overview

The UserScope service now includes comprehensive exception handling to provide clear, user-friendly error messages instead of generic 500 Internal Server Errors.

---

## ✅ Exception Handling Strategy

### 1. **UserFriendlyException**
Used for expected business errors that should be shown to the user:
- Invalid input
- Entity not found
- Duplicate assignments
- Foreign key violations

### 2. **Logging**
All exceptions are logged with context for debugging:
```csharp
_logger.LogError(ex, "Error assigning user scope. UserId: {UserId}", userId);
```

### 3. **Detailed Error Messages in Development**
With the development configuration:
- Full exception messages
- Stack traces
- Error codes

---

## 📋 Error Codes Reference

| Error Code | HTTP Status | Description | Solution |
|------------|-------------|-------------|----------|
| `UserScope:InvalidInput` | 400 | Both UserId and RoleName provided, or neither | Provide only one |
| `UserScope:UserNotFound` | 400 | User with specified ID doesn't exist | Verify user ID exists |
| `UserScope:AlreadyExists` | 400 | Scope assignment already exists | Skip or update existing |
| `UserScope:ForeignKeyViolation` | 400 | Referenced entity doesn't exist | Verify Standard/Division/Board IDs |
| `UserScope:AssignmentFailed` | 500 | General assignment error | Check logs for details |
| `UserScope:NoUsers` | 400 | Bulk assign without user IDs | Provide at least one user ID |
| `UserScope:BulkAssignmentFailed` | 500 | Error during bulk assignment | Check logs |
| `UserScope:AssignmentNotFound` | 404 | Scope assignment doesn't exist | Verify assignment ID |
| `UserScope:RevokeFailed` | 500 | Error revoking assignment | Check logs |
| `UserScope:GetScopeFailed` | 500 | Error retrieving scope | Check logs |

---

## 📝 Error Response Examples

### Invalid Input Error
```json
{
  "error": {
    "code": "UserScope:InvalidInput",
    "message": "Either UserId or RoleName must be provided, but not both.",
    "details": null,
    "validationErrors": null
  }
}
```

### User Not Found
```json
{
  "error": {
    "code": "UserScope:UserNotFound",
    "message": "User with ID '3a1dc7ba-324a-8bad-5467-7485ff3019e2' does not exist.",
    "details": null,
    "validationErrors": null
  }
}
```

### Foreign Key Violation
```json
{
  "error": {
    "code": "UserScope:ForeignKeyViolation",
    "message": "One or more referenced entities (Standard, Division, Board, Medium, etc.) do not exist. Please verify all IDs are valid.",
    "details": "Full stack trace in development mode",
    "validationErrors": null
  }
}
```

### Duplicate Assignment
```json
{
  "error": {
    "code": "UserScope:AlreadyExists",
    "message": "This scope assignment already exists for the user/role with the specified parameters.",
    "details": null,
    "validationErrors": null
  }
}
```

---

## 🔧 Methods with Exception Handling

### 1. AssignScopeAsync
**Handles:**
- Invalid input (both or neither UserId/RoleName)
- User doesn't exist
- Duplicate assignment
- Foreign key violations
- General database errors

**Example:**
```http
POST /api/app/user-scope/assign-scope
{
  "userId": "invalid-guid",
  "standardId": "valid-guid"
}

Response 400:
{
  "error": {
    "code": "UserScope:UserNotFound",
    "message": "User with ID 'invalid-guid' does not exist."
  }
}
```

### 2. BulkAssignAsync
**Handles:**
- No user IDs provided
- Individual user failures (continues processing)
- Foreign key violations
- Database errors

**Behavior:** Partial success - continues even if some users fail

### 3. RevokeScopeAsync
**Handles:**
- Assignment not found
- Database errors

### 4. GetUserScopeAsync
**Handles:**
- User not found
- Database errors
- Mapping errors

### 5. RevokeUserScopesAsync
**Handles:**
- User not found
- Database errors

---

## 🚀 Testing Exception Handling

### Test 1: Invalid Input
```bash
POST /api/app/user-scope/assign-scope
{
  # Both userId and roleName provided
  "userId": "3a1dc7ba-324a-8bad-5467-7485ff3019e2",
  "roleName": "teacher"
}

Expected: 400 Bad Request with UserScope:InvalidInput
```

### Test 2: User Not Found
```bash
POST /api/app/user-scope/assign-scope
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "standardId": "valid-guid"
}

Expected: 400 Bad Request with UserScope:UserNotFound
```

### Test 3: Foreign Key Violation
```bash
POST /api/app/user-scope/assign-scope
{
  "userId": "valid-user-guid",
  "standardId": "non-existent-standard-guid"
}

Expected: 400 Bad Request with UserScope:ForeignKeyViolation
```

### Test 4: Duplicate Assignment
```bash
# Call same endpoint twice with identical data
POST /api/app/user-scope/assign-scope
{
  "userId": "valid-user-guid",
  "standardId": "valid-standard-guid"
}

First call: 200 Success
Second call: 400 Bad Request with UserScope:AlreadyExists
```

---

## 📊 Logging

All operations are logged with appropriate log levels:

### Information Level
```csharp
_logger.LogInformation("User scope assigned successfully. AssignmentId: {AssignmentId}", id);
```

### Error Level
```csharp
_logger.LogError(ex, "Error assigning user scope. UserId: {UserId}", userId);
```

### Debug Level
```csharp
_logger.LogDebug("Getting scope information for user {UserId}", userId);
```

---

## 💡 Best Practices

### 1. Always Check Error Codes
```javascript
// Frontend example
try {
  await assignScope(data);
} catch (error) {
  if (error.code === 'UserScope:UserNotFound') {
    // Show user-specific message
  } else if (error.code === 'UserScope:ForeignKeyViolation') {
    // Prompt to verify IDs
  }
}
```

### 2. Handle Partial Success in Bulk Operations
```javascript
const result = await bulkAssign({ userIds: [id1, id2, id3] });
console.log(`Successfully assigned: ${result} out of 3`);
```

### 3. Use Development Mode for Debugging
- Enable detailed errors in `appsettings.Development.json`
- Check stack traces in error.details
- Review application logs

---

## 🔍 Troubleshooting

### Q: Still getting 500 errors?
**A:** Check:
1. Are you in development mode?
2. Is `SendExceptionsDetailsToClients` enabled?
3. Check application logs for unhandled exceptions

### Q: Error message not specific enough?
**A:** In development mode:
1. Check `error.details` for stack trace
2. Review application logs
3. Enable debug logging

### Q: How to test exception handling?
**A:**
1. Use invalid GUIDs
2. Reference non-existent entities
3. Duplicate requests
4. Send malformed data

---

## 📚 Related Documentation

- [Development Error Handling](./Development-Error-Handling.md)
- [API Quick Reference](./API-Quick-Reference.md)
- [UserScope Permissions Setup](./UserScope-Permissions-Setup.md)

---

**Last Updated:** December 2024  
**Version:** 1.0  
**Applies To:** All UserScope API endpoints
