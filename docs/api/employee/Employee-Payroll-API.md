# Employee Payroll API Reference

## Quick Overview

✅ **Status**: Implemented and ready to use  
📍 **Location**: `/api/employees/{employeeId}/payroll`

---

## API Endpoints

### 1. Get Payroll Details
```http
GET /api/employees/{employeeId}/payroll
```

**Response Example:**
```json
{
  "employeeId": "EMP028",
  "payrollDetails": {
    "bankName": "HDFC Bank",
    "accountNumber": "123456789012",
    "ifscCode": "HDFC0001234",
    "basicSalary": 35000,
    "tdsPercentage": 10
  },
  "earnings": [
    { "componentName": "Basic Salary", "type": 0, "amount": 35000 },
    { "componentName": "HRA", "type": 0, "amount": 10000 }
  ],
  "deductions": [
    { "componentName": "PF", "type": 0, "amount": 1800 },
    { "componentName": "TDS", "type": 2, "amount": 3500 }
  ]
}
```

### 2. Update Payroll Details
```http
PUT /api/employees/{employeeId}/payroll
Content-Type: application/json
```

**Request Body:**
```json
{
  "payrollDetails": {
    "bankName": "HDFC Bank",
    "accountNumber": "123456789012",
    "ifscCode": "HDFC0001234",
    "basicSalary": 50000
  },
  "earnings": [
    { "componentName": "Basic Salary", "type": 0, "amount": 50000 }
  ],
  "deductions": [
    { "componentName": "PF", "type": 0, "amount": 1800 }
  ]
}
```

---

## Data Types

### PayrollComponentType Enum
```csharp
0 = Fixed      // Regular monthly (Basic Salary, HRA)
1 = Variable   // One-time (Bonus, Incentive)
2 = Calculated // Auto-calculated (TDS, PF)
```

---

## Validation Rules

| Field | Rule |
|-------|------|
| accountNumber | Numeric only, max 50 chars |
| ifscCode | Format: XXXX0YYYYYY (11 chars) |
| basicSalary | Must be ≥ 0 |
| tdsPercentage | Range: 0-100 |
| componentName | Required, max 200 chars |
| amount | Must be ≥ 0 |

---

## Database Schema

### AppEmployeePayrollDetails (1:1 with Employee)
```sql
- Id (PK), EmployeeId (FK, Unique)
- BankName, AccountNumber, IfscCode
- BasicSalary, TdsPercentage, TdsAmount
```

### AppEmployeePayrollComponents (1:N with Employee)
```sql
- Id (PK), EmployeeId (FK)
- ComponentName, Type, Amount
- IsEarning (true=earning, false=deduction)
```

---

## Implementation Notes

1. **Update Strategy**: Replace all earnings/deductions (not merge)
2. **Cache Invalidation**: Employee cache auto-cleared on update
3. **Validation**: IFSC code format validated, account number must be numeric
4. **Transactions**: All updates in single transaction

---

## Files Structure

```
Domain.Shared/
  └── HR/Employees/Payroll/
      └── PayrollComponentType.cs         # Enum definition

Domain/
  └── HR/Employees/Payroll/
      ├── EmployeePayrollDetails.cs       # Bank & salary entity
      ├── EmployeePayrollComponent.cs     # Earning/deduction entity
      └── IEmployeePayrollRepository.cs   # Repository interfaces

Application.Contracts/
  └── HR/Employees/Payroll/
      ├── EmployeePayrollDetailsDto.cs    # Response DTOs
      └── UpdateEmployeePayrollDetailsDto.cs # Request DTOs

Application/
  └── HR/Employees/
      └── EmployeeAppService.cs           # API methods

EntityFrameworkCore/
  └── HR/Employees/Payroll/
      ├── EfCoreEmployeePayrollRepository.cs # Repository impl
      └── EmployeePayrollConfiguration.cs    # EF mappings

HttpApi/
  └── HR/Employees/
      └── EmployeePayrollController.cs    # REST endpoints
```

---

## Quick Test

```bash
# Get payroll
curl -X GET "https://localhost:44300/api/employees/{id}/payroll"

# Update payroll
curl -X PUT "https://localhost:44300/api/employees/{id}/payroll" \
  -H "Content-Type: application/json" \
  -d '{"payrollDetails":{"bankName":"Test","basicSalary":50000},"earnings":[],"deductions":[]}'
```

---

**Last Updated**: January 2024  
**Build Status**: ✅ Success
