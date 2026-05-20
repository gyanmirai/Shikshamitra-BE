## ADDED Requirements

### Requirement: IChargeRepository interface exists
The Domain layer SHALL contain `IChargeRepository : IRepository<Charge, Guid>` in `ShikshaMitra.Finance.Fees.Charges` namespace with domain-specific query methods, following the `IEmployeeRepository` pattern.

#### Scenario: Interface is in Domain layer
- **WHEN** code in `ChargeAppService` or `ChargeManager` references `IChargeRepository`
- **THEN** the reference SHALL resolve to `ShikshaMitra.Domain` without a circular dependency

### Requirement: IChargeRepository.GetChargeWithDetailsAsync
`IChargeRepository` SHALL expose `Task<ChargeJoinResult?> GetChargeWithDetailsAsync(Guid chargeId, CancellationToken ct = default)` that returns a single charge with all related entities joined.

#### Scenario: Returns charge with details
- **WHEN** `GetChargeWithDetailsAsync` is called with a valid charge ID
- **THEN** the returned `ChargeJoinResult` SHALL contain Charge, Student, Category, Standard, Division, Board, Medium, AcademicYear, and Requestor

#### Scenario: Returns null for unknown ID
- **WHEN** `GetChargeWithDetailsAsync` is called with a non-existent charge ID
- **THEN** it SHALL return `null`

### Requirement: EfCoreChargeRepository implements IChargeRepository
The EntityFrameworkCore layer SHALL contain `EfCoreChargeRepository : EfCoreRepository<ShikshaMitraDbContext, Charge, Guid>, IChargeRepository` that provides the actual EF Core join queries.

#### Scenario: Join query runs as single SQL statement
- **WHEN** `GetChargeWithDetailsAsync` is executed
- **THEN** a single SQL JOIN query SHALL be issued (not N+1 individual lookups)

### Requirement: ChargeAppService injects IChargeRepository
The `ChargeAppService` SHALL inject `IChargeRepository` instead of generic `IRepository<Charge, Guid>`.

#### Scenario: AppService uses custom repo
- **WHEN** `ChargeAppService` is constructed
- **THEN** its primary constructor SHALL declare `IChargeRepository chargeRepository` (not `IRepository<Charge, Guid>`)
