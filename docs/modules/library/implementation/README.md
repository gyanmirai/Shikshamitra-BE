# Library Implementation Handoff

This page orients future agents to the delivered Library implementation. Use it with the [PRD](../prd/prd.md) and [API contract](../contracts/curated/library-api.md).

## Backend Shape

- Controllers live under `ShikshaMitra.HttpApi/Controllers/Library` and expose canonical `/api/library/...` routes.
- Application contracts live under `ShikshaMitra.Application.Contracts/Library` with DTO groups for books, circulation, members, charges, dashboard, reports, notifications, and settings.
- Domain failures should use `BusinessException` and `LibraryErrorCodes`.
- Permissions are defined in the Library permission partials under `ShikshaMitra.Domain.Shared/Constants/Permissions/SubmodulePermissions/Library`.
- Demo/test Library data is seeded by the Library seeder and must remain idempotent.

## Frontend Shape

- Active Library menu and permissions are defined in `Shikshamitra-FE/src/constants/library-menus.ts`.
- Backend-backed RTK slices live under `Shikshamitra-FE/src/redux/library`, plus reservation compatibility code under `src/redux/book-reservation-api`.
- DTO mapping is centralized in `src/redux/library/library-adapters.ts`; keep screen display types stable and convert at API or hook boundaries.
- Active Library screens expose stable `data-testid` markers for E2E smoke coverage.
- Prototype routes are guarded by release-surface tests and should not be reintroduced into the active menu without backend integration.

## Current Active Routes

- `/library/dashboard`
- `/library/book-list`
- `/library/issued-books`
- `/library/member-management`
- `/library/charges-management`
- `/library/reports`
- `/library/notification`
- `/library/settings/book-category`
- `/library/settings/charge-type`
- `/library/settings/library-timing`
- `/library/settings/borrowing-and-return-rules`
- `/library/settings/sectionManagement`

## Do Not Expand Yet

Do not start Finance settlement, acquisitions/vendor workflows, write-off, RFID, ISBN/OpenLibrary enrichment, SMS, WhatsApp, report scheduling, parent/guest/alumni members, or membership-expiry workflows until the current release surface has browser E2E sign-off.

## Next Agent Checklist

- Read this documentation pack first.
- Confirm the local API and FE app are running before browser QA.
- Run the Library E2E suite with `E2E_LIBRARY=1`.
- Fix only active release-surface defects before starting Phase 4.
- Keep Library charges internal until Finance integration begins.
