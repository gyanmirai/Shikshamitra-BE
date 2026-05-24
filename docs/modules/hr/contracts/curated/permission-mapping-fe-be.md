# HR Permission Mapping (FE to Canonical BE)

## Purpose

This document is the integration contract for HR permission keys consumed by FE and defined by BE.
BE canonical keys are the single source of truth.

## Canonical Namespaces

- `HR.Employee.*`
- `HR.PayrollManagement.*`
- `HR.AttendanceAndLeave.*`
- `HR.ProfessionalManagement.*`
- `HR.Reports.*`
- `HR.Notification.*`

## Mapping Matrix

| FE key (requested/legacy) | Canonical BE key | Status | Notes |
|---|---|---|---|
| `HR.Dashboard` | `HR.Dashboard` | Canonical | No change. |
| `HR.Employee` | `HR.Employee` | Canonical | Used as module/root guard. |
| `HR.Employee.EmployeeList` | `HR.Employee.EmployeeList` | Canonical | No change. |
| `HR.Employee.ICard` | `HR.Employee.ICard` | Canonical | No change. |
| `HR.Employee.Settings` | `HR.Employee.Setting.DocumentTypes` | Mapped | No dedicated canonical `HR.Employee.Settings` key exists. |
| `HR.Employee.Setting.DocumentTypes` | `HR.Employee.Setting.DocumentTypes` | Canonical | No change. |
| `HR.Payroll` | `HR.PayrollManagement` | Mapped | Root normalized to canonical family. |
| `HR.PayrollManagement` | `HR.PayrollManagement` | Canonical | No change. |
| `HR.PayrollManagement.RunPayroll` | `HR.PayrollManagement.RunPayroll` | Canonical | No change. |
| `HR.PayrollManagement.RunPayroll.Lock` | `HR.PayrollManagement.RunPayroll.Lock` | Canonical | Required to lock reviewed payroll rows. |
| `HR.PayrollManagement.RunPayroll.ViewAudit` | `HR.PayrollManagement.RunPayroll.ViewAudit` | Canonical | Required to view payroll lock audit events. |
| `HR.PayrollManagement.GenerateSalarySlip` | `HR.PayrollManagement.GenerateSalarySlip` | Canonical | No change. |
| `HR.Payroll.Settings` | `HR.PayrollManagement.Settings.EarningComponents` | Mapped | FE settings root mapped to canonical settings tree. |
| `HR.PayrollManagement.Settings.EarningComponents` | `HR.PayrollManagement.Settings.EarningComponents` | Canonical | No change. |
| `HR.PayrollManagement.Settings.DeductionComponents` | `HR.PayrollManagement.Settings.DeductionComponents` | Canonical | No change. |
| `HR.PayrollManagement.Settings.PfRule` | `HR.PayrollManagement.Settings.PfRule` | Canonical | No change. |
| `HR.PayrollManagement.Settings.PayrollPolicies` | `HR.PayrollManagement.Settings.PayrollPolicies` | Canonical | Payroll policy management. |
| `HR.AttendanceManagement` | `HR.AttendanceAndLeave` | Mapped | Root normalized to canonical family. |
| `HR.AttendanceAndLeave.MarkAttendance` | `HR.AttendanceAndLeave.TakeAttendance` | Mapped | Mark Attendance behavior aligns with Take Attendance capability. |
| `HR.AttendanceAndLeave.BiometricAttendance` | `HR.AttendanceAndLeave.ImportAttendance` | Mapped | Biometric/import flows align with Import Attendance capability. |
| `HR.AttendanceManagement.Settings` | `HR.AttendanceAndLeave.AttendanceTracking` | Mapped | FE settings root mapped to existing canonical key. |
| `HR.AttendanceAndLeave.Setting.ShiftManagement` | `HR.AttendanceAndLeave.Setting.ShiftManagement` | Canonical | No change. |
| `HR.AttendanceAndLeave.Evaluations` | `HR.AttendanceAndLeave.Evaluations` | Canonical | Attendance policy evaluation lifecycle. |
| `HR.AttendanceAndLeave.Evaluations.ViewAudit` | `HR.AttendanceAndLeave.Evaluations.ViewAudit` | Canonical | Required to view evaluation audit events. |
| `HR.Policies.ViewAudit` | `HR.Policies.ViewAudit` | Canonical | Required to view policy, policy version, and scoped assignment audit events. |
| `HR.LeaveManagement` | `HR.AttendanceAndLeave.LeaveManagement` | Mapped | Leave area is under AttendanceAndLeave family in BE. |
| `HR.LeaveManagement.ApplyLeave` | `HR.AttendanceAndLeave.LeaveManagement` | Mapped | No separate canonical key per leave page action. |
| `HR.LeaveManagement.Approvals` | `HR.AttendanceAndLeave.LeaveManagement` | Mapped | No separate canonical key per leave page action. |
| `HR.LeaveManagement.LeaveBalance` | `HR.AttendanceAndLeave.LeaveManagement` | Mapped | No separate canonical key per leave page action. |
| `HR.LeaveManagement.LeavePolicySetup` | `HR.AttendanceAndLeave.Setting.LeavePolicy` | Mapped | Canonical leave policy setup key. |
| `HR.LeaveManagement.Settings` | `HR.AttendanceAndLeave.Setting.LeavePolicy` | Mapped | FE settings root mapped to canonical setting key. |
| `HR.LeaveManagement.Setting.LeaveType` | `HR.AttendanceAndLeave.Setting.LeaveType` | Mapped | Namespace normalized. |
| `HR.LeaveManagement.Setting.LeavePolicy` | `HR.AttendanceAndLeave.Setting.LeavePolicy` | Mapped | Namespace normalized. |
| `HR.ProfessionalManagement` | `HR.ProfessionalManagement.PerformanceReview` | Mapped | No top-level canonical root key exists in BE submodule list. |
| `HR.Reports` | `HR.Reports` | Canonical | No change. |
| `HR.Notification` | `HR.Notification` | Canonical | No change. |

## Non-Goals

- Old Payroll Configuration and Attendance Configuration permission families are removed.
- Existing DB role grants should be reseeded/migrated to the new policy and evaluation permissions.
