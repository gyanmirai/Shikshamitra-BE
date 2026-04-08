# HR Module API Testing

API-level tests for the HR module endpoints. These validate the full HTTP pipeline including routing, serialization, authentication, and response contracts.

**Total test coverage: 155 tests across 31 controllers (all HR endpoints covered)**

## Test Inventory

### HR/Employees -- 10 controllers, ~40 tests

| Controller | Test Class | Tests | Tiers |
|-----------|-----------|-------|-------|
| EmployeesController | `EmployeeListApi_Tests` | GET list, GET by ID, filter status, pagination | Critical, Important, Extended |
| PersonalInfoController | `EmployeePersonalInfoApi_Tests` | POST create (multipart), GET, PUT update, 404 | Critical, Important |
| AdditionalInfoController | `AdditionalInfoApi_Tests` | GET, PUT update, 404, update+verify | Critical, Important, Extended |
| FamilyInfoController | `FamilyInfoApi_Tests` | GET, PUT update, 404, update+verify | Critical, Important, Extended |
| WorkExperienceController | `WorkExperienceApi_Tests` | GET, PUT update, 404, multiple experiences | Critical, Important, Extended |
| ReportingManagerController | `ReportingManagerApi_Tests` | GET, PUT assign, 404, employees under manager, assign+verify | Critical, Important, Extended |
| PayrollController | `EmployeePayrollApi_Tests` | GET, PUT update, 404 | Critical, Important |
| EmployeeStatusController | `EmployeeStatusApi_Tests` | PUT status change, 404, idempotent, multi-status transitions | Critical, Important, Extended |
| DocumentConfigController | `DocumentConfigApi_Tests` | POST create, GET list, GET by ID, PUT, DELETE, 404 | Critical, Important |
| EmployeeDocumentController | `EmployeeDocumentApi_Tests` | POST upload, GET list, DELETE, 404 | Critical, Important |

### HR/Payroll -- 8 controllers, ~40 tests

| Controller | Test Class | Tests | Tiers |
|-----------|-----------|-------|-------|
| EarningComponentController | `EarningComponentApi_Tests` | CRUD, search, percentage-based, pagination | Critical, Important, Extended |
| DeductionComponentController | `DeductionComponentApi_Tests` | CRUD, search, flat amount, lifecycle | Critical, Important, Extended |
| PayElementController | `PayElementApi_Tests` | CRUD, 404, dependent element | Critical, Important |
| PfRuleController | `PfRuleApi_Tests` | CRUD, 404, search | Critical, Important |
| DayCalculationController | `DayCalculationApi_Tests` | Calendar mode, manual mode | Critical, Important |
| EmployeeSalaryStructureController | `EmployeeSalaryStructureApi_Tests` | GET, PUT with components | Critical |
| RunPayrollController | `RunPayrollApi_Tests` | GET preview, POST lock, invalid format | Critical, Important, Extended |
| SalarySlipController | `SalarySlipApi_Tests` | GET slip, missing params, invalid format | Important |

### HR/Calendar -- 5 controllers, ~28 tests

| Controller | Test Class | Tests | Tiers |
|-----------|-----------|-------|-------|
| EventTypeController | `EventTypeApi_Tests` | CRUD, 404 | Critical, Important |
| HolidayTypeController | `HolidayTypeApi_Tests` | CRUD, 404 | Critical, Important |
| EventController | `EventApi_Tests` | CRUD, by-type, by-status, 404 | Critical, Important |
| HolidayController | `HolidayApi_Tests` | CRUD, by-type, by-status, 404 | Critical, Important |
| CalendarViewController | `CalendarViewApi_Tests` | GET view, 404 item details | Critical, Important |

### HR/AttendanceAndLeaves -- 7 controllers, ~20 tests

| Controller | Test Class | Tests | Tiers |
|-----------|-----------|-------|-------|
| LeaveTypesController | `LeaveTypeApi_Tests` | CRUD, delete | Critical, Important |
| LeavePoliciesController | `LeavePolicyApi_Tests` | CRUD, delete | Critical, Important |
| LeaveManagementController | `LeaveManagementApi_Tests` | GET list, GET by ID, invalid ID, delete | Critical, Important |
| AttendanceTrackingController | `AttendanceTrackingApi_Tests` | Date-wise, employee-wise | Critical, Important |
| AutomatedNotificationsController | `AutomatedNotificationsApi_Tests` | CRUD | Critical, Important |
| NotificationInfoController | `NotificationInfoApi_Tests` | GET list, GET by ID | Important |

### HR/Reports -- 1 controller, ~5 tests

| Controller | Test Class | Tests | Tiers |
|-----------|-----------|-------|-------|
| EmployeeReportController | `EmployeeReportApi_Tests` | GET report, contacts, PDF export, Excel export, gender filter | Critical, Important, Extended |

### Smoke Tests (hosted, real auth)

| Suite | Test Class | What it validates |
|-------|-----------|-------------------|
| Payroll smoke | `HRPayrollSmokeTests` | Earning/deduction list 200, unauthenticated 401 |
| Employee smoke | `HREmployeeSmokeTests` | Employee list 200, document config list 200 |
| Calendar smoke | `HRCalendarSmokeTests` | Event types, holiday types, holidays list 200 |
| Leaves smoke | `HRLeavesSmokeTests` | Leave types, leave policies, automated notifications 200 |

### Shared Test Helpers

| Helper | Location | Purpose |
|--------|----------|---------|
| `EmployeeTestHelper` | `HR/Employees/EmployeeTestHelper.cs` | Creates employee via POST personal-info, returns ID |
| `PayrollTestHelper` | `HR/Payroll/PayrollTestHelper.cs` | Creates earning/deduction components, PF rules |
| `CalendarTestHelper` | `HR/Calendar/CalendarTestHelper.cs` | Creates event types and holiday types |

## Contract Values Reference

The HTTP API uses string-based enum values parsed by contract mappers. Always use uppercase:

| Field | Valid Values |
|-------|-------------|
| `EarningType` | `FIXED`, `VARIABLE` |
| `DeductionType` | `FIXED`, `VARIABLE` |
| `CalculationType` | `AMOUNT`, `PERCENTAGE` |

When `CalculationType` is `PERCENTAGE`, you must provide `SelectedEarningComponentId` or `SelectedDeductionComponentId`.

## Running HR API Tests

```bash
# All HR API tests (non-smoke)
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier!=Smoke"

# Critical HR only (fast, for PR)
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier=Critical"

# HR smoke (requires running host)
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier=Smoke"
```

## Blueprint for Other Modules

Use the HR API tests as a template. Each module should follow the same pattern:

1. **Test classes** per controller area (e.g., `Payroll/`, `Employees/`)
2. **Helper method** for creating test entities via the API
3. **Tier attributes** on every test method
4. **Module attribute** on every test class
5. **Unique test data** via `TestDataHelper`
6. **Smoke test class** under `Smoke/{Module}/`

## Related

- [API testing guide (common)](../../../common/testing/api-testing-guide.md)
- [HR E2E tests (app-service level)](./HR-Module-E2E-Tests.md)
- [HR module overview](../README.md)
