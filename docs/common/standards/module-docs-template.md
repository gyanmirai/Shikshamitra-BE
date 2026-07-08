---
module: common
domain: documentation-template
content_type: Reference
status: canonical
updated_on: 2026-07-08
owner: backend-team
audience: humans-and-agents
---

# Copy the module documentation template

TL;DR: copy this checklist when creating or refactoring module docs.
Replace `<module>` and remove sections that do not apply.

## Folder template

Create this folder structure:

```text
docs/modules/<module>/
  README.md
  product.md
  api.md
  architecture.md
  workflows.md
  testing.md
  references/        # optional, only when active deep notes exist
```

Do not add a `references/README.md` placeholder.
Create `references/` only when there is active deep material to link from a canonical page.

## Page template

Start each canonical page with this metadata:

```yaml
---
module: <module>
domain: <domain>
content_type: Landing | Reference
status: canonical
updated_on: YYYY-MM-DD
owner: backend-team
audience: humans-and-agents
---
```

## README.md sections

Use these sections:

- `# Understand the <module> module`
- `TL;DR`
- `Read order`
- `Ownership boundary`
- `Source of truth`
- `For agents`
- `Supporting material`, only when active references exist
- `Validation`

## product.md sections

Use these sections:

- `# Understand <module> product behavior`
- `Product areas`
- `Business rules that protect data`
- `Product behavior by area`
- `Partial surfaces`
- `Canonical contract rule`

## api.md sections

Use these sections:

- `# Use <module> backend APIs`
- `Shared request rules`
- `<area> API families`
- `Error and contract rules`

## architecture.md sections

Use these sections:

- `# Locate <module> backend code`
- `Layer map`
- `Domain subsystems`
- `Implementation rules`

## workflows.md sections

Use workflow names that match user jobs.
Each workflow table should include main steps and validation focus.

## testing.md sections

Use these sections:

- `# Validate <module> backend changes`
- `Required docs checks`
- `Backend API test commands`
- `Focused test map`
- `Frontend integration checks`
- `Acceptance gates`
- `Known verification gaps`
