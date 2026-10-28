---
module: academic
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-10-28
owner: backend-team
audience: humans-and-agents
---

# Validate Academic setup behavior

TL;DR: run the guide's real API/service boundary suite and isolated Host build, then verify frontend workflows separately; PostgreSQL parallel-visitor acceptance remains unverified.

## Focused commands

Run from `Shikshamitra-BE` with the .NET 9 SDK selected by `global.json`:

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~AcademicSetupGuideApi_Tests --artifacts-path .artifacts/academic-setup-guide
dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj --artifacts-path .artifacts/academic-setup-guide
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module academic
git diff --check
```

Keep generated logs and test output under the ignored artifacts directory.
No runtime database, migration or seeder run is needed for this feature's schema.
The API fixture uses disposable synthetic SQLite data.

## Executable coverage

Tests live in `test/ShikshaMitra.HttpApi.Tests/Academic/Setup/AcademicSetupGuideApi_Tests*.cs`.

| Concern | Evidence |
|---|---|
| First school visit | Repeated GETs do not write; first POST wins across accounts; another year does not reset the marker |
| Action authorization | Exact setting View required; Dashboard/module visibility does not substitute for it |
| Identity and tenancy | Anonymous, host, inactive and forged cross-tenant accounts rejected |
| Context and scope | Foreign years/media, wrong year-medium relationship, missing scope and sibling classes rejected |
| Real configuration | All nine record families affect their selected context; archived subjects do not count |
| Progress | Shared year state, stale-stamp rejection, server review timestamp and review clearing |
| Data preservation | Sibling ExtraProperties and hidden deferred keys survive permitted writes |
| Invalid stored state | Malformed marker and unsupported/incomplete progress JSON fail without resetting data |
| Direct application boundary | Identity enforcement holds without an HTTP controller |
| Visit retry | Synthetic repository collision exercises the actual service, fresh UOW and preserved winning actor/time |

The focused suite passed 15 tests on 2026-10-28.
The final isolated Host build passed with zero errors; earlier full compilations exposed existing repository warnings.
These results do not establish deployed acceptance.

## Concurrency and provider limits

The normal API fixture shares one in-memory SQLite connection.
Simultaneous HTTP transactions fail in provider transaction creation before the visit service, so that fixture does not establish parallel PostgreSQL behavior.
The suite covers two clients with pre-visit snapshots and injects an optimistic repository collision into the real visit service to verify its fresh-UOW retry contract.
Run a disposable PostgreSQL acceptance check before claiming provider-level parallel-visitor evidence.
Do not repurpose a deployed school database for that test.

## Security gate and known baseline

Follow the [security standard](../../common/security/permission-consistency.md) and the reusable workflow in `.github/workflows/security-regression.yml` before merging.
A focused local domain subset covering `UserScopeResolution_Tests`, `SecurityPermissionSeeder_Tests`, `SecurityReleaseWorkflow_Tests`, `RolePermissionSeederContributor_Tests` and `RolesSeederContributor_Tests` yielded 54 passes and one existing failure on 2026-10-28.
`SecurityReleaseWorkflow_Tests.Deployment_Requires_Security_Gates_Before_Migration_And_Seeding` expects a reusable security dependency that the current deployment workflow lacks.
That workflow and test were unchanged by the guide implementation.
The complete security workflow, PostgreSQL gates and deployed browser acceptance were not established by the backend guide tests.
Frontend checks and browser evidence belong to the [frontend testing guide](../../../../Shikshamitra-FE/docs/academic/testing.md).
