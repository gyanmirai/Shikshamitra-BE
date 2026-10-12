---
module: mobile-academics
domain: validation
content_type: Reference
status: canonical
updated_on: 2026-10-12
owner: backend-team
audience: humans-and-agents
---

# Validate mobile Academics

`test/ShikshaMitra.Application.Tests/Academic/Mobile/MobileAcademicAppService_Tests.cs` covers student/guardian reads without staff grants, saved subject colours in the Syllabus dashboard, revoked/unlinked denial, draft/final persistence and locks, unpublished/withheld marks, generated PDF bytes, legacy attendance dates, guardian-only reasons, leave school-day duration and half-day times, school decision locks, and foreign attachment references.

```powershell
dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter FullyQualifiedName~MobileAcademicAppService_Tests
dotnet build src/ShikshaMitra.HttpApi.Host/ShikshaMitra.HttpApi.Host.csproj
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module mobile-academics
git diff --check
```

The nine focused tests pass with the normal test project, without exclusions. The full application suite was not run. Repository-wide build warnings remain. SQLite application tests validate behavior; the additional HTTP test below uses real PostgreSQL.

`test/ShikshaMitra.HttpApi.Tests/Academic/Mobile/MobileAcademicPostgresApi_Tests.cs` passes on PostgreSQL 18.6. It creates a fresh loopback `mobile_academic_tests_*` database and refuses existing databases. It recreates the predecessor columns and executes the actual `MobileAcademicResponses` migration operations. It then verifies `RepairMobileLeaveHalfDayTimes` against both complete and missing half-day columns, repeated application, and recorded migration history. It tests all seven root routes plus the attendance calendar, persisted-link denial, two simultaneous leave requests reaching transaction locks on different connections, one surviving record, half-day times, cancellation, multipart upload, exact downloaded bytes, deletion, and a generated PDF report. Authentication headers come from the HTTP test harness; this is local application integration evidence rather than deployed OIDC evidence.

Set `MOBILE_ACADEMIC_TEST_POSTGRES` to a connection string for a fresh disposable loopback database before running:

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter FullyQualifiedName~MobileAcademicPostgresApi_Tests --artifacts-path .artifacts/mobile-academics-http -m:1 -p:UseSharedCompilation=false
```

The automated test does not migrate or query a school database. Separately, the local development database was inspected after the reported `42703` error: migration history included `MobileAcademicResponses`, but both half-day columns were absent. A schema backup was saved in ignored local artifacts, the repair migration was applied transactionally without seeding, and both existing leave records were retained. Authenticated requests to the running host returned HTTP 200 for attendance calendar (October 2026, 31 days), overview, and leave requests. No host restart was needed. Remote database migration and API deployment remain release steps.

The [mobile guide](../../../../Gyanmirai-Mobile/docs/mobile-academics.md) owns Flutter analysis, regression commands, and browser evidence. Run an authenticated integration smoke after deploying the migration and backend, including child switches and authorized binary downloads.
