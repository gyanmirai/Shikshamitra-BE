# Accounts source activity

`GET /api/accounts/activity?academicYearId=...` is a read-only bridge from Fees to
Accounts. It lists final fee/charge receipts and confirmed refund subledger
entries (including compensating return entries) for students in the caller's
scope. Search is by collection receipt, refund number, or original refund
receipt. The list has server pagination and requires
`Finance.Accounts.Activity.View`. Source navigation is allowed only with the
corresponding Fees view permission.

The activity amount is positive for a collection or returned payout and
negative for a paid refund. Source status describes the Fees action.
`AccountingStatus = Not posted` describes the current General Accounts state:
the endpoint does not create journals or change balances. A final receipt
represents an accepted payment; provisional cheque/DD receipts are omitted
until verification creates a final receipt. A reversed collection remains
visible with `SourceStatus = Reversed`.

This read model uses the canonical receipt and refund subledger rows, so
historical completed actions are visible without backfilling a new event.
Before enabling journal posting, the school must approve its financial calendar
and cutover, and the adapter must resolve exact fee-head, method and settlement
account evidence. Fee income recognition is at accepted payment, per the DDVS
decision on 1 October 2026. Do not treat this source list as a ledger or as a
trial balance.
