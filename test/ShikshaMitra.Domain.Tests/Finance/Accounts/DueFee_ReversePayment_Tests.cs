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
    private static DueFee CreatePaidDueFee(double originalAmount = 1000d, double paidAmount = 1000d)
    {
        var df = new DueFee(
            id: Guid.NewGuid(),
            tenantId: null,
            studentId: Guid.NewGuid(),
            feeHeadId: Guid.NewGuid(),
            month: MonthEnum.June,
            year: 2026,
            isOneTime: false,
            amount: originalAmount);

        df.ApplyPayment(paidAmount);
        return df;
    }

    [Fact]
    public void ReversePayment_Full_Amount_Resets_Status_To_Pending()
    {
        var df = CreatePaidDueFee(1000d, 1000d);

        df.ReversePayment(1000d);

        Assert.Equal(0d, df.PaidAmount);
        Assert.Equal(FeeStatus.Pending, df.Status);
    }

    [Fact]
    public void ReversePayment_Partial_Amount_Leaves_PartiallyPaid_Status()
    {
        var df = CreatePaidDueFee(1000d, 1000d);

        df.ReversePayment(400d);

        Assert.Equal(600d, df.PaidAmount);
        Assert.Equal(FeeStatus.Partial, df.Status);
    }

    [Fact]
    public void ReversePayment_Zero_Amount_Throws_BusinessException()
    {
        var df = CreatePaidDueFee(1000d, 1000d);

        var ex = Assert.Throws<BusinessException>(() => df.ReversePayment(0d));
        Assert.Equal(DueFeeErrorCodes.ReverseAmountMustBePositive, ex.Code);
    }

    [Fact]
    public void ReversePayment_Negative_Amount_Throws_BusinessException()
    {
        var df = CreatePaidDueFee(1000d, 1000d);

        var ex = Assert.Throws<BusinessException>(() => df.ReversePayment(-50d));
        Assert.Equal(DueFeeErrorCodes.ReverseAmountMustBePositive, ex.Code);
    }

    [Fact]
    public void ReversePayment_Exceeds_PaidAmount_Throws_BusinessException()
    {
        var df = CreatePaidDueFee(1000d, 500d);

        var ex = Assert.Throws<BusinessException>(() => df.ReversePayment(600d));
        Assert.Equal(DueFeeErrorCodes.ReverseExceedsPaidAmount, ex.Code);
    }

    [Fact]
    public void ReversePayment_OutstandingAmount_Is_Correct_After_Partial_Reversal()
    {
        var df = CreatePaidDueFee(originalAmount: 1000d, paidAmount: 1000d);

        df.ReversePayment(300d);

        Assert.Equal(700d, df.PaidAmount, precision: 2);
        Assert.Equal(300d, df.OutstandingAmount, precision: 2);
    }
}
