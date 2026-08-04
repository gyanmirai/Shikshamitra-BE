# ADR-004: Dashboard command center contract

## Status

Accepted

## Context

The main `/dashboard` surface is becoming the ERP operating cockpit for Indian schools. It must serve different users without creating a separate hard-coded page for every school role. At the same time, dashboard widgets summarize sensitive module data such as students, attendance, HR, finance, calendar, safety, and compliance readiness.

The frontend and backend currently both maintain dashboard widget metadata. This duplication is intentional for defense in depth during the transition from mock/frontend data to backend aggregation, but it creates drift risk for widget IDs, permissions, owners, and drilldown URLs.

## Decision

Use `/dashboard` as a template-driven command center backed by one dashboard summary contract.

- Supported templates are `leadership`, `principalAdmin`, `teacher`, `financeHr`, and `studentParent`.
- Custom school roles resolve to one of these templates instead of creating new role-specific dashboard pages.
- The backend summary endpoint is the default data source for production dashboard rendering.
- Existing module dashboards and operational routes remain the drilldown destinations.
- Backend permission filtering is authoritative and must happen before widget labels, counts, values, or links are returned.
- Frontend widget filtering remains as defense in depth.
- Summary responses include a safe scope label, generated timestamp, live or partial data state, and semantic widget kind, priority, and action label.
- Semantic fields describe meaning and urgency without prescribing frontend grid spans or responsive layout.
- Production clients must show honest partial or retryable error states and must not replace unavailable live data with mock operational values.

Temporarily accept duplicated FE and BE dashboard widget metadata.

- Every dashboard widget ID emitted by the backend must exist in the frontend registry.
- Widget changes must update both sides until metadata is generated or backend-owned.
- The backend now exposes `GET /api/dashboard/widgets` as the authoritative widget metadata catalog for future FE registry generation or runtime adoption.
- Tests must cover unknown widgets, denied permissions, unsafe drilldowns, and partial slice failures.

## Consequences

**Positive**

- The dashboard can support many school roles without multiplying page implementations.
- The frontend shell, zones, adapter, and permission filtering stay reusable across all templates.
- The backend can progressively enrich widgets from scoped module sources without changing the UI contract.
- Permission and UserScope rules remain visible in both the UI and server-side aggregation path.
- Shared semantic fields let each role template use an appropriate accessible renderer without creating separate page implementations.

**Negative**

- Widget metadata can drift while it exists in both repos.
- Dashboard aggregation still has multiple moving parts as more live slices are added, even though current live enrichment is split into focused providers.
- Readiness-only widgets must be clearly documented so users do not mistake them for live aggregate data.

## Follow-up

- Add a registry drift check before adding more live dashboard slices.
- Use the backend widget metadata endpoint to replace or generate the frontend mirror when that runtime contract is ready.
- Keep frontend mock and contract-preview data behind explicit development or test gates.
