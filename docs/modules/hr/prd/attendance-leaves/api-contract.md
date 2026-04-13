# Attendance & Leaves -- API Contract

## Authentication

All endpoints require Bearer token + `__tenant` header. All controllers use `[Authorize]`.

---

## Endpoints Overview

### Leave Management (`api/leave-management`)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/leave-management` | List leave requests (paginated) |
| GET | `/api/leave-management/{leaveRequestId}` | Get leave request details |
| PUT | `/api/leave-management/{leaveRequestId}` | Update leave status |
| DELETE | `/api/leave-management/{leaveRequestId}` | Cancel leave request |
| POST | `/api/v1/leave-management` | Create leave request (v1, multipart) |

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

### GET /api/leave-management/{leaveRequestId}

**Path params:** `leaveRequestId` (string, parsed as int -- public sequential ID)

**Response (200):**

```
LeaveRequestDetailsResponse {
  id: int
  mediumStageId: Guid
  employeeId: Guid
  employeeName: string
  employeeCode: string
  leaveTypeId: Guid
  leaveType: string
  startDate: DateTime
  endDate: DateTime
  totalDays: int
  description: string
  attachmentUrl: string?
  remarks: string?
  status: string
  createdAt: DateTime
  updatedAt: DateTime
}
```

**Status codes:** 200, 400 (invalid ID format), 404

---

### PUT /api/leave-management/{leaveRequestId}

**Authorization (business rule):** The authenticated user must resolve to an `Employee` who appears in the leave requester’s **reporting-manager chain** (walk `ReportingManagerId` from the requester upward until null). The direct reporting manager may approve; if they are unavailable, **any ancestor manager** in that same chain may approve or reject instead. The requester must not approve their own request. **One** successful approve or reject from any eligible approver is sufficient (no sequential multi-step workflow in v1). Full spec: [prd.md §1a](./prd.md#1a-leave-approval-reporting-chain).

**Request body:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| status | string | yes | "Pending", "Reviewed", "Approved", or "Rejected" |
| remarks | string | conditional | Required when status = "Rejected" |

**Response (200):** `LeaveRequestDetailsResponse`

**Errors:** `HR:Leave:020020` *LeaveApproverNotInReportingChain* — caller is not an approver in the requester’s chain above the requester (includes self-approval). `HR:Leave:020021` *LeaveReportingManagerCycle* — invalid/cyclic reporting hierarchy.

---

### DELETE /api/leave-management/{leaveRequestId}

Cancels a leave request. Only the owning employee can cancel, and only if status is Pending.

**Response (200):** `{ id: int, message: "Leave request cancelled successfully" }`

**Error codes:** `LeaveCancelApproved`, `LeaveCancelRejected`, `LeaveDeleteOwnOnly`

---

### POST /api/v1/leave-management

Creates a leave request with optional file attachment.

**Content-Type:** `multipart/form-data`

**Form fields:**

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| mediumStageId | Guid | yes | |
| leaveTypeId | Guid | yes | |
| startDate | DateTime | yes | must be >= today |
| endDate | DateTime | yes | must be >= startDate |
| reason | string | yes | 5-500 chars |
| file | IFormFile | no | see file validation error codes |

**Response (201):** `{ id: Guid, status: string, appliedAt: DateTime }`

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
