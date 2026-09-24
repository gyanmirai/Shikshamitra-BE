# Agent / contributor guide

**ShikshaMitra** is a multi-tenant **school management / CRM backend** built with **ABP Framework** and **.NET 9**.

## Publishing approval

Commits and feature-branch pushes for authorized work do not require a separate permission request.
Always obtain explicit user approval before merging a pull request into `main`.
Approval to push is not approval to merge; do not bypass this requirement with a direct push to `main`.
Approval applies only to the specific pull requests and scope the user approved.

## GitHub Actions policy

GitHub Actions jobs must run only for `refs/heads/main` to conserve credits.
Only pushes to `main`, normally after a merge, may start CI and deployment.
Do not add pull-request, feature-branch, tag, scheduled, or manual-dispatch triggers.
Every job, including reusable workflow callers and called jobs, must have `if: github.ref == 'refs/heads/main'` to reject calls from every other ref.
Keep optional validation workflows reusable-only; do not add standalone triggers.
Keep security and release gates on `main`, and run relevant checks locally before merging feature work.
Do not restore PR build requirements or broaden workflow triggers without explicit user authorization.

## Start here

Before changing permissions, navigation guards, identity, scope, or security-related seeders, read [the canonical security standard](docs/common/security/permission-consistency.md).
Follow its mandatory checklist, run security checks locally before merge, and require the reusable Security Regression gate on `main` before deployment.
Never restore revoked grants through reseeding or treat screenshots as security sign-off.
Use the .NET 9 SDK selected by `global.json` for local, CI, and deployment builds.
Do not remove the SDK selection to make a missing installation pass; install the matching SDK instead.
Keep `Enumerable.Contains` explicit for captured arrays inside EF expression trees so newer span overloads cannot change query behavior.

1. **[docs/README.md](docs/README.md)** -- documentation map and folder taxonomy.
2. **[docs/best-practices.md](docs/best-practices.md)** -- canonical engineering rules (DDD layers, folders, permissions). Prefer linking here instead of duplicating standards.

---

## Agentic documentation practice

- Trace source code before changing module docs.
- Backend docs own product behavior, API contracts, permissions, errors, domain invariants, data integrity, and backend validation.
- Frontend docs own routes, screen folders, client wiring, cache behavior, UI workflows, browser checks, and frontend validation.
- Link to sibling frontend docs instead of copying frontend route or client details.
- Use `references/` only for active deep notes.
- Do not add placeholder reference pages, old plans, generated exports, or phase notes.
- Use `.agents/skills/shikshamitra-module-docs/SKILL.md` for repeatable module-doc updates.
- Run docs validators before handing off docs changes.

---

## Tech stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 9, C# 14, file-scoped namespaces, primary constructors |
| Framework | ABP Framework (DDD template), OpenIddict for auth |
| ORM | Entity Framework Core (code-first migrations) |
| Database | PostgreSQL/Npgsql (multi-tenant, per-tenant data isolation via ABP filters) |
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

Run commands from the `Shikshamitra-BE` repo root. If you opened the wrapper checkout at `C:\Repo\ShikshaMitra`, `git` will fail there because the wrapper itself is not the repository.

```bash
# Restore + build
dotnet restore
dotnet build

# Windows fallback when a running host locks shared outputs
dotnet build --artifacts-path .artifacts/local-build

# Run database migrations (requires ConnectionStrings__Default)
cd src/ShikshaMitra.DbMigrator && dotnet run

# Run the API host
cd src/ShikshaMitra.HttpApi.Host && dotnet run

# Run tests
dotnet test
```

## Publish / branch sync checks

- Start from `C:\Repo\ShikshaMitra\Shikshamitra-BE` and confirm the current branch and dirty state with `git status --short --branch`.
- Confirm GitHub CLI auth with `gh auth status` before relying on PR commands.
- When syncing `main` while preserving tracked local edits, use `git pull --autostash origin main`.
- After merge-based syncs, run `git diff --name-only --diff-filter=U` to prove no unresolved files remain.
- Run `git diff --check` before commit or PR creation to catch whitespace errors and conflict markers.
- If local runtime log noise appears, prove it is ignored with `git check-ignore -v Logs/logs.txt` instead of guessing.

### Required environment / secrets

| Variable | Purpose |
|----------|---------|
| `ConnectionStrings__Default` | PostgreSQL connection string |
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
- Keep ABP tenant filters enabled and validate tenant ownership at authorization boundaries.
  Explicit tenant predicates are appropriate in focused authorization projections and must agree with the current server tenant.
- Scope hierarchy: **Board → Medium → Stage → Class → Division**.
- A null dimension within an authorized scope assignment is a wildcard only within that assignment's boundaries.
  Missing assignments, missing identity, or database failures must never grant unrestricted access.
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
- **Current coverage**: HR module has focused app-service and API tests.
- Other module coverage changes quickly, so inspect the relevant `test/` folder before claiming a gap.

```bash
# App service tests
dotnet test --filter "Project=ShikshaMitra.Application.Tests"

# Windows fallback when test/build outputs are locked by another local host
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "Tier=Critical" --artifacts-path .artifacts/test-critical

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
| HR documentation landing page | `docs/modules/hr/README.md` |
| Module documentation standard | `docs/common/standards/module-docs-standard.md` |
| HR product behavior | `docs/modules/hr/product.md` |
| HR API contracts, endpoints, and permissions | `docs/modules/hr/api.md` |
| HR backend architecture and source paths | `docs/modules/hr/architecture.md` |
| HR end-to-end workflows | `docs/modules/hr/workflows.md` |
| HR backend test commands and gates | `docs/modules/hr/testing.md` |
| HR FE to BE permission mapping | `docs/modules/hr/references/permissions.md` |
| HR policy architecture | `docs/modules/hr/references/policy-architecture.md` |
| Architecture decisions (ADRs: eager loading, caching, payroll model) | `docs/architecture/decisions/` |
| Rooms / buildings API docs | `docs/modules/institute-setup/api.md` |
| UserScope & bulk scope assignment | `docs/common/platform/` |
| Multi-tenancy (tenants, roles) | `docs/common/multi-tenancy/` |
| Testing guides | `docs/common/testing/` |
| API testing framework & blueprint | `docs/common/testing/api-testing-guide.md` |
| Backend docs CI validation workflow | `.github/workflows/docs-validation.yml` |
| HR docs validation | `powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module hr` |
| Backend docs full local gate | `powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1` |
| Backend all-module docs gate | `powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -AllModules` |
| Backend docs stale-review gate | `powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -AllModules -MaxAgeDays 365` |

When in doubt, search `docs/` for the module name and open the nearest `README.md`.
