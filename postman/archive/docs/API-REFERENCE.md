# UserScope API - Complete Reference

> All patterns, examples, and API details in one place.

---

## 🎯 Common Patterns

### 1. Single Division Assignment
```json
POST /api/app/user-scope/bulk-assign
{
  "userIds": ["{{user_id}}"],
  "standardIds": ["{{standard_id}}"],
  "divisionIds": ["{{division_id}}"]
}
```
**Result:** 1 assignment

---

### 2. Multiple Divisions (Same Standard)
```json
{
  "userIds": ["{{user_id}}"],
  "standardIds": ["{{standard_id}}"],
  "divisionIds": ["{{div_a_id}}", "{{div_b_id}}", "{{div_c_id}}"]
}
```
**Result:** 3 assignments (1 standard × 3 divisions)

---

### 3. All Divisions (Wildcard)
```json
{
  "userIds": ["{{user_id}}"],
  "standardIds": ["{{standard_id}}"]
  // No divisionIds = ALL divisions
}
```
**Result:** 1 assignment with wildcard access

---

### 4. Multiple Standards
```json
{
  "userIds": ["{{user_id}}"],
  "standardIds": ["{{std_9_id}}", "{{std_10_id}}"]
}
```
**Result:** 2 assignments (2 standards, all divisions each)

---

### 5. Multiple Users (Same Scope)
```json
{
  "userIds": ["{{teacher1_id}}", "{{teacher2_id}}", "{{teacher3_id}}"],
  "standardIds": ["{{standard_id}}"],
  "divisionIds": ["{{division_id}}"]
}
```
**Result:** 3 assignments (3 users × 1 scope)

---

### 6. Full Board Access (Principal)
```json
{
  "userIds": ["{{principal_id}}"],
  "educationBoardIds": ["{{cbse_id}}"]
}
```
**Result:** 1 assignment (access to entire board)

---

### 7. Cartesian Product (Complex)
```json
{
  "userIds": ["{{teacher_id}}"],
  "standardIds": ["{{std_9_id}}", "{{std_10_id}}"],
  "divisionIds": ["{{div_a_id}}", "{{div_b_id}}"]
}
```
**Result:** 4 assignments
- Class 9 - Division A
- Class 9 - Division B
- Class 10 - Division A
- Class 10 - Division B

---

### 8. Stage Level (Coordinator)
```json
{
  "userIds": ["{{coordinator_id}}"],
  "educationMediumStageIds": ["{{secondary_stage_id}}"]
}
```
**Result:** 1 assignment (all classes in stage)

---

## 📐 API Structure

### Bulk Assignment DTO
```typescript
{
  userIds: Guid[]                    // REQUIRED: Min 1
  academicYearIds: Guid[]            // REQUIRED: year slice to replace
  educationBoardIds?: Guid[]         // NULL/empty = ALL
  educationMediumIds?: Guid[]        // NULL/empty = ALL
  educationMediumStageIds?: Guid[]   // NULL/empty = ALL
  standardIds?: Guid[]               // NULL/empty = ALL
  divisionIds?: Guid[]               // NULL/empty = ALL
  description?: string               // Max 500 chars
}
```

### Response Format
```json
{
  "deletedAssignments": 2,
  "assignmentsCreated": 4
}
```

---

## 🔄 OVERWRITE Strategy

**Important:** Bulk assign now overwrites existing assignments only within the supplied academic years.

1. **Deletes** existing assignments for specified users and supplied academic years
2. **Creates** new assignments based on parameters
3. **Transactional** - all succeed or all rollback

### Example
```
First call:  { deletedAssignments: 0, assignmentsCreated: 1 }
Second call: { deletedAssignments: 1, assignmentsCreated: 2 }
             ↑ Previous deleted    ↑ New created
```

---

## 📊 Calculation Formula

**Total Assignments =**
```
users × academicYears × boards × mediums × stages × standards × divisions
```

Where NULL/empty below academic year = 1 (wildcard)

### Examples
| Users | Standards | Divisions | Result |
|-------|-----------|-----------|--------|
| 1 | 1 | 1 | 1 |
| 1 | 2 | 3 | 6 |
| 3 | 1 | 2 | 6 |
| 2 | 2 | 2 | 8 |

---

## 🔍 Query Endpoints

### Get User Scope
```
GET /api/app/user-scope/{userId}
```

### Get Users by Division
```
GET /api/app/user-scope/users-by-division/{divisionId}
```

### Get All Assignments
```
GET /api/app/user-scope/all-assignments
```

---

## ⚙️ Management Endpoints

### Activate/Deactivate
```
PUT /api/app/user-scope/{id}/activate
PUT /api/app/user-scope/{id}/deactivate
```

### Delete (Revoke)
```
DELETE /api/app/user-scope/{id}
DELETE /api/app/user-scope/revoke-all/{userId}
```

---

## ✅ Validation Rules

**Required:**
- At least 1 `userId`
- At least 1 scope parameter with values

**Valid:**
```json
✅ { "userIds": ["user1"], "academicYearIds": ["2024"], "standardIds": ["std1"] }
✅ { "userIds": ["user1"], "academicYearIds": ["2024"], "standardIds": null, "divisionIds": ["div1"] }
```

**Invalid:**
```json
❌ { "userIds": ["user1"] }  // Missing academicYearIds
❌ { "userIds": [] }          // No users
```

---

## 🎓 Use Cases

### Teacher - Class Teacher (Single Division)
```json
{
  "userIds": ["{{teacher_id}}"],
  "standardIds": ["{{class_10_id}}"],
  "divisionIds": ["{{div_a_id}}"],
  "description": "Class Teacher - 10-A"
}
```

### Teacher - Subject Teacher (All Divisions)
```json
{
  "userIds": ["{{teacher_id}}"],
  "standardIds": ["{{class_10_id}}"],
  "description": "Math Teacher - All Class 10"
}
```

### Teacher - Multi-Division
```json
{
  "userIds": ["{{teacher_id}}"],
  "standardIds": ["{{class_10_id}}"],
  "divisionIds": ["{{div_a_id}}", "{{div_b_id}}"],
  "description": "Science Teacher - 10-A & 10-B"
}
```

### Coordinator - Stage Level
```json
{
  "userIds": ["{{coordinator_id}}"],
  "educationMediumStageIds": ["{{secondary_id}}"],
  "description": "Coordinator - Secondary Classes"
}
```

### Principal - Full Access
```json
{
  "userIds": ["{{principal_id}}"],
  "educationBoardIds": ["{{cbse_id}}"],
  "description": "Principal - CBSE Board"
}
```

---

## 🆚 Single vs Bulk Assign

### Single Assign (Additive)
```
POST /api/app/user-scope/assign
{
  "userId": "...",
  "standardId": "...",    // Note: Singular
  "divisionId": "..."     // Note: Singular
}
```
- ✅ Adds ONE assignment
- ✅ Keeps existing assignments
- ✅ Uses singular property names

### Bulk Assign (Overwrite)
```
POST /api/app/user-scope/bulk-assign
{
  "userIds": ["..."],
  "standardIds": ["..."],  // Note: Plural, array
  "divisionIds": ["..."]   // Note: Plural, array
}
```
- ✅ Creates multiple assignments
- ⚠️ Deletes existing assignments first
- ✅ Uses plural property names

---

## 🔧 Environment Variables

Set these in Postman environment:

```
tenant_id         - Your tenant GUID (from GET-TENANT-ID.sql)
user_id           - Test user ID (auto-saved)
standard_id       - Standard/Class ID
division_id       - Division ID
division_a_id     - For multi-division examples
division_b_id     - For multi-division examples
board_id          - Education board ID
stage_id          - Education stage ID
access_token      - Auto-saved on login
```

---

## 🐛 Common Errors

**400 - At least one scope parameter must be provided**
→ Add standardIds, divisionIds, or other scope parameter

**400 - At least one user ID must be provided**
→ Add userId to userIds array

**404 - User does not exist**
→ Create user first or verify user_id

**Foreign key violation**
→ Verify standardIds, divisionIds exist in database

**401 Unauthorized**
→ Re-run Login request

---

## 📝 Best Practices

1. ✅ Use **bulk assign** for complete scope redefinition
2. ✅ Use **single assign** for incremental additions
3. ✅ Use **environment variables** for all IDs
4. ✅ Verify response counts match expectations
5. ✅ Test with `GET /api/app/user-scope/{userId}` after assignment

---

## 📚 Collection Structure

```
UserScope-API Collection
├── 🔐 Authentication
│   └── Login (Tenant Admin)
├── 👤 User Management
│   ├── Create User
│   └── Get User by ID
├── 🎯 UserScope - Assignment
│   ├── 1️⃣ Assign Specific Division
│   ├── 2️⃣ Assign All Divisions (Wildcard)
│   ├── 3️⃣ Bulk Assign (Multiple Divisions)
│   ├── 4️⃣ Bulk Assign (Multiple Users)
│   ├── 5️⃣ Role-Based Assignment
│   ├── 6️⃣ Coordinator (Stage Level)
│   └── 7️⃣ Principal (Full Access)
├── 📊 UserScope - Query
│   ├── Get User Scope
│   ├── Get All Assignments
│   ├── Get Assignments by User
│   └── Get Users by Division
├── ⚙️ UserScope - Management
│   ├── Activate Scope
│   ├── Deactivate Scope
│   ├── Update Description
│   ├── Revoke Scope (Delete)
│   └── Revoke All User Scopes
└── 📚 Examples
    ├── Example 1: New Teacher Setup
    ├── Example 2: Multi-Division Teacher
    └── Example 3: Academic Year Transition
```

---

**Version:** 2.0 (List-based API)  
**Last Updated:** December 2024
