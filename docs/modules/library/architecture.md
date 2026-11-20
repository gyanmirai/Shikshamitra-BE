---
module: library
domain: architecture
content_type: Conceptual
status: canonical
updated_on: 2026-11-19
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
| `Receiving` | Stock collection receipts and accepted CSV batch outcomes |
| `InventoryVerification` | Stock count sessions, expected copies, and recorded observations |
| `Setting` | Sections, hours, categories, and borrowing rules |

## Catalogue configuration ownership

| Source | Responsibility |
|---|---|
| `Domain/Library/Catalogue/Aggregates/LibraryCatalogueConfiguration.cs` | Persisted definition and school revision; framework concurrency stamp stays framework-owned |
| `Domain/Library/Catalogue/Services/LibraryCatalogueRules*.cs` | Defaults, choice retention, requirements, old mappings and retired-value rules |
| `Domain/Library/Catalogue/ValueObjects/LibraryCatalogueMetadata.cs` | Descriptive input without financial/stock mutations |
| `Application/Library/Catalogue/LibraryCatalogueConfigurationAppService.cs` | Exact actions, current identity/scope, inventory lock and atomic save |
| `Application/Library/Catalogue/LibraryCatalogueConfigurationReader.cs` | Internal effective projection and command revision checks |
| `Application/Library/Catalogue/LibraryCatalogueLabelReader.cs` | One configuration read per operation; View-owned current labels |
| `EntityFrameworkCore/Library/Catalogue/LibraryCatalogueConfigurationMapping.cs` | Unique school configuration row |
| `EntityFrameworkCore/Migrations/20261118112116_ConfigureSchoolLibraryCatalogue.cs` | Additive configuration/codes, nullable old Language and protected rollback |
| `Domain/Library/Catalogue/Services/LibraryCataloguePriceRules.cs` | Unknown/known distinction, exact command parsing, historic unchanged-value retention and response formatting |
| `EntityFrameworkCore/Migrations/20261118165933_AllowUnknownLibraryCataloguePrice.cs` | Nullable existing decimal price; unchanged old values and rollback refusal while Unknown exists |
| `Application/Library/Books/LibraryCatalogueRevisionReader.cs` | Separate deterministic metadata/full-copy revisions, checked under the existing inventory fence |
| `Application.Contracts/Library/Books/LibraryBookMutationResponses.cs` | Revisions of the accepted create/update/copy command; no follow-up read token adoption |
| `Application/Library/Books/BookAppService.Retention.cs` | Same whole-title retention decision for detail eligibility and the fenced delete command |

Migration retains old language integers, descriptions, copy/loan identities and receiving evidence. It creates no default rows or permission grants.
Rollback locks affected tables and refuses to discard configuration, recorded codes or unknown language; it never converts unknown language to Gujarati.

## Stock import evidence

`Application/Library/Receiving/LibraryStockImportCsv.cs` parses the bounded UTF-8 source and stable row identities. `LibraryStockImportPlanner` validates the school catalogue configuration, assigned destination, existing title evidence, and available accessions and source identifiers. The plan revision covers the submitted data and consulted catalogue, location and stock state.

`LibraryStockImportAppService` acquires the school inventory lock in the ambient write transaction. It uses the existing Books creation and Receiving commands for the new titles and copies. The transaction saves the actual receipt provenance and the import batch and outcome rows. A failed write rolls back all of those effects together.

`20261119100135_PersistLibraryStockImports` adds tenant-bound batch and row records. Unique keys protect command replay, source row ID and copy identity. PostgreSQL verifies that the complete set of row outcomes matches the receiving provenance and makes committed evidence immutable. Raw CSV is supplied for preview and commit. The database retains the fingerprint and selected outcome fields.

## Stock verification evidence

`Domain/Library/InventoryVerification` owns the counting, review, finalization, and cancellation rules. Each session captures the selected physical scope, expected copy count, school date, and opening actor. Baseline records retain the copy's original location and state for the count. Observations retain the accession entered, the actual location when supplied, and the resolved copy when available.

`EntityFrameworkCore/Library/InventoryVerification` maps each record to the school. The composite tenant and session key keeps baseline and observation records within their school. The migration seals baseline and observation rows and retains closed sessions. Rollback requires empty stock verification tables.

The transition command migration links review and cancellation to the saved actor, time, and session in the existing command ledger. The state change and its command are committed together. Rollback to the previous command schema is allowed while only opening and scan evidence exists.

Finding decisions reference the existing replay ledger in the same school and count. Their row source identifies the saved baseline copy, the latest recognized scan outside the baseline, or an unknown scan. The migration preserves prior decision versions and makes changes and deletes unavailable through database writes.

The approval preview compares findings in bounded batches and binds the scan count, finding evidence, and current decisions to a revision. Approval commits the terminal session, its command, and the complete result together. The saved result references immutable decision versions. PostgreSQL verifies the approval actor and time, retained source rows, and complete findings, and refuses rollback after a result is published.

## Portal and pickup ownership

`Library/Portal/**` uses Students' persisted portal association reader for every selected child operation. Private loan and reservation queries use the canonical student or a persisted membership alias in the same school. The portal has its own permission keys and projects family-facing data. Renewal uses the shared circulation policy calculation and immutable loan command evidence behind portal-specific ownership.

Migration `20261119120836_PersistLibraryReservationPickup` records the optional section, campus and label on the original reservation. The queue matches explicit locations before allocating copies and leaves unrelated stock available to the other requests it can serve. The database enforces matching pickup stock and retains historical choices.

## Campus transfer evidence

`LibrarySection.CampusId` records an explicit optional association with the school's Admission Campus. Migration `20261119113627_BindLibrarySectionsToCampuses` preserves existing unassigned sections and enforces school ownership on the binding.

`Library/CampusTransfers/**` owns dispatch records, accession-level outcomes, and partial receipts. A dispatched copy keeps its source location and is In Transit until its receipt assigns the destination shelf. The inventory lock serializes transfer commands with circulation, holds, Section Management, and other copy actions. Migration `20261119114751_PersistLibraryCampusTransfers` enforces the registered route, receipt-backed cross-campus moves, retained copy events, and the relationship between outstanding items and in-transit status. Stock verification uses this evidence for published In Transit findings.

## Classroom custody evidence

`Library/Custody/**` owns the immutable handoff, its accession-level items, and receipts. Each handoff and each received copy links to its actual copy event and recorded actor. Returns fill each item's immutable receipt fields once. The inventory transaction fence serializes custody with circulation, holds, and inventory actions.

Migration `20261119111221_PersistLibraryClassroomCustody` couples the In Custody copy status to the active handoff item, validates matching copy events, and guards the three evidence tables. Stock verification uses the linked active item to classify a copy in custody. Published results keep the finding and its evidence after later returns.

## Student and staff exit evidence

`Application/Library/Clearance/LibraryStudentClearanceReader.cs` provides the outstanding loan count and stable issue IDs for a student already authorized by Student Records. It resolves the student's current record and legacy membership IDs within the same tenant. Both the exit summary and the approval preview use this query across academic years. The saved Student Records approval uses the same sorted loan IDs.

Student Records owns approval and enrollment changes. Library owns the actual return and the recorded loan context. Finance owns financial readiness and amounts.

`Application/Library/Clearance/LibraryStaffClearanceReader.cs` supplies physical loan evidence for an employee already authorized by HR. It requires Library Members View and applies the recorded loan scope. A school-wide reader can confirm the total. A scoped reader receives the permitted count and a Limited status. HR uses the result as advisory evidence for a departure.

## Finance evidence ownership

Library assessment principal, school dates and billing context remain distinct from Finance payment evidence.
Finance owns collection allocation, accepted/settled/pending amounts, waivers, refunds and receipts.
The existing single-charge calculator is `src/ShikshaMitra.Domain/Finance/ExternalReceivables/Services/FinanceExternalSettlementAmounts.cs`.
New immutable Post snapshots can capture reason provenance using `src/ShikshaMitra.Domain.Shared/Finance/ExternalReceivables/FinanceExternalSourceReasonOrigin.cs`.
`src/ShikshaMitra.Application/Library/Charges/LibraryFinanceSourceReader.Reason.cs` checks the bound link/snapshot and exact raw origin before retaining the accepted reason across category-label changes.
No-origin historical snapshots keep strict comparison; this helper does not rewrite evidence or require category-settings grants for normal Finance access.

`src/ShikshaMitra.EntityFrameworkCore/Migrations/20261118195300_AddFinanceExternalPositionEvidenceFunctions.Json.cs` installs pure PostgreSQL scalar/money evidence readers.
They validate the requested JSON path/type before conversion and return null for unsupported evidence; explicit JSON null remains distinguishable from a missing property.
The money reader preserves exact decimal values and rejects subcent input rather than rounding it.
This migration changes no rows, tables, permissions or balances.
The equivalent parser is `src/ShikshaMitra.Domain/Finance/ExternalReceivables/Services/FinanceExternalEvidenceJson.cs`; the SQLite tests register it on their own test connection and return decimal text, avoiding REAL precision loss.
These are query prerequisites, not proof that all Library consumers already use canonical balances.
Full source linkage, current actor/tenant/scope, independent billing-year authorization and canonical calculation remain mandatory at the consuming boundary.

### Internal position queries

The Dashboard financial summary, member charge history and Fine Collection report use these internal ports. Member List balances and reminders still need canonical Finance adoption.

| Source | Responsibility |
|---|---|
| `Application/Library/Charges/LibraryAssessmentSourceScopeQuery.cs` | Shared recorded-source scope predicate; each entry point retains its exact owning action |
| `Application/Library/Charges/Positions/LibraryChargePositionReader.cs` | Active actor, tenant, owning read purpose and independent year before composition; detail reads select at most 100 assessments |
| `EntityFrameworkCore/Library/Charges/EfCoreLibraryChargePositionSourceQuery.cs` | Tenant-fenced raw facts, original bound assessment version and unchanged raw remarks |
| `EntityFrameworkCore/Finance/ExternalReceivables/EfCoreFinanceExternalPositionQuery.cs` | Composable Finance evidence and grouped payment/waiver/refund positions before filtering, totals and paging |
| `Domain/Finance/ExternalReceivables/Services/FinanceExternalPositionReader.cs` | Bounded detail batches using Finance's canonical balance calculation |
| `Domain/Finance/ExternalReceivables/Services/FinanceExternalPositionProjection.cs` | Shared evidence classification and nullable verified amounts |

Member charge history in `Application/Library/Members/LibraryMemberAppService.ChargeReads.cs` intersects the requested person's IDs with the authorized source ID query before count and paging. It loads canonical Finance details in one bounded batch over the selected page and reads copy evidence through the shared stable-copy reader.

`Application/Library/Reports/LibraryReportGenerator.FineRows.cs` selects authorized assessments for the Fine Collection report. It reads Finance and linked display evidence in batches of at most 100 assessments. It resolves linked loans independently of the assessment-year filter. Recorded loan snapshots supply historical labels. Direct student assessments use the Library card of a currently authorized student when the card can be identified. Preview and both export formats use the same row projection. Presentation search and sorting run over the authorized filtered population.

Unposted and NeedsReview rows retain original assessment and local declarations without inventing settlement amounts.
Financial outstanding remains separate from collectible while accepted bank payments await verification; processed refunds remain separate from the original debt.
The present verification rules support INR and must not infer another currency from locale or historical null values.
Application tests exercise composition, bounded detail parity, source scope and Unicode reason normalization through SQLite test infrastructure.
Integrated PostgreSQL cases exercise actual cash/refund/bank workflows, SQL/DMY date formatting, bounded detail parity and a fixed query shape over one and eight source records.
The separate native scale case checks 500 then 2,500 valid sources, all eight monetary totals and bounded pages under the ordinary 30-second command timeout.
Its single-payer population does not establish every school distribution, concurrency level or corrupted historical allocation scenario.

The main PostgreSQL accepted-evidence lookup matches the tenant, source kind and source ID before extracting snapshot fields and calculating the checksum.
Its bounded inner projection carries the actual outer source reason: removing that correlation lets EF 9 rewrite `Take(1)` into a global window that can calculate evidence before matching the source.
SQLite retains the equivalent unique left join. Relevant-ID monetary subqueries remain composable and do not materialize school-wide IDs or duplicate the bounded evidence projection.
This query shape changes no stored snapshots, source versions, financial rules or amounts.

`Application/Library/Dashboard/LibraryDashboardAppService.Finance.cs` aggregates the financial summary and historical local declarations over that same authorized source population.
It materializes only status, evidence and currency groups; it does not enumerate assessment IDs or call Finance separately for each record.
The additive exact-text contract is `Application.Contracts/Library/Dashboard/Dtos/LibraryFinancePositionDtos.cs`.
Financial actionability and overdue counts share the verified currency-group query; review notices reuse the evidence counts.
Overdue financial alerts require the confirmed Library day and the source's confirmed civil due date, independently of raw timestamps or HR fallback.
Existing inventory/circulation materialization still needs its separate scale checks; the old utilization rate remains a local-declaration compatibility field.

### Stored checksum and refund lineage

`Domain/Finance/ExternalReceivables/Services/FinanceExternalSourceFingerprintV1.cs` owns the existing source-fingerprint byte protocol; the Application producer delegates without changing Post-command hashing.
Its fixed envelope preserves property order, invariant G29 amount text, civil dates, exact evidence strings, nullable payer IDs and reason trimming.
Version one retains the original .NET 9 Basic Latin escaping; display fields and ReasonOrigin remain outside that hash and retain their separate validation rules.
The frozen producer vectors must continue to pass when SDK or serializer behavior changes.

`EntityFrameworkCore/Migrations/20261118223401_AddFinanceExternalSourceFingerprintFunctions.Functions.cs` installs versioned pure PostgreSQL quote/hash functions without changing stored snapshots, fingerprints, rows or permissions.
Both list and bounded detail evidence require non-null calculated/stored hashes with exact equality before confirming amounts.
The SQLite test callback uses the same Domain protocol; it is test infrastructure.

`Domain/Finance/ExternalReceivables/Repositories/IFinanceExternalRefundEvidenceQuery.cs` supplies shared refund evidence to the composable query and bounded detail reader.
`EntityFrameworkCore/Finance/ExternalReceivables/EfCoreFinanceExternalRefundEvidenceQuery.cs` checks each line's source owner, claimed charge, payer/year/currency, original allocation/receipt/header and money ranges before grouping.
An inconsistent owner and distinct claimed charge both require review; unrelated valid sources remain readable.
Processed refunds remain separate from settlement and cannot reopen the original debt.

## Loan read context

`Application/Library/Circulation/LibraryLoanAppService.Get.cs` enriches the authorized exact-loan read with current school context and actionable date-review capability.
`Application/Library/Shared/LibrarySchoolContextReader.cs` shares the existing authorized-year and confirmed-Library-timezone projection with circulation lookups.
`Domain/Library/Books/Aggregates/BookIssue.DateChanges.cs` exposes the nonpersisted reviewable-field query while retaining the existing review command's guards.
The core loan mapper used by renewal preview and immutable command results stays unenriched; new optional DTO fields are ignored when null.
Return lookup retains its established outer/copy context and omits those new fields from its nested loan.

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

## AI report boundary

| Component | Responsibility |
|---|---|
| `LibraryAiReportPlanner` | Sends the semantic catalog and opaque permitted options to the configured AI provider and parses strict structured output |
| `LibraryReportRequestGuard` | Rejects unsupported report types, columns, sorts, statuses, filters, ranges, and sizes |
| `LibraryReportScopeService` | Resolves current UserScope, permitted academic years, and the execution-scope snapshot |
| `LibraryReportGenerator` | Executes only repository-backed, tenant-filtered, scope-filtered report builders |
| `LibraryReportExportBackgroundJob` | Replays the already validated typed request with its saved scope snapshot |

The AI provider cannot call repositories or supply SQL.
Opaque catalog tokens are resolved server-side and rejected when they are stale, unknown, or from another option kind.
