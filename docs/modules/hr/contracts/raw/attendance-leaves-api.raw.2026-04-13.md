# HR Attendance & Leaves API (Raw Export)

Generated: 2026-04-13

## Authentication
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Leave Types

### GET `/api/hr-leave-types`
Query:
- `academicYearId` (Guid, required)
- `page` (int, optional, default `1`)
- `pageSize` (int, optional, default `10`)
- `search` (string, optional)

Response `200`:
- `LeaveTypeListResponse { totalCount, currentPage, pageSize, totalPages, items[] }`

### POST `/api/hr-leave-types`
Query:
- `academicYearId` (Guid, required)
Body:
- `name` (string, required)
- `description` (string, optional)
- `color` (hex string, required)

Response `201`:
- `LeaveTypeResponse { id, name, description?, color }`

### GET `/api/hr-leave-types/{id}`
Response `200`: `LeaveTypeResponse`

### PUT `/api/hr-leave-types/{id}`
Body:
- `name?`, `description?`, `color?`

Response `200`: `LeaveTypeResponse`

### DELETE `/api/hr-leave-types/{id}`
Soft delete (`IsActive=false`)

Response `200`:
- `LeaveTypeDeleteResponse { id, message }`

## Leave Policies

### GET `/api/leave-policies`
Query:
- `academicYearId` (Guid, required)
- `page` (int, optional, default `1`)
- `pageSize` (int, optional, default `10`)
- `search` (string, optional)

Response `200`:
- `LeavePolicyListResponse { totalCount, currentPage, pageSize, totalPages, items[] }`
- `items[].leaveType` is nested object `{ id, name }`

### POST `/api/leave-policies`
Query:
- `academicYearId` (Guid, required)
Body:
- `policyTitle` (string, required)
- `leaveType` (Guid, required)
- `accrualPolicyType` (`Monthly|Yearly`, required)
- `leaveCount` (int, required)
- `paidType` (`Paid|Unpaid`, required)
- `carryForward` (bool, required)
- `encashmentAllowed` (bool, required)
- `encashmentTiming` (`next_payroll|end_of_year`, required when encashmentAllowed=true)
- `requiresApproval` (bool, required)
- `description` (string, optional)

Response `201`:
- `LeavePolicyResponse { id, policyTitle, leaveType, accrualPolicyType, leaveCount, paidType, carryForward, encashmentAllowed, encashmentTiming?, requiresApproval, description?, createdAt }`

### GET `/api/leave-policies/{id}`
Response `200`:
- `LeavePolicyDetailResponse` with nested `leaveType { id, name }`

### PUT `/api/leave-policies/{id}`
Body same contract as create.

Response `200`:
- `LeavePolicyDetailResponse`

### DELETE `/api/leave-policies/{id}`
Soft delete.

Response `200`:
- `LeavePolicyDeleteResponse { id, message, deletedAt }`

## Attendance Configuration

### POST `/api/attendance/configuration`
Query:
- `academicYearId` (Guid, required)
Body:
- `dailyRules.hoursBasedPayPercentage` (number, required)
- `lateEarlyRules.graceTime` (number, required)
- `lateEarlyRules.maxLateAllowedPerMonth` (number, required)
- `lateEarlyRules.penaltyType` (`none|deduct_half_day|deduct_full_day`, required)
- `overtime.enabled` (bool, required)
- `overtime.rate` (`1.5x|2.0x|flat`, required when overtime.enabled=true)
- `overtime.minOvertimeMinutes` (number, required when overtime.enabled=true)

Response `200`:
- `AttendanceConfigurationResponse { success: true, message }`

## Attendance Tracking

### GET `/api/attendance-tracking/date-wise`
Query:
- `attendanceDate` (string, required, ISO date-time or `dd-MM-yyyy`)
- `status` (string, required: `PRESENT|ABSENT|HALF_DAY|REJECTED_LEAVE`)
- `page` (int, optional, default `1`)
- `pageSize` (int, optional, default `10`, range `10-100`)
- `educationMediumStageId` (Guid, required)
- `search` (string, optional; matches `fullName`, `employeeIdentifier`, `contactNo`)

Response `200`:
- `AttendanceDateWiseResponse`
- `summary` fields: `{ present, absent, halfDay, rejectedLeave }`
- `items[]` fields:
  - `employeeId` (Guid string)
  - `fullName`, `employeeIdentifier`, `contactNo` (string)
  - `inTime`, `outTime` (ISO 8601 date-time with offset, nullable)
  - `hoursWorked` (string `HH:mm`, nullable)
  - `remarks` (string, nullable)
  - `status` (`PRESENT|ABSENT|HALF_DAY|REJECTED_LEAVE`)

### GET `/api/attendance-tracking/employee-wise`
Query:
- `employeeId` (Guid, required)
- `startDate` (string, required, ISO date-time or `dd-MM-yyyy`)
- `endDate` (string, required, ISO date-time or `dd-MM-yyyy`)
- `status` (string, optional: `PRESENT|ABSENT|HALF_DAY|REJECTED_LEAVE`)
- `page` (int, optional, default `1`)
- `pageSize` (int, optional, default `10`, range `10-100`)
- `educationMediumStageId` (Guid, required)
- `search` (string, optional; matches `date`, `day`, `description`, `status`)

Response `200`:
- `AttendanceEmployeeWiseResponse { totalCount, currentPage, pageSize, totalPages, attendanceSummary, items[] }`
- `attendanceSummary` fields: `{ present, absent, halfDay, rejectedLeave }`
- `items[]` fields:
  - `date` (ISO 8601 date-time with offset)
  - `day` (string)
  - `inTime`, `outTime` (ISO 8601 date-time with offset, nullable)
  - `hoursWorked` (string `HH:mm`, nullable)
  - `description` (string, nullable)
  - `status` (`PRESENT|ABSENT|HALF_DAY|REJECTED_LEAVE`)

## HR automated attendance run logs (mirror)

Same contract as payroll import logs; use when the UI should not require payroll permissions.

### GET `/api/hr/attendance/automated-runs`

Query: `search`, `payrollMonth`, `skipCount`, `maxResultCount`

Auth: requires `HR.AttendanceAndLeave.ImportAttendance.View` **or** `HR.AttendanceAndLeave.AttendanceTracking.View`.

### GET `/api/hr/attendance/automated-runs/{importId}`

### GET `/api/hr/attendance/automated-runs/{importId}/rows`

## Take attendance (manual day grid)

### GET `/api/hr/attendance/take-attendance/day`

Query: `attendanceDate` (dd-MM-yyyy or ISO), `educationMediumStageId` (required), optional `department`, `isTeaching`, `roleId`, `search`, `skipCount`, `maxResultCount`

Auth: `HR.AttendanceAndLeave.TakeAttendance.View`

### POST `/api/hr/attendance/take-attendance/day/save`

Body: `attendanceDate`, `educationMediumStageId`, `entries[]` with `employeeId`, `statusWire` (`PRESENT`|`HALF_DAY`|`ABSENT`), optional `inTime`/`outTime` (ISO-8601 offset), `remarks`

Auth: `HR.AttendanceAndLeave.TakeAttendance.Add`

### POST `/api/hr/attendance/take-attendance/day/lock`

Query: `educationMediumStageId`, `attendanceDate`

Auth: `HR.AttendanceAndLeave.TakeAttendance.Lock`

### DELETE `/api/hr/attendance/take-attendance/day/lock`

Query: same as lock (removes lock row)

## Integration attendance run (programmatic)

### POST `/api/hr/attendance/integration-runs`

Auth: `HR.AttendanceAndLeave.ImportAttendance.Add`

Body: `fileName`, `runSource` (`FileUpload`|`Integration`|`Internal`), optional `externalCorrelationId`, `integrationProvider`, `rows[]` (`employeeId`, `attendanceDate` string, optional times, `status` enum, `educationMediumStageId`, `remarks`)

## Standardized Error Envelope

All `/api/*` errors must follow:

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Human-readable message",
    "fields": {
      "fieldName": "validation reason"
    }
  }
}
```

Allowed `error.code` values include:
- `VALIDATION_ERROR`
- `INVALID_REQUEST`
- `UNAUTHORIZED`
- `FORBIDDEN`
- `RESOURCE_NOT_FOUND`
- `DUPLICATE_RESOURCE`
- `INVALID_STATE_TRANSITION`
- `RATE_LIMIT_EXCEEDED`
- `INTERNAL_SERVER_ERROR`
