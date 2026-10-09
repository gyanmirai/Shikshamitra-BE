---
module: host-tenants
domain: platform
content_type: Reference
status: canonical
updated_on: 2026-10-08
owner: backend-team
audience: humans-and-agents
---

# Tenant behavior

TL;DR: genuine host identities manage tenants; school users retain their tenant boundary.

## Rules

- A role name alone never authorizes a host operation.
- Every host operation checks the database identity, host context and exact action grant.
- Creation persists the tenant, initial roles, grants and validated school administrator in one transaction.
- The production provisioning path never invokes general demo-data seeding.
- The school code is fixed after creation through this API.
- Basic details include institute name, email, contact, postal address, city, state and country.
- Address lines 2 and 3 are optional.
- Deactivation requires a reason and preserves school records.
- Status history records previous/new status, operator and time.
- Updates and status actions require the current concurrency stamp.
- Login, refresh and subsequent tenant API requests check persisted active status.
- Repeated requests for the same current status do not create duplicate history.

## Account bootstrap and upgrades

The migrator reuses the configured host administrator without changing its password, memberships or grants.
A missing account requires a secure configured email and password; no fixed default password is used by this bootstrap.
Only a newly created administrator role receives initial tenant grants.
Existing roles are never reseeded to restore revoked access.
On upgrades, an authorized operator must explicitly grant `AbpTenantManagement.Tenants.ManageStatus` to the intended host administrator through the existing permission-management service.

## Boundaries

This workspace does not expose tenant deletion, impersonation or connection-string editing.
New tenants use the shared database and ordinary tenant setup after their administrator logs in.
