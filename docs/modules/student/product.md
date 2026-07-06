---
module: student
domain: product
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Understand Student product behavior

TL;DR: Student owns inquiry, registration, admission, student records, attendance, leave, compliance, certificates, and dashboard previews.
Only part of that surface has deep written behavior docs today, so use source paths before making broad claims.

## Product areas

| Area | Status | Behavior owned by Student |
|---|---|---|
| Enrollment inquiry | Implemented | Inquiry capture, notes, settings, entrance exam, and lead-source tracking |
| Registration | Implemented | General details, family, communication, bank, additional, medical, previous school, and documents |
| Admission | Implemented | Admission workflow from inquiry or registration into student records |
| Student records | Implemented | Student list, management, bulk upload, dashboard preview, and settings |
| Attendance and leave | Implemented | Student attendance days, entries, leave requests, and leave type settings |
| Compliance | Implemented | Student identity compliance workflow |
| Certificates | Implemented | Student certificates and certificate templates |
| Inquiry Source | Focused documented surface | Product label for compatibility route `inquiry-preferences` |

## Data-integrity rules

| Rule | Consequence |
|---|---|
| Student identity data is critical | Do not silently mutate student, parent, contact, medical, or compliance data |
| Enrollment settings must preserve history | Inactive Inquiry Sources remain visible on records that already use them |
| Academic scope affects reports and fees | Board, medium, standard, division, and academic year must remain consistent |
| Documents need access audit | Sensitive student documents must not be exposed without traceability |
| Attendance affects reporting | Student attendance mutations must preserve date and student consistency |

## Partial documentation status

The current compact docs map the module and preserve the Inquiry Source behavior.
They do not replace source-code review for every Student endpoint yet.
When adding or changing Student behavior, update [api.md](./api.md), [workflows.md](./workflows.md), and [testing.md](./testing.md).
