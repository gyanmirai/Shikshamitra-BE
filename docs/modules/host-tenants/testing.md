---
module: host-tenants
domain: platform
content_type: Reference
status: canonical
updated_on: 2026-10-08
owner: backend-team
audience: humans-and-agents
---

# Host tenant validation

TL;DR: validate authorization and access revocation with executable API/token tests, then validate the frontend browser workflow.

## Focused checks

```powershell
dotnet build src/ShikshaMitra.HttpApi.Host --nologo --verbosity quiet -clp:ErrorsOnly
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~HostTenantApi_Tests|FullyQualifiedName~HostAdministratorBootstrapper_Tests|FullyQualifiedName~TenantAuthorizationApi_Tests"
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module host-tenants
git diff --check
```

## Coverage

`test/ShikshaMitra.HttpApi.Tests/TenantSettings/HostTenantApi_Tests.cs` exercises genuine/fabricated identity boundaries, exact grants, direct application calls, password validation rollback, native password/refresh grants, audit and warmed-cache revocation.
`test/ShikshaMitra.HttpApi.Tests/TenantSettings/TenantAuthorizationApi_Tests.cs` preserves school own-tenant reads and denies cross-tenant operations.
`test/ShikshaMitra.HttpApi.Tests/TenantSettings/HostAdministratorBootstrapper_Tests.cs` verifies secure missing-account creation, idempotence and preservation of existing passwords, memberships and role grants.

## Local evidence (2026-10-08)

- All nine host API/bootstrap tests passed together on the final source, with no skipped tests (`host-tenants-final.trx`). This covers the six API cases and three bootstrap cases.
- Fifty focused security-permission/role/user seeder regressions passed (`host-security-seeders.trx`). Existing grants, memberships and passwords remain preserved on upgrade.
- The host and migrator build; EF reports no pending model changes.
- PostgreSQL 18 loopback fixture: generated migration Up preserved institute names and activated existing rows; inserts defaulted active. Down preserved the latest institute name and unrelated email details.
- Twelve legacy tenant-authorization tests passed. Two permission-management route tests returned 404 in both this worktree and the original checkout, confirming the existing route failure.
- Focused host-tenant docs validation passed. The all-module docs gate stopped on existing mojibake in the Finance charge-management test plan.

## Acceptance

Verify list paging/search, basic-detail editing, concurrency conflicts, bootstrap preservation and PostgreSQL migration preservation.
Local synthetic checks do not prove deployed acceptance.

## Limits

The API fixture uses SQLite and authenticated test principals for ordinary API requests; native OpenIddict processes password and refresh grants.
PostgreSQL evidence covers this migration's generated Up/Down SQL against two synthetic pre-existing tenants, not a full deployed migration chain.
Deployment, production load/p95 profiling and a combined native API/browser environment have not been run.
The frontend browser workflow and final in-app smoke are recorded in [frontend testing](../../../../Shikshamitra-FE/docs/host-tenants/testing.md).
