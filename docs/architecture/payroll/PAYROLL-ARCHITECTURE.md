# 📊 Payroll Architecture Documentation

## Overview

The ShikshaMitra ERP uses a **two-tier payroll architecture** that separates master configuration from employee-specific data. This design provides flexibility, scalability, and maintains data integrity.

---

## 🏗️ Two-Tier Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                    TIER 1: Master Configuration                   │
│                      (HR.Payroll Module)                          │
├──────────────────────────────────────────────────────────────────┤
│                                                                    │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────┐│
│  │ PayrollStructure│  │EarningComponent │  │DeductionComponent││
│  │                 │  │                 │  │                  ││
│  │ - Templates     │  │ - Basic Salary  │  │ - PF             ││
│  │ - Rules         │  │ - HRA           │  │ - ESI            ││
│  │ - Defaults      │  │ - DA            │  │ - TDS            ││
│  └─────────────────┘  └─────────────────┘  └──────────────────┘│
│                                                                    │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │           PayrollStructureManager                            ││
│  │  - Assigns structures to employees                          ││
│  │  - Auto-initializes employee payroll                        ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                    │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                RunPayroll Service                            ││
│  │  - Monthly payroll processing                               ││
│  │  - Generates salary slips                                   ││
│  └─────────────────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
┌──────────────────────────────────────────────────────────────────┐
│                  TIER 2: Employee-Specific Data                   │
│                 (HR.Employees.Payroll Module)                     │
├──────────────────────────────────────────────────────────────────┤
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐│
│  │            EmployeePayrollDetails                            ││
│  │  - Bank account information                                  ││
│  │  - EPF/UAN numbers                                          ││
│  │  - Basic salary (actual amount)                             ││
│  │  - TDS configuration                                        ││
│  └──────────────────────────────────────────────────────────────┘│
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐│
│  │          EmployeePayrollComponent                            ││
│  │  - Individual earning components with amounts               ││
│  │  - Individual deduction components with amounts             ││
│  │  - Can override template amounts                            ││
│  └──────────────────────────────────────────────────────────────┘│
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐│
│  │      EmployeePayrollInitializationService                    ││
│  │  - Initializes employee data from structure                 ││
│  │  - Syncs changes from master templates                      ││
│  │  - Manages employee payroll lifecycle                       ││
│  └──────────────────────────────────────────────────────────────┘│
└──────────────────────────────────────────────────────────────────┘
```

---

## 📋 Component Breakdown

### **TIER 1: HR.Payroll (Master Configuration)**

#### **Purpose**
Organization-wide payroll settings, templates, and processing engine.

#### **Key Entities**

| Entity | Description | Example |
|--------|-------------|---------|
| **PayrollStructure** | Master salary structure templates | "Teacher Grade A Structure" |
| **EarningComponent** | Reusable earning definitions | "Basic Salary", "HRA", "Special Allowance" |
| **DeductionComponent** | Reusable deduction definitions | "Provident Fund", "Professional Tax" |
| **PayElement** | Base class for components | Either earning or deduction |

#### **Key Services**

| Service | Responsibility |
|---------|---------------|
| **PayrollStructureManager** | Assigns structures to employees, auto-initializes data |
| **RunPayroll Service** | Processes monthly payroll for all employees |
| **EarningComponentManager** | Manages earning component definitions |
| **DeductionComponentManager** | Manages deduction component definitions |

#### **Example Usage**
```csharp
// Define a master earning component
var basicSalaryComponent = new EarningComponent(
    id: Guid.NewGuid(),
    name: "Basic Salary",
    shortName: "BASIC",
    percentage: 40, // 40% of CTC
    calculationType: PayCalculationType.PercentageOfCTC
);

// Create a payroll structure template
var teacherStructure = new PayrollStructure(
    id: Guid.NewGuid(),
    payElementId: basicSalaryComponent.Id,
    payElementType: PayElementType.Earning,
    amount: 30000 // Default amount
);

// Assign structure to employee (auto-initializes employee data)
await payrollStructureManager.SetEmployeePayrollStructureAsync(
    employeeId: johnDoeId,
    payrollStructureId: teacherStructure.Id,
    autoInitialize: true // ✅ Automatically creates employee payroll data
);
```

---

### **TIER 2: HR.Employees.Payroll (Employee-Specific)**

#### **Purpose**
Individual employee's actual payroll configuration, bank details, and calculated amounts.

#### **Key Entities**

| Entity | Description | Example |
|--------|-------------|---------|
| **EmployeePayrollDetails** | Employee's bank & salary info | Bank: ICICI, Acc: 123456, Basic: ₹35,000 |
| **EmployeePayrollComponent** | Individual component amounts | Basic Salary: ₹35,000, HRA: ₹14,000 |
| **PayrollContract (DTOs)** | API contracts | Used in REST APIs |

#### **Key Services**

| Service | Responsibility |
|---------|---------------|
| **EmployeePayrollInitializationService** | Initializes employee data from templates |
| **EmployeeAppService** | Provides API endpoints for payroll |

#### **Example Usage**
```csharp
// View employee's actual payroll data
var employee = await employeeRepository.GetAsync(johnDoeId);
var payrollDetails = await payrollDetailsRepository.FindByEmployeeIdAsync(johnDoeId);
var payrollComponents = await payrollComponentRepository.GetByEmployeeIdAsync(johnDoeId);

// Employee's bank details
Console.WriteLine($"Bank: {payrollDetails.BankName}");
Console.WriteLine($"Account: {payrollDetails.AccountNumber}");
Console.WriteLine($"Basic Salary: {payrollDetails.BasicSalary}");

// Employee's actual component amounts
foreach (var component in payrollComponents)
{
    Console.WriteLine($"{component.PayElement.Name}: ₹{component.Amount}");
}
```

---

## 🔄 Data Flow: Template → Employee Data

### **Step 1: Create Master Templates (One-time setup)**
```csharp
// HR Admin creates earning components
var basicSalary = new EarningComponent("Basic Salary", percentage: 40);
var hra = new EarningComponent("House Rent Allowance", percentage: 20);
var da = new EarningComponent("Dearness Allowance", percentage: 10);

// HR Admin creates payroll structure
var teacherStructure = new PayrollStructure(
    payElementId: basicSalary.Id,
    amount: 30000
);
```

### **Step 2: Assign Structure to Employee**
```csharp
// HR Admin assigns structure to John Doe
await payrollStructureManager.SetEmployeePayrollStructureAsync(
    employeeId: johnDoeId,
    payrollStructureId: teacherStructure.Id,
    autoInitialize: true // ✅ AUTO-MAGIC HAPPENS HERE!
);
```

### **Step 3: Auto-Initialization (Behind the scenes)**
```csharp
// EmployeePayrollInitializationService automatically:
// 1. Creates EmployeePayrollDetails with defaults
var details = new EmployeePayrollDetails(
    employeeId: johnDoeId,
    basicSalary: 30000 // From template
);

// 2. Clones components from structure
var components = new List<EmployeePayrollComponent>
{
    new(payElementId: basicSalary.Id, amount: 30000),
    new(payElementId: hra.Id, amount: 12000),
    new(payElementId: da.Id, amount: 3000)
};
```

### **Step 4: HR/Employee Customizes (Optional)**
```csharp
// HR can customize John's specific amounts
payrollDetails.UpdateDetails(
    basicSalary: 35000, // ✅ Override template amount
    bankName: "ICICI Bank",
    accountNumber: "123456789",
    epfNumber: "MH/MUM/1234567"
);
```

### **Step 5: Monthly Payroll Processing**
```csharp
// Run monthly payroll for all employees
await runPayrollService.GeneratePayslipsAsync(
    month: 12,
    year: 2024
);

// Uses EmployeePayrollDetails and EmployeePayrollComponent (NOT templates!)
```

---

## 🎯 Key Workflows

### **Workflow 1: Onboard New Employee**

```csharp
// 1. Create employee
var employee = await employeeManager.CreateEmployeeAsync(...);

// 2. Assign payroll structure (auto-initializes payroll data)
await payrollStructureManager.SetEmployeePayrollStructureAsync(
    employeeId: employee.Id,
    payrollStructureId: teacherStructureId,
    autoInitialize: true
);

// 3. Update bank details
await employeeAppService.UpdatePayrollContractAsync(employee.Id, new PayrollContract
{
    BankDetails = new BankDetailsContract
    {
        BankName = "ICICI Bank",
        AccountNumber = "123456789",
        IfscCode = "ICIC0001234"
    }
});

// ✅ Employee is ready for payroll!
```

### **Workflow 2: Update Master Structure (Affects All Employees)**

```csharp
// HR Admin updates master earning component
var basicSalary = await earningComponentRepository.GetAsync(basicSalaryId);
basicSalary.UpdatePercentage(45); // Increase from 40% to 45%

// Sync all affected employees
var employees = await employeeRepository.GetListAsync(
    e => e.PayrollStructureId == teacherStructureId
);

foreach (var employee in employees)
{
    // Re-sync employee components from updated structure
    await initializationService.SyncComponentsFromStructureAsync(employee.Id);
}
```

### **Workflow 3: Process Monthly Payroll**

```csharp
// Run payroll for December 2024
await runPayrollService.GeneratePayslipsAsync(
    month: 12,
    year: 2024
);

// Behind the scenes:
// 1. Loads all active employees
// 2. For each employee:
//    - Fetches EmployeePayrollDetails (bank, EPF, TDS)
//    - Fetches EmployeePayrollComponents (earnings + deductions)
//    - Calculates gross salary, deductions, net salary
//    - Generates salary slip
//    - Stores in database
```

---

## 🔐 Security & Validation

### **Access Control**
```csharp
// Master configuration (HR.Payroll) - Restricted to HR Admin
[Authorize(Policy = "HR.Payroll.PayrollStructure.Manage")]

// Employee data (HR.Employees.Payroll) - Restricted to HR Manager or Employee (own data)
[Authorize(Policy = "HR.Employee.Payroll.View")]
await authorizationService.CheckEmployeeAccessAsync(employeeId, requireOwnership: true);
```

### **Data Validation**
- ✅ Bank account format validated
- ✅ IFSC code format validated (ABCD0123456)
- ✅ Amount ranges enforced
- ✅ Duplicate component prevention
- ✅ Tenant isolation enforced

---

## 📊 Database Schema

### **TIER 1 Tables (HR.Payroll)**
```sql
-- Master payroll structures
CREATE TABLE PayrollStructures (
    Id UUID PRIMARY KEY,
    TenantId UUID,
    EmployeeId UUID, -- Link to specific employee
    PayElementId UUID NOT NULL,
    PayElementType INT NOT NULL, -- 1=Earning, 2=Deduction
    Amount DECIMAL(18,2) NOT NULL
);

-- Master earning components
CREATE TABLE EarningComponents (
    Id UUID PRIMARY KEY,
    TenantId UUID,
    Name VARCHAR(256) NOT NULL,
    ShortName VARCHAR(50),
    Percentage DECIMAL(5,2),
    CalculationType INT NOT NULL
);

-- Master deduction components
CREATE TABLE DeductionComponents (
    Id UUID PRIMARY KEY,
    TenantId UUID,
    Name VARCHAR(256) NOT NULL,
    ShortName VARCHAR(50),
    Percentage DECIMAL(5,2),
    CalculationType INT NOT NULL
);
```

### **TIER 2 Tables (HR.Employees.Payroll)**
```sql
-- Employee payroll details (bank & salary info)
CREATE TABLE EmployeePayrollDetails (
    Id UUID PRIMARY KEY,
    EmployeeId UUID NOT NULL UNIQUE,
    BankName VARCHAR(256),
    BranchName VARCHAR(256),
    AccountNumber VARCHAR(50),
    IfscCode VARCHAR(11),
    EpfNumber VARCHAR(50),
    UanNumber VARCHAR(50),
    BasicSalary DECIMAL(18,2),
    TdsPercentage DECIMAL(5,2),
    TdsAmount DECIMAL(18,2),
    FOREIGN KEY (EmployeeId) REFERENCES AppEmployees(Id)
);

-- Employee payroll components (individual amounts)
CREATE TABLE EmployeePayrollComponents (
    Id UUID PRIMARY KEY,
    EmployeeId UUID NOT NULL,
    PayElementId UUID NOT NULL,
    ComponentType INT NOT NULL, -- 1=Earning, 2=Deduction
    Amount DECIMAL(18,2) NOT NULL,
    IsFixed BOOLEAN DEFAULT TRUE,
    IsTemp BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (EmployeeId) REFERENCES AppEmployees(Id),
    FOREIGN KEY (PayElementId) REFERENCES PayElements(Id),
    UNIQUE (EmployeeId, PayElementId)
);
```

---

## 🐛 Common Issues & Solutions

### **Issue 1: Employee payroll not initialized after structure assignment**

**Cause**: `autoInitialize` parameter was set to `false` or initialization failed

**Solution**:
```csharp
// Manually initialize
await initializationService.InitializeFromStructureAsync(employeeId);

// Or re-assign structure with auto-init
await payrollStructureManager.SetEmployeePayrollStructureAsync(
    employeeId,
    structureId,
    autoInitialize: true
);
```

### **Issue 2: Employee amounts don't match template**

**Cause**: Employee amounts were customized after initialization

**Solution**: This is expected behavior. Use sync if you want to reset:
```csharp
// Sync components from structure (updates amounts)
await initializationService.SyncComponentsFromStructureAsync(employeeId);

// Or completely re-initialize (replaces everything)
await initializationService.InitializeFromStructureAsync(employeeId, replaceExisting: true);
```

### **Issue 3: RunPayroll generates incorrect amounts**

**Cause**: Payroll processing uses **employee-specific data**, not templates

**Solution**: Check `EmployeePayrollDetails` and `EmployeePayrollComponents` tables:
```csharp
var details = await payrollDetailsRepository.FindByEmployeeIdAsync(employeeId);
var components = await payrollComponentRepository.GetByEmployeeIdAsync(employeeId);

// Verify amounts are correct
foreach (var component in components)
{
    Logger.LogInformation($"{component.PayElement.Name}: {component.Amount}");
}
```

---

## 📚 API Endpoints

### **Master Configuration (HR Admin only)**
```http
POST   /api/payroll/structures          # Create payroll structure
PUT    /api/payroll/structures/{id}     # Update structure
GET    /api/payroll/structures          # List structures
DELETE /api/payroll/structures/{id}     # Delete structure

POST   /api/payroll/earning-components  # Create earning component
POST   /api/payroll/deduction-components # Create deduction component
```

### **Employee Payroll (HR Manager / Employee)**
```http
GET  /api/employees/{id}/payroll        # Get employee payroll (with auth check)
PUT  /api/employees/{id}/payroll        # Update employee payroll (with auth check)
POST /api/employees/{id}/payroll/initialize # Manually initialize payroll
POST /api/employees/{id}/payroll/sync   # Sync from structure
```

### **Payroll Processing**
```http
POST /api/payroll/run                   # Process payroll for month
GET  /api/payroll/salary-slips          # Get generated salary slips
```

---

## 🎓 Best Practices

### **✅ DO**
1. ✅ Always use `autoInitialize: true` when assigning structures
2. ✅ Create master templates before assigning to employees
3. ✅ Use `SyncComponentsFromStructureAsync()` to propagate template changes
4. ✅ Validate bank details before processing payroll
5. ✅ Keep master templates clean and well-organized
6. ✅ Document any customizations to employee amounts

### **❌ DON'T**
1. ❌ Directly modify `EmployeePayrollDetails` without validation
2. ❌ Delete master components that are in use
3. ❌ Assign structures without initializing employee data
4. ❌ Mix up TIER 1 and TIER 2 responsibilities
5. ❌ Process payroll without verifying employee data first
6. ❌ Forget to sync employees after updating master templates

---

## 🔧 Maintenance

### **Monthly Tasks**
- Run payroll for current month
- Generate and send salary slips
- Archive previous month's data

### **Yearly Tasks**
- Review and update master earning/deduction components
- Update salary structures for inflation/policy changes
- Sync all employees with new structures

### **As-Needed**
- Initialize new employees
- Handle salary revisions
- Update bank details
- Handle employee resignations

---

## 📞 Support

For questions or issues with the payroll system:
1. Check this documentation first
2. Review the common issues section
3. Check application logs for initialization errors
4. Contact the development team with:
   - Employee ID
   - Payroll structure ID
   - Error message/logs
   - Expected vs actual behavior

---

**Last Updated**: 2024-12-22  
**Version**: 1.0  
**Author**: ShikshaMitra Development Team
