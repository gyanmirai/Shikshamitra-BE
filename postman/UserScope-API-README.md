# 🎯 UserScope API - Postman Collection

**Version:** 1.0  
**Last Updated:** December 2024

---

## 📦 What's Included

- **UserScope-API.postman_collection.json** - Complete API collection with 25+ requests
- **ShikshaMitra.postman_environment.json** - Environment variables (use existing)

---

## 🚀 Quick Start (5 Minutes)

### 1️⃣ Import Collection
```
1. Open Postman
2. Click "Import"
3. Select: UserScope-API.postman_collection.json
4. Collection appears in sidebar
```

### 2️⃣ Import Environment
```
1. Import: ShikshaMitra.postman_environment.json
2. Select environment in top-right dropdown
3. Click eye icon to view variables
```

### 3️⃣ Configure Required IDs

Run these SQL queries on your database to get entity IDs:

```sql
-- Get your tenant ID
SELECT Id, Name FROM AbpTenants;

-- Get academic year (set tenant_id first!)
SELECT Id, Year FROM AcademicYears 
WHERE TenantId = '3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e';

-- Get education hierarchy
SELECT Id, Name FROM EducationBoards WHERE TenantId = @tenantId;
SELECT Id, Name FROM EducationMediums WHERE TenantId = @tenantId;
SELECT Id, Name FROM EducationMediumStages WHERE TenantId = @tenantId;
SELECT Id, Name FROM Standards WHERE TenantId = @tenantId;
SELECT Id, Name FROM Divisions WHERE TenantId = @tenantId;
```

**Update environment variables with these IDs:**
- `tenant_id`
- `academic_year_id`
- `standard_id` (e.g., Class 10)
- `division_id` (e.g., Division A)
- `division_a_id`, `division_b_id` (for bulk examples)

### 4️⃣ Login
```
1. Open: 🔐 Authentication → Login (Tenant Admin)
2. Click "Send"
3. ✅ Token auto-saved to environment
```

### 5️⃣ Test UserScope
```
1. Open: 👤 User Management → Create User
2. Click "Send" → User ID auto-saved
3. Open: 🎯 UserScope - Assignment → 1️⃣ Assign Specific Division
4. Click "Send" → Scope assigned!
5. Verify: 📊 UserScope - Query → Get User Scope
```

---

## 📚 Collection Structure

### 🔐 Authentication
- **Login (Tenant Admin)** - Get access token

### 👤 User Management
- **Create User** - Create teacher/staff accounts
- **Get User by ID** - View user details

### 🎯 UserScope - Assignment (7 Patterns)
1. **Assign Specific Division** - Class teacher for one division
2. **Assign All Divisions (Wildcard)** - Subject teacher, all divisions
3. **Bulk Assign (Multiple Divisions)** - Teacher for 2-3 divisions
4. **Bulk Assign (Multiple Users)** - Multiple teachers, same scope
5. **Role-Based Assignment** - Default permissions for role
6. **Coordinator (Stage Level)** - All classes in education stage
7. **Principal (Full Access)** - Complete school access

### 📊 UserScope - Query
- **Get User Scope** - View effective permissions
- **Get All Assignments** - Admin view of all scopes
- **Get Assignments by User** - Filter by user
- **Get Users by Division** - Find teachers for division

### ⚙️ UserScope - Management
- **Activate Scope** - Re-enable deactivated scope
- **Deactivate Scope** - Temporarily suspend (preserves history)
- **Update Description** - Change assignment notes
- **Revoke Scope (Delete)** - Permanently delete assignment
- **Revoke All User Scopes** - Remove all user permissions

### 📚 Examples - Common Scenarios
- **Example 1: New Teacher Setup** (3-step workflow)
- **Example 2: Multi-Division Teacher** (bulk assign)
- **Example 3: Academic Year Transition** (year rollover)

---

## 🎯 Assignment Patterns Quick Reference

| Pattern | Use Case | Payload Example |
|---------|----------|-----------------|
| **Specific Division** | Class teacher | `{"standardId": "x", "divisionId": "y"}` |
| **All Divisions** | Subject teacher | `{"standardId": "x", "divisionId": null}` |
| **Multiple Divisions** | Teacher for 2-3 divisions | Bulk assign with `divisionIds: ["a", "b"]` |
| **Stage Level** | Coordinator | `{"educationMediumStageId": "x"}` |
| **Full Access** | Principal | All fields null/omitted |
| **Role-Based** | Default for role | `{"roleName": "teacher"}` instead of userId |

---

## 🔑 NULL = ALL Principle

**Omitting a field = Wildcard access at that level**

```json
{
  "userId": "teacher-guid",
  "academicYearId": "2024-guid",
  "standardId": "class-10-guid"
  // No divisionId = ALL divisions (wildcard)
}
```

**Result:** Access to ALL divisions in Class 10

---

## 🧪 Testing Workflows

### Workflow 1: Simple Assignment
```
1. Login (Tenant Admin)
2. Create User → user_id saved
3. Assign Specific Division → scope_assignment_id saved
4. Get User Scope → Verify
```

### Workflow 2: Bulk Assignment
```
1. Login
2. Create User 1 → user_id saved
3. Create User 2 → teacher_user_id saved
4. Bulk Assign (Multiple Users) → 2 users × 1 division = 2 assignments
5. Get User Scope for both users
```

### Workflow 3: Academic Year Transition
```
1. Assign New Year Scope → new_scope_assignment_id
2. Deactivate Old Year → preserves history
3. Verify User Scope → only new year active
```

---

## 🎨 Best Practices

### ✅ DO
- **Use environment variables** for all IDs (never hardcode)
- **Use Bulk Assign API** when assigning multiple divisions
- **Deactivate** instead of delete (preserves audit history)
- **Verify scope** after assignment with Get User Scope
- **Test with different roles** (teacher, coordinator, principal)

### ❌ DON'T
- Hardcode tenant IDs or entity IDs in requests
- Make multiple single-assign calls (use bulk instead)
- Delete scopes unless absolutely necessary
- Forget to set `__tenant` header for tenant operations
- Use admin token for regular operations

---

## 🔧 Environment Variables Reference

### Auto-Populated (by test scripts)
- `access_token` - Saved after login
- `user_id` - Saved after Create User
- `teacher_user_id` - Saved in examples
- `scope_assignment_id` - Saved after assignment

### Manual Configuration Required
- `tenant_id` - Your tenant GUID
- `academic_year_id` - Current academic year GUID
- `standard_id` - Standard/class GUID (e.g., Class 10)
- `division_id` - Division GUID (e.g., Division A)
- `division_a_id`, `division_b_id` - For bulk examples
- `stage_id` - Education stage GUID (optional)
- `board_id` - Education board GUID (optional)

### Pre-configured
- `base_url` = https://localhost:44300
- `tenant_username` = admin
- `tenant_password` = Temp@123

---

## 📊 API Response Examples

### Assign Scope Response
```json
{
  "id": "assignment-guid",
  "userId": "user-guid",
  "academicYearId": "2024-guid",
  "standardId": "class-10-guid",
  "divisionId": "division-A-guid",
  "description": "Class Teacher - 10-A",
  "isActive": true,
  "scopeLevel": "Division",
  "creationTime": "2024-12-02T10:30:00Z"
}
```

### Get User Scope Response
```json
{
  "userId": "user-guid",
  "userName": "teacher.demo",
  "isAdmin": false,
  "fromCache": false,
  "assignments": [
    {
      "id": "assignment-guid",
      "standardId": "class-10-guid",
      "divisionId": "division-A-guid",
      "scopeLevel": "Division",
      "isActive": true
    }
  ],
  "effectiveScope": {
    "isAdmin": false,
    "academicYearIds": ["2024-guid"],
    "standardIds": ["class-10-guid"],
    "divisionIds": ["division-A-guid"],
    "hasWildcardDivision": false,
    "hasWildcardStandard": false,
    "hasAnyAccess": true
  }
}
```

### Bulk Assign Response
```json
3
```
(Returns count of assignments created)

---

## 🐛 Common Errors & Solutions

### 401 Unauthorized
```
Solution: Re-run Login (Tenant Admin)
```

### 400 Bad Request: "Either UserId or RoleName must be provided"
```
Problem: Both userId and roleName provided
Solution: Use only ONE (XOR constraint)
```

### 404 Not Found: "User does not exist"
```
Problem: Invalid user_id
Solution: 
1. Run "Create User" first
2. Or verify user_id in environment matches database
```

### 400 Bad Request: "Foreign key violation"
```
Problem: Invalid standardId, divisionId, etc.
Solution: 
1. Verify IDs exist in database
2. Update environment variables with correct GUIDs
```

### Missing __tenant header
```
Problem: Request fails with tenant error
Solution: Ensure environment is selected (top-right dropdown)
```

---

## 📖 Related Documentation

For complete understanding of UserScope:
1. **docs/UserScope-QuickReference.md** - Quick patterns
2. **docs/UserScope-Comprehensive.md** - Complete guide
3. **docs/UserScope-Index.md** - Documentation index

---

## 🎓 Learning Path

### Beginner (15 minutes)
1. Import collection & environment
2. Configure entity IDs
3. Run "Example 1: New Teacher Setup"
4. Verify with Get User Scope

### Intermediate (30 minutes)
1. Test all 7 assignment patterns
2. Try bulk assignment with multiple users
3. Practice deactivate/activate
4. Query assignments

### Advanced (1 hour)
1. Create custom workflows
2. Test role-based assignments
3. Simulate academic year transitions
4. Explore edge cases (wildcards, cascading)

---

## ✅ Testing Checklist

Before deploying to production:

- [ ] Login works with tenant credentials
- [ ] Create user and save ID
- [ ] Assign specific division scope
- [ ] Verify scope with Get User Scope
- [ ] Test wildcard division (subject teacher)
- [ ] Bulk assign multiple divisions
- [ ] Deactivate and reactivate scope
- [ ] Test role-based assignment
- [ ] Query users by division
- [ ] Academic year filtering works

---

## 🆘 Support

### Quick Help
1. Check this README
2. View request description in Postman (each request has detailed notes)
3. Check test scripts tab for automated validation

### Detailed Help
1. **docs/UserScope-QuickReference.md** - Common patterns
2. **docs/UserScope-Comprehensive.md** - Full documentation
3. **postman/DATABASE-SETUP.sql** - Database queries

---

## 📝 Updates & Versioning

**Version 1.0** (December 2024)
- Initial release
- 25+ requests covering all UserScope APIs
- Automated test scripts
- 3 complete workflow examples
- Best practices documentation

---

**Happy Testing! 🎉**

For questions or issues, refer to the comprehensive documentation in `docs/UserScope-*.md` files.
