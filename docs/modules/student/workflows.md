---
module: student
domain: workflows
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Trace Student backend workflows

TL;DR: Student workflows start with inquiry and registration, then move into admission, records, attendance, documents, and certificates.

## Core workflows

| Workflow | Main steps | Validation focus |
|---|---|---|
| Inquiry Source setup | Create, update, activate, deactivate, delete only unused source | Active-only selection, inactive historical display, usage count, delete eligibility |
| Inquiry | Capture lead details, source, notes, and follow-up information | Academic year, active source, contact data, and tenant scope |
| Registration | Save general, family, communication, bank, additional, medical, previous school, and document sections | Section persistence, validation, document access, and status |
| Admission | Convert eligible inquiry or registration into admitted student record | Duplicate prevention, academic scope, admission state, and required data |
| Student records | List, search, update, bulk upload, and manage student dashboard data | Scope filters, pagination, dashboard preview freshness, and data integrity |
| Attendance and leave | Capture student attendance, manage leave, and configure leave types | Date consistency, student eligibility, leave type validity, and reporting impact |
| Certificates | Create templates and issue certificates | Student identity, template validity, and audit trail |

## Inquiry Source rules

| Scenario | Expected behavior |
|---|---|
| Active-only list after deactivation | Inactive source is excluded |
| Existing record with unchanged inactive source | Save remains valid |
| New or changed record uses inactive source | Request is rejected |
| In-use source delete | Request is rejected |
| In-use source deactivate | Request succeeds |
| Unused inactive source delete | Request succeeds |
