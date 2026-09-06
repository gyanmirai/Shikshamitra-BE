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
    private static DueFee Create(decimal amount = 1000m)
    {
        var df = new DueFee(
            id: Guid.NewGuid(),
            tenantId: null,
            studentId: Guid.NewGuid(),
            feeHeadId: Guid.NewGuid(),
            month: MonthEnum.June,
            year: 2026,
            isOneTime: false,
            amount: amount,
            dueDate: new DateTime(2026, 6, 10));
        return df;
    }

    // ── ApplyPayment ─────────────────────────────────────────────────────────

    [Fact]
    public void ApplyPayment_Full_Sets_Status_Paid()
    {
        var df = Create(1000m);
        df.ApplyPayment(1000m);
        Assert.Equal(FeeStatus.Paid, df.Status);
        Assert.Equal(0m, df.OutstandingAmount);
    }

    [Fact]
    public void ApplyPayment_Partial_Sets_Status_Partial()
    {
        var df = Create(1000m);
        df.ApplyPayment(400m);
        Assert.Equal(FeeStatus.Partial, df.Status);
        Assert.Equal(600m, df.OutstandingAmount);
    }

    [Fact]
    public void ApplyPayment_Zero_Throws()
    {
        var df = Create(1000m);
        Assert.Throws<BusinessException>(() => df.ApplyPayment(0m));
    }

    [Fact]
    public void ApplyPayment_Successive_Accumulates_Correctly()
    {
        var df = Create(1000m);
        df.ApplyPayment(300m);
        df.ApplyPayment(700m);
        Assert.Equal(FeeStatus.Paid, df.Status);
        Assert.Equal(0m, df.OutstandingAmount);
    }

    // ── ApplyConcession ───────────────────────────────────────────────────────

    [Fact]
    public void ApplyConcession_Full_Clears_Outstanding()
    {
        var df = Create(1000m);
        df.ApplyConcession(1000m);
        Assert.Equal(0m, df.OutstandingAmount);
    }

    [Fact]
    public void ApplyConcession_Partial_Reduces_Outstanding()
    {
        var df = Create(1000m);
        df.ApplyConcession(200m);
        Assert.Equal(800m, df.OutstandingAmount);
        Assert.Equal(200m, df.ConcessionAmount);
        Assert.Equal(200m, df.NetAmount);
        Assert.Equal(FeeStatus.Partial, df.Status);
    }

    [Fact]
    public void ReverseConcession_Restores_Balance_And_Recalculates_Status()
    {
        var df = Create(1000m);
        df.ApplyPayment(600m);
        df.ApplyConcession(400m);

        df.ReverseConcession(250m);

        Assert.Equal(150m, df.ConcessionAmount);
        Assert.Equal(150m, df.NetAmount);
        Assert.Equal(250m, df.OutstandingAmount);
        Assert.Equal(FeeStatus.Partial, df.Status);
    }

    [Fact]
    public void ApplyConcession_On_Top_Of_Payment_Clears_Outstanding()
    {
        var df = Create(1000m);
        df.ApplyPayment(600m);
        df.ApplyConcession(400m);
        Assert.Equal(0m, df.OutstandingAmount);
    }

    // ── ApplyAdjustmentCredit ─────────────────────────────────────────────────

    [Fact]
    public void ApplyAdjustmentCredit_Full_Clears_Outstanding()
    {
        var df = Create(1000m);
        df.ApplyAdjustmentCredit(1000m);
        Assert.Equal(0m, df.OutstandingAmount);
    }

    [Fact]
    public void ApplyAdjustmentCredit_Partial_Reduces_Outstanding()
    {
        var df = Create(1000m);
        df.ApplyAdjustmentCredit(250m);
        Assert.Equal(750m, df.OutstandingAmount);
    }

    // ── ApplyRefund ───────────────────────────────────────────────────────────

    [Fact]
    public void ApplyRefund_Reduces_RefundableBalance()
    {
        var df = Create(1000m);
        df.ApplyPayment(1000m);
        df.ApplyRefund(300m);
        Assert.Equal(700m, df.RefundableBalance);
    }

    [Fact]
    public void ApplyRefund_Full_Amount_Exhausts_RefundableBalance()
    {
        var df = Create(1000m);
        df.ApplyPayment(1000m);
        df.ApplyRefund(1000m);
        Assert.Equal(0m, df.RefundableBalance);
    }

    // ── Negative / guard checks ───────────────────────────────────────────────

    [Fact]
    public void ApplyPayment_Negative_Throws()
    {
        var df = Create();
        Assert.Throws<BusinessException>(() => df.ApplyPayment(-1m));
    }

    [Fact]
    public void ApplyConcession_Negative_Throws()
    {
        var df = Create();
        Assert.Throws<BusinessException>(() => df.ApplyConcession(-0.01m));
    }
}
