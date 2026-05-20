# Capability Spec: Finance Fees — Student Charges

## Overview
Student Charges let finance users apply an ad-hoc monetary charge to a single student for a given academic year, optionally requiring approval based on charge category settings.

### Definitions
- **Charge Category**: a tenant+academic-year scoped setting that may require approval (`IsApprovalRequired`) and may be active/inactive (`IsActive`).
- **Maker (Requestor)**: user who creates the charge.
- **Checker (Approver/Rejector)**: user who approves/rejects; must be different from maker.

---

## ADDED Requirements

### Requirement: CHG-001 — Create a student charge
The system MUST allow an authorized user to create a charge for a student for a given Academic Year.

- Inputs: `AcademicYearId`, `StudentId`, `ChargeCategoryId`, `Amount`, `DueDate`, `Priority`, `PurposeDescription`, optional `AdditionalNotes`.
- The system MUST persist a snapshot of the student’s academic scope (Board/Medium/Standard/Division) at creation time.

#### Scenario: Create charge when category does not require approval
- **WHEN** a maker creates a charge for a category where `IsApprovalRequired = false`
- **THEN** the charge status is `Approved`
- **THEN** the charge is included in payable/overdue calculations

#### Scenario: Create charge when category requires approval
- **WHEN** a maker creates a charge for a category where `IsApprovalRequired = true`
- **THEN** the charge status is `Pending`
- **THEN** the charge is excluded from payable/overdue calculations until approved

---

### Requirement: CHG-002 — Derive student academic scope from StudentId (anti-tamper)
The system MUST derive `BoardId`, `EducationMediumId`, `StandardId`, and `DivisionId` from the Student record.

#### Scenario: Client sends mismatching scope IDs
- **WHEN** the client provides Board/Medium/Standard/Division IDs that do not match the student
- **THEN** the request is rejected with a validation/business error

---

### Requirement: CHG-003 — Charge lifecycle: approve / reject
The system MUST support approval workflow for pending charges.

- Only `Pending` charges can transition.
- Approve transitions `Pending → Approved`.
- Reject transitions `Pending → Rejected`.

#### Scenario: Approve a pending charge
- **WHEN** a checker approves a `Pending` charge with optional approval notes
- **THEN** status becomes `Approved`
- **THEN** approver identity and timestamp are captured

#### Scenario: Reject a pending charge
- **WHEN** a checker rejects a `Pending` charge with a mandatory rejection reason
- **THEN** status becomes `Rejected`
- **THEN** rejector identity and timestamp are captured

---

### Requirement: CHG-004 — Maker-checker enforcement
The system MUST prevent the maker from approving or rejecting their own charge.

#### Scenario: Maker attempts to approve own charge
- **WHEN** a user attempts to approve/reject a charge they requested
- **THEN** the request is rejected with an authorization/business error

---

### Requirement: CHG-005 — Fee collection integration
Only `Approved` charges MUST contribute to:
- Student dues totals
- Overdue calculations (due date in the past and unpaid)

#### Scenario: Pending charge is not included in dues
- **WHEN** a student has a `Pending` charge
- **THEN** dues/overdue calculations exclude that charge

---

### Requirement: CHG-006 — Validation
The system MUST validate:
- Amount > 0
- DueDate is not in the past (UTC date)
- PurposeDescription length within bounds
- Rejection reason minimum length

#### Scenario: Reject invalid due date
- **WHEN** a maker creates a charge with a due date in the past
- **THEN** the request is rejected
