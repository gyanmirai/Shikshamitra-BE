using System;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>
/// Unit tests for all DueFee monetary mutation methods.
/// </summary>
public class DueFee_Mutations_Tests
{
    private static DueFee Create(double amount = 1000d)
    {
        var df = new DueFee(
            id: Guid.NewGuid(),
            tenantId: null,
            studentId: Guid.NewGuid(),
            feeHeadId: Guid.NewGuid(),
            month: MonthEnum.June,
            year: 2026,
            isOneTime: false,
            amount: amount);
        return df;
    }

    // ── ApplyPayment ─────────────────────────────────────────────────────────

    [Fact]
    public void ApplyPayment_Full_Sets_Status_Paid()
    {
        var df = Create(1000d);
        df.ApplyPayment(1000d);
        Assert.Equal(FeeStatus.Paid, df.Status);
        Assert.Equal(0d, df.OutstandingAmount);
    }

    [Fact]
    public void ApplyPayment_Partial_Sets_Status_Partial()
    {
        var df = Create(1000d);
        df.ApplyPayment(400d);
        Assert.Equal(FeeStatus.Partial, df.Status);
        Assert.Equal(600d, df.OutstandingAmount);
    }

    [Fact]
    public void ApplyPayment_Zero_Throws()
    {
        var df = Create(1000d);
        Assert.Throws<BusinessException>(() => df.ApplyPayment(0d));
    }

    [Fact]
    public void ApplyPayment_Successive_Accumulates_Correctly()
    {
        var df = Create(1000d);
        df.ApplyPayment(300d);
        df.ApplyPayment(700d);
        Assert.Equal(FeeStatus.Paid, df.Status);
        Assert.Equal(0d, df.OutstandingAmount);
    }

    // ── ApplyConcession ───────────────────────────────────────────────────────

    [Fact]
    public void ApplyConcession_Full_Clears_Outstanding()
    {
        var df = Create(1000d);
        df.ApplyConcession(1000d);
        Assert.Equal(0d, df.OutstandingAmount);
    }

    [Fact]
    public void ApplyConcession_Partial_Reduces_Outstanding()
    {
        var df = Create(1000d);
        df.ApplyConcession(200d);
        Assert.Equal(800d, df.OutstandingAmount, precision: 2);
    }

    [Fact]
    public void ApplyConcession_On_Top_Of_Payment_Clears_Outstanding()
    {
        var df = Create(1000d);
        df.ApplyPayment(600d);
        df.ApplyConcession(400d);
        Assert.Equal(0d, df.OutstandingAmount);
    }

    // ── ApplyAdjustmentCredit ─────────────────────────────────────────────────

    [Fact]
    public void ApplyAdjustmentCredit_Full_Clears_Outstanding()
    {
        var df = Create(1000d);
        df.ApplyAdjustmentCredit(1000d);
        Assert.Equal(0d, df.OutstandingAmount);
    }

    [Fact]
    public void ApplyAdjustmentCredit_Partial_Reduces_Outstanding()
    {
        var df = Create(1000d);
        df.ApplyAdjustmentCredit(250d);
        Assert.Equal(750d, df.OutstandingAmount, precision: 2);
    }

    // ── ApplyRefund ───────────────────────────────────────────────────────────

    [Fact]
    public void ApplyRefund_Reduces_RefundableBalance()
    {
        var df = Create(1000d);
        df.ApplyPayment(1000d);
        df.ApplyRefund(300d);
        Assert.Equal(700d, df.RefundableBalance, precision: 2);
    }

    [Fact]
    public void ApplyRefund_Full_Amount_Exhausts_RefundableBalance()
    {
        var df = Create(1000d);
        df.ApplyPayment(1000d);
        df.ApplyRefund(1000d);
        Assert.Equal(0d, df.RefundableBalance, precision: 2);
    }

    // ── Negative / guard checks ───────────────────────────────────────────────

    [Fact]
    public void ApplyPayment_Negative_Throws()
    {
        var df = Create();
        Assert.Throws<BusinessException>(() => df.ApplyPayment(-1d));
    }

    [Fact]
    public void ApplyConcession_Negative_Throws()
    {
        var df = Create();
        Assert.Throws<BusinessException>(() => df.ApplyConcession(-0.01d));
    }
}
