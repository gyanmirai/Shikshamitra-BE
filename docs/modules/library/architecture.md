---
module: library
domain: architecture
content_type: Conceptual
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Understand Library backend architecture

TL;DR: Library follows the standard ShikshaMitra backend layering.
Controllers route requests, application services enforce workflows, domain entities hold state, and Entity Framework configurations persist the records.

## Layer map

Use this map before changing Library behavior.

| Layer | Path | Role |
|---|---|---|
| HTTP API | `src/ShikshaMitra.HttpApi/Controllers/Library/**` | Route declarations and HTTP action shape |
| Contracts | `src/ShikshaMitra.Application.Contracts/Library/**` | App service interfaces and DTOs |
| Application | `src/ShikshaMitra.Application/Library/**` | Workflow orchestration, validations, reports, and notifications |
| Domain | `src/ShikshaMitra.Domain/Library/**` | Book, copy, issue, reservation, member, charge, and settings entities |
| Domain shared | `src/ShikshaMitra.Domain.Shared/Library/**` | Error codes and shared enums |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/Library/**` | Entity configuration and database mapping |
| Host worker | `src/ShikshaMitra.HttpApi.Host/BackgroundWorkers/LibraryNotificationReminderWorker.cs` | Reminder scheduling entrypoint |

## Domain areas

Library code is organized by operational responsibility.

| Area | Owns |
|---|---|
| `Books` | Catalog records, copies, copy status, reservations, and Library charges |
| `IssueReturn` | Issue and return workflows |
| `Members` | Library members and member history |
| `Charges` | Charge listing, detail, creation, update, and deletion |
| `Dashboard` | Summary metrics |
| `Reports` | Report catalog, preview, export, and file generation |
| `Notifications` | Settings, history, delivery, recipients, and reminders |
| `Setting` | Sections, hours, categories, and borrowing rules |

## Agent inspection order

Start with the smallest source set that answers the change.

| Task | Inspect first |
|---|---|
| Route change | `HttpApi/Controllers/Library/**` |
| Request or response change | `Application.Contracts/Library/**` |
| Status or availability rule | `Domain/Library/**` and `Application/Library/**` |
| Report issue | `Application/Library/Reports/**` |
| Notification issue | `Application/Library/Notifications/**` and the background worker |
| Database mapping issue | `EntityFrameworkCore/Library/**` |
