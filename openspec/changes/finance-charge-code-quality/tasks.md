## 1. Domain Layer — Custom Repository Interface

- [x] 1.1 Create `src/ShikshaMitra.Domain/Finance/Fees/Charges/Repositories/IChargeRepository.cs` with `IChargeRepository : IRepository<Charge, Guid>` interface — declare `GetChargeWithDetailsAsync(Guid chargeId, CancellationToken ct)` returning `Task<ChargeJoinResult?>` and document with XML summary following `IEmployeeRepository` style
- [x] 1.2 Define `ChargeJoinResult` record alongside `IChargeRepository` (in same file or dedicated `ChargeJoinResult.cs`) with properties: `Charge`, `Student`, `Category`, `Standard`, `Division`, `Board`, `Medium`, `AcademicYear`, `Requestor` — all non-nullable typed to their domain entities

## 2. EF Core Layer — Repository Implementation

- [x] 2.1 Create `src/ShikshaMitra.EntityFrameworkCore/Finance/Fees/Charges/EfCoreChargeRepository.cs` implementing `IChargeRepository` — constructor uses primary constructor DI for `IDbContextProvider<ShikshaMitraDbContext>`, class extends `EfCoreRepository<ShikshaMitraDbContext, Charge, Guid>`
- [x] 2.2 Implement `GetChargeWithDetailsAsync` — write the 8-table LINQ join (Charge, Student, Category, Standard, Division, Board, Medium, AcademicYear, User/Requestor) as a single queryable, call `FirstOrDefaultAsync`, return `ChargeJoinResult` or `null`
- [x] 2.3 Register `EfCoreChargeRepository` as the implementation for `IChargeRepository` in `ShikshaMitraEntityFrameworkCoreModule.cs` (or via ABP auto-registration if `DefaultRepositoryDbContextType` picks it up)

## 3. Application Layer — AutoMapper Profile

- [x] 3.1 Create `src/ShikshaMitra.Application/Finance/FinanceMapperProfile.cs` as `FinanceMapperProfile : Profile` — register `CreateMap<ChargeCategory, ChargeCategoryDto>()` and any other flat entity-to-DTO mappings for Finance; follow `HRMapperProfile.cs` structure exactly (file-scoped namespace, primary ctor calling `base()`)
- [x] 3.2 Verify `FinanceMapperProfile` is picked up by ABP AutoMapper — check that `ShikshaMitraApplicationModule` inherits from the module that scans for `Profile` classes (it should already; confirm no manual registration needed)

## 4. Application Layer — ChargeAppService Refactor

- [x] 4.1 Rewrite `ChargeAppService` to use primary constructor DI — replace all `private readonly` field declarations and the constructor body with a single primary constructor parameter list; replace `_chargeRepository` with `IChargeRepository chargeRepository`, add `ILogger<ChargeAppService> logger`; use tab indentation throughout
- [x] 4.2 Add private guard helper `private async Task<Charge> GetChargeOrThrowAsync(Guid chargeId)` — calls `chargeRepository.GetAsync(chargeId)` (or `FindAsync` + throw) and returns the entity or throws `BusinessException(FinanceChargeErrorCodes.ChargeNotFound)` following the `GetEmployeeAsync` pattern in `EmployeeManager`
- [x] 4.3 Replace `BuildChargeResponseAsync(Charge charge)` with a call to `chargeRepository.GetChargeWithDetailsAsync(charge.Id)` — remove the duplicate 8-table join from `BuildChargeResponseAsync` entirely; `GetByIdAsync` also delegates to this repository method
- [x] 4.4 Extract `private static ChargeListItem MapToListItem(ChargeJoinResult r)` — move the inline 28-property initialiser from `GetAllAsync` into this named static method; call `MapToListItem(r)` from the `.Select()` in `GetAllAsync`
- [x] 4.5 Extract `private static ChargeData MapToChargeData(ChargeJoinResult r, Charge charge)` — move the inline `new ChargeData { … }` block from `BuildChargeResponseAsync` into this method
- [x] 4.6 Extract `private static ChargeDetailData MapToDetailData(ChargeJoinResult r, IdentityUser? approver)` — move the inline `new ChargeDetailData { … }` block from `GetByIdAsync` into this method; call it from `GetByIdAsync`
- [x] 4.7 Decompose `GetAllAsync` — extract `private static IQueryable<ChargeJoinResult> ApplyFilters(IQueryable<ChargeJoinResult> q, GetChargesRequest input)` for the filter block (BoardId, MediumId, Search); the method body should be ≤ 40 lines after extraction
- [x] 4.8 Add structured logging — `logger.LogDebug(...)` at start of `CreateAsync`, `logger.LogInformation(...)` at end of `ApproveAsync` and `RejectAsync` with charge ID and user ID; follow exact log message format from `EmployeeManager`

## 5. Domain Layer — ChargeManager Logging

- [x] 5.1 Add `ILogger<ChargeManager>` to `ChargeManager` primary constructor
- [x] 5.2 Add `logger.LogDebug(...)` at the start of `CreateAsync` (before student lookup) with `StudentId`, `CategoryId`, and `Amount` — match the style of `EmployeeManager.CreateEmployeeAsync` log entry

## 6. Build & Verification

- [x] 6.1 Run `dotnet build` and confirm 0 errors and 0 warnings
- [x] 6.2 Run `dotnet test` (or at minimum `dotnet test test/ShikshaMitra.Application.Tests`) and confirm existing charge tests still pass
- [x] 6.3 Verify the `EfCoreChargeRepository` is correctly registered by checking that the charge API endpoints still respond correctly (manual test or via existing HTTP tests if any)
