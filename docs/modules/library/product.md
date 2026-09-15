---
module: library
domain: product
content_type: Conceptual
status: canonical
updated_on: 2026-09-15
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
| AI plans must compile to the typed report contract | It keeps model output away from SQL, repositories, and unrestricted identifiers |
| Library rows must be limited by tenant, academic-year scope, and student UserScope | It prevents a broad Library report permission from exposing inaccessible students |
| Notification history must reflect sends and reminder runs | It gives staff an audit trail |

## Known partial surfaces

The active docs summarize the backend surface.
They do not replace source inspection for every request shape.

| Surface | Current documentation status |
|---|---|
| Full DTO field catalog | Inspect `Application.Contracts/Library/**` |
| Notification delivery plumbing | Inspect `Application/Library/Notifications/**` and the host reminder worker |
| Export file formats | Inspect `Application/Library/Reports/**` |

## AI report behavior

The Library AI coordinator understands inventory, circulation, overdue, reservation, member, fine, and popularity requests.
It selects among the ten executable Library report types, chooses report-specific columns and filters, and asks for clarification only when a missing choice materially changes the result.
The user reviews a live authorized preview before approving PDF or Excel generation.
The model is a planner only; deterministic application code validates and executes the report.
