# Migration Guide - Upgrade to API v2.0

> Quick guide to update from v1.0 to v2.0 list-based API.

---

## ⚡ Quick Migration

### 1. Import Updated Collection
Simply import the `UserScope-API.postman_collection.json` - it's already updated for v2.0.

### 2. Key Changes
```json
// OLD (v1.0)
{
  "userIds": ["user1"],
  "standardId": "std1",          // Singular
  "divisionIds": ["div1"]
}

// NEW (v2.0)
{
  "userIds": ["user1"],
  "standardIds": ["std1"],       // Plural, array
  "divisionIds": ["div1"]
}
```

### 3. Test Updated Endpoint
```
POST /api/app/user-scope/bulk-assign
Expected Response: { deletedAssignments: N, assignmentsCreated: M }
```

---

## 🔄 Property Name Changes

| Old | New |
|-----|-----|
| `academicYearId` | `academicYearIds` (array) |
| `educationBoardId` | `educationBoardIds` (array) |
| `educationMediumId` | `educationMediumIds` (array) |
| `educationMediumStageId` | `educationMediumStageIds` (array) |
| `standardId` | `standardIds` (array) |
| `divisionIds` | `divisionIds` (no change, already array) |

---

## 📊 Response Changes

**OLD Response:**
```json
3
```
Just a count

**NEW Response:**
```json
{
  "deletedAssignments": 2,
  "assignmentsCreated": 3
}
```
Detailed operation counts

---

## 🎯 New OVERWRITE Behavior

**v1.0:** Added assignments, kept existing  
**v2.0:** Deletes existing first, then creates new

### Example Flow
```
Initial: User has 2 assignments

Call bulk assign with 3 divisions:
Response: { deletedAssignments: 2, assignmentsCreated: 3 }

Result: User now has 3 assignments (old 2 deleted, new 3 created)
```

---

## 🔧 Migration Examples

### Example 1: Single Value
```json
// Before
{
  "userIds": ["{{user_id}}"],
  "standardId": "{{standard_id}}",
  "divisionIds": ["{{division_id}}"]
}

// After
{
  "userIds": ["{{user_id}}"],
  "standardIds": ["{{standard_id}}"],  // Wrapped in array
  "divisionIds": ["{{division_id}}"]
}
```

### Example 2: Wildcard
```json
// Before
{
  "userIds": ["{{user_id}}"],
  "standardId": "{{standard_id}}"
  // No divisionIds = wildcard
}

// After
{
  "userIds": ["{{user_id}}"],
  "standardIds": ["{{standard_id}}"]  // Wrapped in array
  // Still works the same
}
```

### Example 3: Board Level
```json
// Before
{
  "userIds": ["{{user_id}}"],
  "educationBoardId": "{{board_id}}"
}

// After
{
  "userIds": ["{{user_id}}"],
  "educationBoardIds": ["{{board_id}}"]  // Wrapped in array
}
```

---

## ✅ Testing Checklist

After migration:

- [ ] Import updated collection
- [ ] Test single division assignment
- [ ] Verify response format (object, not number)
- [ ] Test multiple divisions
- [ ] Verify overwrite behavior
- [ ] Test wildcard (no divisionIds)
- [ ] Query user scope to confirm

---

## 🆘 Common Issues

**"Invalid property name"**  
→ Use plural names: `standardIds` not `standardId`

**"Value must be an array"**  
→ Wrap values: `["{{id}}"]` not `"{{id}}"`

**Response is a number, not object**  
→ You're using old API version or wrong endpoint

**Assignments not overwriting**  
→ Check you're using bulk assign, not single assign

---

## 🎁 New Features

✨ **Multiple standards** - Assign multiple classes at once  
✨ **Multiple boards** - Cross-board assignments  
✨ **Cartesian products** - 2 standards × 3 divisions = 6 assignments  
✨ **Better responses** - Know exactly what changed  
✨ **Transactional** - All succeed or all rollback  

---

## 📝 Single Assign Unchanged

**Important:** The single assignment endpoint still works the same:

```json
POST /api/app/user-scope/assign
{
  "userId": "{{user_id}}",
  "standardId": "{{standard_id}}",    // Still singular
  "divisionId": "{{division_id}}"     // Still singular
}
```

Use this for **incremental additions** (doesn't delete existing).

---

**Migration Version:** 1.0  
**Target API:** 2.0  
**Last Updated:** December 2024
