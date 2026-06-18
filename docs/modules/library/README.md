# Library Module Documentation

This documentation pack is the canonical handoff for the Library module as implemented through Phase 3D release hardening.

## Documents

- [Product requirements](prd/prd.md) describes the delivered product scope, actors, workflows, and future boundaries.
- [API contract map](contracts/curated/library-api.md) lists the canonical `/api/library/...` routes, request shapes, response conventions, permissions, and error behavior.
- [Implementation handoff](implementation/README.md) explains backend and frontend integration responsibilities for future agents.
- [Testing and readiness](testing/README.md) records the release gates, browser E2E expectations, and current QA status.

## Current Release Surface

The active Library menu is Dashboard, All Books, Issue & Return, Library Members, Charges, Reports, Notification, and Settings. Settings includes Book Categories, Charges Categories, Library Hours, Borrowing Rules, and Section Management.

Prototype Library routes that are not part of that surface should remain redirected, hidden, or marked coming soon until they are deliberately promoted into the release menu.

## Next Phase Boundary

The next step before product expansion is Phase 3E: run the gated browser E2E suite against a live local stack and close any defects it finds. Finance integration should start only after Phase 3E sign-off.
