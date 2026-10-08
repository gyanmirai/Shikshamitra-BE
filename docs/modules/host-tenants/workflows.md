---
module: host-tenants
domain: platform
content_type: Reference
status: canonical
updated_on: 2026-10-08
owner: backend-team
audience: humans-and-agents
---

# Host tenant workflows

TL;DR: bootstrap one host account, provision schools transactionally and suspend access through one audited action.

## Bootstrap

Configure ShikshaMitra__HostAdmin__Email and ShikshaMitra__HostAdmin__Password through secure environment configuration before the first migrator seed.
ShikshaMitra__HostAdmin__UserName defaults to admin.
An existing account is reused untouched.
A new account is created with normal identity validation and assigned the admin role.
Generic identity seeding receives the same username, preventing a second default host login.
The [ABP 9 identity seeder](https://github.com/abpframework/abp/blob/9.0.0/modules/identity/src/Volo.Abp.Identity.Domain/Volo/Abp/Identity/IdentityDataSeeder.cs) returns when that administrator already exists.
Only a newly created host admin role receives the four tenant-management defaults.
Existing roles and accounts keep their current grants, including deliberate revocations.
An operator must explicitly grant `AbpTenantManagement.Tenants.ManageStatus` to an existing authorized host administrator through ABP permission management before enabling status changes.
Do not reset a database, reseed existing grants or introduce a role-name bypass to supply this permission.

## Rollout

Apply migration `20261008065255_HostTenantManagement` before starting the updated API.
Existing schools default to active and retain their institute details.
Run the migrator with secure bootstrap configuration only when the configured host account is missing.
Deploy the backend before the frontend because login initialization requires `/api/host/session-context`.
Sign in with an empty School code and verify the host's exact tenant grants.

## Create a school

1. Authenticate a host identity and check Create.
2. Validate the typed details and unique school code through ITenantManager.
3. Insert the tenant and mark it active.
4. Change into the new tenant and initialize built-in roles plus initial role grants.
5. Create the school admin with a validated password and assign admin.
6. Commit the transaction; any failed identity operation rolls back tenant and identity writes.

## Suspend and restore

1. Check host identity and ManageStatus.
2. Compare the submitted concurrency stamp.
3. Require a reason for suspension.
4. Persist status and history in the same transaction.
5. Subsequent tenant resolution and token checks observe persisted status, including warmed caches.
6. Reactivation restores login and request access without deleting or reseeding data.
