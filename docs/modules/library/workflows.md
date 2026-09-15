---
module: library
domain: workflows
content_type: How-to
status: canonical
updated_on: 2026-09-15
owner: backend-team
audience: humans-and-agents
---

# Trace Library workflows

TL;DR: trace Library workflows from route to controller, then contract, application service, domain entity, and database configuration.
Use this page to pick the first files for common Library changes.

## Trace catalog and copy changes

Catalog and copy changes start in `BookController`.

1. Inspect `src/ShikshaMitra.HttpApi/Controllers/Library/BookController.cs`.
2. Inspect DTOs under `src/ShikshaMitra.Application.Contracts/Library/Books/**`.
3. Inspect `src/ShikshaMitra.Application/Library/Books/BookAppService.cs`.
4. Inspect entities under `src/ShikshaMitra.Domain/Library/Books/**`.
5. Inspect mappings under `src/ShikshaMitra.EntityFrameworkCore/Library/Books/**`.

## Trace issue and return changes

Issue and return changes start in `BookIssueController`.

1. Inspect `src/ShikshaMitra.HttpApi/Controllers/Library/BookIssueController.cs`.
2. Inspect DTOs under `src/ShikshaMitra.Application.Contracts/Library/IssueReturn/**`.
3. Inspect `src/ShikshaMitra.Application/Library/IssueReturn/BookIssueAppService.cs`.
4. Inspect `BookIssue`, `BookCopy`, and `LibraryMember` domain records.
5. Validate copy status, member status, return date, and charge side effects.

## Trace member changes

Member behavior starts with the members controller and service.

1. Inspect `src/ShikshaMitra.HttpApi/Controllers/Library/Members/LibraryMembersController.cs`.
2. Inspect DTOs under `src/ShikshaMitra.Application.Contracts/Library/Members/**`.
3. Inspect `src/ShikshaMitra.Application/Library/Members/LibraryMemberAppService.cs`.
4. Inspect `src/ShikshaMitra.Domain/Library/Members/**`.

## Trace report and notification changes

Reports and notifications have background and export side effects.

| Workflow | Inspect |
|---|---|
| Report options and preview | `Application/Library/Reports/LibraryReportAppService.cs` |
| Report export jobs | `Application/Library/Reports/LibraryReportExportBackgroundJob.cs` |
| PDF output | `Application/Library/Reports/LibraryPdfService.cs` |
| Excel output | `Application/Library/Reports/LibraryExcelService.cs` |
| Notification settings and history | `Application/Library/Notifications/LibraryNotificationAppService.cs` |
| Reminder execution | `Application/Library/Notifications/LibraryNotificationReminderService.cs` |
| Scheduled reminders | `HttpApi.Host/BackgroundWorkers/LibraryNotificationReminderWorker.cs` |

## Trace AI report generation

1. The reports screen posts the user's wording and current clarification answers to `POST api/library/reports/ai/plan`.
2. The application service checks `Library.Reports.View` and resolves the current tenant and UserScope.
3. The planner receives the supported semantic report catalog plus opaque tokens for permitted years, categories, sections, and statuses.
4. The planner either returns material clarification questions or a strict typed report request.
5. The server resolves tokens, rejects unsupported fields and filters, and revalidates academic scope.
6. The frontend runs a live preview through the normal report generator and shows only authorized rows.
7. After user approval, the export endpoint checks `Library.Reports.Export`, saves the validated request and scope snapshot, and queues generation.
8. The background job creates PDF or Excel output from deterministic report builders.
9. Download authorization, ownership or sharing rules, and expiry are checked before the file is returned.
