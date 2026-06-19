---
module: library
domain: library
status: canonical
updated_on: 2026-06-18
owner: backend-team
---

# Library API Contract

## Purpose

Canonical backend contract for the Library module release surface. Frontend RTK endpoints should omit the `/api` prefix because the shared base query adds it.

## Request Headers

- `Authorization: Bearer <token>`
- `__tenant: <tenant-id>`

## Response Conventions

- Paginated Library lists return `items`, `totalCount`, `currentPage`, `pageSize`, and `totalPages`.
- Create mutations return `GenericCreatedResponse` with an `id` when applicable.
- Update mutations return `GenericUpdatedResponse`.
- Domain failures return `BusinessException` problem details with a `Library:*` error code from `LibraryErrorCodes`.

## Canonical Routes

### Books, Copies, Labels, and Reservations

| Method | Route | Purpose | Permission area |
| --- | --- | --- | --- |
| `GET` | `/api/library/books` | List/search books. | `Library.AllBooks.View` |
| `POST` | `/api/library/books` | Create a book, including copy rows when provided. | `Library.AllBooks.Add` |
| `GET` | `/api/library/books/{bookId}` | Get book details. | `Library.AllBooks.View` |
| `PUT` | `/api/library/books/{bookId}` | Update book metadata and copy rows. | `Library.AllBooks.Update` |
| `DELETE` | `/api/library/books/{bookId}` | Delete a book when not blocked by active issues. | `Library.AllBooks.Delete` |
| `GET` | `/api/library/books/{bookId}/copies` | List copies for a book. | `Library.AllBooks.View` |
| `PUT` | `/api/library/books/{bookId}/copies` | Replace/update copy rows for a book. | `Library.AllBooks.Update` |
| `PUT` | `/api/library/books/{bookId}/copies/{accessionNumber}/status` | Update one copy status. | `Library.AllBooks.Update` |
| `GET` | `/api/library/books/{bookId}/copy-labels` | Return printable copy-label data. | `Library.AllBooks.View` |
| `GET` | `/api/library/books/{bookId}/reservations` | List reservations for a book. | `Library.IssueReturn.View` |
| `POST` | `/api/library/books/{bookId}/reservations` | Reserve a book for a member. | `Library.IssueReturn.Reserve` |
| `DELETE` | `/api/library/books/{bookId}/reservations/{reservationId}` | Cancel/delete an outstanding reservation for the selected book. | `Library.IssueReturn.Reserve` |

Important payloads:

- Create/update book: `title`, `author`, `publisher`, `isbn`, `publicationYear`, `academicYearId`, `categoryId`, optional `copies`.
- Copy row: `accessionNo`, `copyNumber`, `sectionId`, `shelfId`, `status`.
- Reservation: `memberId`, `requestedDate`, `reserveDueDate`.

### Circulation

| Method | Route | Purpose | Permission area |
| --- | --- | --- | --- |
| `POST` | `/api/library/issues` | Issue a copy to a member. | `Library.IssueReturn.Issue` |
| `POST` | `/api/library/returns` | Return a copy and optionally create a charge. | `Library.IssueReturn.Return` |
| `GET` | `/api/library/books/accession/{accessionNo}` | Lookup book/copy details by accession. | `Library.IssueReturn.View` |
| `GET` | `/api/library/issue-returns` | List issue/return rows. | `Library.IssueReturn.View` |
| `GET` | `/api/library/returns/search?accessionNo=...` | Lookup active issue data for return. | `Library.IssueReturn.View` |
| `GET` | `/api/library/issuedBook/accession/{accessionNo}/member` | Lookup current borrower by accession. | `Library.IssueReturn.View` |

Important payloads:

- Issue: `accessionNo`, `memberId`, `issueDate`, `dueDate`.
- Return: `accessionNo`, `memberId`, `returnDate`, optional `chargeCategoryId`, `amount`, `fineDueDate`, `copyStatus`, `remarks`.

Legacy-compatible aliases exist for some circulation routes, but new integrations should use the canonical routes above.

### Members

| Method | Route | Purpose | Permission area |
| --- | --- | --- | --- |
| `GET` | `/api/library/members` | List student/staff Library members. | `Library.LibraryMembers.View` |
| `GET` | `/api/library/members/{memberId}` | Member detail summary. | `Library.LibraryMembers.View` |
| `GET` | `/api/library/members/{memberId}/borrowed-books` | Active borrowed books tab. | `Library.LibraryMembers.View` |
| `GET` | `/api/library/members/{memberId}/reserved-books` | Reserved books tab. | `Library.LibraryMembers.View` |
| `GET` | `/api/library/members/{memberId}/charges` | Member charges tab. | `Library.LibraryMembers.View` |
| `GET` | `/api/library/members/{memberId}/borrow-history` | Borrow history tab. | `Library.LibraryMembers.View` |

### Charges

| Method | Route | Purpose | Permission area |
| --- | --- | --- | --- |
| `GET` | `/api/library/charges` | List internal Library charges. | `Library.Charges.View` |
| `POST` | `/api/library/charges` | Create an internal Library charge. | `Library.Charges.Add` |
| `GET` | `/api/library/charges/{id}` | Get charge detail. | `Library.Charges.View` |
| `PUT` | `/api/library/charges/{id}` | Update charge detail/status. | `Library.Charges.Update` |
| `DELETE` | `/api/library/charges/{id}` | Delete a Library charge. | `Library.Charges.Delete` |

Charge create/update uses Library DTOs only. Do not add Finance receipt or payable fields until Phase 4.

### Dashboard

| Method | Route | Purpose | Permission area |
| --- | --- | --- | --- |
| `GET` | `/api/library/dashboard/overview?academicYearId=...` | Composite stock, circulation, charge, reservation, trend, utilization, and alert metrics. | `Library.Dashboard.View` |

### Reports

| Method | Route | Purpose | Permission area |
| --- | --- | --- | --- |
| `GET` | `/api/library/reports/options` | Available report types, coming-soon types, filters, and export formats. | `Library.Reports.View` |
| `POST` | `/api/library/reports/preview` | Preview report rows and columns. | `Library.Reports.View` |
| `POST` | `/api/library/reports/exports` | Create an export job. | `Library.Reports.Export` |
| `GET` | `/api/library/reports/exports` | List export jobs for the current user/context. | `Library.Reports.View` |
| `GET` | `/api/library/reports/exports/{jobId}` | Get one export job status. | `Library.Reports.View` |
| `GET` | `/api/library/reports/exports/{jobId}/download` | Download a ready export. | `Library.Reports.Export` |

Report request shape: `reportType`, `format`, `page`, `pageSize`, optional `sorts`, and `filters`.

Supported Phase 3A report types are Book Master, Library Stock Summary, Book Availability Status, Issue Return, Overdue Books, Fine Collection, Lost Damaged Books, Library Member List, Book Reservation, and Popular Books.

### Notifications

| Method | Route | Purpose | Permission area |
| --- | --- | --- | --- |
| `GET` | `/api/library/notifications/settings` | Read event/channel settings and channel options. | `Library.Notification.View` |
| `PUT` | `/api/library/notifications/settings` | Update notification settings. | `Library.Notification.Add` |
| `GET` | `/api/library/notifications/history` | Read delivery history. | `Library.Notification.View` |
| `POST` | `/api/library/notifications/send` | Send an instant notification. | `Library.Notification.Add` |
| `POST` | `/api/library/notifications/run-reminders` | Manually run reminder automation. | `Library.Notification.Add` |

Real channels are `InApp` and `Email`. `Sms` and `WhatsApp` remain disabled placeholders.

Event groups are `BookManagement`, `MemberManagement`, and `Transactions`. Current real event types include issue confirmation, due reminder, overdue reminder, return confirmation, return charge reminder, reservation confirmed, reserved book available, reservation expiring, fine due reminder, and low stock alert.

### Settings

| Method | Route | Purpose | Permission area |
| --- | --- | --- | --- |
| `GET/POST/GET/PUT/DELETE` | `/api/library/book-categories` and `/{categoryId}` | Book category CRUD. | `Library.Settings.BookCategories.*` |
| `GET/POST/GET/PUT/DELETE` | `/api/library/charge-categories` and `/{categoryId}` | Charge category CRUD. | `Library.Settings.ChargesCategories.*` |
| `GET/POST` | `/api/library/library-hours` | Read/update weekly Library hours. | `Library.Settings.LibraryHours.*` |
| `GET/POST` | `/api/library/borrowing-rules` | Read/update borrowing and reservation rules. | `Library.Settings.BorrowingRules.*` |
| `GET/POST/GET/PUT/DELETE` | `/api/library/sections` and `/{sectionId}` | Section and shelf management. | `Library.Settings.SectionManagement.*` |

Settings endpoints that take `academicYearId` require the active academic year from the UI/app shell.

## Error Codes

Use the central `LibraryErrorCodes` catalog. Common cases include:

| Code | Meaning |
| --- | --- |
| `Library:Books:010008` | Accession number already exists. |
| `Library:Books:010011` | Copy is not available for the requested operation. |
| `Library:Books:010012` | Invalid copy status transition. |
| `Library:Members:010018` | Member not found. |
| `Library:Members:010019` | Member is inactive. |
| `Library:Circulation:010020` | Borrowing limit exceeded. |
| `Library:Circulation:010021` | Reservation limit exceeded. |
| `Library:Circulation:010022` | Duplicate active issue. |
| `Library:Circulation:010027` | Reservation not found or not valid for the selected book. |
| `Library:Charges:010031` | Library charge not found. |
| `Library:Reports:010035` | Report type not supported. |
| `Library:Notifications:010041` | Notification channel disabled. |

## Change Log

- 2026-06-18: Created canonical Library contract handoff after Phase 3D release hardening.
