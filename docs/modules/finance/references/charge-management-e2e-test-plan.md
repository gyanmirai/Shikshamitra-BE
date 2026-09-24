---
module: finance
domain: charge-management-testing
content_type: Reference
status: active
updated_on: 2026-09-22
owner: qa-and-finance-team
audience: humans-and-agents
---

# Charge Management end-to-end test plan

This document is the executable QA plan for Charge Category, Charge Management, approval, Fee Collection, receipts, reconciliation, audit, and reporting. It supplements [Charge management end to end](./charge-management-end-to-end.md), which remains the lifecycle and architecture reference.

## Test objective

Prove that an authorized user can create a valid ad hoc student obligation, a different authorized user can approve it, Fee Collection can collect it exactly once, the correct charge receipt and audit evidence are produced, and corrections never corrupt balances or history.

The primary reconciliation rule is:

```text
approved charge principal
- non-reversed charge collections
- approved voids or corrections
+ reinstated amount from a reversed collection
= charge outstanding
```

Every UI total, API response, receipt, export, audit record, and report must agree with this calculation.

## Review evidence captured on 2026-09-22

Environment reviewed:

| Item | Value |
|---|---|
| Application | `http://localhost:3000` |
| Tenant | `ddvs` |
| User | `admin-ddvs` |
| Academic year | `2026-2027` |
| Browser surface | Local in-app browser |

Observed results:

| Area | Result | Evidence or implication |
|---|---|---|
| Charges workspace | Pass | Page loaded with Board, Medium, Class, Division, Pending, Processed, search, paging, and Create Charge controls |
| Charge form | Pass | Charge fields, explicit student selection mode, class scope, student search, and create action rendered |
| Category lookup | Pass | All seven currently active/accounting-ready categories were shown in the Charge Request form |
| Inactive category exclusion | Not executable | No inactive category exists and the current user cannot change category status |
| Category status action | Blocked | Every status switch was disabled for `admin-ddvs`; grant `Finance.Settings.ChargeCategory.ChangeStatus` to the test role |
| Category table search | Pass | Positive search returned only the matching category; a nonsense search returned the explicit empty state and `0–0 of 0` |
| Category table sort/columns/density | Pass | Name sort worked in both directions, Description could be hidden/restored, and Dense could be toggled/restored |
| Category table page size | Pass | Page size changed from 10 to 25 and retained the correct `1–7 of 7` count |
| Category table next/previous paging | Blocked by data | Only seven rows exist and the smallest page size is 10, so a second page cannot be produced |
| Category Excel export | Inconclusive | The export menu action closed, but the browser emitted no download event within 10 seconds and showed no console error; repeat with download instrumentation before filing a product defect |
| Direct route/reload | Needs confirmation | After fullscreen/reload while Next.js was recompiling, a fresh tab showed `No permission` with empty academic-year context; verify again from a clean dev-server start before classifying it as an authorization defect |
| Charge list data | Blocked by data | Pending and Processed both showed zero records |
| Student selection | Blocked by data | No students were available before selecting a class; seeded student scope is required |
| Fee Collection workspace | Pass | Summary columns include Total Charges and Pending Charges |
| Collection data | Blocked by data | All Classes and Class 1 returned zero rows, so collection detail and payment submission could not be exercised |
| Money-changing workflow | Not run | No charge, payment, receipt, reversal, or deletion was submitted during this review |

The current browser result is therefore a partial UI smoke test, not a financial E2E pass.

Automated execution from the same review:

| Suite | Result | Interpretation |
|---|---|---|
| Frontend charge/category/allocation focus | 14/14 passed | Active-category request, charge amount validation, and fee/charge allocation helpers are green |
| Backend Charge/Fee Collection/Receipt API selection | 4 passed, 49 failed | Not a valid product regression signal: nearly all failures share a test-fixture 404 while creating a student through `/api/student/enrollment/registrations/general-details`; repair/rebuild the fixture route before rerunning |
| Backend Application charge/collection selection | 32 passed, 9 failed | All nine failures are legacy `FeeCollectionAppServiceV2_Tests` calls rejected by the newer student-scope guard because the fixture does not create the fake administrator's database role/scope assignment |
| Backend Domain charge lifecycle selection | No tests discovered with `--no-build` | The normal test output is stale and does not contain `ChargeLifecycle_Tests`; rebuild before using this result as evidence |
| Finance documentation validation | Passed | New plan and cross-links satisfy module documentation rules |

Treat the backend result as a P0 test-infrastructure blocker. It does not prove that the 49 business scenarios are defective, but it currently prevents those suites from providing release evidence.

## Current execution results and non-working list

This is the result of executing the safely testable scenarios one by one against the current local build. A row marked `FAIL` is an observed product behavior. A row marked `BLOCKED` or `INFRA FAIL` is not counted as a product defect.

### Browser results

| Scenario | Status | Observed result | Recommended solution |
|---|---|---|---|
| CC-001 | PASS | Charge Category list loaded for the selected academic year | Retain as a smoke test and add tenant/year isolation automation |
| CC-004/CC-006 | BLOCKED | Deactivate/reactivate switches are disabled for `admin-ddvs` | Assign `Finance.Settings.ChargeCategory.ChangeStatus` to a dedicated category-admin test user; keep the backend permission requirement intact |
| CC-005 | PARTIAL PASS | Charge Request listed all seven currently active/accounting-ready categories | Seed one inactive category, then prove absence across every lookup page and reject its ID through the API |
| CC-012 | PASS (UI) | A user without ChangeStatus cannot operate the status switch | Add a direct API assertion that the same user receives 403 |
| CC-013 (search) | PASS | Matching and no-result searches returned correct rows/counts after the server debounce | Retain as browser regression coverage |
| CC-013 (sort/columns/dense/page size) | PASS | Both Name sort directions, column visibility, Dense, and page-size selection behaved correctly | Retain as component and browser coverage |
| CC-013 (next/previous page) | BLOCKED | Seven rows cannot exceed the minimum 10-row page size | Seed at least 11 categories for pagination coverage |
| CC-013 (Excel export) | INCONCLUSIVE | No browser download event was observed, but the action produced no console error | Add an export integration test that stubs `XLSX.writeFile`; rerun in a clean browser and assert filename plus exported filtered rows |
| UI-001 | NEEDS CONFIRMATION | A direct-route reload during Next.js recompilation displayed `No permission` and an empty academic-year selector | On cold navigation, delay the permission guard until auth grants and academic context are hydrated; add a direct-navigation browser test. Reproduce after a clean dev-server restart before treating as confirmed |
| CR-001 through CR-015 | BLOCKED | No selectable students or existing charge requests are available in the tested scope | Seed disposable in-scope, out-of-scope, and cross-year students plus two categories; record cleanup IDs |
| CV-001 | PASS | Blank submission showed field-level errors for name, category, amount, due date, purpose, and class | Keep browser coverage and assert exact messages |
| CV-002 (`0`, alphabetic) | PASS | Zero and alphabetic input did not become a valid amount | Keep schema and API validation coverage |
| CV-002 (negative) | FAIL | Typing `-1` displayed and stored `₹ 1`; the invalid sign was silently discarded | For this field, allow the raw negative value to reach Formik and show the existing greater-than-zero error, or detect the rejected sign and set a field error immediately; never reinterpret a negative as positive |
| CV-003 | PASS | `0.01` was accepted | Keep the boundary test |
| CV-004 | PASS | `9999999.00` was accepted | Keep the boundary test |
| CV-005 | FAIL | Typing `9999999.01` left the field at `₹ 99,99,999.00` without explaining that the attempted value exceeded the maximum | Permit the raw attempted value long enough to display `Amount cannot exceed 9,999,999.00`, or surface an immediate rejected-input error from `isAllowed`; do not silently retain a different amount |
| CV-006 | FAIL | Typing `10.999` displayed `₹ 10.99`; the third decimal was silently truncated | Capture the source input and show `Amount must have at most 2 decimal places`; do not silently round/truncate a monetary obligation |
| FC-001 through FC-022 | BLOCKED | Fee Collection returned no student rows for All Classes or Class 1 | Seed an assigned fee plus approved charge and use a dedicated collector; then verify persistence, allocations, receipts, audit, and balances after each collection |
| RR/AP/VD/AR monetary scenarios | NOT RUN | No financial mutation was submitted and there is no maker/checker/collector dataset | Execute only in a disposable tenant with separate author/checker/collector users and recorded starting balances |

The three amount failures originate before Yup validation: `NumericFormatCustom` sets `allowNegative={false}`, globally rejects values above `9999999` through `isAllowed`, and the charge form sets `decimalScale={2}`. Those controls transform or refuse the user's attempted text, so the existing correct validation messages never run. Make rejected input explicit rather than weakening backend limits.

### Automated-suite failures and fixes

| ID | Type | Status/evidence | Fix |
|---|---|---|---|
| AUTO-001 | Test infrastructure | 49 of 53 selected HTTP API tests failed, principally at `ChargeTestHelper.CreateStudentAsync` and `ConcessionRequestTestHelper` because `/api/student/enrollment/registrations/general-details` returns 404 | Replace the obsolete route with the current supported onboarding/finalization contract or seed students through repositories/test services; add a fast fixture health assertion before the suite |
| AUTO-002 | Test infrastructure | 9 of 41 selected Application tests failed with `AbpAuthorizationException: student is outside your user scope` | Update `FeeCollectionAppServiceV2_Tests` using the working pattern in `LateFeeCollectionRuntime_Tests`: create the fake principal's real `IdentityUser`/administrator role membership, or seed an explicit `UserScopeAssignment` matching each student/year scope |
| AUTO-003 | Test infrastructure | `ChargeLifecycle_Tests` were not discovered when the Domain suite ran with `--no-build` | Rebuild the Domain test project (or use an isolated artifacts directory) before filtered execution; fail CI when a required test filter discovers zero tests |
| AUTO-004 | Coverage gap | The green frontend schema tests reject unsafe raw strings, but browser input transforms three of them before validation | Add component-level typing/paste tests for `-1`, `10.999`, and `9999999.01`, asserting both unchanged meaning and visible error text |

### Confirmed release blockers

1. Fix the three browser amount-handling defects in the shared numeric input/Charge form interaction.
2. Repair the HTTP API student fixture so Charge, Fee Collection, and Receipt integration tests reach their business assertions.
3. Repair the Fee Collection application fixture's identity/scope setup.
4. Provide disposable students, category-admin permission, and separate author/checker/collector users for browser E2E.
5. Add one automated approved-charge -> collection -> receipt -> reversal reconciliation journey before release.

## Required test data and roles

Do not start the financial journey until all rows below are prepared in a disposable local or staging tenant.

### Users and permissions

| Persona | Required permissions | Purpose |
|---|---|---|
| Charge author | `Finance.Fees.Charges.View`, `.Add`, `.Update`, `.Delete` | Create and maintain pending requests |
| Charge checker | `Finance.Fees.Charges.View`, `.Approve`, `.Reject`, `.Void` | Enforce maker-checker and correction lifecycle |
| Collector | `Finance.Fees.FeeCollections.View`, `.Add` | View balances and collect fees/charges |
| Category administrator | Charge Category View/Add/Edit/ChangeStatus/Delete | Exercise category lifecycle independently |
| Auditor | Charge, collection, receipt, audit, and report View permissions only | Verify evidence without mutation rights |
| Unauthorized user | No Charge/Collection actions | Verify UI and direct-API denial |

The author and checker must be different users. Do not use a role name as a substitute for the exact permission.

### Reference data

Create or identify:

- one active academic year and one closed/different academic year;
- Board, Medium, Class, and Division with at least three active students;
- one out-of-scope student and one student in another academic year;
- an active receipt book and valid payment-method configuration;
- an active Income Category and Income Account Head mapping for charge categories;
- a Collection Policy that allows online methods and another that disallows them;
- enough report/audit access to verify downstream entries.

### Charge categories

| Category | State | Approval | Accounting mapping | Use |
|---|---|---:|---|---|
| `E2E-AUTO` | Active | No | Valid | Immediate approval path |
| `E2E-CHECK` | Active | Yes | Valid | Maker-checker path |
| `E2E-INACTIVE` | Inactive | Either | Valid | Lookup and direct-API rejection |
| `E2E-UNMAPPED` | Active candidate | Either | Missing/invalid | Accounting readiness rejection |

### Students and starting balances

| Student | Scope | Starting data |
|---|---|---|
| Student A | Author/checker/collector scope | Assigned fee structure with an unpaid due fee |
| Student B | Same scope | No scheduled fee required; used for charge-only collection |
| Student C | Same scope | Used for multi-student partial approval |
| Student D | Outside caller scope | Used for authorization isolation |
| Student E | Different academic year | Used for year isolation |

Record all starting balances before execution so cleanup and reconciliation can be proven.

## Execution result values

Use one status for every scenario:

- `PASS`: observed result exactly matched the expected result.
- `FAIL`: observed result contradicted the expected result; record defect ID and evidence.
- `BLOCKED`: prerequisite, permission, or environment prevented execution.
- `NOT RUN`: intentionally deferred.
- `NOT SUPPORTED`: confirmed product gap, not a test failure.

## Scenario matrix

### 1. Charge Category lifecycle

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| CC-001 | P0 | Open category list with View permission | Correct academic-year categories load; no cross-year or cross-tenant data appears |
| CC-002 | P0 | Create active mapped category without approval | Saved once; audit contains mapping and actor; category is available in Charge Request |
| CC-003 | P0 | Create active mapped category requiring approval | Saved once and available in Charge Request with approval guidance |
| CC-004 | P0 | Deactivate an unused active category with a valid reason | PUT/PATCH status call succeeds, status becomes Inactive, row refreshes, audit is written |
| CC-005 | P0 | Open Charge Request after CC-004 | Deactivated category is absent even when there are more than ten categories |
| CC-006 | P0 | Reactivate CC-004 | Category returns to active list and Charge Request lookup |
| CC-007 | P0 | Call create-charge API with inactive category ID | Backend rejects the request; client filtering is not the only protection |
| CC-008 | P0 | Activate category with missing/inactive/wrong-year income mapping | Activation is rejected with an actionable accounting error |
| CC-009 | P1 | Deactivate category referenced by pending/approved/paid charges | Existing charge snapshots remain readable; product dependency rule is enforced and explained |
| CC-010 | P1 | Delete referenced category | Deletion is blocked or archived; financial history is never orphaned |
| CC-011 | P1 | Two administrators change status concurrently | Stale change is rejected or serialized; final status and audit are deterministic |
| CC-012 | P0 | User lacks ChangeStatus | Switch is disabled and direct PUT/PATCH returns 403 |
| CC-013 | P1 | Search, paging, export, dense mode | Results and counts remain correct across pages; export matches active filters |
| CC-014 | P1 | Duplicate normalized name in same year | Duplicate is rejected; same name in another tenant/year follows the documented uniqueness rule |

### 2. Charge authoring and audience resolution

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| CR-001 | P0 | Create one-student charge with `E2E-AUTO` | One immutable student line is created Approved and appears in Processed |
| CR-002 | P0 | Create one-student charge with `E2E-CHECK` | One line is Pending and appears in Pending |
| CR-003 | P0 | Create charge for selected Students A and B | Exactly two lines are created; no other student receives a charge |
| CR-004 | P0 | Create charge using All students in scope | Confirmation shows resolved count; server creates exactly that many unique lines |
| CR-005 | P0 | Selected mode with zero selected students | Create is blocked; it never silently changes to All students |
| CR-006 | P0 | All-in-scope mode resolves zero students | Create is blocked with `No students are available in the selected scope` |
| CR-007 | P0 | Student D supplied by direct API | Backend rejects out-of-scope student even if the UI is bypassed |
| CR-008 | P0 | Student E supplied for current year | Backend rejects academic-year mismatch |
| CR-009 | P1 | Duplicate student IDs in request | At most one charge line per student is created or request is rejected deterministically |
| CR-010 | P1 | Student becomes inactive between preview and submit | Server re-resolves audience and reports exclusion/conflict; it does not charge silently |
| CR-011 | P1 | Large all-scope batch at supported maximum | Completes atomically within performance budget; progress and final count are accurate |
| CR-012 | P1 | Batch exceeds supported maximum | Request is rejected before partial line creation |
| CR-013 | P0 | Double-click Create or retry same request | Only one batch is created; duplicate submission is prevented or idempotently replayed |
| CR-014 | P1 | Switch board/medium/class after selecting students | Invalid selections are cleared; hidden students are not submitted |
| CR-015 | P1 | Search students by name/admission/roll | Matching scoped students appear; short/no-result searches behave consistently |

### 3. Field validation and boundary cases

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| CV-001 | P0 | Blank charge name/category/purpose/due date | Field-level validation prevents submission |
| CV-002 | P0 | Amount `0`, negative, whitespace, alphabetic, or symbols | Rejected by UI and API |
| CV-003 | P0 | Amount `0.01` | Accepted |
| CV-004 | P0 | Amount `9999999.00` | Accepted |
| CV-005 | P0 | Amount `9999999.01` | Rejected |
| CV-006 | P0 | Amount with three decimals such as `10.999` | Rejected, not silently rounded |
| CV-007 | P1 | Leading zeros and values `10`, `10.5`, `10.50` | Normalized consistently and stored as exact decimal value |
| CV-008 | P0 | Due date before academic-year start or after end | Rejected in UI and API |
| CV-009 | P1 | Due date at academic-year boundaries | Start and end dates are accepted according to the inclusive rule |
| CV-010 | P1 | Leap day, locale input, and timezone boundary | Stored date does not shift by one day |
| CV-011 | P1 | Name/purpose min, max, max+1, Unicode, emoji, HTML, and script text | Limits are enforced; output is encoded and not executable |
| CV-012 | P0 | Missing or stale accounting mapping | Request is rejected before line creation |

### 4. Review, update, approval, and rejection

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| AP-001 | P0 | Different checker approves all pending lines | Lines become Approved; batch moves to Processed; actor/time/notes are captured |
| AP-002 | P0 | Author attempts self-approval | Backend rejects maker-checker violation even through direct API |
| AP-003 | P0 | Approve selected subset in a multi-student batch | Only selected pending lines change; derived batch status and counts are correct |
| AP-004 | P0 | Reject selected subset with reason | Only selected lines are Rejected; reason and actor are immutable |
| AP-005 | P0 | Reject without required reason | UI and API reject the action |
| AP-006 | P0 | Approve/reject an already processed line | Illegal transition is rejected without duplicate events |
| AP-007 | P0 | Update wholly pending batch with current concurrency stamp | Header and all pending lines update consistently |
| AP-008 | P0 | Update mixed or processed batch | Update is rejected; approved/rejected line snapshots do not drift |
| AP-009 | P0 | Submit stale concurrency stamp from second browser/session | Conflict is shown; newer data is not overwritten |
| AP-010 | P0 | Delete wholly pending batch | Deleted once with audit; no charge remains collectible |
| AP-011 | P0 | Delete approved, rejected, paid, or mixed batch | Deletion is rejected; historical lines remain available |
| AP-012 | P1 | Checker lacks Approve but has View | Approve is absent/disabled and direct API returns 403 |
| AP-013 | P1 | Search, sort, status tabs, paging | Server results, badge counts, rows, and URL/filter state agree |
| AP-014 | P1 | Refresh/back navigation during form or review | No duplicate submission; dirty state and selected lines behave predictably |

### 5. Void and correction before payment

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| VD-001 | P0 | Void approved unpaid line with valid reason | Line becomes Voided; outstanding becomes zero; audit records actor/reason |
| VD-002 | P0 | Void selected subset of approved unpaid batch | Only selected lines become Voided; other lines remain collectible |
| VD-003 | P0 | Void pending or rejected line | Rejected as an illegal transition |
| VD-004 | P0 | Void collected or partially collected line | Rejected with guidance to use reversal/refund workflow |
| VD-005 | P0 | Void without reason or permission | UI blocks or backend returns validation/403; no state changes |
| VD-006 | P1 | Retry same void command | No duplicate audit/event or balance mutation |

### 6. Fee Collection integration

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| FC-001 | P0 | Open summary for student with approved charge | Total Charges and Pending Charges include only approved effective charge lines |
| FC-002 | P0 | Pending/rejected/voided category charge | It is not collectible and is excluded from pending charge balance |
| FC-003 | P0 | Open collect form for Student B with charge only | Approved charge appears without requiring scheduled fees |
| FC-004 | P0 | Collect full charge with cash | Outstanding becomes zero; collection is Paid; provisional charge receipt is created |
| FC-005 | P0 | Collect part of charge | Paid and pending amounts update exactly; status becomes Partial |
| FC-006 | P0 | Complete remaining partial charge | Pending becomes zero without duplicating the first allocation |
| FC-007 | P0 | Collect scheduled fee and charge together | One transaction may produce separate Fee and Charge receipt scopes; sums reconcile |
| FC-008 | P0 | Charge-only request sends empty due-fee list | Valid charge collection succeeds |
| FC-009 | P0 | Amount exceeds charge outstanding by `0.01` | Entire transaction is rejected; no receipt/allocation/fee mutation is written |
| FC-010 | P0 | Zero/negative charge allocation | Validator rejects it |
| FC-011 | P0 | Duplicate ChargeId in payload | Validator rejects duplicate allocation rows |
| FC-012 | P0 | Charge belongs to another student/year/tenant | Backend rejects InvalidCharge; no information leakage |
| FC-013 | P0 | Charge changes state between form load and payment | Server revalidates Approved state and current outstanding inside transaction |
| FC-014 | P0 | Two collectors pay same final outstanding concurrently | At most one succeeds for that amount; no over-collection |
| FC-015 | P0 | Retry same idempotency key and same payload | Original successful response is returned; no second receipt or allocation |
| FC-016 | P0 | Reuse idempotency key with different payload | Rejected as key/payload mismatch |
| FC-017 | P0 | Cash, UPI, card, bank transfer, cheque, demand draft | Required details and final/provisional/reconciliation status follow payment rules |
| FC-018 | P0 | Online method when policy disallows online collection | Backend rejects even if client is bypassed; allowed office method remains usable |
| FC-019 | P1 | Split payment methods whose total equals allocations | Succeeds; method records and receipt total reconcile |
| FC-020 | P0 | Split method total differs from fee+late-fee+charge allocations | Entire request is rejected |
| FC-021 | P1 | Future scheduled fee plus current charge | Explicit selections are honored; allocation ordering is visible and deterministic |
| FC-022 | P1 | Search/filter/export after charge payment | Summary totals and export reflect payment without stale cache |

### 7. Receipts, reconciliation, reversal, and refunds

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| RR-001 | P0 | Charge-only successful collection | Charge receipt has unique number, student/year, charge name, allocation, method, amount, and transaction link |
| RR-002 | P0 | Mixed fee+charge collection | Fee and charge receipts are separate rows/scopes and share the correct transaction relationship |
| RR-003 | P0 | Generate/preview/download provisional charge receipt | UI and PDF values equal stored allocations; repeated generation is stable/cached |
| RR-004 | P0 | Reverse a charge collection in reconciliation | Reversed allocation stops counting as paid; charge outstanding is reinstated exactly once |
| RR-005 | P0 | Reopen collect form after reversal | Reinstated amount is collectible and original/reversal evidence remains linked |
| RR-006 | P0 | Retry or concurrently reverse same transaction | One reversal is recorded; balance is not reinstated twice |
| RR-007 | P1 | Partial mixed transaction reversal | Product rule is explicit; affected fee and charge allocations reconcile correctly |
| RR-008 | P0 | Attempt standard Fee Refund against charge receipt | UI/API clearly reports charge refund as unsupported rather than refunding wrong scope |
| RR-009 | P1 | Paid charge correction/refund target workflow | Currently `NOT SUPPORTED`; implement linked charge refund/correction before release if required |
| RR-010 | P1 | Receipt list filter by Charge kind and month | Only matching charge receipts appear; paging and counts are correct |
| RR-011 | P1 | Receipt viewed after category rename/deactivation | Historical snapshotted category/account/name remains reproducible |

### 8. Accounting, audit, reporting, and notifications

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| AR-001 | P0 | Approve mapped charge | Charge line retains snapshotted Income Category and Account Head IDs/names |
| AR-002 | P0 | Collect charge | Required accounting transaction/journal is produced or the system explicitly reports the current unsupported gap |
| AR-003 | P0 | Reverse collected charge | Accounting reversal is equal and opposite and links to the original transaction |
| AR-004 | P0 | Review audit trail | Create/update/approve/reject/void/collect/reverse show actor, time, reason, entity, before/after, and correlation |
| AR-005 | P1 | Daily collection and student ledger reports | Charge principal, paid, reversed, and outstanding values reconcile with source allocations |
| AR-006 | P1 | Charge/category/collection exports | Filters, row counts, decimal values, statuses, and totals match UI/API |
| AR-007 | P1 | Approved/collected event delivery retry | Event/outbox emits once after commit; no duplicate notification or posting |
| AR-008 | P2 | User notification for approval/rejection/payment | Delivered only to scoped intended recipients with source link and no sensitive overexposure |

### 9. Security and isolation

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| SE-001 | P0 | Anonymous calls every charge/category/collection endpoint | 401 |
| SE-002 | P0 | Authenticated user lacks exact action permission | 403 and no mutation; UI action is absent/disabled |
| SE-003 | P0 | Cross-tenant IDs supplied in list/read/mutation | Not found or forbidden without revealing foreign data |
| SE-004 | P0 | Cross-year batch/line/category/student supplied | Rejected; selected academic year remains authoritative |
| SE-005 | P0 | Out-of-scope board/medium/class/division/student | Empty/forbidden according to contract; never returned or mutated |
| SE-006 | P1 | Manipulated status, creator, amount, mapping, or collected fields | Server ignores non-contract fields and protects server-owned state |
| SE-007 | P1 | HTML/script/formula injection in name/reason/export | Encoded in UI/PDF; CSV/Excel export prevents formula execution |
| SE-008 | P1 | Excessive page size, search length, and malformed GUIDs | Bounded validation response; no unbounded query or 500 error |

### 10. Reliability, usability, and accessibility

| ID | Pri | Scenario | Expected result |
|---|---:|---|---|
| UX-001 | P1 | Loading, empty, no-results, and API-error states | Distinct, actionable states without blank or misleading tables |
| UX-002 | P1 | Slow create/approve/collect response | Submit disabled while pending; progress shown; no duplicate mutation |
| UX-003 | P1 | Backend business error | Stable message explains corrective action and preserves entered data |
| UX-004 | P1 | Keyboard-only category, student, review, and payment flow | Focus order, labels, dialogs, and actions are operable |
| UX-005 | P1 | Mobile/tablet/desktop widths | No hidden critical amount/action; tables and dialogs remain usable |
| UX-006 | P1 | Browser refresh during pending operation | Result can be recovered by idempotency key/list refresh; no duplicate |
| UX-007 | P2 | Large batch/table performance | Filtering, paging, detail, and export meet agreed response budgets |
| UX-008 | P1 | Timezone and currency formatting | Dates and INR decimals are consistent between form, list, receipt, and export |

## Automated coverage review

Existing useful coverage includes:

- charge amount validation, audience resolution, maker-checker, basic create/update/delete, filtering, and status transitions;
- fee collection validation, due-fee collection, payment methods, receipt number uniqueness, and late-fee behavior;
- receipt list/detail/PDF behavior including provisional charge receipt shape;
- reconciliation, refunds, audit, and receipt-number concurrency in their own bounded suites;
- frontend charge amount validation, active-category request contract, and payment allocation helpers.

The most important missing automated journeys are:

| Gap ID | Pri | Missing automated proof | Required test |
|---|---:|---|---|
| TG-001 | P0 | Charge creation through actual Fee Collection | Create approved charge, collect full/partial, assert ChargeCollectionRecord, balances, and receipt |
| TG-002 | P0 | Charge overpayment/concurrent final payment | Parallel collection commands cannot exceed outstanding |
| TG-003 | P0 | Charge collection idempotent replay | Same key/payload returns original charge receipt; mismatch is rejected |
| TG-004 | P0 | Charge reversal reinstates outstanding | Collect, reconcile/reverse, reload collect form, assert exact reinstatement |
| TG-005 | P0 | Inactive category backend enforcement | Deactivate category, exclude from lookup, and reject direct create |
| TG-006 | P0 | Exact permission denial across every action | UI and API matrix for Add/Update/Delete/Approve/Reject/Void/ChangeStatus/Collect |
| TG-007 | P0 | Tenant/year/student-scope isolation for every endpoint | Foreign identifiers never read or mutate data |
| TG-008 | P1 | Mixed fee+charge receipt reconciliation | One transaction yields correct separate receipt scopes and totals |
| TG-009 | P1 | Category/account snapshot history | Rename/deactivate mapping after approval and prove old receipt/report stability |
| TG-010 | P1 | Browser maker-checker journey | Author creates, second user approves, collector pays, auditor verifies |
| TG-011 | P1 | Browser error/retry/accessibility journey | Validation, stale write, duplicate submit, keyboard, and responsive coverage |
| TG-012 | P1 | Charge list aggregate test | Restore the currently commented paginated/status-count test and expand mixed-status assertions |
| TG-013 | P0 | Charge/Fee Collection API fixture cannot create students | Replace the obsolete registration helper route or seed students through a supported test service, rebuild, and require the full selection to pass |

## Missing product or implementation capabilities

These are not ordinary QA failures; they require implementation or a signed-off product decision.

| Gap ID | Pri | Current gap | Release condition |
|---|---:|---|---|
| PG-001 | P0 | Local `admin-ddvs` lacks Charge Category ChangeStatus | Provide a dedicated category-admin test user or grant the exact permission |
| PG-002 | P0 | Local tenant lacks seeded charge/collection rows for browser E2E | Seed disposable students, fee assignment, categories, receipt book, and cleanup strategy |
| PG-003 | P0 | No single automated charge-to-collection-to-reversal test | Add an API integration test spanning the complete monetary lifecycle |
| PG-004 | P1 | Paid charge refund/correction is unsupported | Add explicit linked charge refund/correction or declare it unsupported in UI/API |
| PG-005 | P1 | Automatic ledger posting/reversal is not proven end to end | Implement/verify balanced postings using snapshotted account mapping |
| PG-006 | P1 | Source-module origin and replay contract is incomplete | Persist unique source identity, correlation, and idempotency for Transport/Library/Hostel |
| PG-007 | P1 | Transactional outbox/notification exactly-once delivery is incomplete | Publish lifecycle events only after commit with idempotent consumers |
| PG-008 | P1 | Charge reports and exports lack allocation/reversal parity proof | Reconcile reports to immutable charge lines and collection records |
| PG-009 | P1 | Category history/concurrency/dependency behavior needs complete coverage | Add stamps, immutable mapping history, and dependency tests |
| PG-010 | P2 | Frontend charge/collection contracts still contain broad `any` types and duplicate surfaces | Consolidate the routed workflow and use generated/typed API contracts |
| PG-011 | P2 | Full immutable charge timeline is not visible in one place | Show origin, author, checker, collection, receipt, void/reversal, and audit links |
| PG-012 | P0 | Existing API suites fail during fixture setup before reaching Finance assertions | Repair the shared student-creation fixture and eliminate the ten-minute setup timeout |

## Recommended execution order

1. Fix PG-001 and PG-002 in a disposable tenant.
2. Run CC-001 through CC-012 and prove inactive categories cannot be charged.
3. Run CR/CV scenarios with the author account.
4. Sign out and use the checker account for AP and VD scenarios.
5. Use the collector account for FC scenarios, starting with charge-only cash before mixed and online methods.
6. Verify receipts immediately after each successful payment.
7. Run reconciliation/reversal and confirm outstanding reinstatement.
8. Verify audit, account, report, and export parity with the auditor account.
9. Run concurrency/idempotency tests last against fresh records.
10. Archive or remove only disposable pending test data; preserve paid/reversed audit history according to retention rules.

## Evidence required for each executed scenario

Record:

- scenario ID, environment/build/commit, tenant, academic year, persona, and timestamp;
- IDs for category, charge request, charge line, student, collection transaction, receipts, audit events, and reversal;
- request/response or browser evidence with secrets and personal data redacted;
- before/after balances and the reconciliation calculation;
- PASS/FAIL/BLOCKED status, defect ID, and retest result.

For money-changing scenarios, a toast or HTTP 200 alone is insufficient. Verify persisted balance, allocation, receipt, audit, and downstream projection.

## Exit criteria

Charge Management is ready for release only when:

- every P0 scenario passes in API integration tests and the core browser journey;
- no user can approve their own request or bypass exact permissions/scope;
- inactive/unmapped categories cannot create new charges;
- charge outstanding cannot become negative under retries or concurrency;
- full, partial, mixed, reversed, and unsupported-refund behavior is explicit and reconciled;
- receipt and audit evidence exists for every monetary transition;
- all P0 test gaps are automated or have a time-bounded approved exception;
- Finance docs validation and repository diff checks pass.

## Suggested focused commands

```powershell
dotnet test test/ShikshaMitra.HttpApi.Tests --filter "FullyQualifiedName~Charge|FullyQualifiedName~FeeCollection|FullyQualifiedName~FeeReceipt|FullyQualifiedName~Reconciliation"
dotnet test test/ShikshaMitra.Application.Tests --filter "FullyQualifiedName~Charge|FullyQualifiedName~FeeCollection"
dotnet test test/ShikshaMitra.Domain.Tests --filter "FullyQualifiedName~Charge|FullyQualifiedName~Receipt"
powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module finance
git diff --check
```

Frontend focused checks:

```powershell
npx vitest run src/redux/finance/chargeCategory.test.ts src/screens/finance/ChargesManagement/Components/UpdateChargeForm/UpdateChargeValidation.test.ts src/screens/finance/FeeCollection/FeeCollectionComponent/preview/paymentAllocationUtils.test.ts
```
