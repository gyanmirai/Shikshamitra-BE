# ShikshaMitra API - Postman Collections

Comprehensive Postman collections for testing all ShikshaMitra APIs with automated token management, ID extraction, and complete workflow examples.

---

## 📦 Available Collections

| Collection | File | Documentation | Endpoints | Description |
|------------|------|---------------|-----------|-------------|
| **🏢 Main** | `ShikshaMitra.postman_collection.json` | [README](#main-collection) | 25+ | Auth, Users, Basic UserScope |
| **👥 HR Module** | `HR-Module.postman_collection.json` | [HR-Module-README.md](./HR-Module-README.md) | 40+ | Leave, Payroll, Employee Management |
| **🏢 Rooms** | `RoomsManagement.postman_collection.json` | [RoomsManagement-README.md](./RoomsManagement-README.md) | 18+ | Buildings, Room Types, Rooms |
| **🔍 UserScope** | `UserScope-API.postman_collection.json` | [QUICKSTART.md](./QUICKSTART.md) | 30+ | Advanced UserScope Testing |

---

## 🚀 Quick Start (3 minutes)

### 1️⃣ Import Collection (1 min)

**Choose your collection based on what you're testing:**

<details>
<summary><b>🔹 HR Module APIs</b> (Leave, Payroll, Employees)</summary>

```
Open Postman → Import:
  ✅ HR-Module.postman_collection.json
  ✅ ShikshaMitra-Consolidated.postman_environment.json

Select Environment: "ShikshaMitra - All Modules"
```

**Pre-configured variables:**
- ✅ base_url, staging_url
- ✅ tenant_id, employee_id
- ✅ username/password

📖 [Full HR Module Guide →](./HR-Module-README.md)

</details>

<details>
<summary><b>🔹 UserScope APIs</b> (Permissions, Scope Assignment)</summary>

```
Open Postman → Import:
  ✅ UserScope-API.postman_collection.json
  ✅ ShikshaMitra-Consolidated.postman_environment.json

Select Environment: "ShikshaMitra - All Modules"
```

**Follow quick setup:**
📖 [6-Step Quick Start →](./QUICKSTART.md)

</details>

<details>
<summary><b>🔹 Main Collection</b> (Basic APIs)</summary>

```
Open Postman → Import:
  ✅ ShikshaMitra.postman_collection.json
  ✅ ShikshaMitra-Consolidated.postman_environment.json

Select Environment: "ShikshaMitra - All Modules"
```

</details>

<details>
<summary><b>🔹 Rooms Management</b> (Buildings, Rooms)</summary>

```
Open Postman → Import:
  ✅ RoomsManagement.postman_collection.json
  ✅ ShikshaMitra-Consolidated.postman_environment.json

Select Environment: "ShikshaMitra - All Modules"
```

📖 [Rooms Management Guide →](./RoomsManagement-README.md)

</details>

### 2️⃣ Configure Environment (1 min)

**Update these key variables in your environment:**

```javascript
base_url       = "https://localhost:44300"          // or staging URL
tenant_id      = "your-tenant-id"                   // Get from database
username       = "admin"                            // Your username
password       = "Temp@123"                         // Your password
```

💡 **Pro Tip:** Use `DATABASE-SETUP.sql` to get IDs from your database

### 3️⃣ Authenticate & Test (1 min)

```
1. Run: "Authentication → Login"
   → access_token auto-saved ✅

2. Test any endpoint
   → All requests use token automatically
```

---

## 📖 Main Collection

### Features

- ✅ **Auto Token Management** - Login once, use everywhere
- ✅ **Auto ID Extraction** - Response IDs saved to environment
- ✅ **Host vs Tenant Context** - Clear separation
- ✅ **Pre-configured Examples** - Real-world workflows

### API Coverage

```
🔐 Authentication
├── Host Admin Login         → For tenant management
└── Tenant User Login        → For regular operations

👤 User Management
├── Create User              → Saves user_id
├── Get Users
├── Update User
└── Delete User

👥 Role Management
├── Get Roles
├── Create Role
└── Assign Role to User

🎯 UserScope - Assignment
├── Assign Scope             → Saves scope_assignment_id
├── Bulk Assign
└── Class/Stage Level Assignment

🎯 UserScope - Query
└── Get User Scopes

🎯 UserScope - Management
├── Update Scope
└── Delete Scope
```

### Auto-saved Variables

All IDs are automatically saved to environment after creation:

| Variable | Saved From | Used In |
|----------|------------|---------|
| `access_token` | Login response | All authenticated requests |
| `user_id` | Create User | User operations |
| `scope_assignment_id` | Assign Scope | Scope management |

### Common Workflows

#### Create User & Assign Scope
```
1. Tenant User Login
2. User Management → Create User
   → user_id auto-saved
3. UserScope → Assign Scope
   → Use {{user_id}} from previous step
```

#### Setup New Tenant
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

---

## 🎯 Collection-Specific Guides

### HR Module Collection
**File:** `HR-Module.postman_collection.json`  
**Documentation:** [HR-Module-README.md](./HR-Module-README.md)

**Includes:**
- 📋 Leave Management (Types & Policies)
- 💰 Payroll Management (Earning & Deduction Components)
- 👤 Employee Management (Documents, Payroll, Additional Details)

**Features:**
- ✅ 40+ endpoints
- ✅ Multipart/form-data for file uploads
- ✅ Auto-save component IDs
- ✅ Local & staging environment support

**Quick Example:**
```json
POST /api/payroll/earning-components
{
  "name": "Basic Salary",
  "componentType": 0,
  "amount": "50000"
}
→ earning_component_id auto-saved
```

### UserScope API Collection
**File:** `UserScope-API.postman_collection.json`  
**Documentation:** [QUICKSTART.md](./QUICKSTART.md)

**Includes:**
- 🎯 7 Assignment Patterns
- 📊 Scope Query & Verification
- 🔄 Complete Workflows

**Features:**
- ✅ 30+ endpoints
- ✅ Step-by-step examples
- ✅ Database setup scripts
- ✅ Detailed validation

**Quick Example:**
```
Run: "1️⃣ Assign Specific Division"
→ Teacher assigned to specific division
→ scope_assignment_id auto-saved
```

### Rooms Management Collection
**File:** `RoomsManagement.postman_collection.json`  
**Documentation:** [RoomsManagement-README.md](./RoomsManagement-README.md)

**Includes:**
- 🏢 Building Management
- 🚪 Room Type Management
- 📐 Room Management

**Features:**
- ✅ 18+ endpoints
- ✅ CRUD operations
- ✅ Pagination support

---

## 🔧 Environment Variables

### Core Variables (All Collections)

| Variable | Description | Example | Required |
|----------|-------------|---------|----------|
| `base_url` | API base URL | `https://localhost:44300` | ✅ |
| `tenant_id` | Tenant identifier | `3a1dc7ba-...` | ✅ |
| `username` | Login username | `admin` | ✅ |
| `password` | Login password | `Temp@123` | ✅ |
| `access_token` | Auth token | Auto-saved | Auto |

### HR Module Variables

| Variable | Description | Auto-saved |
|----------|-------------|------------|
| `employee_id` | Test employee ID | ❌ Manual |
| `document_id` | Document ID | ✅ Yes |
| `leave_type_id` | Leave type ID | ✅ Yes |
| `earning_component_id` | Earning component ID | ✅ Yes |
| `deduction_component_id` | Deduction component ID | ✅ Yes |

### UserScope Variables

| Variable | Description | Auto-saved |
|----------|-------------|------------|
| `user_id` | Created user ID | ✅ Yes |
| `scope_assignment_id` | Scope assignment ID | ✅ Yes |
| `academic_year_id` | Academic year ID | ❌ From DB |
| `standard_id` | Standard ID | ❌ From DB |
| `division_id` | Division ID | ❌ From DB |

💡 **Get DB IDs:** Use `DATABASE-SETUP.sql` queries

---

## 📊 Database Setup

### Quick ID Extraction

Run these queries to get required IDs:

```sql
-- Set your tenant ID
DECLARE @TenantId UNIQUEIDENTIFIER = '3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e';

-- Get Academic Year ID
SELECT Id, Year FROM AcademicYears 
WHERE TenantId = @TenantId AND IsCurrent = 1;

-- Get Board ID
SELECT Id, Type FROM EducationBoards WHERE TenantId = @TenantId;

-- Get Standard ID
SELECT Id, Name FROM Standards WHERE TenantId = @TenantId;

-- Get Division ID
SELECT Id, Name FROM Divisions WHERE TenantId = @TenantId;
```

**Update environment with results:**
1. Click eye icon (top-right)
2. Edit environment
3. Paste IDs from SQL results
4. Save

📄 **Full SQL Script:** [DATABASE-SETUP.sql](./DATABASE-SETUP.sql)

---

## ⚙️ Automated Features

### Token Management
```javascript
// Automatically included in all requests
Authorization: Bearer {{access_token}}
__tenant: {{tenant_id}}
```

### ID Auto-Extraction
```javascript
// Test script example (auto-included)
if (pm.response.code === 200) {
    const response = pm.response.json();
    pm.environment.set('user_id', response.id);
    console.log('✅ User ID saved:', response.id);
}
```

### Response Validation
```javascript
// Validates successful responses
pm.test("Status is 200", () => {
    pm.response.to.have.status(200);
});
```

---

## 🐛 Troubleshooting

### Common Issues

<details>
<summary><b>❌ 401 Unauthorized</b></summary>

**Cause:** Token expired or not set

**Solution:**
```
1. Run: Authentication → Login
2. Verify access_token in environment
3. Retry failed request
```

</details>

<details>
<summary><b>❌ 403 Forbidden</b></summary>

**Cause:** Insufficient permissions

**Solution:**
```
1. Verify you're using Tenant User token (not Host Admin)
2. Check user has required permissions
3. Login with admin account
```

</details>

<details>
<summary><b>❌ 404 Not Found</b></summary>

**Cause:** Invalid ID in URL

**Solution:**
```
1. Check ID variable exists in environment
2. Verify ID format is correct GUID
3. Run "Get All" request to find valid IDs
```

</details>

<details>
<summary><b>❌ 415 Unsupported Media Type</b></summary>

**Cause:** Wrong Content-Type for file upload

**Solution:**
```
1. Ensure request uses multipart/form-data
2. Select files in form-data body
3. Don't add Content-Type header manually
```

</details>

<details>
<summary><b>❌ Connection Refused</b></summary>

**Cause:** API not running

**Solution:**
```
1. Start API: dotnet run --project src/ShikshaMitra.HttpApi.Host
2. Verify API is on https://localhost:44300
3. Check base_url in environment
```

</details>

### Environment Issues

<details>
<summary><b>Variables not working</b></summary>

**Solution:**
```
1. Verify environment is selected (top-right dropdown)
2. Check variable names match exactly: {{variable_name}}
3. Re-import environment file if corrupted
```

</details>

---

## 🎓 Best Practices

### ✅ Do

- **Use environment variables** for all IDs and URLs
- **Let test scripts auto-save** response IDs
- **Use correct token** (Host Admin vs Tenant User)
- **Follow workflow order** in examples
- **Check console logs** for auto-saved values

### ❌ Don't

- **Hardcode IDs** in request bodies
- **Mix Host Admin and Tenant contexts**
- **Skip login step**
- **Ignore environment selection**
- **Manually edit access_token**

---

## 📚 File Structure

```
postman/
├── 📄 Collections
│   ├── ShikshaMitra.postman_collection.json          ⭐ Main
│   ├── HR-Module.postman_collection.json             👥 HR APIs
│   ├── UserScope-API.postman_collection.json         🎯 UserScope
│   └── RoomsManagement.postman_collection.json       🏢 Rooms
│
├── 🌍 Environment
│   └── ShikshaMitra-Consolidated.postman_environment.json All modules
│
├── 📖 Documentation
│   ├── README.md                                     ← You are here
│   ├── HR-Module-README.md                           HR guide
│   ├── QUICKSTART.md                                 Quick start
│   ├── RoomsManagement-README.md                     Rooms guide
│   └── API-REFERENCE.md                              API reference
│
└── 📊 Database
    ├── DATABASE-SETUP.sql                            ID extraction
    └── UserScope-Database-Setup.sql                  UserScope setup
```

---

## 📝 Additional Resources

### Documentation Files

| File | Purpose | When to Use |
|------|---------|-------------|
| [QUICKSTART.md](./QUICKSTART.md) | 6-step quick start | First time setup |
| [HR-Module-README.md](./HR-Module-README.md) | HR APIs guide | Testing HR module |
| [RoomsManagement-README.md](./RoomsManagement-README.md) | Rooms APIs guide | Testing rooms |
| [API-REFERENCE.md](./API-REFERENCE.md) | Complete API docs | Reference lookup |
| [DATABASE-SETUP.sql](./DATABASE-SETUP.sql) | Get DB IDs | Environment setup |

### External Links

- [ABP Framework Docs](https://docs.abp.io/)
- [Postman Learning Center](https://learning.postman.com/)
- [ShikshaMitra GitHub](https://github.com/ShikshaMitra-Org)

---

## 🎯 Quick Reference

### Essential Commands

```bash
# Start API locally
dotnet run --project src/ShikshaMitra.HttpApi.Host

# Run database migrations
dotnet run --project src/ShikshaMitra.DbMigrator

# Build solution
dotnet build
```

### Essential Requests

| Action | Collection → Request |
|--------|---------------------|
| Login | Authentication → Tenant User Login |
| Create User | User Management → Create User |
| Assign Scope | UserScope → Assign Specific Division |
| Get Scopes | UserScope → Get User Scopes |
| Upload Document | Employee Management → Upload Documents |
| Create Leave Type | Leave Management → Create Leave Type |

---

## ✅ Collection Status

| Collection | Status | Endpoints | Last Updated |
|------------|--------|-----------|--------------|
| Main | ✅ Ready | 25+ | 2024-11 |
| HR Module | ✅ Ready | 40+ | 2025-01 |
| UserScope | ✅ Ready | 30+ | 2024-11 |
| Rooms | ✅ Ready | 18+ | 2024-11 |

---

## 🤝 Contributing

Found an issue or have suggestions?

1. Update the relevant collection
2. Test all requests
3. Update documentation
4. Submit changes

---

**Version:** 3.0  
**Last Updated:** January 2025  
**Maintained By:** ShikshaMitra Development Team

---

## 📬 Support

- 📧 Technical issues: See troubleshooting section above
- 📖 Documentation: Read collection-specific READMEs
- 💬 Questions: Check existing documentation first

---

**Happy Testing! 🚀**
