# HR Module -- Technical Reference

> **Audience**: AI agents, solution architects.
> **Namespace root**: `ShikshaMitra.HR`
>
> **See also**: [PRD & API Contracts](../prd/hr/README.md) for behavior specs, business rules, and endpoint reference; [Architecture decisions (ADRs)](../architecture/decisions/README.md) for employee loading/caching and payroll model.

---

## 1. Module Overview

The HR module manages the full employee lifecycle within a multi-tenant educational institution: hiring, profile management, attendance tracking, leave workflows, payroll computation, and calendar/event planning.

### Subdomains

| Subdomain | Namespace | Purpose |
|---|---|---|
| **Employees** | `ShikshaMitra.HR.Employees` | Employee aggregate, family, documents, profile, bank details, work experience, additional details, payroll details, code generation, caching |
| **AttendanceAndLeaves** | `ShikshaMitra.HR.AttendanceAndLeaves` | Daily attendance records, leave requests, leave approval workflow, leave policies, leave types, custom attendance rules, automated notifications |
| **Payroll** | `ShikshaMitra.HR.Payroll` | Payroll structures, salary slips, earning/deduction components, pay elements, professional tax, provident fund, attendance import |
| **Calendar** | `ShikshaMitra.HR.Calendar` | Events, holidays, event types, holiday types, calendar view aggregation |
| ~~**EventAndHolidays**~~ | ~~`ShikshaMitra.HR.EventAndHolidays`~~ | **Deprecated — removed.** The flat `EventAndHoliday` aggregate duplicated `HR/Calendar/`. Consolidated into `Calendar`. Code and DB table to be deleted after data migration completes. |
| **Reports** | (Application only) | PDF/Excel report generation for employee data |

---

## 2. Employees Subdomain

### 2.1 Aggregate: `Employee`

**File**: `Domain/HR/Employees/Aggregates/Employee.cs`
**Base**: `FullAuditedAggregateRoot<Guid>, IMultiTenant`

| Property Group | Fields |
|---|---|
| Identity | `TenantId`, `UserId`, `EmployeeCode` (generated, unique) |
| Personal | `FirstName`, `LastName`, `MiddleName`, `ShortName`, `DateOfBirth`, `GenderType`, `CastType`, `FamilyCast`, `Religion`, `MaritalStatus`, `BloodGroup`, `IdentificationMark1` |
| Contact | `MobileNo`, `SecondaryMobileNo`, `Email`, `Address1-3`, `City`, `State`, `Country`, `PinCode` |
| Employment | `EmployeeStatusType`, `ChangeReason`, `StatusLastChangedAt`, `Department`, `Designation`, `DateOfJoining`, `EmployeeGroup`, `Qualification`, `RoleId`, `ShiftId`, `ReportingManagerId` |
| Profile | `ProfilePictureBlobName` |
| Payroll | `PayrollStructureId` |

**Owned collections** (value objects, same table or owned tables):
- `BankDetails` — `OwnsOne`, stored inline
- `List<WorkExperience>` — `OwnsMany`, stored in owned table
- `List<EmployeeDocument>` — `OwnsMany`, stored in owned table

**Navigation properties**:
- `IdentityRole Role` — FK to ABP Identity
- `PayrollStructure PayrollStructure` — FK to Payroll subdomain
- `ShiftManagement Shift` — FK to TenantSettings
- `ICollection<EmployeeFamilyMember> FamilyMembers` — separate table, 1:N

### 2.2 Related Entities

| Entity | Relationship | Table |
|---|---|---|
| `EmployeeFamilyMember` | 1:N from Employee | Separate table, FK `EmployeeId` |
| `EmployeeAdditionalDetails` | 1:1 from Employee | Separate table, FK `EmployeeId` |
| `EmployeeAttendance` | 1:N from Employee | Separate table, implements `IUserScopedEntity` |
| `EmployeePayrollDetails` | 1:1 from Employee | Separate table with payroll-specific bank/PF data |
| `EmployeePayrollComponent` | 1:N from EmployeePayrollDetails | Earnings/deductions per employee |
| `DocumentSetting` | Standalone config entity | Tenant-scoped document type configuration |

### 2.3 Value Objects

| Value Object | Owner | Storage |
|---|---|---|
| `BankDetails` | Employee | `OwnsOne` — inline columns |
| `WorkExperience` | Employee | `OwnsMany` — owned table |
| `EmployeeDocument` | Employee | `OwnsMany` — owned table |
| `EmployeeDocumentData` | (transient) | In-memory DTO for upload payloads |
| `ContactDetails` | (transient) | Used in family member upsert flow |

### 2.4 Enums (Domain.Shared)

| Enum | Namespace | Values |
|---|---|---|
| `EmployeeStatusType` | `ShikshaMitra.Common` | Active, Inactive, Left, Retired |
| `EmployeeGroupType` | `ShikshaMitra.Common` | (Teaching/Non-teaching classification) |
| `FamilyRelationType` | `ShikshaMitra.HR.Employees` | Father, Mother, Spouse |
| `CastType` | `ShikshaMitra.Common` | General, OBC, SC, ST |
| `GenderType` | `ShikshaMitra.Common` | (Male, Female, Other) |
| `MaritalStatusType` | `ShikshaMitra.Common` | (Single, Married, etc.) |

### 2.5 Domain Services

| Service | File | Responsibility |
|---|---|---|
| `EmployeeManager` | `Managers/EmployeeManager.cs` | Employee lifecycle: create (with user+role), update, status change. Family member CRUD. Work experience + bank details updates. Domain validation (DOB, DOJ cross-field rules). |
| `EmployeeCodeGenerator` | `Services/EmployeeCodeGenerator.cs` | Generates unique employee codes. Format: `{RoleCode3}{Year2}{Base36Sequence4}`. Scoped per tenant+year. Role code from `IdentityRole.ExtraProperties["Code"]` or fallback to first 3 chars of role name. |
| `EmployeeDocumentManager` | `Managers/EmployeeDocumentManager.cs` | Document CRUD via blob storage. Profile picture upload/replace. Delegates file validation to `FileValidationService`. |
| `EmployeeCacheManager` | `Managers/EmployeeCacheManager.cs` | Cache invalidation for employee data. |

### 2.6 Repositories

| Interface | Custom Methods |
|---|---|
| `IEmployeeRepository` | `GetEmployeeByUserIdAsync`, `GetLastEmployeeAsync`, `GetWithDetailsAsync(id, includePayrollStructure)` |
| `IEmployeeFamilyMemberRepository` | `GetByEmployeeIdAsync`, `GetByEmployeeIdAndRelationTypeAsync`, `DeleteByEmployeeIdAsync` |
| `IEmployeeAdditionalDetailsRepository` | `GetByEmployeeIdAsync` |
| `IEmployeeAttendanceRepository` | (standard CRUD) |
| `IEmployeePayrollRepository` | `GetByEmployeeIdAsync`, `GetEarningsByEmployeeIdAsync`, `GetDeductionsByEmployeeIdAsync` |
| `IDocumentSettingRepository` | (standard CRUD) |

### 2.7 Application Services

> **Refactored**: The original `EmployeeAppService` (1 145 lines) was decomposed into focused services.
> All 5 employee controllers now use focused service interfaces instead of `IEmployeeAppService`.
> 28 FluentValidation validators were added across HR Application.Contracts.
> Domain error handling is standardized — all business errors use `BusinessException` with `HRErrorCodes`.
> See [application-layer-refactoring.md](../implementation/hr/application-layer-refactoring.md) and [httpapi-migration.md](../implementation/hr/httpapi-migration.md) for full details.

| App Service | Key Methods |
|---|---|
| `EmployeePersonalInfoAppService` | `CreateAsync`, `GetAsync`, `UpdateAsync`, `GetByUserIdAsync`, `GetWithDetailsAsync`, `UpdateBankDetailsAsync` |
| `EmployeeDocumentAppService` | `UploadDocumentsAsync`, `GetDocumentAsync`, `DeleteDocumentAsync`, `UploadProfilePictureAsync`, `GetProfilePictureAsync` |
| `EmployeePayrollAppService` | `GetPayrollDetailsAsync`, `UpdatePayrollDetailsAsync`, `GetPayrollContractAsync`, `UpdatePayrollContractAsync` |
| `EmployeeSearchAppService` | `GetFilteredListAsync`, `SearchAsync` |
| `EmployeeFamilyInfoAppService` | Family member management (`GetAsync`, `UpsertAsync`) |
| `EmployeeAdditionalDetailsAppService` | Additional details CRUD + PATCH support |
| `EmployeeWorkExperienceAppService` | Work experience CRUD |
| `EmployeeStatusAppService` | Status transitions with domain events |
| `ReportingManagerAppService` | Reporting hierarchy management |
| `DocumentSettingAppService` | Document type configuration per tenant |
| ~~`EmployeeAppService`~~ | **Deprecated** — `IEmployeeAppService` marked `[Obsolete]`, retained for test compatibility |

### 2.8 HTTP Controllers

All controllers delegate to focused service interfaces. Contract mappers live in `HttpApi/Mappers/HR/` (see [httpapi-migration.md](../implementation/hr/httpapi-migration.md)).

| Controller | Route | Service | Purpose |
|---|---|---|---|
| `EmployeesController` | `api/employees` | `IEmployeeSearchAppService` + `IEmployeePersonalInfoAppService` | Employee CRUD, list with pagination/filtering |
| `PersonalInfoController` | `api/employees/{id}/personal-info` | `IEmployeePersonalInfoAppService` | Personal info subset |
| `FamilyInfoController` | `api/employees/{id}/family` | `IEmployeeFamilyInfoAppService` | Family members |
| `AdditionalInfoController` | `api/employees/{id}/additional-info` | `IEmployeeAdditionalDetailsAppService` | Additional details |
| `WorkExperienceController` | `api/employees/{id}/work-experience` | `IEmployeeWorkExperienceAppService` | Work experience |
| `PayrollController` | `api/employees/{id}/payroll` | `IEmployeePayrollAppService` | Employee payroll details |
| `ReportingManagerController` | `api/employees/reporting-manager` | `ReportingManagerAppService` | Reporting hierarchy |
| `EmployeeStatusController` | `api/employees/{id}/status` | `IEmployeeStatusAppService` | Employment status (Active / Inactive / Left / Retired) |
| `DocumentConfigController` | `api/employees/document-config` | `DocumentSettingAppService` | Document settings |

---

## 3. Attendance & Leaves Subdomain

### 3.1 Core Entities

| Entity | Base | Key Fields |
|---|---|---|
| `EmployeeAttendance` | `FullAuditedEntity<Guid>` | `EmployeeId`, `AttendanceDate` (DateOnly), `TimeIn`/`TimeOut` (DateTimeOffset?), `StatusType`, `EducationMediumStageId` (denormalized for scope filtering) |
| `EmployeeLeave` | `FullAuditedAggregateRoot<Guid>` | `PublicId` (auto-increment), `EmployeeId`, `LeaveTypeId` → `LeavePolicy`, `HalfDay`, `StartDate`/`EndDate`, `Status`, `Description`, `RejectionReason`, `Attachment` |

### 3.2 Settings Entities

| Entity | Purpose |
|---|---|
| `LeaveType` | Leave type master (Casual, Sick, etc.) |
| `LeavePolicy` | Leave policy rules: accrual, max balance, carry-forward, probation rules |
| `CustomAttendanceRule` | Tenant-specific attendance rules |
| `AutomatedNotificationsSettings` | Notification triggers for attendance/leave events |

### 3.3 Domain Services

| Service | Responsibility |
|---|---|
| `EmployeeLeaveManager` | Leave request creation, approval/rejection workflow |
| `LeaveValidationService` | Cross-field leave validation (overlap, balance, policy compliance) |
| `LeaveTypeManager` | Leave type CRUD with tenant isolation |
| `LeavePolicyManager` | Policy CRUD, accrual rule management |

### 3.4 Enums

| Enum | Values |
|---|---|
| `EmployeeAttendanceStatusType` | Present, Absent, HalfDayLeave, FullDayLeave, etc. |
| `EmployeeLeaveStatusType` | Pending, Approved, Rejected, Cancelled |
| `LeavePaidType` | Paid, Unpaid |
| `TimeUnitType` | Days, Hours |
| `NotificationMethodType` | Email, SMS, Push |

---

## 4. Payroll Subdomain

### 4.1 Core Entities

| Entity | Purpose |
|---|---|
| `PayrollStructure` | Links Employee → PayElement with amount. Tenant-scoped. |
| `SalarySlip` | Generated salary slip aggregate with computed totals |
| `PayElement` | Master pay element (Earning/Deduction category) |
| `EarningComponent` | Earning component definition (Basic, HRA, DA, etc.) |
| `DeductionComponent` | Deduction component definition (PF, PT, TDS, etc.) |
| `ProfessionalTax` | Tax slab configuration per tenant |
| `ProvidentFund` | PF rules and contribution rates |

### 4.2 Domain Services

| Service | Responsibility |
|---|---|
| `PayrollStructureManager` | Assign/remove payroll structures to employees |
| `SalarySlipManager` | Salary computation and slip generation |
| `EarningComponentManager` | Earning component CRUD |
| `DeductionComponentManager` | Deduction component CRUD |
| `PayElementManager` | Pay element CRUD |
| `EmployeePayrollInitializationService` | Initialize employee payroll on first assignment |

---

## 5. Calendar Subdomain

Calendar is structured as four nested concerns. Each concern follows the canonical domain template (`Aggregates/`, `Entities/`, `ValueObjects/`, `Managers/`, `Services/`, `Repositories/`, `Exceptions/`).

### 5.1 Nested Concerns

| Concern Folder | Aggregates | Purpose |
|---|---|---|
| `Calendar/Events/` | `Event`, `EventStatus` | Calendar event with academic year scoping, recurrence support, linked to an event type |
| `Calendar/EventTypes/` | `EventType` | Event categorization master data (Meeting, Assembly, Sports, etc.) |
| `Calendar/Holidays/` | `Holiday`, `HolidayStatus` | Holiday with date range and holiday type, scoped to academic year |
| `Calendar/HolidayTypes/` | `HolidayType` | Holiday categorization master data (National Holiday, Festival, Declared Holiday, etc.) |

> `EventStatus` and `HolidayStatus` are status types tightly coupled to their respective aggregates. They live in the same `Aggregates/` folder as their aggregate root — they are **not** enums in `Domain.Shared`.

> **Structural fix required**: `HolidayType.cs` currently lives in `Calendar/Aggregates/` and `IHolidayTypeRepository.cs` in `Calendar/Repositories/`. Both must be moved into `Calendar/HolidayTypes/` to be consistent with the nested-concern pattern used by the rest of this sub-module.

### 5.2 Enums (Domain.Shared)

| Enum | Namespace | Purpose |
|---|---|---|
| `EventMode` | `ShikshaMitra.HR.Calendar` | Online, Offline, Hybrid |
| `CalendarItemType` | `ShikshaMitra.HR.Calendar` | Event, Holiday — used for unified calendar view queries |

---

## 6. Reports (Application Layer Only)

| Service | Output |
|---|---|
| `EmployeeReportAppService` | Filtered employee reports |
| `HRPdfService` | QuestPDF-based PDF generation |
| `HRExcelService` | ClosedXML-based Excel export |

---

## 7. Cross-Cutting Concerns

### 7.1 Employee ↔ Identity Integration

- Each `Employee` links to an ABP `IdentityUser` via `UserId`.
- Employee ID = User ID (set explicitly at creation).
- Role assignment: `UserManager.AddToRoleAsync` / `RemoveFromRoleAsync` during employee create/update.
- Username auto-generated: `{firstInitial}{lastName}` with sequence suffix for uniqueness.

### 7.2 User-Scope Filtering

- `EmployeeAttendance` implements `IUserScopedEntity` with denormalized `EducationMediumStageId`.
- Scope filters applied at query time via EF Core global query filters.

### 7.3 File Validation

`FileValidationService` (static, `ShikshaMitra.Common`):
- Profile pictures: max 5MB, extensions `.jpg`/`.jpeg`/`.png`/`.gif`/`.webp`, magic byte verification.
- Documents: max 10MB, extensions `.pdf`/`.jpg`/`.jpeg`/`.png`/`.doc`/`.docx`/`.xls`/`.xlsx`, magic byte verification per extension.

### 7.4 Employee Code Format

Generated by `EmployeeCodeGenerator`:
- Pattern: `{RoleCode}{YearCode}{Sequence}` → e.g., `TEA240001`
- `RoleCode`: 3 chars from `IdentityRole.ExtraProperties["Code"]` or first 3 chars of role name
- `YearCode`: 2-digit year from `DateOfJoining`
- `Sequence`: 4-char base-36 suffix, incremented per tenant+year
- Capacity: 1,679,616 unique codes per role/year/tenant

---

## 8. Data Flow: Employee Creation

```
Controller → EmployeePersonalInfoAppService.CreateAsync
  ├── FluentValidation (auto, via CreateEmployeePersonalInfoDtoValidator)
  ├── BusinessException if email/mobile missing or duplicate
  ├── Generate unique username
  ├── Create IdentityUser + set password (transactional UoW)
  ├── EmployeeManager.CreateEmployeeAsync
  │     ├── Validate user + role existence
  │     ├── EmployeeCodeGenerator.GenerateAsync → employee code
  │     └── new Employee(...)
  ├── Repository.InsertAsync
  ├── UoW.CompleteAsync (commit)
  ├── Publish EmployeeCreatedEto (distributed event)
  └── Send welcome email
```

---

## 9. Database Tables (Employee Subdomain)

| Table | Entity | Relationship |
|---|---|---|
| Employees | `Employee` | Root |
| Employee_BankDetails | `BankDetails` | OwnsOne (inline or shadow table) |
| Employee_WorkExperiences | `WorkExperience` | OwnsMany |
| Employee_Documents | `EmployeeDocument` | OwnsMany |
| EmployeeFamilyMembers | `EmployeeFamilyMember` | 1:N FK to Employee |
| EmployeeAdditionalDetails | `EmployeeAdditionalDetails` | 1:1 FK to Employee |
| EmployeeAttendances | `EmployeeAttendance` | 1:N FK to Employee |
| EmployeePayrollDetails | `EmployeePayrollDetails` | 1:1 FK to Employee |
| EmployeePayrollComponents | `EmployeePayrollComponent` | 1:N FK to EmployeePayrollDetails |
| DocumentSettings | `DocumentSetting` | Standalone, tenant-scoped |

---

## 10. Domain Folder Reference

Target folder structure for the HR Domain layer. Every listed folder must exist; initially empty ones contain `.gitkeep`. Items marked `[MOVE]` require a file to be relocated from its current path to conform to the canonical template.

### Employees

```
HR/Employees/
  Aggregates/           Employee.cs
  Entities/             EmployeeFamilyMember.cs · EmployeeAdditionalDetails.cs
  ValueObjects/         BankDetails.cs · WorkExperience.cs · EmployeeDocument.cs
                        EmployeeDocumentData.cs · ContactDetails.cs
  Managers/             EmployeeManager.cs · EmployeeDocumentManager.cs · EmployeeCacheManager.cs
  Services/             EmployeeCodeGenerator.cs
  Repositories/         IEmployeeRepository.cs · IEmployeeFamilyMemberRepository.cs
                        IEmployeeAdditionalDetailsRepository.cs · IEmployeeAttendanceRepository.cs
  Exceptions/           .gitkeep
  Settings/
    Aggregates/         DocumentSetting.cs
    Repositories/       IDocumentSettingRepository.cs
    Entities/ ValueObjects/ Managers/ Services/ Exceptions/   ← .gitkeep
  Payroll/
    Aggregates/         EmployeePayrollDetails.cs
    ValueObjects/       EmployeePayrollComponent.cs
    Services/           EmployeePayrollInitializationService.cs
    Repositories/       IEmployeePayrollRepository.cs
    Entities/ Managers/ Exceptions/   ← .gitkeep
```

### AttendanceAndLeaves

```
HR/AttendanceAndLeaves/
  Aggregates/           EmployeeLeave.cs [MOVE] · EmployeeAttendance.cs [MOVE]
  Entities/             NotificationInfo.cs [MOVE from NotificationHistories/]
  ValueObjects/         .gitkeep
  Managers/             EmployeeLeaveManager.cs [MOVE]
  Services/             LeaveValidationService.cs [MOVE]
  Repositories/         IEmployeeLeaveRepository.cs [MOVE]
  Exceptions/           .gitkeep
  Settings/
    Aggregates/         LeaveType.cs · LeavePolicy.cs · CustomAttendanceRule.cs
                        AutomatedNotificationsSettings.cs [MOVE from Settings/ root]
    Managers/           LeaveTypeManager.cs · LeavePolicyManager.cs
    Repositories/       ILeaveTypeRepository.cs · ILeavePolicyRepository.cs
                        ICustomAttendanceRuleRepository.cs · IAutomatedNotificationsRepository.cs
    Entities/ ValueObjects/ Services/ Exceptions/   ← .gitkeep
```

### Payroll

```
HR/Payroll/
  SalarySlips/
    Aggregates/         SalarySlip.cs
    Managers/           SalarySlipManager.cs
    Repositories/       ISalarySlipRepository.cs
    Entities/ ValueObjects/ Services/ Exceptions/   ← .gitkeep
  Settings/
    Aggregates/         EarningComponent.cs · DeductionComponent.cs · ProvidentFund.cs
                        ProfessionalTax.cs · PayElement.cs
    Managers/           EarningComponentManager.cs · DeductionComponentManager.cs · PayElementManager.cs
    Repositories/       IEarningComponentRepository.cs · IDeductionComponentRepository.cs
                        IProvidentFundRepository.cs · IProfessionalTaxRepository.cs · IPayElementRepository.cs
    Entities/ ValueObjects/ Services/ Exceptions/   ← .gitkeep
  PayrollStructures/
    Aggregates/         PayrollStructure.cs
    Managers/           PayrollStructureManager.cs
    Repositories/       IPayrollStructureRepository.cs
    Entities/ ValueObjects/ Services/ Exceptions/   ← .gitkeep
```

### Calendar

```
HR/Calendar/
  Events/
    Aggregates/         Event.cs · EventStatus.cs
    Repositories/       IEventRepository.cs
    Entities/ ValueObjects/ Managers/ Services/ Exceptions/   ← .gitkeep
  EventTypes/
    Aggregates/         EventType.cs
    Repositories/       IEventTypeRepository.cs
    Entities/ ValueObjects/ Managers/ Services/ Exceptions/   ← .gitkeep
  Holidays/
    Aggregates/         Holiday.cs · HolidayStatus.cs
    Repositories/       IHolidayRepository.cs
    Entities/ ValueObjects/ Managers/ Services/ Exceptions/   ← .gitkeep
  HolidayTypes/                           ← CREATE this sub-concern
    Aggregates/         HolidayType.cs [MOVE from Calendar/Aggregates/]
    Repositories/       IHolidayTypeRepository.cs [MOVE from Calendar/Repositories/]
    Entities/ ValueObjects/ Managers/ Services/ Exceptions/   ← .gitkeep
```

### EventAndHolidays — Migration Artifact

`HR/EventAndHolidays/` must be deleted. Steps:

1. Map each `EventAndHoliday` record: if `IsHoliday = true` → seed into `Holiday`; if `false` → seed into `Event`.
2. Remove the EF Core entity configuration and the corresponding migration.
3. Drop the `DbSet<EventAndHoliday>` from `ShikshaMitraDbContext`.
4. Delete the `HR/EventAndHolidays/` folder from the Domain project.
