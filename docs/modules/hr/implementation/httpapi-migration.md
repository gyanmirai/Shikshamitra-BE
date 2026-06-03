# HR HttpApi Migration

> Documents the migration of HR HttpApi controllers from the monolithic
> `focused employee service interfaces` to focused service interfaces.

---

## Controllers Migrated

| Controller | Previous Service | New Service(s) | Mapper Status |
|-----------|-----------------|----------------|---------------|
| `FamilyInfoController` | `focused employee service interfaces` | `IEmployeeFamilyInfoAppService` | Eliminated — service accepts contract types directly |
| `WorkExperienceController` | `focused employee service interfaces` | `IEmployeeWorkExperienceAppService` | Eliminated — service accepts contract types directly |
| `PayrollController` | `focused employee service interfaces` | `IEmployeePayrollAppService` | Not needed — service accepts `PayrollContract` directly |
| `AdditionalInfoController` | `focused employee service interfaces` | `IEmployeeAdditionalDetailsAppService` | Inline mapping in controller |
| `EmployeesController` | `focused employee service interfaces` | `IEmployeeSearchAppService` + `IEmployeePersonalInfoAppService` | Local `MapToEmployeeDetails` helper retained |
| `PersonalInfoController` | Already migrated | `IEmployeePersonalInfoAppService` | Uses `PersonalInfoContractMapper` (moved to HttpApi) |

## Contract Mapper Relocation

9 contract mapper files were moved from `Application.Contracts` to `HttpApi/Mappers/HR/`:
- `PersonalInfoContractMapper` — 6 `UserFriendlyException` replaced with `BusinessException(HRErrorCodes.X)`
- `AdditionalInfoContractMapper`, `ReportingManagerContractMapper`, `LeaveManagementContractMapper`
- `DocumentConfigContractMapper`, `LeavePolicyContractMapper`, `LeaveTypeContractMapper`

2 contract mappers deleted (no longer needed):
- `FamilyInfoContractMapper` — new service accepts `FamilyInfoRequest` directly
- `WorkExperienceContractMapper` — new service accepts `WorkExperienceContract` directly

## Other Controller Fixes

- `PersonalInfoController`: Removed 50-line `ValidateRequest()` method with 8 `UserFriendlyException` throws (now handled by FluentValidation)
- `RunPayrollController`: 1 `UserFriendlyException` → `BusinessException(HRErrorCodes.InvalidMonthYearFormat)`
- `SalarySlipController`: 4 `UserFriendlyException` → `BusinessException(HRErrorCodes.InvalidMonthYearFormat)`
- `EventAndHolidayController`: Marked `current` — use Calendar module controllers instead

## Current Code Status

- `EmployeePersonalInfoAppService.cs` and `focused employee service interfaces.cs` are marked `current`
- Retained because `EmployeePersonalInfoAppService_Tests.cs` (679 lines) still references them
- No production code (controllers) depends on them — safe to delete once tests are rewritten
