# Accounts source activity

`GET /api/accounts/register?academicYearId=...` is the unified, read-only
Income & Expenses source register. The older `/api/accounts/activity` remains
available for historical clients. The new register lists accepted final
fee/charge receipts, confirmed refund subledger entries (including compensating
returns), and legacy manual income/expense transactions. Search and pagination
are server-side. Fees rows require `Finance.Accounts.Activity.View`; manual
income and expense rows require their respective exact Tracking View permission.
Explicit source filters without the matching permission are denied. Student
rows are restricted to the caller's student scope and source navigation requires
the relevant Fees view permission.

The activity amount is positive for a collection or returned payout and
negative for a paid refund. Source status describes the Fees action.
`AccountingStatus = Not posted` describes the current General Accounts state:
the endpoint does not create journals or change balances. A final receipt
represents an accepted payment; provisional cheque/DD receipts are omitted
until verification creates a final receipt. Reversed and provisional
collections are excluded from the accepted-income register. Legacy manual
entries are labelled `Recorded (legacy)` and are not journals.

This read model uses the canonical receipt and refund subledger rows, so
historical completed actions are visible without backfilling a new event.
Before enabling journal posting, the school must approve its financial calendar
and cutover, and the adapter must resolve exact fee-head, method and settlement
account evidence. Fee income recognition is at accepted payment, per the DDVS
decision on 1 October 2026. Do not treat this source list as a ledger or as a
trial balance.
