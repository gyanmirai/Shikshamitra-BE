using System;
using System.Linq;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionPolicyCalculationService_Tests
{
	private readonly ConcessionPolicyCalculationService _service = new();

	[Fact]
	public void Fixed_Percentage_Is_Calculated_From_Eligible_Outstanding()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.PolicyFixed,
			ConcessionCalculationMethod.Percentage,
			percentageValue: 25m);

		Assert.Equal(250m, _service.Calculate(policy, 1000m));
	}

	[Fact]
	public void Full_Waiver_Equals_Eligible_Outstanding()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.PolicyFixed,
			ConcessionCalculationMethod.FullWaiver);

		Assert.Equal(1000m, _service.Calculate(policy, 1000m));
	}

	[Fact]
	public void Request_Percentage_Above_Configured_Maximum_Is_Rejected()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.RequestEnteredWithinLimits,
			ConcessionCalculationMethod.Percentage,
			maximumPercentage: 20m);

		var exception = Assert.Throws<BusinessException>(() =>
			_service.Calculate(policy, 1000m, DiscountBasis.Percentage, 25m));
		Assert.Equal(ConcessionPolicyErrorCodes.LimitExceeded, exception.Code);
	}

	[Fact]
	public void Request_Amount_Is_Capped_By_Live_Eligible_Outstanding()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.RequestEnteredWithinLimits,
			ConcessionCalculationMethod.FixedAmount,
			maximumFixedAmount: 1000m);

		Assert.Equal(450m, _service.Calculate(policy, 450m, DiscountBasis.FlatAmount, 900m));
	}

	[Fact]
	public void Request_Amount_Above_Configured_Maximum_Is_Rejected()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.RequestEnteredWithinLimits,
			ConcessionCalculationMethod.FixedAmount,
			maximumFixedAmount: 500m);

		var exception = Assert.Throws<BusinessException>(() =>
			_service.Calculate(policy, 1000m, DiscountBasis.FlatAmount, 501m));
		Assert.Equal(ConcessionPolicyErrorCodes.LimitExceeded, exception.Code);
	}

	[Fact]
	public void Aggregated_Request_Fixed_Values_Above_Request_Maximum_Are_Rejected()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.RequestEnteredWithinLimits,
			ConcessionCalculationMethod.FixedAmount,
			maximumFixedAmount: 500m);
		var requestEnteredValues = new[] { 300m, 250m };

		var exception = Assert.Throws<BusinessException>(() =>
			_service.Calculate(
				policy,
				1000m,
				DiscountBasis.FlatAmount,
				requestEnteredValues.Sum()));
		Assert.Equal(ConcessionPolicyErrorCodes.LimitExceeded, exception.Code);
	}

	[Fact]
	public void Request_Basis_Must_Match_The_Policy_Calculation_Method()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.RequestEnteredWithinLimits,
			ConcessionCalculationMethod.Percentage,
			maximumPercentage: 20m);

		var exception = Assert.Throws<BusinessException>(() =>
			_service.Calculate(policy, 1000m, DiscountBasis.FlatAmount, 100m));
		Assert.Equal(ConcessionPolicyErrorCodes.InputNotAllowed, exception.Code);
	}

	[Fact]
	public void Nearest_Rupee_Uses_Away_From_Zero_Rounding()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.PolicyFixed,
			ConcessionCalculationMethod.FixedAmount,
			fixedAmount: 100.5m,
			roundingMethod: RoundingMethodType.NearestRupee);

		Assert.Equal(101m, _service.Calculate(policy, 1000m));
	}

	[Fact]
	public void Round_Up_Cannot_Exceed_Eligible_Outstanding()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.PolicyFixed,
			ConcessionCalculationMethod.Percentage,
			percentageValue: 100m,
			roundingMethod: RoundingMethodType.RoundUp);

		Assert.Equal(10.20m, _service.Calculate(policy, 10.20m));
	}

	[Fact]
	public void Full_Waiver_Preserves_Exact_Minor_Units_Regardless_Of_Rounding()
	{
		var policy = BuildPolicy(
			ConcessionValueSource.PolicyFixed,
			ConcessionCalculationMethod.FullWaiver,
			roundingMethod: RoundingMethodType.RoundUp);

		Assert.Equal(10.20m, _service.Calculate(policy, 10.20m));
	}

	private static ConcessionPolicySnapshot BuildPolicy(
		ConcessionValueSource valueSource,
		ConcessionCalculationMethod calculationMethod,
		decimal? percentageValue = null,
		decimal? fixedAmount = null,
		decimal? maximumPercentage = null,
		decimal? maximumFixedAmount = null,
		RoundingMethodType roundingMethod = RoundingMethodType.None) => new(
		PolicyId: Guid.NewGuid(),
		AcademicYearId: Guid.NewGuid(),
		PolicyName: "Test policy",
		PolicyCode: "TEST",
		Status: ConcessionPolicyStatus.Active,
		IsDefault: false,
		ValueSource: valueSource,
		CalculationMethod: calculationMethod,
		PercentageValue: percentageValue,
		FixedAmount: fixedAmount,
		MaximumPercentage: maximumPercentage,
		MaximumFixedAmount: maximumFixedAmount,
		ApplyOn: ConcessionApplyOn.AllOutstanding,
		RoundingMethod: roundingMethod,
		Version: 1);
}
