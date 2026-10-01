---
module: finance
domain: accounts-posting-foundation
content_type: Reference
status: active
updated_on: 2026-10-01
owner: product-and-engineering
audience: engineers
---

# Accounts posting foundation

The integration-only plan at `output/accounts-integration-only/Accounts-Integration-Frontend-Backend-Plan.md` governs this work. The first backend slice adds `FinancialEvent`, `JournalEntry`, and `JournalLine` domain records and database constraints. It does **not** capture live Fee events or produce a journal from a collection or refund yet. No Accounts UI or API should imply that live posting is available.

`FinancialEvent.EventKey` hashes tenant, source type and ID, transition, and transition version. Its canonical source snapshot and hash are immutable. The unique database key makes duplicate delivery or retry detectable across host and tenant scopes. A journal references one financial event; its lines must have positive whole-paise amounts on exactly one side and equal debit and credit totals. Corrections are separate entries linked to the entry corrected.

The domain balance check is a prerequisite, not a complete posting transaction. A posting service still needs to validate tenant, account type and active mapping version; lock the accounting period; insert event, journal, lines, and consumer result atomically; and expose scoped read/retry APIs. Do not insert journals through generic repositories until those checks exist. The journal tables are not a substitute for the existing refund subledger.

Live posting remains gated on approval of the accounting recognition basis, financial calendar and closed-period rule, verified cutover/opening balances, accepted-payment evidence and bank destinations, and adjustment equations. Preserve the existing Fee Collection and Refund Management source journeys while those contracts are resolved.
