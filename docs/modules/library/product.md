---
module: library
domain: product
content_type: Conceptual
status: canonical
updated_on: 2026-11-19
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
| Stock receiving and CSV import | Reviewed new stock, acquisition records and retained batch outcomes | `Library/Receiving/**` |
| Classroom custody | Bulk handoffs to school staff, outstanding accessions, and partial receipts | `Library/Custody/**` |
| Campus transfers | Campus section association, cross-campus dispatch, and partial receiving | `Library/CampusTransfers/**` |
| My Library | Catalogue availability, owned loans, own reservations, and reviewed renewals for linked student or guardian accounts | `Library/Portal/**` |
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
| Copies in classroom custody require a custody receipt | It keeps classroom stock accounted for and unavailable for ordinary checkout |
| Reservations must reference valid catalog and member records | It prevents orphan queue entries |
| Charges must remain tied to a member and Library event | It keeps fee history auditable |
| Reports must use the same status semantics as operations | It keeps operational and reporting views consistent |
| AI plans must compile to the typed report contract | It keeps model output away from SQL, repositories, and unrestricted identifiers |
| Library rows must be limited by tenant, academic-year scope, and student UserScope | It prevents a broad Library report permission from exposing inaccessible students |
| Notification history must reflect sends and reminder runs | It gives staff an audit trail |

## School-specific catalogue rules

Without a saved override, Author and Language remain required; ISBN, Publisher, Edition, Subject, Applicable Age and Material remain optional.
Default languages retain Gujarati=0, Hindi=1, English=2, Marathi=3 and Sanskrit=4. Material choices start empty; existing materials remain unspecified.

| School example | Configuration | Result |
|---|---|---|
| Primary school receiving anonymous donations | Make Author optional; add Bengali code `bn` and a picture-book material | Empty author and explicitly selected Bengali are valid; no author or built-in language is invented |
| Secondary school keeping its current process | Keep Defaults | Required fields and all five numeric language meanings continue to work |
| School adding a bilingual collection | Add stable language/material codes | Current labels appear in catalogue reads, circulation lookups and book reports without settings-read grants |

Configuration management requires dedicated View/Update actions and tenant-wide scope. Catalogue editors receive effective rules through their own editor-options authority.
Saved codes must be retained; administrators retire choices instead of deleting their meanings. Existing books may retain their unchanged retired choice, while new selections must be active.
Newly required blank metadata must be completed when editing that metadata. Reading old records and handling physical stock do not backfill descriptions.
The server rejects stale configuration revisions before catalogue writes. This token protects school configuration; it is separate from book and copy concurrency.
Metadata and complete copy edits also carry their own reviewed revisions. An older copy list cannot remove a copy added by another operator, and an older metadata form cannot replace a saved description.
Metadata-only edits preserve stock. Conflict responses retain the draft for explicit review; they must not trigger automatic retries with newly fetched revisions.
Whole-title deletion requires its own Delete action and no retained copy or reservation history. It does not require Update.
Detail exposes the current deletion decision and reason; the delete command checks the same facts again under the inventory fence.
Material labels describe records. They do not establish a digital licence, RFID connection or reference-only circulation restriction.
Catalogue price may be Unknown. A donated book with no known price stores null; a free book retains its known zero.
Existing stored prices are preserved, including historic negative values; only unchanged historic values can be retained during metadata edits, while newly entered or changed values must be nonnegative.
Exact known prices retain two decimal places through catalogue reads and reports. Reports leave Unknown empty rather than inventing zero or currency.
Receiving acquisition costs and any approved replacement assessment remain independent records.
Permanent catalogue, categories and locations belong to the school across academic years; a title's origin year must not hide holdings from a currently authorized operator.
Personal and financial activity still requires current tenant/user scope and its recorded context.

## School setup examples

| School setup | First operating flow |
|---|---|
| Small school with a shared lending desk | Use a single section and Manual lending desk example. Receive and label books, then enter due dates and reservation expiries at the desk. |
| Primary school with classroom sets | Select the Weekly classroom reading example for personal loans. Record a separate staff custody handoff for the class set and receive the copies individually. |
| Secondary or multilingual school | Start with Secondary school lending. Configure catalogue languages and material codes, then publish the reviewed borrowing rules for the applicable scope. |
| School with several campuses | Assign real campus sections, select the pickup desk for a request, and dispatch stock between campuses through the transfer register. |
| School using linked student or guardian accounts | Grant the school-selected portal actions. Confirm the Library calendar and publish automatic rules for online requesting and renewal. |

Use Section Management, Catalogue Configuration, and Borrowing Rules to adapt these examples. Example terms populate a new draft and require the existing review and publish steps.

Use CSV receiving to bring holdings exported from another library system into the physical register. Barcode workflows accept input from a keyboard or a scanner configured as a keyboard. Live ILS synchronization, digital entitlement providers, RFID-specific hardware integration and queued offline circulation require their respective integrations.

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
