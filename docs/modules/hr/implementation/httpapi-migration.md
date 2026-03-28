# HR HttpApi Migration

> Documents the migration of HR HttpApi controllers from the monolithic
> `IEmployeeAppService` to focused service interfaces.

---

## Controllers Migrated

| Controller | Previous Service | New Service(s) | Mapper Status |
|-----------|-----------------|----------------|---------------|
| `FamilyInfoController` | `IEmployeeAppService` | `IEmployeeFamilyInfoAppService` | Eliminated — service accepts contract types directly |
| `WorkExperienceController` | `IEmployeeAppService` | `IEmployeeWorkExperienceAppService` | Eliminated — service accepts contract types directly |
| `PayrollController` | `IEmployeeAppService` | `IEmployeePayrollAppService` | Not needed — service accepts `PayrollContract` directly |
| `AdditionalInfoController` | `IEmployeeAppService` | `IEmployeeAdditionalDetailsAppService` | Inline mapping in controller |
| `EmployeesController` | `IEmployeeAppService` | `IEmployeeSearchAppService` + `IEmployeePersonalInfoAppService` | Local `MapToEmployeeDetails` helper retained |
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
- `EventAndHolidayController`: Marked `[Obsolete]` — use Calendar module controllers instead

## Legacy Code Status

- `EmployeeAppService.cs` and `IEmployeeAppService.cs` are marked `[Obsolete]`
- Retained because `EmployeeAppService_Tests.cs` (679 lines) still references them
- No production code (controllers) depends on them — safe to delete once tests are rewritten
