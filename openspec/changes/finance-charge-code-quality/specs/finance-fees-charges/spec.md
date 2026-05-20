## MODIFIED Requirements

### Requirement: ChargeAppService uses primary constructor DI
The `ChargeAppService` SHALL use a C# primary constructor for dependency injection, matching the style used by `EmployeePersonalInfoAppService`, instead of a constructor body with private field assignments.

#### Scenario: Primary constructor declaration
- **WHEN** a developer reads `ChargeAppService.cs`
- **THEN** the class declaration SHALL begin with `public class ChargeAppService(` and all dependencies SHALL be declared in the constructor parameter list
- **THEN** there SHALL be no `private readonly` field declarations or constructor body assignment

### Requirement: ChargeAppService has structured logging
The `ChargeAppService` SHALL inject `ILogger<ChargeAppService>` and emit structured log messages at key lifecycle points.

#### Scenario: Log on charge creation
- **WHEN** `CreateAsync` is called and succeeds
- **THEN** a `LogDebug` message SHALL be emitted with `ChargeId`, `StudentId`, and `Status`

#### Scenario: Log on charge approval
- **WHEN** `ApproveAsync` is called and succeeds
- **THEN** a `LogInformation` message SHALL be emitted with `ChargeId` and `ApprovedBy`

#### Scenario: Log on charge rejection
- **WHEN** `RejectAsync` is called and succeeds
- **THEN** a `LogInformation` message SHALL be emitted with `ChargeId` and `RejectedBy`

### Requirement: ChargeManager has structured logging
The `ChargeManager` SHALL inject `ILogger<ChargeManager>` and emit structured log messages on charge creation, following the `EmployeeManager` pattern.

#### Scenario: Log on create attempt
- **WHEN** `CreateAsync` is called
- **THEN** a `LogDebug` message SHALL be emitted at the start with `StudentId`, `CategoryId`, and `Amount`

### Requirement: GetAllAsync is decomposed into named private methods
The `GetAllAsync` method SHALL be no longer than 40 lines. It SHALL delegate to private helper methods with descriptive names for query building, filter application, count retrieval, and list mapping.

#### Scenario: Private method exists for filter application
- **WHEN** a developer navigates `GetAllAsync`
- **THEN** filter logic SHALL be in a separate `private static` or `private` method named clearly (e.g., `ApplyFilters`)

#### Scenario: Private method exists for list item mapping
- **WHEN** the list items are assembled
- **THEN** a `private static ChargeListItem MapToListItem(...)` method SHALL be called instead of an inline initialiser

### Requirement: Duplicate join logic is eliminated
The duplicate 8-table join that appears in both `BuildChargeResponseAsync` and `GetByIdAsync` SHALL be replaced with a single private async method (or delegated to `IChargeRepository.GetChargeWithDetailsAsync`).

#### Scenario: Single join helper used by both create and get-by-id
- **WHEN** `CreateAsync` or `GetByIdAsync` needs charge detail data
- **THEN** both SHALL use the same code path (either a shared private method or the repository method)
- **THEN** there SHALL be no copy-pasted join query in two different methods

### Requirement: File indentation uses tabs
All charge-related files (`ChargeAppService.cs`, `ChargeManager.cs`, `IChargeRepository.cs`, `EfCoreChargeRepository.cs`, `FinanceMapperProfile.cs`) SHALL use tab indentation consistent with the rest of the codebase.

#### Scenario: Roslynator formatting passes
- **WHEN** the solution is built
- **THEN** Roslynator SHALL report no indentation warnings on charge files
