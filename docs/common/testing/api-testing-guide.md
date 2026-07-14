# Test backend APIs

This guide explains the ShikshaMitra API test framework.
Module-specific validation commands live in `docs/modules/<module>/testing.md`.

## Test suite model

Use this table to pick the right API test tier.

| Suite | Engine | Auth | Purpose | Run cadence |
|---|---|---|---|---|
| Tier 0 | In-process `WebApplicationFactory` | Test auth handler | Business-critical CRUD and response contracts | Every pull request |
| Tier 1 | In-process `WebApplicationFactory` | Test auth handler | Search, validation, and edge cases | Pull request and main branch |
| Tier 2 | In-process `WebApplicationFactory` | Test auth handler | Broader lifecycle and data-heavy flows | Nightly or manual |
| Smoke | Real HTTP against running host | OpenIddict password grant | Auth pipeline, network, and deployment health | Post-deploy or manual |

## Test project layout

The HTTP API tests use this structure.

```text
test/ShikshaMitra.HttpApi.Tests/
  Infrastructure/
    Traits/
    Auth/
    Data/
    Extensions/
  HR/
    Payroll/
    Employees/
  Smoke/
    HR/
  ShikshaMitraApiTestFactory.cs
  ShikshaMitraApiTestBase.cs
  ShikshaMitraSmokeTestBase.cs
  appsettings.json
```

## Run tests

Use these commands from the backend repo root.

```bash
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier!=Smoke"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Critical"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Important"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Extended"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Smoke"
```

Smoke tests are opt-in.
Set `APITEST_ENABLE_SMOKE=true` before running `Tier=Smoke`.

## Filter by module

Use module filters when you only need one API surface.

```bash
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR&Tier=Critical"
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier!=Smoke&Tier!=Extended"
```

## Write a new API test

Create new tests under `test/ShikshaMitra.HttpApi.Tests/{ModuleName}/`.
Use `ShikshaMitraApiTestBase` for in-process endpoint tests.

```csharp
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class MyEndpointApi_Tests : ShikshaMitraApiTestBase
{
    public MyEndpointApi_Tests(ShikshaMitraApiTestFactory factory)
        : base(factory)
    {
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Created_On_Valid_Input()
    {
        var response = await Client.PostAsJsonAsync("/api/my-endpoint", input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
```

## Test data conventions

Use unique test data for every write test.
Do not rely on seed data for in-process API tests.

| Need | Helper |
|---|---|
| Unique name | `TestDataHelper.UniqueName("BasicSalary")` |
| Unique email | `TestDataHelper.UniqueEmail("employee")` |
| Cleanup | `ITestDataCleanup` for destructive Tier 2 tests |

## Authentication model

In-process tests use the test auth handler, allow-all authorization handler, and always-grant permission checker.
These tests validate endpoint contracts and behavior, not the real auth pipeline.

Smoke tests use real OpenIddict token flows.
Configure smoke tests with these variables.

| Variable | Purpose |
|---|---|
| `APITEST_BASE_URL` | Target host URL |
| `APITEST_AUTHORITY` | OpenIddict authority URL |
| `APITEST_USERNAME` | Test user login |
| `APITEST_PASSWORD` | Test user password |
| `APITEST_CLIENT_ID` | OAuth client ID |
| `APITEST_CLIENT_SECRET` | OAuth client secret |
| `APITEST_ENABLE_SMOKE` | Enables smoke tests |

## Add a new module

Follow these steps when adding API tests for a module.

1. Create `test/ShikshaMitra.HttpApi.Tests/{ModuleName}/`
2. Add test classes inheriting `ShikshaMitraApiTestBase`
3. Apply `[TestModule(TestTraits.Modules.{ModuleName})]`
4. Add the module constant to `TestTraits.Modules` if needed
5. Add smoke tests under `Smoke/{ModuleName}/` when real-host coverage matters
6. Add module-specific commands under `docs/modules/{module}/testing.md`

## Related docs

- [HR testing](../../modules/hr/testing.md)
- [Testing overview](./README.md)
