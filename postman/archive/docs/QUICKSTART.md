# 🚀 UserScope API - Quick Start Guide

**Get up and running in 5 minutes!**

---

## ✅ Prerequisites

- [ ] Postman installed
- [ ] ShikshaMitra API running (https://localhost:44300)
- [ ] Database access (for getting entity IDs)
- [ ] Admin credentials for your tenant

---

## 📥 Step 1: Import Files (2 minutes)

### Import Collection
```
1. Open Postman
2. Click "Import" button (top-left)
3. Drag and drop: UserScope-API.postman_collection.json
4. Click "Import"
```

### Import Environment
```
1. Click "Import" button again
2. Drag and drop: ShikshaMitra.postman_environment.json
3. Click "Import"
4. Select "ShikshaMitra" environment (top-right dropdown)
```

---

## 🗄️ Step 2: Get Database IDs (2 minutes)

### Run This SQL Query

```sql
-- Set your tenant ID
DECLARE @TenantId UNIQUEIDENTIFIER = '3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e';

-- Get required IDs in one query
SELECT 
    'academic_year_id' AS Variable,
    CAST(ay.Id AS VARCHAR(50)) AS Value
FROM AcademicYears ay
WHERE ay.TenantId = @TenantId AND ay.IsCurrent = 1

UNION ALL

SELECT 
    'standard_id' AS Variable,
    CAST(st.Id AS VARCHAR(50)) AS Value
FROM Standards st
WHERE st.TenantId = @TenantId
ORDER BY st.Name
OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY

UNION ALL

SELECT 
    'division_id' AS Variable,
    CAST(d.Id AS VARCHAR(50)) AS Value
FROM Divisions d
WHERE d.TenantId = @TenantId
ORDER BY d.Name
OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY;
```

### Update Environment Variables

In Postman:
1. Click eye icon (top-right, next to environment dropdown)
2. Click "Edit" on ShikshaMitra environment
3. Update these values from SQL results:
   - `academic_year_id`
   - `standard_id`
   - `division_id`
4. Click "Save"

---

## 🔐 Step 3: Login (30 seconds)

```
1. Expand: 🔐 Authentication
2. Click: "Login (Tenant Admin)"
3. Verify __tenant header = your tenant ID
4. Click "Send"
5. ✅ Check console: "Token saved: ..."
```

**Troubleshooting:**
- 401 Error? Check username/password in environment
- Connection refused? Ensure API is running on https://localhost:44300

---

## 👤 Step 4: Create Test User (30 seconds)

```
1. Expand: 👤 User Management
2. Click: "Create User"
3. (Optional) Edit username in request body
4. Click "Send"
5. ✅ Check console: "User created: ... | ID: ..."
```

User ID is automatically saved to environment as `user_id`!

---

## 🎯 Step 5: Assign UserScope (30 seconds)

```
1. Expand: 🎯 UserScope - Assignment
2. Click: "1️⃣ Assign Specific Division"
3. Click "Send"
4. ✅ Check console: "Scope assigned | ID: ..."
```

**What just happened?**
- Teacher assigned to specific division
- Can now see students in that division only
- Scope assignment ID saved automatically

---

## ✅ Step 6: Verify (30 seconds)

```
1. Expand: 📊 UserScope - Query
2. Click: "Get User Scope"
3. Click "Send"
4. Review response:
   - assignments: [{ ... }]
   - effectiveScope: { ... }
```

**Check these values:**
```json
{
  "isAdmin": false,
  "fromCache": false,
  "assignments": [
    {
      "scopeLevel": "Division",
      "isActive": true
    }
  ],
  "effectiveScope": {
    "hasWildcardDivision": false,
    "divisionIds": ["your-division-id"]
  }
}
```

---

## 🎉 Success!

You've successfully:
- ✅ Imported Postman collection
- ✅ Configured environment
- ✅ Logged in
- ✅ Created a user
- ✅ Assigned UserScope
- ✅ Verified permissions

---

## 🎓 Next Steps

### Try More Patterns

1. **Wildcard Division** (Subject teacher)
   ```
   🎯 UserScope - Assignment → 2️⃣ Assign All Divisions
   ```

2. **Bulk Assignment** (Multiple divisions)
   ```
   🎯 UserScope - Assignment → 3️⃣ Bulk Assign
   ```

3. **Complete Workflow** (Step-by-step)
   ```
   📚 Examples → Example 1: New Teacher Setup
   ```

### Explore Collection

- **25+ pre-built requests**
- **7 assignment patterns**
- **3 complete workflows**
- **Auto-save test scripts**

---

## 🐛 Common Issues

### "401 Unauthorized"
```
Solution: Re-run Login (token expired)
```

### "User does not exist"
```
Solution: Run "Create User" first
```

### "Foreign key violation"
```
Problem: Invalid division_id or standard_id
Solution: 
1. Run SQL queries from Step 2 again
2. Verify IDs exist in your database
3. Update environment variables
```

### "Missing __tenant header"
```
Solution: 
1. Ensure environment is selected (top-right)
2. Check __tenant header = your tenant ID
```

---

## 📚 Full Documentation

For complete details:
- **UserScope-API-README.md** - Complete collection guide
- **[docs/platform/UserScope-Usage.md](../docs/platform/UserScope-Usage.md)** - UserScope model and usage

---

## 🆘 Need Help?

1. Check request description (each request has notes)
2. View test script (explains what gets auto-saved)
3. Read UserScope-API-README.md
4. Refer to [docs/platform/UserScope-Usage.md](../docs/platform/UserScope-Usage.md)

---

**Ready to test? Start with Step 1! 🚀**
