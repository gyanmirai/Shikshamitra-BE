# ShikshaMitra Role Setup Guide

## 🎯 Overview

This guide explains how to set up and use roles in ShikshaMitra application.

---

## 📋 Creating Roles

Roles need to be created manually in your system before you can assign them to users.

### Method 1: Using ABP Identity API

Create roles via the Identity API:

```http
POST /api/identity/roles
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "teacher",
  "isDefault": false,
  "isPublic": true
}
```

### Method 2: Using Database

You can also insert roles directly into the database:

```sql
INSERT INTO "AbpRoles" ("Id", "TenantId", "Name", "NormalizedName", "IsDefault", "IsStatic", "IsPublic", "ConcurrencyStamp")
VALUES 
  (gen_random_uuid(), NULL, 'teacher', 'TEACHER', false, false, true, gen_random_uuid()::text),
  (gen_random_uuid(), NULL, 'principal', 'PRINCIPAL', false, false, true, gen_random_uuid()::text),
  (gen_random_uuid(), NULL, 'student', 'STUDENT', false, false, true, gen_random_uuid()::text);
```

---

## 📝 Recommended Roles

Here are some common roles you might want to create:

| Role Name | Description |
|-----------|-------------|
| `teacher` | Teachers who teach subjects |
| `principal` | School principal with full access |
| `vice_principal` | Vice principal with administrative access |
| `hod` | Head of Department for specific subjects |
| `librarian` | Library management staff |
| `accountant` | Accounts and finance staff |
| `admin_staff` | General administrative staff |
| `student` | Students enrolled in the school |
| `parent` | Parent or guardian of students |

---

## 📝 Creating Users with Roles

### Using Postman

**Important:** Role names are **case-sensitive** and should be **lowercase**.

#### ✅ Correct Example
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

#### ❌ Incorrect Examples
```json
{
  "roleNames": ["TEACHER"]  // ❌ Wrong - uppercase
}

{
  "roleNames": ["Teacher"]  // ❌ Wrong - capitalized
}
```

### Multiple Roles

You can assign multiple roles to a single user:

```json
{
  "userName": "admin.jane",
  "name": "Jane",
  "surname": "Smith",
  "email": "jane@school.com",
  "phoneNumber": "+1234567890",
  "roleNames": ["principal", "teacher"],
  "password": "Admin@123"
}
```

---

## 🔧 Common Issues & Solutions

### Issue 1: "Role TEACHER does not exist!" or "Role teacher does not exist!"

**Cause:** The role hasn't been created in the database yet.

**Solution:**
1. Create the role first using the Identity API or SQL
2. Then create the user with that role name

### Issue 2: Case Sensitivity

**Note:** Role names are case-sensitive. Make sure you use the exact same case when:
- Creating the role
- Assigning it to users

**Example:**
```json
// If you created role as "teacher" (lowercase)
{
  "roleNames": ["teacher"]  // ✅ Correct
}

// Don't use different casing
{
  "roleNames": ["Teacher"]  // ❌ Wrong - will fail
}
```

---

## 🛠️ Creating Custom Roles

### Via Identity API (Recommended)

Use the ABP Identity API to create roles:

```http
POST /api/identity/roles
Content-Type: application/json
Authorization: Bearer {token}

{
  "name": "sports_coach",
  "isDefault": false,
  "isPublic": true
}
```

### Via Database

Insert directly into the database:

```sql
INSERT INTO "AbpRoles" ("Id", "TenantId", "Name", "NormalizedName", "IsDefault", "IsStatic", "IsPublic", "ConcurrencyStamp")
VALUES (gen_random_uuid(), NULL, 'sports_coach', 'SPORTS_COACH', false, false, true, gen_random_uuid()::text);
```

---

## 📖 Role Naming Conventions

### Best Practices

1. **Use lowercase with underscores** for multi-word roles
   - ✅ `vice_principal`
   - ✅ `admin_staff`
   - ❌ `VicePrincipal`
   - ❌ `Admin Staff`

2. **Be descriptive but concise**
   - ✅ `teacher`
   - ✅ `librarian`
   - ❌ `t`
   - ❌ `person_who_teaches_students`

3. **Use singular form**
   - ✅ `teacher`
   - ❌ `teachers`

---

## 🔐 Role-Based Permissions

Roles are separate from UserScope. Remember:

- **Roles** = What a user CAN do (permissions)
- **UserScope** = What data a user CAN see (data filtering)

### Example

A user with role `teacher` might have permissions to:
- Create assignments
- Grade students
- View attendance

But their **UserScope** determines:
- Which divisions they can see
- Which students they can access
- Which classes they can manage

---

## 📚 Related Documentation

- [API Endpoints](../api/endpoints.md)
- [UserScope Usage Guide](../UserScope-Usage.md)

---

## 🆘 Need Help?

If you encounter issues:

1. **Check available roles** in database: `SELECT Name FROM AbpRoles;`
2. **Verify exact role name** (case-sensitive)
3. **Create missing roles** using Identity API
4. **Check the logs** in `src/ShikshaMitra.HttpApi.Host/Logs/`

---

**Last Updated:** December 2024  
**Version:** 2.0
