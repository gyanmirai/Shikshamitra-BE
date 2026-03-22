# Reports -- Behavior Specification

## Purpose

Generates filtered employee reports and exports them as PDF or Excel files. Provides a lightweight reporting layer on top of the employee data.

## Actors

| Actor | Description |
|-------|-------------|
| Admin | Requests filtered employee reports, downloads exports |

---

## Behaviors

### 1. Employee Report

1. Admin queries employees with optional gender filter.
2. Returns full employee records matching the filter.

### 2. Employee Contact Report

1. Admin queries employee contact information (name, email, mobile, DOB, code) with optional gender filter.
2. Returns a lightweight contact-only projection.

### 3. Export

1. Admin requests an export with optional gender filter and export type (PDF or Excel).
2. System generates the file using `HRPdfService` (QuestPDF) or `HRExcelService` (ClosedXML) from contact report data.
3. Returns the file as a binary download with appropriate MIME type.
4. If export fails, returns `ReportExportFailed`.

---

## Business Rules

- Reports query the full employee dataset, filtered only by `GenderType` currently.
- PDF generation uses QuestPDF; Excel uses ClosedXML.
- Multi-tenancy: reports only include employees from the current tenant.

---

## Permissions

| Permission Key | Description |
|----------------|-------------|
| `HR.Reports.View` | View and generate reports |
| `HR.Reports.Add` | Create reports |
| `HR.Reports.Delete` | Delete reports |

---

## Error Codes

| Code | Constant | When |
|------|----------|------|
| `HR:Reports:050001` | ReportExportFailed | PDF/Excel generation failed |

---

## Domain Events

None.

---

## Dependencies

- [Employees](../employees/prd.md) -- source data for all reports
