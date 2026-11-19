using System;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Fees;

public class UnpaidFeeTransfer_Tests
{
    private static DueFee Due(decimal amount) => new(
        Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), MonthEnum.June,
        2026, false, amount, new DateTime(2026, 6, 10));

    [Fact]
    public void Moving_unpaid_principal_preserves_total_due_without_creating_paid_credit()
    {
        var source = Due(100m);
        var destination = Due(50m);
        source.ApplyPayment(20m);
        var before = source.OutstandingAmount + destination.OutstandingAmount;

        source.TransferUnpaidOut(30.25m);
        destination.TransferUnpaidIn(30.25m);

        Assert.Equal(before, source.OutstandingAmount + destination.OutstandingAmount);
        Assert.Equal(49.75m, source.OutstandingAmount);
        Assert.Equal(80.25m, destination.OutstandingAmount);
        Assert.Equal(20m, source.PaidAmount);
        Assert.Equal(0m, source.AdjustableBalance);
        Assert.Equal(0m, destination.AdjustmentCreditAmount);
    }

    [Fact]
    public void Source_rejects_more_than_the_current_outstanding_amount()
    {
        var source = Due(100m);
        source.ApplyPayment(20m);

        Assert.Throws<BusinessException>(() => source.TransferUnpaidOut(80.01m));
        Assert.Equal(80m, source.OutstandingAmount);
    }
}
