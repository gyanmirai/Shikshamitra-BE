---
module: finance
domain: fee-assignment
content_type: Reference
status: active
updated_on: 2026-10-02
owner: backend-team
audience: humans-and-agents
---

# Assign student amounts for variable fees

TL;DR: a structure detail decides fixed versus variable pricing independently of optional selection.
Finance staff supplies included variable amounts during student assignment; Payment Terms still control installments.

## Conditions

| Pricing | Selection | Assignment behavior |
|---|---|---|
| Fixed, required | Always included | Use configured amount; existing authorized override rules apply |
| Fixed, optional | Selected | Use configured amount; no variable entry |
| Fixed, optional | Skipped | No dues |
| Variable, required | Always included | Student amount required before preview/commit |
| Variable, optional | Selected | Student amount required before preview/commit |
| Variable, optional | Skipped | No amount entry and no dues |

`FeeStructureDetail.IsVariable` defaults to false for old rows.
Fixed `Amount` must be present; variable `Amount` must be null, including draft saves and PATCH changes.
Both types require valid Payment Term versions before publication.
Variable schedule previews retain dates and percentages with null monetary amounts until assignment.
Amounts are nonnegative, at most two decimal places, and capped at 9,999,999,999.99.
Explicit zero student amounts require a nonblank reason of at most 500 characters.

## Assignment contract

Existing preview, assign and bulk endpoints accept this additional member:

```json
{
  "selectedOptionalFeeHeadIds": ["11111111-1111-1111-1111-111111111111"],
  "variableFeeHeads": [
    {
      "feeHeadId": "11111111-1111-1111-1111-111111111111",
      "amount": 12000.00,
      "zeroAmountReason": null
    }
  ]
}
```

`SelectedOptionalFeeHeadIds` is the single authority for optional inclusion.
`VariableFeeHeads` supplies amounts only for included variable heads; omitted optional heads are excluded.
Required variable heads must have an amount entry.
Reject duplicate/unknown IDs, fixed-head entries, excluded-head entries, null amounts, negative amounts, excess precision, excess magnitude, and unreasoned zero.
The existing `FeeHeadDetails` member remains the fixed amount override contract and rejects variable heads.
Initial variable amount entry requires the existing assignment Add permission; it is not a fixed amount override.
Fixed override permission and reason checks remain unchanged.

Preview heads expose `IsVariable`, nullable `ConfiguredAmount`, nullable excluded `EffectiveAmount`, `ZeroAmountReason`, and `AmountBasis=CoverageTotal` for variable rows.
Included amounts are captured in the assignment snapshot and used by the existing decimal allocation, proration and due generation pipeline.
The amount is the total for Payment Term service coverage before Finance proration, not a monthly rate.
Changed amounts, optional selections, periods or policies invalidate the preview token.
Retry hashing keeps the prior fixed-only fingerprint unchanged when the variable list is empty.
Commit remains transactional and preserves existing duplicate-assignment protection.

## Module compatibility and downstream behavior

New Fee Head and nested Fee Category head creation ignores the deprecated Module input.
Unrelated edits preserve any existing legacy Module value.
Module storage, lookup and old fixed-head service verification remain internally during the compatibility window.
Variable assignment accepts academic-year coverage or existing authorized manual-date overrides, with no module/source mapping or automatic rate lookup.
Verified transport source selection is rejected for variable heads.
Transport and hostel rate configuration stays in their owning modules.

Admission selection accepts a null configured amount for variable heads and stores the variable flag in its intent snapshot.
Admission selection does not resolve student amounts or post fees.
The class fee setup report displays variable pricing as entered at assignment.
Downstream collections continue to use concrete assigned DueFee amounts.
Historical fixed assignments, paid balances, receipts, and policy snapshots are not rewritten.

## Persistence and validation

The `VariableFeeStructurePricing` migration makes structure Amount nullable, adds a false-default IsVariable column, and adds `CK_FeeStructureDetail_Pricing`.
Its downgrade must not reinterpret null variable prices as free fixed fees.
Apply migrations before deploying the updated API and frontend.
No migration is applied to a live school database as part of source implementation.

| Check | Source |
|---|---|
| Domain pricing invariant | `test/ShikshaMitra.Domain.Tests/Finance/Fees/VariableFeePricing_Tests.cs` |
| Four pricing/selection conditions, dues, retry, missing/zero/stale amounts | `test/ShikshaMitra.Application.Tests/Finance/Fees/Settings/VariableFeeAssignment_Tests.cs` |
| Pricing PATCH, immutable publication review, independent revisions | `test/ShikshaMitra.Application.Tests/Finance/Fees/Settings/VariableFeeAssignment_Tests.cs` |
| Nullable model, fixed data preservation, PostgreSQL constraints and guarded downgrade | `test/ShikshaMitra.EntityFrameworkCore.Tests/EntityFrameworkCore/VariableFeeMigration_Tests.cs` |
| Existing fixed behavior, scope, bulk retries | `test/ShikshaMitra.Application.Tests/Finance/Fees/Settings/StudentFeeAssignmentWorkspace_Tests.cs` |
| Structure validation and mapping | `src/ShikshaMitra.Application/Finance/Fees/Settings/FeeStructureAppService.Validation.cs`, `FeeStructureAppService.Mapping.cs` |
| Assignment validation and snapshot | `src/ShikshaMitra.Application/Finance/Fees/StudentFeeAssignment/StudentFeeStructureAppService.Preparation.cs`, `StudentFeeStructureAppService.cs` |

Later activation of a previously skipped service and changes to committed amounts require a separate reviewed correction flow.
This implementation does not introduce unresolved committed dues or automatic module billing.

## Local verification on 2 October 2026

Focused domain and allocation checks passed (15 tests). The focused assignment run passed (12 tests),
and the pricing PATCH/publication/revision check passed separately (1 test).
The HTTP API host built successfully to an isolated output directory with zero errors;
the existing running API and Visual Studio session were not stopped or restarted.
The latest EF model snapshot check passed. All three variable pricing EF/migration checks passed,
including executing the generated pricing upgrade/downgrade SQL on a new disposable loopback PostgreSQL database.
That check uses a minimal legacy pricing table and is not a full production database upgrade rehearsal.
The disposable database was removed after the run.

The broader structure/assignment regression class passed 53 of 57 tests.
Four existing policy scenarios remain unresolved: active refund policy fixtures lack the Expense Category required
by the concurrent routing changes (two tests), a policy update omits its default flag (one test),
and a newly created collection exception is absent from the effective policy options (one test).
Do not interpret the focused variable checks as a clean full-suite result.

Backend documentation validation reports an existing encoding issue in
`docs/modules/finance/references/charge-management-e2e-test-plan.md`.
Production data, existing assignments and receipts have not been modified by verification.
