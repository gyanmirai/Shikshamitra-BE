# Postman Collections - Quick Overview

A visual guide to all available Postman collections and when to use them.

---

## 🎯 Which Collection Should I Use?

```
Testing...                              Use Collection
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔐 Authentication & Basic APIs          → ShikshaMitra.postman_collection.json
👥 HR: Leave, Payroll, Employees       → HR-Module.postman_collection.json
🎯 UserScope & Permissions             → UserScope-API.postman_collection.json
🏢 Buildings & Rooms                    → RoomsManagement.postman_collection.json
```

---

## 📦 Collection Details

### 1️⃣ ShikshaMitra (Main Collection)
**Perfect for:** Basic API testing, authentication, user management

```
📁 ShikshaMitra.postman_collection.json (34 KB)
📁 ShikshaMitra-Consolidated.postman_environment.json

Endpoints: 25+
Features:
  ✅ Host & Tenant authentication
  ✅ User CRUD operations
  ✅ Basic UserScope assignment
  ✅ Auto token & ID management

Common Uses:
  - Login & get access token
  - Create/manage users
  - Assign basic user scopes
  - Test authentication flows
```

**Quick Start:**
```
1. Import: ShikshaMitra.postman_collection.json
2. Import: ShikshaMitra-Consolidated.postman_environment.json
3. Run: Authentication → Tenant User Login
4. Test: User Management → Create User
```

---

### 2️⃣ HR Module Collection
**Perfect for:** HR operations, employee management, payroll

```
📁 HR-Module.postman_collection.json (34 KB)
📁 ShikshaMitra-Consolidated.postman_environment.json
📖 HR-Module-README.md

Endpoints: 40+
Features:
  ✅ Leave management (Types & Policies)
  ✅ Payroll components (Earning & Deduction)
  ✅ Employee documents (multipart uploads)
  ✅ Employee payroll & additional details
  ✅ Component activation/deactivation

Common Uses:
  - Create leave types & policies
  - Configure payroll components
  - Upload employee documents
  - Manage employee payroll details
```

**Quick Start:**
```
1. Import: HR-Module.postman_collection.json
2. Import: ShikshaMitra-Consolidated.postman_environment.json
3. Set: employee_id in environment
4. Run: Authentication → Login
5. Test: Leave Management → Create Leave Type
```

**API Groups:**
```
📋 Leave Management
├── Leave Types (6 endpoints)
└── Leave Policies (6 endpoints)

💰 Payroll Management
├── Earning Components (8 endpoints)
└── Deduction Components (8 endpoints)

👤 Employee Management
├── Documents (4 endpoints)
├── Payroll (3 endpoints)
└── Additional Details (2 endpoints)
```

---

### 3️⃣ UserScope API Collection
**Perfect for:** Advanced permissions, scope testing, access control

```
📁 UserScope-API.postman_collection.json (106 KB)
📁 ShikshaMitra-Consolidated.postman_environment.json
📖 QUICKSTART.md

Endpoints: 30+
Features:
  ✅ 7 assignment patterns
  ✅ Wildcard & specific scopes
  ✅ Bulk assignment
  ✅ Class/stage level assignment
  ✅ Complete workflow examples
  ✅ Database setup scripts

Common Uses:
  - Assign teachers to divisions
  - Set up class permissions
  - Test scope inheritance
  - Verify access control
```

**Quick Start:**
```
1. Import: UserScope-API.postman_collection.json
2. Run SQL: UserScope-Database-Setup.sql
3. Update environment IDs
4. Follow: QUICKSTART.md (6 steps)
```

**Assignment Patterns:**
```
1️⃣ Specific Division      → Teacher for one division
2️⃣ All Divisions          → Subject teacher (wildcard)
3️⃣ Bulk Assignment        → Multiple divisions at once
4️⃣ Class Level            → All divisions in a class
5️⃣ Stage Level            → All classes in a stage
6️⃣ Admin Assignment       → Full access
7️⃣ Complex Scenarios      → Mixed assignments
```

---

### 4️⃣ Rooms Management Collection
**Perfect for:** Building & room setup, facility management

```
📁 RoomsManagement.postman_collection.json (8 KB)
📁 ShikshaMitra-Consolidated.postman_environment.json
📖 RoomsManagement-README.md

Endpoints: 18+
Features:
  ✅ Building CRUD
  ✅ Room type management
  ✅ Room CRUD with details
  ✅ Pagination support

Common Uses:
  - Create buildings
  - Define room types
  - Set up classrooms
  - Manage facilities
```

**Quick Start:**
```
1. Import: RoomsManagement.postman_collection.json
2. Run: Authentication → Login
3. Test: Buildings → Create Building
4. Test: Rooms → Create Room
```

**API Groups:**
```
🏢 Building Management (6 endpoints)
├── List, Get, Create, Update, Delete

🚪 Room Type Management (6 endpoints)
├── List, Get, Create, Update, Delete

📐 Room Management (6 endpoints)
├── List, Get, Create, Update, Delete
```

---

## 🔄 Workflow Examples

### New Employee Setup (HR Module)
```
1. Login
   → Authentication → Login
   
2. Create Leave Type
   → Leave Management → Create Leave Type
   → leave_type_id auto-saved
   
3. Create Leave Policy
   → Leave Policies → Create Leave Policy
   → Uses {{leave_type_id}}
   
4. Create Earning Components
   → Payroll → Create Earning Component
   → earning_component_id auto-saved
   
5. Upload Employee Documents
   → Employee Management → Upload Documents
   → Multipart form-data
   
6. Set Payroll Details
   → Employee Payroll → Update Payroll Details
   → Uses earning/deduction components
```

### Teacher Permission Setup (UserScope)
```
1. Login & Create User
   → User Management → Create User
   → user_id auto-saved
   
2. Assign Division Scope
   → UserScope → Assign Specific Division
   → Uses {{user_id}}
   → scope_assignment_id auto-saved
   
3. Verify Assignment
   → UserScope Query → Get User Scopes
   → Check effectiveScope.divisionIds
```

### Facility Setup (Rooms)
```
1. Create Building
   → Buildings → Create Building
   → building_id auto-saved
   
2. Create Room Type
   → Room Types → Create Room Type
   → room_type_id auto-saved
   
3. Create Rooms
   → Rooms → Create Room
   → Uses {{building_id}} and {{room_type_id}}
```

---

## 🎓 Learning Path

### Beginner → Start Here
```
1. ShikshaMitra Collection
   - Learn authentication
   - Practice basic CRUD
   - Understand token management
```

### Intermediate
```
2. HR Module Collection
   - Test leave management
   - Configure payroll
   - Upload documents

3. Rooms Collection
   - Set up facilities
   - Practice pagination
```

### Advanced
```
4. UserScope API Collection
   - Complex permission scenarios
   - Bulk operations
   - Access control testing
```

---

## 📊 Feature Comparison

| Feature | Main | HR Module | UserScope | Rooms |
|---------|------|-----------|-----------|-------|
| **Authentication** | ✅ Full | ✅ Bearer | ✅ Bearer | ✅ Bearer |
| **Auto Token Save** | ✅ | ✅ | ✅ | ✅ |
| **Auto ID Save** | ✅ | ✅ | ✅ | ✅ |
| **File Uploads** | ❌ | ✅ Multipart | ❌ | ❌ |
| **Pagination** | ✅ | ✅ | ✅ | ✅ |
| **Bulk Operations** | ❌ | ❌ | ✅ | ❌ |
| **Examples** | Basic | Advanced | Complete | Basic |
| **Database Scripts** | ✅ | ❌ | ✅ | ❌ |

---

## 🗂️ File Organization

```
postman/
│
├── 📄 Collections (Choose one based on your needs)
│   ├── ShikshaMitra.postman_collection.json          ← Basic APIs
│   ├── HR-Module.postman_collection.json             ← HR operations
│   ├── UserScope-API.postman_collection.json         ← Permissions
│   └── RoomsManagement.postman_collection.json       ← Facilities
│
├── 🌍 Environment
│   └── ShikshaMitra-Consolidated.postman_environment.json ← Single for all collections
│
├── 📖 Documentation (Read before testing)
│   ├── README.md                                     ← Start here
│   ├── OVERVIEW.md                                   ← This file
│   ├── HR-Module-README.md                           ← HR guide
│   ├── QUICKSTART.md                                 ← UserScope quick start
│   ├── RoomsManagement-README.md                     ← Rooms guide
│   └── API-REFERENCE.md                              ← Complete API docs
│
└── 📊 Database Scripts
    ├── DATABASE-SETUP.sql                            ← Get IDs for environment
    └── UserScope-Database-Setup.sql                  ← UserScope specific
```

---

## 💡 Pro Tips

### 1. Environment Selection
```
All collections use the same environment:
  "ShikshaMitra - All Modules"
```

### 2. Token Management
```
Token expires? Just re-run login:
  1. Authentication → Login
  2. Token auto-refreshes in environment
  3. Continue testing
```

### 3. Using Variables
```
Always use variables, never hardcode:
  ✅ {{employee_id}}
  ✅ {{leave_type_id}}
  ❌ "3a1e19bb-4fe8-be24-a3d5-6c64963783b2"
```

### 4. Database IDs
```
Get fresh IDs from database:
  1. Open: DATABASE-SETUP.sql
  2. Run queries in SQL client
  3. Copy results to environment
  4. Save environment
```

### 5. Request Order
```
Follow logical order:
  1. Login first
  2. Create parent entities (e.g., Leave Type)
  3. Create child entities (e.g., Leave Policy)
  4. Test operations
  5. Clean up (delete)
```

---

## 🎯 Quick Decision Guide

**I want to...**

| Task | Collection | Starting Point |
|------|-----------|----------------|
| Test basic login | Main | Auth → Tenant User Login |
| Create a user | Main | User Management → Create User |
| Set up employee leave | HR Module | Leave Management → Create Leave Type |
| Configure payroll | HR Module | Payroll → Create Earning Component |
| Upload documents | HR Module | Employee → Upload Documents |
| Assign teacher scope | UserScope | Assignment → Assign Division |
| Test permissions | UserScope | Query → Get User Scopes |
| Create a building | Rooms | Buildings → Create Building |
| Set up classroom | Rooms | Rooms → Create Room |

---

## 📞 Need Help?

### Quick Links
- 📖 [Main README](./README.md) - Complete guide
- 🚀 [Quick Start](./QUICKSTART.md) - 6-step setup
- 👥 [HR Module Guide](./HR-Module-README.md) - HR operations
- 🏢 [Rooms Guide](./RoomsManagement-README.md) - Facilities
- 📚 [API Reference](./API-REFERENCE.md) - All endpoints

### Troubleshooting
See [README.md - Troubleshooting Section](./README.md#-troubleshooting)

---

**Last Updated:** January 2025  
**Version:** 1.0
