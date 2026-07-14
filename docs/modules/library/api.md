---
module: library
domain: api
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Find Library backend APIs

TL;DR: Library routes live under `api/library/**`.
Use controllers for route truth and application contracts for request and response shapes.

## Endpoint families

This table maps the active route families to their controller and contract owner.

| Surface | Route family | Controller | Contract owner |
|---|---|---|---|
| Dashboard | `api/library/dashboard` | `LibraryDashboardController` | `ILibraryDashboardAppService` |
| Books | `api/library/books` | `BookController` | `IBookAppService` |
| Issue and return | `api/library` | `BookIssueController` | `IBookIssueAppService` |
| Members | `api/library/members` | `LibraryMembersController` | `ILibraryMemberAppService` |
| Charges | `api/library/charges` | `LibraryChargeController` | `ILibraryChargeAppService` |
| Reports | `api/library/reports` | `LibraryReportController` | `ILibraryReportAppService` |
| Notifications | `api/library/notifications` | `LibraryNotificationController` | `ILibraryNotificationAppService` |
| Sections | `api/library/sections` | `LibrarySectionController` | `ILibrarySectionAppService` |
| Library hours | `api/library/library-hours` | `LibraryHoursController` | `ILibraryTimingAppService` |
| Charge categories | `api/library/charge-categories` | `LibraryChargeCategoryController` | `ILibraryChargeCategoryAppService` |
| Book categories | `api/library/book-categories` | `BookCategoryController` | `IBookCategoryAppService` |
| Borrowing rules | `api/library/borrowing-rules` | `BorrowingRuleController` | `IBorrowingRuleAppService` |

## Compatibility routes

Some controllers expose more than one route for the same action.
Keep those aliases when changing APIs unless a migration plan removes them.

| Controller | Compatibility route |
|---|---|
| `LibraryChargeController` | `POST api/library/charges/create` |
| `LibraryChargeController` | `GET api/library/charges/details/{id}` |
| `BookIssueController` | `POST api/library/return` |
| `BookIssueController` | `GET api/library/issue-return` |
| `BookIssueController` | `GET api/library/returns/search` |
| `LibraryMembersController` | `GET api/library/members/{memberId}/borrowed` |
| `LibraryMembersController` | `GET api/library/members/{memberId}/reserved` |

## Permission source

Library permissions are defined in these files.

| Need | File |
|---|---|
| Permission definitions | `src/ShikshaMitra.Application.Contracts/Security/Permissions/PermissionDefinitionProviders/LibraryPermissionDefinitionProvider.cs` |
| Operations constants | `src/ShikshaMitra.Domain.Shared/Constants/Permissions/SubmodulePermissions/Library/ShikshaMitraSubmodulePermissions.Library.Operations.cs` |
| Settings constants | `src/ShikshaMitra.Domain.Shared/Constants/Permissions/SubmodulePermissions/Library/ShikshaMitraSubmodulePermissions.Library.Settings.cs` |
