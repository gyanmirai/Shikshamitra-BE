using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class AdmissionFeeCollectionSettlement_Tests
{
    private static FeeCollectFormDto SettledForm() => new()
    {
        Summary = new() { TotalFee = 1000, PaidFee = 1000, PendingFee = 0 },
        Ledger = [new() { Items = [new() { Total = 1000, Paid = 1000, Due = 0 }] }],
    };

    [Fact]
    public void Fully_settled_ledger_is_read_only() =>
        AdmissionFeeCollectionAppService.IsCollectionSettled(SettledForm()).ShouldBeTrue();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Outstanding_or_reopened_due_is_not_settled(bool futureInstallment)
    {
        var form = SettledForm();
        form.Ledger.Add(new() { Year = futureInstallment ? 2027 : 2026, Items = [new() { Due = 100 }] });
        AdmissionFeeCollectionAppService.IsCollectionSettled(form).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Charge")]
    [InlineData("LateFee")]
    public void Paid_tuition_does_not_hide_outstanding_charges(string type)
    {
        var form = SettledForm();
        form.Charges.Add(new() { SourceType = type, PendingAmount = 50 });
        AdmissionFeeCollectionAppService.IsCollectionSettled(form).ShouldBeFalse();
    }

    [Theory]
    [InlineData("fee")]
    [InlineData("charge")]
    [InlineData("summary")]
    public void Money_awaiting_reconciliation_is_not_settled(string location)
    {
        var form = SettledForm();
        if (location == "fee") form.Ledger[0].Items[0].ReconciliationAmount = 100;
        if (location == "charge") form.Charges.Add(new() { ReconciliationAmount = 100m });
        if (location == "summary") form.Summary.ReconciliationAmount = 100;
        AdmissionFeeCollectionAppService.IsCollectionSettled(form).ShouldBeFalse();
    }

    [Fact]
    public void Missing_or_unprepared_ledger_is_not_zero_balance()
    {
        AdmissionFeeCollectionAppService.IsCollectionSettled(new()).ShouldBeFalse();
        AdmissionFeeCollectionAppService.IsCollectionSettled(new() { Summary = new() }).ShouldBeFalse();
    }
}
