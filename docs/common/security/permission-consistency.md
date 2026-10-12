# Permission consistency standard

This is the canonical security contract for every module.
Read it before changing permissions, navigation guards, identity, user scope, or security-related seeders.

## Authorization model

- Action permission: whether the caller may perform the exact operation.
- Module visibility: whether navigation exposes a module, independent of action grants.
- Tenant boundary: which tenant owns the target, enforced by the server.
- User scope: which records within that tenant the caller may access.

All four concerns are separate.
Hidden controls are not authorization.
A role name must not bypass an action check.
The existing configured administrator data-scope exception remains server-controlled and uses current database role membership, never a client claim alone.

## Catalogue and editor

The current security release retains the old frontend UI at the user's request.
The replacement-editor redesign remains withdrawn.
The user's September 17 correction makes the deployed staging UI and grouped behavior the frontend reference.
Retain expandable navigation groups, compact View/Add/Delete controls, Student action bundles, Accounts Settings exceptions, and separate Module Permission and Set Module Visibility dialogs.
Do not add individual backend action tiles, catalogue search, or a replacement editor.

The backend permission definitions are authoritative for names, hierarchy, display labels, allowed providers, and tenancy side.
Retain the complete server catalogue in the draft and preserve entries outside the displayed navigation groups.
Grouped frontend controls may only submit names present in that catalogue and eligible for the selected provider.
A missing visibility grant must not lock an authorized administrator out of configuring a known module.

Frontend grouping follows the staging mappings and dependencies; it does not change backend permission definitions or endpoint enforcement.
The user-approved Statutory Schemes exception retains the same UI with View/Edit/Delete controls.
Edit groups Edit, Activate, ManageEnrollment, ApproveOverride, and ConfigureCalculation permissions when present in the server catalogue; View remains independent.
Delete is disabled unless the server defines that action; this permission-editor change does not add a deletion operation.
Statutory Schemes Revoke All clears every provider-eligible direct grant at or beneath its exact feature namespace, including undisplayed actions, without touching neighboring features or inherited role grants.
It remains available for partial direct grants, while Grant All only grants explicitly mapped catalogue actions.
The API continues to accept exact permission names without normalizing aliases or inferring additional grants.
Existing domain constants that intentionally share one permission ID remain one capability until an explicitly approved domain change separates them.
The editor must not invent distinct grants for those aliases.

Bulk operations use the complete mapped navigation subtree, deduplicate names, and include collapsed descendants.
Expanding or collapsing a group never changes a grant.
Only changed direct role grants or explicit user override states are submitted.
Users follow live role grants by default. Multiple role grants combine; role edits affect only permissions without an explicit user override.
A user Allow is a direct ABP user grant. A user Deny is an audited, tenant-scoped `UserPermissionDeny` record and takes precedence over role grants, aliases, and assignment-derived access in both single and batch checks.
Follow role removes both the direct user grant and the denial. Role edits never rewrite these user exceptions.
Overrides do not widen tenant or record scope. An authorized permission manager configures them; users cannot grant themselves access through the self-read exception.
Explain both the role source and the effective result in the editor.

Retain drafts across loading failures, background refetches, and provider changes.
Never submit one provider's draft to another provider.
Refetch and verify persisted direct grants or explicit override states after saving.
A pre-save comparison reduces accidental concurrent overwrites but is not a compare-and-swap guarantee under the unchanged API contract.

## Server enforcement

The flat, nested, and user-override permission-service entry points share provider policy and catalogue validation.

| API | Contract |
| --- | --- |
| `GET /api/shikshamitra-permissions/users/{userId}/permissions` | Flat ABP catalogue plus `overrides`, a dictionary of exact permission names to `allow` or `deny`. Missing entries mean `inherit`. Grant sources remain available even when a denial makes `isGranted` false. |
| `PUT /api/shikshamitra-permissions/users/{userId}/permissions` | `{ "permissions": [{ "name": "<exact catalogue name>", "state": "inherit|allow|deny" }] }`; changed entries only; 204 on success. |

The legacy binary U update remains compatible: true means direct Allow, false removes that direct grant, and either clears any prior explicit Deny for that entry. New user editors must use the override API to express Deny.
Existing direct U grants migrate logically to Allow without copying role permissions or reseeding users.
Apply the `AddUserPermissionDenies` migration before starting a backend with override support; it adds storage only and does not alter existing role/user grants.
The deny value provider and the replacement permission checker enforce denial precedence, including legacy catalogue reads. Denial lookup reuse is limited to a unit of work and invalidated by persisted denial changes.
Role management requires the configured R provider policy, normally `AbpIdentity.Roles.ManagePermissions`.
User management requires the configured U provider policy, normally `AbpIdentity.Users.ManagePermissions`.
Authenticated users may read their own effective grants without receiving permission to grant themselves access.

Validate the complete batch before writing.
Names are exact and case-sensitive.
Reject unknown, duplicate, disabled, wrong-tenancy-side, and provider-ineligible entries atomically.
Validate role names and user IDs against current-tenant identity records.
Never normalize Add/Create/Edit aliases or silently discard unknown entries.

Use ABP permission-management writes for provider semantics, cache invalidation, and audit participation.
Use a transaction for the whole batch, reusing an existing transactional unit of work.
Application services enforce authorization even when called without the HTTP controller.
Employee-targeted ESI reads and writes require the exact ESI action plus employee scope, not unrelated employee-profile editing.

## Seeding and upgrades

Baseline grants belong only to the creation transaction of a new built-in role.
An existing role must receive no automatic restoration of missing grants.
A changed seeder version, missing marker, profile switch, or rerun is not authorization to widen access.
Custom roles and administrator-managed grants remain untouched.
For new databases, Clerk and Office Assistant receive only HR leave self-service: HR/leave navigation and Leave Center View, Add, and Delete.
Their non-HR defaults are unchanged.
This explicit default-policy decision does not authorize rewriting grants on existing roles.

User initialization is clean-database setup, not an identity repair workflow.
Existing baseline, HR, and test-logins keep their passwords, role memberships, and identity details on reruns.
Only newly created logins receive initial passwords and roles.
Do not remove passwords, replace role memberships, or recreate removed academic access to make a test login work.
The operator owns any deployed database reset; never infer reset or reseed authorization from a code change.

`RolePermissionSeederContributor` is a compatibility no-op.
`RolesSeederContributor` initializes defaults only after inserting a new role.
The FullDemo ESI seeder configures a Ready scheme but must not create or reactivate privileged HR scopes.
Academic-source-derived synchronization may reconcile records it owns.
It must not overwrite manual scope assignments or treat default seeding as authority to repair access.

## Scope, caching, and failures

Resolve current identity, membership, reporting relationships, and assignments from the database.
Reuse resolved scope only within the current request/unit of work.
Invalidate that local reuse after relevant scope, identity, role, or reporting changes, including inherited reporting results.
Legacy distributed scope-cache settings are not authorization authority.

Project employee IDs and reporting-manager links rather than loading employee graphs.
Batch assignments for the resolved population and project bounded academic hierarchy queries.
Do not issue a query per employee or hierarchy node.
Do not parallelize EF operations sharing a context.

Missing scope yields no access.
Cancellation, missing identity, query failures, and timeouts must not become unrestricted access.
Keep tenant filters explicit and preserve year filtering, wildcard boundaries, ordering, reporting inheritance, and full-access flags.

## Approved access repair

A suspected broad grant is not proof of incorrect access.
Audit current direct grants, role inheritance, manual/derived scope provenance, and relevant seed history.
Mark uncertain provenance explicitly.
Keep identifiable audit exports in restricted storage, never source control, screenshots, or public CI artifacts.

Before a repair, obtain approval for the exact tenant, provider, permission/scope IDs, and before/after values.
Re-read those values immediately before applying the approved delta.
Abort on drift.
Use audited services, record the operator and reason, verify the result, and retain an approved inverse delta for recovery.
Never use a database reset or blanket reseed as an access repair.

## Mandatory change checklist

- Catalogue, editor, route/action guards, and API enforcement agree.
- Preserve approved frontend grouping without inventing API permission names, role-name action bypasses, development bypasses, or blanket reseeding.
- Allowed and denied action tests, direct/inherited grant round trips, invalid-batch rollback, tenant/scope tests, and upgrade-seeding tests pass.
- Direct application calls are covered where controllers duplicate a security boundary.
- `PermissionConsistencyApi_Tests` covers user overrides, role-update persistence, Follow role, invalid-batch rollback, management policy, tenant isolation, and real single/principal/batch checker denial precedence.
- Revocations and membership changes take effect on subsequent requests.
- Permission changes in unrelated module PRs and reverts receive explicit review.
- Browser tests cover dirty state, focus, accessible labels, 44px targets, narrow layouts, and contained scrolling.
- Performance evidence states fixture size, cold latency, warm-up count, sample count, query counts, and p95.
- Security sign-off uses executable and runtime evidence, not screenshots or documentation alone.

## Required local checks and main release gates

Run the backend and frontend Security Regression checks locally before merging feature work.
GitHub Actions runs these jobs only on `main`, through reusable deployment gates triggered by pushes after merge.
Do not add pull-request triggers or require PR workflow runs; every job must guard `github.ref == 'refs/heads/main'`.
Cross-repository changes still require both repositories' local checks and coordinated contract review.
Branch-protection configuration needs separate authorization.
Document Passed, Failed, and Not run cases explicitly.
Local synthetic tests do not prove deployed acceptance.
Keep deployed DDVS verification read-only unless writes are separately authorized.
An exposed database credential remains an outstanding security risk until rotation is verified.
