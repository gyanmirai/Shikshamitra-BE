## ADDED Requirements

### Requirement: Approve pending charge transitions status and records approver
The system SHALL approve a pending charge when called by a user other than the requestor, setting status to Approved and recording the approver identity.

#### Scenario: Approve pending charge succeeds
- **WHEN** `POST /api/app/charges/{chargeId}/approve` is called by a different user than the requestor
- **THEN** response status is 200, `data.status` equals `1` (Approved), `data.approvedBy` is non-empty, and `data.approvedOn` is set

### Requirement: Reject pending charge transitions status and records reason
The system SHALL reject a pending charge when called by a user other than the requestor, recording the rejection reason.

#### Scenario: Reject pending charge succeeds
- **WHEN** `POST /api/app/charges/{chargeId}/reject` is called with a non-empty `rejectionReason` by a different user
- **THEN** response status is 200, `data.status` equals `2` (Rejected), `data.rejectedBy` is non-empty, and `data.rejectionReason` matches the input

### Requirement: Update pending charge modifies allowed fields
The system SHALL apply partial updates to a charge in Pending status and return the updated values.

#### Scenario: Update pending charge succeeds
- **WHEN** `PUT /api/app/charges/{chargeId}` is called with a new `amount` value on a Pending charge
- **THEN** response status is 200 and `data.amount` reflects the updated value

#### Scenario: Update approved charge fails
- **WHEN** `PUT /api/app/charges/{chargeId}` is called on an Approved charge
- **THEN** response status is 400 or 422 or 500 (not 200)

### Requirement: Delete pending charge removes it from the system
The system SHALL delete a charge in Pending status and return 204 No Content.

#### Scenario: Delete pending charge succeeds
- **WHEN** `DELETE /api/app/charges/{chargeId}` is called for a Pending charge
- **THEN** response status is 204 and a subsequent `GET /api/app/charges/{chargeId}` returns an error

#### Scenario: Delete approved charge fails
- **WHEN** `DELETE /api/app/charges/{chargeId}` is called for an Approved charge
- **THEN** response status is 400 or 422 or 500 (not 204)

### Requirement: GetAll filters by boardId
The system SHALL apply `boardId` query filter so only charges for that board are returned.

#### Scenario: Filter by boardId narrows result
- **WHEN** `GET /api/app/charges?academicYearId={id}&boardId={boardId}` is called
- **THEN** response status is 200 and all returned items have `boardId` matching the filter

### Requirement: GetAll filters by educationMediumId
The system SHALL apply `educationMediumId` filter to narrow charge results.

#### Scenario: Filter by educationMediumId returns matching items
- **WHEN** `GET /api/app/charges?academicYearId={id}&educationMediumId={mediumId}` is called
- **THEN** response status is 200 and all returned items have `educationMediumId` matching the filter

### Requirement: GetAll pageSize is silently capped at 100
When `pageSize > 100` is requested, the system SHALL return at most 100 items and report `pageSize = 100`.

#### Scenario: PageSize over 100 is capped
- **WHEN** `GET /api/app/charges?academicYearId={id}&pageSize=200` is called
- **THEN** response status is 200 and `pageSize` in response equals 100

### Requirement: GetAll pagination returns correct page
The system SHALL support page-based navigation returning the correct slice of results.

#### Scenario: Page 2 returns empty when only one page of data exists
- **WHEN** fewer than `pageSize` charges exist and `GET .../charges?page=2&pageSize=50` is called
- **THEN** response status is 200 and `items` array is empty
