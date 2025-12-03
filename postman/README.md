# ShikshaMitra Postman Collection

Multi-tenant school management API testing collection with automated setup.

## 📦 Quick Start

### 1. Import Files
```
Postman → Import → Select both files:
  ├── ShikshaMitra.postman_collection.json
  └── ShikshaMitra.postman_environment.json
```

### 2. Select Environment
```
Top-right dropdown → "ShikshaMitra - Development"
```

### 3. Get Database IDs (One-time)
Run queries from `DATABASE-SETUP.sql` to get actual entity IDs from your database:
```sql
-- Copy tenant ID, academic year ID, board ID, etc.
-- Paste into environment variables
```

### 4. Start Testing
```
1. Authentication → Tenant User Login (SSVM)
   → Automatically saves access_token

2. Create/manage users, roles, scopes
   → IDs auto-saved to environment
```

## 🔐 Authentication

### Two Login Types

| Type | Token Variable | Use For | Header |
|------|---------------|---------|--------|
| **Host Admin** | `admin_access_token` | ⚠️ Tenant creation ONLY | No `__tenant` |
| **Tenant User** | `access_token` | ✅ All tenant operations | With `__tenant` |

### Pre-configured SSVM Tenant
```
Tenant ID: 3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e
Username: admin
Password: Temp@123
```

## 🎯 Collection Structure

```
Authentication
├── Host Admin Login             → admin_access_token (tenant creation only)
└── Tenant User Login (SSVM)     → access_token (all operations) ✅

Tenant Management (Host Admin)
├── Create Tenant
└── Get All Tenants

User Management
├── Create User                  → Saves user_id
├── Get Users
├── Update User
└── Delete User

Role Management
├── Get Roles
├── Create Role
└── Assign Role to User

UserScope - Assignment
├── Assign Scope                 → Saves scope_assignment_id
├── Bulk Assign
└── Class/Stage Level Assignment

UserScope - Query
└── Get User Scopes

UserScope - Management
├── Update Scope
└── Delete Scope
```

## ⚙️ Automated Features

### Auto-saved Variables
All IDs are automatically saved to environment after creation:
- `access_token` - After login
- `user_id` - After user creation
- `scope_assignment_id` - After scope assignment
- `teacher_user_id` - From examples
- `principal_user_id` - From examples

### Test Scripts
Each request includes test scripts for:
- Token extraction and storage
- ID capture from responses
- Variable population
- Response validation

## 📝 Common Workflows

### Create User & Assign Scope
```
1. Tenant User Login
2. User Management → Create User
   → user_id auto-saved
3. UserScope → Assign Scope
   → Use {{user_id}} from previous step
```

### Setup New Tenant
```
1. Host Admin Login
   → admin_access_token saved
2. Tenant Management → Create Tenant
   → Note the tenant ID
3. Update environment:
   - Set tenant_id = new tenant ID
4. Tenant User Login
   → Ready to work with new tenant
```

## 🗄️ Database Setup

Use `DATABASE-SETUP.sql` to:
1. Get existing entity IDs from database
2. Set up test data
3. Configure environment variables

```sql
-- Example: Get IDs for your environment
SELECT Id, Name FROM AbpTenants;
SELECT Id, Year FROM AcademicYears WHERE TenantId = @tenantId;
SELECT Id, Type FROM EducationBoards WHERE TenantId = @tenantId;
```

## 🔧 Environment Variables

### Pre-configured
- `base_url` = https://localhost:44300
- `tenant_id` = 3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e
- `tenant_username` = admin
- `tenant_password` = Temp@123

### Set from Database
Run SQL queries to get these IDs:
- `academic_year_id`
- `board_id`
- `medium_id`
- `stage_id`
- `standard_id`
- `division_id`

### Auto-populated
These are set automatically by test scripts:
- `access_token`
- `admin_access_token`
- `user_id`
- `scope_assignment_id`

## 🎨 Visual Guide

For detailed authentication flows and architecture diagrams, see:
- **[POSTMAN_VISUAL_GUIDE.md](./POSTMAN_VISUAL_GUIDE.md)**

## ⚠️ Important Rules

### ❌ Don't
- Use Host Admin token for user/role/scope operations
- Forget `__tenant` header for tenant operations
- Hardcode IDs (use environment variables)

### ✅ Do
- Always use Tenant User Login for regular work
- Let test scripts auto-save IDs
- Update database IDs in environment from SQL queries
- Use {{variable}} syntax for all IDs

## 🐛 Troubleshooting

| Issue | Solution |
|-------|----------|
| 401 Unauthorized | Re-run Tenant User Login |
| 403 Forbidden | Check if using correct token (tenant user, not host admin) |
| 404 Not Found | Verify entity IDs in environment match your database |
| Missing __tenant | Ensure environment is selected |

## 📚 Files in This Folder

```
postman/
├── ShikshaMitra.postman_collection.json    ⭐ Main collection
├── ShikshaMitra.postman_environment.json   ⭐ Environment variables
├── DATABASE-SETUP.sql                       📊 Get DB IDs
├── POSTMAN_VISUAL_GUIDE.md                 📐 Architecture diagrams
└── README.md                                📖 This file
```

## 🎯 Best Practices

1. **Single Source of Truth**: Use environment variables, not hardcoded values
2. **Automation First**: Leverage test scripts for ID management
3. **Separation of Concerns**: Host admin vs tenant user contexts
4. **Clean Architecture**: Each request does one thing well
5. **DRY Principle**: Variables eliminate duplication

---

**Version**: 2.0  
**Last Updated**: November 28, 2025  
**Maintained By**: ShikshaMitra Development Team
