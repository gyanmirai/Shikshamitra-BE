# ShikshaMitra UserScope API - Quick Reference

## 📁 Files to Import

✅ `ShikshaMitra-UserScope-Postman-Collection.json`  
✅ `ShikshaMitra-Development-Environment.json`

---

## 🎯 Quick Setup (3 Steps)

1. **Import** both files into Postman
2. **Select** "ShikshaMitra - Development" environment
3. **Run** Authentication > Login

---

## 🔑 API Endpoints Summary

### Authentication
```
POST /connect/token - Login (auto-saves token)
```

### User Management
```
POST   /api/identity/users          - Create user
GET    /api/identity/users          - List users
GET    /api/identity/users/{id}     - Get user
PUT    /api/identity/users/{id}     - Update user
DELETE /api/identity/users/{id}     - Delete user
```

### UserScope - Assignment
```
POST   /api/app/user-scope/assign       - Assign single scope
POST   /api/app/user-scope/bulk-assign  - Assign multiple scopes
DELETE /api/app/user-scope/{id}         - Revoke scope
DELETE /api/app/user-scope/revoke-all/{userId} - Revoke all user scopes
```

### UserScope - Query
```
GET /api/app/user-scope/{userId}                   - Get user scopes
GET /api/app/user-scope/users-by-division/{id}     - Users in division
GET /api/app/user-scope/users-by-standard/{id}     - Users in class
GET /api/app/user-scope/all-assignments            - All assignments
```

### UserScope - Management
```
PUT /api/app/user-scope/{id}/activate    - Activate scope
PUT /api/app/user-scope/{id}/deactivate  - Deactivate scope
PUT /api/app/user-scope/{id}/description - Update description
```

---

## 📝 Common Request Bodies

### Create User
```json
{
  "userName": "teacher.john",
  "name": "John",
  "surname": "Doe",
  "email": "john@school.com",
  "phoneNumber": "+1234567890",
  "roleNames": ["teacher"],
  "password": "Teacher@123"
}
```

### Assign Scope - Division Level
```json
{
  "userId": "{{user_id}}",
  "standardId": "{{standard_id}}",
  "divisionId": "{{division_id}}",
  "description": "Math Teacher - Class 5A"
}
```

### Assign Scope - Class Level (All Divisions)
```json
{
  "userId": "{{user_id}}",
  "standardId": "{{standard_id}}",
  "description": "Class Teacher - All Class 5"
}
```

### Assign Scope - Board Level (Full Access)
```json
{
  "userId": "{{user_id}}",
  "educationBoardId": "{{board_id}}",
  "description": "Principal - Full CBSE"
}
```

### Bulk Assign
```json
{
  "assignments": [
    {
      "userId": "{{user_id}}",
      "standardId": "{{standard_id}}",
      "divisionId": "{{division_a_id}}",
      "description": "Division A"
    },
    {
      "userId": "{{user_id}}",
      "standardId": "{{standard_id}}",
      "divisionId": "{{division_b_id}}",
      "description": "Division B"
    }
  ]
}
```

---

## 🎓 NULL = ALL Hierarchy

**From Least to Most Specific:**

1. **Board** → 2. **Medium** → 3. **Stage** → 4. **Standard** → 5. **Division**

**NULL = ALL at that level**

| Assignment | Access |
|------------|--------|
| boardId only | All below board |
| stageId only | All classes/divisions in stage |
| standardId only | All divisions in class |
| standardId + divisionId | Only that division |

---

## ⚡ Quick Test Workflow

```
1. Login → GET TOKEN
   ↓
2. Create User → GET USER_ID
   ↓
3. Assign Scope → GET SCOPE_ID
   ↓
4. Get User Scope → VERIFY
   ↓
5. Test Filtering → SUCCESS!
```

---

## 🔧 Required Environment Variables

**Must Set Manually:**
- `user_id` - From Create User response
- `standard_id` - From your database
- `division_id` - From your database
- `board_id` - From your database (optional)
- `stage_id` - From your database (optional)

**Auto-Set by Collection:**
- `access_token` - From Login
- `scope_assignment_id` - From Assign Scope

---

## 🐛 Quick Troubleshooting

| Error | Fix |
|-------|-----|
| 401 Unauthorized | Re-run Login |
| User exists | Change userName |
| Invalid GUID | Check environment variables |
| Foreign key error | Verify IDs exist in DB |
| Role does not exist | Create role first via Identity API: POST /api/identity/roles |

---

## 📊 Test Scenarios

### Scenario 1: Teacher with 2 Divisions
```
1. Create User
2. Bulk Assign (Division A + B)
3. Verify Scope
```

### Scenario 2: Principal Full Access
```
1. Create User
2. Assign Board-Level Scope
3. Verify Access to All
```

### Scenario 3: Class Teacher
```
1. Create User
2. Assign Standard-Level (no division)
3. Verify Access to All Divisions
```

---

## 🎯 Status Codes

- `200` - Success
- `201` - Created
- `204` - Success (no body)
- `400` - Bad request
- `401` - Unauthorized (re-login)
- `403` - Forbidden (no permission)
- `404` - Not found

---

## 💡 Pro Tips

1. ✅ **Always login first** - Token expires in 1 hour
2. ✅ **Use environment variables** - Don't hardcode GUIDs
3. ✅ **Test with actual data** - Get IDs from your database
4. ✅ **Start specific → general** - Test division level first
5. ✅ **Use bulk assign** - More efficient for multiple scopes

---

**Quick Reference Version**: 1.0  
**Date**: November 26, 2024
