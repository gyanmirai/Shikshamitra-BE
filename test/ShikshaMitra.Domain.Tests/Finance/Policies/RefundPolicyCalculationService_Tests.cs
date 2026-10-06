using System;
using System.Collections.Generic;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class RefundPolicyCalculationService_Tests
{
	private readonly RefundPolicyCalculationService _service = new();

	[Fact]
	public void Remaining_Paid_Amount_Subtracts_Processed_Refunds_And_Reservations()
	{
		var result = _service.Calculate(
			BuildPolicy(RefundAmountMode.RemainingPaidAmount),
			new RefundPolicyCalculationInput(1000m, 150m, 100m, 250m, null));

		Assert.Equal(750m, result.RemainingEligible);
		Assert.Equal(750m, result.FinalRefund);
	}

	[Fact]
	public void Percentage_Deduction_And_Both_Caps_Are_Applied_In_Order()
	{
		var policy = BuildPolicy(
			RefundAmountMode.Percentage,
			percentageValue: 80m,
			maximumPerRequest: 600m,
			maximumPerStudentYear: 700m,
			deductionType: RefundDeductionType.Percentage,
			deductionValue: 10m);

		var result = _service.Calculate(
			policy,
			new RefundPolicyCalculationInput(1000m, 0m, 0m, 250m, null));

		Assert.Equal(800m, result.RawRefund);
		Assert.Equal(80m, result.DeductionAmount);
		Assert.Equal(450m, result.FinalRefund);
		Assert.Equal(270m, result.CapAdjustment);
	}

	[Fact]
	public void Refund_Never_Exceeds_Remaining_Settled_Money()
	{
		var result = _service.Calculate(
			BuildPolicy(RefundAmountMode.FixedAmount, fixedAmount: 5000m),
			new RefundPolicyCalculationInput(500m, 100m, 50m, 0m, null));

		Assert.Equal(350m, result.FinalRefund);
	}

	[Fact]
	public void Round_Up_Never_Exceeds_A_Fractional_Eligible_Balance()
	{
		var policy = BuildPolicy(
			RefundAmountMode.RemainingPaidAmount,
			roundingMethod: RoundingMethodType.RoundUp);

		var result = _service.Calculate(
			policy,
			new RefundPolicyCalculationInput(100m, 99.60m, 0m, 0m, null));

		Assert.Equal(0.40m, result.RemainingEligible);
		Assert.Equal(0m, result.FinalRefund);
	}

	[Fact]
	public void Manual_Mode_Requires_A_Positive_Requested_Amount()
	{
		var exception = Assert.Throws<BusinessException>(() => _service.Calculate(
			BuildPolicy(RefundAmountMode.ManualUpToEligible),
			new RefundPolicyCalculationInput(500m, 0m, 0m, 0m, null)));

		Assert.Equal(RefundPolicyErrorCodes.InputNotAllowed, exception.Code);
	}

	[Fact]
	public void Unarchive_Returns_An_Archived_Policy_To_Draft()
	{
		var policy = new RefundPolicy(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			BuildUpdateModel());

		policy.Archive();
		policy.Unarchive();

		Assert.Equal(RefundPolicyStatus.Draft, policy.Status);
		Assert.Equal(3, policy.Version);
	}

	private static RefundPolicySnapshot BuildPolicy(
		RefundAmountMode amountMode,
		decimal? percentageValue = null,
		decimal? fixedAmount = null,
		decimal? maximumPerRequest = null,
		decimal? maximumPerStudentYear = null,
		RefundDeductionType deductionType = RefundDeductionType.None,
		decimal? deductionValue = null,
		RoundingMethodType roundingMethod = RoundingMethodType.None) => new(
		Guid.NewGuid(),
		Guid.NewGuid(),
		"Test refund policy",
		"REF-TEST",
		RefundPolicyStatus.Active,
		false,
		RefundFeeHeadScope.AllFeeHeads,
		[],
		false,
		false,
		true,
		amountMode,
		percentageValue,
		fixedAmount,
		maximumPerRequest,
		maximumPerStudentYear,
		roundingMethod,
		deductionType,
		deductionValue,
		null,
		deductionType == RefundDeductionType.None ? string.Empty : "Administrative deduction",
		[RefundPayoutMethod.BankTransfer],
		7,
		1);

	private static RefundPolicyUpdateModel BuildUpdateModel() => new(
		"Test refund policy",
		"REF-TEST",
		null,
		RefundPolicyStatus.Draft,
		false,
		RefundFeeHeadScope.AllFeeHeads,
		Array.Empty<Guid>(),
		false,
		false,
		true,
		RefundAmountMode.RemainingPaidAmount,
		null,
		null,
		null,
		null,
		RoundingMethodType.None,
		RefundDeductionType.None,
		null,
		null,
		null,
		new List<RefundPayoutMethod> { RefundPayoutMethod.BankTransfer },
		7);
}
