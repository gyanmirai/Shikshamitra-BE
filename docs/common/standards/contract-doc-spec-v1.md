# Contract Doc Spec v1

Applies to canonical contract docs under `docs/modules/<module>/contracts/curated/*.md`.

Required section order:
1. Purpose
2. Endpoint(s)
3. Auth & permissions
4. Request headers
5. Path/query/body contract
6. Response contract (success + errors)
7. Business rules / invariants
8. Idempotency / concurrency notes (if relevant)
9. Examples (minimal, copy-ready)
10. Change log

Rules:
- Keep concise and implementation-accurate.
- Avoid duplicating full raw contract text.
- Link PRD and raw source for deep details.
