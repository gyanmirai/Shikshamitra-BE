---
module: finance
domain: engineering-constitution
content_type: Conceptual
status: canonical
updated_on: 2026-08-31
owner: product-and-engineering
audience: humans-and-agents
---

# Finance Engineering Constitution

## Authority and scope

This is the mandatory decision framework for ShikshaMitra Finance. Read it before planning, implementing, reviewing, migrating, or testing any financial behavior.

It applies to Finance-owned code and to any cross-module flow that creates, prices, schedules, discounts, waives, collects, allocates, refunds, reverses, posts, reconciles, reports, exports, or displays money. That includes relevant work in Students, Admission, Transport, Hostel, Library, HR/Payroll, Dashboards, Notifications, and Reports.

Backend Finance product docs and source code describe current behavior. This constitution governs the quality and safety of new or changed behavior. If existing code conflicts with it, preserve unrelated production behavior, record the conflict as migration debt, and correct the touched path with an explicit migration and tests. Never copy a conflict as precedent.

Normative words have their usual meaning:

- **MUST / MUST NOT**: required for merge.
- **SHOULD / SHOULD NOT**: expected unless the change records a concrete reason.
- **MAY**: optional.

## Vision

Make ShikshaMitra Finance the most trustworthy, explainable, configurable, and operationally complete finance system for schools: every amount has an origin, every rule has an owner and effective period, every action has the correct authority, and every reported figure can be reconciled to immutable evidence.

## Mission

Build coherent end-to-end financial journeys for schools, staff, students, and families by:

- turning school policies into versioned, testable, effective-dated rules;
- maintaining one auditable chain from setup to obligation, settlement, accounting, reconciliation, and reporting;
- enforcing tenant, academic, data-scope, and action-level authorization on the server;
- making every field, status, calculation, exception, and correction understandable;
- integrating other modules through explicit contracts without losing Finance ownership of monetary truth;
- preventing silent data corruption through transactional writes, idempotency, concurrency control, and reconciliation.

## Non-negotiable principles

1. **Correctness before convenience.** Incorrect balances, receipts, approvals, or reports are production defects.
2. **One meaning per field.** A field cannot be a label in one flow and an accounting fact in another.
3. **One source of truth per fact.** Do not independently calculate the same amount in UI, API, report, and export.
4. **Configuration drives behavior.** Do not hide school policy in UI defaults, magic numbers, seed data, or controller branches.
5. **History is append-only in effect.** Posted or approved financial history is corrected through reversal, credit/debit adjustment, refund, or superseding version, not destructive mutation.
6. **Server authority is final.** The backend revalidates money, scope, permissions, state, and policy; UI guards are usability controls, not security.
7. **Maker and checker are separate.** A creator cannot approve, reject, reconcile, waive, or refund their own request wherever separation of duties applies.
8. **Least privilege is end to end.** Menu visibility, routes, buttons, API methods, exports, and data rows all enforce the same permission intent.
9. **Every side effect is atomic or recoverable.** Partial updates to dues, allocations, receipts, postings, audit, or events are forbidden.
10. **Explainability is a feature.** A user must be able to understand how a balance was produced and who changed it.
11. **Time and version matter.** Financial behavior uses the policy/configuration effective for the transaction, not whichever record is current today.
12. **Cross-module convenience never bypasses Finance.** Other modules request financial outcomes through contracts; they do not write Finance-owned tables directly.

## Domain map and ownership

| Area | Owns | Must not own |
|---|---|---|
| Fee setup | Fee categories, fee heads, payment terms, receipt books, fee structures, policy attachments | Student identity or academic hierarchy definitions |
| Student fee assignment | Assignment snapshot and generation of obligations | Enrollment admission decisions |
| Dues and collection | Obligations, allocations, payment evidence, collection status | Bank settlement confirmation |
| Charges | Ad hoc charge request, audience snapshot, approval, resulting student charges | Library/Transport/Hostel operational reason source |
| Concessions and waivers | Eligibility, evidence, approval, allocation, policy limits | Cash return |
| Adjustments | Non-cash credits/reallocation between eligible obligations | Parent refund or bank payout |
| Refunds | Cash-return request, approval, payout state, linkage to refundable payment | Rewriting the original receipt |
| Receipts | Immutable numbered evidence of accepted payment/reversal linkage | Policy calculation |
| Accounts | Income/expense categories, bank accounts, postings, transactions | Student academic data |
| Reconciliation | Match/exception/approval between internal evidence and external settlement | Original collection mutation |
| Audit | Actor, action, reason, before/after context, correlation | Business workflow state itself |
| Reports | Read models over canonical operational/accounting facts | Independent business calculations |

## Canonical dependency chain

All implementation plans MUST trace the affected links in this chain:

`Tenant + academic year + academic scope` -> `categories/heads/accounts` -> `terms/books/policies` -> `fee structure/version` -> `student assignment` -> `due/charge` -> `collection/concession/waiver/adjustment/refund` -> `receipt/posting` -> `reconciliation` -> `audit/report/notification`

A change at any link MUST identify downstream consumers. For example, changing a fee head can affect structures, policy applicability, dues, receipt labels, account heads, reports, exports, and integrations.

## Mandatory flow contract before implementation

No financial flow begins implementation until the change answers these questions in its issue, plan, PR, or test notes:

| Question | Required answer |
|---|---|
| Business outcome | Who needs what outcome, and why? |
| Owner | Which bounded context owns the rule and stored fact? |
| Actor | Which roles initiate, view, edit, approve, reject, cancel, reverse, export, and reconcile? |
| Scope | Tenant, academic year, board, medium, stage, class, division, student, employee, and/or account scope |
| Preconditions | Required setup, active status, effective version, balance, evidence, and prior state |
| State machine | Allowed states, transitions, terminal states, and actor for each transition |
| Monetary equation | Inputs, rounding, allocation order, caps, taxes/fees, and expected totals |
| Policy resolution | Exact precedence and the effective-dated/versioned snapshot used |
| Side effects | Dues, receipts, postings, audit events, notifications, exports, and integration events |
| Failure behavior | Atomic rollback, retry/idempotency behavior, user-safe error, and recovery path |
| Correction behavior | Cancel, reverse, refund, adjust, void, or supersede; never an ambiguous edit |
| Permission map | Exact backend permission for each action plus frontend route/action mapping |
| Evidence | Tests and reconciliation proof required to accept the change |

If any answer is unknown and affects money, authority, scope, or history, stop and resolve it before coding.

## Settings and policy architecture

### Every setting must be operationally complete

Every new or changed Finance setting MUST define:

- tenant and academic-year ownership;
- optional academic scope and its `NULL = ALL` semantics where applicable;
- stable ID, unique business code where humans/integrations need one, name, and description;
- status lifecycle (`Draft`, `Active/Published`, `Archived`, or a deliberately documented alternative);
- effective-from/effective-to dates when behavior can change over time;
- default-selection rules and uniqueness scope;
- dependency/use checks before archive or replacement;
- editability rules once used downstream;
- clone/revision behavior when direct edit is unsafe;
- permission for each supported action;
- audit event and mandatory reason for high-impact changes;
- migration/backfill and compatibility behavior for existing records.

Deleting a referenced setting is forbidden. Prefer archive/deactivate, and explain impact to the user. If a setting is safe to delete only while unused, the backend MUST prove it is unused inside the transaction.

### Policy lifecycle

A Finance policy is not merely a form record. It MUST have:

1. a defined policy type and owner;
2. draft creation and validation;
3. deterministic simulation or impact preview for calculation policies;
4. publish/activate authority distinct from ordinary editing when risk warrants it;
5. immutable version snapshots;
6. effective dates and overlap/gap rules;
7. assignment and resolution precedence;
8. exception/override rules with tighter permissions, reason, expiry, and audit;
9. downstream snapshotting so historical transactions retain the applied version;
10. archive/supersede behavior that does not rewrite past transactions.

Policy resolution MUST be deterministic and tested. The expected precedence is explicitly documented per policy, for example: student override -> fee-structure attachment -> scoped policy -> academic-year default -> documented safe fallback. Never invent a fallback inside a UI component or unrelated service.

Collection policy currently owns late-fee and waiver behavior. Payment terms own the installment schedule, not partial-payment or reminder policy. Keep those meanings separate.

## Field contract: every field must earn its place

Before adding or changing a field, document this mini-contract:

| Property | Meaning |
|---|---|
| Business meaning | The single real-world fact represented |
| Source/owner | User, policy, derived calculation, integration, or system clock |
| Type/unit | Money/currency, percentage, count, date, timestamp, enum, identifier, text |
| Required/default | Why it may be absent and what a default means |
| Validation | Range, format, cross-field and database rules |
| Editability | Which states and permissions allow changes |
| Downstream use | Calculations, reports, receipts, postings, notifications, integrations |
| Sensitivity | Masking, encryption, logging, export, and retention rules |
| Audit | Whether before/after value and reason are recorded |

Mandatory type rules:

- New money fields MUST use `decimal`, with explicit precision/scale and currency. Do not introduce `double` for money.
- Percentages MUST state whether `10` means ten percent or `0.10`, define valid range, and define rounding order.
- Payment/event instants MUST use `DateTimeOffset` and UTC-compatible ISO 8601 contracts. Date-only policy boundaries and due dates MUST be deliberately modelled as date semantics.
- Payment methods and statuses MUST be shared enums/contracts, never magic integers or display strings.
- Identifiers MUST be server-validated for tenant, type, active/effective status, and user scope; existence alone is insufficient.
- Nullable values MUST mean something explicit. Never use null interchangeably for unknown, not applicable, all, and zero.
- Free-text reasons MUST have a defined purpose, length, sanitization, and audit visibility.
- Bank account and payment reference data MUST be masked in UI/logs and protected according to sensitivity.
- Derived fields MUST be labelled derived/read-only and calculated by one canonical service.

## Ledger and balance invariants

Every monetary mutation MUST state and test its accounting equation. At minimum:

- original obligation, paid amount, concession/waiver amount, adjustment credit, refund amount, reversal amount, late fee, and outstanding amount remain distinct facts;
- allocations are positive, use one currency, and their sum equals the approved header amount exactly after rounding;
- no allocation exceeds its eligible source balance or target outstanding balance;
- a payment cannot exceed the allowed collectible amount unless an explicit advance/credit-balance design exists;
- a concession cannot create cash and a refund cannot masquerade as an adjustment;
- a refund cannot exceed the refundable paid balance after prior refunds/reversals;
- the same benefit or payment cannot be applied twice;
- generated due rows use a stable uniqueness/idempotency key;
- receipt numbers are unique within their defined tenant/book/year scope and generated safely under concurrency;
- reconciliation never mutates the original collection to force a match;
- report totals reconcile to the same canonical rows used by operational screens.

Rounding MUST be centralized, deterministic, and tested at line and total levels. Any remainder allocation rule (for example, last eligible line) MUST be documented and stable.

## Workflow and state-machine rules

All financial aggregates MUST expose named domain transitions. Public setters or generic status assignment are not acceptable for new code.

Each transition MUST enforce:

- current state and allowed target state;
- actor permission and data scope;
- maker-checker separation where required;
- active/effective configuration and policy version;
- monetary and evidence preconditions;
- concurrency token/version;
- mandatory reason for reject, cancel, archive, override, waiver, reversal, or manual match;
- atomic persistence of balance mutations, workflow state, receipt/posting, and audit intent;
- idempotent retry behavior for commands that may be repeated;
- a domain or integration event only after the operation succeeds.

Approved, posted, paid, reconciled, refunded, or otherwise terminal records MUST NOT return to an earlier state by ordinary edit. Use a named correction flow with linked evidence.

Bulk operations MUST provide per-item results, preserve atomicity at the documented boundary, prevent cross-tenant/scope access, and be safe to retry.

## Authorization, roles, and data scope

### Backend is the permission source of truth

- Every public Finance application-service/API method MUST have an exact action permission, not only `[Authorize]` or a broad module permission.
- Read, create, edit, delete/archive, submit, approve, reject, cancel, waive, override, refund, reverse, reconcile, export, and audit-view are distinct actions when their risk differs.
- Approval and rejection MUST NOT reuse a generic `Add` permission.
- High-value or exceptional actions MAY require an additional threshold permission.
- Controllers remain thin and cannot weaken application-service authorization.
- Every ID-based method MUST perform fine-grained tenant and user-scope/IDOR checks.
- List, count, dashboard, search, report, preview, and export queries MUST apply the same row scope at database level.

### Frontend parity is required

- Permission names MUST come from a canonical mapping that matches backend constants exactly.
- Menu visibility, route/layout guard, screen query, action button, and mutation call MUST use the permission appropriate to that action.
- Hidden buttons are not authorization. The API must reject unauthorized calls.
- A parent permission MUST NOT accidentally grant a child action.
- A page MUST render a deliberate forbidden/empty/loading/error state, never leak data before permission resolution.

### Roles are bundles, not code branches

Recommended school roles can bundle permissions, such as tenant administrator, finance administrator, accountant/cashier, fee manager, approver/principal, auditor, and read-only viewer, but production code MUST authorize by permission and scope, not by role-name string. Tenant-specific grants determine what a role can do; UserScope determines which rows it can access.

Permission changes MUST include parity tests covering backend definition, seeded/granted permission behavior, frontend constant, menu/route guard, action visibility, and denied API calls.

## API and validation contract

- DTOs state field meaning and use shared enums; backend contracts are canonical.
- FluentValidation covers shape and cross-field input rules; domain methods enforce invariants again.
- Business failures use stable `BusinessException` codes from Domain.Shared and localized messages.
- APIs never accept caller-computed totals as truth. Recalculate from canonical lines and compare when useful.
- Update contracts distinguish omitted, cleared, and unchanged values.
- Pagination, sorting, filtering, as-of date, timezone, currency, and academic year are explicit.
- Mutating commands that can be retried accept or derive an idempotency key.
- Concurrency conflicts are reported as conflicts, not overwritten silently.
- Backward-compatible aliases belong in a mapper and have a removal plan; screens do not duplicate contract repair logic.
- Sensitive payment/bank data, tokens, documents, and internal exception detail never appear in logs or client errors.

## User experience rules

Every financial screen MUST help a user make a safe decision:

- show the academic year, student/account context, currency, status, and effective policy/version;
- show a transparent breakdown: original, late fee, concession/waiver, paid, adjusted, refunded, and outstanding as applicable;
- explain disabled actions and unmet setup/preconditions;
- validate early for usability but rely on backend validation for truth;
- prevent double submission and show idempotent retry outcomes;
- require confirmation plus reason for destructive/high-impact actions;
- distinguish save draft, submit, approve, post, collect, reconcile, reverse, and refund;
- display immutable receipt/reference numbers and correction links;
- preserve accessible labels, keyboard flow, focus, error summaries, and printable/downloadable evidence;
- never use dummy JSON or hardcoded financial values on an active production route.

## Audit, observability, and evidence

Every high-impact action MUST produce an auditable event containing, as applicable:

- tenant, academic year, submodule, entity type/id, student/account reference;
- normalized action and previous/new status;
- actor ID and role snapshot;
- occurred-at timestamp;
- reason;
- safe before/after changed fields or calculation summary;
- policy/configuration version IDs;
- correlation and idempotency identifiers;
- related receipt, payment, posting, refund, adjustment, or reconciliation IDs.

Audit is append-only and permission-protected. Do not place full sensitive bank data, uploaded documents, secrets, or unnecessary personal data in audit JSON.

Operational logs and metrics SHOULD cover command success/failure, retries, concurrency conflicts, receipt sequence exhaustion, posting/event failures, unmatched reconciliation totals, and report reconciliation drift without leaking financial secrets.

## Reporting and reconciliation

- Preview, export, dashboard, and scheduled output MUST use the same filtered query/calculation pipeline.
- Every report defines grain, included statuses, amount columns, sign convention, timezone, currency, academic-year rule, and as-of semantics.
- Report access applies both action permission and row scope.
- Totals MUST reconcile to canonical operational/accounting facts; report-only patches are forbidden.
- Reconciliation records external reference, internal reference, match method, discrepancy, actor, reason, and approval trail.
- Manual match/override requires a dedicated permission and reason.
- Closed-period behavior must be explicit before period locking or accounting close is implemented.

## Cross-module integration rules

| Integration | Rule |
|---|---|
| Students / Enrollment / Admission | Student identity and academic placement are source facts. Finance owns fee assignment, generated dues, payments, and finance handoff status. Enrollment changes must trigger an explicit impact/reassignment workflow rather than silently rewriting dues. |
| Transport | Transport owns route/stop/service pricing inputs; approved billing enters Finance through a charge/fee contract with a stable external reference and idempotency key. |
| Hostel | Hostel owns accommodation/service facts. Hostel fees remain separate unless an explicit Finance adapter maps them to canonical heads, dues, receipts, and reports. |
| Library | Library owns fine reasons and source events. Finance settlement is not assumed until an explicit charge/payment mapping exists. |
| HR / Payroll | HR owns compensation and payroll calculation. Finance/Accounts owns any approved posting/payment representation. Integration must use a versioned event/contract and never let payroll write Finance tables directly. |
| Dashboard / Notifications | These are projections/consumers. They cannot invent statuses or totals and must honor permission and row scope. |
| Reports / Exports | These consume canonical Finance read models and never reimplement business rules. |

Cross-module contracts MUST be versioned, tenant-aware, idempotent, traceable by correlation ID, and explicit about ownership. Consumers must tolerate duplicate delivery. A failure after the Finance transaction commits must be retryable; use the project event/outbox pattern rather than an unsafe distributed transaction.

## Implementation architecture

- Follow ABP DDD layering and the repository folder conventions in `docs/best-practices.md`.
- Put invariants and state transitions in aggregates/domain services, orchestration in application services, persistence in repositories, and HTTP mapping in thin controllers/mappers.
- Use a transaction boundary that covers all local financial mutations.
- Use database constraints and unique indexes for business uniqueness and idempotency, not application checks alone.
- Use optimistic concurrency for mutable requests/settings and safe sequence generation for receipts.
- Cache only reference/read data with tenant-aware keys and write-path invalidation. Never cache authorization scope or balances unsafely.
- Publish versioned ETOs after successful persistence; consumers are idempotent.
- Schema changes include precision, indexes, foreign keys, tenant/academic ownership, backfill, rollback/forward-fix plan, and production-volume impact.

## Required tests and proof

Every change selects tests according to risk; monetary or authorization changes cannot be accepted with UI-only proof.

| Risk | Minimum proof |
|---|---|
| Calculation/invariant | Domain unit tests with boundary, zero/negative, rounding, cap, and allocation cases |
| State transition | Domain and application tests for every allowed and rejected transition |
| API contract | HTTP tests for success, validation, stable error codes, and concurrency/idempotent retry |
| Permission | Allowed and denied tests per action, maker-checker, threshold permission, and IDOR |
| Tenant/scope | Cross-tenant denial plus board/medium/stage/class/division/student row filtering |
| Persistence | EF/unique constraint, transaction rollback, migration/backfill, and sequence concurrency tests |
| Frontend | Mapper/client contract, cache invalidation, guard/action visibility, validation, and error-state tests |
| End to end | Setup -> policy/version -> assignment -> obligation -> action -> receipt/posting -> reconciliation/report |
| Reports | Preview/export parity and totals reconciled to seeded canonical rows |
| Integration event | Published payload, duplicate delivery, retry, and consumer idempotency |

Run the focused Finance suites from [testing.md](../testing.md), relevant frontend checks from the [Finance frontend testing guide](../../../../../Shikshamitra-FE/docs/finance/testing.md), docs validators, type/build checks, and `git diff --check`. Run a wider suite when shared contracts, permissions, migrations, or cross-module behavior change.

## Definition of done

A Finance change is complete only when all applicable statements are true:

- [ ] The mandatory flow contract is answered.
- [ ] Field meanings, ownership, units, null/default behavior, sensitivity, and downstream effects are documented.
- [ ] Settings/policies have lifecycle, version/effective-date, default, dependency, and archive behavior.
- [ ] State transitions and monetary equations are enforced in the domain.
- [ ] Writes are transactional, concurrency-safe, and idempotent at the required boundary.
- [ ] Posted history is preserved and a correction/reversal path exists.
- [ ] Backend action permissions, tenant isolation, UserScope/IDOR, and maker-checker rules are enforced.
- [ ] Frontend menu, route, page, action, and client permissions match backend constants.
- [ ] Audit, reason, correlation, receipts/postings, events, reports, and notifications are updated as applicable.
- [ ] Cross-module ownership and failure/retry behavior are explicit.
- [ ] Domain, application, API, permission, scope, frontend, concurrency, and E2E tests pass in proportion to risk.
- [ ] Migrations/backfills are safe for existing tenants and historical data.
- [ ] Canonical backend and frontend Finance docs reflect the shipped behavior.
- [ ] No dummy data, magic financial value, permission alias, or unexplained fallback was introduced.

## Stop conditions

Do not implement or merge when any of these is unresolved:

- the source of a monetary fact is ambiguous;
- the balance equation or rounding rule is missing;
- a policy would mutate history instead of versioning/snapshotting;
- an action lacks an exact backend permission or frontend parity;
- tenant or row-scope enforcement is absent;
- maker-checker can be bypassed;
- retries can duplicate charges, dues, payments, receipts, refunds, or postings;
- a cross-module flow writes another module's owned data directly;
- a migration can orphan or reinterpret existing financial records;
- a report/export uses different logic from the operational source;
- failure can leave partially updated balances or evidence;
- the change has no test capable of detecting financial corruption.

## Reviewed baseline: do not copy these patterns

The August 2026 repository review found legacy/partial patterns that this constitution intentionally supersedes for new work:

- some frontend policy routes alias Fee Collection permissions instead of the dedicated backend `Finance.Policies.*` families;
- some frontend permission aliases map one feature to another feature's permission (for example, Account Heads to Expense Category);
- some backend services rely on coarse `[Authorize]`, and some approval/rejection operations reuse broad action permissions;
- some legacy entities expose public setters, use `double` for money, or represent payment methods as integers;
- several menu/routes are partial, commented, or ahead of complete backend behavior;
- legacy due-date behavior includes a default day where a tenant setting is not yet available.

These observations are not permission to make unrelated mass changes. When a touched flow encounters one, either include a safe, tested migration in scope or record the precise follow-up and avoid extending the pattern.

## Change governance

Any intentional exception to a MUST rule requires a documented architecture/product decision that states the reason, risk, compensating control, owner, and expiry/review date. Convenience or compatibility alone is not sufficient without a migration plan.

When this constitution changes, update the reviewed date, explain the decision in the commit/PR, validate backend and frontend documentation links, and ensure both repository `AGENTS.md` files still require it for Finance-related work.
