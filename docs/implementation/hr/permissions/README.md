# HR Module Permissions Implementation

Authorization and permission implementation for all HR module controllers.

## 📄 Documentation Files

### [Implementation.md](./Implementation.md)
**Comprehensive permission implementation guide**

Covers:
- Permission structure and hierarchy
- Controller-level authorization
- Method-level permissions (View, Add, Delete)
- Permission mapping for all HR endpoints
- Testing recommendations
- Frontend integration guide

**Best for:** Backend developers, security implementation

### [Quick-Reference.md](./Quick-Reference.md)
**Quick lookup guide**

Covers:
- Permission constants quick reference
- Endpoint-to-permission mapping table
- Common scenarios and examples
- Troubleshooting guide

**Best for:** Quick lookups, frontend developers checking permissions

## 🎯 Controllers Covered

All 6 HR controllers with comprehensive permissions:

1. **LeaveTypeController** - Leave type management
2. **LeavePolicyController** - Leave policy management
3. **EarningComponentController** - Payroll earning components
4. **DeductionComponentController** - Payroll deduction components
5. **EmployeeController** - Employee documents and payroll
6. **EmployeeAdditionalDetailsController** - Additional employee info

## 🔐 Permission Levels

- **View** - Read-only access (GET operations)
- **Add** - Create/Update access (POST/PUT operations)
- **Delete** - Delete access (DELETE operations)

## 🔗 Related

- **Permission Definitions**: `/src/ShikshaMitra.Domain.Shared/Constants/Permissions/`
- **HR Module**: `../`

---

**Status**: ✅ Implementation Complete  
**Last Updated**: January 2024
