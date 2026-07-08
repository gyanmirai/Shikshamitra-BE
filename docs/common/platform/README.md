---
description: Canonical backend platform docs for access control and academic scope.
tags: [docs, backend, platform, user-scope]
---

# Platform access and scope

This folder owns shared backend docs for UserScope and academic access control.
Use these pages before searching module docs for scope behavior.

## Read order

| Order | Document | Use it for |
|---|---|---|
| 1 | [UserScope usage](./UserScope-Usage.md) | Scope model, `NULL = ALL`, hierarchy response, cache rules, and implementation patterns |
| 2 | [Bulk scope assignment API](./BulkScopeAssignment-API-Guide.md) | Bulk assignment endpoint, overwrite semantics, payloads, responses, and rollback rules |

## Source of truth

| Concern | Source path |
|---|---|
| Scope service | `src/ShikshaMitra.Domain/Security/UserScope/**` |
| Scope app service | `src/ShikshaMitra.Application/Security/UserScope/**` |
| Scope contracts | `src/ShikshaMitra.Application.Contracts/Security/UserScope/**` |
| Scope tests | `test/ShikshaMitra.*.Tests/Security/UserScope/**` |

## Do not add old handoff docs

Do not keep optimization summaries, migration handoff notes, generated exports, or one-off frontend integration guides in this folder.
Fold current behavior into the canonical docs above instead.

[Back to common docs](../README.md)
