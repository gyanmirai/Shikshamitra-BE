---
module: finance
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-27
owner: backend-team
audience: humans-and-agents
---

# Use Finance backend APIs

## Fee structure copy policy preflight

`GET /api/fee-structures/{id}/copy-policies?feeStructureMetadataId=...&standardId=...`
requires Fee Structure View and checks source/target tenant and academic scope. It returns
linked policy family/name, source snapshot ID/version, current active snapshot ID/version
and an actionable error when unavailable. Current means the active master's version,
not the highest historical snapshot number. It performs no writes. Legacy concession
attachments can be inspected; existing retirement of structure-level concessions is preserved.
Draft creation retains its existing permissions, idempotency and authoritative validation.
Stale attachments identify policy name and selected/current versions before any insertion.

## Demand Draft collection dates

Collection DD details accept `issueDate` and `paymentDate` from the collection form;
legacy `ddDate` remains an issue-date alias. Both are preserved independently as calendar
dates in the existing instrument JSON. Missing issue dates remain null instead of defaulting
to today. Reconciliation adds nullable `paymentDate` from the linked payment record, allowing
legacy instruments without an entered payment date to show their recorded collection date.
Receipt issuance date is a separate fact. No schema migration or historical rewrite occurs;
previously discarded entered dates or substituted issue dates cannot be reconstructed.

## Paid charges in refund requests

`GET /api/fee-refunds/eligibility` includes settled charge payments when the assignment's pinned
refund policy has `includePaidCharges=true`. The separate **Paid charges** group has a null
`feeHeadId`; its rows carry `chargeId`, `chargeName`, `sourceAllocationId` (the charge payment),
and `sourceReceiptId`. A create/preview line supplies exactly one of `dueFeeId`, `chargeId`, or `lateFeeDueFeeId`.
The server checks the policy, payer, academic year, receipt and available payment balance.
Reversed collections and unverified provisional receipts are excluded. Reservations and net
processed refunds reduce eligibility; returned payouts restore it. Charges participate in the
same policy amount limits, whole-receipt selection rule, approval, payout and refund subledger.
Fee-head scope applies to fee dues; the paid-charge toggle independently controls charge sources.

Apply `EnablePaidChargeRefunds` before deploying this contract. It adds nullable charge references
to refund lines/reservations and makes the alternative fee-due references nullable, retaining all
existing refund history. Its downgrade rejects removal after charge-refund evidence exists.
Focused regression cases are `FeeRefundApi_Tests.Paid_Charges_*`.

The independent pinned `includePaidLateFee` setting enables a **Paid late fees** group with
null `feeHeadId`. Its rows carry `lateFeeDueFeeId` (the source due, without refunding its
principal), `chargeName` (the late-fee display label), the late-fee `sourceAllocationId`, and
`sourceReceiptId`. Only settled `LateFeeCollectionAllocation` payments for the payer, year,
and assignment qualify. The same reservation, payout, return, whole-receipt selection and
policy caps apply; principal paid/refunded balances remain separate. Apply
`EnablePaidLateFeeRefunds` before deployment. It adds one nullable refund-line reference,
preserves history, and rejects a downgrade after late-fee refund evidence exists.
Focused regression cases are `FeeRefundApi_Tests.Paid_Late_Fees_*`.

Whole-receipt requests retain every selected source when a cap or deduction rounds an
individual calculated share to zero. Such a line preserves selection evidence without a
reservation or principal balance mutation; the request still requires a positive total payout.

## Collection grace period

Collection Policy grace periods and late-fee day counts always use calendar days, including weekends and holidays.
`gracePeriodDays` remains an integer from 0 to 365; `gracePeriodType` is removed from create/update/read/clone contracts and new version snapshots.
Legacy snapshots containing either the string or numeric day type remain readable: the obsolete property is ignored, retaining the existing calendar-day calculation and all stored historical JSON.
The `RemoveCollectionPolicyGracePeriodType` migration removes only the unused column from `Finance_CollectionPolicies`; it does not rewrite policy versions, dues, collections or receipts.
Rollback restores the obsolete column with its Calendar value (0), since working-day counting was never implemented.

## Automatic collection allocation

Collection Policy DTOs accept `allocationBasis`: `Installment` or `FeeHead`; `Legacy` preserves
deployed behavior. Omitted values preserve the saved basis on update and use Legacy on older
create clients. The UI defaults new policies to Installment. Invalid values fail validation.
The enum is persisted in the aggregate's existing `ExtraProperties` JSON, copied by clone and
included in immutable `CollectionPolicySnapshot` versions. Existing rows/snapshots need no
schema migration and resolve to Legacy. No fee-head priority/rank records are introduced.

Student and Admission collect-form responses add `allocationBasis`, `collectionPolicyId`,
`collectionPolicyName`, `collectionPolicyVersion`, `allocationDueFeeOrder` and `allocationRevision`.
Configured modes use the same ordered ledger in the screen and command validation. Installment
groups use schedule installment IDs (date/label fallback for legacy dues), not month labels alone.
Fee-head groups retain the collection head-name order. Both retain the displayed line sequence.

Configured collection commands require `automaticAllocation: { revision, dueFeeIds, chargeIds }`.
Selection includes unfunded rows. The server reloads the scope-checked, assignment/account-pinned
collect form inside the existing serializable transaction, checks its revision and recalculates
allocations in decimal currency. Principal retains the existing whole-rupee ceiling; no additional
rounding mode is added. Linked late fee follows its principal, and selected charges follow fees.
The sum of accepted allocations equals tendered money; partial and exact payments work, excess
is rejected. Unknown/duplicate selections, stale revisions and altered amounts fail before writes.
`ShikshaMitra:FeeCollection:AllocationChanged` instructs the caller to refresh and review again.
Retry replay is checked before current-balance validation, preserving the original result.

The collection metadata's `AutomaticAllocation` extension stores the applied policy/version,
basis, revision and selected sequence with the existing immutable allocation and receipt evidence.
Policy changes do not rewrite existing assignment snapshots or posted receipts. Basis resolution
uses `CollectionPolicyResolver`: pinned assignment/account version, with existing direct-attachment
and academic-year-default resolution where applicable. Student late-fee overrides do not alter basis.
Permissions remain the existing Collection Policy View/Add and Fee Collection View/Add plus payer
scope; no override grant is added. Legacy calls retain their existing request/fingerprint behavior.

TL;DR: use this page to find Finance endpoint families and route ownership.
Controller source paths are canonical when a route detail changes.

Fee Structure configuration, publication and revision contracts: [workspace implementation](./references/fee-structure-workspace.md).

Contextual Admissions/Onboarding collections, nullable applicant payers and Finance receipt/refund continuity: [admission fee collection](./references/admission-fee-collection.md).

## Shared request rules

Admission collection summary/prepare responses include nullable, output-only `proration` (`FeeProrationResultDto`): policy/version IDs, joining/service dates, scheduled fees, admission reduction, withdrawal credit, payable fees, excess paid and explanations. Collection requests accept neither policy choices nor computed totals. `FeeLedgerLineDto` separately exposes withdrawal credit/explanation without replacing original or paid totals.

Records withdrawal preview rows use the same Finance result. There is no new public recalculation endpoint. An actual monetary adjustment requires existing `Finance.Policies.ProrationPolicy.Recalculate` and the approved transition's serializable transaction; Collection View/Add and Records permissions stay unchanged. Stable `Finance:Proration:*` errors identify missing/invalid joining date, ambiguous default, unavailable version, invalid evidence, pending owner workflows, denied recalculation and adjustment conflicts.

Finance APIs require authenticated access and tenant context through the platform pipeline.
Mutations must enforce backend permissions and audit important financial actions.
Year-only setup checks permit users with database-backed tenant-wide scope to configure an existing current-tenant academic year before its first Student exists. Unknown or foreign-tenant years remain denied. Board, medium, class, division and individual Student checks continue to use the scoped Student query.

## Charge workflow contract

`GET api/charges` filters the batch lifecycle with `status=0` (Pending), `1` (In Process), `2` (Completed), or `3` (Rejected). Responses expose `workflowStatus` separately from the existing student approval `status`. New charges always await an independent checker; completion is derived from settlement evidence and reversals reopen outstanding batches. See [the charge lifecycle contract](./references/charge-management-end-to-end.md#four-state-batch-lifecycle) for totals, mixed decisions, waivers and compatibility.

## Fees API families

| Family                 | Controller path                                                                  | Main routes or base route                                                                        |
| ---------------------- | -------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Charges                | `Controllers/Finance/Fees/Charges/ChargeController.cs`                           | `api/charges`                                                                                    |
| Concession requests    | `Controllers/Finance/Fees/Concessions/ConcessionRequestController.cs`            | `api/concession-request`, `api/concession-requests`                                              |
| Fee adjustments        | `Controllers/Finance/Fees/Adjustments/FeeAdjustmentController.cs`                | `api/fee-adjustments`                                                                            |
| Fee collection         | `Controllers/Finance/Fees/Collections/FeeCollectionController.cs`                | Fee collection routes                                                                            |
| Fee receipts           | `Controllers/Finance/Fees/Receipts/FeeReceiptController.cs`                      | Fee receipt routes                                                                               |
| Fee refunds            | `Controllers/Finance/Fees/Refunds/FeeRefundController.cs`                        | Fee refund routes                                                                                |
| Student fee assignment | `Controllers/Finance/Fees/StudentFeeAssignment/StudentFeeStructureController.cs` | Student fee structure routes                                                                     |
| Fee settings           | `Controllers/Finance/Fees/Settings/**`                                           | Fee category, fee head, fee structure, receipt book, concession type, and charge category routes |

`GET /api/fee-categories` accepts academic year, active status, search, page, and page size. Its optional `filters` query value is a JSON array of `{column,type,value}` entries: `name` and `description` support contains, equals, startsWith, and endsWith; `totalFeeHeads` supports equals, greaterThan, lessThan, and an inclusive `{min,max}` between range. `sortBy` accepts `name`, `description`, or `totalFeeHeads`, with `sortDirection` `asc` or `desc`. Filtering and sorting run before pagination; ties use category ID.

`GET api/v1/fee-receipts` requires `academicYearId`, `boardId`, and
`educationMediumId`. Its optional `month` is a calendar month from 1 (January)
through 12 (December), applied directly to `ReceiptDate.Month`. Omit `month` to
list every month in the selected academic year. The list retains tenant and user
scope filtering, optional class/division and receipt-kind filters, and server pagination.

Fee-category and fee-head create/update contracts accept `module` as an optional numeric `FeeHeadModule` enum value. Send `null` or omit the property when no module applies. The fee-head module lookup returns numeric `value` fields with user-facing `displayName` labels. Every standalone or nested Fee Head request must also send `incomeCategoryId`. Responses return the Income Category. Historical Income Account Head fields are compatibility evidence. The backend accepts active, effective Income Categories from the Fee Head's academic year without requiring an Account Head.

Fee Head list/detail responses and nested Fee Category heads expose `dependencies` with distinct `studentCount`, `assignmentCount`, `feeStructureCount`, `paymentTermCount`, and `isBlocked`. Deactivation and category head removal repeat these checks on the server and return `Finance:FeeHead:010002` and `:010001` respectively, with the head name and numeric counts in error data. Legacy student obligations remain dependencies even when they have no assignment ID. `GET api/student-fee-structure/students` accepts optional `feeHeadId` to narrow the existing tenant/year/user-scoped student list to affected obligations. Assignment details resolve current master fee-head labels while preserving the original stored pricing, policy, service-period, and label evidence.

Charge Category creation defaults omitted `isApprovalRequired` to true and accepts explicit false; omitted update values preserve the stored flag. Charge Category search matches name, description and linked Income Category name before pagination. Interactive charge creation with explicit no-approval configuration applies collectible lines through the existing approval side effects and pins the approval rule on the request. Approval-required categories retain the existing checker flow and documented administrator creation exception. Existing pending requests and source-module commands keep their current workflow.

Fee Head status changes reject an archived parent category with `Finance:FeeHead:010007` (restore the category before changing status). This rejects both activation and deactivation; active-head lookups also exclude archived categories.

Charge endpoints and exact permissions are documented in [Charge management end to end](./references/charge-management-end-to-end.md). The current base route is `api/charges`, with Add, View, Update, Approve, Reject, and Delete action permissions. The reference also records current contract defects: academic-year filtering is not consistently applied, row-level caller scope is incomplete, and the API has no charge-specific correction/refund or command concurrency contract. Do not treat those gaps as supported behavior.

Payment-term requests and responses serialize lifecycle, version, holiday-shifting, demand-generation, and initial-demand enums by name. For example, `demandGenerationRule` uses `OnInstallmentStartDate`, `XDaysBeforeDue`, `OnDueDate`, or `SpecificDate`; numeric enum values are not the canonical wire representation.

Payment-term installment coverage, demand, due, and reminder fields are calendar dates. Create and draft-update requests may send ISO 8601 timestamps with offsets; the API uses the date written in each field, independent of the server time zone. The installment dates must stay within the selected Education Board's start and end dates.

`isDefault: true` is valid only when the same create/update request uses `status: Active`. At most one Active Payment Term may be default per tenant, academic year, and Education Board. Selecting another default atomically removes the previous flag for that board; other boards retain their own defaults. Draft and Archived terms cannot retain the flag. List requests accept `boardId` to filter the workspace to one board.

`GET api/finance/payment-terms/{paymentTermId}/usage` returns distinct Fee Head, Fee Structure and Class counts plus assignment rows (class, structure, head and saved term version). Omit `versionId` for all versions, or supply a version belonging to that term. Usage rows respect tenant and caller academic scope. `GET api/finance/payment-terms/default-impact` accepts `academicYearId`, `boardId` and optional `paymentTermId` and identifies the existing board default, its published version, usage and whether it will be replaced. Default replacement records the old/new term identities and versions in Finance audit with the actor/time; it never updates Fee Structure term/version assignments.

Payment-term version history is exposed separately from the full detail snapshot through `GET api/finance/payment-terms/{paymentTermId}/versions`. It accepts the standard `skipCount` and `maxResultCount` paging parameters and returns version number, lifecycle status, change action/reason, creation time, creator id, and hydrated creator name for the history drawer.

`POST api/finance/payment-terms/templates/preview` is an Add-permission authoring preview. It accepts a board, built-in template (`Annual`, `HalfYearly`, `Quarterly`, `Monthly`, or `Custom`), due-date rule, custom count when applicable, and the selected invoice rule (`demandGenerationRule`) with `demandOffsetDays` for `XDaysBeforeDue`.
The optional additive `specificDemandDates` array contains user-selected invoice calendar dates in generated installment sequence order and is required with exactly one date per installment for `SpecificDate`; no date is silently substituted.
An omitted invoice rule retains the existing `OnInstallmentStartDate` default. The save payload and existing version snapshots are unchanged.
The server partitions the authoritative board window, allocates exactly 100% with the rounding remainder on the final installment, returns each invoice date as `demandDate` alongside start/end/due dates, and runs the normal Payment Term validator.
Template offsets are capped at 365 days and the smallest generated start-to-due gap; rejected offsets include that global maximum, including zero for same-day due dates.
The preview does not persist data; the returned installment DTOs may be reviewed, edited, and submitted through the ordinary create/update contract.

Create, update, publish, template preview and resolved fee-structure customization reject invoice dates before their installment start or after their payment due date. Coverage and schedule events must remain within the selected board's academic period. `XDaysBeforeDue` requires an integer of at least one day and at most the smallest start-to-due gap across installments. Errors `Finance:PaymentTerm:000040` (invoice before start) and `:000041` (offset exceeds maximum) identify the violated rule; save-time errors identify the affected installment, and offset errors include `Maximum` and `Actual`. Dates inside the valid range may precede today for backfilled terms. Due dates after coverage end remain supported within the board period. Existing published snapshots are not rewritten.

Concession Type settings use `GET/POST api/{academicYearId}/concession-types`, `GET/PUT/DELETE api/concession-types/{id}`, `PATCH api/concession-types/{id}/status`, `GET api/concession-types/{id}/versions`, and `GET api/concession-types/document-defaults`. The paged versions endpoint returns the audited action, actor, timestamp, version number, and changed business fields. Administrative lists pass `includeInactive=true`; request creation omits it so only active types are returned. Document contracts use `documentName`, `maxFileSizeBytes`, `acceptedFormats`, and `isRequired`. Active records and used records cannot be deleted; deletion is allowed only after deactivation and while unused. Duplicate normalized names, edits to used document definitions, and missing deactivation reasons return stable business errors. `POST api/concession-request/types/{concessionTypeId}/documents/{documentId}/file` accepts multipart `File` and validates the selected definition's size, extension, MIME type, and file signature before storage. It returns an opaque staged route; `GET api/concession-request/staged-documents/{uploadId}/file` works only for the uploader while the receipt remains unattached and unexpired. Attached evidence is downloaded through `GET api/concession-request/{id}/documents/{documentId}/file`, which requires `ViewDocument` and repeats student-scope validation. Concession responses never expose the underlying shared-storage identifier.

Concession requests select their policy at request time. `GET api/concession-request/context` returns active academic-year policy/version options, eligible heads, outstanding due rows, types, and evidence requirements. When no heads are eligible, `blockers` explains whether the assignment has no outstanding dues, its heads are not concession-enabled/active for the year, its fee-structure scope excludes the enabled heads, or the selected policy has no matching installments. Blockers are explanatory and never relax server-side eligibility. If more than one current assignment matches, it returns `assignmentSelectionRequired=true` plus scoped assignment choices; the caller repeats the query with one `studentFeeStructureId`. It never selects an ambiguous assignment. Preview, draft, create, and update inputs carry `concessionPolicyId` plus `concessionPolicyVersionId`; the backend verifies that they identify the current active version and pins both values on the request. `POST api/concession-request/preview` returns server-calculated values and a 64-character fingerprint. An incomplete draft may omit fee selections and required evidence, but Submit always reruns strict policy, value, due-row, and evidence validation.

Drafts use `POST api/concession-request/drafts`, `PUT api/concession-request/{id}`, and `POST api/concession-request/{id}/submit`; the compatibility `POST api/concession-request` performs create-and-submit and requires both Add and Submit. `POST api/concession-requests/{id}/approval-preview` is the authoritative pre-commit approval calculation, and `POST api/concession-requests/{id}/reversal-preview` returns the balance impact before reversal. Approve repeats approval validation using the preview fingerprint and request/due stamps. Reverse repeats reversal validation using request and allocation due-row stamps. Detail and list responses expose state-derived `availableActions`; detail and student applications also expose the stored lifecycle `timeline` so clients do not infer legal transitions from labels.

An `AllFees` concession policy permits paid installments. Approval preview reports the paid refund amount and `autoCreateRefund` mode. The pinned policy controls automatic creation on approval or manual creation through `POST /api/concession-request/{id}/refund-requests` (Refund Add plus Concession View, student scope, request stamp and idempotency key). Linked requests are grouped by settled source fee receipt. Concession detail reports `paidConcessionAmount`, `autoCreateRefund`, `canCreateRefundRequests`, and refund references for callers with Refund View. Each refund exposes `concessionRequestId`. Refund approval and settlement retain the normal Refund permissions and maker-checker flow; the payout method is selected during settlement. A linked refund blocks concession reversal until cancelled, rejected, or reversed, and linked concessions require a full reversal. See [the concession contract](./references/concession-policy-end-to-end.md#paid-concession-refund-creation).

Every retryable command requires its idempotency key; every mutation of an existing request requires the request concurrency stamp, and submit/approve also require the current preview fingerprint. Approval requires explicit due-row allocations and due-row stamps. Exact actions are `View`, `Add`, `Edit`, `Submit`, `Cancel`, `UploadDocument`, `ViewDocument`, `Delete`, `Approve`, `Reject`, and `Reverse` under `Finance.Fees.Concession`.

Staged concession uploads are tenant- and uploader-bound. Their stored receipt records the concession type and document definition; attaching a URL binds it to one request and rejects another uploader, requirement, type, tenant, request, or an upload older than 24 hours. Removing or replacing evidence on a Draft releases its old upload for cleanup in the same transaction. An hourly worker removes at most 100 expired unattached uploads per tenant per run and never selects evidence still attached to a request. Submitted requirement and due-row selections are immutable snapshots. Only approval changes due balances, and reversal reopens the linked obligation without rewriting payment or receipt history.

The student concession detail endpoint, `GET api/concession-requests/{studentId}`, includes
`studentDetails.boardName` and `studentDetails.educationMediumName` from the saved student's
academic board and medium. Existing Concession View authorization and student-scope checks apply.

## Accounts API families

### Category and bank settings data

| Contract                  | Validation and exposure                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Income Category name      | Required trimmed business text, maximum 40 characters on create/update; Expense Category maximum remains 128; numbers, punctuation and Unicode are preserved                                                                                                                                                                                                                                                                                                                 |
| Category deletion         | Income Category `canDelete` is true whenever unused, including newly created active categories. DELETE checks Fee Head, charge-category, policy and transaction references; `deletionBlockedReason` identifies an actual dependency. PostgreSQL uses a shared tenant transaction lock for income-category deletion and its initial reference writers, preventing concurrent creation after the usage proof. Expense Categories still require deactivation and no references. |
| Bank names                | Bank, branch and holder names are required trimmed business text, maximum 100 characters; Purpose maximum 500                                                                                                                                                                                                                                                                                                                                                                |
| Bank list/status response | Full `accountNumber` and `upiId` remain null; `maskedAccountNumber`, masked `ifscCode` and `maskedUpiId` are the safe display/export values; UPI masks the entire local part (`***@handle`)                                                                                                                                                                                                                                                                                  |
| Bank detail/edit response | Existing authorized detail route retains full identifiers; list projection is never used as an edit payload                                                                                                                                                                                                                                                                                                                                                                  |
| Versions                  | Categories and banks register with ABP entity-history selectors; edits and status changes record safe field diffs; sensitive bank identifiers remain excluded from entity and input-argument auditing                                                                                                                                                                                                                                                                        |
| Older versions            | Known versions without recorded details are explicitly unavailable; actors/times/values are never reconstructed                                                                                                                                                                                                                                                                                                                                                              |

No schema narrowing or automatic cleanup of historical over-limit names is performed.
Existing over-limit text must be corrected before a subsequent details save; lifecycle checks do not silently rename it.

| Family                 | Controller path                                                                                     |
| ---------------------- | --------------------------------------------------------------------------------------------------- |
| Income                 | `Controllers/Finance/Accounts/Income/IncomeController.cs`                                           |
| Expense                | `Controllers/Finance/Accounts/Expense/ExpenseController.cs`                                         |
| Bank accounts          | `Controllers/Finance/Accounts/Settings/BankAccountController.cs`                                    |
| Account heads          | `Controllers/Finance/Accounts/Settings/AccountHeadController.cs`                                    |
| Transaction categories | `Controllers/Finance/Accounts/Settings/IncomeCategoryController.cs`, `ExpenseCategoryController.cs` |
| Reconciliation         | `Controllers/Finance/Accounts/Reconciliation/ReconciliationController.cs`                           |
| Cash Management | `Controllers/Finance/Accounts/CashManagement/CashManagementController.cs`; [commands and exact authorities](./references/cash-management.md) |

Cash approval validates `countedAmount` against the remaining pending cash after paid expenses and immediately confirms that bank deposit and net Income.
`amount` retains original collection, `spentAmount` retains actual expenses, `depositedAmount` becomes the net deposit and `remainingAmount` becomes 0.
A fully spent source accepts count 0 without a visible Income/deposit row.
Expense options expose only pending positive sources; deposit/credit endpoints remain for legacy approved-held/transit history.
Original manual Income creation retries retain their gross request amount after net finalization.

## Reports and audit API families

| Family              | Controller path                                            |
| ------------------- | ---------------------------------------------------------- |
| Finance reports     | `Controllers/Finance/Reports/FinanceReportController.cs`   |
| Finance audit trail | `Controllers/Finance/Audit/FinanceAuditTrailController.cs` |

## Permission families

Current individual late-fee waivers use `Finance.Policies.CollectionPolicy.ViewWaivers`, `RequestWaiver`, `ApproveWaiver`, and `RejectWaiver` with the scoped `GET /api/finance/late-fee-waivers/list` queue.
The approved but unshipped target removes that queue and approval lifecycle, along with every Collection Policy and Charge Management waiver field.
Fee Collection instead exposes a toggle on each eligible late-fee and approved charge row; the collector enters only a reason and the server waives the full live remaining balance through `Finance.Fees.FeeCollections.ApplyWaiver` as documented in [Fee Collection waiver end to end](./references/fee-collection-waiver-end-to-end.md).

Use backend constants as the security source of truth.
Known Finance families include `Finance.Fees.Charges.*`, `Finance.Fees.Concession.*`, `Finance.Fees.Adjustment.*`, and related Fees, Accounts, Reports, and Audit permissions.

Accounts settings use `Finance.Accounts.Settings.{IncomeCategory|ExpenseCategory|BankAccount}.{View|Add|Edit|ChangeStatus|Delete}`.
Each action is independent; module visibility and Add do not authorize Edit or ChangeStatus.
Authenticated requests to `/api/account-heads`, including former options and CRUD routes, return HTTP 410. Category head lookups return empty options for authorized category editors. Income and Expense Category requests accept name, description, optional tags, and concurrency stamps; they do not require or accept a new Account Head mapping. Internal codes are generated on create and existing historical metadata is preserved.

Bank Accounts require school bank details, year/scope, uniqueness, and concurrency checks. They no longer require an Asset Account Head. Refer to [Income, expense, and bank routing](./references/income-expense-routing.md) for the required charge, policy, collection, and refund fields and the workflow-owned options endpoint.

The previous simplification migration remains historical rollout evidence:

Deploy FE and BE together after applying `20260909183000_SimplifyAccountHeads`. The migration archives the seven removed Account Head columns in `ExtraProperties.LegacyAccountHeadSettings20260909`, then drops those columns and the code index; it preserves IDs, name uniqueness, category/bank links, and transaction records. Rollback restores archived values; heads created after migration receive ID-based codes, INR, posting enabled, and an unrestricted start date. Old control/non-posting/date restrictions no longer determine head eligibility. This is the requested simplification of master configuration; it does not alter transaction currency or monetary calculations. Run on a backed-up database during release coordination, with the old host stopped before schema changes.

Regression coverage: `AccountSettingsSimplificationApi_Tests` checks the minimal create/edit contracts, lookup authorization/type/status, and migration operations; `AccountSettingsGovernance_Tests` checks identity, parent cycles, status, and versioning.
All four families are registered in `ShikshaMitraSubmodulePermissions.Finance.Submodules` and consumed by `FinancePermissionDefinitionProvider`.

If these actions are absent from the permission-management response, rebuild and restart the API with the current definitions before trying to assign grants.
An existing definition returned with `isGranted=false` is a grant issue rather than a missing definition.
Existing roles do not receive missing grants automatically on any seed rerun or version change.
Approve and apply an exact permission-management delta in the intended tenant context, then refresh the signed-in permission snapshot.
Follow the [security standard](../../common/security/permission-consistency.md) for direct/inherited access and audited repairs.
Coverage is in `AccountSettingsPermissionContract_Tests`, `RolePermissionSeederContributor_Tests`, and `AccountSettingsPermissionApi_Tests`.

## Contract rule

Do not create a parallel curated contract tree for Finance.
Add endpoint details here when Finance API docs need more route detail.
Do not restore generated exports under `docs/`.

## Receipt Book and audit authorization

Receipt Book list/detail reads require `Finance.Settings.ReceiptBook.View`.
Creation, editing and status changes require `.Add`; deletion requires `.Delete`.
These checks apply in the application service, including direct service calls.

Finance audit list, detail, correlation, entity, student history and student lookup
require `Finance.Auditing.View`. Module visibility, `Finance.AuditTrail.View` and
Collection View do not substitute for this action. Tenant and payer scope still apply.

## Fee configuration permissions

Refund and Proration list endpoints (`GET /api/finance/refund-policies` and `GET /api/finance/proration-policies`) exclude Archived policies when `status` is omitted, matching Concession.
An explicit `status=Archived` selects only archived policies; Draft and Active select their respective current states.
Academic-year and search filters apply before counting and pagination, so archive badges and list totals reflect the same scoped records.
Existing archive and unarchive endpoints retain lifecycle guards and version history; unarchive restores Draft.
Application regression coverage: `RefundProrationPolicyArchiveFiltering_Tests.cs` under `test/ShikshaMitra.Application.Tests/Finance/Policies`.

Fee Structure and each of the four policy configuration families expose exactly three actions: `View`, `Add`, and `Delete`.

| Action | Fee Structure                                   | Collection, Concession, Refund and Proration policies         |
| ------ | ----------------------------------------------- | ------------------------------------------------------------- |
| View   | Lists, details, context, readiness review       | Lists, detail, version/audit history, simulation              |
| Add    | Create, edit/PATCH, publish and create revision | Create, edit, activate, clone, restore and manage attachments |
| Delete | Delete an unused draft                          | Archive/delete where the existing endpoint supports it        |

Canonical roots are `Finance.Settings.FeeStructure` and `Finance.Policies.{CollectionPolicy,ConcessionPolicy,RefundPolicy,ProrationPolicy}`. Menu/module visibility grants do not authorize these API actions. Structure attachment changes require both structure Add and the supported changed policy family's Add. Proration is not a supported structure attachment; its attachment-impact endpoint is retired. Context capability flags named `edit` and `publish` both derive from structure Add.

Student overrides, exception windows, and charge-period overrides retain their existing permission keys and grants.
The target retires waiver approval and threshold permissions, replacing them with one dedicated Fee Collection apply-waiver permission; configuration Add does not grant authority to reduce receivables.

Deployment: rebuild/restart the API so its permission definitions and permission-list responses reflect this contract, then reload the role editor and application permission state. Existing View/Add/Delete grants keep the same names. Retired fine-grained configuration grants are not promoted automatically: a role that had only Edit/Archive/Publish must be explicitly reviewed and granted Add/Delete as appropriate. Concession and Refund now have canonical Delete grants; an old Archive grant alone does not imply Delete. No permission-grant data migration or tenant grant update is performed by this code change.

## Student fee assignment

The existing student `by-scope` lookup accepts optional `search`, `gender`, `rollNoFrom`,
`rollNoTo`, `hasFeeStructureAssignment`, `chargeCategoryId`, `alreadyCharged`, `skipCount`
and `maxResultCount` parameters. Pagination is opt-in for compatibility with callers that
need the complete scoped audience. Charged status means Pending or Approved charges in
the selected category and academic year. Requests for charge metadata or charged-status
filtering require Charges View, Add, or Update as well as the endpoint's student access.
Charge creation still requires a fee structure assignment; these filters do not relax
that audience rule. Student results retain tenant and current-user academic scope.

The current preview/commit, year-scoped history, permission and migration contracts are documented in [Student fee assignment](./references/student-fee-assignment.md). That contract supersedes older assignment descriptions that require concession configuration.

## Category and bank routing

See [Income and expense routing](./references/income-expense-routing.md) for the current category selection, school bank, recognition, historical compatibility, and validation contracts.

## Separate Income and Expenses registers

GET `/api/accounts/register` accepts direction=income|expense, academicYearId, optional month/year/categoryId/source/search and page/pageSize.
GET `/api/accounts/register/{source}/{id}` accepts academicYearId and enforces the matching Accounts View permission plus year/student scope.
POST `/api/incomes` and `/api/expenses` accept optional bankAccountId, counterparty, paymentReference and UUID idempotencyKey alongside the existing manual fields.
Financial routing lookups add income-entry and expense-entry operations using the exact owning Add grant.
See [the workspace contract](./references/income-expense-workspaces.md) for field meanings, lifecycle, compatibility and integrity.

## Configuration name validation and cloning

Fee Category and all four Finance policy types trim names, collapse Unicode whitespace, and compare names case-insensitively within the current tenant and academic year, including archived records. Duplicate errors carry `Name` and `Location` (`Current` or `Archived`); Fee Category additionally carries `CorrectiveAction`. Archived conflicts direct users to restore the record or choose another name. Existing records and historical snapshots are not rewritten.

Cloning without an explicit name selects the first available `Copy`, `Copy 2`, etc. name across Current and Archived policies, bounded to the 128-character name limit. Explicit names still undergo duplicate validation. Copies remain Draft and nondefault; cloning the default leaves the source default unchanged. Automatically generated policy codes use random GUID material rather than truncated sequential GUID prefixes, which can repeat across clones. Existing database unique indexes remain the final exact-name/code collision guard. Legacy normalization collisions must be reconciled before introducing a database normalized-key index.

Payment Term create and draft-update requests trim `name` before required/minimum/maximum-length validation and save the trimmed display name. Name comparisons use `FinanceName.Equals` (collapsed whitespace, case-insensitive) across all boards and archived/current terms in the current tenant and academic year, excluding the edited term itself. Duplicate names return `Finance:PaymentTerm:000042` with `Name`; duplicate generated codes retain `Finance:PaymentTerm:000023`. Academic-year cloning also skips name conflicts, reporting each skipped source code in `conflictedCodes`. Create/update and target-year bulk-clone name checks and persistence share a tenant/year PostgreSQL transaction advisory lock through commit or rollback. These commands require an actual Read Committed transaction; a stricter ambient snapshot is rejected rather than permitting a stale duplicate comparison. Existing stored names and version snapshots remain unchanged. Concession policy percentages accept at most two decimal places; excessive precision is rejected rather than rounded silently (`Finance:ConcessionPolicy:010027`).

Concession Apply On retains the canonical enum contract: `0 = AllOutstanding`, `1 = CurrentInstallment`, `2 = FutureInstallments`, `3 = AllFees`. DTOs expose the symbolic strings; immutable historical snapshot JSON retains its original numeric enum representation. History presentation must compare canonical enum values before displaying labels.

## Bank transfer evidence

Bank transfer collection details retain `transactionReferenceNumber`, `transferDate` (the entered calendar date), and `accountHolderName` in the original payment instrument JSON. Collection also accepts the existing `transactionId` input alias; a nonblank canonical reference takes precedence. The selected receiving bank remains the validated payment-level `bankAccountId`, with a masked `bankLabel` snapshot captured during collection.

Reconciliation list rows expose the stored evidence in `instrumentDetails` and the original receiving bank through `bankAccountId` and `bankLabel`. Receipt detail responses expose the same evidence in `paymentInstruments[]`, including method, amount, collection timestamp, bank snapshot and instrument details. Provisional and official receipt PDFs show the bank transfer reference, transfer date, account holder and masked receiving bank for fee, charge and combined receipts. Verification reads the original payment instrument rather than recreating its evidence.

Missing historical transfer dates and account holder names remain unknown; receipt or collection dates must not be substituted for the entered transfer date. Previously discarded references or names cannot be recovered without independent evidence. This change uses the existing JSONB column and requires no schema migration or automatic backfill. Receipt PDF cache version `v3` regenerates earlier cached documents from the available original evidence without altering amounts, payment status or receipt numbering. Existing tenant, academic scope and action permissions continue to apply.

## Family mobile Finance

Family monetary responses identify `currency: "INR"` and encode amounts as decimal strings. Concession and adjustment preview fingerprints normalize equivalent decimal scales before hashing. Clients must refresh older previews after this normalization; existing command receipts continue matching their original submitted payloads for replay.

Root: `/api/mobile/v1/finance/students/{studentId}`. Reads return the standard
`{success:true,data:...}` envelope; PDF routes return `application/pdf`.
`academicYearId` is required except for `context`. Pages use `page=1&pageSize=20`
(maximum 100) and return `{items,totalCount,page,pageSize}`. Money is an INR
fixed two-decimal string; due dates are date-only, activity timestamps UTC instants.

| GET suffix | Existing permission | Result |
|---|---|---|
| `context` | `Finance.Fees.FeeCollections.View` | Linked student, enrolled years, selected year, section/action capabilities and disabled reasons |
| `overview`, `installments`, `payments` | `Finance.Fees.FeeCollections.View` | Collection-account summary, obligations and payment history; embedded receipt details additionally require Receipts View |
| `charges` | `Finance.Fees.Charges.View` | Detailed approved child/year charges |
| `receipts`, `receipts/{receiptId}/pdf` | `Finance.Fees.InvoiceReceipt.View` | Official receipts and PDF after tenant/student/year ownership checks |
| `requests?kind=concession` | `Finance.Fees.Concession.View` | Authorized concession history |
| `requests?kind=refund` | `Finance.Fees.Refund.View` | Authorized refund history |
| `requests?kind=adjustment` | `Finance.Fees.Adjustment.View` | Authorized adjustment history |
| `requests` without kind | `Finance.Fees.FeeCollections.View` plus at least one request-family View | Only permitted request families; pagination/count excludes denied families |
| `statement/pdf` | `Finance.Fees.FeeCollections.View` | Official statement of the collection-account summary, installments and payment history |

Every call reloads persisted portal association and identity through `StudentPortalAccessService`.
Existing Finance grants authorize actions; class assignments or a Parent/Student role name alone never establish a family association.
Inactive/departed students, revoked links, revoked enrollments, foreign children and years fail closed.
Legacy year fallback is allowed only without enrollment history.
A student-self identity stays read-only even when also linked as guardian.
Parent commands additionally require a persisted guardian association and the exact existing write action below.

`MobileFinancePermissions` references existing constants and is not a new permission catalogue or a runtime fallback.
Old `Finance.SelfService.*` entries/grants remain untouched, but no longer authorize these routes.
Existing role grants and explicit user denials are not reseeded or rewritten.
The context requires Collections View and adds `canViewCharges`, `canViewReceipts`, `canViewConcessions`, `canViewRefunds` and `canViewAdjustments`.
Request capabilities require the corresponding View and write grants; refund creation also requires Submit.
The overview/statement is the collection-account financial summary (including charge, concession and refund totals); detailed feature lists require their separate View grant.
Deploy this backend before the companion mobile update, whose missing section capabilities fail closed.

Principal amounts come from decimal DueFee ledger values; transaction amounts come
from decimal PaymentMethodRecord splits. Legacy charge/receipt storage is converted
per row at the boundary, never summed through the legacy double DTOs. Stored
outstanding already reflects provisional allocations; pending verification is shown
separately. Future scheduled amount is remaining principal before demand generation.
LateFeeAmount is outstanding policy-assessed late fee; ChargeOutstandingAmount is
outstanding approved ad hoc charge. Neither is hidden inside billed principal.
Concession and noncash adjustment are principal credits. WaiverAmount covers charge
and late-fee waiver evidence. RefundedAmount is refunded principal.

Request history includes submitted staff-created requests and the current actor's
drafts. Other people's drafts, evidence, payout beneficiaries, internal notes and
rejection/approval commentary are excluded. Own draft concessions expose actions
only to the owning guardian with each exact edit, submit or cancel grant. Receipt PDFs reuse the official
receipt generator directly after ownership checks; no staff API bypass is added.
Statements use the same overview, installment and payment projection, show the academic year,
INR, as-of date and provisional-payment distinction, and create no financial writes.

Family command contracts live in `Application.Contracts/Finance/Mobile`; each group
uses the same student/year root above and the standard response envelope.

| Method / suffix | Existing permission | Behavior |
|---|---|---|
| GET `concessions/eligibility`, GET `concessions/{requestId}` | `Finance.Fees.Concession.View` | Child-assigned policy/form, owner-only detail; eligibility includes create/edit/submit/upload capabilities |
| POST `concessions/preview` | Concession View plus one of Add/Edit/Submit | Canonical preview; no financial mutation |
| POST `concessions/drafts` | `Finance.Fees.Concession.Add` | Own draft, nested source validation and idempotency |
| PUT `concessions/{requestId}` | `Finance.Fees.Concession.Edit` | Own draft, concurrency stamp and current state |
| POST `concessions/{requestId}/submit` | `Finance.Fees.Concession.Submit` | Own request, reviewed preview fingerprint, concurrency and idempotency |
| POST `concessions/{requestId}/cancel` | `Finance.Fees.Concession.Cancel` | Own request cancellation, reason, concurrency and idempotency |
| POST `concessions/documents/{concessionTypeId}/{documentId}` | `Finance.Fees.Concession.UploadDocument` | Requirement/signature/size validated staged upload (10 MiB hard cap) |
| GET `concessions/{requestId}/documents/{documentId}` | `Finance.Fees.Concession.ViewDocument` | Exact creator/child/year evidence download |
| GET `refunds/eligibility`, POST `refunds/preview` | `Finance.Fees.Refund.Add` | Eligible settled receipt/allocation IDs, assigned policy and canonical preview |
| POST `refunds` | `Finance.Fees.Refund.Add` and `.Submit` | Beneficiary validation, fingerprint and pending review |
| GET `adjustments/eligibility`, POST `adjustments/preview`, POST `adjustments` | `Finance.Fees.Adjustment.Add` | Exact child/year sources, canonical preview and pending noncash review |
| GET `activity`, GET `notifications` | `Finance.Fees.FeeCollections.View` | Safe status events filtered by each feature's View; `unreadOnly=true` supported |
| POST `notifications/{eventId}/read` | `Finance.Fees.FeeCollections.View` and visibility of that event | Per-user marker for an event in this exact child/year |

Financial mutations require a stable nonempty `idempotencyKey` (maximum 100
characters); reuse the key and original payload after an unknown outcome. Changed
payloads do not reuse a key. Concession edits and actions include the current
`concurrencyStamp`; submission and refund/adjustment creation include the server
`previewFingerprint`. Parent-created submissions always wait for staff review,
including when the guardian also has administrator permissions. Source receipt,
allocation, due, policy, type and document IDs are checked against the authorized
child/year and current workflow state. Payout references, school bank choices,
staff approval fields and caller-calculated final amounts are not trusted inputs.

Activity and notification content is a fixed safe status projection over durable
Finance audit events; private audit payloads and evidence are excluded. Per-user
read markers deduplicate by event identity and survive reloads. Marking a status
message read does not mutate financial history. There is no push delivery or
scheduled reminder worker in this surface.
A missing or incomplete decimal payment-method split does not become a zero payment. `Finance:SelfService:PaymentEvidenceIncomplete` fails the affected overview/history/statement and directs the caller to school Finance for reconciliation; the legacy header is used only to check evidence completeness.
