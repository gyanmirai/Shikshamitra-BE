---
module: common
domain: documentation
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Add documentation metadata

TL;DR: active module docs need consistent frontmatter so humans and agents can classify a page without reading the whole file.

## Required keys

Use these keys for active canonical and reference docs:

- `module`
- `domain`
- `content_type`
- `status`
- `updated_on`
- `owner`
- `audience`

## Example

```yaml
---
module: hr
domain: api
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---
```

## Status values

Use these status values:

| Status | Meaning |
|---|---|
| `canonical` | Current source-of-truth doc |
| `active` | Current supporting reference doc |

## Metadata rule

Do not use metadata to keep old plans, raw exports, or stale generated files in `docs/`.
Update canonical pages from current source instead.
