# Agent / contributor guide

**ShikshaMitra** is a multi-tenant **school management / CRM backend** built with **ABP Framework** and **.NET 9**.

## Start here

1. **[docs/README.md](docs/README.md)** -- documentation map and folder taxonomy.
2. **[docs/best-practices.md](docs/best-practices.md)** -- canonical engineering rules (DDD layers, folders, permissions). Prefer linking here instead of duplicating standards.

---

## Tech stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 9, C# 14, file-scoped namespaces, primary constructors |
| Framework | ABP Framework (DDD template), OpenIddict for auth |
| ORM | Entity Framework Core (code-first migrations) |
| Database | SQL Server (multi-tenant, per-tenant data isolation via ABP filters) |
| Validation | FluentValidation (primary), DataAnnotations (secondary) |
| PDF generation | QuestPDF |
| Excel export | ClosedXML |
| Blob storage | ABP BlobStoring (database provider) |
| Analyzers | Roslynator (code analysis + formatting) |
| Test framework | xUnit + ABP test infrastructure (in-memory DB) + WebApplicationFactory (API tests) |
| CI/CD | GitHub Actions → Azure Web App |

---

## Solution structure (DDD layers)

```
src/
  ShikshaMitra.Domain.Shared     → Enums, consts, ETOs, error codes (no deps)
  ShikshaMitra.Domain            → Aggregates, entities, value objects, domain services, repo interfaces
  ShikshaMitra.Application.Contracts → DTOs, app service interfaces, permission definitions, validators
  ShikshaMitra.Application       → App service implementations, AutoMapper profiles, event handlers
  ShikshaMitra.EntityFrameworkCore → DbContext, entity configs, repo implementations, migrations
  ShikshaMitra.HttpApi           → REST controllers (thin delegation only), contract mappers
  ShikshaMitra.HttpApi.Host      → Hosting, middleware, OpenIddict config, startup
  ShikshaMitra.DbMigrator        → Console app for running EF migrations + data seeding

test/
  ShikshaMitra.Application.Tests       → App service integration tests
  ShikshaMitra.HttpApi.Tests           → HTTP API endpoint tests (tiered: Critical/Important/Extended/Smoke)
  ShikshaMitra.Domain.Tests            → Domain logic unit tests
  ShikshaMitra.EntityFrameworkCore.Tests → Repository / query tests
  ShikshaMitra.TestBase                → Shared test infrastructure & seeders
```

**Rule**: Never reference Application or EntityFrameworkCore from Domain. Domain is the dependency root.

---

## Build & run

```bash
# Restore + build
dotnet restore
dotnet build

# Run database migrations (requires ConnectionStrings__Default)
cd src/ShikshaMitra.DbMigrator && dotnet run

# Run the API host
cd src/ShikshaMitra.HttpApi.Host && dotnet run

# Run tests
dotnet test
```

### Required environment / secrets

| Variable | Purpose |
|----------|---------|
| `ConnectionStrings__Default` | SQL Server connection string |
| `DB_CONNECTION_STRING` | Same, used in CI pipeline |

---

## Module map

| Module | Domain entities | Key app services |
|--------|----------------|------------------|
| **HR** | Employee, EmployeeLeave, LeaveType, LeavePolicy, Event, Holiday, Payroll, SalarySlip | EmployeePersonalInfoAppService, LeaveManagementAppService, SalarySlipAppService |
| **Students** | Student, StudentEnrollment, StudentAttendance, StudentLeave, StudentDocument | StudentRegistrationAppService, StudentAdmissionAppService, StudentAttendanceAppService |
| **Academic** | Subject, SubjectAssignment, Exam, Assignment, HomeworkType, ClassTeacher | SubjectAppService, ExamAppService, AssignmentAppService |
| **Finance** | Charge, Expense, Income, FeeStructure, ConcessionRequest | ChargeAppService, FeeStructureAppService, ConcessionRequestAppService |
| **Library** | Book, BookCopy, BookIssue, LibraryMember, LibraryCharge | BookAppService, BookIssueAppService, LibraryMemberAppService |
| **TenantSettings** | AcademicYear, Standard, Division, EducationBoard, Room, Building | AcademicYearAppService, StandardAppService, RoomAppService |
| **Security** | UserScopeAssignment | UserScopeAppService, ShikshaMitraPermissionAppService |
| **Groups** | Group, GroupMember | GroupAppService |
| **Communication** | AnnouncementType | AnnouncementTypeAppService |

---

## Multi-tenancy & user scope

- Every tenant-scoped entity implements `IMultiTenant`.
- ABP automatic tenant filter handles query isolation — **never** manually filter by `TenantId`.
- Scope hierarchy: **Board → Medium → Stage → Class → Division**.
- `NULL = ALL` principle: null scope value = unrestricted access at that level.
- Entities needing hierarchical filtering implement `IUserScopedEntity`.

---

## Key conventions (quick reference)

| Rule | Detail |
|------|--------|
| Aggregate roots | `FullAuditedAggregateRoot<Guid>` + `IMultiTenant`, `private set` properties, named mutation methods |
| Folder template | `{SubModule}/Aggregates/`, `Entities/`, `ValueObjects/`, `Managers/`, `Services/`, `Repositories/`, `Exceptions/` |
| Max nesting depth | `{Module}/{SubModule}/{NestedConcern}/` — never a 4th level |
| App services | Thin orchestration only; no business logic; target ~200 lines max |
| Error handling | `BusinessException` with codes from `Domain.Shared` (e.g., `"HR:Employee:010001"`). **Never** `UserFriendlyException`. |
| Permissions | Coarse-grained `[Authorize]` attribute + fine-grained IDOR checks in method body |
| Namespace root | `ShikshaMitra` — mirrors folder paths |
| Controllers | Thin delegation, `[Area("app")]`, `[RemoteService(Name = "Default")]` |
| Contract mappers | Static classes in `HttpApi/Mappers/` — **not** in Application.Contracts |
| Code style | Tabs, file-scoped namespaces, primary constructors for DI, nullable reference types enabled |

---

## Known tech debt (do not extend)

| Item | Status | Notes |
|------|--------|-------|
| `EmployeeAppService` (monolithic) | `[Obsolete]` | Use focused services: `EmployeePersonalInfoAppService`, `EmployeeDocumentAppService`, etc. |
| `HR/EventAndHolidays/` duplicate sub-module | Pending deletion | Use `HR/Calendar/Events/` instead. Delete after data migration. |
| `Students/Attendance/` vs `Students/Attendances/` | Needs consolidation | Two folders modelling the same concept |
| `FilterableCrudAppService.cs` | Fully commented out | Delete or implement |
| `ClassTeacherAssignmentAppService_Fixed.cs` | Dead file | Delete after verifying original works |
| Flat `Students/` root | Violates folder template | Needs `Aggregates/`, `Entities/`, `ValueObjects/` separation |

---

## Testing

- App-service test classes: `{AppService}_Tests` (e.g., `EmployeeAppService_Tests`).
- API test classes: `{Controller}Api_Tests` (e.g., `EarningComponentApi_Tests`).
- Seeders: `ShikshaMitraSeederContributor` → module-specific seeders.
- **Current coverage**: HR module (app-service + API tests). Students, Finance, Academic, Library have **zero test coverage**.

```bash
# App service tests
dotnet test --filter "Project=ShikshaMitra.Application.Tests"

# API tests (all tiers except smoke)
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier!=Smoke"

# API tests (critical only — fast PR gate)
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Critical"

# API tests (by module)
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Module=HR"
```

---

## Task routing

| Task | Location |
|------|----------|
| HR behavior specs (PRD) & API contracts | `docs/prd/hr/` |
| Employee PRD & endpoints | `docs/prd/hr/employees/` |
| Calendar PRD & endpoints | `docs/prd/hr/calendar/` |
| Attendance & leave PRD & endpoints | `docs/prd/hr/attendance-leaves/` |
| Payroll operations PRD & endpoints | `docs/prd/hr/payroll/` |
| Payroll settings PRD & endpoints | `docs/prd/hr/payroll/settings/` |
| Reports PRD & endpoints | `docs/prd/hr/reports/` |
| Architecture decisions (ADRs: eager loading, caching, payroll model) | `docs/architecture/decisions/` |
| HR application-layer patterns & refactor notes | `docs/implementation/hr/` |
| Rooms / buildings API docs | `docs/api/rooms/` |
| UserScope & bulk scope assignment | `docs/platform/` |
| Multi-tenancy (tenants, roles) | `docs/multi-tenancy/` |
| Testing guides | `docs/testing/` |
| API testing framework & blueprint | `docs/common/testing/api-testing-guide.md` |
| HR API tests & contract reference | `docs/modules/hr/testing/api-testing.md` |
| Historical doc moves / old structure notes | `docs/meta/` -- **not** current architecture |

When in doubt, search `docs/` for the module name and open the nearest `README.md`.
