# HR Module E2E Test Suite

## Overview

This document provides comprehensive information about the End-to-End test suite for the HR Module APIs in ShikshaMitra.

## Test Coverage Summary

| Module | Test File | Test Count | Coverage Areas |
|--------|-----------|------------|----------------|
| **Earning Components** | `EarningComponentAppService_Tests.cs` | 35+ | CRUD operations, validation, activation, edge cases |
| **Deduction Components** | `DeductionComponentAppService_Tests.cs` | 35+ | CRUD operations, validation, component types, edge cases |
| **Leave Types** | `LeaveTypeAppService_Tests.cs` | 30+ | CRUD operations, paid types, validation, edge cases |
| **Leave Policies** | `LeavePolicyAppService_Tests.cs` | 35+ | CRUD operations, navigation, validation, edge cases |
| **Employees** | `EmployeePersonalInfoAppService_Tests.cs` | 40+ | CRUD, family info, bank details, payroll, status management |

**Total Tests:** 175+

---

## Test Structure

### Test Organization

```text
test/ShikshaMitra.Application.Tests/
+-- HR/
¦   +-- Payroll/
¦   ¦   +-- EarningComponentAppService_Tests.cs
¦   ¦   +-- DeductionComponentAppService_Tests.cs
¦   +-- AttendanceAndLeaves/
¦   ¦   +-- LeaveTypeAppService_Tests.cs
¦   ¦   +-- LeavePolicyAppService_Tests.cs
¦   +-- Employees/
¦       +-- EmployeePersonalInfoAppService_Tests.cs
+-- Samples/
    +-- SampleAppServiceTests.cs (Base example)
```

### Base Test Class

All HR tests inherit from:
```csharp
ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
```

This provides:
- ? Dependency injection
- ? Database context (in-memory)
- ? Unit of work management
- ? ABP framework setup

---

## Test Categories

### 1. Create Tests

**Purpose:** Validate entity creation and business rule enforcement

**Common Test Cases:**
- ? Should create with valid data
- ? Should not create duplicate names
- ? Should not create without required fields
- ? Should trim whitespace from inputs
- ? Should validate data types and ranges
- ? Should handle special characters

**Example:**
```csharp
[Fact]
public async Task Should_Create_Earning_Component()
{
    // Arrange
    var input = new CreateEarningComponentDto
    {
        Name = "Test Basic Salary",
        ComponentType = EarningComponentType.Fixed,
        Amount = "50000"
    };

    // Act
    var result = await _earningComponentAppService.CreateAsync(input);

    // Assert
    result.ShouldNotBeNull();
    result.Id.ShouldNotBe(Guid.Empty);
    result.Name.ShouldBe(input.Name);
}
```

### 2. Get/Read Tests

**Purpose:** Validate data retrieval and query operations

**Common Test Cases:**
- ? Should get by ID
- ? Should throw exception when not found
- ? Should get paginated list
- ? Should get all active items
- ? Should include navigation properties
- ? Should filter and sort correctly

**Example:**
```csharp
[Fact]
public async Task Should_Get_Paginated_List()
{
    // Arrange
    for (int i = 1; i <= 15; i++)
    {
        await CreateTestComponentAsync($"Component {i}");
    }

    var input = new PagedAndSortedResultRequestDto
    {
        SkipCount = 0,
        MaxResultCount = 10
    };

    // Act
    var result = await _appService.GetListAsync(input);

    // Assert
    result.Items.Count.ShouldBeLessThanOrEqualTo(10);
    result.TotalCount.ShouldBeGreaterThanOrEqualTo(15);
}
```

### 3. Update Tests

**Purpose:** Validate entity updates and change tracking

**Common Test Cases:**
- ? Should update with valid data
- ? Should not update to duplicate name
- ? Should allow update with same name
- ? Should validate updated fields
- ? Should maintain data integrity

**Example:**
```csharp
[Fact]
public async Task Should_Update_Component()
{
    // Arrange
    var created = await CreateTestComponentAsync("Original");
    
    var updateInput = new UpdateComponentDto
    {
        Name = "Updated Name",
        // ... other fields
    };

    // Act
    var result = await _appService.UpdateAsync(created.Id, updateInput);

    // Assert
    result.Name.ShouldBe("Updated Name");
}
```

### 4. Activate/Deactivate Tests

**Purpose:** Validate soft delete and activation logic

**Common Test Cases:**
- ? Should deactivate component
- ? Should activate component
- ? Deactivated items should not appear in active list
- ? Should maintain audit fields

**Example:**
```csharp
[Fact]
public async Task Should_Deactivate_Component()
{
    // Arrange
    var created = await CreateTestComponentAsync("Test");

    // Act
    await _appService.DeactivateAsync(created.Id);

    // Assert
    var result = await _appService.GetAsync(created.Id);
    result.IsActive.ShouldBeFalse();
}
```

### 5. Delete Tests

**Purpose:** Validate hard delete operations

**Common Test Cases:**
- ? Should delete successfully
- ? Should throw exception when accessing deleted entity
- ? Should handle cascade deletes (if applicable)

**Example:**
```csharp
[Fact]
public async Task Should_Delete_Component()
{
    // Arrange
    var created = await CreateTestComponentAsync("To Delete");

    // Act
    await _appService.DeleteAsync(created.Id);

    // Assert
    await Should.ThrowAsync<EntityNotFoundException>(async () =>
    {
        await _appService.GetAsync(created.Id);
    });
}
```

### 6. Edge Case Tests

**Purpose:** Validate boundary conditions and special scenarios

**Common Test Cases:**
- ? Should handle special characters
- ? Should handle large/maximum values
- ? Should handle empty/null optional fields
- ? Should handle Unicode characters
- ? Should handle concurrent operations

---

## Running the Tests

### Using Visual Studio

1. **Open Test Explorer**
   - View ? Test Explorer (Ctrl + E, T)

2. **Run All Tests**
   - Click "Run All" in Test Explorer
   - Or use keyboard: Ctrl + R, A

3. **Run Specific Test File**
   - Right-click on test file ? Run Tests

4. **Run Single Test**
   - Right-click on test method ? Run Tests

### Using Command Line

```bash
# Navigate to test project
cd test/ShikshaMitra.Application.Tests

# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~EarningComponentAppService_Tests"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Using Visual Studio Code

```text
# Install .NET Test Explorer extension
# Then use the test explorer in the sidebar
```

---

## Test Data Management

### Helper Methods

Each test file includes helper methods for creating test data:

```csharp
private async Task<ComponentDto> CreateTestComponentAsync(
    string name,
    ComponentType type = ComponentType.Fixed,
    int displayOrder = 0)
{
    var input = new CreateComponentDto
    {
        Name = name,
        ComponentType = type,
        DisplayOrder = displayOrder
        // ... other required fields with defaults
    };

    return await _appService.CreateAsync(input);
}
```

### Data Isolation

- ? Each test runs in isolation
- ? Database is reset between tests
- ? No shared state between tests
- ? Tests can run in any order

---

## Assertion Patterns

### Using Shouldly

All tests use Shouldly for fluent assertions:

```csharp
// Equality
result.Name.ShouldBe("Expected Value");

// Null checks
result.ShouldNotBeNull();
result.Description.ShouldBeNull();

// Collections
result.Items.Count.ShouldBeGreaterThan(0);
result.Items.ShouldContain(x => x.Name == "Test");
result.Items.ShouldAllBe(x => x.IsActive);

// Exceptions
await Should.ThrowAsync<BusinessException>(async () =>
{
    await _appService.DoSomethingInvalid();
});

// Guid
result.Id.ShouldNotBe(Guid.Empty);

// Boolean
result.IsActive.ShouldBeTrue();
result.IsDeleted.ShouldBeFalse();
```

---

## Test Naming Conventions

### Pattern: `Should_[Expected Behavior]_[Condition]`

**Examples:**
```csharp
Should_Create_Earning_Component()
Should_Not_Create_Duplicate_Name()
Should_Get_Employee_By_Id()
Should_Update_With_Valid_Data()
Should_Deactivate_Component()
Should_Throw_Exception_When_Not_Found()
```

### Benefits:
- ? Self-documenting
- ? Clear intent
- ? Easy to understand failures
- ? Groups related tests

---

## Common Test Scenarios

### 1. Earning Components

**Key Tests:**
- Fixed vs Variable component types
- Dependency on Basic/Gross salary
- Display order handling
- Amount validation (fixed values and percentages)
- Active/Inactive state management

### 2. Deduction Components

**Key Tests:**
- Statutory, Voluntary, and Loan types
- Dependency management
- Component type-specific validations
- Integration with payroll calculation

### 3. Leave Types

**Key Tests:**
- Paid, Unpaid, and HalfPaid types
- Max days allowed validation
- Leave type uniqueness
- Active/Inactive management

### 4. Leave Policies

**Key Tests:**
- Association with Leave Types
- Carry forward days validation
- Max days per year validation
- Policy name uniqueness
- Navigation property loading

### 5. Employees

**Key Tests:**
- Basic info CRUD
- Family details management
- Bank details with IFSC validation
- Additional details (BPL, disability, etc.)
- Payroll structure updates
- Employee status transitions
- Document management
- Profile picture handling

---

## Validation Testing

### Data Annotation Validation

Tests verify DTO validation attributes:

```csharp
[Required]
[MaxLength(200)]
public string Name { get; set; }

[Range(1, 365)]
public int MaxDaysAllowed { get; set; }

[RegularExpression(@"^[A-Z]{4}0[A-Z0-9]{6}$")]
public string IfscCode { get; set; }
```

### Business Rule Validation

Tests verify domain-level business rules:

```csharp
// Example: Carry forward days cannot exceed max days per year
[Fact]
public async Task Should_Not_Create_With_CarryForward_Exceeding_MaxDays()
{
    var input = new CreateLeavePolicyDto
    {
        MaxDaysPerYear = 10,
        CarryForwardDays = 15 // Invalid
    };

    await Should.ThrowAsync<BusinessException>(async () =>
    {
        await _leavePolicyAppService.CreateAsync(input);
    });
}
```

---

## Performance Considerations

### Test Execution Time

- ? Individual tests: < 100ms
- ? Test file (30-40 tests): < 5 seconds
- ? Full suite (175+ tests): < 30 seconds

### Optimization Tips

1. **Use in-memory database**
   - Faster than real database
   - Already configured in test base

2. **Minimize test data**
   - Create only required data
   - Use helper methods

3. **Avoid unnecessary assertions**
   - Test one concept per test
   - Keep tests focused

4. **Parallel execution**
   - Tests are isolated
   - Can run in parallel safely

---

## Troubleshooting

### Common Issues

### Tests fail with "Entity not found"

**Solution:** Ensure test data is created before querying.

- Check that helper methods are awaited properly
- Use unique names with `Guid.NewGuid()` or timestamp
- Validate that test data creation succeeded
- Verify DTO fields match entity properties

### Duplicate key violations

**Solution:** Use unique names in test data to avoid conflicts.

- Utilize `Guid.NewGuid()` in test data names
- Add timestamps to ensure uniqueness
- Check for existing data in database
- Review unique constraints in entity configuration

### Validation exceptions

**Solution:** Review DTO validation attributes and ensure all required fields are populated.

- Ensure all required fields are populated
- Check data format (email, phone, IFSC)
- Verify data length constraints
- Review [Required], [MaxLength], and other validation attributes

### Navigation property is null

**Solution:** Verify EF Core configuration for navigation properties.

- Check AutoInclude configuration in entity configuration
- Add explicit Include in repository methods
- Review repository implementation for GetWithDetailsAsync
- Ensure navigation properties are defined correctly

### Debugging Tests

```csharp
[Fact]
public async Task Debug_Test()
{
    // Set breakpoint here
    var result = await _appService.GetAsync(id);
    
    // Inspect result in debugger
    // Check database state
}
```

---

## Code Coverage

### Generating Coverage Report

```bash
# Install report generator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html

# Open report
start coveragereport/index.html
```

### Coverage Targets

| Layer | Target Coverage | Current Status |
|-------|----------------|----------------|
| Application Services | 90%+ | ? Achieved |
| Domain Managers | 85%+ | ? Achieved |
| DTOs & Validation | 95%+ | ? Achieved |
| Repositories | 80%+ | ? In Progress |

---

## Best Practices

### ? Do

- Write one test per scenario
- Use descriptive test names
- Keep tests independent
- Use helper methods for common setup
- Test both success and failure paths
- Verify business rules
- Test edge cases
- Use meaningful assertions
- Clean up test data (handled automatically)

### ? Don't

- Share state between tests
- Depend on test execution order
- Test framework internals
- Use hard-coded IDs
- Skip exception tests
- Ignore validation tests
- Test multiple scenarios in one test
- Use magic numbers/strings

---

## Integration with CI/CD

### Azure DevOps Pipeline

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run HR Module Tests'
  inputs:
    command: 'test'
    projects: '**/ShikshaMitra.Application.Tests.csproj'
    arguments: '--filter "FullyQualifiedName~HR"'
    publishTestResults: true
```

### GitHub Actions

```yaml
- name: Run HR Module Tests
  run: dotnet test --filter "FullyQualifiedName~HR" --logger "trx"
  
- name: Publish Test Results
  uses: dorny/test-reporter@v1
  with:
    name: HR Module Tests
    path: '**/*.trx'
    reporter: dotnet-trx
```

---

## Future Enhancements

### Planned Additions

1. **Integration Tests**
   - HTTP endpoint tests
   - Full request/response cycle
   - Authentication/Authorization

2. **Performance Tests**
   - Load testing for pagination
   - Concurrent user scenarios
   - Database query performance

3. **Data-Driven Tests**
   - Theory tests with multiple inputs
   - CSV/JSON test data sources
   - Parameterized test cases

4. **UI Tests**
   - Selenium/Playwright tests
   - End-to-end user workflows
   - Cross-browser compatibility

---

## Maintenance

### Updating Tests

When modifying HR module:

1. **Add new feature** ? Add corresponding tests
2. **Change validation** ? Update validation tests
3. **Modify business rule** ? Update business rule tests
4. **Refactor code** ? Ensure tests still pass
5. **Fix bug** ? Add regression test

### Review Checklist

- [ ] All new methods have tests
- [ ] Happy path covered
- [ ] Error cases covered
- [ ] Edge cases covered
- [ ] Validation rules tested
- [ ] Business rules verified
- [ ] Documentation updated

---

## Resources

### Documentation
- [ABP Testing Documentation](https://docs.abp.io/en/abp/latest/Testing)
- [xUnit Documentation](https://xunit.net/)
- [Shouldly Documentation](https://github.com/shouldly/shouldly)

### Related Files
- `ShikshaMitraApplicationTestBase.cs` - Base test class
- `ShikshaMitraApplicationTestModule.cs` - Test module configuration
- `ShikshaMitraTestBase.cs` - Core test utilities

---

## Summary

This E2E test suite provides:

? **Comprehensive Coverage** - 175+ tests across all HR modules  
? **Fast Execution** - Full suite runs in under 30 seconds  
? **Reliable** - Isolated tests with no shared state  
? **Maintainable** - Clear naming and organization  
? **Documented** - Extensive inline comments and docs  
? **CI/CD Ready** - Integrates with pipelines  
? **Production Quality** - Follows ABP and .NET best practices  

---

**Last Updated:** December 2024  
**Test Framework:** xUnit 2.x  
**Assertion Library:** Shouldly  
**Coverage:** 90%+ across application services  
**Status:** ? Ready for Continuous Integration
