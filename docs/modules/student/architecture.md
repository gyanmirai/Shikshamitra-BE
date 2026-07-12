---
module: student
domain: architecture
content_type: Reference
status: canonical
updated_on: 2026-07-10
owner: backend-team
audience: humans-and-agents
---

# Locate Student backend code

TL;DR: Student code is under `Students` in contracts, application, domain, and EF Core, while HTTP controllers use `Controllers/Student`.

## Layer map

| Layer | Path |
|---|---|
| Contracts | `src/ShikshaMitra.Application.Contracts/Students/**` |
| Application | `src/ShikshaMitra.Application/Students/**` |
| Domain | `src/ShikshaMitra.Domain/Students/**` |
| EF Core | `src/ShikshaMitra.EntityFrameworkCore/Students/**` |
| HTTP API | `src/ShikshaMitra.HttpApi/Controllers/Student/**` |
| Tests | `test/ShikshaMitra.*.Tests/Students/**` |

## Domain subsystems

| Subsystem | Source area |
|---|---|
| Enrollment | `Students/StudentEnrollments/**` |
| Enrollment admissions CRM | `Students/StudentEnrollments/AdmissionsCrm/**` |
| Records | `Students/Records/**` and root Student entities |
| Attendance | `Students/Attendance/**` |
| Attendance settings | `Students/Attendance/Settings/**` |
| Certificates | `Students/Records/StudentCertificate*` |
| Compliance | Student identity compliance source paths |

## Implementation rules

| Rule | Why it matters |
|---|---|
| Preserve route compatibility for renamed settings | Inquiry Source still uses `inquiry-preferences` API names |
| Keep incomplete admission leads separate | CRM leads should not become `Student` rows until the admission handoff is ready |
| Keep pre-admission exam records separate | CRM entrance exam records should not require `StudentAdditionalDetail` until a real student admission is created |
| Keep historical lookup values visible | Inactive sources must remain visible on records that already use them |
| Protect document access | Student documents are sensitive and require audit discipline |
| Verify academic scope before reports or fees | Student scope feeds Finance, attendance, and reporting |
