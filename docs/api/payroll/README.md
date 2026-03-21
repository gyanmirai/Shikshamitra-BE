# Payroll APIs

This directory contains documentation for all Payroll Management APIs in the ShikshaMitra system.

---

## Available APIs

### 1. Earning Components API ✨ NEW

**Path**: [Earning-Components-API.md](./Earning-Components-API.md)

**Purpose**: Manage master data for payroll earning components (Basic Salary, HRA, Transport Allowance, etc.)

**Key Features**:
- Full CRUD operations
- Support for Fixed and Variable components
- Percentage-based calculations (e.g., HRA = 40% of Basic)
- Dependency management (None/Basic/Gross)
- Distributed caching for performance
- Soft delete (Activate/Deactivate)

**Base URL**: `/api/payroll/earning-components`

**Endpoints**:
- `GET /api/payroll/earning-components` - List all (paginated)
- `GET /api/payroll/earning-components/active` - Get all active (cached)
- `GET /api/payroll/earning-components/{id}` - Get by ID
- `POST /api/payroll/earning-components` - Create new
- `PUT /api/payroll/earning-components/{id}` - Update
- `DELETE /api/payroll/earning-components/{id}` - Delete
- `POST /api/payroll/earning-components/{id}/activate` - Activate
- `POST /api/payroll/earning-components/{id}/deactivate` - Deactivate

**Quick Example**:
```bash
# Create a basic salary component
curl -X POST "https://localhost:44300/api/payroll/earning-components" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Basic Salary",
    "componentType": 0,
    "dependOn": 0,
    "amount": "30000",
    "visibleInPayslip": true
  }'

# Get all active components (cached)
curl -X GET "https://localhost:44300/api/payroll/earning-components/active"
```

---

### 2. Deduction Components API

**Path**: [Deduction-Components-API.md](./Deduction-Components-API.md)

**Purpose**: Master data for payroll deductions (PF, professional tax, TDS, etc.).

**Base URL**: `/api/payroll/deduction-components` (see the API doc for full routes).

---

## Coming Soon

### Payroll Structures API
Define complete payroll structures combining earning and deduction components

### Payslip Generation API
Generate employee payslips based on assigned payroll structures

---

## API Architecture

All payroll APIs follow a consistent architecture:

```
┌─────────────────────────────────────┐
│      HTTP API Controller            │
│  (RESTful endpoints, validation)    │
└─────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────┐
│     Application Service             │
│  (Business orchestration, caching)  │
└─────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────┐
│      Domain Service                 │
│  (Business rules, validation)       │
└─────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────┐
│      Repository                     │
│  (Data access, queries)             │
└─────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────┐
│      Database                       │
│  (PostgreSQL)                       │
└─────────────────────────────────────┘
```

### Design Principles

1. **Domain-Driven Design (DDD)**: Rich domain models with behavior
2. **Clean Architecture**: Clear separation of concerns
3. **SOLID Principles**: Maintainable, extensible code
4. **Performance First**: Caching, indexing, async operations
5. **Security**: Multi-tenancy, audit trails, validation

---

## Common Patterns

### Pagination
All list endpoints support pagination:
```
GET /api/payroll/earning-components?SkipCount=0&MaxResultCount=10
```

### Sorting
```
GET /api/payroll/earning-components?Sorting=Name DESC
```

### Filtering Active Items
Use dedicated endpoints for better performance:
```
GET /api/payroll/earning-components/active  // Cached, fast
```

### Error Handling
All APIs return consistent error responses:
```json
{
  "error": {
    "code": "ShikshaMitra:ErrorCode",
    "message": "Human-readable message",
    "data": { /* Additional context */ }
  }
}
```

### Validation
- **DTO Level**: Data annotations (`[Required]`, `[StringLength]`)
- **Domain Level**: Business rules (uniqueness, format, ranges)
- **Database Level**: Constraints, indexes

---

## Quick Links

- [Earning Components API](./Earning-Components-API.md)
- [Deduction Components API](./Deduction-Components-API.md)
- [HR earning components — implementation](../../../implementation/hr/earning-components/README.md)
- [HR deduction components — implementation](../../../implementation/hr/deduction-components/README.md)

---

## Support

For questions or issues:
- Check the API documentation for detailed examples
- Review the implementation guide for technical details
- Consult the quick start guide for common scenarios

---

**Last Updated**: January 2024  
**Version**: 1.0.0
