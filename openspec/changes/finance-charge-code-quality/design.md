## Context

The `finance-student-charge-management` change delivered a working charge management feature (CRUD, approve/reject, maker-checker, FluentValidation) but the resulting code does not meet the structural standards set by the HR module.

Current problems in `ChargeAppService` and `ChargeManager`:

| Problem | Impact |
|---|---|
| Old-style constructor with `_privateField` assignment (11 fields) | Verbose boilerplate; inconsistent with primary-constructor style used everywhere else |
| Inline DTO mapping: 30-line `new ChargeListItem {…}` and 50-line `new ChargeDetailData {…}` | Unmaintainable; any field rename requires touching 3+ places |
| Duplicate 8-table join in `BuildChargeResponseAsync` and `GetByIdAsync` | Two separate multi-table queries for the same projection; a bug fix in one misses the other |
| `GetAllAsync` is 110 lines — query building, filtering, pagination, mapping all inline | Can't read the method at a glance; difficult to unit-test individual parts |
| No `ILogger` anywhere in the charge stack | No audit trail; no observability on create/approve/reject |
| Generic `IRepository<Charge, Guid>` in both AppService and Manager | Queries needing joins stay in the application layer instead of the domain-aware repo |
| No AutoMapper profile for Finance | Other modules (HR, Students) centralise mapping in `*MapperProfile`; Finance has none |
| File uses space-indented code (violates tab standard) | Roslynator formatting failure; CI will flag it |

---

## Goals / Non-Goals

**Goals:**
- Match HR module conventions: primary constructors, `ILogger`, `MapToDto` static helpers, named helpers (`GetChargeOrThrowAsync`), AutoMapper profile
- Eliminate duplicated 8-table join — one private async helper owns the projection
- Decompose `GetAllAsync` into named private methods for readability and testability
- Add `IChargeRepository` custom domain interface (mirrors `IEmployeeRepository`)
- Add `EfCoreChargeRepository` implementation
- Add `FinanceMapperProfile` — simple field-level maps for charge ↔ DTO where applicable
- Fix indentation to tabs throughout charge files
- Add `ILogger` to `ChargeAppService` and `ChargeManager`
- Zero behaviour changes — no API surface, no migration, no DTO changes

**Non-Goals:**
- Refactoring `FeeCollectionAppService` or other Finance services
- Adding new endpoints or changing DTO shapes
- Performance tuning beyond eliminating the duplicated join

---

## Decisions

### D1 — Primary constructor for `ChargeAppService`

**Decision**: Convert to primary constructor.

```csharp
public class ChargeAppService(
    IChargeRepository chargeRepository,
    IStudentRepository studentRepository,
    IRepository<ChargeCategory, Guid> chargeCategoryRepository,
    // ...
    ILogger<ChargeAppService> logger
) : ApplicationService, IChargeAppService
```

**Rationale**: Matches `EmployeePersonalInfoAppService`, `LeaveManagementAppService`, and all newly written services. C# 12 primary constructors eliminate the constructor body boilerplate; ABP's `ApplicationService` base provides `ObjectMapper`, `CurrentTenant`, `AsyncExecuter` as protected members so no DI needed for those.

**Alternative rejected**: Keep old style — inconsistent; rejected because this PR is purely a quality improvement change.

---

### D2 — `IChargeRepository` custom interface

**Decision**: Add `IChargeRepository : IRepository<Charge, Guid>` in Domain with two methods:

```csharp
Task<ChargeJoinResult?> GetChargeWithDetailsAsync(Guid chargeId, CancellationToken ct = default);
Task<(List<ChargeJoinResult> Items, int TotalCount)> GetChargesPagedAsync(ChargePagedQuery query, CancellationToken ct = default);
```

**Rationale**: The 8-table join currently lives in `ChargeAppService` — a violation of DDD (queries should be in repositories, not application services). Moving to `IChargeRepository` allows `EfCoreChargeRepository` to own the SQL, the AppService becomes a thin coordinator, and the query can be unit-tested via the repo.

**Alternative rejected**: Keep inline `GetQueryableAsync()` joins in app service — fast but couples query knowledge to app layer; HR precedent shows domain-specific repo methods are the right pattern.

---

### D3 — Single `ChargeJoinResult` projection type

**Decision**: Define an internal `record ChargeJoinResult` in the EntityFrameworkCore project as the raw output of the join. The AppService maps this to DTOs.

```csharp
internal record ChargeJoinResult(
    Charge Charge,
    Student Student,
    ChargeCategory Category,
    Standard Standard,
    Division Division,
    EducationBoard Board,
    EducationMedium Medium,
    AcademicYear AcademicYear,
    IdentityUser Requestor
);
```

**Rationale**: The two join results (`GetByIdAsync` and `BuildChargeResponseAsync`) produce structurally identical data. A shared return type removes the duplication and makes the mapping helpers type-safe.

---

### D4 — AutoMapper profile for simple maps; static helpers for complex projection

**Decision**: Add `FinanceMapperProfile` for `ChargeCategory` → `ChargeCategoryDto` style maps. For the complex `ChargeJoinResult` → `ChargeData` / `ChargeDetailData` / `ChargeListItem` projections, use **private static `MapTo*` methods** (not AutoMapper) because:
- These projections assemble from multiple joined entities (not a 1-to-1 source type)
- Static methods are readable, debuggable, and avoid `AfterMap` callback complexity
- Mirrors how HR uses `MapToDto(Employee)` for flat entities and `MapFamilyMemberToContactDetailsDto(FamilyMember)` for nested objects

---

### D5 — `GetAllAsync` decomposition

Break 110-line method into:

```csharp
// Entry point
public async Task<ChargeListResponse> GetAllAsync(Guid academicYearId, GetChargesRequest input)

// Delegates to:
private static ChargeListQuery BuildFilter(Guid academicYearId, GetChargesRequest input)
private async Task<StatusCounts> GetStatusCountsAsync(IQueryable<ChargeJoinResult> baseQuery)
private static ChargeListItem MapToListItem(ChargeJoinResult r)
private static IQueryable<ChargeJoinResult> ApplySorting(IQueryable<ChargeJoinResult> q, string? sortBy, string? sortOrder)
```

---

### D6 — `ILogger` structured messages

Follow the HR pattern:
- `[ChargeAppService] Creating charge: StudentId={0}, CategoryId={1}, Amount={2}`
- `[ChargeAppService] Charge approved: ChargeId={0}, ApprovedBy={1}`
- `[ChargeManager] Creating charge: StudentId={0}, CategoryId={1}`

---

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| `IChargeRepository` methods return `ChargeJoinResult` with `IdentityUser` — crosses module boundary | `IdentityUser` is already referenced in Domain/Application layers across the codebase (HR does the same); acceptable |
| Moving the join into `EfCoreChargeRepository` makes it EF-specific | Repo implementations are always EF-specific; the interface stays clean |
| `ChargeManager` still uses generic `IRepository<Student, Guid>` | Out of scope for this change; student lookup in manager is acceptable until `IStudentRepository` is extended with a `FindOrThrowAsync` method |
| `FinanceMapperProfile` may not be needed immediately if static helpers cover all cases | Add it anyway for consistency; register empty maps that future developers can extend |

---

## Migration Plan

No database migration. No API changes. Purely internal code structure.

Deploy steps: build → test → merge. Rollback: revert commit.

## Open Questions

- Should `ChargeJoinResult` be an `internal record` in EfCore project, or a DTO in Application.Contracts? **Decision: internal record in EfCore** — it is an implementation detail of the query, not a contract.
- Should `EfCoreChargeRepository.GetChargesPagedAsync` handle sorting? **Yes** — mirrors how `EfCoreEmployeeRepository` applies `OrderBy` inside the repo method.
