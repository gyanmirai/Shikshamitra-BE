# Authentication & Workflow Architecture

Visual guide for ShikshaMitra API authentication patterns and workflows.

## 🔐 Authentication Architecture

```
┌─────────────────────────────────────────────────────────────┐
│              ShikshaMitra Multi-Tenant API                   │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
                  ┌──────────────────┐
                  │ POST /connect/   │
                  │ token            │
                  └────┬─────────┬───┘
                       │         │
          Has __tenant │         │ No __tenant
          header?      │         │ header
                       │         │
        ┌──────────────▼─┐   ┌──▼──────────────┐
        │  TENANT USER   │   │  HOST ADMIN     │
        │  Context       │   │  Context        │
        └──────┬─────────┘   └─┬───────────────┘
               │                 │
               │                 │
        ┌──────▼─────────────────▼──────┐
        │     JWT Token Generated        │
        └──────┬─────────────────┬───────┘
               │                 │
               ▼                 ▼
    ┌──────────────────┐  ┌─────────────────┐
    │ access_token     │  │ admin_access_   │
    │                  │  │ token           │
    │ • Has tenant_id  │  │ • No tenant_id  │
    │ • User roles     │  │ • Global admin  │
    │ • User scopes    │  │ • Cross-tenant  │
    └──────────────────┘  └─────────────────┘
```

## 🎯 Decision Tree

```
Need to call an API?
│
├─ Creating a NEW tenant?
│  │
│  YES ──→ Use Host Admin Login
│          └─→ admin_access_token
│              └─→ NO __tenant header
│
└─ Any other operation?
   │
   YES ──→ Use Tenant User Login
           └─→ access_token
               └─→ WITH __tenant header
                   └─→ Examples:
                       • Create users
                       • Assign roles
                       • Manage scopes
                       • Query data
```

## 📋 Workflow Diagrams

### Standard Workflow: Daily Operations

```
┌─────────────┐
│   START     │
└──────┬──────┘
       │
       ▼
┌──────────────────────────────┐
│ Tenant User Login (SSVM)     │
│ __tenant: 3a1dc7ba-...       │
│ → access_token (auto-saved) │
└──────┬───────────────────────┘
       │
       ├─→ Create User ──→ user_id saved
       │
       ├─→ Assign Role
       │
       ├─→ Assign Scope ──→ scope_assignment_id saved
       │
       └─→ Query/Update/Delete
           └─→ Use saved IDs via {{variable}}
```

### Complete User Setup Flow

```
Step 1: Login
┌─────────────────────────────┐
│ Tenant User Login           │
│ Result: access_token saved  │
└─────────────┬───────────────┘
              │
Step 2: Create User
              ▼
┌─────────────────────────────┐
│ POST /api/identity/users    │
│ Headers:                    │
│   Authorization: {{token}}  │
│   __tenant: {{tenant_id}}   │
│ Result: user_id saved       │
└─────────────┬───────────────┘
              │
Step 3: Assign Role (Optional)
              ▼
┌─────────────────────────────┐
│ PUT /api/identity/users/    │
│ {user_id}/roles             │
│ Body: {"roleNames":[...]}   │
└─────────────┬───────────────┘
              │
Step 4: Assign Scope
              ▼
┌─────────────────────────────┐
│ POST /api/app/user-scope/   │
│ assign                      │
│ Body: {                     │
│   userId: {{user_id}},      │
│   standardId: {{standard}}  │
│ }                           │
│ Result: scope_assignment_id │
└─────────────┬───────────────┘
              │
              ▼
┌─────────────────────────────┐
│ User Ready!                 │
│ Can login & access data     │
└─────────────────────────────┘
```

### Tenant Creation Flow (One-time)

```
┌─────────────────────────────┐
│ Host Admin Login            │
│ NO __tenant header          │
│ Result: admin_access_token  │
└─────────────┬───────────────┘
              │
              ▼
┌─────────────────────────────┐
│ POST /api/multi-tenancy/    │
│ tenants                     │
│ Body: {                     │
│   name: "NewSchool",        │
│   adminEmail: "...",        │
│   adminPassword: "..."      │
│ }                           │
│ Result: New tenant created  │
│         Save tenant_id      │
└─────────────┬───────────────┘
              │
              ▼
┌─────────────────────────────┐
│ Update Environment:         │
│ tenant_id = new ID          │
└─────────────┬───────────────┘
              │
              ▼
┌─────────────────────────────┐
│ Tenant User Login           │
│ WITH new tenant_id          │
│ Result: access_token        │
└─────────────┬───────────────┘
              │
              ▼
┌─────────────────────────────┐
│ Continue with standard      │
│ workflow                    │
└─────────────────────────────┘
```

## 🎨 Collection Architecture

```
Collection Root
│
├── 🔐 Authentication Layer
│   ├── Host Admin ────────────┐
│   │   (Tenant creation only) │
│   │                          │
│   └── Tenant User ───────────┤
│       (All operations) ✅    │
│                              │
├── 🏢 Tenant Management ◄─────┘
│   │   (Uses: admin_access_token)
│   ├── Create Tenant
│   └── List Tenants
│
├── 👤 User Management ◄───────┐
│   │   (Uses: access_token)   │
│   ├── CRUD Operations        │
│   └── Auto-saves: user_id    │
│                              │
├── 🎭 Role Management ◄───────┤
│   │   (Uses: access_token)   │
│   ├── Create/Assign Roles    │
│   └── Permission Management  │
│                              │
└── 🎯 UserScope ◄─────────────┘
    │   (Uses: access_token)
    ├── Assignment
    │   └── Saves: scope_assignment_id
    ├── Queries
    └── Management
```

## 📊 Request Header Patterns

### Tenant Operation (Standard)
```http
POST /api/identity/users
Authorization: Bearer {{access_token}}
__tenant: {{tenant_id}}
Content-Type: application/json

{...request body...}
```

### Host Admin Operation (Rare)
```http
POST /api/multi-tenancy/tenants
Authorization: Bearer {{admin_access_token}}
Content-Type: application/json

{...request body...}
```

## 🔄 Variable Flow

```
Manual Setup (One-time)
├── base_url
├── tenant_id (from DB or seeder)
├── tenant_username
├── tenant_password
└── Entity IDs (from DATABASE-SETUP.sql)
    ├── academic_year_id
    ├── board_id
    ├── medium_id
    ├── stage_id
    ├── standard_id
    └── division_id

↓ Login

Automatic (Test Scripts)
├── access_token ◄──── From login response
├── admin_access_token ◄──── From host admin login
├── user_id ◄──── From create user
├── scope_assignment_id ◄──── From assign scope
└── other IDs ◄──── From respective create operations

↓ Use in subsequent requests

Variables Used
└── {{variable_name}} in:
    ├── URLs
    ├── Headers
    └── Request bodies
```

## ⚡ Automation Architecture

```
Request Execution
      │
      ▼
┌─────────────────┐
│ Pre-request     │
│ Script          │
│ (if any)        │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ API Call        │
│ Executed        │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Test Scripts    │◄─── Extract data from response
│ Execute         │     Save to environment
└────────┬────────┘     Example:
         │              var data = pm.response.json();
         │              pm.environment.set("user_id", data.id);
         ▼
┌─────────────────┐
│ Variables       │
│ Auto-saved      │◄─── Used in next request
└─────────────────┘     via {{variable_name}}
```

## 🎭 Multi-Tenancy Model

```
┌─────────────────────────────────────────┐
│           Database Layer                 │
│                                         │
│  ┌──────────┐  ┌──────────┐           │
│  │ Tenant A │  │ Tenant B │  ...      │
│  │ (SSVM)   │  │ (DDVS)   │           │
│  └────┬─────┘  └────┬─────┘           │
│       │             │                  │
│       │             │                  │
│  ┌────▼─────┐  ┌───▼──────┐           │
│  │ Users    │  │ Users    │           │
│  │ Roles    │  │ Roles    │           │
│  │ Scopes   │  │ Scopes   │           │
│  │ Data     │  │ Data     │           │
│  └──────────┘  └──────────┘           │
└─────────────────────────────────────────┘
         │                │
         │                │
    __tenant header determines context
         │                │
         ▼                ▼
    Tenant A          Tenant B
    Operations        Operations
```

## 🏗️ Clean Architecture Alignment

```
Presentation (Postman)
├── Collections (Interface)
├── Environments (Configuration)
└── Scripts (Automation)
    ↓
Application Layer (API)
├── Authentication
├── Authorization
└── Business Logic
    ↓
Domain Layer
├── Entities
├── Repositories
└── Services
    ↓
Infrastructure
└── Database (Multi-tenant)
```

## 📐 SOLID Principles Applied

### Single Responsibility
- Each request does ONE thing
- Host admin: ONLY tenant creation
- Tenant user: ALL tenant operations

### Open/Closed
- Collection extensible (add new requests)
- Core structure closed to modification

### Liskov Substitution
- All tenant operations use same pattern
- Interchangeable within tenant context

### Interface Segregation
- Separate authentication interfaces
- Host admin vs tenant user contexts

### Dependency Inversion
- Depends on environment variables (abstraction)
- Not hardcoded values (concrete implementation)

---

**Architecture Version**: 2.0  
**Last Updated**: November 28, 2025
