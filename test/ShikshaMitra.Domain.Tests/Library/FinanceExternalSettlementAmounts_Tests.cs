using System.Globalization;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class FinanceExternalSettlementAmounts_Tests
{
	[Fact]
	public void A_Partial_Settled_Payment_Should_Preserve_The_Exact_Remaining_Paise()
	{
		var result = FinanceExternalSettlementAmounts.Balance(100.75m, 40.25m, 40.25m, 0m, 0m, 0m, ChargeStatus.Approved);
		Assert.Equal(60.50m, result.Collectible);
		Assert.Equal(60.50m, result.Unsettled);
		Assert.Equal(100.75m, result.Original);
	}

	[Fact]
	public void An_Unverified_Instrument_Should_Reserve_Collection_Without_Claiming_Settlement()
	{
		var result = FinanceExternalSettlementAmounts.Balance(100.75m, 40.25m, 0m, 40.25m, 0m, 0m, ChargeStatus.Approved);
		Assert.Equal(60.50m, result.Collectible);
		Assert.Equal(100.75m, result.Unsettled);
		Assert.Equal(0m, result.Settled);
	}

	[Fact]
	public void A_Partial_Waiver_Reversal_Should_Restore_Only_The_Reversed_Amount()
	{
		var result = FinanceExternalSettlementAmounts.Balance(100.75m, 0m, 0m, 0m, 10.10m, 2.05m, ChargeStatus.Approved);
		Assert.Equal(8.05m, result.NetWaived);
		Assert.Equal(92.70m, result.Collectible);
	}

	[Fact]
	public void Split_Instruments_Should_Sum_Exactly_In_Decimal()
	{
		Assert.Equal(40.25m, FinanceExternalSettlementAmounts.PaymentTotal([20.10m, 20.15m]));
	}

	[Theory]
	[InlineData("0")]
	[InlineData("-1")]
	[InlineData("0.001")]
	public void A_Payment_Should_Reject_Nonpositive_And_Subcent_Amounts(string value)
	{
		var amount = decimal.Parse(value, CultureInfo.InvariantCulture);
		Assert.Throws<BusinessException>(() => FinanceExternalSettlementAmounts.PaymentTotal([amount]));
	}

	[Fact]
	public void Inconsistent_Settlement_Evidence_Should_Require_Review_Instead_Of_Clamping_A_Balance()
	{
		Assert.Throws<BusinessException>(() => FinanceExternalSettlementAmounts.Balance(
			100.75m, 40.25m, 20m, 0m, 0m, 0m, ChargeStatus.Approved));
		Assert.Throws<BusinessException>(() => FinanceExternalSettlementAmounts.Balance(
			100.75m, 90m, 90m, 0m, 20m, 0m, ChargeStatus.Approved));
		Assert.Throws<BusinessException>(() => FinanceExternalSettlementAmounts.Balance(
			100.75m, 0m, 0m, 0m, 1m, 2m, ChargeStatus.Approved));
	}
}
