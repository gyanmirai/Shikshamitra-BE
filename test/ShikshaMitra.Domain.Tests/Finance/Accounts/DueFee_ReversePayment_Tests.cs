using System;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>
/// Unit tests for <see cref="DueFee.ReversePayment"/> — covers edge cases that
/// protect financial integrity during reconciliation rejection.
/// </summary>
public class DueFee_ReversePayment_Tests
{
    private static DueFee CreatePaidDueFee(decimal originalAmount = 1000m, decimal paidAmount = 1000m)
    {
        var df = new DueFee(
            id: Guid.NewGuid(),
            tenantId: null,
            studentId: Guid.NewGuid(),
            feeHeadId: Guid.NewGuid(),
            month: MonthEnum.June,
            year: 2026,
            isOneTime: false,
            amount: originalAmount,
            dueDate: new DateTime(2026, 6, 10));

        df.ApplyPayment(paidAmount);
        return df;
    }

    [Fact]
    public void ReversePayment_Full_Amount_Resets_Status_To_Pending()
    {
        var df = CreatePaidDueFee(1000m, 1000m);

        df.ReversePayment(1000m);

        Assert.Equal(0m, df.PaidAmount);
        Assert.Equal(FeeStatus.Pending, df.Status);
    }

    [Fact]
    public void ReversePayment_Partial_Amount_Leaves_PartiallyPaid_Status()
    {
        var df = CreatePaidDueFee(1000m, 1000m);

        df.ReversePayment(400m);

        Assert.Equal(600m, df.PaidAmount);
        Assert.Equal(FeeStatus.Partial, df.Status);
    }

    [Fact]
    public void ReversePayment_Zero_Amount_Throws_BusinessException()
    {
        var df = CreatePaidDueFee(1000m, 1000m);

        var ex = Assert.Throws<BusinessException>(() => df.ReversePayment(0m));
        Assert.Equal(DueFeeErrorCodes.ReverseAmountMustBePositive, ex.Code);
    }

    [Fact]
    public void ReversePayment_Negative_Amount_Throws_BusinessException()
    {
        var df = CreatePaidDueFee(1000m, 1000m);

        var ex = Assert.Throws<BusinessException>(() => df.ReversePayment(-50m));
        Assert.Equal(DueFeeErrorCodes.ReverseAmountMustBePositive, ex.Code);
    }

    [Fact]
    public void ReversePayment_Exceeds_PaidAmount_Throws_BusinessException()
    {
        var df = CreatePaidDueFee(1000m, 500m);

        var ex = Assert.Throws<BusinessException>(() => df.ReversePayment(600m));
        Assert.Equal(DueFeeErrorCodes.ReverseExceedsPaidAmount, ex.Code);
    }

    [Fact]
    public void ReversePayment_OutstandingAmount_Is_Correct_After_Partial_Reversal()
    {
        var df = CreatePaidDueFee(originalAmount: 1000m, paidAmount: 1000m);

        df.ReversePayment(300m);

        Assert.Equal(700m, df.PaidAmount);
        Assert.Equal(300m, df.OutstandingAmount);
    }
}
