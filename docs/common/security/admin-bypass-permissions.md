# Permission-Driven Access

## Overview

ShikshaMitra uses explicit ABP permission grants for endpoint access. Runtime role-name bypasses are not used for tenant or permission-management APIs.

Admin users get broad access because the seeders grant the admin roles the required permissions, not because application code skips permission checks.

## Rules

- Permissions decide whether a user can call an endpoint.
- User scope decides which board, medium, stage, class, division, or tenant-scoped data the user can see.
- Host users with tenant-management permissions can list and manage multiple tenants.
- Tenant users can never list multiple tenants, even if they have admin-style permissions.
- Tenant users with tenant-management permissions can view or update only their current tenant.
- Permission-management APIs must be opened by granting ABP permission-management permissions to the role or user.

## Tenant Access

Tenant access must use `TenantManagementPermissions.Tenants.*` checks.

- `TenantManagementPermissions.Tenants.Default` allows tenant read access.
- `TenantManagementPermissions.Tenants.Update` allows tenant update access.
- Host context with the required permission can access all tenants.
- Tenant context with the required permission is limited to `CurrentTenant.Id`.

Do not use `CurrentUser.IsInRole(...)` to decide tenant API access.

## Permission Management

Roles that should open permission-management screens need these grants:

- `AbpPermissionManagement.Permissions`
- `AbpPermissionManagement.Permissions.Manage`

Do not add custom permission value providers to grant all permissions to operational roles. New roles should work by assigning the needed permissions in seed data or through the permission-management UI.

## Admin Access

The seeded `admin` and `super-admin` roles should receive all application/platform permissions through `RolePermissionSeederContributor`.

Admin access is still tenant-bound:

- Host admin can manage all tenants.
- Tenant admin can manage only the current tenant.
- Tenant admin data access remains subject to user-scope behavior unless a specific scope service intentionally defines an admin data-scope rule.
