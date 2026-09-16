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
The user subsequently approved fixing hidden actions, bundled toggles, incomplete bulk removal, and draft/inheritance behavior within the existing layout and separate dialogs.
Do not restore legacy behavior or silently replace the approved layout.

The backend permission definitions are authoritative for names, hierarchy, display labels, allowed providers, and tenancy side.
Show every enabled server-defined entry, including unfamiliar future actions.
Provider-ineligible entries may be shown read-only with their allowed providers.
A missing navigation mapping or visibility grant must not hide configuration from an authorized permission administrator.

Toggle exactly one permission at a time.
Do not imply View, Add, Edit, or Delete, and do not bundle actions by their suffix.
Existing domain constants that intentionally share one permission ID remain one capability until an explicitly approved domain change separates them.
The editor must not invent distinct grants for those aliases.

Bulk operations use the complete selected catalogue subtree, deduplicate names, and include collapsed and search-hidden entries.
Show the count and pending delta before saving.
Only changed direct grants are submitted.
Role-inherited grants remain effective after a direct user grant is removed.
Display the remaining provider and do not describe that result as access fully revoked.

Retain drafts across loading failures, background refetches, and provider changes.
Never submit one provider's draft to another provider.
Refetch and verify persisted direct grants after saving.
A pre-save comparison reduces accidental concurrent overwrites but is not a compare-and-swap guarantee under the unchanged API contract.

## Server enforcement

Both permission-service entry points use the same validation and ABP permission manager.
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
- No fixed CRUD assumptions, implicit action bundles, role-name action bypasses, development bypasses, or blanket reseeding.
- Allowed and denied action tests, direct/inherited grant round trips, invalid-batch rollback, tenant/scope tests, and upgrade-seeding tests pass.
- Direct application calls are covered where controllers duplicate a security boundary.
- Revocations and membership changes take effect on subsequent requests.
- Permission changes in unrelated module PRs and reverts receive explicit review.
- Browser tests cover dirty state, focus, accessible labels, 44px targets, narrow layouts, and contained scrolling.
- Performance evidence states fixture size, cold latency, warm-up count, sample count, query counts, and p95.
- Security sign-off uses executable and runtime evidence, not screenshots or documentation alone.

## Required PR checks and release

The backend `Security Regression / security-backend` and frontend `Security Regression / security-frontend` jobs run on pull requests.
Merge guidance requires both repository checks and coordinated contract review when a change spans repositories.
Branch-protection configuration needs separate authorization.
Document Passed, Failed, and Not run cases explicitly.
Local synthetic tests do not prove deployed acceptance.
Keep deployed DDVS verification read-only unless writes are separately authorized.
An exposed database credential remains an outstanding security risk until rotation is verified.
