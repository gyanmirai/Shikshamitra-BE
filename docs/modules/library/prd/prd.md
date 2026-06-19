# Library Module PRD

## Purpose

The Library module gives school staff a production-grade workflow for cataloging books, managing physical copies, issuing and returning books, tracking member activity, handling internal Library charges, sending Library notifications, and reporting on circulation and stock health.

The module is currently backend-backed through the active Library menu. It is not yet integrated with Finance settlement, acquisitions, vendors, RFID, ISBN enrichment, SMS, WhatsApp, or scheduled report delivery.

## Actors

| Actor | Description |
| --- | --- |
| Librarian/Admin | Operates catalog, copies, circulation, reservations, charges, reports, notifications, and settings. |
| Student Library Member | Existing student with Library member eligibility. Can borrow, reserve, and receive notifications. |
| Staff Library Member | Existing employee/staff with Library member eligibility. Can borrow, reserve, and receive notifications. |

Parent, guest, and alumni Library members are out of scope for the current release.

## Active Product Surface

| Menu area | Route | Status |
| --- | --- | --- |
| Dashboard | `/library/dashboard` | Backend-backed metrics and alerts. |
| All Books | `/library/book-list` | Catalog, book details, copy management, reservations, labels. |
| Issue & Return | `/library/issued-books` | Issue, return, accession/member lookup, return charges. |
| Library Members | `/library/member-management` | Member list, details, borrowed/reserved books, charges, history. |
| Charges | `/library/charges-management` | Internal Library charge list, detail, create, update, delete. |
| Reports | `/library/reports` | Backend report options, preview, export jobs, download. |
| Notification | `/library/notification` | Settings, history, instant send, reminder execution. |
| Settings | `/library/settings/...` | Book categories, charge categories, hours, borrowing rules, sections/shelves. |

Legacy circulation, duplicate settings, vendor, acquisition, RFID, and other prototype pages are not part of the release surface.

## Delivered Capabilities

### Catalog and Copies

- Manage books with category, academic year, publication metadata, and copy rows.
- Manage copies under a book with accession number, copy number, section, shelf, and copy status.
- Enforce unique accession numbers and safe copy status transitions.
- Generate printable copy-label data with accession number, copy number, section, shelf, and barcode value.

### Reservations

- Reserve a selected book for an active student or staff Library member.
- List reservations for a book.
- Cancel/delete an outstanding reservation for the selected book.
- Enforce member eligibility, duplicate-reservation prevention, and reservation limits.

### Issue and Return

- Issue an available or reserved copy to an active Library member.
- Validate member status, due date, borrowing limits, duplicate active issue, and copy availability.
- Search active issue data by accession number for return.
- Return a book, update copy status, and optionally create an internal Library charge for late, damaged, or lost returns.

### Members

- Use existing students and employees as Library members through Library member flags.
- Show member detail tabs for borrowed books, reserved books, charges, and borrow history.
- Keep parent, guest, alumni, membership expiry, and reactivation workflows out of the current release.

### Internal Charges

- Manage Library-only charges and charge categories.
- Link charges to members and optionally to issue/return context.
- Keep Finance charge/receipt mapping out of current DTOs and workflows.

### Dashboard, Reports, and Labels

- Dashboard overview aggregates stock, availability, active issues, overdue issues, charges, reservations, popular books, weekly circulation, category distribution, section inventory, utilization, and alerts from Library data.
- Reports support options, preview, export job creation, export status/listing, and download.
- Supported report types are Book Master, Stock Summary, Availability Status, Issue Return, Overdue Books, Fine Collection, Lost Damaged Books, Member List, Reservation List, and Popular Books.
- Acquisition, vendor, write-off, audit, membership-expiry, and blocked-member reports are coming soon until those domains exist.

### Notifications and Automation

- Library notification settings support event type, recipient role, channel, active state, reminder offsets, and editable message templates.
- Notification history records sent, skipped, and failed delivery attempts with event, recipient, channel, status, rendered content, error, and related entity context.
- Real delivery channels are In-App and Email. SMS and WhatsApp remain disabled placeholders.
- Reminder automation covers due-date, overdue, fine due, reservation availability/expiry, and low-stock scenarios using existing Library data only.

### Settings

- Configure book categories, charge categories, library hours, borrowing rules, sections, and shelves.
- Settings are scoped by academic year where the backend contract requires it.
- Seed data is intended to be idempotent for demo and test environments.

## Business Rules

- Canonical backend routes are under `/api/library/...`.
- Lists use the Library pagination shape: `items`, `totalCount`, `currentPage`, `pageSize`, `totalPages`.
- Domain failures use `BusinessException` with `LibraryErrorCodes`, not generic success/fake UI behavior.
- Borrowing and reservation limits come from Library borrowing rules.
- Copy status must reflect the physical workflow: available, issued, reserved, lost, or damaged as appropriate.
- Library charges remain internal until Finance integration begins.

## Phase Boundary

Before Finance integration, complete Phase 3E by running the browser E2E release gate against a live local API and FE stack. After Phase 3E is green, Phase 4 can map approved or collectable Library charges into Finance charge and receipt flows while preserving Library charge history.
