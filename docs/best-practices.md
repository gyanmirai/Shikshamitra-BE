# ShikshaMitra — Project Best Practices

> **Audience**: AI agents, solution architects.
> **Applies to**: All modules across the ShikshaMitra backend.

---

## 1. Architecture

### 1.1 ABP DDD Layering (Strict)

| Layer | Project | Allowed Dependencies | Owns |
|---|---|---|---|
| Domain.Shared | `ShikshaMitra.Domain.Shared` | None (leaf) | Enums, consts, shared value types |
| Domain | `ShikshaMitra.Domain` | Domain.Shared | Aggregates, entities, value objects, domain services, repository interfaces |
| Application.Contracts | `ShikshaMitra.Application.Contracts` | Domain.Shared | DTOs, app service interfaces, permission definitions |
| Application | `ShikshaMitra.Application` | Domain, Application.Contracts | App service implementations, AutoMapper profiles |
| EntityFrameworkCore | `ShikshaMitra.EntityFrameworkCore` | Domain | DbContext, entity configs, repository implementations |
| HttpApi | `ShikshaMitra.HttpApi` | Application.Contracts | REST controllers (thin delegation only) |
| HttpApi.Host | `ShikshaMitra.HttpApi.Host` | All | Hosting, middleware, startup |

**Rule**: Never reference Application or EntityFrameworkCore from Domain. Domain is the dependency root.

### 1.2 Folder Conventions

Each module follows identical folder structure across all layers:

```
{Layer}/
  {Module}/
    {Subdomain}/
      {Concern}/
```

#### Canonical Domain Sub-module Template

Every sub-module in the Domain layer **must** contain these seven folders. Add a `.gitkeep` file to any folder that starts empty — Git does not track empty directories and the scaffolding is lost on a fresh clone without it.

```
{SubModule}/
  Aggregates/     → Aggregate roots, one class per file
  Entities/       → Non-root entities with own identity and lifecycle (separate DB table)
  ValueObjects/   → Immutable owned types (record or sealed class; OwnsOne/OwnsMany in EF)
  Managers/       → Domain services: lifecycle orchestration, cross-repo or cross-aggregate ops
  Services/       → Focused domain services: code generation, validation, initialization
  Repositories/   → IXxxRepository interface definitions
  Exceptions/     → Domain exceptions: InvalidXxxException, XxxAlreadyExistsException, etc.
```

#### Nested Concern Template

When a sub-module contains a secondary concern with its own aggregates and repository (e.g., `Settings`, `SalarySlips`, `PayrollStructures`), that concern gets a child folder that follows the **same canonical template**:

```
{SubModule}/
  {NestedConcern}/
    Aggregates/
    Entities/
    ValueObjects/
    Managers/
    Services/
    Repositories/
    Exceptions/
```

A sub-module may combine both patterns: canonical folders at its root **and** nested concern subfolders. `Employees/` is the reference for this hybrid — canonical folders at root plus `Settings/` and `Payroll/` as nested concerns.

**Depth cap**: Maximum nesting is `{Module}/{SubModule}/{NestedConcern}/`. A fourth level is never added. If a concern feels like it needs deeper nesting, promote it to its own top-level sub-module.

#### Placement Rules

| Rule | Detail |
|---|---|
| No flat root files | Every `.cs` file belongs inside a canonical folder. A file at `{SubModule}/` root is a structural violation. |
| Exceptions in `Exceptions/` | Domain exception classes must not be placed at the sub-module root or alongside the type they relate to. |
| One aggregate per file | `Aggregates/` holds one aggregate root per file. Status enums or auxiliary types tightly coupled to one aggregate (e.g., `EventStatus`, `HolidayStatus`) live in the same `Aggregates/` folder — not in `Domain.Shared`. |
| No ad-hoc folders | One-off folders like `NotificationHistories/` violate the template. The contained type belongs in `Entities/` (if it has identity) or `ValueObjects/` (if it does not). |
| No orphaned types | If a sub-module uses nested concerns, every type for a concern lives inside its concern folder. Do not leave related aggregates at the parent level while siblings are nested. |
| No duplicate sub-modules | Never maintain two sub-modules that model the same real-world concept. The richer, structured model wins. The flat duplicate is a migration artifact and must be deleted after data migration. |

#### Anti-patterns

**Flat sub-module root** — `.cs` files loose at `{SubModule}/` root:

```
❌  HR/AttendanceAndLeaves/
      EmployeeLeave.cs
      EmployeeAttendance.cs
      IEmployeeLeaveRepository.cs
      NotificationHistories/NotificationInfo.cs

✅  HR/AttendanceAndLeaves/
      Aggregates/EmployeeLeave.cs
      Aggregates/EmployeeAttendance.cs
      Entities/NotificationInfo.cs
      Repositories/IEmployeeLeaveRepository.cs
      Exceptions/.gitkeep
```

**Orphaned type outside its nested-concern folder**:

```
❌  HR/Calendar/
      Aggregates/HolidayType.cs            ← orphaned from HolidayTypes/ group
      Repositories/IHolidayTypeRepository.cs

✅  HR/Calendar/
      HolidayTypes/Aggregates/HolidayType.cs
      HolidayTypes/Repositories/IHolidayTypeRepository.cs
```

**Duplicate sub-modules** — two sub-modules modelling the same domain concept:

```
❌  HR/Calendar/Events/Aggregates/Event.cs                  ← rich, academic-year-scoped
    HR/EventAndHolidays/Aggregates/EventAndHoliday.cs       ← flat single-flag duplicate

✅  HR/Calendar/Events/Aggregates/Event.cs
    (HR/EventAndHolidays/ deleted after data migration completes)
```

### 1.3 Namespace Convention

- Root namespace: `ShikshaMitra`
- Modules: `ShikshaMitra.{Module}.{Subdomain}` (e.g., `ShikshaMitra.HR.Employees`)
- Shared enums/types: `ShikshaMitra.Common` (lives in Domain.Shared but namespace is `ShikshaMitra.Common`)
- All projects use `<RootNamespace>ShikshaMitra</RootNamespace>` — namespaces mirror folder paths from the project root.

---

## 2. Domain Layer Rules

### 2.1 Aggregate Design

- Aggregate roots inherit `FullAuditedAggregateRoot<Guid>` and implement `IMultiTenant`.
- Non-root entities inherit `FullAuditedEntity<Guid>` + `IMultiTenant`.
- All properties use `private set` — mutations only through named methods on the entity.
- Constructors: public constructor with all required parameters; `private` parameterless constructor for ORM.
- Value objects: owned by aggregate, configured via EF Core `OwnsOne`/`OwnsMany`.

### 2.2 Domain Service Responsibilities

Domain services (`DomainService` base class) exist when logic:
- Spans multiple aggregates or repositories
- Requires external service coordination (Identity, BlobStorage)
- Encapsulates complex invariant enforcement

**Single Responsibility**: Each domain service owns one cohesive concern:

| Responsibility | Service Class |
|---|---|
| Employee lifecycle (create, update, status) | `EmployeeManager` |
| Employee code generation | `EmployeeCodeGenerator` |
| Employee documents & blob ops | `EmployeeDocumentManager` |
| File validation (magic bytes, size) | `FileValidationService` (static utility) |
| Payroll structure assignment | `PayrollStructureManager` |
| Leave request validation | `LeaveValidationService` |

**Anti-pattern**: God-manager classes that handle lifecycle + documents + code gen + file validation. Split by concern.

### 2.3 Repository Interfaces

- Defined in Domain layer: `I{Entity}Repository : IRepository<{Entity}, Guid>`
- Custom query methods added to interface, implemented in EntityFrameworkCore layer.
- Use `GetQueryableAsync()` + `AsyncExecuter` for LINQ queries in domain services.

### 2.4 Value Objects vs Entities

- **Value objects** (`BankDetails`, `WorkExperience`, `ContactDetails`): No identity, owned by aggregate, EF `OwnsOne`/`OwnsMany`.
- **Entities** (`EmployeeFamilyMember`, `EmployeeAdditionalDetails`): Own identity + lifecycle, separate table, FK to aggregate.

---

## 3. Application Layer Rules

### 3.1 App Services

- Inherit `ApplicationService` or `CrudAppService<>`.
- Thin orchestration: validate input → delegate to domain service → persist via repository → map to DTO.
- Never contain business logic — that belongs in domain services or entities.
- Use `ObjectMapper.Map<>()` (AutoMapper) for DTO ↔ Entity mapping.
- **Single Responsibility**: One AppService per cohesive concern. Target ~200 lines max per class; if larger, split by concern.
- **No manual `MapToDto` helpers** — register all mappings in the module's `AutoMapper Profile`. Exception: complex projections or DTO-to-DTO transforms may use extension methods in the Application layer.

### 3.2 DTOs

- Defined in Application.Contracts.
- Input DTOs: `Create{Entity}Dto`, `Update{Entity}Dto`, `Get{Entity}ListInput`.
- Output DTOs: `{Entity}Dto`, `{Entity}ListResponse`.
- No domain types in DTOs — enums from Domain.Shared are acceptable.
- **No mapper classes in Application.Contracts** — Contracts define shapes only; mapping lives in the Application layer.

### 3.3 Authorization

Two-layer authorization pattern:

1. **Coarse-grained** — `[Authorize(Permission)]` attribute on every public AppService method. Enforces "does this user have the right to call this endpoint at all?"
2. **Fine-grained (IDOR)** — Programmatic ownership/access checks inside the method body for operations on a specific resource. Use a dedicated authorization service (e.g., `EmployeeAuthorizationService.CheckEmployeeAccessAsync(id)`).

```csharp
[Authorize(HRPermissions.Employee.EmployeeList.View)]
public async Task<EmployeeDto> GetAsync(Guid employeeId)
{
    await _authorizationService.CheckEmployeeAccessAsync(employeeId);
    // ...
}
```

- Define permissions in `ShikshaMitraPermissions` (Application.Contracts).
- Per-module permission definition providers: `HRPermissionDefinitionProvider`, `StudentsPermissionDefinitionProvider`, etc.

### 3.4 Input Validation (FluentValidation)

- Use **FluentValidation** for all input DTO validation. ABP auto-discovers validators via `Volo.Abp.FluentValidation`.
- Validators live in **Application.Contracts**, alongside the DTOs they validate.
- Naming: `{DtoName}Validator` (e.g., `CreateEmployeeBasicInfoDtoValidator`).
- DataAnnotations (`[Required]`, `[MaxLength]`) may coexist for simple constraints, but FluentValidation is the primary mechanism for complex rules.
- Remove inline validation from AppService methods once a validator exists for the DTO.

### 3.5 Error Handling

- All business/domain errors use **`BusinessException`** with error codes from a centralized catalog.
- Error code constants live in `Domain.Shared` (e.g., `HRErrorCodes.cs`).
- Convention: `"{Module}:{SubModule}:{6-digit code}"` (e.g., `"HR:Employee:010001"`).
- **Never use `UserFriendlyException`** for business errors — reserve it only for truly exceptional edge cases where no error code makes sense.
- `BusinessException` supports localization via the ABP localization framework.

```csharp
// Good
throw new BusinessException(HRErrorCodes.EmployeeEmailAlreadyExists);

// Avoid
throw new UserFriendlyException("Email is already in use.");
```

### 3.6 Domain Events

- Use ABP's **distributed event bus** (`IDistributedEventBus`) for key state changes that other modules may react to.
- Event Transfer Objects (ETOs) live in **Domain.Shared** under `{Module}/Events/`.
- Naming: `{Entity}{Action}Eto` (e.g., `EmployeeCreatedEto`, `PayrollLockedEto`).
- Publish events **after** the core operation succeeds (after repository save).
- Event handlers live in the Application layer under `{Module}/EventHandlers/`.

---

## 4. Infrastructure Layer Rules

### 4.1 EF Core Configuration

- One `{Entity}Configuration.cs` per entity, implementing `IEntityTypeConfiguration<T>`.
- Place in: `EntityFrameworkCore/{Module}/{Subdomain}/{Entity}Configuration.cs`.
- Use `OwnsOne`/`OwnsMany` for value objects with `builder.Navigation(...).IsRequired()` as appropriate.
- Indexes: add unique indexes on business keys (e.g., `EmployeeCode`).

### 4.2 Repository Implementations

- Inherit `EfCoreRepository<ShikshaMitraDbContext, {Entity}, Guid>`.
- Only implement custom methods defined in the domain interface.
- File naming: `EfCore{Entity}Repository.cs`.

---

## 5. HttpApi Layer Rules

- Controllers inherit `AbpControllerBase`.
- Route pattern: `api/{resource}` (e.g., `api/employees`, `api/leave-types`).
- Thin delegation: controller → app service interface → return.
- Use `[ProducesResponseType]` for OpenAPI documentation.
- Area: `[Area("app")]`, Remote service: `[RemoteService(Name = "Default")]`.
- Inline validation in controllers is discouraged — use FluentValidation on input DTOs instead.

### 5.1 Contract Mappers

- Contract mappers (static classes that convert between HTTP-layer Request/Response types and AppService DTOs) belong in the **HttpApi** project under `Mappers/{Module}/`.
- They are NOT allowed in Application.Contracts — Contracts define shapes only.
- Use `BusinessException(HRErrorCodes.X)` for mapping failures, never `UserFriendlyException`.
- Naming: `{Concern}ContractMapper` (e.g., `PersonalInfoContractMapper`).

---

## 6. Multi-Tenancy

- Every tenant-scoped entity implements `IMultiTenant` (`Guid? TenantId`).
- ABP automatic tenant filter handles query isolation — never manually filter by `TenantId` in queries.
- Tenant-agnostic entities (host-level data) omit `IMultiTenant`.

---

## 7. Common Patterns

### 7.1 Enum Placement

All shared enums live in `ShikshaMitra.Domain.Shared` project, typically under:
- `Common/` → Cross-cutting types: `CastType`, `GenderType`, `MaritalStatusType`, `State`, `Country`
- `HR/Employees/` → Module-specific: `EmployeeStatusType`, `EmployeeGroupType`, `FamilyRelationType`
- `HR/AttendanceAndLeaves/` → `EmployeeAttendanceStatusType`, `EmployeeLeaveStatusType`
- `HR/Payroll/Settings/` → `PayElementType`, `EarningComponentType`, `DeductionComponentType`

All use namespace `ShikshaMitra.Common` or `ShikshaMitra.HR.*`.

### 7.2 Blob Storage

- ABP `IBlobContainer` with database provider (`BlobStoringDatabaseDomainModule`).
- Blob name convention: `{purpose}/{entityId}_{uniqueId}_{sanitizedFileName}`.
- Validation before save: extension whitelist + magic byte verification via `FileValidationService`.
- Profile pictures: separate blob path `profile-pictures/`.
- Documents: `employee-documents/{employeeId}/`.

### 7.3 Caching

- `IEmployeeCacheManager` pattern for entity-level cache invalidation.
- Invalidate on write paths (update, status change).

### 7.4 User-Scope Filtering

- Entities needing hierarchical filtering implement `IUserScopedEntity`.
- Scope hierarchy: Board → Medium → Stage → Class → Division.
- `NULL = ALL` principle: null scope value means unrestricted access at that level.

---

## 8. Testing

- Test projects: `ShikshaMitra.Application.Tests`, `ShikshaMitra.EntityFrameworkCore.Tests`, `ShikshaMitra.Domain.Tests`.
- Integration tests use ABP test infrastructure with in-memory DB.
- Test class naming: `{AppService}_Tests`.
- Data seeders: `ShikshaMitraSeederContributor` and module-specific seeders (`HRSeederContributor`, `StudentSeederContributor`).

---

## 9. Security Patterns

- `SecurityHelper` (static): Input sanitization, filename sanitization, dangerous character detection.
- `FileValidationService` (static): Magic byte validation, extension whitelist, size limits.
- Path traversal prevention: Always `Path.GetFileName()` before using user-supplied filenames.
- Password generation: `{CapitalizedUsername}@{Last4DigitsOfMobile}`.

---

## 10. Code Style

- C# latest (14.0), file-scoped namespaces, primary constructors for DI.
- Nullable reference types enabled (`<Nullable>enable</Nullable>`).
- Analyzers: Roslynator (code analysis + formatting).
- XML doc comments on public API surfaces (domain services, app services, repository interfaces).
- Tab indentation throughout.
