## Why

The Charge Management APIs (7 endpoints) have zero automated test coverage. Every business rule — maker-checker enforcement, auto-approval, status guards, pagination caps — is unverified, making refactors and deployments risky.

## What Changes

- New test helper class `ChargeTestHelper` with factory methods for all charge prerequisites
- New abstract base class `ChargeApiTestsBase` with shared setup and convenience helpers
- 3 new test files covering all 7 charge endpoints across Critical / Important / Extended tiers
- `CreateClientAs(Guid userId)` helper added to `ShikshaMitraApiTestBase` to support maker-checker testing via `X-Test-UserId` header override

## Capabilities

### New Capabilities

- `charge-core-api-tests`: Happy-path CRUD tests (create, get-by-id, get-all) at Critical tier
- `charge-workflow-api-tests`: Approve, reject, update, delete, filter, and pagination tests at Important tier
- `charge-validation-api-tests`: All error paths including maker-checker, inactive category, state guards, and boundary values at Extended tier

### Modified Capabilities

- `test-base-infra`: `ShikshaMitraApiTestBase` gains `CreateClientAs(Guid userId)` helper using the existing `X-Test-UserId` header mechanism

## Impact

- **New files**: `test/ShikshaMitra.HttpApi.Tests/Finance/Charges/ChargeApiTestsBase.cs`, `ChargeTestHelper.cs`, `ChargeCoreApi_Tests.cs`, `ChargeWorkflowApi_Tests.cs`, `ChargeValidationApi_Tests.cs`
- **Modified**: `test/ShikshaMitra.HttpApi.Tests/ShikshaMitraApiTestBase.cs` — one new protected method
- **No production code changes**
- **API endpoints covered**: `POST /api/app/charges`, `GET /api/app/charges`, `GET /api/app/charges/{id}`, `PUT /api/app/charges/{id}`, `POST /api/app/charges/{id}/approve`, `POST /api/app/charges/{id}/reject`, `DELETE /api/app/charges/{id}`
