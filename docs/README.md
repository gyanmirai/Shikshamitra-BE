# ShikshaMitra Documentation

Multi-tenant school management system built with ABP Framework (.NET 10).

## 📁 Structure

```
docs/
├── userscope/               → UserScope access control
├── multi-tenancy/          → Multi-tenant architecture  
└── api/                    → API documentation
```

## 🚀 Quick Start

### API Testing
[postman/](../postman/) folder contains everything:
- `ShikshaMitra.postman_collection.json`
- `ShikshaMitra.postman_environment.json`
- [Setup guide](../postman/README.md)

### Development
```bash
dotnet restore && dotnet build
dotnet run --project src/ShikshaMitra.DbMigrator
dotnet run --project src/ShikshaMitra.HttpApi.Host
```

## 📚 Core Concepts

### UserScope
Hierarchical access: Board → Medium → Stage → Class → Division
- Principle: `NULL = ALL`
- [Architecture](./userscope/UserScope.md)

### Multi-Tenancy
Complete tenant isolation
- [Tenant setup](./multi-tenancy/tenants.md)
- [Role config](./multi-tenancy/roles.md)

### Authentication
- Host Admin: Tenant creation
- Tenant User: All operations
- [Details](../postman/ARCHITECTURE.md)

## 🎯 Documentation Map

| Topic | Document |
|-------|----------|
| UserScope | [userscope/UserScope.md](./userscope/UserScope.md) |
| Permissions | [userscope/permissions-setup.md](./userscope/permissions-setup.md) |
| Error handling | [userscope/exception-handling.md](./userscope/exception-handling.md) |
| Tenants | [multi-tenancy/tenants.md](./multi-tenancy/tenants.md) |
| Roles | [multi-tenancy/roles.md](./multi-tenancy/roles.md) |
| API | [api/endpoints.md](./api/endpoints.md) |
| Testing | [../postman/README.md](../postman/README.md) |

## 🔧 SSVM Tenant

```
ID: 3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e
User: admin
Pass: Temp@123
```

---

.NET 10 | ABP Framework | EF Core | PostgreSQL
