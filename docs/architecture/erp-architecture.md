# ShikshaMitra ERP Architecture

This document is the canonical high-level architecture overview for ShikshaMitra ERP. It explains how the current system is implemented across the Next.js frontend, ABP/.NET backend, PostgreSQL persistence, and cross-cutting platform services.

## Runtime System

```mermaid
flowchart LR
    User["School user"] --> FE["Next.js frontend<br/>App Router, MUI, Redux"]
    FE --> Auth["NextAuth session<br/>JWT access token"]
    Auth --> Token["ABP OpenIddict<br/>/connect/token"]
    FE --> RTK["RTK Query API layer<br/>src/redux/service.ts"]
    RTK --> Host["ABP HTTP API Host<br/>ShikshaMitra.HttpApi.Host"]
    Host --> Controllers["HTTP controllers<br/>and ABP conventional APIs"]
    Controllers --> App["Application services<br/>DTO orchestration and authorization"]
    App --> Domain["Domain services and entities<br/>business rules and UserScope"]
    Domain --> EF["EF Core repositories<br/>default and custom repositories"]
    EF --> DB["PostgreSQL"]
    Host --> Workers["Background workers<br/>dashboard previews, reports, reminders"]
    Host --> ABP["ABP platform<br/>identity, tenant, permissions, audit, settings, blobs"]
```

The application is a modular monolith. Modules are separated by folder, project layer, contracts, permissions, and aggregate boundaries, not by independently deployed services.

## Backend Layers

```mermaid
flowchart TB
    Shared["Domain.Shared<br/>permissions, enums, constants, localization"] --> Domain["Domain<br/>aggregates, entities, domain services, UserScope"]
    Domain --> Contracts["Application.Contracts<br/>DTOs, service interfaces, permission definitions"]
    Contracts --> Application["Application<br/>AppServices, readers, workflows, AutoMapper"]
    Domain --> EFCore["EntityFrameworkCore<br/>DbContext partials, entity configs, repositories"]
    Application --> HttpApi["HttpApi<br/>thin controllers and response metadata"]
    HttpApi --> Host["HttpApi.Host<br/>runtime pipeline, auth, CORS, Swagger, workers"]
    EFCore --> Host
```

The backend follows the ABP DDD layer rules in [project best practices](../best-practices.md). Important runtime responsibilities are:

| Concern | Current owner |
| --- | --- |
| Authentication and token validation | OpenIddict and ABP auth in `HttpApi.Host` |
| Tenant isolation | ABP multi-tenancy filters and tenant middleware |
| Authorization | `[Authorize]`, ABP permission checks, and module permission definitions |
| Data scope | `UserScope` services and `IUserScopedEntity` filtering |
| Persistence | EF Core with PostgreSQL, partial DbContext files, and custom repositories |
| Auditing | ABP audit logging plus module audit tables where needed |
| Files and generated artifacts | ABP BlobStoring database provider and module file services |
| Background processing | ABP background workers and jobs in the host |

## Frontend Layers

```mermaid
flowchart TB
    Routes["src/app routes<br/>Next.js App Router"] --> AuthLayout["AuthRouteLayout<br/>login redirect and shell guard"]
    AuthLayout --> DashboardLayout["DashboardLayout and SubLayout"]
    DashboardLayout --> Screens["src/screens/*<br/>module screens and workflows"]
    Screens --> Components["shared components<br/>MUI, forms, tables, cards"]
    Screens --> Store["Redux store<br/>auth, global state, RTK Query"]
    Store --> AuthSlice["authSlice<br/>permissions and employee profile"]
    Store --> Api["RTK Query injected APIs"]
    Api --> Backend["NEXT_PUBLIC_API_URL/api"]
    Screens --> Constants["src/constants<br/>menus, paths, FE permission names"]
```

The frontend is route-first and module-screen oriented. Authenticated routes render through the shared authenticated layout, then each module screen uses local UI components, Redux state, RTK Query endpoints, and menu/permission constants.

## ERP Module Map

```mermaid
mindmap
  root((ShikshaMitra ERP))
    Dashboard
      Leadership
      Principal/Admin
      Teacher
      Finance/HR
      Student/Parent
    Students
      Records
      Enrollment
      Attendance
      Documents
      Identity compliance
    Academic
      Curriculum
      Timetable
      Homework
      Exams
      Gradebook
      LMS
    HR
      Employees
      Attendance and leave
      Payroll
      Policies
      Reports
    Finance
      Fees
      Collections
      Concessions
      Refunds
      Reconciliation
      Accounts
    Communication
      Announcements
      Notifications
      Meetings
    Calendar
      Events
      Holidays
      Groups
    Operations
      Transport
      Hostel
      Security guard
      Library
      Resource management
    Platform
      Security
      Permissions
      UserScope
      Tenant settings
      Institute setup
```

## Dashboard Command Center

```mermaid
sequenceDiagram
    actor User
    participant Page as /dashboard page
    participant Resolver as Template resolver
    participant Source as Dashboard data source
    participant Api as RTK Query
    participant Endpoint as GET /api/dashboard/summary
    participant Metadata as GET /api/dashboard/widgets
    participant Reader as DashboardSummaryReader
    participant Providers as Dashboard slice providers
    participant Modules as Scoped module sources
    participant UI as Shell, zones, widgets

    User->>Page: Open /dashboard
    Page->>Resolver: Resolve template from session roles or explicit template claim
    Page->>Source: Request dashboard data
    Source->>Api: useGetDashboardSummaryQuery
    Api->>Endpoint: dashboardTemplate, academicYearId, from, to
    Endpoint->>Reader: Build summary
    Reader->>Providers: Apply live/readiness enrichment
    Providers->>Modules: Student, Attendance, Academic, Finance, HR, Calendar, Compliance
    Modules-->>Providers: Scoped values or slice errors
Providers-->>Reader: Enriched widgets and zoneErrors
Reader-->>Endpoint: Contract plus zoneErrors
    Endpoint-->>Api: Permission-filtered widgets
    Metadata-->>Api: Backend-owned widget metadata for drift control
    Source->>Source: Adapt payload, drop unsafe drilldowns, enrich registry metadata
    Page->>UI: Apply frontend permission filtering
    UI-->>User: Actionable dashboard zones and drilldowns
```

`/dashboard` is implemented as a role-template command center. The current templates are `leadership`, `principalAdmin`, `teacher`, `financeHr`, and `studentParent`. The frontend resolves the template, the backend returns the summary contract, and both layers filter widgets by permissions.

Live dashboard areas currently include scoped student records, scoped student attendance, selected action queue counts, Finance/HR snapshots, Academic curriculum/exam/result signals, Calendar item enrichment, local student identity compliance readiness, limited Teacher assigned-class/workload/exam/result signals, and limited Student/Parent personal attendance, assignment, fee, document, and result signals through exact single-student email resolution. Communication announcements, Transport/Security exception counts, broad Teacher workload, Student/Parent transport/safety values, and official external compliance portal status remain readiness or drilldown oriented until their owning modules expose scoped sources.

`DashboardSummaryReader` also records slice-level operational signals: elapsed time is logged at debug level, new `zoneErrors` are logged at warning level, and unexpected slice exceptions are logged at error level before they propagate.

`GET /api/dashboard/widgets` exposes the backend-owned dashboard widget catalog. It is protected by the Dashboard permission and returns registered widget IDs, zones, drilldown URLs, owners, and required permissions so future frontend registry generation or runtime adoption can move away from duplicated metadata.

Supporting dashboard docs live in the frontend repo:

- [Dashboard PRD](../../../Shikshamitra-FE/docs/dashboard/prd.md)
- [Dashboard data contract](../../../Shikshamitra-FE/docs/dashboard/data-contract.md)
- [Dashboard testing and acceptance](../../../Shikshamitra-FE/docs/dashboard/testing-and-acceptance.md)

## Security And Scope Model

```mermaid
flowchart LR
    Role["Role and permissions<br/>what user can do"] --> Permission["ABP permission checks"]
    Scope["UserScope<br/>what data user can see"] --> Query["Scoped module query"]
    Permission --> ApiResult["Allowed endpoint/widget/action"]
    Query --> ApiResult
    ApiResult --> FEFilter["Frontend defense-in-depth filtering"]
    FEFilter --> UI["Visible screen, count, link, or action"]
```

Security is layered:

- Backend endpoint access is enforced with ABP authorization attributes and permission checks.
- Backend data summaries must apply UserScope before counts, lists, alerts, or drilldowns are returned.
- Frontend permission filtering is defense in depth and must not be treated as the primary security control.
- Tenant isolation is handled by ABP multi-tenancy and must not be bypassed with manual cross-tenant queries.

## Documentation Boundaries

- Cross-cutting architecture docs live under `docs/architecture`.
- Cross-cutting platform docs live under `docs/common`.
- Module-owned docs live under `docs/modules/<module>`.
- Frontend-specific dashboard product and testing docs remain in `Shikshamitra-FE/docs/dashboard`.
- Significant architecture choices are recorded as ADRs under [decisions](decisions/README.md).
