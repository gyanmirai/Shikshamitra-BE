---
module: finance
domain: api
content_type: Reference
status: canonical
updated_on: 2026-09-24
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

Fee-category and fee-head create/update contracts accept `module` as an optional numeric `FeeHeadModule` enum value. Send `null` or omit the property when no module applies. The fee-head module lookup returns numeric `value` fields with user-facing `displayName` labels. Every standalone or nested Fee Head request must also send `incomeCategoryId`. Responses return the Income Category plus its resolved default Income Account Head ID and name. The backend accepts only active Income Categories from the Fee Head's academic year whose default account is an active Income head.

Charge endpoints and exact permissions are documented in [Charge management end to end](./references/charge-management-end-to-end.md). The current base route is `api/charges`, with Add, View, Update, Approve, Reject, and Delete action permissions. The reference also records current contract defects: academic-year filtering is not consistently applied, row-level caller scope is incomplete, and the API has no charge-specific correction/refund or command concurrency contract. Do not treat those gaps as supported behavior.

Payment-term requests and responses serialize lifecycle, version, holiday-shifting, demand-generation, and initial-demand enums by name. For example, `demandGenerationRule` uses `OnInstallmentStartDate`, `XDaysBeforeDue`, `OnDueDate`, or `SpecificDate`; numeric enum values are not the canonical wire representation.

`isDefault: true` is valid only when the same create/update request uses `status: Active`. At most one Active Payment Term may be default per tenant, academic year, and Education Board. Selecting another default atomically removes the previous flag for that board; other boards retain their own defaults. Draft and Archived terms cannot retain the flag. List requests accept `boardId` to filter the workspace to one board.

Payment-term version history is exposed separately from the full detail snapshot through `GET api/finance/payment-terms/{paymentTermId}/versions`. It accepts the standard `skipCount` and `maxResultCount` paging parameters and returns version number, lifecycle status, change action/reason, creation time, creator id, and hydrated creator name for the history drawer.

`POST api/finance/payment-terms/templates/preview` is an Add-permission authoring preview. It accepts a board, built-in template (`Annual`, `HalfYearly`, `Quarterly`, `Monthly`, or `Custom`), due-date rule, custom count when applicable, and the draft's demand-generation rule. The server partitions the authoritative board window, allocates exactly 100% with the rounding remainder on the final installment, derives demand dates, and runs the normal Payment Term validator. It does not persist data; the returned installment DTOs may be reviewed, edited, and submitted through the ordinary create/update contract.

Concession Type settings use `GET/POST api/{academicYearId}/concession-types`, `GET/PUT/DELETE api/concession-types/{id}`, `PATCH api/concession-types/{id}/status`, `GET api/concession-types/{id}/versions`, and `GET api/concession-types/document-defaults`. The paged versions endpoint returns the audited action, actor, timestamp, version number, and changed business fields. Administrative lists pass `includeInactive=true`; request creation omits it so only active types are returned. Document contracts use `documentName`, `maxFileSizeBytes`, `acceptedFormats`, and `isRequired`. Active records and used records cannot be deleted; deletion is allowed only after deactivation and while unused. Duplicate normalized names, edits to used document definitions, and missing deactivation reasons return stable business errors. `POST api/concession-request/types/{concessionTypeId}/documents/{documentId}/file` accepts multipart `File` and validates the selected definition's size, extension, MIME type, and file signature before storage. It returns an opaque staged route; `GET api/concession-request/staged-documents/{uploadId}/file` works only for the uploader while the receipt remains unattached and unexpired. Attached evidence is downloaded through `GET api/concession-request/{id}/documents/{documentId}/file`, which requires `ViewDocument` and repeats student-scope validation. Concession responses never expose the underlying shared-storage identifier.

Concession requests select their policy at request time. `GET api/concession-request/context` returns active academic-year policy/version options, eligible heads, outstanding due rows, types, and evidence requirements. When no heads are eligible, `blockers` explains whether the assignment has no outstanding dues, its heads are not concession-enabled/active for the year, its fee-structure scope excludes the enabled heads, or the selected policy has no matching installments. Blockers are explanatory and never relax server-side eligibility. If more than one current assignment matches, it returns `assignmentSelectionRequired=true` plus scoped assignment choices; the caller repeats the query with one `studentFeeStructureId`. It never selects an ambiguous assignment. Preview, draft, create, and update inputs carry `concessionPolicyId` plus `concessionPolicyVersionId`; the backend verifies that they identify the current active version and pins both values on the request. `POST api/concession-request/preview` returns server-calculated values and a 64-character fingerprint. An incomplete draft may omit fee selections and required evidence, but Submit always reruns strict policy, value, due-row, and evidence validation.

Drafts use `POST api/concession-request/drafts`, `PUT api/concession-request/{id}`, and `POST api/concession-request/{id}/submit`; the compatibility `POST api/concession-request` performs create-and-submit and requires both Add and Submit. `POST api/concession-requests/{id}/approval-preview` is the authoritative pre-commit approval calculation, and `POST api/concession-requests/{id}/reversal-preview` returns the balance impact before reversal. Approve repeats approval validation using the preview fingerprint and request/due stamps. Reverse repeats reversal validation using request and allocation due-row stamps. Detail and list responses expose state-derived `availableActions`; detail and student applications also expose the stored lifecycle `timeline` so clients do not infer legal transitions from labels.

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

Accounts settings use `Finance.Accounts.Settings.{AccountHead|IncomeCategory|ExpenseCategory|BankAccount}.{View|Add|Edit|ChangeStatus|Delete}`.
Each action is independent; module visibility and Add do not authorize Edit or ChangeStatus.
`GET /api/account-heads/options` requires `Finance.Accounts.Settings.AccountHead.View`. Category form lookups use `GET /api/{income|expense}-categories/account-head-options?academicYearId=...`, authorized by the corresponding category `Add` OR `Edit` action. They return only active, same-year heads of the matching type (`id`, `name`, `headType`, `displayName`). Category View alone, another category's grants, and module grants do not authorize these lookups or the Account Head master.

Account Head create/update contracts accept `headName`, `headType`, optional `parentAccountHeadId`, `notes`, and the update `concurrencyStamp`. Code, currency, posting/control flags, and effective dates are removed from requests, responses, the domain, and EF mapping. Active status determines availability; tenant/year/type, name uniqueness, parent-cycle, dependency, and concurrency checks remain enforced.

Income and Expense Category create/update requests accept `name`, `description`, `tags`, `defaultAccountHeadId`, and the update `concurrencyStamp`. They no longer accept code or effective dates. Creation generates a unique internal code from the complete category ID and has no date window; editing preserves existing code and date metadata. Existing legacy category date windows still govern transactions. Default-head changes remain rejected once a category is referenced by transactions or Fee Heads.

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

Fee Structure and each of the four policy configuration families expose exactly three actions: `View`, `Add`, and `Delete`.

| Action | Fee Structure | Collection, Concession, Refund and Proration policies |
|---|---|---|
| View | Lists, details, context, readiness review | Lists, detail, version/audit history, simulation |
| Add | Create, edit/PATCH, publish and create revision | Create, edit, activate, clone, restore and manage attachments |
| Delete | Delete an unused draft | Archive/delete where the existing endpoint supports it |

Canonical roots are `Finance.Settings.FeeStructure` and `Finance.Policies.{CollectionPolicy,ConcessionPolicy,RefundPolicy,ProrationPolicy}`. Menu/module visibility grants do not authorize these API actions. Structure attachment changes require both structure Add and the changed policy family's Add. Context capability flags named `edit` and `publish` both derive from structure Add.

Student overrides, exception windows, and charge-period overrides retain their existing permission keys and grants.
The target retires waiver approval and threshold permissions, replacing them with one dedicated Fee Collection apply-waiver permission; configuration Add does not grant authority to reduce receivables.

Deployment: rebuild/restart the API so its permission definitions and permission-list responses reflect this contract, then reload the role editor and application permission state. Existing View/Add/Delete grants keep the same names. Retired fine-grained configuration grants are not promoted automatically: a role that had only Edit/Archive/Publish must be explicitly reviewed and granted Add/Delete as appropriate. Concession and Refund now have canonical Delete grants; an old Archive grant alone does not imply Delete. No permission-grant data migration or tenant grant update is performed by this code change.

## Student fee assignment

The current preview/commit, year-scoped history, permission and migration contracts are documented in [Student fee assignment](./references/student-fee-assignment.md). That contract supersedes older assignment descriptions that require concession configuration.
