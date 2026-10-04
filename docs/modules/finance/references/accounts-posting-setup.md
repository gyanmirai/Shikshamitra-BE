---
module: finance
domain: accounts
content_type: Reference
status: active
updated_on: 2026-10-02
owner: backend-team
audience: humans-and-agents
---

# Accounts posting setup

`GET /api/accounts/readiness?academicYearId=...` is an alias for the setup read endpoint.

`GET /api/accounts/posting-setup?academicYearId=...` returns the tenant's financial periods, first posting date, cash Asset head, effective non-cash destination rules, and explicit readiness issues. `PUT` on the same path configures the date/cash head; `POST /periods` and `POST /destinations` add period and destination rows. `POST /destinations/{id}/end` sets the last effective date while preserving historical applicability. Read requires `Finance.Accounts.Settings.PostingSetup.View`; writes require `.Edit`. Every operation checks the academic scope and tenant. Bank and account identifiers are validated against active records in the selected academic year. Bank numbers are never returned by this API.

The tables are `Finance_AccountingPeriods`, `Finance_AccountsPostingConfigurations`, and `Finance_PaymentDestinationRules`. A journal now has a foreign key to a real period. The configuration defaults to disabled and editing it disables posting again. The readiness response explicitly reports that instrument allocation posting and accountant-approved refund treatment are pending. There is no activation endpoint and these settings do not create Fee or Accounts journals yet.

`AccountsJournalPoster` is an internal, transaction-bound journal writer. It validates an enabled configuration, open period, whole-paise balance, active same-school heads, and an idempotent source transition before inserting an immutable `FinancialEvent` and balanced `JournalEntry`. No source command calls it until the Fees allocation and refund treatment contracts are complete.

Before activating live posting, finish instrument-to-source allocations, school destination and evidence selection at Fees commands, refund policy treatment, exact journal links in the register, and source transition tests. Do not treat the current source register or refund subledger as a General Accounts journal.
