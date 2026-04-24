# Payroll Settings -- Behavior Specification

## Purpose

Manages the tenant-scoped payroll master configuration used by payroll runs and salary structures. The current API surface covers day calculation settings, earning components, deduction components, provident fund (PF) rules, and pay elements. Professional tax configuration exists at the domain layer but is not yet exposed via an API controller.

## Actors

| Actor | Description |
|---|---|
| Admin | Configures payroll settings for an academic year and maintains salary components, PF rules, and pay elements |
| System | Reads payroll settings during payroll preview, payroll lock, and downstream salary computations |

---

## Behaviors

### 1. Day Calculation Settings

1. Admin saves payroll day calculation settings via the HTTP API (request body: `dayCalculationType`, optional `manualDays`). The aggregate is **tenant-scoped** today; academic-year scoping in this document is a forward-looking alignment goal when the API and model add `academicYearId`.
2. System supports a primary calculation mode through `dayCalculationType`.
3. Supported modes are:
   - `working-days` (0) — Mon–Fri days in the payroll month minus tenant holidays that fall on weekdays (denominator for LOP proration).
   - `total-month-days` (1) — calendar days in the month (`DateTime.DaysInMonth`); holidays are **not** subtracted in this mode.
   - `manual-days` (2) — admin-specified denominator (1–31).
4. When `working-days` is selected, no extra day count is required.
5. When `manual-days` is selected, a manual day count must be provided and remain within the allowed month range.
6. Successful save returns a simple success response. The active row is read during **payroll preview**, **payroll lock**, and **salary slip generation** to compute basis days and LOP proration (see payroll operations PRD). If no row exists, the system defaults to **`working-days`** behaviour.

### 2. Earning Components

1. Admin creates, updates, lists, retrieves, and deletes earning components.
2. All list and create operations are scoped by `academicYearId`.
3. Each component includes:
   - `earningComponentName`
   - `description`
   - `earningType` — `Fixed` (0) or `Variable` (1)
   - `calculationType` — `Amount` (0) or `Percentage` (1)
   - `amount`
   - `percentage`
   - `selectedEarningComponentId` — references another component when `ComponentDependency` is `CustomComponent`
   - `visibleInPayslip`
4. Components may reference another configured component through `selectedEarningComponentId`.
5. List responses are paginated and support search.
6. Single-item retrieval returns the component plus any nested custom-component metadata needed by the client.

### 3. Deduction Components

1. Admin creates, updates, lists, retrieves, and deletes deduction components.
2. All list and create operations are scoped by `academicYearId`.
3. Deduction components follow the same overall interaction model as earning components, with deduction-specific field names and response DTOs.
4. List responses are paginated and support search.
5. Single-item retrieval returns deduction component details plus nested custom-component metadata when applicable.

### 4. PF Rules

1. Admin creates, updates, lists, and deletes PF rules for an academic year.
2. All list and create operations are scoped by `academicYearId`.
3. Each PF rule contains:
   - `name`
   - `code` — unique, uppercase letters and underscores only
   - `employeePercentage` — 0–100
   - `employerPercentage` — 0–100
4. Create returns the created rule identity and creation timestamp.
5. List responses are paginated (page 1-based, pageSize 10–100) and provide created/updated timestamps for operational traceability.

### 5. Pay Elements

1. Admin creates, updates, lists, retrieves, and deletes pay elements.
2. Each pay element contains:
   - `code` — unique identifier code
   - `name` — human-readable name
   - `type` — `Credit` (0) or `Debit` (1)
   - `isEmployeeSpecific` — whether amounts differ per employee
   - `isDependent` — whether this element depends on another
   - `dependent` — if dependent, includes `elementId`, optional `percentage`, optional `amount`
   - `isVisibleInPaySlip`
3. List responses use standard ABP pagination (`PagedAndSortedResultRequestDto`).
4. No specific permission attributes beyond class-level `[Authorize]`.

### 6. Professional Tax (Not Yet Exposed)

1. Domain model (`ProfessionalTax` aggregate), app service (`ProfessionalTaxAppService`), repository, and DTOs exist.
2. Supports slab-based tax configuration by:
   - `genderType` — Male/Female/Other
   - `fromSalary` / `toSalary` — salary range for the slab
   - `taxAmount` — tax amount for the slab
3. **No HTTP controller has been created yet.** This feature is ready for API exposure when needed.

---

## Business Rules

- Payroll settings are tenant-isolated and require bearer auth plus tenant context.
- Academic-year-scoped endpoints must receive a valid `academicYearId`.
- Day calculation mode must be one of the supported values from the API contract.
- Manual day count is only valid when the selected day calculation mode requires it.
- Earning and deduction component list endpoints support pagination and optional search.
- Component payloads may include both direct value fields and relational fields such as `selectedEarningComponentId` or `selectedDeductionComponentId`.
- PF rule percentages must remain valid numeric percentages.
- Mutations rely on request validation and standard API error responses for invalid payloads or missing resources.

---

## Permissions

| Permission Area | View | Add/Update | Delete |
|---|---|---|---|
| Day Calculation | — | `PayrollManagement_SettingsDayCalculation.Add` | — |
| Earning Components | `PayrollManagement_SettingsEarningComponents.View` | `PayrollManagement_SettingsEarningComponents.Add` | `PayrollManagement_SettingsEarningComponents.Delete` |
| Deduction Components | `PayrollManagement_SettingsDeductionComponents.View` | `PayrollManagement_SettingsDeductionComponents.Add` | `PayrollManagement_SettingsDeductionComponents.Delete` |
| PF Rules | `PayrollManagement_SettingsPfRule.View` | `PayrollManagement_SettingsPfRule.Add` | `PayrollManagement_SettingsPfRule.Delete` |
| Pay Elements | `[Authorize]` (class-level only) | `[Authorize]` (class-level only) | `[Authorize]` (class-level only) |

Note: Pay Elements currently use class-level authorization only — no granular permission keys are enforced.

---

## Error Handling

- Validation failures return `400 Bad Request`.
- Unauthorized access returns `401 Unauthorized`.
- Missing payroll setting resources return `404 Not Found` where applicable.
- Forbidden responses may be returned for admin-only flows such as restricted PF rule operations.

---

## Dependencies

- [Payroll Operations](../prd.md) -- consumes payroll settings during payroll preview and lock flows
- [Employees](../../employees/prd.md) -- employee payroll structures depend on configured salary components and PF rules
- [Canonical curated contract](../../../contracts/curated/payroll-settings-api.md)
- [Raw contract source](../../../contracts/raw/payroll-settings-api.raw.2026-03-28.md)
- [Implementation Reference](../../../../hr/payroll/README.md) -- full endpoint-level documentation with request/response examples
