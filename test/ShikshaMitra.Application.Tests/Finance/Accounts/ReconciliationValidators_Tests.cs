using System;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>
/// Unit tests for <see cref="ApproveReconciliationRequestValidator"/> and
/// <see cref="RejectReconciliationRequestValidator"/> — validates business rules
/// enforced at the application boundary.
/// </summary>
public class ReconciliationValidators_Tests
{
    // ── Approve validator ─────────────────────────────────────────────────────

    private readonly ApproveReconciliationRequestValidator _approveValidator = new();

    [Fact]
    public void Approve_WithoutRemarks_IsValid()
    {
        var input = new ApproveReconciliationInput { Remarks = null };
        var result = _approveValidator.Validate(input);
        result.IsValid.ShouldBeTrue("Remarks are optional for approve");
    }

    [Fact]
    public void Approve_WithRemarks_IsValid()
    {
        var input = new ApproveReconciliationInput { Remarks = "Verified by bank statement" };
        var result = _approveValidator.Validate(input);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Approve_WithExcessivelyLongRemarks_IsInvalid()
    {
        var input = new ApproveReconciliationInput { Remarks = new string('X', 1001) };
        var result = _approveValidator.Validate(input);
        result.IsValid.ShouldBeFalse("Remarks exceeding max length should fail");
        result.Errors.ShouldContain(e => e.PropertyName == "Remarks");
    }

    // ── Reject validator ──────────────────────────────────────────────────────

    private readonly RejectReconciliationRequestValidator _rejectValidator = new();

    [Fact]
    public void Reject_WithValidRemarks_IsValid()
    {
        var input = new RejectReconciliationInput { Remarks = "Cheque bounced by ICICI Bank" };
        var result = _rejectValidator.Validate(input);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Reject_WithNullRemarks_IsInvalid()
    {
        var input = new RejectReconciliationInput { Remarks = null! };
        var result = _rejectValidator.Validate(input);
        result.IsValid.ShouldBeFalse("Remarks must not be null for reject");
        result.Errors.ShouldContain(e => e.PropertyName == "Remarks");
    }

    [Fact]
    public void Reject_WithEmptyRemarks_IsInvalid()
    {
        var input = new RejectReconciliationInput { Remarks = "   " };
        var result = _rejectValidator.Validate(input);
        result.IsValid.ShouldBeFalse("Whitespace-only remarks must fail for reject");
        result.Errors.ShouldContain(e => e.PropertyName == "Remarks");
    }

    [Fact]
    public void Reject_WithExcessivelyLongRemarks_IsInvalid()
    {
        var input = new RejectReconciliationInput { Remarks = new string('X', 1001) };
        var result = _rejectValidator.Validate(input);
        result.IsValid.ShouldBeFalse("Remarks exceeding max length should fail");
        result.Errors.ShouldContain(e => e.PropertyName == "Remarks");
    }

    [Fact]
    public void Reject_MinimumValidRemarks_IsValid()
    {
        var input = new RejectReconciliationInput { Remarks = "Bounced" };
        var result = _rejectValidator.Validate(input);
        result.IsValid.ShouldBeTrue();
    }
}
