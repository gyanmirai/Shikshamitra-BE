# Payroll Settings -- Behavior Specification

## Purpose

Manages the tenant-scoped payroll master configuration used by payroll runs and salary structures. The current API surface covers day calculation settings, earning components, deduction components, and provident fund (PF) rules.

## Actors

| Actor | Description |
|---|---|
| Admin | Configures payroll settings for an academic year and maintains salary components and PF rules |
| System | Reads payroll settings during payroll preview, payroll lock, and downstream salary computations |

---

## Behaviors

### 1. Day Calculation Settings

1. Admin saves payroll day calculation settings for an academic year using `academicYearId`.
2. System supports a primary calculation mode through `dayCalculationType`.
3. Supported modes are:
   - `working-days`
   - `total-month-days`
   - `manual-days`
4. When `working-days` is selected, no extra day count is required.
5. When `manual-days` is selected, a manual day count must be provided and remain within the allowed month range.
6. Successful save returns a simple success response and becomes the active calculation basis for payroll processing in that academic year.

### 2. Earning Components

1. Admin creates, updates, lists, retrieves, and deletes earning components.
2. All list and create operations are scoped by `academicYearId`.
3. Each component includes:
   - `earningComponentName`
   - `description`
   - `earningType`
   - `calculationType`
   - `amount`
   - `percentage`
   - `selectedEarningComponentId`
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
   - `code`
   - `employeePercentage`
   - `employerPercentage`
4. Create returns the created rule identity and creation timestamp.
5. List responses are paginated and provide created/updated timestamps for operational traceability.

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

| Permission Area | Description |
|---|---|
| Payroll settings configuration | Required for day calculation settings changes |
| Earning components | View and manage earning component definitions |
| Deduction components | View and manage deduction component definitions |
| PF rules | View and manage provident fund rule definitions |

Note: Exact permission keys should be taken from the application layer and source contract usage where enforced.

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
