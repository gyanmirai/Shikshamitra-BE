# Attendance & Leaves -- API Contract

## Authentication

All endpoints require Bearer token + `__tenant` header. All controllers use `[Authorize]`.

---

## Endpoints Overview

### Leave Management (`api/leave-management`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/leave-management` | List leave requests (academicYearId + month-year + optional search) |
| GET | `/api/leave-management/{id}` | Get leave request details (Guid) |
| POST | `/api/leave-management` | Create leave request (multipart) |
| PATCH | `/api/leave-management/{id}/approve` | Approve leave |
| PATCH | `/api/leave-management/{id}/reject` | Reject leave |
| DELETE | `/api/leave-management/{id}` | Cancel leave request |
| GET | `/api/leave-management/{id}/attachment` | Attachment DTO |
| GET | `/api/leave-management/{id}/attachment/download` | Attachment download |

**Note:** Historical export; canonical details: [api-contract.md](../../prd/attendance-leaves/api-contract.md).

### Leave Types (`api/leave-types`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/leave-types` | List leave types |
| GET | `/api/leave-types/{leaveTypeId}` | Get leave type |
| POST | `/api/leave-types?mediumStageId={id}` | Create leave type |
| PUT | `/api/leave-types/{leaveTypeId}` | Update leave type |
| DELETE | `/api/leave-types/{leaveTypeId}` | Delete leave type |

### Leave Policies (`api/leave-policies`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/leave-policies` | List leave policies |
| POST | `/api/leave-policies?mediumStageId={id}` | Create leave policy |
| PUT | `/api/leave-policies/{policyId}` | Update leave policy |
| DELETE | `/api/leave-policies/{policyId}` | Delete leave policy |

### Attendance Tracking (`api/attendance-tracking`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/attendance-tracking/date-wise` | Attendance by date |
| GET | `/api/attendance-tracking/employee-wise` | Attendance by employee |

### Notifications (`api/attendance/notifications`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/attendance/notifications` | List notification history |
| GET | `/api/attendance/notifications/{id}` | Get notification detail |

### Automated Notifications (`api/attendance/automated-notifications`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/attendance/automated-notifications` | List settings |
| GET | `/api/attendance/automated-notifications/{id}` | Get setting |
| POST | `/api/attendance/automated-notifications` | Create setting |
| PUT | `/api/attendance/automated-notifications/{id}` | Update setting |
| DELETE | `/api/attendance/automated-notifications/{id}` | Delete setting |

---

## Endpoint Details

### GET /api/leave-management

**Query params:**

| Param | Type | Required | Default | Constraints |
|-------|------|----------|---------|-------------|
| mediumStageId | Guid | yes | | |
| month | int | no | | 1-12 |
| year | int | no | | |
| page | int | no | 1 | >= 1 |
| pageSize | int | no | 10 | 10-100 |

**Response (200):**

```
LeaveManagementListResponse {
  totalCount: int
  currentPage: int
  pageSize: int
  totalPages: int
  items: [{
    id: int                      // public sequential ID
    name: string                 // employee name
    employeeCode: string
    leaveType: string
    startDate: DateTime
    endDate: DateTime
    totalDays: int
    organization: string
    description: string
    remarks: string?
    status: string               // "Pending"/"Reviewed"/"Approved"/"Rejected"
  }]
}
```

---

**Leave management detail schemas below are superseded.** Use [api-contract.md](../../prd/attendance-leaves/api-contract.md) for Guid ids, PATCH approve/reject, POST on `/api/leave-management`, and list query parameters.

---

### POST /api/leave-types?mediumStageId={id}

**Request body:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| name | string | yes | 1-100 chars |
| description | string | no | max 500 |
| color | string | yes | `^#[A-Fa-f0-9]{6}$` |

**Response (201):** `{ id: Guid, name, description?, color }`

---

### PUT /api/leave-types/{leaveTypeId}

**Request body:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| name | string | no | 1-100 chars |
| description | string | no | max 500 |
| color | string | no | `^#[A-Fa-f0-9]{6}$` |

**Response (200):** `{ id, name, description?, color }`

---

### POST /api/leave-policies?mediumStageId={id}

**Request body:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| policyTitle | string | yes | 1-200 chars |
| leaveType | Guid | yes | Reference to leave type |
| maxLeavePerYear | int | yes | 0-365 |
| paidType | string | yes | "Paid" or "Unpaid" |
| carryForward | bool | yes | |
| encashmentAllowed | bool | yes | |
| requiresApproval | bool | yes | |
| description | string | no | max 500 |

**Response (201):**

```
{
  id: string
  policyTitle: string
  leaveType: Guid
  maxLeavePerYear: int
  paidType: string
  carryForward: bool
  encashmentAllowed: bool
  requiresApproval: bool
  description: string?
  createdAt: DateTime
}
```

---

### GET /api/attendance-tracking/date-wise

**Query params:**

| Param | Type | Required | Constraints |
|-------|------|----------|-------------|
| attendanceDate | string | yes | DD-MM-YYYY or ISO |
| status | int | no | 0=PRESENT, 1=ABSENT, 2=HALF_DAY, 3=REJECTED_LEAVE |
| page | int | no | >= 1 |
| pageSize | int | no | 10-100 |

**Response (200):**

```
AttendanceDateWiseResponse {
  totalCount, currentPage, pageSize, totalPages: int
  summary: { all, present, absent, halfDay, rejectedLeave: int }
  items: [{
    employeeId: Guid
    fullName: string
    employeeIdentifier: string
    contactNo: string
    inTime: string?              // HH:mm
    outTime: string?
    hoursWorked: string?
    remarks: string?
    status: string
  }]
}
```

---

### GET /api/attendance-tracking/employee-wise

**Query params:**

| Param | Type | Required |
|-------|------|----------|
| employeeId | Guid | yes |
| startDate | string | yes (DD-MM-YYYY) |
| endDate | string | yes (DD-MM-YYYY) |
| status | int | no |

**Response (200):**

```
AttendanceEmployeeWiseResponse {
  summary: { all, present, absent, halfDay, rejectedLeave: int }
  items: [{
    date: string                 // DD-MM-YYYY
    day: string                  // e.g., "Monday"
    inTime: string?              // HH:mm AM/PM
    outTime: string?
    hoursWorked: string?         // e.g., "09H 00M"
    description: string?
    status: string
  }]
}
```

---

### POST /api/attendance/automated-notifications

**Request body:**

```
AutomatedNotificationsSettingsDto {
  employeeAttendanceStatusType: int    // 0=Present, 1=HalfDayLeave, 2=FullDayLeave
  notificationMethods: int[]           // 0=Email, 1=SMS, 2=PushNotification
}
```

**Response (201):** same shape with `id`

---

### GET /api/attendance/notifications

**Query params:** ABP standard `PagedAndSortedResultRequestDto` (skipCount, maxResultCount, sorting)

**Response (200):** `PagedResultDto<NotificationInfoDto>`

```
NotificationInfoDto {
  id: Guid
  receiverName: string
  notificationTime: DateTimeOffset
  receiverRole: string
  notificationType: int          // EmployeeAttendanceStatusType
  isDelivered: bool
}
```
