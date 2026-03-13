# ShikshaMitra Documentation

Multi-tenant school management system built with ABP Framework (.NET 9).

## 📁 Structure

```
docs/
├── api/                    → API documentation & references
├── implementation/         → Step-by-step implementation guides
├── userscope/             → UserScope access control
└── multi-tenancy/         → Multi-tenant architecture  
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

### Architecture & Best Practices
- [Project Best Practices](./best-practices.md) — Architecture rules, coding conventions, patterns enforced across all modules

### Module Documentation
- [HR Module](./modules/hr.md) — Employees, Attendance & Leave, Payroll, Calendar

### UserScope
Hierarchical access: Board → Medium → Stage → Class → Division
- Principle: `NULL = ALL`
- [Architecture](./UserScope-Usage.md)

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
| **Architecture** | |
| Best Practices | [best-practices.md](./best-practices.md) |
| **Module Documentation** | |
| HR Module | [modules/hr.md](./modules/hr.md) |
| **API Reference** | |
| Employee API | [api/employee/README.md](./api/employee/README.md) |
| Employee Payroll API | [api/employee/Employee-Payroll-API.md](./api/employee/Employee-Payroll-API.md) |
| General Endpoints | [api/endpoints.md](./api/endpoints.md) |
| **Implementation Guides** | |
| Employee Payroll | [implementation/employee/README.md](./implementation/employee/README.md) |
| All Implementations | [implementation/README.md](./implementation/README.md) |
| **Access Control** | |
| UserScope | [UserScope-Usage.md](./UserScope-Usage.md) |
| Bulk Assignment | [BulkScopeAssignment-API-Guide.md](./BulkScopeAssignment-API-Guide.md) |
| **Multi-Tenancy** | |
| Tenants | [multi-tenancy/tenants.md](./multi-tenancy/tenants.md) |
| Roles | [multi-tenancy/roles.md](./multi-tenancy/roles.md) |
| **Testing** | |
| Postman Setup | [../postman/README.md](../postman/README.md) |

## 📖 Documentation Types

### API Documentation (`docs/api/`)
Complete API reference with endpoints, request/response examples, and usage guides.
- **Purpose**: API consumers, frontend developers
- **Content**: Endpoints, schemas, examples, validation rules

### Implementation Guides (`docs/implementation/`)
Step-by-step instructions for implementing features from scratch.
- **Purpose**: Backend developers adding new features
- **Content**: Code examples, database migrations, testing procedures

### Architecture (`docs/multi-tenancy/`, `docs/UserScope-Usage.md`)
System design and architectural concepts.
- **Purpose**: Architects, senior developers, new team members
- **Content**: Concepts, patterns, best practices

## 🔧 SSVM Tenant

```
ID: 3a1dc7ba-395f-4af3-7ac7-ad923cc1f20e
User: admin
Pass: Temp@123
```

## 📝 Quick Links

### For Developers
- [Employee API Quick Reference](./api/employee/Employee-QuickRef.md)
- [Employee Payroll Implementation](./implementation/employee/Employee-Payroll-QuickStart.md)
- [API Endpoints](./api/endpoints.md)

### For Architects
- [Employee Module Complete Summary](./api/employee/Employee-Complete-Optimization-Summary.md)
- [Multi-Tenancy Architecture](./multi-tenancy/README.md)
- [UserScope System](./UserScope-Usage.md)

### For Project Managers
- [Implementation Progress](./implementation/employee/Employee-Payroll-Implementation-Complete.md)
- [Feature Documentation](./api/employee/README.md)

---

**.NET 9** | **ABP Framework** | **EF Core** | **PostgreSQL**
