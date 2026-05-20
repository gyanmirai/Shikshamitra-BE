## Why

The `finance-student-charge-management` implementation completed the feature but the code does not match the structural and readability quality of the HR module. The `ChargeAppService` has 400+ lines with inline DTO mapping, duplicated join logic, no logging, old-style constructor injection, and no AutoMapper profile — making it harder to maintain and extend.

## What Changes

- **Refactor `ChargeAppService`** to use primary-constructor DI, `ILogger`, and private named guard/helper methods (`GetChargeOrThrowAsync`, `GetCurrentUserId`)
- **Add `FinanceMapperProfile`** (AutoMapper) in the Application layer for all charge ↔ DTO mappings; remove all inline mapping from the service
- **Introduce `IChargeRepository`** custom repository interface in the Domain layer (mirrors `IEmployeeRepository`)
- **Decompose `GetAllAsync`** into focused private methods: `BuildBaseJoinQuery`, `ApplyFilters`, `GetStatusCountsAsync`, `MapToListItem`
- **Deduplicate `GetByIdAsync` / `BuildChargeResponseAsync`** — both currently rebuild the same multi-table join; unify into a single `BuildChargeDetailJoinAsync`
- **Add `ILogger` to `ChargeManager`** with structured log messages on create (mirrors `EmployeeManager`)
- **Convert `ChargeManager`** to use named arguments on `new Charge(...)` constructor call for readability
- **Fix indentation** — current file was written without tabs, violating the codebase standard (`\t` indent, file-scoped namespaces, primary constructors)

## Capabilities

### New Capabilities

- `finance-charge-automapper`: AutoMapper profile and object-mapping conventions for Charge → DTO
- `finance-charge-repository`: Custom `IChargeRepository` domain interface with query methods for common access patterns

### Modified Capabilities

- `finance-fees-charges`: Refactored `ChargeAppService` and `ChargeManager` — no behavior changes, implementation quality only

## Impact

- `src/ShikshaMitra.Application/Finance/Fees/Charges/ChargeAppService.cs` — full refactor
- `src/ShikshaMitra.Domain/Finance/Fees/Charges/ChargeManager.cs` — add ILogger, named args
- `src/ShikshaMitra.Domain/Finance/Fees/Charges/IChargeRepository.cs` — new file
- `src/ShikshaMitra.EntityFrameworkCore/Finance/Fees/Charges/EfCoreChargeRepository.cs` — new file (implements IChargeRepository)
- `src/ShikshaMitra.Application/Finance/FinanceMapperProfile.cs` — new file
- No API contract changes, no database migration needed
