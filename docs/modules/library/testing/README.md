# Library Testing and Release Readiness

## Automated Gates

Run these from the indicated repo roots.

Backend:

```powershell
dotnet build --no-restore -m:1
dotnet test .\test\ShikshaMitra.Application.Tests\ShikshaMitra.Application.Tests.csproj --no-restore --filter FullyQualifiedName~Library -m:1
dotnet test .\test\ShikshaMitra.HttpApi.Tests\ShikshaMitra.HttpApi.Tests.csproj --no-restore --filter FullyQualifiedName~LibraryReleaseGateApi_Tests -m:1
```

Frontend:

```powershell
npm test -- library
npm run ts -- --pretty false --incremental false
$env:E2E_LIBRARY='1'; npm run test:e2e -- e2e/library --project=chromium
```

## Browser E2E Coverage

The gated Library E2E suite should cover:

- Active route smoke for every Library menu item and settings page.
- Reservation create/cancel.
- Issue and return with damaged/lost charge behavior.
- Member detail tabs.
- Charge create/update/delete.
- Report options, preview, export status, and download.
- Copy label drawer data and print readiness.
- Notification settings, history, instant send, and reminder run.
- Settings write paths for categories, hours, borrowing rules, and sections/shelves.

## Current QA Status

See the frontend release-gate note: [Library release QA](../../../../../Shikshamitra-FE/e2e/library/library-release-qa.md).

As of Phase 3D, backend build, focused Library application tests, targeted Library API release-gate tests, frontend Library tests, and frontend TypeScript passed. Browser E2E specs are implemented but require a live local API on `localhost:44332` and the FE app to be running.

## Acceptance Criteria for Phase 3E

- Local backend, database/seed data, and frontend app are running.
- `E2E_LIBRARY=1` Chromium suite passes without release-surface failures.
- No fake-success actions remain on active Library pages.
- Prototype Library routes stay redirected, hidden, or coming soon.
- Generated Playwright auth, reports, and result artifacts remain ignored.
- Finance integration has not been started in this release-closure phase.
