# Multi-Tenancy Documentation

Multi-tenant architecture for ShikshaMitra.

## Files

- **[tenants.md](./tenants.md)** - Tenant management and architecture
- **[roles.md](./roles.md)** - Role configuration and setup

## Quick Overview

### Concept
Each school/institution is a separate tenant with complete data isolation.

### Tenant Isolation
- Separate users per tenant
- Separate roles per tenant
- Separate data per tenant
- Separate access control per tenant

### Authentication Contexts

#### Host Admin
- Creates new tenants
- Manages tenant list
- Cross-tenant access
- Uses `admin_access_token`

#### Tenant User
- All operations within tenant
- User management
- Role management
- Data operations
- Uses `access_token` with `__tenant` header

## Start Here

1. **Understanding multi-tenancy?** → Read [tenants.md](./tenants.md)
2. **Setting up roles?** → See [roles.md](./roles.md)

---

[Back to main docs](../README.md)
