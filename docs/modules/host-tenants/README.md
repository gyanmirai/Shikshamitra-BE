---
module: host-tenants
domain: platform
content_type: Landing
status: canonical
updated_on: 2026-10-08
owner: backend-team
audience: humans-and-agents
---

# Host tenant management

## TL;DR

TL;DR: host administrators manage school tenants through a dedicated, permission-protected workspace.
This module covers production tenant provisioning, basic details and audited access suspension.

## Read order

1. [Product](product.md): behavior and boundaries.
2. [API](api.md): contracts and permissions.
3. [Architecture](architecture.md): source ownership.
4. [Workflows](workflows.md): bootstrap, provisioning and suspension.
5. [Testing](testing.md): executable validation.

## Source of truth

| Concern | Source |
| --- | --- |
| Contract | `src/ShikshaMitra.Application.Contracts/HostTenants/` |
| Application | `src/ShikshaMitra.Application/HostTenants/` |
| Domain | `src/ShikshaMitra.Domain/HostTenants/` |
| Persistence | `src/ShikshaMitra.EntityFrameworkCore/HostTenants/` |
| HTTP tests | `test/ShikshaMitra.HttpApi.Tests/TenantSettings/HostTenantApi_Tests.cs` |

[Frontend documentation](../../../../Shikshamitra-FE/docs/host-tenants/README.md) owns routes and screen behavior.

## For agents

Start with the contract and application service for API changes.
Read [permission consistency](../../common/security/permission-consistency.md) before changing grants or account bootstrap.

## Validation

Run the focused commands in [testing](testing.md) and `git diff --check`.
