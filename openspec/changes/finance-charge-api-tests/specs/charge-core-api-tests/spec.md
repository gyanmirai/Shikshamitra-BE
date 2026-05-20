## ADDED Requirements

### Requirement: Create charge with approval-required category returns Pending status
When a charge is created for a student using a category with `IsApprovalRequired = true`, the system SHALL return `status = 0` (Pending) in the response and populate all charge fields.

#### Scenario: Create charge with approval-required category
- **WHEN** `POST /api/app/charges?academicYearId={id}` is called with a valid student and active category (`IsApprovalRequired = true`)
- **THEN** response status is 200, `data.status` equals `0` (Pending), `data.chargeId` is non-empty, and `data.studentId` matches the input

#### Scenario: Create charge response shape is complete
- **WHEN** a charge is created successfully
- **THEN** response contains `data.boardId`, `data.educationMediumId`, `data.standardId`, `data.divisionId`, `data.studentName`, `data.categoryName`, `data.requestorId`, and `data.academicYearId` all populated (not empty Guids)

### Requirement: Create charge with no-approval category returns Approved status immediately
When a charge is created using a category with `IsApprovalRequired = false`, the system SHALL auto-approve and return `status = 1` (Approved).

#### Scenario: Auto-approve on category without approval requirement
- **WHEN** `POST /api/app/charges?academicYearId={id}` is called with a category where `IsApprovalRequired = false`
- **THEN** response status is 200 and `data.status` equals `1` (Approved)

### Requirement: Get charge by ID returns full detail shape
The system SHALL return the complete `ChargeDetailResponse` for a valid `chargeId`.

#### Scenario: GetById returns structured detail
- **WHEN** `GET /api/app/charges/{chargeId}` is called for an existing charge
- **THEN** response status is 200, `data.chargeId` matches the requested ID, `data.student.studentId` is populated, `data.requestor.requestorId` is populated, and `data.approvalDetails` is not null

#### Scenario: GetById with nonexistent ID returns error
- **WHEN** `GET /api/app/charges/{chargeId}` is called with a random non-existent Guid
- **THEN** response status is 404 or 400 or 500 (not 200)

### Requirement: Get all charges returns paginated list with status counts
The system SHALL return a `ChargeListResponse` including items, pagination metadata, and pre-computed status counts.

#### Scenario: GetAll returns list containing created charge
- **WHEN** a charge is created, then `GET /api/app/charges?academicYearId={id}&page=1&pageSize=20` is called
- **THEN** response status is 200, `items` array is not empty, `totalCount >= 1`, `currentPage = 1`, and `pageSize = 20`

#### Scenario: GetAll includes status counts
- **WHEN** `GET /api/app/charges?academicYearId={id}` is called
- **THEN** response contains `pendingCount`, `approvedCount`, and `rejectedCount` as integers (may be zero)
