# API Documentation

RESTful API documentation for ShikshaMitra.

## 📁 Documentation Organization

### Module Documentation
- **[Employee Module](./employee/)** ⭐ **Featured** - Comprehensive documentation with enterprise optimizations
  - Complete API reference
  - Performance optimization guides (95% faster)
  - Distributed caching (90% DB load reduction)
  - Eager loading strategies (99.7% query reduction)
  - Production deployment checklists
  - **NEW**: [Payroll API](./employee/Employee-Payroll-API.md) - Complete payroll management

### Implementation Guides
- **[Employee Implementations](../implementation/employee/)** - Step-by-step feature implementation guides
  - Payroll system setup
  - Database migrations
  - Code examples
  - Testing procedures

### General API Reference
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

### Core APIs
- **Authentication**: Login and token management
- **Tenants**: Tenant creation and management (Host Admin)
- **Users**: User CRUD operations (Tenant)
- **Roles**: Role management (Tenant)
- **UserScope**: Scope assignment and queries (Tenant)

### HR Module
- **[Employee APIs](./employee/)** - Complete employee management with advanced optimizations
  - CRUD operations
  - Profile management
  - Document handling
  - **Payroll management** (NEW)
  - Performance: 1-2ms (cached), 99.7% fewer queries
  - [Implementation Guides](../implementation/employee/) - Step-by-step setup instructions

### Future Modules (Planned)
- **Student Management** - Student CRUD and enrollment
- **Academic Setup** - Boards, mediums, standards, divisions
- **Attendance** - Student and employee attendance tracking
- **Payroll** - Salary processing and pay slip generation

## Start Here

### For General API Usage
1. **API Reference** → [endpoints.md](./endpoints.md)
2. **Testing APIs** → [../../postman/README.md](../../postman/README.md)

### For Employee Module
1. **Overview** → [Employee Module README](./employee/README.md)
2. **Quick Reference** → [Employee Quick Guide](./employee/Employee-QuickRef.md)
3. **Payroll API** → [Payroll API Documentation](./employee/Employee-Payroll-API.md)
4. **Implementation** → [Payroll Implementation Guide](../implementation/employee/README.md)
5. **Performance Optimization** → [Complete Summary](./employee/Employee-Complete-Optimization-Summary.md)

### For Implementing Features
1. **Choose Feature** → Browse [implementation/employee/](../implementation/employee/)
2. **Follow QuickStart** → Get up and running in 15-30 minutes
3. **Reference API Docs** → Use API documentation for endpoint details
4. **Test & Deploy** → Follow testing and deployment guides

### By Role
- **Backend Developer** → Employee module implementation guides
- **Frontend Developer** → endpoints.md for API contracts
- **DevOps Engineer** → Employee deployment checklists
- **Architect** → Employee optimization patterns for other modules

---

## 🎯 Featured: Employee Module Optimizations

The Employee module showcases enterprise-level patterns applicable across all modules:

### Performance Achievements
```
Before → After (Improvement)
├─ Queries: 301+ → 1 (99.7% reduction)
├─ Memory: 500MB → 1MB (99% reduction)
├─ Response: 3000ms → 150ms (95% faster)
└─ DB Load: 1500/min → 150/min (90% reduction)
```

### Applicable Patterns
- **Blob Storage** - Binary data optimization
- **Eager Loading** - N+1 query elimination
- **Distributed Caching** - Read performance boost
- **Database Indexes** - Query optimization

**Use as template for:** Student, Course, Department, and other modules

---

[Back to main docs](../README.md)
