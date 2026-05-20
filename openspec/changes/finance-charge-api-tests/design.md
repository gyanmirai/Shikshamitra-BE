## Context

The Finance module has two test suites today (`ConcessionRequest*`, `FeeCollection*`) but zero coverage for Charge Management. The charge domain has non-trivial business rules (maker-checker, auto-approval, per-status state guards) that are invisible to CI without API tests.

The test infrastructure (`TestAuthHandler`, `ShikshaMitraApiTestFactory`, `ShikshaMitraApiTestBase`) is already mature. The `X-Test-UserId` header mechanism in `TestAuthHandler` provides a clean way to simulate different users in the same test — critical for maker-checker scenarios.

## Goals / Non-Goals

**Goals:**
- Cover all 7 charge endpoints with at least one test at the correct tier
- Verify maker-checker enforcement without mocking or DB manipulation
- Follow the same patterns as `ConcessionRequest*` tests for consistency
- Add `CreateClientAs(Guid userId)` to base class so any future finance test can reuse it

**Non-Goals:**
- Performance benchmarking or load testing
- Testing authentication/authorization framework (tested elsewhere)
- Covering `ChargeCategory` CRUD (separate settings concern)

## Decisions

**D1 — Maker-checker via header, not a second seeded user**  
`TestAuthHandler` already reads `X-Test-UserId` header. We use `CreateClientAs(Guid.NewGuid())` to create a second "approver" client inline in the test. This avoids factory changes and is cleaner than inserting a user into the DB.  
*Alternative*: Seed a `SeededApproverUserId` in `ShikshaMitraApiTestFactory` — rejected because it tightly couples the factory to one module's edge case.

**D2 — Three test files, not one monolith**  
`ChargeCoreApi_Tests` (Critical), `ChargeWorkflowApi_Tests` (Important), `ChargeValidationApi_Tests` (Extended). Mirrors ConcessionRequest structure. Allows `dotnet test --filter "Tier=Critical"` to run only the fast gate.

**D3 — `ChargeTestHelper` is a static class with `static Guid` fields**  
Same pattern as `ConcessionRequestTestHelper`. The base ctor seeds defaults from factory. All helper methods accept `HttpClient` as first arg so tests can use either the default client or a scoped approver client.

**D4 — `CreateChargeCategoryAsync` creates with `IsApprovalRequired = true` by default**  
Most tests need a pending charge to test approval flow. Tests that need auto-approval pass `isApprovalRequired: false` explicitly.

**D5 — `CreateClientAs` added to `ShikshaMitraApiTestBase` (not the Finance base class)**  
The mechanism is generic and could be used by HR or Student tests too. Belongs in the shared base.

## Risks / Trade-offs

- **SQLite in-memory share**: All tests share one SQLite instance via `ApiTestCollection`. Charge IDs created in one test class persist in others. Tests must use unique data (via `TestDataHelper.UniqueName`) — same constraint as existing tests.
- **Maker-checker `RequestorId` is the ABP current-user id** (`ICurrentUser.Id`): In test, this is set by the `AbpClaimTypes.UserId` claim. If that claim resolution ever changes, tests break. Mitigation: the claim is set in `TestAuthHandler` which is under our control.
- **`ChargeCategory` route is `/api/charge-categories`** (not `/api/app/charge-categories`) — must use exact route in `ChargeTestHelper.CreateChargeCategoryAsync`.

## Migration Plan

No migration needed — tests only, no schema or production code changes (except the one-line base class addition).
