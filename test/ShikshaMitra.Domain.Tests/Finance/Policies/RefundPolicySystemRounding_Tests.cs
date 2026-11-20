using System;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class RefundPolicySystemRounding_Tests
{
	[Theory]
	[InlineData(RoundingMethodType.None)]
	[InlineData(RoundingMethodType.RoundDown)]
	[InlineData(RoundingMethodType.NearestRupee)]
	[InlineData(RoundingMethodType.RoundUp)]
	public void Compatibility_Rounding_Input_Should_Save_The_Current_Whole_Rupee_Rule(RoundingMethodType input)
	{
		var snapshot = Policy(input).ToSnapshot();

		Assert.Equal(RoundingMethodType.RoundUp, snapshot.RoundingMethod);
		Assert.Equal(50m, snapshot.PercentageValue);
	}

	[Fact]
	public void A_Fractional_Request_Should_Remain_A_Ceiling_For_The_Recorded_Whole_Rupee_Rule()
	{
		var snapshot = Policy(RoundingMethodType.None).ToSnapshot();
		var input = new RefundPolicyCalculationInput(40.25m, 0m, 0m, 0m, 15.25m);

		var result = new RefundPolicyCalculationService().Calculate(snapshot, input);

		Assert.Equal(20.125m, result.RawRefund);
		Assert.Equal(15m, result.FinalRefund);
		Assert.Equal(15.25m, input.RequestedAmount);
		Assert.Equal(40.25m, input.SettledPaidAmount);
		Assert.Equal(0m, result.ActiveReservation);
	}

	[Fact]
	public void Requested_150_Should_Cap_A_600_Maximum_From_1200_Settled_Money()
	{
		var snapshot = Policy(RoundingMethodType.None).ToSnapshot();

		var result = new RefundPolicyCalculationService().Calculate(snapshot, new(1200m, 0m, 0m, 0m, 150m));

		Assert.Equal(600m, result.RawRefund);
		Assert.Equal(150m, result.FinalRefund);
		Assert.Equal(450m, result.CapAdjustment);
	}

	private static RefundPolicy Policy(RoundingMethodType input) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new RefundPolicyUpdateModel(
		"Explicit refund rule", "REFUND-ROUNDING", null, RefundPolicyStatus.Active, false, RefundFeeHeadScope.AllFeeHeads,
		[], true, false, true, RefundAmountMode.Percentage, 50m, null, null, null, input,
		RefundDeductionType.None, null, null, null, [RefundPayoutMethod.Upi], null));
}
