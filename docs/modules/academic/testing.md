---
module: academic
domain: testing
content_type: Reference
status: canonical
updated_on: 2026-10-30
owner: backend-team
audience: humans-and-agents
---

# Validate Academic setup behavior

TL;DR: run the guide's real API/service boundary suite and isolated Host build, then verify frontend workflows separately; PostgreSQL parallel-visitor acceptance remains unverified.

## Focused commands

Academic Insights tests are in `test/ShikshaMitra.Application.Tests/Academic/Dashboard/AcademicInsights*_Tests.cs`.
The contract suite covers required context, exact scope wildcard boundaries, denied-feature query suppression, revocation, personal-account entry selection, sibling-only navigation, technical error isolation, owner filters, latest progress, and assignment all-target scope.
The disposable SQLite query suite checks direct application identity denial, invalid hierarchy/missing scope, cross-tenant/year/division lesson isolation, and execution of empty aggregate/scatter queries.
Calendar query cases cover exclusion of generated weekends before the five-entry limit, retaining real Sunday events and other holidays, date order, past/foreign-division exclusion, and a weekend-only empty result.
Personal calendar projection cases verify the same limit and filtering against the full verified monthly source, including unordered input and past entries.
These checks do not establish PostgreSQL performance or deployed/browser acceptance.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~AcademicInsights --artifacts-path .artifacts/academic-insights
dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj --artifacts-path .artifacts/academic-insights
```

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

## Homework school-date and calendar checks

Run `dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~HomeworkCalendarDate_Tests|FullyQualifiedName~LearningTaskCalendarReadPerformance_Tests|FullyQualifiedName~LearningTaskScheduleTimestampTests" --artifacts-path .artifacts/homework-calendar-tests -p:RunAnalyzers=false`.

Coverage includes IST midnight versus the previous UTC date, valid and blocked selected dates, calendar changes after draft creation, inactive calendars, other media/divisions, UTC storage round trips and one batched marker query across division schedules. These SQLite tests do not establish deployed PostgreSQL acceptance; the [frontend testing guide](../../../../Shikshamitra-FE/docs/academic/testing.md) owns browser evidence.

## Elective late admissions

Elective late-admission regression: `StudentElectiveAssignmentAppService_Tests.New_Admission_Should_Remain_Editable_Without_Unlocking_Existing_Students` covers initial locking, admission, search-independent lock status, incomplete-roster rejection, saving and locking new selections, and rejection of changes/deletions to locked selections. Run it with the shared-option/count regressions:

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~StudentElectiveAssignmentAppService_Tests|FullyQualifiedName~StudentElectiveReadPerformance_Tests" --artifacts-path .artifacts/elective-lock-tests -p:RunAnalyzers=false
```

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
