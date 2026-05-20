## ADDED Requirements

### Requirement: Maker-checker — approver cannot be the requestor
The system SHALL reject approval or rejection attempts where the acting user is the same as the charge requestor.

#### Scenario: Approve own charge returns error
- **WHEN** `POST /api/app/charges/{chargeId}/approve` is called by the same user who created the charge
- **THEN** response status is 400 or 422 or 500 (not 200)

#### Scenario: Reject own charge returns error
- **WHEN** `POST /api/app/charges/{chargeId}/reject` is called by the same user who created the charge
- **THEN** response status is 400 or 422 or 500 (not 200)

### Requirement: Create with inactive category returns error
The system SHALL refuse to create a charge when the specified `ChargeCategoryId` belongs to an inactive category.

#### Scenario: Inactive category returns business error
- **WHEN** `POST /api/app/charges?academicYearId={id}` is called with a `chargeCategoryId` for a category where `IsActive = false`
- **THEN** response status is 400 or 422 or 500 (not 200)

### Requirement: Create with nonexistent student returns error
The system SHALL return an error when `studentId` does not correspond to any student in the system.

#### Scenario: Nonexistent student returns not-found error
- **WHEN** `POST /api/app/charges?academicYearId={id}` is called with a random `studentId` Guid
- **THEN** response status is 404 or 400 or 500 (not 200)

### Requirement: Create with nonexistent academic year returns error
The system SHALL return an error when `academicYearId` does not exist.

#### Scenario: Nonexistent academic year returns error
- **WHEN** `POST /api/app/charges?academicYearId={randomGuid}` is called
- **THEN** response status is 404 or 400 or 500 (not 200)

### Requirement: Create with zero or negative amount returns validation error
The system SHALL return 400 when `amount <= 0`.

#### Scenario: Zero amount fails validation
- **WHEN** `POST /api/app/charges` is called with `amount = 0`
- **THEN** response status is 400

#### Scenario: Negative amount fails validation
- **WHEN** `POST /api/app/charges` is called with `amount = -1`
- **THEN** response status is 400

### Requirement: Create with empty purposeDescription returns validation error
The system SHALL return 400 when `purposeDescription` is empty or whitespace.

#### Scenario: Empty description fails validation
- **WHEN** `POST /api/app/charges` is called with `purposeDescription = ""`
- **THEN** response status is 400

### Requirement: Approve non-pending charge returns error
The system SHALL reject approval of a charge that is not in Pending status.

#### Scenario: Approve already-approved charge returns error
- **WHEN** a charge is approved, then `POST /api/app/charges/{chargeId}/approve` is called again by a different user
- **THEN** response status is 400 or 422 or 500 (not 200)

### Requirement: Reject non-pending charge returns error
The system SHALL reject rejection of a charge that is not in Pending status.

#### Scenario: Reject already-rejected charge returns error
- **WHEN** a charge is rejected by one user, then `POST /api/app/charges/{chargeId}/reject` is called again by a different user
- **THEN** response status is 400 or 422 or 500 (not 200)

### Requirement: Reject with empty rejection reason returns validation error
The system SHALL return 400 when `rejectionReason` is empty.

#### Scenario: Empty rejection reason fails validation
- **WHEN** `POST /api/app/charges/{chargeId}/reject` is called with `rejectionReason = ""`
- **THEN** response status is 400
