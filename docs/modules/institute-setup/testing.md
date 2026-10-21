---
module: institute-setup
domain: testing
content_type: Troubleshooting
status: canonical
updated_on: 2026-10-21
owner: backend-team
audience: humans-and-agents
---

# Validate Institute Setup backend changes

TL;DR: run the docs validator for documentation changes and targeted Tenant Settings or Academic tests for behavior changes.
Rooms Management has focused domain layout and direct-call authorization coverage.

## Reports

`InstituteReportApi_Tests.ModuleReports.cs` covers tenant/deletion-safe document/file counts, category filtering, real CSV generation, exact source grants, signature denial, scoped identity listing/counts and source/scope revocation on downloads. `InstituteReportModuleRules_Tests` covers new catalogue validation and shared identity read/management scope behavior. Also run `SchoolIdentityAuthorization_Tests` after changing the shared scope policy.

`InstituteReportCalculation_Tests` covers same-day, fractional, overnight, equal-time and rounded duration values, numeric filter/sort behavior and rejection of unsupported calculations. `InstituteReportApi_Tests.CalculatedColumns.cs` covers AI plan/approval, same-day and overnight preview values, actual PDF/Excel/CSV generation, Excel/CSV column contents and numeric filtering.

Run focused tests with `dotnet test test/ShikshaMitra.Application.Tests --filter FullyQualifiedName~InstituteReportRules`, `dotnet test test/ShikshaMitra.Domain.Tests --filter FullyQualifiedName~InstituteReportJob`, and `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FullyQualifiedName~InstituteReportApi`. Use a separate `--artifacts-path` when a running host locks outputs.

Rules tests cover catalogue/field validation, academic prerequisites, numeric filtering/sorting, scope projection and approval tampering. Run `dotnet test test/ShikshaMitra.Application.Tests --filter FullyQualifiedName~InstituteReportResponse` for independent provider parsing and class-count grouping. Domain tests cover export lifecycle. HTTP tests use disposable SQLite with synthetic authentication/permissions and a deterministic scope service; they cover real tenant-owned queries, scoped previews, requester ownership, background execution and PDF/Excel/CSV contents. The exact single-division prompt is tested through planning, preview and CSV generation; ZAI empty, truncated and malformed responses must use Institute Setup errors, and its request must disable thinking. PostgreSQL migration checks and live provider/staging acceptance remain separate deployment evidence. The [frontend testing guide](../../../../Shikshamitra-FE/docs/institute-setup/testing.md#reports) owns browser evidence.

## School Documents

| Scope | Command |
| --- | --- |
| File signatures, size/count limits, details and concurrency | `dotnet test test/ShikshaMitra.Domain.Tests --filter FullyQualifiedName~SchoolDocuments_Tests --artifacts-path .artifacts/school-documents-build` |
| Direct application-call authorization | `dotnet test test/ShikshaMitra.Application.Tests --filter FullyQualifiedName~SchoolDocumentAuthorization_Tests --artifacts-path .artifacts/school-documents-build` |
| Real HTTP CRUD/files, permissions, tenant isolation, retention and rollback | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FullyQualifiedName~SchoolDocumentsApi_Tests --artifacts-path .artifacts/school-documents-build` |
| PostgreSQL migration, real blob rollback, concurrent saves and retained bytes | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FullyQualifiedName~SchoolDocumentsPostgresApi_Tests --artifacts-path .artifacts/school-documents-build` |
| Browser bridge | `dotnet run --project scripts/testing/school-documents-bridge/SchoolDocumentsBridge.csproj --artifacts-path .artifacts/school-documents-build --no-launch-profile` |

The PostgreSQL case requires `SCHOOL_DOCUMENTS_TEST_POSTGRES` pointing to a **new** loopback `school_documents_tests_*` database. The fixture refuses existing databases and non-loopback hosts, creates the test database itself, validates additive Up and destructive Down against unrelated sentinel data, then removes only its owned disposable database. It never migrates a school database. Without the variable, the PostgreSQL case is explicitly skipped.

The bridge listens only at `http://127.0.0.1:4394`, uses synthetic test authentication/permissions and the real HTTP pipeline with fresh in-memory SQLite. Pair it with the [frontend fixture](../../../../Shikshamitra-FE/docs/institute-setup/testing.md#school-documents). Its labelled failure switches simulate transport failure for UI retry tests; API tests separately verify real transactional rollback. Stop both bridge and frontend fixture when finished. Test logs, SQL and browser screenshots belong in ignored artifacts directories.

Release requires applying `20261021052944_AddSchoolDocuments` before exposing the new route, explicitly assigning View/Add/Delete and independent navigation visibility, and validating file access with the intended school roles. Do not reseed existing grants to enable this feature.

## Room layout and features

| Check | Command |
| --- | --- |
| Unequal columns, derived capacity, invalid layouts and feature normalization | `dotnet test test/ShikshaMitra.Domain.Tests --filter FullyQualifiedName~RoomLayout_Tests` |
| Read/write room and catalogue policy enforcement | `dotnet test test/ShikshaMitra.Application.Tests --filter FullyQualifiedName~RoomAuthorization_Tests` |

The room migration script and EF model consistency were checked locally. Applying its seed SQL and verifying tenant isolation against a disposable PostgreSQL database remain release checks; no disposable PostgreSQL connection was verified. These focused tests do not establish deployed persistence.

## Initial setup checks

| Scope | Command |
| --- | --- |
| State invariants | `dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter InitialSetupProgress_Tests --artifacts-path .artifacts/initial-setup-build` |
| HTTP integration | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter InitialSetupApi_Tests --artifacts-path .artifacts/initial-setup-build` |
| Host provisioning regression | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter HostTenantApi_Tests --artifacts-path .artifacts/initial-setup-build` |
| Model consistency | `dotnet ef migrations has-pending-model-changes --project src/ShikshaMitra.EntityFrameworkCore --startup-project src/ShikshaMitra.DbMigrator --configuration InitialSetup` |

HTTP checks exercise real services and persistence: new-school provisioning, reload, stale drafts, confirmed steps, staff retry, review edits, idempotent completion, denied writes, masked reads, anonymous/host denial and cross-school year selection.
The clean setup fixture avoids unrelated demo-data initialization.

Host tenant regressions also read the list after deactivation and reactivation, with and without a status filter.
They verify saved status, institute names, detail responses, concurrency protection, audit history and blocked school access.

Validate generated migration UP and DOWN scripts on a disposable PostgreSQL database before deployment.
Include explicit legacy completion, legacy partial progress, operational schools without markers, blank schools, mixed-tenant branches and deleted tenants.
Confirm rollback preserves all old school records.
Do not run a fixture or rollback against a live school database.
The [frontend testing page](../../../../Shikshamitra-FE/docs/initial-setup/testing.md) owns wizard and browser checks.

## Documentation checks

Run this command after editing active Institute Setup docs.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module institute-setup
```

## Focused backend tests

Use these tests as the first validation set when Rooms Management affects Tenant Settings or scheduling.

| Risk area | Command |
|---|---|
| Tenant authorization | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter TenantAuthorizationApi_Tests` |
| Academic room references | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter LectureSlotRoomApi_Tests` |
| Tenant Settings application behavior | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter TenantSettings` |

## Source checks

Use these checks when docs or routes change.

```powershell
rg -n "api/institute-setup|RoomsManagement|RoomController|BuildingController" src test docs/modules/institute-setup
git diff --check
```

## Add coverage when needed

Add or update tests when a change affects these areas.

| Change | Expected coverage |
|---|---|
| Building, floor, or room type relationship | Application test |
| Room payload or validation | HTTP API test |
| Delete behavior | Integration test with downstream references |
| Timetable or exam room impact | Academic HTTP API test |

## Academic Setup validation checks

Run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~DivisionCreationApi_Tests|FullyQualifiedName~AcademicSetupValidationApi_Tests" --artifacts-path .artifacts/division-regression` for division/standard duplicate responses, whitespace/case normalization, literal names, self edits, failed rename persistence, tenant filtering and optional-room persistence. These tests use disposable SQLite through the production HTTP pipeline; they do not establish PostgreSQL deployment acceptance or simultaneous-request uniqueness.

Run `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter AcademicSetupValidationApi_Tests --artifacts-path .artifacts/academic-setup-validation`. The disposable SQLite HTTP tests cover exact/case/padded duplicates, rejected rename persistence, self-edits, different stages, literal SQL wildcard characters, and preserved room allocations after an update omits the field.

Run `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~StandardAppService_Tests|FullyQualifiedName~DivisionOnboardingIntegration_Tests" --artifacts-path .artifacts/academic-setup-validation` for existing shift-reference and room-free onboarding regressions.

The browser bridge starts with `dotnet run --project test/browser/academic-setup/AcademicSetupBrowser.csproj --artifacts-path .artifacts/academic-setup-validation`. It binds only `127.0.0.1:4392` and uses production ABP endpoints with test authentication, synthetic tenant-wide scope and disposable SQLite. Start it from this repository root, then follow the [frontend checks](../../../../Shikshamitra-FE/docs/institute-setup/testing.md). Stop the owned servers after testing. These checks do not validate the deployed QA/Staging build.

## Shift isolation checks

| Scope | Command |
| --- | --- |
| Repository/service tenant boundary, assignment validation, times and deletion dependencies | `dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~ShiftTenant\|FullyQualifiedName~ShiftManagementAppService_Tests\|FullyQualifiedName~StandardAppService_Tests" --artifacts-path .artifacts/shift-isolation` |
| HTTP creation, forged tenant payload/header, foreign CRUD, standard assignment and anonymous denial | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter ShiftTenantApi_Tests --artifacts-path .artifacts/shift-http` |
| PostgreSQL migration preservation and rollback | Set `SHIFT_TEST_POSTGRES` to a new loopback `shift_isolation_tests_*` database connection, then run the HTTP test project with filter `ShiftTenantPostgresMigration_Tests` |
| Disposable real-backend browser bridge | `dotnet run --project test/browser/shift-isolation/ShiftIsolation.csproj --artifacts-path .artifacts/shift-browser-build` |

The bridge listens only on `127.0.0.1:4391`, uses the production HTTP pipeline with test authentication and an isolated SQLite database, and seeds synthetic schools. It disables live background workers and seeding. Stop it after testing. The [frontend testing guide](../../../../Shikshamitra-FE/docs/institute-setup/testing.md) owns grid, dropdown and session-switch evidence. These local checks do not prove a deployment or actual QA data repair.

## School Identity checks

Organization coverage in `SchoolIdentityIntegration_Tests.Organization.cs` verifies image persistence, inheritance/reset, independent siblings, preview/history, document retention, parent validation, scope denial and idempotent legacy banner import. Run `dotnet test test/ShikshaMitra.Application.Tests --filter FullyQualifiedName~SchoolIdentity`.

Run `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter "FullyQualifiedName~School_identity|FullyQualifiedName~Invented_school_identity" --artifacts-path .artifacts/school-identity-permissions/build` for the four-action role/user catalogue, inherited grants and overrides, atomic rejection of invented Delete grants, and HTTP 405 for profile deletion. These checks use the disposable HTTP test factory; they do not change school permissions.

| Scope | Command |
| --- | --- |
| Domain inheritance and image validation | `dotnet test test/ShikshaMitra.Domain.Tests/ShikshaMitra.Domain.Tests.csproj --filter SchoolIdentity --artifacts-path .artifacts/school-identity-build` |
| Configuration, scope, copy, retention and document layouts | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter "FullyQualifiedName~SchoolIdentity\|FullyQualifiedName~StudentIdCardPdfLayout\|FullyQualifiedName~StudentCertificatePdfRenderer\|FullyQualifiedName~StudentRecordActionAuthorization" --artifacts-path .artifacts/school-identity-build` |
| Host build without disturbing a debugger | `dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj --artifacts-path .artifacts/school-identity-build` |
| Real PostgreSQL HTTP and migration acceptance | Set `SCHOOL_IDENTITY_TEST_POSTGRES` to a fresh loopback `school_identity_tests_*` connection, then run `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter SchoolIdentityPostgresApi_Tests --artifacts-path .artifacts/school-identity-build` |

Tests must distinguish exact-action denial from tenant and UserScope denial. Include mismatched medium parents, stale saves, signature redaction, independent overrides, omission/reset, cross-year copies without file duplication, preserved legacy custom values and immutable reprints.

Cleanup coverage checks the age cutoff, conditional single-claim behavior, retained-file exclusion and contention between cleanup and retention. Real PostgreSQL HTTP/migration acceptance must use a disposable database and preserve all pre-existing tables. Never apply a rollback to a live school database.

The PostgreSQL factory refuses existing databases and non-loopback hosts. It verifies both migration directions, preserves sentinel rows in the three unrelated legacy tables, checks simultaneous first saves and updates, and drops only the database it created. The HTTP fixture's permission checker supports allowed/denied request headers; real permission-provider regressions remain a separate security gate.

`Record_ID_issuance_is_idempotent_snapshot_is_frozen_and_reprint_and_void_are_audited` and `Record_certificate_snapshots_idempotency_and_leaving_exit_are_enforced` exercise real issuance and reprints after school defaults change. Legacy-import tests check idempotency, original-byte preservation and configured-default protection. Staff preview tests check current shared assets and omission.

The [frontend testing guide](../../../../Shikshamitra-FE/docs/institute-setup/testing.md) owns browser evidence. Stored test output is evidence of only the cases it actually exercises, not a claim that every suite or deployment has passed.
