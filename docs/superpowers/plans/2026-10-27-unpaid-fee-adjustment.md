# Unpaid Fee Adjustment Request Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task in the current session. Steps use checkbox (`- [ ]`) syntax for tracking. Delegate only if the user explicitly chooses delegation.

**Goal:** Deliver a student-specific adjustment request against term-wise unpaid fees, with optional main/manual amount entry and atomic approval or rejection.

**Architecture:** Keep the current frontend destination and reuse concession student-selection/shared UI patterns. Add an unpaid-request aggregate, decimal target-line storage and API inside the adjustment feature, reusing `DueFee.ApplyAdjustmentCredit`. Preserve the old source/application model as historical evidence rather than remapping its status integers or amounts.

**Tech Stack:** .NET 9, ABP, EF Core/PostgreSQL, FluentValidation, xUnit; Next.js/React/TypeScript, MUI shared components, Formik/Yup, RTK Query, Vitest, Playwright.

**Spec:** [Unpaid Fee Adjustment Request Design](../specs/2026-10-27-unpaid-fee-adjustment-design.md).

## Global Constraints

- Exactly three statuses for new requests: Pending=0, Approved=1, Rejected=2.
- Automatic allocation starts with the earliest unpaid term and moves forward.
- Main amount or fee-wise amounts: neither is independently mandatory; at least one positive amount is required.
- Only unpaid principal amounts from the selected student's selected-year fee assignment are eligible; partially paid lines expose only the outstanding portion.
- Approval applies the entire saved adjustment atomically. Pending/Rejected do not change dues.
- Use decimal/numeric(18,2), INR and DateTimeOffset for new monetary/time contracts; preserve legacy data.
- Backend Finance constitution and permission constants govern money, tenant/scope, authority and audit.
- Preserve current shared UI, grouped permission editor, user edits and existing cash-management migration work. Do not merge main or modify CI triggers.
- No implementation, database migration execution, commit or publishing is part of this planning deliverable.

## Review Focus

1. Fees paid or otherwise changed between request and approval: keep Pending and report changed rows; never silently change the approved target.
2. Identical installment labels across assignments/years: group by persisted identities and filter by academic ownership, not display text or calendar year.
3. Automatic/manual switching, clear actions and late student-query responses: preserve the intended allocation without double counting or cross-student state.
4. Concurrent requests/decisions/retries: no over-reservation, duplicated benefit, partial credit or contradictory terminal status.
5. Old enum/DTO consumers and old partial requests: new three-state requests stay distinct; history remains truthful and readable.

## Repository evidence and preparation

Both repositories currently use `feat/simple-cash-management` and have substantial unrelated uncommitted changes. Planning adds only this plan and its design document. At execution, inspect fresh status in each repo, create/reuse an appropriate isolated checkout using the worktree skill, and preserve these documents and required current dependency changes explicitly. Do not stash, reset or copy all unrelated work as a shortcut.

Reference implementations:

- FE `src/screens/finance/Concession/Components/ConcessionRequestForm.tsx`: scoped student query, dependency resets and fee-assignment selection.
- FE `src/screens/finance/Concession/Components/ConcessionRequestWorkspace.tsx` and `ConcessionSteps/RequestEnteredInstallments.tsx`: shared section anatomy and fee-row presentation; do not reuse concession calculations.
- BE `src/ShikshaMitra.Domain/Finance/Fees/Collections/Aggregates/DueFee.cs`: outstanding balance and adjustment credit transition.
- BE `src/ShikshaMitra.Application/Finance/Fees/Adjustments/`: present orchestration, audit and legacy contract to retire from active callers.

## Planned interfaces

New Domain.Shared enums: `UnpaidFeeAdjustmentStatus { Pending=0, Approved=1, Rejected=2 }`, `FeeAdjustmentEntryMode { Automatic=0, Manual=1 }`.

New command `UnpaidFeeAdjustmentCommandDto`: `Guid AcademicYearId`, `Guid StudentId`, `Guid StudentFeeStructureId`, `string Reason`, `FeeAdjustmentEntryMode EntryMode`, `decimal? MainAmount`, `List<UnpaidFeeAdjustmentInputLineDto> Lines`, `string? PreviewToken`, `Guid? LegacyAdjustmentId`. Each manual input line is `{ Guid DueFeeId, decimal Amount }`. Preview accepts no token; create/revise require the returned token. Empty optional UI amounts are omitted. Automatic commands have MainAmount > 0 and no manual Lines; manual commands have MainAmount=null and positive unique Lines. The optional legacy link is validated for accessible same-student/year ownership; its display reference is resolved by the server.

Context DTO: student/year/assignment choices and selected assignment, currency, ordered `Terms`, total unpaid and total available. Each line supplies due ID, fee head ID/name, actual installment identity/label/order/dates, original/paid/prior-credit/outstanding/reserved/available amounts and concurrency stamp. Domain query records own these monetary facts; the UI does not invent capacity.

Preview DTO: `EntryMode`, requested total, ordered positive allocations, current unpaid total, after-adjustment unpaid total, and opaque `PreviewToken`. Each allocation contains due ID, amount and before/after outstanding. Token is bound to current actor/tenant/student/year/assignment, entry intent, due versions and allocation hash.

Decision DTOs: approval `{ string ConcurrencyStamp, string? Notes }`; rejection `{ string ConcurrencyStamp, string Reason }`. Decision callers supply `Idempotency-Key`. Create/revise also require that header. Detail DTO returns saved allocations, current context, creator/reviewer IDs and names, status, reference, reasons, DateTimeOffset timestamps, concurrency stamp and allowed actions. Applications DTO contains rows, exact three-state counts and server pagination.

New `IUnpaidFeeAdjustmentAppService`: `GetContextAsync(studentId, academicYearId, studentFeeStructureId?, requestId?)`, `PreviewAsync(command, requestId?)`, `CreateAsync(command, idempotencyKey)`, `UpdateAsync(id, command, concurrencyStamp, idempotencyKey)`, `GetAsync(id)`, `GetApplicationsAsync(input)`, `ApproveAsync(id, decision, idempotencyKey)`, `RejectAsync(id, decision, idempotencyKey)`. Optional requestId is only for a creator revising their accessible Pending request; bind it into the token and exclude only that request's own reservation. Creation rejects a revision token; update requires a token bound to its route ID.

New domain query repository `IUnpaidFeeAdjustmentRepository`: scoped eligibility projection, request/detail/list/count reads, pending-capacity projection excluding an optional request ID, acquisition of a transaction-scoped student/year/assignment mutation lock, and locked loading of a request/dues in deterministic order. EF owns provider-specific locking; Domain never references EF.

## Task 1: Decimal allocation and three-state domain model

**Create (BE):**
- `src/ShikshaMitra.Domain.Shared/Finance/Fees/Adjustments/UnpaidFeeAdjustmentStatus.cs`
- `src/ShikshaMitra.Domain.Shared/Finance/Fees/Adjustments/FeeAdjustmentEntryMode.cs`
- `src/ShikshaMitra.Domain.Shared/Finance/Fees/Adjustments/UnpaidFeeAdjustmentErrorCodes.cs`
- `src/ShikshaMitra.Domain/Finance/Fees/Adjustments/Aggregates/UnpaidFeeAdjustmentRequest.cs`
- `src/ShikshaMitra.Domain/Finance/Fees/Adjustments/Entities/UnpaidFeeAdjustmentLine.cs`
- `src/ShikshaMitra.Domain/Finance/Fees/Adjustments/UnpaidFeeAdjustmentAllocator.cs`
- `src/ShikshaMitra.Domain/Finance/Fees/Adjustments/Repositories/IUnpaidFeeAdjustmentRepository.cs`
- `test/ShikshaMitra.Domain.Tests/Finance/Fees/UnpaidFeeAdjustmentLifecycle_Tests.cs`
- `test/ShikshaMitra.Domain.Tests/Finance/Fees/UnpaidFeeAdjustmentAllocator_Tests.cs`

**Interfaces:** allocator `Allocate(IReadOnlyList<UnpaidFeeAdjustmentEligibleLine> orderedLines, decimal mainAmount)` returns immutable positive due/amount allocations. Aggregate exposes `RevisePending(...)`, `Approve(Guid reviewerId, DateTimeOffset decidedAt, string? notes, IReadOnlyList<UnpaidFeeAdjustmentAppliedLine> evidence)` and `Reject(Guid reviewerId, DateTimeOffset decidedAt, string reason)`; constructors establish Pending and validated saved allocations. Repository query record types live beside their interface.

- [ ] Write failing allocator tests: capacities [6000,7000,8000], main 10000 -> [6000,4000,0]; zero-capacity rows skipped; equal-date order stable; 0.01 preserved; excess/negative/empty rejected; manual duplicates rejected.
- [ ] Write failing lifecycle tests: create/revise preserve exact total and reference; creator cannot decide; only Pending transitions; Approved applied total equals requested total; reject applies zero; terminal edits/repeat decisions denied.
- [ ] Run `dotnet test test/ShikshaMitra.Domain.Tests --filter FullyQualifiedName~UnpaidFeeAdjustment`; confirm intended assertions fail, not fixture setup.
- [ ] Implement the types, allocator and transitions. Positive monetary values have maximum two decimals and must fit numeric(18,2) and live fee capacity; use canonical monetary validation and reject unsafe client numeric conversion rather than losing precision. Use decimal on the server and exact minor-unit comparisons in client validation. Keep legacy enum/aggregate unchanged.
- [ ] Run the same tests and focused `DueFee` balance tests; require PASS. Commit only this task's intentional files when execution commits are appropriate.

## Task 2: Scoped eligibility, reservations and additive persistence

**Create (BE):**
- `src/ShikshaMitra.EntityFrameworkCore/Finance/Fees/Adjustments/UnpaidFeeAdjustmentRequestConfiguration.cs`
- `src/ShikshaMitra.EntityFrameworkCore/Finance/Fees/Adjustments/UnpaidFeeAdjustmentLineConfiguration.cs`
- `src/ShikshaMitra.EntityFrameworkCore/Finance/Fees/Adjustments/EfCoreUnpaidFeeAdjustmentRepository.cs`
- `src/ShikshaMitra.EntityFrameworkCore/Finance/Fees/Adjustments/EfCoreUnpaidFeeAdjustmentRepository.Eligibility.cs`
- `test/ShikshaMitra.EntityFrameworkCore.Tests/Finance/UnpaidFeeAdjustmentQuery_Tests.cs`
- `test/ShikshaMitra.HttpApi.Tests/Finance/FeeAdjustment/UnpaidFeeAdjustmentMigration_Tests.cs`

**Modify (BE):** `EntityFrameworkCore/DbContextPartials/ShikshaMitraDbContext.Finance.cs` under the existing EF project; `Migrations/ShikshaMitraDbContextModelSnapshot.cs`. Generate an additive migration named `AddUnpaidFeeAdjustmentRequests` against the verified current migration chain; preserve concurrent cash-management schema changes.

**Interfaces:** implement Task 1 repository using persisted assignment/account year links and actual installment snapshot fields. Add `Finance_UnpaidFeeAdjustmentRequests`, `Finance_UnpaidFeeAdjustmentLines`, and a command-replay table keyed by tenant/actor/operation/request/key with payload hash and result. Request and due concurrency stamps participate in approval checks. Index tenant/year/scope/status and request/due IDs; unique reference and request/due allocation constraints.

- [ ] Write failing query tests: fully paid omitted, partially paid remainder retained, same student in two academic years isolated, duplicate labels remain distinct, inconsistent/missing provenance rejected/excluded, view/list/count apply identical row scope.
- [ ] Write failing reservation tests: two Pending requests share capacity without reducing parent payable; exclude own reservation on revise; rejection releases capacity; old legacy Pending rows do not reserve new-flow capacity.
- [ ] Run `dotnet test test/ShikshaMitra.EntityFrameworkCore.Tests --filter FullyQualifiedName~UnpaidFeeAdjustment`; confirm expected failures.
- [ ] Implement exact year/assignment predicates, deterministic installment order, locked capacity read/write, decimal configs, and replay uniqueness. Use provider-backed transaction locks and sorted due row locking; no process-local lock as the correctness boundary.
- [ ] Generate/review migration. Test upgrade from the existing migration baseline with synthetic legacy Pending/PartiallyApplied/Applied/Rejected/Cancelled rows; all legacy rows, enum values and monetary balances must remain identical.
- [ ] Run query/migration suites on a disposable PostgreSQL database and EF model checks; require PASS and a clean intentional migration diff. Do not run migrations against a live school database for validation.

## Task 3: API preview, create/revise and atomic decisions

**Create (BE):**
- `src/ShikshaMitra.Application.Contracts/Finance/Fees/Adjustments/UnpaidFeeAdjustmentDtos.cs`
- `src/ShikshaMitra.Application.Contracts/Finance/Fees/Adjustments/IUnpaidFeeAdjustmentAppService.cs`
- `src/ShikshaMitra.Application/Finance/Fees/Adjustments/UnpaidFeeAdjustmentAppService.cs`
- `src/ShikshaMitra.Application/Finance/Fees/Adjustments/UnpaidFeeAdjustmentAppService.Queries.cs`
- `src/ShikshaMitra.Application/Finance/Fees/Adjustments/UnpaidFeeAdjustmentAppService.Commands.cs`
- `src/ShikshaMitra.Application/Finance/Fees/Adjustments/UnpaidFeeAdjustmentPreviewService.cs`
- `src/ShikshaMitra.Domain.Shared/Finance/Events/UnpaidFeeAdjustmentApprovedEto.cs`
- `src/ShikshaMitra.HttpApi/Controllers/Finance/Fees/Adjustments/FeeAdjustmentRequestController.cs`
- `test/ShikshaMitra.HttpApi.Tests/Finance/FeeAdjustment/UnpaidFeeAdjustmentRequestApi_Tests.cs`
- `test/ShikshaMitra.HttpApi.Tests/Finance/FeeAdjustment/UnpaidFeeAdjustmentAuthorizationApi_Tests.cs`

**Modify (BE):** canonical `Domain.Shared/Constants/Permissions/SubmodulePermissions/Finance/ShikshaMitraSubmodulePermissions.Finance.Fees.cs`, its registry/definition wiring where required, `Domain.Shared/Localization/ShikshaMitra/en.json` and `hi.json`; reuse `FinanceAuditWriter` and the existing adjustment-applied event mechanism with a versioned unpaid-request event identity.

**Interfaces:** implement the app-service/DTO/endpoint signatures above. Add exact adjustment Approve/Reject permissions; View/Add retain their canonical identifiers. Token verification is server-owned and bound to the full context. Events carry new request IDs with an explicit entity kind so they cannot be mistaken for legacy adjustment IDs.

- [ ] Write failing HTTP tests: context filters and required assignment choice; automatic/manual preview; one-positive validation; extra decimal/negative/duplicate/foreign due rejection; create Pending with zero due change; preview tampering/staleness denial; revise preserves ID/reference and reuses only its own reservation; foreign/noncreator requestId and create/update token substitution are denied.
- [ ] Write failing decision tests: full atomic approval; rejection with required reason and zero credit; no self-review including administrators; exact action grants; denied tenant/year/scope; no partial apply or silent target substitution; unchanged saved allocations when fees change.
- [ ] Write failing replay tests: identical create/decision retries return one result; same key/different payload denied; actor and tenant keys isolated; terminal different command cannot reuse an approval result.
- [ ] Run `dotnet test test/ShikshaMitra.HttpApi.Tests --filter FullyQualifiedName~UnpaidFeeAdjustment`; confirm intended failures.
- [ ] Implement FluentValidation plus domain validation, transactional orchestrations, concurrency checks, saved preview evidence, audit and replay writes. Approval invokes `DueFee.ApplyAdjustmentCredit` exactly once per saved line and saves all-or-nothing. Persist event intent through the established durable outbox/UoW mechanism; a publish failure must not leave an unrecoverable successful credit.
- [ ] Implement Pending revision in place with creator/state/concurrency checks. Creation/revision/decision use the same reservation lock. No role-name bypass and no automatic new grants to existing roles.
- [ ] Run API and permission regression suites, build Contracts/Application/HttpApi/EF, and require PASS before frontend integration.

## Task 4: Concession-style student selection and term-wise amount form

**Modify (FE):**
- `src/screens/finance/adjustments/components/AdjustmentForm.tsx`
- `src/screens/finance/adjustments/components/AdjustmentGeneralInfoCard.tsx`
- `src/screens/finance/adjustments/types/adjustmentFormValues.ts`
- `src/screens/finance/adjustments/types/adjustmentApi.types.ts`
- `src/screens/finance/adjustments/types/adjustment.types.ts`
- `src/redux/finance/adjustments/adjustmentApi.ts`

**Create (FE):**
- `src/screens/finance/adjustments/components/AdjustmentUnpaidTerms.tsx`
- `src/screens/finance/adjustments/hooks/useAdjustmentRequestForm.ts`
- `src/screens/finance/adjustments/types/adjustmentFormValues.test.ts`
- `src/screens/finance/adjustments/components/AdjustmentForm.test.tsx`
- `src/redux/finance/adjustments/adjustmentApi.test.ts`

**Interfaces:** Formik values contain academic selectors, student/assignment IDs, reason, `entryMode`, raw-string `mainAmount`, and raw-string `lineAmounts: Record<dueFeeId,string>`. `buildAdjustmentApiPayload(values)` emits exactly one entry mode. `useAdjustmentRequestForm` owns scoped context, debounced server preview, cancellation/stale-response guards and awaited create/revise. The accepted server preview is the submission authority; use pending preview requests as a blocking busy state.

- [ ] Write failing form/schema tests: main=10000 accepted with generated earliest-first preview; main blank plus one line=2500 accepted; all blank/zero rejected with specified message; optional unused lines accepted; over-capacity, stale preview and invalid decimals denied.
- [ ] Write failing interaction tests: generated-line edit clears main and enters manual mode once; clearing main clears only auto allocations; student/year/assignment changes reset dependent state; late prior-student response ignored; failed submit stays open; successful awaited submit closes once.
- [ ] Run `npm test -- src/screens/finance/adjustments src/redux/finance/adjustments`; confirm targeted assertions fail.
- [ ] Replace refund/month-based entry with the new context and term groups. Use the scoped student query and selector resets from concession; preserve prefilled student entry from Fee Collection. Auto-select only one valid assignment and require choice when multiple.
- [ ] Use `FormSectionCard`, `NumericFormatCustom`, `formatNumber`, Formik/Yup and shared feedback. Show unpaid/current reservations/available/after amounts; no percentages or paid-fee source picker. Use server allocations for accepted totals, preserving raw string input and precision.
- [ ] Remove premature reset/close and ensure `onSubmit` returns/rejects a Promise all the way through the parent. Preserve dirty-close behavior and show server field/conflict errors without losing input.
- [ ] Run focused form/client tests and focused eslint; require PASS.

## Task 5: Three-status register, review decisions and all entry points

**Modify (FE):**
- `src/screens/finance/adjustments/index.tsx`
- `src/screens/finance/adjustments/enums/adjustment.enum.ts`
- `src/screens/finance/adjustments/constants/adjustmentOptions.ts`
- `src/screens/finance/adjustments/hooks/useAdjustment.ts`
- `src/screens/finance/adjustments/components/AdjustmentTable.tsx`
- `src/screens/finance/adjustments/components/AdjustmentView.tsx`
- `src/screens/finance/adjustments/components/AdjustmentPreview.tsx`
- `src/screens/finance/adjustments/components/AdjustmentAuditSection.tsx`
- `src/screens/finance/adjustments/components/AdjustmentApproveRejectForm.tsx`
- `src/screens/finance/adjustments/utils/mapAdjustmentApplication.ts`
- `src/screens/finance/adjustments/utils/mapAdjustmentDetail.ts`
- `src/screens/finance/FeeCollection/index.tsx`
- `src/constants/finance-menu.ts`, `src/screens/security/role/finance-permission-groups.ts` only in owning adjustment mappings.

**Create (FE):** `components/AdjustmentTable.test.tsx`, `components/AdjustmentView.test.tsx`, `utils/mapAdjustmentApplication.test.ts`, and `src/screens/security/role/adjustment-permissions.test.ts` in their owning folders.

**Interfaces:** use the new DTO enum directly; tab keys are distinct UI values, never assumed to equal API status numbers. Review consumes saved request allocations plus live conflict context. New review-specific permission checks use exact Approve/Reject identifiers without widening unrelated shared-hook behavior.

- [ ] Write failing tests: exact three labels/counts; correct status filtering with server pagination; unknown new status produces explicit unsupported-state behavior, never Approved; saved total and term lines shown; only independent authorized checker can decide.
- [ ] Write failing tests: main register and Fee Collection create the same new request; noncreator cannot revise; paid-in-the-meantime conflict refreshes row evidence and keeps request Pending; repeated action uses one idempotency key until outcome is known.
- [ ] Run focused tests and confirm intended failures.
- [ ] Replace the separate apply screen with a review view and Approve/Reject decisions. Approval cannot edit saved amounts. Remove partial/remaining-credit UI from new records. Remove active references to `ApplyAdjustmentScreen`, `ApplyAdjustmentDialog`, `useApplyAdjustment` and their source DTOs after a full caller search; retain only any explicitly required legacy read helpers.
- [ ] Use `CustomizableTable`, current header/filter/search/row-action conventions, actual counts and server paging. Return author/reviewer identity from the server; do not depend on missing mapper fields.
- [ ] Update the grouped permission-editor mapping in place; preserve direct/inherited grants, explicit denials and unavailable-action behavior. Add no new editor design or broad auto-grants.
- [ ] On success invalidate the owning adjustment, collection/student balance, audit, relevant report/dashboard and fee-context tags found in the live client; test that the next view reflects the credited dues. Create/reject invalidate reservations too. Keep cash/receipt totals unchanged.
- [ ] Add the clearly labelled read-only legacy history entry outside the three request status tabs; use a separate legacy mapper preserving old numeric meanings. Keep old references reachable without presenting partial records as new Approved records.
- [ ] Run focused unit/client/permission tests, `npm run ts`, and focused eslint; require PASS or record unrelated baseline failures with exact evidence.

## Task 6: Real concurrency, downstream balances and browser acceptance

**Create (BE):** `test/ShikshaMitra.HttpApi.Tests/Finance/FeeAdjustment/UnpaidFeeAdjustmentPostgres_Tests.cs`.
**Create (FE):** `e2e/finance/adjustment-requests.spec.ts` and a focused fixture under `e2e/fixtures/adjustment-requests/` only if the existing authenticated test harness cannot isolate these scenarios.

**Interfaces:** exercise the public new endpoints and actual persisted DueFee amounts; do not certify transactions using only the SQLite fixture. Use synthetic disposable PostgreSQL data and independent authenticated maker/checker identities.

- [ ] Test concurrent creates exceeding capacity: one request succeeds, the conflicting request is denied, and no parent balance changes.
- [ ] Test collection/concession versus approval and simultaneous approve/reject: exactly one compatible outcome persists, no negative outstanding and no duplicated credit. Test failure after the first due mutation rolls back every due, request status, reservation, audit and command result.
- [ ] Test payment after request leaves approval Pending with changed-line evidence. Revising and re-reviewing permits the new valid amount; approval never silently redistributes it.
- [ ] Test principal 21000 with saved credit 10000 -> principal outstanding 11000, unchanged paid/cash/receipt values, adjustment total 10000 in collection/student views and reports. Ensure prior concession credit is not counted twice. Test rejection -> original principal outstanding retained.
- [ ] Run PostgreSQL-focused API tests, allocation/lifecycle/domain/query/API tests, and permission/tenant regression tests; require PASS.
- [ ] Browser test: select student like concessions, see only unpaid term groups, enter main 10000, verify 6000+4000; submit as maker; approve as checker; verify Approved and refreshed collection balance. Repeat with one manual line, rejection, empty/all-paid cases, unavailable assignment, and failed network retry.
- [ ] Run `npm run test:e2e -- e2e/finance/adjustment-requests.spec.ts`. Then finish the actual UI smoke in Codex's in-app browser at desktop and mobile widths, including keyboard navigation, dirty close and conflict recovery. Record actual evidence, not an unexecuted checklist.

## Task 7: Historical cutover, docs and release verification

**Modify (BE):** legacy `FeeAdjustmentController.cs`/command entry points only to implement explicit retired-write responses after callers move; `docs/modules/finance/{product,api,architecture,workflows,testing}.md`.
**Modify (FE):** `docs/finance/{README,routes,clients,workflows,testing}.md` for the actual new route destination, clients, amount modes, statuses, decision permissions and smoke commands. Use the module-docs skill at execution; link backend product rules rather than copying them.

**Interfaces:** old historical reads continue to return legacy contracts; old create/update/apply/delete writes return the stable retired-workflow business error after cutover. Legacy requests are never automatically reapproved or renamed. A recreated request records the previous reference as evidence with no inherited cash/credit.

- [ ] Audit all repo callers of `/fee-adjustments` and old enum/mapper types; prove active creation and decisions use the new API. Test old writes cannot bypass new request rules after cutover and old scoped reads remain accessible.
- [ ] Rehearse additive migration and staged backend -> frontend -> legacy-write-retirement rollout on a disposable environment. Compare all preexisting due balances and legacy rows before/after; expect no migration-induced monetary change.
- [ ] Update canonical docs to the tested behavior and migration ordering. Do not overwrite concurrent cash-management notes or remove other features' documentation.
- [ ] Run backend `dotnet build --artifacts-path .artifacts/unpaid-adjustment-build`; focused suites listed above; `powershell -ExecutionPolicy Bypass -File scripts/docs/validate-docs.ps1 -Module finance`; `git diff --check`.
- [ ] Run frontend focused Vitest/eslint, `npm run ts`, `npm run build`, `npm run docs:check`, focused E2E and `git diff --check`. Do not repeat broad successful checks without a subsequent relevant change.
- [ ] Review only intentional feature diffs, changed permissions, schema and historical compatibility. Report any actual blocker; do not claim implementation completion from this plan alone. Any PR merge into main still requires the user's explicit PR-specific approval.

## Planning self-review

- Confirmed unpaid-fee, earliest-term, optional amount, validation and three-state requirements map to Tasks 1-6.
- All five Review Focus conditions have named test steps in Tasks 2-6.
- New statuses/decimals/contracts are separate from legacy types throughout the plan; history is not silently remapped.
- Student selection, both form entry points, pending revision, maker-checker permissions, all-or-nothing approval, retries, concurrency, audit/events and downstream refresh are covered.
- This plan is ready for user review. No application code has been changed and no tests or live migrations have been run as part of planning.
