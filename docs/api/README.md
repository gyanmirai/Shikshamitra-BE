# API Documentation

RESTful API documentation for ShikshaMitra.

## Files

- **[endpoints.md](./endpoints.md)** - Complete API endpoint reference

## Quick Overview

### Base URL
```
https://localhost:44300
```

### Authentication
All requests require JWT token in `Authorization` header:
```
Authorization: Bearer {token}
```

### Tenant Context
Tenant-specific requests require `__tenant` header:
```
__tenant: {tenant-id}
```

### Testing
See [../../postman/](../../postman/) for:
- Postman collection
- Environment setup
- Test automation

## API Categories

- **Authentication**: Login and token management
- **Tenants**: Tenant creation and management (Host Admin)
- **Users**: User CRUD operations (Tenant)
- **Roles**: Role management (Tenant)
- **UserScope**: Scope assignment and queries (Tenant)

## Start Here

1. **API Reference** → [endpoints.md](./endpoints.md)
2. **Testing APIs** → [../../postman/README.md](../../postman/README.md)

---

[Back to main docs](../README.md)
