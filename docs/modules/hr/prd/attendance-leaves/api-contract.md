# Attendance & Leaves -- API Contract

## Authentication

All endpoints require:
- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

All controllers are `[Authorize]`.

---

## Endpoint Overview

### Leave Types (`/api/hr-leave-types`)

| Method | Route | Description |
|---|---|---|
| GET | `/api/hr-leave-types` | List leave types by academic year, with pagination + search |
| GET | `/api/hr-leave-types/{id}` | Get leave type by id |
| POST | `/api/hr-leave-types?academicYearId={id}` | Create leave type |
| PUT | `/api/hr-leave-types/{id}` | Update leave type |
| DELETE | `/api/hr-leave-types/{id}` | Soft-delete leave type |

### Leave Policies (`/api/leave-policies`)

| Method | Route | Description |
|---|---|---|
| GET | `/api/leave-policies` | List policies by academic year, with pagination + search |
| GET | `/api/leave-policies/{id}` | Get leave policy detail by id |
| POST | `/api/leave-policies?academicYearId={id}` | Create leave policy |
| PUT | `/api/leave-policies/{id}` | Update leave policy |
| DELETE | `/api/leave-policies/{id}` | Soft-delete leave policy |

### Attendance Configuration (`/api/attendance/configuration`)

| Method | Route | Description |
|---|---|---|
| POST | `/api/attendance/configuration?academicYearId={id}` | Create or update attendance configuration (upsert) |

### Attendance Tracking (`/api/attendance-tracking`)

| Method | Route | Description |
|---|---|---|
| GET | `/api/attendance-tracking/date-wise` | Attendance by date with status + scope filters |
| GET | `/api/attendance-tracking/employee-wise` | Attendance by employee/date-range with paging |

### Leave Management (`/api/leave-management`)

| Method | Route | Description |
|---|---|---|
| GET | `/api/leave-management` | Paginated leave requests for an academic year. Optional `month-year`: when set, results are limited to that calendar month (clipped to the year); when omitted, the full academic year date span is used. Optional `status`, `leavePolicyId`, `leaveTypeMasterId`, `search` |
| GET | `/api/leave-management/balances` | Per active policy: entitled, used (approved business days YTD), remaining (`employeeId` optional; defaults to caller’s employee; respects employee access rules) |
| GET | `/api/leave-management/calendar` | Requests overlapping `rangeStart`–`rangeEnd` for calendar UI (optional filters). Use ISO-8601 date-time with offset (example: `2026-04-01T00:00:00-07:00`). |
| GET | `/api/leave-management/export` | Excel export using the same date scope and filters as the list (`month-year` optional; full academic year when omitted) |
| POST | `/api/leave-management?academicYearId=` | Multipart create: `leavePolicyId` (preferred) or legacy `leaveTypeId` (leave **policy** id), `startDate`, `endDate`, `reason`, optional `employeeId` (manager/HR on-behalf), optional attachment |
| GET | `/api/leave-management/{id}` | Detail (includes `publicId`, `leavePolicyId`; `leaveTypeId` duplicates `leavePolicyId` for backward compatibility) |
| PATCH | `/api/leave-management/{id}/approve` | Approve |
| PATCH | `/api/leave-management/{id}/reject` | Reject |
| DELETE | `/api/leave-management/{id}` | Cancel (pending, own request) |
| GET | `/api/leave-management/{id}/attachment` | Attachment metadata |
| GET | `/api/leave-management/{id}/attachment/download` | Attachment file bytes |

Leave durations and balances use **business days** (weekends excluded) per PRD.

---

## Leave Types Contract

### GET `/api/hr-leave-types`
Query:
- `academicYearId` (Guid, required)
- `page` (int, default `1`)
- `pageSize` (int, default `10`)
- `search` (string, optional)

Response `200`: `LeaveTypeListResponse`

### POST `/api/hr-leave-types`
Query:
- `academicYearId` (Guid, required)
Body:
- `name` (required)
- `description` (optional)
- `color` (hex string, required)

Response `201`: `LeaveTypeResponse`

### DELETE `/api/hr-leave-types/{id}`
Behavior:
- Soft delete (`IsActive=false`), contract response remains:
`{ id, message }`

---

## Leave Policies Contract

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
- `encashmentTiming` (`next_payroll|end_of_year`, required if `encashmentAllowed=true`)
- `requiresApproval` (bool, required)
- `description` (string, optional)

Response `201`: `LeavePolicyResponse`

### GET `/api/leave-policies/{id}`
Response `200`: `LeavePolicyDetailResponse`
- includes nested `leaveType: { id, name }`

### GET `/api/leave-policies`
Query:
- `academicYearId` (Guid, required)
- `page` / `pageSize`
- `search` (optional)

Response `200`: paginated `LeavePolicyListResponse`

---

## Attendance Configuration Contract

### POST `/api/attendance/configuration`
Query:
- `academicYearId` (Guid, required)

Body:
- `dailyRules.hoursBasedPayPercentage` (number, required)
- `lateEarlyRules.graceTime` (number, required)
- `lateEarlyRules.maxLateAllowedPerMonth` (number, required)
- `lateEarlyRules.penaltyType` (`none|deduct_half_day|deduct_full_day`, required)
- `overtime.enabled` (bool, required)
- `overtime.rate` (`1.5x|2.0x|flat`, required when `enabled=true`)
- `overtime.minOvertimeMinutes` (number, required when `enabled=true`)

Response `200`:
- `{ "success": true, "message": "Attendance configuration saved successfully" }`

---

## Attendance Tracking Contract

### GET `/api/attendance-tracking/date-wise`
Query:
- `attendanceDate` (string, required; ISO date-time or `dd-MM-yyyy`)
- `status` (string, required; `PRESENT|ABSENT|HALF_DAY|REJECTED_LEAVE`)
- `page` (int, default `1`)
- `pageSize` (int, default `10`, range `10-100`)
- `educationMediumStageId` (Guid, required)
- `search` (string, optional)

Response `200`: `AttendanceDateWiseResponse`
- Paging: `totalCount`, `currentPage`, `pageSize`, `totalPages`
- Summary: `{ present, absent, halfDay, rejectedLeave }`
- Item fields: `employeeId`, `fullName`, `employeeIdentifier`, `contactNo`, `inTime?`, `outTime?`, `hoursWorked?`, `remarks?`, `status`
- `inTime` and `outTime` are ISO 8601 date-time with offset.

### GET `/api/attendance-tracking/employee-wise`
Query:
- `employeeId` (Guid, required)
- `startDate` (string, required; ISO date-time or `dd-MM-yyyy`)
- `endDate` (string, required; ISO date-time or `dd-MM-yyyy`)
- `status` (string, optional; `PRESENT|ABSENT|HALF_DAY|REJECTED_LEAVE`)
- `page` (int, default `1`)
- `pageSize` (int, default `10`, range `10-100`)
- `educationMediumStageId` (Guid, required)
- `search` (string, optional)

Response `200`: `AttendanceEmployeeWiseResponse`
- Paging: `totalCount`, `currentPage`, `pageSize`, `totalPages`
- Summary: `{ present, absent, halfDay, rejectedLeave }`
- Item fields: `date`, `day`, `inTime?`, `outTime?`, `hoursWorked?`, `description?`, `status`
- `date`, `inTime`, and `outTime` are ISO 8601 date-time with offset.

---

## Error Envelope (Global)

All `/api/*` errors must use RFC 7807 (`application/problem+json`):

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Validation failed",
  "status": 400,
  "detail": "Request validation failed",
  "instance": "/api/...",
  "errorCode": "VALIDATION_ERROR",
  "errors": {
    "field": ["reason"]
  }
}
```

Notes:
- `errorCode` carries machine-readable business/validation code when available.
- `errors` is included for field-level validation failures.
