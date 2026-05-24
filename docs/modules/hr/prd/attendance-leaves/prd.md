# Attendance & Leaves -- Behavior Specification

## Purpose

Tracks employee attendance (daily in/out, status) and manages the full leave lifecycle: request creation, validation, approval/rejection, cancellation. Includes tenant-scoped settings for leave types, leave policies, custom attendance rules, and automated notifications.

New HR policy architecture introduces `AttendancePolicy` as the attendance-owned replacement for one tenant-wide attendance configuration. Attendance policies reference shared `WorkCalendarPolicy` records and may be assigned through scoped `HrPolicyAssignment` records.

## Actors

| Actor | Description |
|-------|-------------|
| Admin | Manages leave types, policies, custom rules, notification settings; views attendance and leave data where permitted |
| Manager (approver) | Any employee in the **reporting chain above** the leave requester who may approve or reject that request (see §1a) |
| Employee | Creates leave requests, views own attendance and leave history |
| System | Validates leave requests against policies, computes attendance summaries, sends automated notifications |

---

## Behaviors

### 1. Leave Management

1. Employee creates a leave request (v1 endpoint) with leave type, date range, reason, and optional attachment.
2. System validates via `LeaveValidationService` in order:
   - `StartDate <= EndDate` (`LeaveStartAfterEnd`)
   - StartDate >= today (`LeavePastDate`)
   - No overlapping active leaves for the same employee (`LeaveOverlap`)
   - Leave policy must exist for the leave type (`LeavePolicyNotFound`)
   - Duration <= 30 days per request (`LeaveMaxDaysExceeded`)
   - Remaining annual balance >= requested days (`LeaveInsufficientBalance`)
   - Minimum 2 days advance notice (`LeaveNoticePeriod`)
   - Policy must be active if it requires approval (`LeavePolicyInactive`)
3. Business days calculation excludes Saturday and Sunday.
4. See **§1a Leave approval (reporting chain)** for who may change status. Valid transitions remain Pending -> Reviewed -> Approved / Rejected. Rejection requires a `remarks` field.
5. Employee can cancel (delete) only their own pending leave requests. Cannot cancel approved (`LeaveCancelApproved`) or rejected (`LeaveCancelRejected`) leaves.
6. Leave requests use a public sequential integer ID (`PublicId`) for display, not the internal Guid.
7. System publishes `LeaveStatusChangedEto` on status transitions.

#### 1a. Leave approval (reporting chain)

1. **Reporting chain:** Starting from the leave request’s `EmployeeId`, walk `Employee.ReportingManagerId` repeatedly until `null`. The set of employees on that walk (excluding the requester) is the **approver set** for that leave. Data model: see [Employees PRD](../employees/prd.md) — `ReportingManagerId` on `Employee`.
2. **Who may approve or reject:** The signed-in user must map to an `Employee` who is a member of the **approver set**. The **direct reporting manager** is the usual approver; if they are unavailable, **any higher-level manager** in the same chain (the direct manager’s manager, and so on) may act instead. The requester must **not** approve their own leave.
3. **Single decision:** **One** approval or rejection from any eligible approver is sufficient to reach `Approved` or `Rejected` per existing status rules (no multi-step sign-off chain in v1).
4. **Authorization failure:** If the caller is not in the approver set (and no separate product rule grants override, e.g. global HR), reject the update with `HR:Leave:020020` *LeaveApproverNotInReportingChain*. Corrupt / cyclic reporting lines use `HR:Leave:020021` *LeaveReportingManagerCycle*.

### 2. Leave Types (Settings)

1. Admin CRUDs leave type master data per `mediumStageId` (e.g., Casual Leave, Sick Leave).
2. Each type has a name (1-100 chars), hex color (`^#[A-Fa-f0-9]{6}$`), and optional description (max 500).

### 3. Leave Policies (Settings)

1. Admin CRUDs leave policies per `mediumStageId`.
2. Each policy links to a leave type and defines: max days/year (1-365), paid/unpaid, carry-forward, encashment, requires-approval.
3. Policy title: 1-200 chars; description: max 500.

### 4. Attendance Tracking

1. **Date-wise**: Admin queries attendance for a specific date, paginated, optionally filtered by status.
2. **Employee-wise**: Admin queries attendance for an employee over a date range, optionally filtered by status.
3. Both views return a summary (present/absent/half-day/rejected-leave counts) and item list.
4. Date format: `DD-MM-YYYY` or ISO.

### 5. Custom Attendance Rules

1. Admin CRUDs tenant-specific attendance rules (late threshold, half-day threshold, notification settings).
2. No HTTP controller exposed; available only via application service.

### 5a. Attendance Policies

1. Admin creates multiple attendance policies per academic year using `/api/hr/attendance/policies`.
2. Each policy references one `WorkCalendarPolicy`.
3. One active attendance policy can be marked as default per tenant and academic year.
4. Employee-specific policy assignment overrides the default policy for the assignment date range.
5. Attendance policy owns punch thresholds, missing punch behavior, late/early rules, overtime, holiday work behavior, sandwich rule, and payroll integration mode.
6. Calendar holidays remain neutral; attendance policy decides whether attendance is required and how holiday work is treated.

### 6. Automated Notifications

1. Admin CRUDs notification trigger settings per attendance status type.
2. Configures which notification methods (Email, SMS, Push) to use per trigger.

### 7. Notification History

1. Read-only view of sent notifications with receiver info, timestamp, delivery status.

---

## Business Rules

- Leave balance = `policy.MaximumDaysPerYear - approvedDaysThisYear`. Balance cannot go negative.
- Max 30 days per single leave request.
- Minimum 2 days advance notice required.
- Only pending leaves can be cancelled; only by the owning employee.
- Status transitions: Pending -> Reviewed -> Approved/Rejected. No backward transitions.
- Leave status updates (approve/reject/review): only an employee in the requester’s **reporting-manager chain** above the requester (see §1a); one such action is final for approve/reject.
- Attachment upload validates: file size, extension, content type match, filename length/characters.
- Leave types and policies are scoped to `mediumStageId` (education medium/stage).
- Multi-tenancy: all data is tenant-isolated.

---

## Permissions

| Permission Key | Description |
|----------------|-------------|
| `HR.AttendanceAndLeave.ImportAttendance.View` | View imported attendance |
| `HR.AttendanceAndLeave.ImportAttendance.Add` | Import attendance data |
| `HR.AttendanceAndLeave.ImportAttendance.Delete` | Delete imported attendance |
| `HR.AttendanceAndLeave.LeaveManagement.View` | View leave requests |
| `HR.AttendanceAndLeave.LeaveManagement.Add` | Create/update leave requests |
| `HR.AttendanceAndLeave.LeaveManagement.Delete` | Cancel/delete leave requests |
| `HR.AttendanceAndLeave.AttendanceTracking.View` | View attendance tracking |
| `HR.AttendanceAndLeave.AttendanceTracking.Add` | Add attendance records |
| `HR.AttendanceAndLeave.AttendanceTracking.Delete` | Delete attendance records |
| `HR.AttendanceAndLeave.Setting.ShiftManagement.View` | View shift settings |
| `HR.AttendanceAndLeave.Setting.ShiftManagement.Add` | Create/update shifts |
| `HR.AttendanceAndLeave.Setting.ShiftManagement.Delete` | Delete shifts |
| `HR.AttendanceAndLeave.Setting.LeaveType.View` | View leave types |
| `HR.AttendanceAndLeave.Setting.LeaveType.Add` | Create/update leave types |
| `HR.AttendanceAndLeave.Setting.LeaveType.Delete` | Delete leave types |
| `HR.AttendanceAndLeave.Setting.LeavePolicy.View` | View leave policies |
| `HR.AttendanceAndLeave.Setting.LeavePolicy.Add` | Create/update leave policies |
| `HR.AttendanceAndLeave.Setting.LeavePolicy.Delete` | Delete leave policies |

---

## Error Codes

| Code | Constant | When |
|------|----------|------|
| `HR:Leave:020001` | LeaveInvalidMonth | Invalid month value |
| `HR:Leave:020002` | LeaveInvalidYear | Invalid year value |
| `HR:Leave:020003` | LeaveInvalidPage | Invalid page number |
| `HR:Leave:020004` | LeaveInvalidPageSize | Invalid page size |
| `HR:Leave:020005` | LeaveUserNotAuthenticated | User not authenticated |
| `HR:Leave:020006` | LeaveEmployeeNotFound | Employee not found for current user |
| `HR:Leave:020007` | LeaveInvalidStatus | Invalid status value |
| `HR:Leave:020008` | LeaveInvalidStatusTransition | Cannot transition from current status |
| `HR:Leave:020009` | LeaveCancelApproved | Cannot cancel an approved leave |
| `HR:Leave:020010` | LeaveCancelRejected | Cannot cancel a rejected leave |
| `HR:Leave:020011` | LeaveDeleteOwnOnly | Can only delete own leave requests |
| `HR:Leave:020012` | LeaveStartAfterEnd | Start date is after end date |
| `HR:Leave:020013` | LeavePastDate | Leave requested for a past date |
| `HR:Leave:020014` | LeaveOverlap | Overlapping active leave exists |
| `HR:Leave:020015` | LeavePolicyNotFound | No policy found for leave type |
| `HR:Leave:020016` | LeaveMaxDaysExceeded | Exceeds 30-day per-request maximum |
| `HR:Leave:020017` | LeaveInsufficientBalance | Insufficient annual leave balance |
| `HR:Leave:020018` | LeaveNoticePeriod | Minimum 2-day advance notice required |
| `HR:Leave:020019` | LeavePolicyInactive | Referenced leave policy is inactive |
| `HR:Leave:020020` | LeaveApproverNotInReportingChain | Approver is not in the requester’s reporting chain above the requester (includes self-approval) |
| `HR:Leave:020021` | LeaveReportingManagerCycle | Reporting-manager chain has a cycle or exceeds maximum depth |
| `HR:Leave:020101` | LeaveFileTooLarge | Attachment file too large |
| `HR:Leave:020102` | LeaveFileEmpty | Attachment file is empty |
| `HR:Leave:020103` | LeaveFileInvalidExtension | Invalid file extension |
| `HR:Leave:020104` | LeaveFileNameTooLong | Filename too long |
| `HR:Leave:020105` | LeaveFileInvalidCharacters | Invalid characters in filename |
| `HR:Leave:020106` | LeaveFileTooSmall | File too small |
| `HR:Leave:020107` | LeaveFileContentMismatch | Content type does not match extension |
| `HR:Leave:020108` | LeaveFileInvalidAttachment | Invalid attachment |

---

## Enums

| Enum | Values |
|------|--------|
| EmployeeLeaveStatusType | Pending=0, Approved=1, Rejected=2, Reviewed=3 |
| EmployeeAttendanceStatusType | Present=0, HalfDayLeave=1, FullDayLeave=2 |
| AttendanceTrackingStatus | PRESENT=0, ABSENT=1, HALF_DAY=2, REJECTED_LEAVE=3 |
| LeavePaidType | Paid=0, Unpaid=1 |
| TimeUnitType | Minutes=0, Hours=1 |
| NotificationMethodType | Email=0, SMS=1, PushNotification=2 |

---

## Domain Events

| Event | Trigger | Payload |
|-------|---------|---------|
| `LeaveStatusChangedEto` | Leave status transition | LeaveId, EmployeeId, PreviousStatus, NewStatus, TenantId, ChangedAt |

---

## Dependencies

- [Employees](../employees/prd.md) -- leave requests reference `EmployeeId`
- [HR Policy Architecture](../../implementation/hr-policy-architecture.md) -- shared calendar, attendance policy, payroll policy, and employee assignment model
- Education Medium/Stage -- leave types and policies are scoped by `mediumStageId`
- Blob Storage -- leave request attachments
