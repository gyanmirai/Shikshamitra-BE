# HR Application Layer — Patterns & Conventions

> Companion to [best-practices.md](../../best-practices.md). Covers HR-specific
> Application layer standards adopted during the refactoring.

---

## 1. AppService Decomposition

The HR Employee module follows a **one-service-per-concern** pattern. The original
monolithic `EmployeeAppService` (~1 145 lines, 25+ methods) was split into focused
services. `EmployeeAppService` and `IEmployeeAppService` are marked `[Obsolete]`
but retained for test compatibility (`EmployeeAppService_Tests.cs`, 679 lines).

| Service | Concern | Key Methods |
|---------|---------|-------------|
| `EmployeePersonalInfoAppService` | Employee lifecycle (create, read, update basic info) | `CreateAsync`, `GetAsync`, `UpdateAsync`, `GetByUserIdAsync` |
| `EmployeeDocumentAppService` | Documents & profile pictures | `UploadDocumentsAsync`, `GetDocumentAsync`, `DeleteDocumentAsync`, `UploadProfilePictureAsync`, `GetProfilePictureAsync` |
| `EmployeePayrollAppService` | Employee-level payroll details & contract | `GetPayrollDetailsAsync`, `UpdatePayrollDetailsAsync`, `GetPayrollContractAsync`, `UpdatePayrollContractAsync` |
| `EmployeeSearchAppService` | Filtered list & search | `GetFilteredListAsync`, `SearchAsync` |
| `EmployeeStatusAppService` | Status transitions | `ChangeStatusAsync` |
| `EmployeeFamilyInfoAppService` | Family information | `GetAsync`, `UpsertAsync` |
| `EmployeeWorkExperienceAppService` | Work experience | `GetAsync`, `UpsertAsync` |
| `EmployeeAdditionalDetailsAppService` | Additional details (BPL, disability, etc.) | `GetAsync`, `UpsertAsync`, `PatchAsync` |
| `ReportingManagerAppService` | Reporting hierarchy | `AssignAsync`, `GetAsync`, `GetByManagerAsync` |
| `EmployeeAuthorizationService` | IDOR / ownership checks | `CheckEmployeeAccessAsync`, `CheckEmployeeModifyAccessAsync` |
| `EmployeeAppService` *(obsolete)* | Legacy facade — retained for test compatibility | All methods delegated to focused services above |

### Why split?

- **Readability**: Each file is < 200 lines, focused on one concern.
- **Testability**: Smaller surface = focused unit tests.
- **Authorization**: Each concern can have its own permission granularity.
- **Merge conflicts**: Fewer developers touching the same file.

---

## 2. Canonical AppService Template

```csharp
[RemoteService(false)]
[Authorize(HRPermissions.SomeModule.View)]
public class SomeFeatureAppService(
    ISomeRepository repository,
    SomeDomainManager manager,
    SomeAuthorizationService authService)
    : ApplicationService, ISomeFeatureAppService
{
    [Authorize(HRPermissions.SomeModule.View)]
    public async Task<SomeDto> GetAsync(Guid id)
    {
        await authService.CheckAccessAsync(id);
        var entity = await repository.GetAsync(id);
        return ObjectMapper.Map<SomeEntity, SomeDto>(entity);
    }

    [Authorize(HRPermissions.SomeModule.Add)]
    public async Task<SomeDto> CreateAsync(CreateSomeDto input)
    {
        // FluentValidation runs automatically — no manual checks here
        var entity = await manager.CreateAsync(/* ... */);
        await repository.InsertAsync(entity);
        return ObjectMapper.Map<SomeEntity, SomeDto>(entity);
    }
}
```

Key traits:
- Primary constructor for DI.
- `[RemoteService(false)]` — controller delegates; the service is not auto-exposed.
- `[Authorize]` on every public method.
- No inline validation (FluentValidation handles it).
- No `UserFriendlyException` — only `BusinessException(HRErrorCodes.X)`.
- No manual `MapToDto` — all mappings in `HRMapperProfile`.

---

## 3. Error Code Catalog

All HR error codes live in `Domain.Shared/HR/HRErrorCodes.cs`.

Convention: `"HR:{SubModule}:{6-digit}"`.

| Range | SubModule |
|-------|-----------|
| `01xxxx` | Employee |
| `02xxxx` | Attendance & Leave |
| `03xxxx` | Payroll |
| `04xxxx` | Calendar |
| `05xxxx` | Reports |

See the file for the full list.

---

## 4. FluentValidation

Validators live in **Application.Contracts** next to the DTO they validate.

```
Application.Contracts/
  HR/
    Employees/
      PersonalInfo/
        CreateEmployeePersonalInfoDtoValidator.cs
```

ABP auto-discovers validators — no manual registration needed.

---

## 5. AutoMapper Profile

All HR entity-to-DTO mappings are registered in a single `HRMapperProfile` in the
Application project. Manual `MapToDto` helper methods are not used.

---

## 6. Domain Events

ETOs in `Domain.Shared/HR/Events/`:

| ETO | Published When |
|-----|----------------|
| `EmployeeCreatedEto` | After employee + user creation succeeds |
| `EmployeeStatusChangedEto` | After status transition persisted |
| `PayrollLockedEto` | After monthly payroll is locked |
| `LeaveStatusChangedEto` | After leave request approved/rejected |

Handlers in `Application/HR/EventHandlers/` — stub implementations ready for
future cross-module reactions (notifications, audit logs, etc.).
