# API Testing Guide

Central reference for the ShikshaMitra API test framework. Module-specific guides live under `docs/modules/<module>/testing/`.

## Architecture

The API testing framework uses a **hybrid** approach:

| Suite | Engine | Auth | Purpose | When to Run |
|-------|--------|------|---------|-------------|
| **Tier 0 (Critical)** | In-process `WebApplicationFactory` | Test auth handler | Business-critical CRUD, response contracts | Every PR |
| **Tier 1 (Important)** | In-process `WebApplicationFactory` | Test auth handler | Edge cases, search, validation | PR + main branch |
| **Tier 2 (Extended)** | In-process `WebApplicationFactory` | Test auth handler | Bulk data, full lifecycle, destructive | Nightly / manual |
| **Smoke** | Real HTTP against running host | OpenIddict password grant | Auth pipeline, network, deployment health | Post-deploy / manual |

```
test/ShikshaMitra.HttpApi.Tests/
├── Infrastructure/
│   ├── Traits/          # TestTier, TestModule attributes + discoverers
│   ├── Auth/            # TestAuthHandler, token providers, permission bypass
│   ├── Data/            # TestDataHelper, ITestDataCleanup
│   └── Extensions/      # HttpClient helpers
├── HR/                  # HR module API tests (blueprint)
│   ├── Payroll/
│   └── Employees/
├── Smoke/               # Hosted smoke tests
│   └── HR/
├── ShikshaMitraApiTestFactory.cs   # WebApplicationFactory configuration
├── ShikshaMitraApiTestBase.cs      # Base class for in-process tests
├── ShikshaMitraSmokeTestBase.cs    # Base class for hosted smoke tests
└── appsettings.json                # Test configuration
```

## Running Tests

### All non-smoke tests (fast, in-process)

```bash
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier!=Smoke"
```

### By tier

```bash
# Critical only (PR gate)
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Critical"

# Important only
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Important"

# Extended only (slow, data-heavy)
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Extended"

# Smoke (requires running host)
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Smoke"
```

> Smoke tests are opt-in by default. Set `APITEST_ENABLE_SMOKE=true` before running Tier=Smoke.

### By module

```bash
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier=Critical"
```

### Combined filters

```bash
# Critical HR tests only
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier=Critical"

# Everything except smoke and extended
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier!=Smoke&Tier!=Extended"
```

## How to Classify Tests

| Tier | Criteria | Runtime Budget | Examples |
|------|----------|---------------|----------|
| **Critical** | Breaks core business flow if failing | < 30s total | CRUD happy path, response shape, auth gate |
| **Important** | High-value but non-blocking | < 2 min total | Search, 404 handling, validation rules |
| **Extended** | Broad coverage, expensive setup | < 10 min total | Bulk pagination, full lifecycle, data cleanup |
| **Smoke** | Validates deployment health | < 1 min total | List endpoint returns 200, auth token works |

## Writing a New API Test

### 1. Create the test class

```csharp
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class MyEndpointApi_Tests : ShikshaMitraApiTestBase
{
    public MyEndpointApi_Tests(ShikshaMitraApiTestFactory factory)
        : base(factory) { }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Created_On_Valid_Input()
    {
        var input = new MyCreateRequestDto
        {
            Name = TestDataHelper.UniqueName("MyEntity"),
            // ...
        };

        var response = await Client.PostAsJsonAsync("/api/my-endpoint", input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
```

### 2. Use correct API contract values

Contract mappers in `src/ShikshaMitra.HttpApi/Mappers/` parse string enum values. Always use **uppercase** values that match the mapper's expected inputs (e.g., `"FIXED"`, `"AMOUNT"`, `"PERCENTAGE"`).

### 3. Generate unique test data

```csharp
TestDataHelper.UniqueName("BasicSalary")  // "Test_BasicSalary_a3f7e2b1"
TestDataHelper.UniqueEmail("employee")     // "employee_a3f7e2@test.shikshamitra.local"
```

### 4. Apply tier and module traits

Every test method must have `[TestTier(...)]`. The class should have `[TestModule(...)]`.

## Authentication

### In-process tests

The `ShikshaMitraApiTestFactory` configures:
- `TestAuthHandler`: authenticates all requests as the admin test user
- `AllowAllAuthorizationHandler`: bypasses ASP.NET Core authorization policies
- `AlwaysGrantPermissionChecker`: bypasses ABP permission checks

This means in-process tests focus on **endpoint contracts and behavior**, not auth rules. Auth behavior is verified in smoke tests.

### Smoke tests

Smoke tests use `PasswordGrantTokenProvider` to obtain real OpenIddict tokens. Configure via environment variables or `appsettings.json`:

| Variable | Purpose |
|----------|---------|
| `APITEST_BASE_URL` | Target host URL |
| `APITEST_AUTHORITY` | OpenIddict authority URL |
| `APITEST_USERNAME` | Test user login |
| `APITEST_PASSWORD` | Test user password |
| `APITEST_CLIENT_ID` | OAuth client ID |
| `APITEST_CLIENT_SECRET` | OAuth client secret (for client credentials) |
| `APITEST_ENABLE_SMOKE` | Set to `true` (or `1`) to enable smoke tests |

## Data Conventions

- Use `TestDataHelper.UniqueName()` for all entity names to prevent collisions
- Each test creates its own data; do not rely on seed data for API tests
- Destructive tests (Tier 2) should clean up after themselves using `ITestDataCleanup`
- In-process tests use an isolated in-memory SQLite database per test run

## Adding a New Module

1. Create folder: `test/ShikshaMitra.HttpApi.Tests/{ModuleName}/`
2. Add test classes inheriting `ShikshaMitraApiTestBase`
3. Apply `[TestModule(TestTraits.Modules.{ModuleName})]`
4. Add the module constant to `TestTraits.Modules` if not already present
5. Add smoke tests under `Smoke/{ModuleName}/`
6. Add module-specific docs under `docs/modules/{module}/testing/`

## Related

- [HR API testing guide](../../modules/hr/testing/api-testing.md)
- [HR E2E tests (app-service level)](../../modules/hr/testing/HR-Module-E2E-Tests.md)
- [Testing overview](./README.md)
