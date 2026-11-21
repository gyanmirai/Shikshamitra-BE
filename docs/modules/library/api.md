---
module: library
domain: api
content_type: Reference
status: canonical
updated_on: 2026-11-20
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
| Canonical loans, return, renewal and date review | `api/library/circulation/loans` | `LibraryLoanController` | `ILibraryLoanAppService`, `ILibraryCirculationReadAppService` |
| Checkout preview | `api/library/circulation/issues` | `LibraryIssuePlanController` | `ILibraryIssuePlanAppService` |
| Borrower/copy lookups | `api/library/circulation/lookups` | `LibraryCirculationLookupController` | `ILibraryCirculationLookupAppService` |
| Borrower search | `api/library/circulation/members` | `LibraryCirculationMemberController` | `ILibraryCirculationMemberAppService` |
| Compatibility issue and return | `api/library` | `BookIssueController` | `IBookIssueAppService` |
| Copy history | `api/library/books` | `LibraryCopyHistoryController` | `ILibraryCopyHistoryAppService` |
| Copy lifecycle | `api/library/books/{bookId}/copies/{copyId}` | `LibraryCopyLifecycleController` | `ILibraryCopyLifecycleAppService` |
| Reservations | `api/library/reservations` | `LibraryReservationController` | `ILibraryReservationAppService` |
| Reservation member options | `api/library/reservations/members` | `LibraryReservationMemberController` | `ILibraryReservationMemberAppService` |
| Stock receiving | `api/library/receiving` | `LibraryReceivingController` | `ILibraryReceivingAppService` |
| Stock import | `api/library/receiving/imports` | `LibraryStockImportController` | `ILibraryStockImportAppService` |
| Stock verification | `api/library/stock-verification` | `LibraryStockVerificationController` | `ILibraryStockVerificationAppService` |
| Classroom custody | `api/library/custody` | `LibraryCustodyController` | `ILibraryCustodyAppService` |
| Campus transfers | `api/library/campus-transfers` | `LibraryCampusTransferController` | `ILibraryCampusTransferAppService` |
| My Library | `api/mobile/v1/library/students`, `api/mobile/v1/students/{studentId}/library` | `LibraryPortalController` | `ILibraryPortalAppService` |
| Members | `api/library/members` | `LibraryMembersController` | `ILibraryMemberAppService` |
| Charges | `api/library/charges` | `LibraryChargeController` | `ILibraryChargeAppService` |
| Charge editor options | `api/library/charges` | `LibraryChargeEditorController` | `ILibraryChargeEditorLookupAppService` |
| Reports | `api/library/reports` | `LibraryReportController` | `ILibraryReportAppService` |
| Notifications | `api/library/notifications` | `LibraryNotificationController` | `ILibraryNotificationAppService` |
| Notification recipient options | `api/library/notifications/recipients` | `LibraryNotificationRecipientLookupController` | `ILibraryNotificationRecipientLookupAppService` |
| Own recipient inbox | `api/library/inbox` | `LibraryInboxController` | `ILibraryInboxAppService` |
| Sections | `api/library/sections` | `LibrarySectionController` | `ILibrarySectionAppService` |
| Library hours | `api/library/library-hours` | `LibraryHoursController` | `ILibraryTimingAppService` |
| Charge categories | `api/library/charge-categories` | `LibraryChargeCategoryController` | `ILibraryChargeCategoryAppService` |
| Book categories | `api/library/book-categories` | `BookCategoryController` | `IBookCategoryAppService` |
| Borrowing rules | `api/library/borrowing-rules` | `BorrowingRuleController` | `IBorrowingRuleAppService` |
| Versioned borrowing policies | `api/library/policies` | `LibraryBorrowingPolicyController` | `ILibraryBorrowingPolicyAppService` |
| Academic-year rollover preview | `api/library/policies/rollover` | `LibraryRolloverController` | `ILibraryRolloverAppService` |
| School calendar and closures | `api/library/calendar` | `LibraryCalendarController` | `ILibraryCalendarAppService` |

## Reservation pickup and My Library

`GET /api/library/reservations/pickup-locations?academicYearId={id}` requires catalogue View and Reserve. It returns actual sections at active school campuses. Staff can submit an optional `pickupSectionId` with preview and creation. The request retains the section, campus and display label. With a choice, the hold allocates only from that section. With a blank choice, any otherwise eligible copy can satisfy the request.

`GET /api/mobile/v1/library/students` uses exact `Library.Portal.View` to list students linked to the current active, verified portal account. `GET` requests for `catalogue`, `loans`, and `reservations` under a selected student's Library path require the same grant. Each request resolves that student's persisted account association in the current school.

`Library.Portal.Reserve` also permits `GET pickup-locations`, `POST reservations/preview`, `POST reservations` and `POST reservations/{id}/cancel`. A student or guardian supplies a title and optional pickup section. The school's configured timezone and applicable borrowing policy supply the request date and automatic pickup terms. Staff enter dates for requests governed by manual terms. Cancel sends the saved revision.

`Library.Portal.Renew` also permits `GET loans/{loanId}/renewal-preview` and `POST loans/{loanId}/renew`. The command carries the reviewed revision, school date and operation key. The shared loan workflow checks the original terms, outstanding requests, loan limit and school calendar under the inventory lock. Staff review manual dates or a changed Library timezone. Changed opening hours and closures require a fresh preview.

`GET receipt-years` and `GET receipts?academicYearId={id}` under the selected student's Library path require both exact `Library.Portal.View` and `Finance.Fees.InvoiceReceipt.View`. Finance checks the student's account association and access to the selected academic year. The list includes official Finance receipts issued for collections on typed Library assessments owned by that student, with their recorded kind and verification status. The PDF uses the independently authorized Finance endpoint `GET /api/mobile/v1/finance/students/{studentId}/receipts/{receiptId}/pdf?academicYearId={id}`.

## Campus configuration and transfers

`GET /api/library/sections/campus-options?academicYearId={id}` lists the school's active campuses. `PUT /api/library/sections/{sectionId}/campus?academicYearId={id}` accepts `{ campusId }` and binds the section and its existing shelves to that campus. Both require exact Section Management View and Add and school-wide scope. An exact duplicate succeeds and attempting to select a different campus for a bound section is rejected. Section reads expose nullable `campusId` and `campusName`.

All `/api/library/campus-transfers` calls require exact `Library.CampusTransfers.View`, `Library.AllBooks.View`, and school-wide user scope.

| Request | Additional action | Result |
|---|---|---|
| `GET /`, `GET /{id}` | None | Paged transfers or a saved route, physical copy states and receipt history |
| `GET /locations`, `GET /copies?sourceSectionId={id}&search={text}` | `Library.CampusTransfers.Dispatch` | Active campus sections and shelves, and eligible copy candidates at the selected source |
| `POST /preview`, `POST /` | `Library.CampusTransfers.Dispatch` | Review and dispatch up to 100 distinct copies to a shelf at a different active campus |
| `POST /{id}/receipts/preview`, `POST /{id}/receipts` | `Library.CampusTransfers.Receive` | Review and receive an outstanding subset at the recorded destination |

Both writes require the reviewed revision and idempotency key. Dispatch accepts a civil date and reason. Receipts accept a civil date on or after dispatch, optional notes, and a physical condition of Available, Damaged, or Under Repair for each received copy.

## Classroom custody contracts

All requests require exact `Library.Custody.View`, `Library.AllBooks.View`, and school-wide user scope in the current tenant.

| Request under `/api/library/custody` | Additional action | Result |
|---|---|---|
| `GET /`, `GET /{id}` | None | Paged handoffs or saved copies, individual conditions, and receipts |
| `GET /options?academicYearId={id}`, `GET /copies?search={text}` | `Library.Custody.Add` | Real handoff choices and available copy search |
| `POST /preview`, `POST /` | `Library.Custody.Add` | Review and record a custody handoff of up to 100 distinct copies |
| `POST /{id}/returns/preview`, `POST /{id}/returns` | `Library.Custody.Return` | Review and receive an explicit outstanding subset |

The handoff uses a selected active employee and a civil handoff date within the selected academic year. Class, room, and expected return date are optional. The receipt records a civil date, optional notes, and each received copy's condition of Available, Damaged, or Under Repair. Supply the preview revision and an idempotency key on each write. A stale preview needs a new review.

## Rollover preview contracts

`GET /api/library/policies/rollover/options` returns the tenant's authorized year identifiers, labels and dates. `POST /api/library/policies/rollover/preview` accepts a source year, a later target year and an optional school date. A blank date uses the target year's first day. The date must lie within the target year.

Both requests require exact `Library.Settings.BorrowingRules.View`. The preview requires current access to both selected years. It reports verified target calendar prerequisites for automatic dates, visible policy versions effective on the date, and visible drafts created for the target year. Effective versions include applicable school-wide and category or location rules from earlier years. Class and stage rules belong to their actual academic targets.

Loan totals require exact `Library.IssueReturn.View`. The response gives outstanding loans recorded in the source year. A school-wide reader also receives a separate count for loans with an unconfirmed year. Scoped responses count only readable recorded loans and label their scope. The existing policy publish and physical return endpoints own the respective changes.

## Stock import contracts

Use `GET /api/library/receiving/imports/options?academicYearId={id}` for allowed academic years, destinations, available categories and effective catalogue rules. The chosen section belongs to the selected year. A school-wide operator can also choose a legacy section with no recorded year.

| Method and path under `/api/library/receiving/imports` | Result |
|---|---|
| `POST /preview` | Copy actions, new title count, CSV line and field errors, and a revision when the entire batch can be accepted |
| `POST /` | Atomic commit with `import`, `revision` and `idempotencyKey` |
| `GET /` | Paged accepted batches for the actor's current tenant and academic-year scope |
| `GET /{id}` | Saved batch with the original source row IDs, copy identities, accessions and receiving receipt IDs |

Both exact View grants, `Library.Receiving.View` and `Library.AllBooks.View`, are required on every endpoint. Options, preview and commit also require `Library.Receiving.Add`. A batch creating catalogue titles requires `Library.AllBooks.Add`, including when the actor retries that accepted write.

The input uses `csv`, `academicYearId`, `sectionId`, `shelfId`, optional `categoryId`, `receivedOn`, `source`, optional `sourceName` and optional `columnMap`. Each mapping key is a canonical template field and its value is a CSV heading. CSV input supports up to 1 MiB, 64 columns and 1,000 copy rows. Source IDs and accessions are text and match case-insensitively within the school. A source ID identifies one copy across all accepted school batches.

Exactly one of `BookId` and a new-title definition identified by `TitleKey` identifies each copy's title. A new title uses the destination year's selected category or an eligible unassigned legacy category. Explicit acquisition cost and currency stay on the receipt. Blank catalogue price means Unknown for a new title. A blank accession requests assignment at import.

The server returns `Library:Receiving:010095` with status 409 for changed preview evidence and `Library:Receiving:010096` with status 409 for a reused actor key carrying different input. An exact replay returns the original accepted batch without creating new stock.

## Stock verification contracts

All stock verification routes require `Library.InventoryVerification.View` and `Library.AllBooks.View`. The server resolves the current school and academic scope before serving any record. The whole-school option requires tenant-wide scope. Section counts retain their recorded academic year for later reads.

| Operation | Route suffix | Additional action |
|---|---|---|
| List counts and read available locations | `GET /` and `GET /options` | View pair |
| Read a count, its baseline or its scan history | `GET /{id}`, `GET /{id}/baseline`, `GET /{id}/observations` | View pair |
| Read live reconciliation for a counting or in-review session | `GET /{id}/reconciliation` | View pair |
| Preview or open a count | `POST /preview` and `POST /` | `Library.InventoryVerification.Add` |
| Record a scan | `POST /{id}/observations` | `Library.InventoryVerification.Update` |
| Close scanning for review or cancel the count | `POST /{id}/review` and `POST /{id}/cancel` | `Library.InventoryVerification.Update` |
| Read retained finding decisions | `GET /{id}/decisions` | View pair |
| Record a decision on a finding | `POST /{id}/decisions` | `Library.InventoryVerification.Update` |
| Preview and approve the reviewed count | `GET /{id}/approval-preview`, `POST /{id}/finalize` | `Library.InventoryVerification.Approve` |
| Read the published result | `GET /{id}/result` | View pair |

Pass a school calendar date or use the confirmed Library clock. Opening requires the preview revision and a retry key. A scan accepts an accession or the Library copy barcode, optional observed section and shelf, school date and retry key. The server returns the current count on an exact opening retry and the original observation on an exact scan retry. The key belongs to the acting user in the current school. A changed request using the same key or a changed preview returns conflict.

Reconciliation accepts `source=1` for expected copies (default), `source=2` for recognized copies outside the baseline, or `source=3` for unknown scans. It follows the same page bounds as the baseline. Each expected or unexpected recognized copy uses its latest observation sequence. Unknown scans retain their original recognition outcome and sequence. Expected rows may include the opening status and currently readable register status. The findings contain no borrower or reservation identifiers.

Start review with `expectedObservationCount` and `idempotencyKey` while the session is Counting. Cancel from Counting or In Review with the same fields, `expectedStatus`, and a nonblank `reason` up to 500 characters. A changed count or stage returns conflict. An exact retry returns the accepted transition even if the session has moved on. Read the count detail for its current status, transition actor, time, and cancellation reason.

Decision requests carry the reconciliation source, row ID, evidence revision, the latest decision ID or null, an outcome, a reason, and a retry key. New decisions require In Review and a finding that needs review. The evidence and latest decision must still match. Each accepted decision appends a version. Reconciliation returns the latest decision and whether its evidence remains current. The paginated history groups versions by finding and shows its newest decision first, including after cancellation.

Approval requires the preview revision and an idempotency key. The server checks the recorded count, current evidence, and current decisions under the inventory lock. A changed preview or any missing or stale decision returns HTTP 409. The result accepts the same three source choices as reconciliation. The result returns the approval actor and time, the saved counts, and a page of approved findings with their recorded locations, copy statuses, and decision versions. View access to the count is sufficient to read it.

An outside-baseline finding includes `receivedAfterOpening` when a matching stock receipt was recorded after the count opened and by the time of that scan. The server uses the receipt's recording timestamp and the count's review scope. This evidence is retained in the published result.

## Exact-loan read context

`GET api/library/circulation/loans/{loanId}` retains `Library.IssueReturn.View`, current active identity/tenant and the saved loan's recorded scope.
It adds the existing `schoolContext` shape: nullable `schoolToday`/`confirmedTimeZoneId`, `isConfirmed`, `requiresExplicitDate`, `unavailableReason` and authorized academic-year `{ id, name }` choices.
Only an explicitly configured valid Library timezone confirms the school day; a missing, invalid or HR-only timezone retains an unknown day and explicit-entry guidance.
Year choices are real tenant rows within the actor's authorized years, independently of the saved loan or title origin year.
No Return, Calendar or Academic setup action is needed for this owning read.

The additive `canReviewDates` requires exact `Library.IssueReturn.ReviewDates` and at least one field the existing review command may accept.
Explicit issue/due dates and a known original due date cannot be overwritten through legacy date review; a returned loan may still have an uncertain return date.
These nullable DTO properties are populated only for the exact-loan GET and omitted from existing command/replay and nested return-lookup payloads.
They do not enter loan revisions, rewrite stored command JSON or change command authorization.

## Catalogue configuration contract

| Action | Exact authority | Contract |
|---|---|---|
| `GET api/library/catalogue/configuration` | `Library.Settings.CatalogueConfiguration.View`, active identity and school scope | Effective revision/source/canManageConfiguration, requirements and choices |
| `PUT api/library/catalogue/configuration` | Dedicated View **and** Update, plus tenant-wide scope | `{ expectedRevision, definition: { requiredFields, languages, materials } }` |
| `GET api/library/books/editor-options?academicYearId=...` | `Library.AllBooks.View` **and** Add or Update; authorized year | Categories, locations and effective catalogueConfiguration; no settings-read grant |

Requirements use fixed keys `author`, `isbn`, `publisher`, `edition`, `subject`, `language`, `applicableAge`, `material`.
Choices contain `code`, `label`, `isActive`. Codes are unique lowercase ASCII identifiers up to 32 characters; labels support Unicode up to 128 characters; each list supports 200 choices.
Title/category and existing price/page/copy invariants remain outside this configurable list.

Book create/update accept `expectedCatalogueConfigurationRevision`, nullable numeric `language` and nullable `languageCode`/`materialCode`.
Old numeric meanings map to `gu`/`hi`/`en`/`mr`/`sa`; custom codes use numeric Language=null. Conflicting code/numeric input is rejected.
An omitted token is accepted only without a school override. Every saved override requires the exact current revision, including one whose rules equal Defaults.
List/detail return canonical codes and current labels under catalogue View authority. These labels are not historical acquisition or loan snapshots.
Configuration source is `Defaults` or `School`. There is no Delete action or automatic grant seeding.

| Error | Meaning |
|---|---|
| `Library:Catalogue:010085` | Invalid requirements/choices or removal of saved codes |
| `Library:Catalogue:010086` | HTTP 409 configuration conflict; review current rules before retrying |
| `Library:Catalogue:010087` | Metadata violates effective school rules |

Sources: `LibraryCatalogueConfigurationController`, `ILibraryCatalogueConfigurationAppService` and `ShikshaMitraSubmodulePermissions.Library.Catalogue.cs`.

## Catalogue price contract

Book create/update accept nullable `price` and nullable `priceText`. Both null or omitted mean Unknown; numeric zero remains a known price.
Updated clients send `{ "price": null, "priceText": "12.50" }` for a known value and both null for Unknown.
Known values must be nonnegative, within `decimal(18,2)` and have at most two decimal places; malformed text and contradictory numeric/text values return `Library:Catalogue:010087`.
Exact text uses invariant digits and a decimal point, without grouping, symbols, whitespace or exponents.
List/detail return nullable numeric `price` plus nullable exact `priceText`; prefer the text field to avoid JavaScript precision loss.
An unchanged historic negative may be retained during an unrelated edit; it cannot authorize a newly changed negative price.
Catalogue price does not establish currency, acquisition cost or a Finance assessment.

## Catalogue mutation revisions

Detail returns independent opaque `metadataRevision` and `copiesRevision` strings.
Metadata PUT requires `expectedMetadataRevision`; complete copy PUT requires `expectedCopiesRevision`, including an empty replacement list.
A combined metadata PUT with nonempty `copies` requires both revisions. The server checks them under the tenant inventory fence before mutation or accession allocation.
Create, metadata update and copy PUT retain `id`/`message` and return the accepted command's resulting revisions.
Keep opened revisions with the draft; advance them only from that command's successful response. Review a conflict explicitly before another write.
Missing or stale revisions return `Library:Catalogue:010088` with HTTP 409. Revisions do not replace action, active identity, tenant or scope checks.
The copy revision covers the whole physical set and retention facts, independently of paging. Metadata revisions exclude derived copy count and framework audit stamps.

Book detail also returns `canDelete` and `deletionBlockedReason` under the current Delete action, independently of Update.
The decision reuses the full copy retention facts and checks reservation history; list reads do not load full copy sets for each row.
The DELETE command repeats that decision under the inventory fence. A prior allowed detail response never authorizes deleting newly received or historical stock.

## Library hours capability

`GET api/library/library-hours?academicYearId=...` retains its paging/items fields and adds boolean `canManageHours`.
It reflects the current own Hours View/Add actions, active school identity, authorized year and actual tenant-wide management scope.
A scoped reader can read the authorized year's hours without receiving permission to change the whole-school schedule.
Calendar management uses its existing Borrowing Rules action; it does not establish Hours management authority.
The POST body remains `{ hours: [...] }` and the server rechecks its exact actions and scope before changing rows.
Status values remain Open=0, Closed=1 and Holiday=2; Open times use the existing `TimeSpan` contract such as `11:45:00`.
No school-hours copying provider is implied by this API.

## Dashboard financial summary

`GET api/library/dashboard/overview?academicYearId=...` requires Dashboard View, an active actor and current tenant/user scope.
The financial population uses the independently recorded assessment year and recorded source scope; category year, current borrower placement and original loan year do not substitute for them.

| Response field | Meaning |
|---|---|
| `financePosition.verifiedCount` | Sources with valid canonical Finance evidence, independently of approval/collectibility |
| `financePosition.needsReviewCount` | Sources whose linkage, snapshot or monetary evidence needs review |
| `financePosition.unpostedCount` | Sources without a bound/accepted Finance link |
| `financePosition.currencyTotals` | Per-currency verified totals only; empty when no verified group exists |
| Currency group `*AmountText` fields | Exact two-decimal strings for original, accepted, settled, pending verification, net waived, financial outstanding, collectible and net refunded amounts |
| Existing `charges` fields | Original assessment principal and recorded local Pending/Paid/Waived declarations; not payment evidence |

This aggregate read does not grant assessment detail, cashier, approval or refund actions.
Unknown historical currency does not create an INR group. Refund totals are processed payouts less recorded reversals and do not reopen the original debt.

| Financial alert type | Population |
|---|---|
| `pendingCharges` | Verified sources with a positive canonical collectible balance |
| `overdueCharges` | That collectible population with a confirmed school due date before the confirmed Library day |
| `unpostedCharges` | Unposted assessment review notices; no amount-due claim |
| `reviewCharges` | Source or settlement evidence review notices; no amount-due claim |

Financial overdue alerts are unavailable when only an HR timezone or no valid Library timezone is configured.
School date availability remains explicit in the overview metadata; absence of an overdue notice does not prove historical clearance.

## Member charge history

`GET api/library/members/{memberId}/charges?page=1&pageSize=10` requires Library Members View and current access to the requested person or their membership alias.
The endpoint includes all authorized recorded assessment years. It filters recorded source scope and assessment year access before returning counts or pages. Page size is limited to 100.

| Field | Meaning |
| --- | --- |
| `amount`, `amountText`, `recordedCurrency` | Stored assessment principal, invariant exact text, and recorded currency if known |
| `paymentStatus` | Original Library declaration: Pending=0, Paid=1, Waived=2 |
| `academicYearId`, `academicYearProvenance` | Recorded assessment year and evidence |
| `schoolChargeDate`, `schoolDueDate` and provenance | Recorded civil-date evidence. `date` retains the older timestamp field. |
| `bookId`, `copyId`, `copyLinkSource`, `requiresCopyReconciliation` | Linked loan and copy evidence, with any legacy accession match explicitly labelled |
| `financePosition` | Evidence, nullable approval status and currency, nullable amounts, and safe review messages |

Evidence values are Unposted=0, Verified=1 and NeedsReview=2. Verified positions expose the eight exact two-decimal amount fields used by the Dashboard summary. Unposted and review positions have null amounts. Approval and collectibility are separate from verification.

## Finance source contracts

Library assessments use Finance's existing `api/finance/external-receivables` family for posting, binding, review, collection, waivers and refunds.
The operator's discovery/context endpoints live under `api/finance/external-receivables/operator`; each requires its exact Finance action in addition to current Library source access.
They do not grant collection authority through a posting or review action.
The canonical refund list accepts optional `financeExternalSourceLinkId` and applies it before counts and paging.

New Post snapshots can retain typed reason provenance: schema, actual category ID, raw remarks and whether category fallback supplied the reason.
A later category-label rename does not replace that captured reason; changed raw remarks or category identity must still fail source verification.
Existing snapshots without that optional provenance keep strict current-facts comparison/review behavior and are never rewritten to infer lineage.
This is internal source evidence, not a client-supplied authorizing token.
See `src/ShikshaMitra.Application/Library/Charges/LibraryFinanceSourceReader.Reason.cs` and `src/ShikshaMitra.Application.Contracts/Finance/ExternalReceivables/IFinanceExternalReceivableSourceReader.cs`.

## Fine Collection report

Use `POST api/library/reports/preview` with report type `Fine Collection Report` and the owning Reports View action. Export creation also requires Reports Export.
The query applies recorded source scope and independent assessment year before filtering and counts. A date range selects confirmed school assessment dates. The status filter selects recorded Library declarations.

The default columns show the recorded assessment, school dates, Finance evidence and current verified settlement, pending verification, outstanding and collectible values. The eight canonical amount columns contain exact two-decimal text for verified rows. Unposted or review rows carry blank canonical amounts. Existing legacy columns remain selectable.

## AI report planning

`POST api/library/reports/ai/plan` accepts a natural-language request plus prior clarification answers.
It returns either `NeedsClarification` with one to three material questions or `Ready` with a typed `LibraryReportRequestDto`.
The planner receives only semantic report capabilities and opaque tokens for permitted options.
It never receives database schema or permission-bypassing query access.

| Stage | Required permission | Server enforcement |
|---|---|---|
| Plan | `Library.Reports.View` | Tenant, current user, UserScope, and permitted academic-year options |
| Preview | `Library.Reports.View` | Request guard, report-specific filters, academic scope, and scoped student members |
| Export | `Library.Reports.Export` | Fresh request validation plus the saved permission-scope snapshot |

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
