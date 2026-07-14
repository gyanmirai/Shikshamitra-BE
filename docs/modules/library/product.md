---
module: library
domain: product
content_type: Conceptual
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Understand Library product behavior

TL;DR: Library manages book inventory, member borrowing, reservations, charges, notifications, and operational reports.
Treat availability, issue status, member history, and charge records as auditable school data.

## Active product areas

This table names the shipped backend areas agents should inspect first.

| Area | Owns | Primary source |
|---|---|---|
| Dashboard | Library operational summary metrics | `Library/Dashboard/**` |
| Books and copies | Catalog records, accession copies, labels, copy status, and reservations | `Library/Books/**` |
| Issue and return | Issue transactions, return transactions, accession lookup, and return search | `Library/IssueReturn/**` |
| Members | Borrowed books, reservations, charges, and member history | `Library/Members/**` |
| Charges | Fine and fee records tied to Library activity | `Library/Charges/**` |
| Reports | Report options, previews, export jobs, and downloads | `Library/Reports/**` |
| Notifications | Reminder settings, history, manual sends, and reminder runs | `Library/Notifications/**` |
| Settings | Sections, library hours, charge categories, book categories, and borrowing rules | `Library/Setting/**` |

## Data-integrity rules

Library changes must preserve these rules.

| Rule | Why it matters |
|---|---|
| Copy status must match issue and return state | It controls whether the same copy can be borrowed twice |
| Reservations must reference valid catalog and member records | It prevents orphan queue entries |
| Charges must remain tied to a member and Library event | It keeps fee history auditable |
| Reports must use the same status semantics as operations | It keeps operational and reporting views consistent |
| Notification history must reflect sends and reminder runs | It gives staff an audit trail |

## Known partial surfaces

The active docs summarize the backend surface.
They do not replace source inspection for every request shape.

| Surface | Current documentation status |
|---|---|
| Full DTO field catalog | Inspect `Application.Contracts/Library/**` |
| Notification delivery plumbing | Inspect `Application/Library/Notifications/**` and the host reminder worker |
| Export file formats | Inspect `Application/Library/Reports/**` |
