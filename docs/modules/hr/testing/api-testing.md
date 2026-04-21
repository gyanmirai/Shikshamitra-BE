# HR Module API Testing

API-level tests for the HR module endpoints. These validate the full HTTP pipeline including routing, serialization, authentication, and response contracts.

**Total test coverage: ~194 tests across 32 controllers (all HR endpoints covered)**

## Test Inventory

### HR/Employees -- 10 controllers, ~76 tests

| Controller | Test Class | Tests | Tiers |
|-----------|-----------|-------|-------|
| EmployeesController | `EmployeeListApi_Tests` | GET list, GET by ID, 404, filter status, filter isTeaching, filter roleId, search by name, pagination, verify response contract | Critical, Important, Extended |
| PersonalInfoController | `EmployeePersonalInfoApi_Tests` | POST create (multipart), POST with profile picture (image upload), POST with all optional fields, GET after create, PUT update, 404, not-401 check, invalid JSON, missing required fields, invalid phone format, duplicate secondary mobile, PUT+GET round-trip verify | Critical, Important, Extended |
| AdditionalInfoController | `AdditionalInfoApi_Tests` | GET, PUT update, 404, update+verify, clear all fields, round-trip all boolean fields | Critical, Important, Extended |
| FamilyInfoController | `FamilyInfoApi_Tests` | GET, PUT update, 404, update+verify, all three members with full address, clear with empty request, address fields round-trip | Critical, Important, Extended |
| WorkExperienceController | `WorkExperienceApi_Tests` | GET, PUT update, 404, multiple experiences, clear with empty list, country/state info, update then verify count and fields | Critical, Important, Extended |
| ReportingManagerController | `ReportingManagerApi_Tests` | GET, PUT assign, 404, employees under manager, assign+verify, reassign manager A to B, verify employee count under manager | Critical, Important, Extended |
| PayrollController | `EmployeePayrollApi_Tests` | GET, PUT with bank details, 404, full payroll with components, update then verify bank details, TDS boundary values (0%, 100%) | Critical, Important, Extended |
| EmployeeStatusController | `EmployeeStatusApi_Tests` | PUT status to inactive, 404, idempotent same status, multi-status transitions, Left and Retired statuses, reject invalid status value | Critical, Important, Extended |
| DocumentConfigController | `DocumentConfigApi_Tests` | POST create, GET list, GET by ID, PUT, DELETE, 404, reject invalid title, reject out-of-range size, full CRUD lifecycle | Critical, Important, Extended |
| EmployeeDocumentController | `EmployeeDocumentApi_Tests` | Upload with valid doc config, upload image file (JPEG), GET list, 404, DELETE, download document, fail upload without file, upload PDF, update existing document, search by filename | Critical, Important, Extended |

### HR/Payroll -- 9 controllers, ~43 tests

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
| AttendanceImportLogsController / HrAutomatedAttendanceRunsController | `AttendanceImportLogsApi_Tests` | Payroll logs + HR mirror `/api/hr/attendance/automated-runs` list; GET detail+rows, reject multi-month preview file | Critical, Important |

### HR/Calendar -- 5 controllers, ~28 tests

| Controller | Test Class | Tests | Tiers |
|-----------|-----------|-------|-------|
| EventTypeController | `EventTypeApi_Tests` | CRUD, 404 | Critical, Important |
| HolidayTypeController | `HolidayTypeApi_Tests` | CRUD, 404 | Critical, Important |
| EventController | `EventApi_Tests` | CRUD, by-type, by-status, 404 | Critical, Important |
| HolidayController | `HolidayApi_Tests` | CRUD, by-type, by-status, 404 | Critical, Important |
| CalendarViewController | `CalendarViewApi_Tests` | GET view, 404 item details | Critical, Important |

### HR/AttendanceAndLeaves -- 8 controllers, ~22 tests

| Controller | Test Class | Tests | Tiers |
|-----------|-----------|-------|-------|
| LeaveTypesController | `LeaveTypeApi_Tests` | `/api/hr-leave-types` create + get-by-id, list search with `academicYearId` | Critical, Important |
| LeavePoliciesController | `LeavePolicyApi_Tests` | `/api/leave-policies` create + get-by-id, list search with `academicYearId` | Critical, Important |
| AttendanceConfigurationController | `AttendanceConfigurationApi_Tests` | `/api/attendance/configuration` upsert success, overtime validation failure | Critical, Important |
| LeaveManagementController | `LeaveManagementApi_Tests` | GET list (academicYearId + month-year), GET by Guid404, delete404, invalid page/pageSize 400 | Critical, Important |
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
| `EmployeeTestHelper` | `HR/Employees/EmployeeTestHelper.cs` | Creates employee (with/without picture), creates document configs, builds full/minimal personal info requests, builds synthetic JPEG |
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

> Smoke tests are opt-in by default. Set `APITEST_ENABLE_SMOKE=true` before running Tier=Smoke.

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
