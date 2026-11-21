---
module: library
domain: testing
content_type: Troubleshooting
status: canonical
updated_on: 2026-11-20
owner: backend-team
audience: humans-and-agents
---

# Validate Library backend changes

TL;DR: run the docs validator for documentation changes and targeted Library tests for behavior changes.
Do not claim Library behavior is validated unless the relevant tests or a stronger suite ran.

## Documentation checks

Run this command after editing active Library docs.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module library
```

## Focused backend tests

Use these tests as the first backend validation set.

| Risk area | Command |
|---|---|
| Core workflows | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter LibraryCoreWorkflow_Tests` |
| Reports and export behavior | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter LibraryPhase3A_Tests` |
| AI request guard and filter allowlists | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter LibraryReportRequestGuard_Tests` |
| Notifications | `dotnet test test/ShikshaMitra.Application.Tests/ShikshaMitra.Application.Tests.csproj --filter LibraryPhase3BNotification_Tests` |
| HTTP release gate | `dotnet test test/ShikshaMitra.HttpApi.Tests/ShikshaMitra.HttpApi.Tests.csproj --filter LibraryReleaseGateApi_Tests` |
| Exact-loan school context, actionable date review and unchanged command JSON | `dotnet test test/ShikshaMitra.Application.Tests --filter LibraryLoanMaintenanceRead_Tests` |
| Existing circulation lookup/calendar behavior | `dotnet test test/ShikshaMitra.Application.Tests --filter LibraryCanonicalLookup` |
| Exact-loan HTTP context, action denials and unchanged checkout payload | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Loan_Maintenance_Read_Http_Should_` |
| Reservation queue, lifecycle, preview, member lookup and recorded scope | `dotnet test test/ShikshaMitra.Application.Tests --filter LibraryReservation` |
| Stock import parsing, school configuration, outcomes and retry | `dotnet test test/ShikshaMitra.Application.Tests --filter LibraryStockImport_Tests` |
| Rollover calendar, policy state, original loan and authority | `dotnet test test/ShikshaMitra.Application.Tests --filter 'LibraryRolloverPreview_Tests|Carry_Forward_Return'` |

With the isolated PostgreSQL fixture configured, run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Migrated_Postgres_Stock_Import_Should_Roll_Back` for an actual HTTP import. It checks denied actions, full rollback after a late write failure, concurrent accepted retry, linked receipt evidence and migration rollback protection.

## Catalogue configuration checks

| Risk | Command |
|---|---|
| Defaults/codes/revisions | `dotnet test test/ShikshaMitra.Domain.Tests --filter LibraryCatalogueRules_Tests` |
| School rules and View-owned labels | `dotnet test test/ShikshaMitra.Application.Tests --filter LibraryCatalogueConfiguration_Tests` |
| Current catalogue context | `dotnet test test/ShikshaMitra.Application.Tests --filter LibraryCatalogueReadAuthority_Tests` |
| Owning options | `dotnet test test/ShikshaMitra.Application.Tests --filter LibraryCatalogEditorOptions_Tests` |
| Actual custom-metadata HTTP flow | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Configured_Catalogue_Http_` |
| Exact configuration actions | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Catalogue_Configuration_Http_` |
| PostgreSQL migration/storage/concurrency | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Catalogue_Postgres_` |
| Exact catalogue price rules | `dotnet test test/ShikshaMitra.Domain.Tests --filter LibraryCataloguePrice_Tests` |
| Unknown/exact catalogue HTTP commands | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Catalogue_Price_Http_` |
| Native price migration/rollback/storage | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Catalogue_Price_Postgres_` |
| Unknown versus zero in reports | `dotnet test test/ShikshaMitra.Application.Tests --filter Catalogue_Price_Report_` |
| Metadata/copy revision boundaries | `dotnet test test/ShikshaMitra.Application.Tests --filter Catalogue_Revision_Should_` |
| Actual two-operator PostgreSQL contention | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Catalogue_Postgres_Contending_Editors` |
| Created-command revisions after decimal storage | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Catalogue_Postgres_Created_Command_Revisions` |
| Whole-title deletion decisions | `dotnet test test/ShikshaMitra.Application.Tests --filter Catalogue_Deletion_Eligibility_Should_` |
| Delete-only authority and late receiving | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Catalogue_Title_Deletion_Http_Should_` |

PostgreSQL tests require `LIBRARY_TEST_POSTGRES` (an owned loopback administrator connection to postgres) and `LIBRARY_TEST_POSTGRES_DATA_DIRECTORY` (the actual owned server data directory).
They verify ownership before creating, cloning or dropping uniquely named disposable databases. Missing configuration causes skips; skipped tests are not migration proof.
Cases cover all five old meanings, raw descriptions, copy/loan/receiving retention, empty rollback, destructive rollback refusal, custom metadata and concurrent settings saves.
Frontend type/lint, cache/component checks and final in-app browser evidence are separate; see the [frontend Library testing guide](https://github.com/gyanmirai/Shikshamitra-FE/blob/main/docs/library/testing.md).

## Settings authority checks

`LibraryHoursCapabilities_Tests` checks the additive Hours capability, own actions and current scope; `LibraryPolicyScopeSafety_Tests` retains the existing class/global settings boundaries.
`LibraryHoursCapabilitiesApi_Tests` verifies actual HTTP payload serialization and action/scope refusal: school-wide writer, View-only, scoped reader and Add without View.
Application tests use a controlled permission checker and persisted scope; API tests use the test authentication/header checker and persisted users/scopes.
Neither replaces deployed OIDC/browser validation.

## Finance integration checks

| Risk | Command |
|---|---|
| Source-scoped refund counts/pages | `dotnet test test/ShikshaMitra.Application.Tests --filter Typed_Refund_Source_Filter_Should_` |
| Exact owning waiver context | `dotnet test test/ShikshaMitra.Application.Tests --filter Operator_Waiver_Context_Should_` |
| Waiver context HTTP bindings | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Waiver_Context_Http_Should_` |
| New payout instant, replay and retained money evidence | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Migrated_Postgres_Employee_Finance_Should_Post_Collect_Refund_Pay_Return_And_Preserve_Immutable_Evidence` |
| Strict JSON and whole-paise primitives | `dotnet test test/ShikshaMitra.Domain.Tests --filter FinanceExternalEvidenceJson_Tests` |
| PostgreSQL migration/query and SQLite query bridge | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FinanceExternalPositionEvidence` |
| Posted reason continuity after category rename | `dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Posted_Source_Category_Rename_Should_|FullyQualifiedName~Posted_Source_With_Explicit_Remarks_Should_"` |
| Raw-origin changes and old no-origin compatibility | `dotnet test test/ShikshaMitra.Application.Tests --filter Reason_Origin_Should_` |
| Canonical consumer amounts, evidence, own action and SQL composition | `dotnet test test/ShikshaMitra.Application.Tests --filter Consumer_Position_Should` |
| Bound source version and Unicode reason projection | `dotnet test test/ShikshaMitra.Application.Tests --filter Consumer_Source_Projection_Should_` |
| Existing settlement workflows with the shared balance kernel | `dotnet test test/ShikshaMitra.Application.Tests --filter LibraryFinanceExternalReceivable_Tests` |
| Native reserved bank position and review before aggregates | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Migrated_Postgres_Canonical_Consumer_Should_` |
| Native 500-to-2,500 source totals, fixed query shape and bounded pages | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Migrated_Postgres_Canonical_Consumer_Should_Aggregate_500_Then_2500_Valid_Sources_Within_Default_Timeout` |
| Existing producer byte protocol and unsupported snapshots | `dotnet test test/ShikshaMitra.Domain.Tests --filter FinanceExternalSourceFingerprintV1_Tests` |
| Native checksum functions, string encoding and rollback | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FinanceExternalSourceFingerprintPostgres_Tests` |
| Stored checksum and unknown approval status | `dotnet test test/ShikshaMitra.Application.Tests --filter Consumer_Evidence_Should_Review_` |
| Redirected refund owner/claim before count and page | `dotnet test test/ShikshaMitra.Application.Tests --filter Canonical_Consumer_Position_Should_Review_A_Redirected_Refund_Line` |
| Dashboard balances, retained declarations and recorded source scope | `dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Dashboard_Finance_Position_Should_|FullyQualifiedName~Dashboard_Finance_And_Compatibility_Amounts_Should_Use_Recorded_Source_Scope"` |
| Dashboard HTTP summary, exact action and missing scope | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Dashboard_Finance_Http_Should_` |
| Actual partial settlement in the public PostgreSQL Dashboard | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Migrated_Postgres_Dashboard_Http_Should_Show_Actual_Partial_Settlement_With_Only_Dashboard_Action` |
| Collectible/review alerts and confirmed school-day boundaries | `dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Dashboard_Collection_Alert_Should_|FullyQualifiedName~Dashboard_Overdue_Charge_Alert_Should_Use_The_Confirmed_Library_Day|FullyQualifiedName~Dashboard_Should_Keep_Charge_Overdue_Classification_Unavailable"` |
| Member charge balances, scope, copy identity and bounded pages | `dotnet test test/ShikshaMitra.Application.Tests --filter FullyQualifiedName~ShikshaMitra.Library.LibraryFinanceExternalReceivable_Tests.Member_Charge_` |
| Native member charge API, actual partial payment and owning permission | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Migrated_Postgres_Member_Charges_Should_Page_Actual_Settlement` |
| Fine Collection balances, recorded scope and export evidence | `dotnet test test/ShikshaMitra.Application.Tests --filter FullyQualifiedName~LibraryFinanceExternalReceivable_Tests.Fine_Collection_` |
| Native Fine Collection API and confirmed assessment-date range | `dotnet test test/ShikshaMitra.HttpApi.Tests --filter Migrated_Postgres_Fine_Collection_Should_Read_Confirmed_School_Dates` |

The native payout workflow covers explicit +05:30 input stored as the same UTC instant, UTC-equivalent replay, actual source-filtered refund GETs and the original collection/refund/payout reversal history.
The evidence-provider tests prove guarded SQL count/sum/page behavior with invalid siblings, exact decimal values, missing versus explicit null, Unicode, cancellation, and pure-function Up/Down/Up preservation.
Primitive parser tests alone do not establish source checksum integrity or completed adoption by dashboard, reminder and member balance consumers.
The consumer application cases execute the new composable query and bounded detail adapter through SQLite, including partial settlement, pending bank amounts, review classification before counts and recorded source scope under the owning read action.
The native payout and reserved-bank cases also execute the integrated consumer query and detail adapter, with SQL/DMY date formatting, actual canonical balance comparisons, one-versus-eight source records and soft-deleted receipt review before aggregates/pages.
The checksum protocol cases compare frozen outputs from the unchanged .NET 9 producer, both payer types, exact amounts and Unicode/ASCII escaping.
Native cases cover unsupported full-snapshot siblings before count/page and pure-function Up/Down/Up row preservation; application cases compare checksum mismatch, unknown status and redirected refund claims with valid-source controls.
The integrated money and reserved-bank workflows target the source-fingerprint migration so the actual query executes its current checksum and refund rules.
Their captured SQL and EXPLAIN ANALYZE measurements prove the bounded money cases; they do not replace the larger population check or additional historical allocation cases.
The separate 500-to-2,500 case retains the ordinary 30-second timeout without a manual statistics refresh, verifies eight monetary totals, and checks first, middle and empty 25-row pages.
Representative actual source GETs and Post replays validate its persisted evidence; command count and SQL length must remain independent of population size.
It uses one actual employee payer and complements the money-transition cases; varied payer/scope distributions and broader school loads require their own measurements.
Native cases require the owned PostgreSQL configuration described above; SQLite is test infrastructure only.

Dashboard application cases use real permission grants and a non-admin actor, denying unrelated Finance, Charges, Members and Settings actions before reading the overview.
Both old declaration fields and new financial groups must follow the original source scope after a staff placement change.
HTTP cases exercise the existing header-based permission harness separately from those database-grant checks; they do not prove deployed OIDC authorization.
`test/Shared/FinanceExternalPositionSqliteTestFunctions.cs` registers identical Domain callbacks on each owned application/API SQLite test connection; PostgreSQL continues to use its migrated pure functions.
Alert cases retain real full-cash and bank-reservation evidence, genuine unposted/old-source review states, unchanged source snapshots and the UTC/Honolulu boundary.
The missing/HR-only clock case is a negative control: collectible money remains actionable while financial overdue classification stays unavailable.
The large native case also reads the public Dashboard under its own action, with an explicitly configured Library UTC day, eight exact totals and financial alert counts.
Its HTTP SQL capture checks three grouped financial commands, their actual 30-second timeouts and a query shape independent of the 500/2,500 population.

Member charge cases exercise stored assessment years, moved student and employee scope, canonical and alias IDs, old local declarations, stable copy identity and the application's real current grant.
They compare canonical detail reads for one and twenty returned rows, and a native API workflow checks settled cash, page metadata, permission denials and accepted snapshot preservation.

Fine Collection cases cover actual cash, waiver and bank evidence, unposted and review states, scope after a member move, separate assessment years, and current export access. The worker case compares the actual XLSX cells to preview and generates a downloadable PDF. Rendered PDF text and large-population behavior have separate verification needs.

## Member List Finance checks

Run the Member List report tests for Reports-only access, exact Finance amounts, recorded year selection, legacy column selection, and unposted evidence.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter 'FullyQualifiedName~Member_List_Should_'
```

## Fine Due reminder checks

Run the focused Application tests for posted, partially settled and unposted assessments, date eligibility, invalidation of a queued reminder after settlement, and ongoing inbox recovery for inactive members.

```powershell
dotnet test test/ShikshaMitra.Application.Tests --filter 'FullyQualifiedName~Fine_Due_Reminders_Should_'
```

Use the existing Notification and Phase 3B Application suites to verify recipient routing, consent, manual delivery, and retry behavior after notification changes.

## Stock verification evidence checks

Run `LibraryStockVerification` domain tests for count lifecycle, scope checks, and observation evidence. Run the `Stock_Verification_Postgres_Migration_Should_` HTTP test filter with the isolated PostgreSQL fixture to check additive migration, tenant and sequence constraints, append-only evidence, and rollback protection.

Run `LibraryStockVerificationCommands_Tests` for current scope and action checks, baseline capture, stale preview, and exact retries. Run `Migrated_Postgres_Stock_Verification_Should_Open` with the same PostgreSQL fixture for simultaneous opening requests, scan replay, HTTP action checks, and command evidence retention.

The same filters cover reconciliation classification, repeat scan grouping, current academic scope, and the three reconciliation sources over the migrated HTTP API.

Run `Migrated_Postgres_Stock_Verification_Should_Seal` for the review and cancellation migration, stale scan count and exact permission enforcement, concurrent replay, and retained transition evidence.

Run `Migrated_Postgres_Stock_Verification_Should_Persist_Decisions` for the finding decision migration, versioned history, exact concurrent replay, and the append-only database guard. The application suite also checks evidence changes and competing decision revisions.

Run `Migrated_Postgres_Stock_Verification_Should_Publish` for exact approval authority, concurrent retry, published findings, and migration protection. The application suite includes stale and missing decisions, an empty approved count, stable scope, and published data after a copy changes.

The publication and application suites check a donation recorded after opening. The test uses an earlier civil receipt date and verifies the label from the actual recording timestamp.

## Pickup and portal checks

Run `LibraryReservationFifo_Tests` and `Migrated_Postgres_Pickup_Should` for pickup-constrained allocation and the open queue at other locations. Run `LibraryCanonicalRenewal` for saved terms, exact replay, guardian ownership and revocation. Run `My_Library_Should_Use_Exact` on the isolated PostgreSQL fixture for action grants, linked-student isolation and manual-date mode.

Run `My_Library_Should_List_The_Exact_Finance_Receipt` to trace a real Finance collection to the linked student's receipt list. The PostgreSQL portal test checks that a Library-only account cannot request receipt years. The Finance mobile permission contract and PDF tests cover the official Finance download.

## Campus transfer checks

Run `LibrarySectionCampus_Tests` for campus binding, ordinary move options, partial receipt, and stock verification. Run `Migrated_Postgres_Campus_Transfer_Should` with the isolated PostgreSQL fixture to check upgrade and empty rollback, separate action grants, exact concurrent replay, protected locations, published In Transit findings, and retained receipts.

## Classroom custody checks

Run `Classroom_Custody_Should` in Application tests for partial receipt, exact retries, custody finding evidence, checkout eligibility, and blocked generic condition changes. Run `Migrated_Postgres_Classroom_Custody_Should` with the isolated PostgreSQL fixture for upgrade and empty downgrade, exact HTTP actions, concurrent handoff retry, approved stock findings, immutable records, and populated downgrade protection.

## Source checks

Use these checks when docs or routes change.

```powershell
rg -n "api/library|LibraryPermission|Library.*Controller" src test docs/modules/library
git diff --check
```

## Add coverage when needed

Add or update tests when a change affects these areas.

| Change | Expected coverage |
|---|---|
| Copy availability or issue status | Application workflow test |
| Public route shape | HTTP API test |
| Report export | Report service or application test |
| Notification timing or recipients | Notification application test |
| Permission behavior | HTTP API authorization test |
