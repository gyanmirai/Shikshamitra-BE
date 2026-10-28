using System;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Mobile;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Xunit;

namespace ShikshaMitra.Finance;

public class MobileFinanceCommandRules_Tests
{
    [Fact]
    public void Family_money_wire_contract_includes_explicit_currency_and_decimal_text()
    {
        var receipt = new MobileFinanceReceipt(Guid.NewGuid(), "R-1", "FinalFee", "Fee", "Verified", "0.30", DateOnly.FromDateTime(DateTime.Today), DateTime.Today, null, true, null, null, null, null);
        var payment = new MobileFinancePayment(Guid.NewGuid(),receipt.ReceiptNumber, receipt.ReceiptDate, "0.30", "Paid", ["Cash"], [MobileFinanceProjection.ReceiptReference(receipt)]);
        var result = new MobileFinanceCommandResult(Guid.NewGuid(), "adjustment", "Pending", "0.30", []);
        foreach (var value in new object[] { receipt, payment, result })
        {
            var json = JsonSerializer.SerializeToElement(value);
            json.GetProperty("Currency").GetString().ShouldBe("INR");
            json.GetProperty(value is MobileFinanceReceipt ? "TotalPaidAmount" : "Amount").GetString().ShouldBe("0.30");
        }
    }
    [Theory]
    [InlineData("0.01", "0.01")]
    [InlineData("12345678.99", "12345678.99")]
    public void Canonical_money_preserves_exact_paise(string input, string expected) =>
        MobileFinanceCommandRules.Money(MobileFinanceCommandRules.ParseMoney(input)).ShouldBe(expected);

    [Theory]
    [InlineData("1.001")]
    [InlineData("1e2")]
    [InlineData("NaN")]
    [InlineData("-1.00")]
    [InlineData("0.00")]
    [InlineData("1,000.00")]
    [InlineData(" 1.00")]
    public void Parent_amounts_reject_noncanonical_or_nonpositive_money(string value) =>
        Should.Throw<BusinessException>(() => MobileFinanceCommandRules.ParseMoney(value));

    [Fact]
    public void Owner_guard_binds_child_year_and_actor()
    {
        var child = Guid.NewGuid(); var year = Guid.NewGuid(); var actor = Guid.NewGuid();
        MobileFinanceCommandRules.RequireOwner(child, year, actor, child, year, actor);
        Should.Throw<AbpAuthorizationException>(() => MobileFinanceCommandRules.RequireOwner(child, year, actor, Guid.NewGuid(), year, actor));
        Should.Throw<AbpAuthorizationException>(() => MobileFinanceCommandRules.RequireOwner(child, year, actor, child, Guid.NewGuid(), actor));
        Should.Throw<AbpAuthorizationException>(() => MobileFinanceCommandRules.RequireOwner(child, year, actor, child, year, Guid.NewGuid()));
    }

    [Fact]
    public void Stale_or_missing_preview_fails_closed()
    {
        MobileFinanceCommandRules.RequireFingerprint("abc", "abc");
        Should.Throw<BusinessException>(() => MobileFinanceCommandRules.RequireFingerprint("abc", "stale"));
        Should.Throw<BusinessException>(() => MobileFinanceCommandRules.RequireFingerprint("abc", null));
    }

    [Fact]
    public void Retry_hash_binds_all_nested_fields_and_is_stable()
    {
        var first = new { child = Guid.NewGuid(), year = Guid.NewGuid(), details = new { account = "1234" } };
        MobileFinanceCommandRules.Hash(first).ShouldBe(MobileFinanceCommandRules.Hash(first));
        MobileFinanceCommandRules.Hash(first).ShouldNotBe(MobileFinanceCommandRules.Hash(new { first.child, first.year, details = new { account = "5678" } }));
    }

    [Fact]
    public void Read_marker_is_per_user_per_event_and_deterministic()
    {
        var actor = Guid.NewGuid(); var eventId = Guid.NewGuid();
        MobileFinanceCommandRules.ReadMarkerId(actor, eventId).ShouldBe(MobileFinanceCommandRules.ReadMarkerId(actor, eventId));
        MobileFinanceCommandRules.ReadMarkerId(actor, eventId).ShouldNotBe(MobileFinanceCommandRules.ReadMarkerId(Guid.NewGuid(), eventId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Refund_eligibility_preserves_full_receipt_policy_and_all_original_allocation_amounts(bool allowPartial)
    {
        var receipt = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var result = new RefundEligibilityDto { AllowPartialRefund = allowPartial, TotalRefundable = 125.50m,
            Receipts = [new() { ReceiptId = receipt, ReceiptNumber = "P-001" }],
            FeeHeads = [new() { FeeHeadName = "Tuition", Months = [
                new() { SourceReceiptId = receipt, SourceAllocationId = first, DueFeeId = Guid.NewGuid(), Refundable = 100.25m },
                new() { SourceReceiptId = receipt, SourceAllocationId = second, DueFeeId = Guid.NewGuid(), Refundable = 25.25m }
            ] }] };
        var json = JsonSerializer.SerializeToElement(MobileFinanceRefundEligibilityProjection.Map(result));
        json.GetProperty("TotalRefundable").GetString().ShouldBe("125.50");
        var source = json.GetProperty("Receipts")[0];
        json.GetProperty("AllowPartialRefund").GetBoolean().ShouldBe(allowPartial);
        var lines = json.GetProperty("FeeHeads")[0].GetProperty("Months").EnumerateArray().ToList();
        lines.Select(x => x.GetProperty("SourceAllocationId").GetGuid()).ShouldBe([first, second]);
        lines.Select(x => x.GetProperty("Refundable").GetString()).ShouldBe(["100.25", "25.25"]);
    }
    [Theory]
    [InlineData(null, "500.00")]
    [InlineData("300.00", "300.00")]
    public void Refund_history_uses_domain_percentage_and_cap_result_instead_of_the_selected_paid_base(string? maximum, string expected)
    {
        var policy = new RefundPolicySnapshot(Guid.NewGuid(), Guid.NewGuid(), "Half refund", "HALF", RefundPolicyStatus.Active,
            false, RefundFeeHeadScope.AllFeeHeads, [], false, false, true, RefundAmountMode.Percentage, 50m, null,
            maximum == null ? null : MobileFinanceCommandRules.ParseMoney(maximum), null, RoundingMethodType.None,
            RefundDeductionType.None, null, null, string.Empty, [RefundPayoutMethod.Cash], 7, 1);
        var calculation = new RefundPolicyCalculationService().Calculate(policy, new RefundPolicyCalculationInput(1000m, 0m, 0m, 0m, null));
        MobileFinanceProjection.Money(MobileFinanceProjection.RefundRequestAmount(1000m, calculation.FinalRefund, false)).ShouldBe(expected);
        MobileFinanceProjection.RefundRequestAmount(1000m, 0m, false).ShouldBe(0m);
        MobileFinanceProjection.RefundRequestAmount(1000m, 0m, true).ShouldBe(1000m);
    }
}
