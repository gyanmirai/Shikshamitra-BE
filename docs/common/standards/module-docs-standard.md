---
module: common
domain: documentation
content_type: Reference
status: canonical
updated_on: 2026-07-06
owner: backend-team
audience: humans-and-agents
---

# Structure module documentation

TL;DR: each module should have six canonical docs and one optional reference folder.
This keeps first-read docs small and avoids stale material.

## Required structure

Use this structure for each backend module:

```text
docs/modules/<module>/
  README.md
  product.md
  api.md
  architecture.md
  workflows.md
  testing.md
  references/
    README.md
    <topic>.md
```

## File responsibilities

Each canonical file has one job:

| File | Content type | Job |
|---|---|---|
| `README.md` | Landing | Route humans and agents to the right doc in under 2 minutes with required handoff sections |
| `product.md` | Reference | Explain shipped behavior, business rules, partial surfaces, and data integrity notes |
| `api.md` | Reference | List endpoint families, permissions, request conventions, errors, and contract caveats |
| `architecture.md` | Reference | Locate source paths, ownership boundaries, domain shape, and implementation rules |
| `workflows.md` | Reference | Trace end-to-end operational flows and integration effects |
| `testing.md` | Reference | Name validation commands, focused tests, acceptance gates, and known gaps |

## Supporting folder

Use `references/` only when the canonical files need more detail:

| Folder | Rule |
|---|---|
| `references/` | Active deep notes that are too detailed for canonical pages |

Do not place first-read docs under `references/`.
Do not keep generated exports, raw exports, or old plans under `docs/`.

## Metadata

Each active canonical and reference doc must use this frontmatter:

```yaml
---
module: <module>
domain: <domain>
content_type: Landing | Reference | Tutorial | How-to | Troubleshooting | Conceptual
status: canonical
updated_on: YYYY-MM-DD
owner: <team>
audience: humans-and-agents
---
```

Reference docs use `status: active` when they support a canonical page.

## README handoff sections

Each module `README.md` must include these sections:

| Section | Purpose |
|---|---|
| `TL;DR` | State what the module doc set covers |
| `Read order` | Route readers through canonical pages |
| `Source of truth` | Name current source paths |
| `For agents` | Map common questions to first files |
| `Validation` | Name local docs and hygiene commands |

## Writing rules

Write docs for a tired engineer or agent joining mid-task:

- Start every page with one TL;DR paragraph
- Use sentence-case headings
- Keep one sentence per source line
- Keep each page focused on one job
- Prefer tables for maps and contracts
- Name source paths and commands exactly
- Mark partial surfaces instead of implying shipped behavior
- Link to source-of-truth code paths when behavior can drift

## Validator expectations

Module validation should prove these points:

| Check | Expected result |
|---|---|
| Required files | The six canonical files exist |
| Frontmatter | Active canonical and reference docs have required metadata |
| Content type | `content_type` uses a supported value |
| Freshness | `updated_on` is a valid ISO date within the configured max age |
| Links | Internal links resolve inside active docs |
| Source paths | Inline source paths resolve, including `*` and `**` wildcards |
| README handoff | Module README files include the required handoff sections |
| Supporting folders | Only `references/` is allowed |
| Old structure | New reference modules do not keep active `prd/`, `contracts/`, `implementation/`, or `testing/` folders |
| Stale text | Active docs do not contain old paths, mojibake, duplicate README names, or placeholder markers |

## Current reference modules

Use [HR](../../modules/hr/README.md) as the deepest reference implementation for this standard.
Use [Finance](../../modules/finance/README.md), [Student](../../modules/student/README.md), [Library](../../modules/library/README.md), and [Institute Setup](../../modules/institute-setup/README.md) as compact module examples.
