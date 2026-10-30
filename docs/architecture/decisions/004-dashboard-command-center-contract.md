# ADR-004: Dashboard command center contract

## Status

Accepted

## Context

The main `/dashboard` surface is becoming the ERP operating cockpit for Indian schools. It must serve different users without creating a separate hard-coded page for every school role. At the same time, dashboard widgets summarize sensitive module data such as students, attendance, HR, finance, calendar, safety, and compliance readiness.

The frontend and backend currently both maintain dashboard widget metadata. This duplication is intentional for defense in depth during the transition from mock/frontend data to backend aggregation, but it creates drift risk for widget IDs, permissions, owners, and drilldown URLs.

## Decision

Use `/dashboard` as a template-driven command center backed by one dashboard summary contract.

- Supported templates are `leadership`, `principalAdmin`, `teacher`, `financeHr`, and `studentParent`.
- `presentationRole` selects a presentation within those existing families: leadership; principal/admissions; teacher; finance/hr/financeHr; student/parent. It never grants source access. Omitted staff presentation is inferred from the current roles and exact source capabilities; personal presentation comes from persisted verified account links.
- Custom school roles resolve to one of these templates instead of creating new role-specific dashboard pages.
- The backend summary endpoint is the default data source for production dashboard rendering.
- Existing module dashboards and operational routes remain the drilldown destinations.
- Backend permission filtering is authoritative and must happen before widget labels, counts, values, or links are returned.
- Frontend widget filtering remains as defense in depth.
- Summary responses include a safe scope label, generated timestamp, live or partial data state, and semantic widget kind, priority, and action label.
- Semantic fields describe meaning and urgency without prescribing frontend grid spans or responsive layout.
- Production clients must show honest partial or retryable error states and must not replace unavailable live data with mock operational values.

The summary has an additive typed `focus` projection with collections, attendance, admissions pipeline, published weekly timetable, saved payroll review, or published personal results. Its metrics and series use nullable numeric values and explicit units. Staff projections return the exact `requiredPermissions`; every required permission must be granted before reading the source. Focus failures leave the remaining authorized summary available and set `zoneErrors.focus`.

Requests may supply `studentId` and `presentationRole`; existing template IDs and widget arrays remain compatible. Date ranges are ordered and limited to 367 inclusive days. Collection previews stop at 2,000 scoped final receipts; timetable previews stop at 60 scoped weekly lessons. An exceeded limit returns unavailable, never an aggregate from a truncated population. Attendance displays at most 30 canonical class summaries. Collections use recorded fee amounts from confirmed final receipts, exclude charges, reversed parent transactions, rejected and superseded documents, and do not reinterpret older combined receipts without a recorded breakdown.

Student/parent requests take a separate path before any staff slice executes. `StudentPortalAccessService` validates the active, email-confirmed persisted identity and current tenant links; contact email matching is never an ownership rule. Responses expose only distinct child IDs and labels. Multiple children require explicit selection; an unrelated, revoked or cross-tenant selection is denied. Academic context resolution verifies the selected enrollment and year. Published/Frozen result headers exclude withheld details and unpublished exams; result previews contain up to six latest released results. Personal cards contain no staff drilldown URLs. Learning tasks and upcoming exams reuse verified portal owners after conservative population guards (200 works and 100 exams); parent next-installment evidence additionally requires the exact Finance View action and a maximum of 500 due rows.

Verified student resolution uses the existing nontracking Student repository query without automatic relationship inclusion, after the same link authorization and with explicit tenant/ID predicates. Tenant and soft-delete filters stay enabled. Personal context readers consume scalar identity/placement fields; profile detail owners load their own explicit projections. Admission action counts reuse the canonical scoped application pending-decision query for the selected year, rather than inferring workflow state from a Student lifecycle status.

Main-dashboard GET requests must not calculate or refresh payroll previews. Saved payroll evidence comes from existing monthly snapshots, restricted to payroll-eligible scoped employees. It includes its period and oldest evidence timestamp, and is labelled a saved preview rather than current lock readiness. Missing snapshots return unavailable. Both leadership review actions and Finance/HR summaries use this read-only path; preview generation, stale-row cleanup, and snapshot insertion remain owned by the Payroll workflow.

Each widget now emits its canonical `requiredPermissions`. Personal-only widget catalogue entries have empty action arrays and empty links; they are emitted only by the verified personal path. The staff filter treats an empty permission array as denied. Clients must not infer links or staff grants for personal-only entries.

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
