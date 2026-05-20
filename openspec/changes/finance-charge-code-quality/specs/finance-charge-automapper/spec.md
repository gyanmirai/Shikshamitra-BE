## ADDED Requirements

### Requirement: Finance mapper profile exists
The application layer SHALL contain a `FinanceMapperProfile : Profile` class that registers AutoMapper mappings for all Finance DTO types, following the same pattern as `HRMapperProfile`.

#### Scenario: ChargeCategory maps to DTO
- **WHEN** `ObjectMapper.Map<ChargeCategory, ChargeCategoryDto>()` is called
- **THEN** the result SHALL have all fields populated without manual property assignment

### Requirement: ChargeAppService uses ObjectMapper for flat entity maps
The `ChargeAppService` SHALL use `ObjectMapper.Map<>()` for flat entity-to-DTO conversions registered in `FinanceMapperProfile`, and SHALL use private static `MapTo*` methods for multi-join projections.

#### Scenario: Flat entity mapping via ObjectMapper
- **WHEN** a `ChargeCategory` entity is mapped to a DTO inside `ChargeAppService`
- **THEN** `ObjectMapper.Map<ChargeCategory, ChargeCategoryDto>()` SHALL be used instead of inline `new ChargeCategoryDto { … }`

### Requirement: Static MapToListItem helper exists
The `ChargeAppService` SHALL contain a `private static ChargeListItem MapToListItem(ChargeJoinResult r)` method that maps a join result to a list DTO.

#### Scenario: List mapping uses named helper
- **WHEN** `GetAllAsync` builds the response list
- **THEN** it SHALL call `MapToListItem(r)` per item instead of an inline anonymous initialiser

### Requirement: Static MapToDetailData helper exists
The `ChargeAppService` SHALL contain a `private static ChargeDetailData MapToDetailData(ChargeJoinResult r, IdentityUser? approver)` method.

#### Scenario: Detail mapping uses named helper
- **WHEN** `GetByIdAsync` or `CreateAsync` assembles the detail response
- **THEN** it SHALL delegate to `MapToDetailData` instead of a 50-line inline block
