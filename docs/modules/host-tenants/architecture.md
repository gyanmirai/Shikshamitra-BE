---
module: host-tenants
domain: platform
content_type: Reference
status: canonical
updated_on: 2026-10-08
owner: backend-team
audience: humans-and-agents
---

# Host tenant architecture

TL;DR: ABP Tenant remains the tenant aggregate; a host-owned history records access changes.

## Ownership

| Layer | Source |
| --- | --- |
| Constants and errors | `src/ShikshaMitra.Domain.Shared/HostTenants/HostTenantConsts.cs` |
| Permissions and DTOs | `src/ShikshaMitra.Application.Contracts/HostTenants/` |
| Guards and orchestration | `src/ShikshaMitra.Application/HostTenants/` |
| Provisioning and status rules | `src/ShikshaMitra.Domain/HostTenants/` |
| Filtered list and state queries | `src/ShikshaMitra.EntityFrameworkCore/HostTenants/EfCoreHostTenantQueryRepository.cs` |
| Thin HTTP controller | `src/ShikshaMitra.HttpApi/Controllers/HostTenants/HostTenantController.cs` |
| Token enforcement | `src/ShikshaMitra.HttpApi.Host/Controllers/StudentPortalTokenController.cs` |
| Middleware response | `src/ShikshaMitra.HttpApi.Host/MultiTenancy/InactiveTenantResponse.cs` |

## Data and caching

IsActive and InstituteName are mapped tenant extensions.
TenantStatusChanges is host-owned, so TargetTenantId deliberately does not implement an IMultiTenant filter key.
TenantActivityStore preserves ordinary cached tenant configuration but refreshes status from the database on every lookup.
TenantActivityChecker opens an independent, read-only host unit of work because middleware resolution precedes the request unit of work.
Query errors are never converted into active status.

## Migration

`src/ShikshaMitra.EntityFrameworkCore/Migrations/20261008065255_HostTenantManagement.cs` adds active-by-default status, queryable institute name and audit history.
Existing institute names are copied from ExtraProperties into the new column.
Rollback copies the current institute name back into ExtraProperties before removing the column.
Names over the new 256-character bound cause migration failure rather than silent truncation.
Rollback removes the new status-history table; retain history before an operational downgrade if it is needed.
