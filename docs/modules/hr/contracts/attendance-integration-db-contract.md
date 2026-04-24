# Attendance import batch (automated run) DB contract

Integrations that load data directly into SQL should respect the same aggregates as file import:

- **Batch:** `HR_AttendanceImportBatches` (`AttendanceImportBatch`). Set `RunSource` to `Integration` or `Internal`, optional `ExternalCorrelationId` / `IntegrationProvider`, `RunStartedAt` / `RunFinishedAt`. Move from `Preview` to `Confirmed` via `MarkConfirmed` after facts are written.
- **Row audit:** `HR_AttendanceImportRows` (`AttendanceImportRow`) — one row per source line with `Action` `create` or `replace`.
- **Facts:** `AppEmployeeAttendances` (`EmployeeAttendance`) — unique per tenant + employee + calendar date; set `EducationMediumStageId` and `Remarks` when applicable.

Prefer the HTTP API `POST /api/hr/attendance/integration-runs` (permission `ImportAttendance.Add`) so validation and `MarkConfirmed` stay consistent; raw SQL is possible but must mirror those rules.