---
module: student
domain: api
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Use Student backend APIs

TL;DR: use this page to find Student endpoint families and compatibility notes.
Controller source paths are canonical when route details change.

## Enrollment API families

| Family | Controller path | Notes |
|---|---|---|
| Inquiry | `Controllers/Student/Enrollment/InquiryController.cs` | Enrollment inquiry workflows |
| Registration | `Controllers/Student/Enrollment/RegistrationController.cs` and registration sub-controllers | Multi-section registration data |
| Admission | `Controllers/Student/Enrollment/AdmissionController.cs` | Admission workflow |
| Entrance exam | `Controllers/Student/Enrollment/EntranceExamController.cs` | Entrance exam slot and workflow APIs |
| Notes | `Controllers/Student/Enrollment/NotesController.cs` | Student enrollment notes |
| Enrollment settings | `Controllers/Student/Enrollment/Settings/**` | Inquiry Source, document type, and entrance exam slot settings |

## Records API families

| Family | Controller path |
|---|---|
| Student list | `Controllers/Student/Records/StudentListController.cs` |
| Student management | `Controllers/Student/Records/StudentManagementController.cs` |
| Bulk upload | `Controllers/Student/Records/StudentBulkUploadController.cs` |
| Dashboard | `Controllers/Student/Records/StudentDashboardController.cs` |
| Certificates | `Controllers/Student/Records/StudentCertificateController.cs` |
| House settings | `Controllers/Student/Records/Settings/HouseSettingsController.cs` |

## Attendance, leave, and compliance API families

| Family | Controller path |
|---|---|
| Student attendance | `Controllers/Student/Attendance/StudentAttendanceController.cs` |
| Student leave | `Controllers/Student/Attendance/StudentLeaveController.cs` |
| Student leave type settings | `Controllers/Student/Attendance/Settings/StudentLeaveTypeController.cs` |
| Identity compliance | `Controllers/Student/Compliance/StudentIdentityComplianceController.cs` |

## Inquiry Source compatibility

Inquiry Source is exposed through the existing `inquiry-preferences` route for compatibility.
Product copy should say Inquiry Source, while route and DTO names can still use Inquiry Preference.

| Route | Purpose |
|---|---|
| `/api/student-enrollment/{academicYearId}/settings/inquiry-preferences` | List or create Inquiry Sources |
| `/api/student-enrollment/settings/inquiry-preferences/{id}` | Get, update, or delete one source |
| `/api/student-enrollment/settings/inquiry-preferences/{id}/status` | Activate or deactivate one source |

## Contract rule

Do not add a parallel curated contract tree for Student.
Add endpoint summaries here when Student API docs need more route detail.
Do not restore generated exports under `docs/`.
