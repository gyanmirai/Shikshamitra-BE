# Architecture Hardening Roadmap

This roadmap records non-functional hardening needed to keep ShikshaMitra ERP maintainable as more dashboard and module integrations become live. It is intentionally conservative: hardening must not change module behavior unless a later implementation plan explicitly approves it.

## Current Risks

| Risk | Why it matters | Current control |
| --- | --- | --- |
| FE and BE dashboard widget registries can drift | Widget IDs, permissions, owners, and drilldowns are duplicated while the dashboard matures | Backend emits only known widgets from `DashboardWidgetCatalog`; `GET /api/dashboard/widgets` exposes backend-owned widget metadata; frontend hides unknown widgets when permission enforcement is active; frontend registry tests compare FE widget IDs with backend definitions |
| Permission-name drift between FE and BE | Menus and widget visibility may diverge from server authorization | Dashboard registry tests verify each frontend widget permission stays the same as, or narrower than, backend widget permissions |
| Route and drilldown drift | Dashboard cards can point to routes that move or become permission-invalid | Registry-owned drilldown URLs, route-tree and route-leaf permission registry tests, and E2E drilldown smoke tests |
| Large dashboard backend reader | More live slices make one reader harder to reason about and test | Dashboard widget metadata and current live enrichment slices are extracted into focused providers; `DashboardSummaryReader` now orchestrates template construction and final permission filtering |
| Slice failure opacity | Partial dashboard failures can be hard to diagnose if only `zoneErrors` reach the UI | `DashboardSummaryReader` logs slice latency at debug level and logs new `zoneErrors` at warning level |
| Mixed live and readiness-only widgets | Users may misread readiness widgets as live operational data | Dashboard docs must label readiness-only areas clearly |
| UserScope bypass risk in aggregates | Counts could expose data the user cannot inspect in the owning module | Dashboard aggregation must reuse module-owned scoped readers or apply equivalent UserScope |

## Guardrails

- Backend data and authorization are authoritative. Frontend permission filtering is defense in depth only.
- Dashboard widgets must use registered IDs and must be actionable through a safe relative drilldown URL or accountable through an owner.
- Backend dashboard aggregation must not return labels, values, counts, or links for widgets the user cannot access.
- Live dashboard counts must come from a scoped module source. If a scoped source does not exist, the widget remains readiness or drilldown only.
- Slice failures must populate `zoneErrors` and leave unrelated dashboard zones usable.
- Dashboard implementation must not alter Finance, HR, Academic, Calendar, Communication, Transport, Security, Student, Settings, or navigation workflows.
- Production mock and contract-preview modes must stay disabled unless explicitly enabled by environment flags.

## Short-Term Checklist

Apply this checklist before adding or changing dashboard widgets:

- Verify the widget ID exists in both the frontend registry and backend dashboard definition.
- Verify required permissions match the owning module's backend permission constants.
- Verify the drilldown URL exists in the frontend route tree and points to the owning module workflow.
- Verify denied permissions hide the widget title, value, count, owner, and link.
- Verify unknown backend widgets are hidden by the frontend adapter/permission layer.
- Verify all live counts use UserScope-safe queries or module-owned scoped readers.
- Verify failed slices return `zoneErrors` without blanking the full dashboard.
- Verify docs identify whether the widget is live, readiness-only, or drilldown-only.

## Validation Commands

Backend dashboard API checks:

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter Dashboard
```

Frontend dashboard checks:

```powershell
npm run test -- dashboard
npx eslint "src/screens/mainDashboard/**/*.{ts,tsx}" "src/redux/**/*dashboard*.ts" "e2e/dashboard/**/*.ts" --quiet
```

Run the frontend commands from `Shikshamitra-FE`. Run the backend command from `Shikshamitra-BE`.

## Implemented Hardening Checks

Current automated checks:

- `src/screens/mainDashboard/dashboard-widget-registry.test.ts` verifies every mock widget ID exists in the frontend registry.
- The same test now compares frontend dashboard widget IDs with backend `DashboardWidgetCatalog` widget definitions.
- The same test verifies frontend widget permissions stay the same as, or narrower than, backend widget permissions, while tolerating existing FE suffix-only permission naming.
- The same test verifies every registered dashboard `drilldownUrl` points to an existing Next.js page route and matches the target route's most specific permission guard.
- Backend dashboard constants, widget definitions, and summary mutation helpers are now outside `DashboardSummaryReader`.
- `GET /api/dashboard/widgets` exposes backend-owned widget metadata for IDs, zones, drilldowns, owners, and required permissions.
- Backend API tests verify the widget metadata endpoint is Dashboard-permission protected and returns actionable registered widgets.
- The scoped student attendance live slice now runs through `DashboardStudentAttendanceSliceProvider`.
- Action queue enrichment now runs through `DashboardActionQueueSliceProvider`.
- Finance and HR snapshot enrichment now runs through `DashboardFinanceHrSnapshotSliceProvider`.
- Calendar enrichment now runs through `DashboardCalendarSliceProvider`.
- Compliance readiness enrichment now runs through `DashboardComplianceReadinessSliceProvider`.
- `DashboardSummaryReader` records lightweight observability for each slice: elapsed time at debug level, new `zoneErrors` at warning level, and unexpected slice exceptions at error level.
- Existing dashboard permission and template tests verify unknown widgets, denied widgets, and partial slice failures stay controlled.

## Future Hardening Sequence

1. Use the backend widget metadata endpoint to replace or generate the frontend registry when the frontend runtime contract is ready.
2. Add module-specific live slices only after each owning module exposes a scoped aggregate source.

## Out Of Scope For This Roadmap

- No module workflow redesign.
- No database migration.
- No frontend layout redesign.
- No replacement of ABP authorization, tenant isolation, or UserScope.
- No production use of mock or contract-preview dashboard sources.
