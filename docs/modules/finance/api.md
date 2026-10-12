---
module: finance
domain: api
content_type: Reference
status: canonical
updated_on: 2026-10-12
owner: backend-team
audience: humans-and-agents
---

# Use Finance backend APIs

TL;DR: use this page to find Finance endpoint families and route ownership.
Controller source paths are canonical when a route detail changes.

Fee Structure configuration, publication and revision contracts: [workspace implementation](./references/fee-structure-workspace.md).

## Shared request rules

Finance APIs require authenticated access and tenant context through the platform pipeline.
Mutations must enforce backend permissions and audit important financial actions.
Year-only setup checks permit users with database-backed tenant-wide scope to configure an existing current-tenant academic year before its first Student exists. Unknown or foreign-tenant years remain denied. Board, medium, class, division and individual Student checks continue to use the scoped Student query.

## Charge workflow contract

`GET api/charges` filters the batch lifecycle with `status=0` (Pending), `1` (In Process), `2` (Completed), or `3` (Rejected). Responses expose `workflowStatus` separately from the existing student approval `status`. New charges always await an independent checker; completion is derived from settlement evidence and reversals reopen outstanding batches. See [the charge lifecycle contract](./references/charge-management-end-to-end.md#four-state-batch-lifecycle) for totals, mixed decisions, waivers and compatibility.

## Fees API families

| Family | Controller path | Main routes or base route |
|---|---|---|
| Charges | `Controllers/Finance/Fees/Charges/ChargeController.cs` | `api/charges` |
| Concession requests | `Controllers/Finance/Fees/Concessions/ConcessionRequestController.cs` | `api/concession-request`, `api/concession-requests` |
| Fee adjustments | `Controllers/Finance/Fees/Adjustments/FeeAdjustmentController.cs` | `api/fee-adjustments` |
| Fee collection | `Controllers/Finance/Fees/Collections/FeeCollectionController.cs` | Fee collection routes |
| Fee receipts | `Controllers/Finance/Fees/Receipts/FeeReceiptController.cs` | Fee receipt routes |
| Fee refunds | `Controllers/Finance/Fees/Refunds/FeeRefundController.cs` | Fee refund routes |
| Student fee assignment | `Controllers/Finance/Fees/StudentFeeAssignment/StudentFeeStructureController.cs` | Student fee structure routes |
| Fee settings | `Controllers/Finance/Fees/Settings/**` | Fee category, fee head, fee structure, receipt book, concession type, and charge category routes |

`GET /api/fee-categories` accepts academic year, active status, search, page, and page size. Its optional `filters` query value is a JSON array of `{column,type,value}` entries: `name` and `description` support contains, equals, startsWith, and endsWith; `totalFeeHeads` supports equals, greaterThan, lessThan, and an inclusive `{min,max}` between range. `sortBy` accepts `name`, `description`, or `totalFeeHeads`, with `sortDirection` `asc` or `desc`. Filtering and sorting run before pagination; ties use category ID.

`GET api/v1/fee-receipts` requires `academicYearId`, `boardId`, and
`educationMediumId`. Its optional `month` is a calendar month from 1 (January)
through 12 (December), applied directly to `ReceiptDate.Month`. Omit `month` to
list every month in the selected academic year. The list retains tenant and user
scope filtering, optional class/division and receipt-kind filters, and server pagination.

Fee-category and fee-head create/update contracts accept `module` as an optional numeric `FeeHeadModule` enum value. Send `null` or omit the property when no module applies. The fee-head module lookup returns numeric `value` fields with user-facing `displayName` labels. Every standalone or nested Fee Head request must also send `incomeCategoryId`. Responses return the Income Category. Historical Income Account Head fields are compatibility evidence. The backend accepts active, effective Income Categories from the Fee Head's academic year without requiring an Account Head.

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

## Accounts API families

| Family | Controller path |
|---|---|
| Income | `Controllers/Finance/Accounts/Income/IncomeController.cs` |
| Expense | `Controllers/Finance/Accounts/Expense/ExpenseController.cs` |
| Bank accounts | `Controllers/Finance/Accounts/Settings/BankAccountController.cs` |
| Account heads | `Controllers/Finance/Accounts/Settings/AccountHeadController.cs` |
| Transaction categories | `Controllers/Finance/Accounts/Settings/IncomeCategoryController.cs`, `ExpenseCategoryController.cs` |
| Reconciliation | `Controllers/Finance/Accounts/Reconciliation/ReconciliationController.cs` |

## Reports and audit API families

| Family | Controller path |
|---|---|
| Finance reports | `Controllers/Finance/Reports/FinanceReportController.cs` |
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


## Fee configuration permissions

Refund and Proration list endpoints (`GET /api/finance/refund-policies` and `GET /api/finance/proration-policies`) exclude Archived policies when `status` is omitted, matching Concession.
An explicit `status=Archived` selects only archived policies; Draft and Active select their respective current states.
Academic-year and search filters apply before counting and pagination, so archive badges and list totals reflect the same scoped records.
Existing archive and unarchive endpoints retain lifecycle guards and version history; unarchive restores Draft.
Application regression coverage: `RefundProrationPolicyArchiveFiltering_Tests.cs` under `test/ShikshaMitra.Application.Tests/Finance/Policies`.

Fee Structure and each of the four policy configuration families expose exactly three actions: `View`, `Add`, and `Delete`.

| Action | Fee Structure | Collection, Concession, Refund and Proration policies |
|---|---|---|
| View | Lists, details, context, readiness review | Lists, detail, version/audit history, simulation |
| Add | Create, edit/PATCH, publish and create revision | Create, edit, activate, clone, restore and manage attachments |
| Delete | Delete an unused draft | Archive/delete where the existing endpoint supports it |

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
