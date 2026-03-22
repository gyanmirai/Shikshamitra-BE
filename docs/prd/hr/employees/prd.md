# Employees -- Behavior Specification

## Purpose

Manages the full employee lifecycle within a multi-tenant school: onboarding, profile data (personal, family, work experience, additional details), document management, bank/payroll details, status transitions, search/filter, and reporting hierarchy.

## Actors

| Actor | Description |
|-------|-------------|
| Admin | Creates employees, manages all profile data, assigns reporting managers, configures document settings |
| Employee | Views own profile (when ownership check passes) |
| System | Generates employee codes, creates identity users, publishes domain events |

---

## Behaviors

### 1. Employee Creation (Personal Info)

1. Admin submits personal info (name, DOB, contact, role, shift, address).
2. System validates via `CreateEmployeePersonalInfoDtoValidator` (FirstName required, mobile format `^[6-9]\d{9}$`, age >= 18 at DOJ, DOJ <= 1 year in future).
3. System generates a unique username from `{firstInitial}{lastName}` with sequence suffix for uniqueness.
4. System creates an `IdentityUser` with a default password inside a transactional UoW.
5. `EmployeeManager.CreateEmployeeAsync` validates the user and role exist, generates an employee code via `EmployeeCodeGenerator`, and creates the Employee aggregate (Employee.Id = User.Id).
6. System publishes `EmployeeCreatedEto` (distributed event).
7. Returns the created employee with code.

### 2. Employee Update (Personal Info)

1. Admin submits updated personal info for an existing employee.
2. If `RoleId` changed, system removes the user from the old role and assigns the new one. Failures produce `RoleRemovalFailed` / `RoleAssignmentFailed`.
3. Domain validates DOB < today (`DobInFuture`) and DOJ > DOB (`DojBeforeDob`).

### 3. Family Info

1. Admin upserts father, mother, and/or spouse details for an employee.
2. If a family member's name is null/empty, system deletes the existing member.
3. Validation: Name required (`FamilyMemberNameRequired`), Name <= 256 (`FamilyMemberNameTooLong`), ContactNo <= 20 (`FamilyContactTooLong`), PinCode <= 10 (`FamilyPincodeTooLong`).

### 4. Work Experience

1. Admin replaces the entire work experience list for an employee.
2. Each entry includes organization, designation, department, location, date range.

### 5. Additional Details

1. Admin upserts supplementary info: BPL card number, disability status, second language, library/transport membership.
2. Stored as a separate entity with 1:1 relationship to Employee.
3. Supports both full upsert and partial PATCH.

### 6. Bank Details

1. Admin updates bank information (bank name, branch, account holder, account number, IFSC).
2. Validation: Account number `^\d{9,18}$`, IFSC `^[A-Z]{4}0[A-Z0-9]{6}$`.

### 7. Employee Payroll

1. Admin retrieves/updates employee-level payroll: bank details, salary details (PF rule, UAN, basic salary, TDS), earning/deduction component amounts.
2. Sensitive field access requires `CheckSensitiveFieldAccessAsync`.
3. TDS percentage must be 0-100; component amounts must be >= 0.

### 8. Document Configuration (Settings)

1. Admin CRUDs document type settings per tenant (title, allowed sizes, allowed file types).
2. Title must match `^[A-Za-z ]+$`; size 1-20 MB; at least one file type required.

### 9. Employee Documents (Upload/Download)

1. Admin uploads documents for an employee. System validates via `FileValidationService` (max 10 MB, allowed extensions: pdf/jpg/jpeg/png/doc/docx/xls/xlsx, magic byte verification).
2. Documents stored as blobs: `employee-documents/{employeeId}/{documentId}_{sanitizedFileName}`.
3. Profile picture: max 5 MB, extensions jpg/jpeg/png/gif/webp, stored as `profile-pictures/{employeeId}_{guid}_{sanitizedFileName}`.

### 10. Status Management

1. Admin changes employee status (Active, Inactive, Left, Retired).
2. System publishes `EmployeeStatusChangedEto` (distributed event).

### 11. Search and Filter

1. List: paginated (page 1-based, pageSize 10-50), filterable by `mediumStageId`, `status`, `isTeaching`, `roleId`.
2. Search: minimum 3 characters, returns up to 50 results.

### 12. Reporting Manager

1. Admin assigns a reporting manager to an employee.
2. Admin can query the reporting manager for an employee.
3. Admin can list all employees under a given manager.

---

## Business Rules

- Employee.Id == IdentityUser.Id (set explicitly at creation).
- Employee code format: `{RoleCode3}{Year2}{Base36Sequence4}` (e.g., `TEA240001`). Capacity: 1,679,616 per role/year/tenant.
- `RoleCode` from `IdentityRole.ExtraProperties["Code"]` or first 3 chars of role name.
- Age must be >= 18 at date of joining.
- DOB must not be in the future; DOJ must be after DOB.
- DOJ must not be more than 1 year in the future (on create).
- Mobile number (India): `^[6-9]\d{9}$`.
- IFSC code (India): `^[A-Z]{4}0[A-Z0-9]{6}$`.
- Multi-tenancy: all data is tenant-isolated.
- Cache: employee data is cached (15-min TTL); PUT operations auto-invalidate.

---

## Permissions

| Permission Key | Description |
|----------------|-------------|
| `HR.Employee.EmployeeList.View` | View employee list and details |
| `HR.Employee.EmployeeList.Add` | Create / update employees |
| `HR.Employee.EmployeeList.Delete` | Delete employees |
| `HR.Employee.ICard.View` | View employee ID cards |
| `HR.Employee.ICard.Add` | Create ID cards |
| `HR.Employee.ICard.Delete` | Delete ID cards |
| `HR.Employee.Setting.DocumentTypes.View` | View document type settings |
| `HR.Employee.Setting.DocumentTypes.Add` | Create/update document types |
| `HR.Employee.Setting.DocumentTypes.Delete` | Delete document types |
| `HR.Employee.ReportingManager.View` | View reporting manager assignments |
| `HR.Employee.ReportingManager.Update` | Assign/change reporting manager |
| `HR.Employee.AdditionalDetails.View` | View additional details |
| `HR.Employee.AdditionalDetails.Update` | Update additional details |

Additionally, programmatic authorization checks:

| Check | Purpose |
|-------|---------|
| `CheckEmployeeAccessAsync` | IDOR protection on read |
| `CheckEmployeeModifyAccessAsync` | IDOR protection on write |
| `CheckSensitiveFieldAccessAsync` | Guard salary/role fields |

---

## Error Codes

| Code | Constant | When |
|------|----------|------|
| `HR:Employee:010001` | EmployeeEmailRequired | Email missing on create |
| `HR:Employee:010002` | EmployeeMobileRequired | Mobile missing on create |
| `HR:Employee:010003` | EmployeeMobileTooShort | Mobile number too short |
| `HR:Employee:010004` | EmployeeEmailAlreadyExists | Duplicate email in tenant |
| `HR:Employee:010005` | EmployeeUsernameGenerationFailed | Could not generate unique username |
| `HR:Employee:010006` | EmployeeUserCreationFailed | IdentityUser creation failed |
| `HR:Employee:010007` | EmployeeUserVerificationFailed | User verification failed |
| `HR:Employee:010008` | EmployeeNotFound | Employee ID not found |
| `HR:Employee:010009` | InvalidGenderValue | Invalid gender enum |
| `HR:Employee:010010` | InvalidCasteTypeValue | Invalid caste enum |
| `HR:Employee:010011` | InvalidMaritalStatusValue | Invalid marital status enum |
| `HR:Employee:010012` | InvalidBloodGroupValue | Invalid blood group enum |
| `HR:Employee:010013` | UserNotFound | IdentityUser not found |
| `HR:Employee:010014` | RoleNotFound | IdentityRole not found |
| `HR:Employee:010015` | RoleNameMissing | Role.Name is null |
| `HR:Employee:010016` | RoleAssignmentFailed | Could not add user to role |
| `HR:Employee:010017` | RoleRemovalFailed | Could not remove user from role |
| `HR:Employee:010018` | DobInFuture | Date of birth is in the future |
| `HR:Employee:010019` | DojBeforeDob | Date of joining is before DOB |
| `HR:Employee:010020` | FamilyMemberNameRequired | Family member name is empty |
| `HR:Employee:010021` | FamilyMemberNameTooLong | Family member name > 256 chars |
| `HR:Employee:010022` | FamilyContactTooLong | Family contact > 20 chars |
| `HR:Employee:010023` | FamilyPincodeTooLong | Family pincode > 10 chars |
| `HR:Employee:010101` | EmployeeDocumentNotFound | Document ID not found |
| `HR:Employee:010201` | EmployeePayrollStructureRequired | Payroll structure not assigned |
| `HR:Employee:010202` | EmployeePayrollDetailsRequired | Payroll details missing |

---

## Domain Events

| Event | Trigger | Payload |
|-------|---------|---------|
| `EmployeeCreatedEto` | Employee created | EmployeeId, EmployeeCode, FullName, Email, TenantId, CreatedAt |
| `EmployeeStatusChangedEto` | Status transition | EmployeeId, PreviousStatus, CurrentStatus, TenantId, ChangedAt |

---

## Dependencies

- [Calendar](../calendar/prd.md) -- academic year scoping
- [Payroll](../payroll/prd.md) -- payroll structure assignment
- [Attendance & Leaves](../attendance-leaves/prd.md) -- attendance records linked by EmployeeId
- ABP Identity -- user/role management
- Blob Storage -- document and profile picture storage
