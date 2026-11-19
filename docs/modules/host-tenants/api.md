---
module: host-tenants
domain: platform
content_type: Reference
status: canonical
updated_on: 2026-10-08
owner: backend-team
audience: humans-and-agents
---

# Host tenant API

TL;DR: these endpoints accept typed details and enforce host identity in the application service.

## Endpoints

| Method and path | Exact permission |
| --- | --- |
| GET /api/host/session-context | Authenticated identity; returns host or tenant account context |
| GET /api/host/tenants | AbpTenantManagement.Tenants |
| GET /api/host/tenants/{id} | AbpTenantManagement.Tenants |
| POST /api/host/tenants | AbpTenantManagement.Tenants.Create |
| PUT /api/host/tenants/{id} | AbpTenantManagement.Tenants.Update |
| PUT /api/host/tenants/{id}/status | AbpTenantManagement.Tenants.ManageStatus |

## Contracts

`src/ShikshaMitra.Application.Contracts/HostTenants/HostTenantDtos.cs` owns names and field limits.
All detail inputs use instituteName, email, contactNumber, address1, optional address2/address3, city, state, country and postalCode.
Creation adds name (school code), adminEmailAddress and adminPassword.
Updates include concurrencyStamp and exclude code, credentials and status.
Status uses isActive, reason and concurrencyStamp.
Responses exclude credentials and image blobs.
Creation action auditing is disabled to prevent password parameter logging.

List requests use filter, optional isActive, skipCount, maxResultCount and sorting.
Search filters all matching names/institutes before counting and paging.
Supported sorting is name, instituteName or isActive with asc/desc; ordering has stable name/ID ties.

## Errors

| Condition | Contract |
| --- | --- |
| Missing authentication | 401 |
| Wrong context or action grant | 403 |
| Stale concurrency stamp | 409 |
| Invalid input or identity password policy | ABP validation/identity error |
| Inactive tenant API request | 403 with errorCode TENANT_INACTIVE |
| Inactive password login | OAuth invalid_grant |
| Inactive refresh | OAuth invalid_grant |

The legacy tenant API rejects the reserved IsActive extra property; suspension must pass through the audited status endpoint.
